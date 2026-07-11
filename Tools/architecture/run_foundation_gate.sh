#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  Tools/architecture/run_foundation_gate.sh /path/to/Unity [project-root] [--skip-playmode]

Runs the static architecture guard, EditMode tests, GameRoot installer, a second
EditMode pass, and (unless skipped) PlayMode tests. The installer intentionally
changes Gameplay.unity and may create GameDatabase.asset; review that diff before committing.
USAGE
}

if [[ $# -lt 1 ]]; then
  usage
  exit 2
fi

UNITY="$1"
PROJECT="${2:-$(pwd)}"
SKIP_PLAYMODE="${3:-}"
PROJECT="$(cd "$PROJECT" && pwd)"
LOG_DIR="$PROJECT/Logs/ArchitectureValidation"
mkdir -p "$LOG_DIR"

if [[ ! -x "$UNITY" ]]; then
  echo "Unity executable is not executable: $UNITY" >&2
  exit 2
fi

cd "$PROJECT"
python3 Tools/architecture/guard.py --project-root .
python3 Tools/architecture/audit.py --project-root . --output Docs/Architecture/Generated
python3 Tools/architecture/serialized_refs.py --project-root . --output Docs/Architecture/Generated/serialized_script_refs.csv

run_tests() {
  local platform="$1"
  local label="$2"
  "$UNITY" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT" \
    -runTests \
    -testPlatform "$platform" \
    -testResults "$LOG_DIR/${label}.xml" \
    -logFile "$LOG_DIR/${label}.log"
}

run_tests EditMode editmode-before-root

"$UNITY" \
  -batchmode \
  -nographics \
  -quit \
  -projectPath "$PROJECT" \
  -executeMethod Chateau.Editor.Architecture.GameRootInstaller.InstallGameplaySceneBatch \
  -logFile "$LOG_DIR/game-root-install.log"

run_tests EditMode editmode-after-root

if [[ "$SKIP_PLAYMODE" != "--skip-playmode" ]]; then
  run_tests PlayMode playmode-after-root
fi

python3 Tools/architecture/guard.py --project-root .
git diff --check 2>/dev/null || true

cat <<EOF2
Foundation gate completed.

Review before committing:
  git status --short
  git diff -- Assets/Scenes/Gameplay.unity Assets/_Chateau/Data

Evidence:
  $LOG_DIR
EOF2
