# Headless verification

Compiles every AlphaTown assembly and runs the whole EditMode suite **without a Unity Editor**.

```bash
sudo apt-get install -y mono-mcs libnunit-framework2.6.3-cil   # once
./tools/headless/run.sh                                        # ~10 seconds
./tools/headless/run.sh Producer                               # only fixtures/tests matching "Producer"
```

## Why this exists

The simulation was written, reviewed and shipped to a device before a single line of it had ever
been compiled, and before one of its 217 tests had ever run. Unity is normally the only thing that
can do either, and Unity needs a licence, a workstation and several minutes. That gap is where the
expensive bugs live: the input backend failure that killed pan, zoom and swipe on the first APK
compiled perfectly and failed only at runtime, on a phone, in someone's hand.

This harness closes most of that gap in ten seconds, for free, on any machine.

## How it works

`shim/` contains hand-written stand-ins for the ~90 `UnityEngine` and `UnityEditor` members this
project actually touches — `Debug`, `Mathf`, `Vector2/3`, `ScriptableObject`, `JsonUtility`, the UI
Toolkit element and style types, `SerializedObject`, `AssetDatabase`, `PlayerSettings` and so on.
The project's own code is compiled against those, one assembly at a time, in dependency order.

`runner/Runner.cs` is a small NUnit-attribute runner. Assertions come from the real NUnit assembly;
the runner only handles discovery, fixture lifecycle, and Unity's `LogAssert` rules, which no stock
NUnit runner knows about.

Sources are staged into `stage/` and lightly normalised first — Mono's compiler predates C# 7 digit
separators, and the distro's NUnit is 2.6 where the project targets the 3.x that ships with Unity.
Both rewrites are exact equivalents (`Is.Zero` → `Is.EqualTo(0)`, `Does.Not.Contain(x)` →
`Has.No.Member(x)`), applied to the copy so the repo's own source stays idiomatic.

## What a green run proves

- **Every assembly type-checks.** Core, Data, Services, Gameplay, UI and Editor, all six.
- **The layering holds.** Each assembly is compiled separately against only the ones below it, so
  an upward reference is a compile error here exactly as it is in Unity.
- **The simulation is correct**, to the extent 217 tests say so: production chains, offline
  progression, the wallet and its ledger, XP and unlocks, order generation and expiry, slot pacing,
  grid placement, construction and upgrades, land purchase, clock synchronisation and tampering,
  the save round trip through the real serializer, and the command layer the UI drives.
- **Error paths stay honest.** Unity fails a test that writes an unexpected `Debug.LogError`; so
  does this. That rule alone caught a test passing for the wrong reason.

## What a green run does **not** prove

Read this part before trusting it.

- **The shims are approximations written from the API surface, not Unity's real assemblies.** A
  signature that differs from Unity's would compile here and fail there. Green here is evidence
  that the Unity build is green — it is not proof.
- **Nothing renders, nothing is a scene, nothing is serialized by Unity.** `MonoBehaviour`
  lifecycle, `SerializedProperty`, `AssetDatabase` and the UI Toolkit layout engine are stubs that
  return defaults. The Editor and UI assemblies are *compiled*, never *exercised*.
- **`JsonUtility` is a re-implementation** that keeps Unity's documented constraints (public fields,
  arrays not dictionaries, enums as ints, a null nested object round-tripping to a default
  instance). It is not Unity's code, and a DTO that behaves differently under the real one is
  possible.
- **No input, no touch, no device.** The pointer sources compile; only a build on hardware shows
  whether they fire.
- **Play mode, coroutines, physics, addressables and IL2CPP are all out of scope.**

The Editor still has to run for anything that touches assets, scenes or a build. This tells you
whether the code is *sound*, not whether the game is *right*.

## Keeping it working

When a Unity API is used that the shim lacks, the build fails with a plain "does not contain a
definition for" error naming the member. Add it to `shim/` with the signature Unity documents —
that is the whole maintenance burden, and it is a few lines per occurrence.

If a change here and a change in Unity ever disagree, **Unity is right**. Fix the shim, never the
game code, unless the game code is genuinely wrong.
