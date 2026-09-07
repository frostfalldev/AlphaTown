#!/usr/bin/env bash
#
# Clone to APK in one command.
#
#   ./tools/build-android.sh              development APK (keeps Log.Info — use this to test)
#   ./tools/build-android.sh --release    release APK
#   ./tools/build-android.sh --install    build, then adb install -r onto the attached device
#
# Needs a Unity 6.3 LTS install with the Android Build Support module. Point UNITY at the editor
# binary if it is not in one of the usual places:
#
#   UNITY=/path/to/Unity ./tools/build-android.sh

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT="Builds/Android/AlphaTown.apk"
EXTRA_ARGS=()
INSTALL=0

for arg in "$@"; do
  case "$arg" in
    --release) EXTRA_ARGS+=("-alphatownRelease") ;;
    --skip-setup) EXTRA_ARGS+=("-alphatownSkipSetup") ;;
    --install) INSTALL=1 ;;
    -h|--help) sed -n '2,14p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

# The project must have been created from the Universal 3D template first — see docs/SETUP.md.
if [[ ! -d "$PROJECT_ROOT/ProjectSettings" ]]; then
  echo "No ProjectSettings/ in $PROJECT_ROOT." >&2
  echo "This repo is the source tree; the Unity project is created on your machine." >&2
  echo "Follow docs/SETUP.md first." >&2
  exit 1
fi

find_unity() {
  if [[ -n "${UNITY:-}" ]]; then echo "$UNITY"; return; fi

  # Newest first, so a machine with several editors installed picks the current one.
  local candidates=()
  case "$(uname -s)" in
    Darwin) candidates=(/Applications/Unity/Hub/Editor/*/Unity.app/Contents/MacOS/Unity) ;;
    Linux)  candidates=("$HOME"/Unity/Hub/Editor/*/Editor/Unity) ;;
    *)      candidates=("/c/Program Files/Unity/Hub/Editor"/*/Editor/Unity.exe) ;;
  esac

  local newest=""
  for candidate in "${candidates[@]}"; do
    [[ -x "$candidate" ]] && newest="$candidate"
  done

  echo "$newest"
}

UNITY_BIN="$(find_unity)"
if [[ -z "$UNITY_BIN" || ! -x "$UNITY_BIN" ]]; then
  echo "Could not find a Unity editor. Set UNITY to the editor binary:" >&2
  echo "  UNITY=/Applications/Unity/Hub/Editor/6000.3.0f1/Unity.app/Contents/MacOS/Unity $0" >&2
  exit 1
fi

LOG="$PROJECT_ROOT/Builds/unity-build.log"
mkdir -p "$(dirname "$LOG")"

echo "Unity:  $UNITY_BIN"
echo "Output: $OUTPUT"
echo "Log:    $LOG"
echo

# -nographics is deliberately omitted: the build generates a scene and renders UI Toolkit panels,
# and a headless GPU context has been known to trip that up. -batchmode alone is enough for CI.
set +e
"$UNITY_BIN" \
  -batchmode \
  -quit \
  -projectPath "$PROJECT_ROOT" \
  -logFile "$LOG" \
  -executeMethod AlphaTown.EditorTools.Build.AndroidBuilder.BuildFromCommandLine \
  -alphatownOutput "$OUTPUT" \
  "${EXTRA_ARGS[@]}"
STATUS=$?
set -e

if [[ $STATUS -ne 0 ]]; then
  echo >&2
  echo "Build failed (exit $STATUS). Last errors from the log:" >&2
  grep -nE "error CS|\[AlphaTown\]|BuildFailedException|Aborting" "$LOG" | tail -40 >&2 || true
  echo >&2
  echo "Full log: $LOG" >&2
  exit $STATUS
fi

echo "Built $PROJECT_ROOT/$OUTPUT"

if [[ $INSTALL -eq 1 ]]; then
  echo "Installing..."
  adb install -r "$PROJECT_ROOT/$OUTPUT"
  echo "Watch the game's own logs with:"
  echo "  adb logcat -s Unity | grep AlphaTown"
fi
