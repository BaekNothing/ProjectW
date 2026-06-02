#!/usr/bin/env sh
set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
git config core.hooksPath .githooks
chmod +x .githooks/pre-commit .githooks/post-commit 2>/dev/null || true
echo "Installed git hooks: core.hooksPath=.githooks"
echo "  pre-commit  -> sync architecture doc fingerprint (index)"
echo "  post-commit -> finalize commit SHA in architecture doc"
