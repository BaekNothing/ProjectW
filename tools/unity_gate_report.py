#!/usr/bin/env python3
"""
Unity Test Runner XML -> Prototype Gate report.

Usage:
  python3 tools/unity_gate_report.py --results TestResults.xml --output GateChecklist.md
"""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

GATE_SPEC_PATH = "Assets/Specification/Ingame/CoreLoop/06 – Prototype Gate.md"

MANDATORY_TESTS = {
    "T01": {
        "method": "ProjectW.Tests.EditMode.RoutineObservationMvpSessionTests.T01_ThreeConsecutiveCycles_EnforcesRequiredCoreLoopOrder",
        "spec": "Assets/Specification/Ingame/CoreLoop/02 – State Machine.md",
    },
    "T02": {
        "method": "ProjectW.Tests.EditMode.IngameCoreInterventionQueueTests.T02_InterventionApplyTiming_AppliesFromNextTickOnly",
        "spec": "Assets/Specification/Ingame/CoreLoop/03 – Intervention Boundary.md",
    },
    "T03": {
        "method": "ProjectW.Tests.EditMode.IngameCoreSessionEndPersistenceTests.T03_ObjectiveCompleteEnd_WhenNoHigherPriorityReason",
        "spec": "Assets/Specification/Ingame/CoreLoop/05 – Session End and Persistence.md",
    },
    "T10": {
        "method": "ProjectW.Tests.EditMode.RoutineObservationMvpSessionTests.T10_PauseResume_MaintainsTickIndexContinuity",
        "spec": "Assets/Specification/Ingame/CoreLoop/01 – Tick and Timebase.md",
    },
    "T11": {
        "method": "ProjectW.Tests.EditMode.IngameCoreInterventionQueueTests.T11_ConflictingInterventions_UsesTieBreakOrderConsistently",
        "spec": "Assets/Specification/Ingame/CoreLoop/03 – Intervention Boundary.md",
    },
    "T21": {
        "method": "ProjectW.Tests.EditMode.IngameCoreSessionEndPersistenceTests.T21_PersistRetry_TransitionsToRetryThenSuccess",
        "spec": "Assets/Specification/Ingame/CoreLoop/05 – Session End and Persistence.md",
    },
    "T22": {
        "method": "ProjectW.Tests.EditMode.IngameCoreSimulationTests.T22_InvalidTransitionReject_StateMachineRejectsForbiddenTransition",
        "spec": "Assets/Specification/Ingame/CoreLoop/02 – State Machine.md",
    },
    "T23": {
        "method": "ProjectW.Tests.EditMode.IngameCoreSimulationTests.T23_DeterministicReplay_LogCoreFieldsMatchForSameSeedAndInput",
        "spec": "Assets/Specification/Ingame/CoreLoop/10 – Observability and Replay.md",
    },
}

PASS_RESULTS = {"Passed", "Success"}
FAIL_RESULTS = {"Failed", "Failure", "Error", "Inconclusive"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Prototype Gate report from Unity Test Runner XML.")
    parser.add_argument("--results", required=True, type=Path, help="Path to Unity Test Runner XML file.")
    parser.add_argument("--output", required=True, type=Path, help="Path to output report file (markdown).")
    parser.add_argument("--json-output", type=Path, help="Optional JSON summary path.")
    return parser.parse_args()


def normalize_outcome(node: ET.Element) -> str:
    for key in ("result", "outcome", "status", "label"):
        value = node.attrib.get(key)
        if value:
            return value
    return "Unknown"


def is_passed(outcome: str) -> bool:
    return outcome in PASS_RESULTS


def is_failed(outcome: str) -> bool:
    return outcome in FAIL_RESULTS or outcome not in PASS_RESULTS


def parse_results(xml_path: Path) -> dict[str, dict[str, str]]:
    tree = ET.parse(xml_path)
    root = tree.getroot()

    found = {}
    for case in root.findall(".//test-case"):
        fullname = case.attrib.get("fullname") or case.attrib.get("name") or ""
        outcome = normalize_outcome(case)
        found[fullname] = {"outcome": outcome}

    return found


def evaluate_gate(found_cases: dict[str, dict[str, str]]) -> dict[str, object]:
    test_results = {}
    failed_ids = []

    for test_id, meta in MANDATORY_TESTS.items():
        method = meta["method"]
        case = found_cases.get(method)
        if case is None:
            outcome = "Missing"
            status = "FAIL"
        else:
            outcome = case["outcome"]
            status = "PASS" if is_passed(outcome) else "FAIL"

        if status == "FAIL":
            failed_ids.append(test_id)

        test_results[test_id] = {
            "status": status,
            "outcome": outcome,
            "method": method,
            "spec": meta["spec"],
        }

    return {
        "gate_status": "PASS" if not failed_ids else "FAIL",
        "failed_ids": failed_ids,
        "gate_spec": GATE_SPEC_PATH,
        "tests": test_results,
    }


def render_markdown(summary: dict[str, object]) -> str:
    gate_status = summary["gate_status"]
    failed_ids = summary["failed_ids"]
    tests = summary["tests"]

    lines = [
        "# Prototype Gate Checklist Report",
        "",
        f"- Gate Spec: `{summary['gate_spec']}`",
        f"- Overall Gate Result: **{gate_status}**",
        "",
    ]

    if gate_status == "PASS":
        lines.append("- Mandatory gate tests are all PASS.")
    else:
        lines.append("- Mandatory gate tests include failures.")
        lines.append(f"- Failed Test IDs: `{', '.join(failed_ids)}`")
    lines.append("")

    lines.extend(
        [
            "| Test ID | PASS/FAIL | Runner Outcome | Method | Related Spec |",
            "|---|---|---|---|---|",
        ]
    )

    for test_id in sorted(tests.keys()):
        row = tests[test_id]
        lines.append(
            f"| {test_id} | {row['status']} | {row['outcome']} | `{row['method']}` | `{row['spec']}` |"
        )

    lines.append("")
    lines.append("## Gate Rule")
    lines.append("- 필수 테스트 하나라도 실패(또는 누락)하면 Gate는 FAIL.")
    return "\n".join(lines) + "\n"


def main() -> int:
    args = parse_args()
    if not args.results.exists():
        print(f"[gate-report] Results file not found: {args.results}", file=sys.stderr)
        return 2

    found_cases = parse_results(args.results)
    summary = evaluate_gate(found_cases)
    markdown = render_markdown(summary)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(markdown, encoding="utf-8")

    if args.json_output:
        args.json_output.parent.mkdir(parents=True, exist_ok=True)
        args.json_output.write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"Gate {summary['gate_status']}")
    if summary["failed_ids"]:
        print("Failed IDs:", ", ".join(summary["failed_ids"]))
    print(f"Report: {args.output}")
    return 0 if summary["gate_status"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
