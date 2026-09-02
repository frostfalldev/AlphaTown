# AlphaTown

A Township-style mobile game: farming, city building, production chains, built to stay
live-ops friendly.

Unity 6 LTS (6000.x) · Universal Render Pipeline · Android + iOS

## Current state

The repo holds the project scaffolding — `Assets/` structure, assembly definitions, Unity
git configuration, and the editor tooling that applies our mobile URP profile.

**The Unity project has not been created yet.** `Packages/` and `ProjectSettings/` are not
in the repo; generating them needs Unity Hub and the Universal 3D template on a
workstation. [docs/SETUP.md](docs/SETUP.md) has the exact steps.

## Quick start

```bash
git lfs install          # once per machine, before the first commit
git clone <repo> AlphaTown
```

Then follow [docs/SETUP.md](docs/SETUP.md) to create the Unity project from the
Universal 3D template and merge it in. Once it opens, run
**AlphaTown ▸ Setup ▸ Apply Mobile URP Profile**.

## Documentation

| | |
| --- | --- |
| [docs/SETUP.md](docs/SETUP.md) | Creating the Unity project, repo layout, assembly layering, LFS |
| [docs/URP_MOBILE_PROFILE.md](docs/URP_MOBILE_PROFILE.md) | Every mobile rendering setting and the reasoning behind it |

## Layout

```
Assets/AlphaTown/   first-party content — Art, Audio, Code, Content, Prefabs, Scenes, Tests
Assets/Settings/    URP assets, renderers, volume profiles
Assets/ThirdParty/  asset-store and vendor SDK imports, kept unmodified
docs/               setup and rendering documentation
```

Code is split into layered assemblies — `Core → Data → Services → Gameplay → UI` — so
references only ever point downward and the simulation stays headless and testable. See
[docs/SETUP.md](docs/SETUP.md#assembly-definitions).
