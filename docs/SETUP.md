# AlphaTown — project setup

## Status

This repository currently holds the **repo-side scaffolding**: the `Assets/` folder
structure, assembly definitions, Unity `.gitignore` / `.gitattributes`, and the editor
tooling that applies our mobile URP profile.

The **Unity project itself does not exist yet.** `Packages/` and `ProjectSettings/` are
not in the repo. Creating a project from the Universal 3D template requires Unity Hub and
the Unity Editor, neither of which can run in the automation environment this scaffolding
was authored in, so step 1 below has to happen on a workstation.

## 1. Create the Unity project

Use **Unity 6 LTS (6000.x)**. Pick one exact patch version and make sure everyone on the
team installs the same one — `ProjectSettings/ProjectVersion.txt` will pin it once the
project exists.

1. Unity Hub → **New project** → **Universal 3D** (the URP template, not "3D Core").
2. Name it anything; create it in a scratch folder such as `~/AlphaTownTemplate`.
3. Let it open once so the Editor generates `Packages/` and `ProjectSettings/`, then close it.

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

`Assets/Settings/` is where the template keeps its URP assets, renderer data and volume
profiles. On Unity 6 those are `PC_RPAsset` / `Mobile_RPAsset` and their renderers; on
2022 LTS they are `URP-Performant` / `URP-Balanced` / `URP-HighFidelity`. Either naming is
recognised by the configurator.

Do **not** copy the template's `Assets/Scenes/SampleScene.unity` — our scenes live in
`Assets/AlphaTown/Scenes/`. Delete the sample once you have a real bootstrap scene.

## 3. Open and apply the mobile profile

Open the repo folder in the Editor, wait for the first import, then:

1. **AlphaTown ▸ Setup ▸ Audit Mobile URP Profile (dry run)** — logs every value that
   differs from the profile without writing anything. Read it first.
2. **AlphaTown ▸ Setup ▸ Apply Mobile URP Profile** — writes the profile to every URP
   asset and renderer, assigns the pipeline assets to the quality levels, and sets
   `GraphicsSettings.defaultRenderPipeline`.
3. **File ▸ Save Project** — Quality and Graphics settings are project settings and are
   flushed on save.
4. Commit the resulting diff in `Assets/Settings/` and `ProjectSettings/`.

What the profile changes and why is documented in
[URP_MOBILE_PROFILE.md](URP_MOBILE_PROFILE.md).

In CI the same pass runs headless:

```bash
Unity -batchmode -quit -projectPath . \
  -executeMethod AlphaTown.EditorTools.Setup.MobileUrpConfigurator.ApplyFromCommandLine
```

## 4. Switch the build target

`File ▸ Build Settings` → **Android** or **iOS**, then Switch Platform. Player settings
(scripting backend, target architectures, orientation, bundle identifier, graphics APIs)
are deliberately **not** touched by the configurator — they come in the project-settings
pass, alongside the rest of the architecture work.

## Repository layout

```
Assets/
├── AlphaTown/              all first-party content, one folder so third-party
│   ├── Art/                imports never interleave with ours
│   │   ├── Animations/  Buildings/  Characters/  Crops/  Environment/
│   │   └── Materials/   Shaders/    Textures/    UI/Fonts/   VFX/
│   ├── Audio/              Music/  SFX/  Mixers/
│   ├── Code/               one assembly per folder, see below
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

`Content/` is separate from `Data/` on purpose: `Code/Data` holds the ScriptableObject
*types* (`CropDefinition`, `RecipeDefinition`, …) while `Content/` holds the *instances*
designers author. Keeping instances out of the code folders means a live-ops content drop
never touches an assembly.

## Assembly definitions

Compilation is layered, and the layering is enforced by the asmdefs rather than by
convention. References only ever point downward:

```
AlphaTown.Core        (no references — utilities, events, math, pooling)
   └── AlphaTown.Data       (ScriptableObject definitions, pure data types)
        └── AlphaTown.Services  (save/load, remote config, analytics, IAP, server time)
             └── AlphaTown.Gameplay  (simulation: farming, production chains, placement)
                  └── AlphaTown.UI    (screens, HUD, presenters)

AlphaTown.Editor            (Editor platform only, references all of the above)
AlphaTown.Tests.EditMode    (Editor only, UNITY_INCLUDE_TESTS)
AlphaTown.Tests.PlayMode    (all platforms, UNITY_INCLUDE_TESTS)
```

Two consequences worth knowing up front: touching a UI script recompiles only
`AlphaTown.UI`, and a `Gameplay` type can never reach back into `UI`, so the simulation
stays headless and testable.

## Git LFS

`.gitattributes` routes textures, models, audio, fonts and native binaries to LFS. Run
`git lfs install` **once per machine before your first commit** — without it, git stores
those files as plain blobs and the repo grows permanently.

If the team would rather not use LFS, delete the LFS block from `.gitattributes` now,
while the repo still has no binary assets. Retrofitting or removing it later means
rewriting history.
