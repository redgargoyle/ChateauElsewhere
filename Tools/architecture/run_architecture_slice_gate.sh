#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  run_architecture_slice_gate.sh UNITY PROJECT TEST_FILTER MIN_TESTS [MAX_FAILED]

Runs repository checks, Unity compilation, a focused Unity test run, and verifies
that the result XML exists and contains a real test run. Do not run while the
same project is open in another Unity editor.
EOF
}

[[ $# -ge 4 ]] || { usage; exit 2; }
UNITY="$1"
PROJECT="$(cd "$2" && pwd)"
FILTER="$3"
MIN_TESTS="$4"
MAX_FAILED="${5:-0}"
STAMP="$(date +%Y%m%d-%H%M%S)"
LOG_DIR="$PROJECT/Logs/ArchitectureMigration/$STAMP"
mkdir -p "$LOG_DIR"

[[ -x "$UNITY" ]] || { echo "Unity not executable: $UNITY" >&2; exit 2; }
cd "$PROJECT"
[[ -z "$(git status --porcelain)" ]] || {
  echo "Working tree must be clean before beginning a slice gate." >&2
  git status --short
  exit 2
}

python3 Tools/architecture/guard.py --project-root .
python3 Tools/architecture/audit.py --project-root . --output Docs/Architecture/Generated
python3 Tools/architecture/serialized_refs.py --project-root . --output Docs/Architecture/Generated/serialized_script_refs.csv
python3 Tools/architecture/scan_unity_script_integrity.py --project-root .
python3 Tools/architecture/validate_runtime_ledger.py --project-root .
git diff --check

# Compile/import gate. -quit is valid here because this is not a test run.
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
  -logFile "$LOG_DIR/compile.log"

RESULT="$LOG_DIR/focused.xml"
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode -testFilter "$FILTER" \
  -testResults "$RESULT" -logFile "$LOG_DIR/focused.log"

python3 Tools/architecture/verify_nunit_xml.py "$RESULT" \
  --minimum-total "$MIN_TESTS" --maximum-failed "$MAX_FAILED"

python3 Tools/architecture/guard.py --project-root .
python3 Tools/architecture/scan_unity_script_integrity.py --project-root .
git diff --check

echo "Slice gate passed. Evidence: $LOG_DIR"
