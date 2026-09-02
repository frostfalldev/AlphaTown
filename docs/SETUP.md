# AlphaTown — project setup

## Status

The repository holds the **repo-side scaffolding**: the `Assets/` structure, assembly
definitions, the core architecture, Unity git configuration, and the editor tooling that applies
our project settings and mobile URP profile.

`Packages/` and `ProjectSettings/` are **not** in the repo. Creating a project from the Universal
3D template needs Unity Hub, which cannot run in the automation environment this scaffolding was
authored in — so step 1 happens on a workstation, once.

Target: **Unity 6.3 LTS**. Pick one exact patch version for the whole team;
`ProjectSettings/ProjectVersion.txt` pins it once the project exists.

---

## 1. Create the Unity project (once, on a workstation)

1. Unity Hub → **New project** → **Universal 3D** (the URP template, not "3D Core").
2. Create it in a scratch folder such as `~/AlphaTownTemplate`.
3. Open it once so the Editor generates `Packages/` and `ProjectSettings/`, then close it.

## 2. Merge the template into this repo

```bash
git clone <this-repo> AlphaTown
cd AlphaTown
git lfs install                     # required — see .gitattributes
git checkout claude/alphatown-project-setup-qqgymc

cp -R ~/AlphaTownTemplate/Packages        ./Packages
cp -R ~/AlphaTownTemplate/ProjectSettings ./ProjectSettings
cp -R ~/AlphaTownTemplate/Assets/Settings/. ./Assets/Settings/
```

`Assets/Settings/` is where the template keeps its URP assets, renderer data and volume profiles.
On Unity 6 those are `PC_RPAsset` / `Mobile_RPAsset` and their renderers; on 2022 LTS they are
`URP-Performant` / `URP-Balanced` / `URP-HighFidelity`. Either naming works — the configurator
copies from whichever it finds.

Do **not** copy `Assets/Scenes/SampleScene.unity`; our scenes live in `Assets/AlphaTown/Scenes/`.

---

## 3. First open — do these in order

Open the repo folder in the Editor and wait for the first import to finish.

### 3.1 Confirm the code compiles

The Console should be clean. Eight assemblies build: `AlphaTown.Core`, `.Data`, `.Services`,
`.Gameplay`, `.UI`, `.Editor`, and the two test assemblies.

> This code was authored without a Unity Editor available, so it has been syntax-checked but
> **not compiled**. If anything fails here it will be a missing `using` or a Unity API that moved,
> not a design problem — fix it before continuing.

### 3.2 Audit before you write

**AlphaTown ▸ Setup ▸ Audit Mobile URP Profile (dry run)**

Logs every value that differs from the target profile and writes nothing. Read it first, so the
next step holds no surprises.

### 3.3 Apply the configuration

**AlphaTown ▸ Setup ▸ Apply All Project Settings**

Runs three passes in dependency order:

| Pass | What it does |
| --- | --- |
| Player Settings | Identity, orientation, IL2CPP, architectures, graphics APIs, min SDK |
| Quality Levels | **Replaces** the template's levels with Low / Medium / High, creating `AlphaTown_Low`, `AlphaTown_Medium` and `AlphaTown_High` URP assets in `Assets/Settings/` |
| Mobile URP Profile | Tunes every URP asset and renderer to its tier |

Each pass is also on the menu individually. Everything is idempotent — re-running only writes what
differs.

### 3.4 Save and review

1. **File ▸ Save Project** — Quality and Graphics settings only flush to disk on save.
2. Review the diff in `ProjectSettings/` and `Assets/Settings/`.
3. Commit it. This is the moment the configuration becomes reproducible for everyone else.

### 3.5 Run the tests

**Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.** 95 tests covering the barn, production
chains, the clock, the wallet and its ledger, town progression, order generation and expiry, grid
placement, building construction and upgrades, save round trips, and the full economic loop end to
end. They need no scene and should take under a second.

### 3.6 Switch the build target

**File ▸ Build Settings → Android → Switch Platform.** First switch reimports every asset, so
expect it to take a while.

### 3.7 Confirm the open decisions

Two placeholders in `Assets/AlphaTown/Code/Editor/Setup/AlphaTownProjectProfile.cs` are cheap to
change now and expensive later:

- **`ApplicationIdentifier`** — `com.frostfall.alphatown`. Immutable once a store listing exists.
- **`AllowLandscape` / `AllowPortrait`** — currently landscape. One constant today, a UI rebuild
  after screens exist.

Then delete the template's leftover `PC_RPAsset` / `Mobile_RPAsset` once nothing references them.

---

## 4. Wiring up a first scene

Nothing is scene-bound yet, by design. When you want the simulation running:

1. Create a `GameDatabase` asset: **Assets ▸ Create ▸ AlphaTown ▸ Game Database**, in
   `Assets/AlphaTown/Content/`.
2. Create the three entries `GameWorld` requires, and assign each in the database's
   *Well-known entries* section as well as its content list — construction throws with a named
   message if any is missing, rather than shipping a build where half the economy silently
   does nothing:
   - a `StorageDefinition` (**AlphaTown ▸ Economy ▸ Storage Definition**) as the default storage
   - a `CurrencyDefinition` of kind **Soft** (coins) as the soft currency
   - a `ProgressionCurve` (**AlphaTown ▸ Economy ▸ Progression Curve**)
3. Optionally add a **Hard** `CurrencyDefinition` (gems) and one or more
   `OrderTemplateDefinition` assets. Without a template the order board simply stays empty.
   A `TownDefinition` (**AlphaTown ▸ Town Definition**) sets the buildable grid size; without one
   the world falls back to 32x32.
   `BuildingDefinition` assets (**AlphaTown ▸ Buildings ▸ Building Definition**) are what the
   player spends coins on — set a footprint, a level 1 cost, and optionally a producer.
4. Author items, recipes and producers under `Assets/AlphaTown/Content/` and register them.
   Set `CoinValue` and `XpValue` on items — order payouts are derived from them.
5. Add an empty GameObject to the scene, attach **GameRunner**, assign the database.

`GameRunner` seeds a new town from the starting balances on the currency definitions, or loads a
save if one exists and catches up offline progress. It auto-saves every 30 seconds and on pause.

Order generation draws only from the outputs of recipes the player has unlocked, so a brand-new
town with no level-1 recipe produces no orders. That is correct behaviour, not a bug.

---

## 5. Headless configuration (CI)

```bash
Unity -batchmode -quit -projectPath . \
  -executeMethod AlphaTown.EditorTools.Setup.AlphaTownSetupMenu.ApplyAllFromCommandLine
```

The dry-run audit is also usable as a drift check: it reports any URP asset that has wandered
from the profile.

---

## Repository layout

```
Assets/
├── AlphaTown/              all first-party content, one folder so third-party
│   ├── Art/                imports never interleave with ours
│   │   ├── Animations/  Buildings/  Characters/  Crops/  Environment/
│   │   └── Materials/   Shaders/    Textures/    UI/Fonts/   VFX/
│   ├── Audio/              Music/  SFX/  Mixers/
│   ├── Code/               one assembly per folder, see ARCHITECTURE.md
│   │   ├── Core/  Data/  Gameplay/  Services/  UI/  Editor/
│   ├── Content/            designer-authored ScriptableObject instances
│   │   ├── Buildings/  Crops/  Goods/  Recipes/
│   │   └── Orders/  Quests/  Progression/  Economy/
│   ├── Localization/
│   ├── Prefabs/            Buildings/  Characters/  Systems/  UI/  VFX/
│   ├── Scenes/
│   └── Tests/              EditMode/  PlayMode/
├── Plugins/                native plugins
├── Settings/               URP assets, renderers, volume profiles (template location)
├── StreamingAssets/
└── ThirdParty/             asset-store and vendor SDK imports, unmodified
```

`Content/` is separate from `Code/Data` on purpose: `Code/Data` holds the ScriptableObject *types*
(`CropDefinition`, `RecipeDefinition`, …) while `Content/` holds the *instances* designers author.
Keeping instances out of the code folders means a live-ops content drop never touches an assembly.

## Assembly definitions

```
AlphaTown.Core        (no references — events, clock interface, guards, logging)
   └── AlphaTown.Data       (ScriptableObject definitions, pure data types)
        └── AlphaTown.Services  (clock, save/load, remote config, analytics, IAP)
             └── AlphaTown.Gameplay  (simulation: inventory, production, world)
                  └── AlphaTown.UI    (screens, HUD, presenters)

AlphaTown.Editor            (Editor platform only, references all of the above)
AlphaTown.Tests.EditMode    (Editor only, UNITY_INCLUDE_TESTS)
AlphaTown.Tests.PlayMode    (all platforms, UNITY_INCLUDE_TESTS)
```

References only point downward. See [ARCHITECTURE.md](ARCHITECTURE.md) for what lives in each and
why.

## Git LFS

`.gitattributes` routes textures, models, audio, fonts and native binaries to LFS. Run
`git lfs install` **once per machine before your first commit** — without it, git stores those
files as plain blobs and the repo grows permanently.

To drop LFS, delete that block from `.gitattributes` now, while the repo still has no binary
assets. Retrofitting or removing it later means rewriting history.
