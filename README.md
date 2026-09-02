# AlphaTown

A Township-style mobile game: farming, city building, production chains, built to stay
live-ops friendly.

Unity 6.3 LTS · Universal Render Pipeline · Android primary, iOS secondary

## Current state

Project scaffolding and core architecture. No gameplay UI, no content, no scenes yet — the
foundations the rest gets built on.

- `Assets/` structure and eight layered assemblies
- Core systems: game clock, event bus, inventory, production chains, save/load
- A closed economic loop: currency with source/sink auditing, town level and XP, delivery orders
- Buildings and a town grid: placement, timed construction, upgrades, and the primary coin sink
- Farming: fields as no-input producers, with auto-replant as a data-driven upgrade
- Order board pacing: per-slot cooldowns throttling the main coin faucet
- Editor tooling that applies player settings, quality levels and a mobile URP profile
- EditMode tests covering the simulation, with no scene required

**The Unity project has not been created yet.** `Packages/` and `ProjectSettings/` are not in the
repo; generating them needs Unity Hub and the Universal 3D template on a workstation.
[docs/SETUP.md](docs/SETUP.md) has the exact steps.

## Quick start

```bash
git lfs install          # once per machine, before the first commit
git clone <repo> AlphaTown
```

Then follow [docs/SETUP.md](docs/SETUP.md) to create the Unity project and merge it in. Once it
opens, run **AlphaTown ▸ Setup ▸ Apply All Project Settings**.

## Documentation

| | |
| --- | --- |
| [docs/SETUP.md](docs/SETUP.md) | Creating the project, first-open checklist, repo layout, LFS |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Assembly layers and the core systems |
| [docs/ECONOMY.md](docs/ECONOMY.md) | The economic loop, reason codes and the tuning levers |
| [docs/BUILDINGS_AND_GRID.md](docs/BUILDINGS_AND_GRID.md) | Placement, construction and upgrade design |
| [docs/FARMING_AND_PACING.md](docs/FARMING_AND_PACING.md) | Fields, auto-replant, and order slot cooldowns |
| [docs/PROJECT_SETTINGS.md](docs/PROJECT_SETTINGS.md) | Every player and quality setting, and why |
| [docs/URP_MOBILE_PROFILE.md](docs/URP_MOBILE_PROFILE.md) | Every mobile rendering setting, and why |

## Layout

```
Assets/AlphaTown/   first-party content — Art, Audio, Code, Content, Prefabs, Scenes, Tests
Assets/Settings/    URP assets, renderers, volume profiles
Assets/ThirdParty/  asset-store and vendor SDK imports, kept unmodified
docs/               setup, architecture and settings documentation
```

Code is split into layered assemblies — `Core → Data → Services → Gameplay → UI` — so references
only point downward and the simulation stays headless and testable.
