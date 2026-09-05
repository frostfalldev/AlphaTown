#!/usr/bin/env bash
# Compiles every AlphaTown assembly and runs the EditMode suite without a Unity Editor.
# See README.md in this directory for what that does and does not prove.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
SRC="${ALPHATOWN_SRC:-$ROOT/Assets/AlphaTown}"
STAGE="$HERE/stage"
OUT="$HERE/out"

find_nunit() {
  if [[ -n "${NUNIT_DLL:-}" ]]; then echo "$NUNIT_DLL"; return; fi
  for candidate in \
    /usr/lib/cli/nunit.framework-2.6.3/nunit.framework.dll \
    /usr/lib/mono/gac/nunit.framework/*/nunit.framework.dll; do
    [[ -f "$candidate" ]] && { echo "$candidate"; return; }
  done
}

NUNIT="$(find_nunit)"

if ! command -v mcs >/dev/null || [[ -z "$NUNIT" ]]; then
  echo "Needs Mono and NUnit:" >&2
  echo "  sudo apt-get install -y mono-mcs libnunit-framework2.6.3-cil" >&2
  exit 1
fi
DEFINES="-d:UNITY_EDITOR -d:UNITY_INCLUDE_TESTS -d:DEVELOPMENT_BUILD -d:ENABLE_LEGACY_INPUT_MANAGER"

rm -rf "$STAGE" "$OUT"
mkdir -p "$STAGE" "$OUT"
cp -r "$SRC/Code" "$SRC/Tests" "$STAGE/"

# mcs 6.8 predates C# 7 digit separators, which Unity's Roslyn compiler handles. Stripping them
# is semantics-preserving, and keeps the repo's literals readable rather than bending source to
# suit the harness.
python3 - "$STAGE" <<'PY'
import re, sys, pathlib
root = pathlib.Path(sys.argv[1])
pattern = re.compile(r'(?<![\w.])(\d[\d_]*\d)(?=[LlFfDdUuMm]?\b)')

# The distro's NUnit is 2.6; the project targets the NUnit 3 that ships with Unity. These two
# rewrites are exact API equivalents, not weakened assertions:
#   Is.Zero              == Is.EqualTo(0)
#   Does.Not.Contain(x)  == Has.No.Member(x)   (both mean "collection lacks this element")
#   Does.Contain(s)      == Is.StringContaining(s)  (on a string; obsolete in 3.x, present in 2.6)
nunit3 = [
    (re.compile(r'\bIs\.Zero\b'), 'Is.EqualTo(0)'),
    (re.compile(r'\bDoes\.Not\.Contain\b'), 'Has.No.Member'),
    (re.compile(r'\bDoes\.Contain\b'), 'Is.StringContaining'),
]

changed = 0
bridged = 0
for path in root.rglob('*.cs'):
    text = path.read_text()
    fixed = pattern.sub(lambda m: m.group(1).replace('_', ''), text)
    if fixed != text:
        changed += 1

    before = fixed
    for expression, replacement in nunit3:
        fixed = expression.sub(replacement, fixed)
    if fixed != before:
        bridged += 1

    if fixed != text:
        path.write_text(fixed)

if changed:
    print(f"  (normalised digit separators in {changed} file(s))")
if bridged:
    print(f"  (bridged NUnit 3 constraints in {bridged} file(s))")
PY

compile() {
  local name="$1"; shift
  local refs=(); local sources=()
  local collecting_refs=1
  for arg in "$@"; do
    if [[ "$arg" == "--" ]]; then collecting_refs=0; continue; fi
    if [[ $collecting_refs -eq 1 ]]; then refs+=("-r:$OUT/$arg"); else sources+=("$arg"); fi
  done

  local files=()
  for dir in "${sources[@]}"; do
    while IFS= read -r file; do files+=("$file"); done < <(find "$STAGE/$dir" -name '*.cs')
  done

  echo "  $name (${#files[@]} files)"
  mcs -target:library -out:"$OUT/$name.dll" "${refs[@]}" $DEFINES -langversion:latest \
      -warnaserror+:CS0108,CS0114,CS0162,CS0164,CS0168,CS0169,CS0219,CS0414,CS0429,CS0649 \
      "${files[@]}"
}

echo "Compiling..."
mcs -target:library -out:"$OUT/UnityEngine.dll" -langversion:latest "$HERE"/shim/*.cs

compile AlphaTown.Core       UnityEngine.dll -- Code/Core
compile AlphaTown.Data       UnityEngine.dll AlphaTown.Core.dll -- Code/Data
compile AlphaTown.Services   UnityEngine.dll AlphaTown.Core.dll AlphaTown.Data.dll -- Code/Services
compile AlphaTown.Gameplay   UnityEngine.dll AlphaTown.Core.dll AlphaTown.Data.dll AlphaTown.Services.dll -- Code/Gameplay

# The Input System companion assembly is skipped: its define constraints exclude it whenever the
# legacy backend is present, which is the configuration this harness compiles.
rm -rf "$STAGE/Code/UI/InputSystemSupport"
compile AlphaTown.UI          UnityEngine.dll AlphaTown.Core.dll AlphaTown.Data.dll AlphaTown.Services.dll AlphaTown.Gameplay.dll -- Code/UI

compile AlphaTown.Editor      UnityEngine.dll AlphaTown.Core.dll AlphaTown.Data.dll AlphaTown.Services.dll AlphaTown.Gameplay.dll AlphaTown.UI.dll -- Code/Editor

echo "  AlphaTown.Tests.EditMode"
mcs -target:library -out:"$OUT/AlphaTown.Tests.EditMode.dll" \
    -r:"$OUT/UnityEngine.dll" -r:"$OUT/AlphaTown.Core.dll" -r:"$OUT/AlphaTown.Data.dll" \
    -r:"$OUT/AlphaTown.Services.dll" -r:"$OUT/AlphaTown.Gameplay.dll" -r:"$OUT/AlphaTown.UI.dll" -r:"$NUNIT" \
    $DEFINES -langversion:latest \
    $(find "$STAGE/Tests/EditMode" -name '*.cs')

echo "  runner"
mcs -target:exe -out:"$OUT/Runner.exe" -r:"$OUT/UnityEngine.dll" -r:"$NUNIT" "$HERE/runner/Runner.cs"

cp "$NUNIT" "$OUT/"

echo
echo "Running tests..."
cd "$OUT"
exec mono Runner.exe AlphaTown.Tests.EditMode.dll "$@"
