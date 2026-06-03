#!/usr/bin/env python3
"""
Architecture doc ↔ Git tracking sync.

Updates the machine-readable block in:
  Assets/Specification/Architecture – ProjectW System Overview.md

Usage:
  python tools/sync_architecture_doc.py           # write sync metadata
  python tools/sync_architecture_doc.py --check   # exit 1 if stale (CI)
  python tools/sync_architecture_doc.py --finalize  # set commit SHA to HEAD (post-commit)
"""

from __future__ import annotations

import argparse
import hashlib
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
ARCH_DOC = REPO_ROOT / "Assets/Specification/Architecture – ProjectW System Overview.md"
MARKER_BEGIN = "<!-- arch-sync:begin -->"
MARKER_END = "<!-- arch-sync:end -->"

TRACK_ROOTS = (
    "Assets/Specification",
    "Assets/Scripts",
    "Assets/Tests",
    "Assets/Editor",
    "Assets/Resources/CaseReviewData",
)


def run_git(*args: str) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or f"git {' '.join(args)} failed")
    return result.stdout.strip()


def tracked_index_entries() -> list[str]:
    lines: list[str] = []
    arch_doc_rel = ARCH_DOC.relative_to(REPO_ROOT).as_posix()
    for root in TRACK_ROOTS:
        output = run_git("-c", "core.quotePath=false", "ls-files", "-s", "--", root)
        if not output:
            continue
        for line in output.splitlines():
            parts = line.split("\t", 1)
            if len(parts) != 2:
                continue
            meta, path = parts
            if path == arch_doc_rel:
                continue
            blob = meta.split()[1]
            lines.append(f"{path}\t{blob}")
    return sorted(set(lines))


def content_fingerprint() -> str:
    payload = "\n".join(tracked_index_entries())
    digest = hashlib.sha256(payload.encode("utf-8")).hexdigest()
    return f"sha256:{digest}"


def head_sha() -> str:
    return run_git("rev-parse", "HEAD")


def short_sha(full: str) -> str:
    return full[:7]


def current_branch() -> str:
    branch = run_git("rev-parse", "--abbrev-ref", "HEAD")
    return branch if branch != "HEAD" else "(detached)"


def read_sync_block(doc_text: str) -> str | None:
    match = re.search(
        re.escape(MARKER_BEGIN) + r"(.*?)" + re.escape(MARKER_END),
        doc_text,
        flags=re.DOTALL,
    )
    return match.group(1) if match else None


def parse_field(block: str, label: str) -> str | None:
    pattern = rf"\|\s*\*\*{re.escape(label)}\*\*\s*\|\s*`([^`]*)`\s*\|"
    match = re.search(pattern, block)
    return match.group(1) if match else None


def build_sync_block(
    *,
    commit_full: str,
    commit_short: str,
    branch: str,
    fingerprint: str,
    synced_at_utc: str,
    mode: str,
) -> str:
    paths_cell = "<br>".join(f"`{p}/`" for p in TRACK_ROOTS)
    return f"""
| 항목 | 값 |
|------|-----|
| **동기화 모드** | `{mode}` |
| **동기화 시각 (UTC)** | `{synced_at_utc}` |
| **기준 커밋 (전체 SHA)** | `{commit_full}` |
| **기준 커밋 (단축)** | `{commit_short}` |
| **브랜치** | `{branch}` |
| **추적 경로 지문** | `{fingerprint}` |
| **추적 경로** | {paths_cell} |

> 지문은 Git 인덱스(`git ls-files -s`)에 등록된 추적 경로 파일 목록·blob 해시의 SHA-256이다.  
> 자기참조를 피하기 위해 본 Architecture 문서 자체는 지문 계산에서 제외한다.
> `pre-commit` 훅 또는 `python tools/sync_architecture_doc.py` 실행 시 갱신된다. CI는 `--check`로 불일치를 검출한다.
"""


def replace_sync_block(doc_text: str, inner: str) -> str:
    wrapped = f"{MARKER_BEGIN}{inner}{MARKER_END}"
    if MARKER_BEGIN in doc_text and MARKER_END in doc_text:
        return re.sub(
            re.escape(MARKER_BEGIN) + r".*?" + re.escape(MARKER_END),
            wrapped,
            doc_text,
            count=1,
            flags=re.DOTALL,
        )
    raise RuntimeError(f"Markers not found in {ARCH_DOC}")


def write_doc(doc_text: str) -> None:
    ARCH_DOC.write_text(doc_text, encoding="utf-8", newline="\n")


def sync(*, finalize: bool) -> int:
    if not ARCH_DOC.is_file():
        print(f"error: missing {ARCH_DOC}", file=sys.stderr)
        return 1

    doc_text = ARCH_DOC.read_text(encoding="utf-8")
    existing = read_sync_block(doc_text)
    fp = content_fingerprint()
    branch = current_branch()
    utc = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    if finalize:
        commit = head_sha()
        mode = "finalize (post-commit)"
    else:
        commit = head_sha()
        mode = "index (pre-commit / manual)"

    inner = build_sync_block(
        commit_full=commit,
        commit_short=short_sha(commit),
        branch=branch,
        fingerprint=fp,
        synced_at_utc=utc,
        mode=mode,
    )
    updated = replace_sync_block(doc_text, inner)
    if updated != doc_text:
        write_doc(updated)
        rel = str(ARCH_DOC.relative_to(REPO_ROOT)).encode("ascii", "backslashreplace").decode("ascii")
        print(f"updated: {rel}")
    else:
        print("architecture doc sync block already up to date")
    return 0


def check() -> int:
    if not ARCH_DOC.is_file():
        print(f"error: missing {ARCH_DOC}", file=sys.stderr)
        return 1

    doc_text = ARCH_DOC.read_text(encoding="utf-8")
    block = read_sync_block(doc_text)
    if block is None:
        print("error: arch-sync markers missing", file=sys.stderr)
        return 1

    doc_fp = parse_field(block, "추적 경로 지문")
    current_fp = content_fingerprint()
    if doc_fp != current_fp:
        print("architecture doc is STALE (fingerprint mismatch)", file=sys.stderr)
        print(f"  doc:     {doc_fp}", file=sys.stderr)
        print(f"  current: {current_fp}", file=sys.stderr)
        print("  run: python tools/sync_architecture_doc.py", file=sys.stderr)
        return 1

    doc_sha = parse_field(block, "기준 커밋 (전체 SHA)")
    head = head_sha()
    if doc_sha != head:
        print(
            "warning: doc commit SHA differs from HEAD "
            f"(doc={doc_sha}, HEAD={head}); fingerprint OK",
            file=sys.stderr,
        )

    print("architecture doc sync metadata is current")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Sync Architecture doc Git metadata.")
    parser.add_argument("--check", action="store_true", help="Verify doc matches index fingerprint.")
    parser.add_argument(
        "--finalize",
        action="store_true",
        help="Refresh commit SHA to HEAD after commit (post-commit hook).",
    )
    args = parser.parse_args()

    if args.check:
        return check()
    return sync(finalize=args.finalize)


if __name__ == "__main__":
    sys.exit(main())
