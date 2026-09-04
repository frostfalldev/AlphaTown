# AlphaTown

A Township-style mobile game: farming, city building, production chains, built to stay
live-ops friendly.

Unity 6.3 LTS · Universal Render Pipeline · Android primary, iOS secondary

## Current state

A playable vertical slice: one scene where you plant, harvest, deliver, earn, build and unlock
land, on the same headless simulation the tests exercise.

- `Assets/` structure and eight layered assemblies
- Core systems: game clock, event bus, inventory, production chains, save/load
- A closed economic loop: currency with source/sink auditing, town level and XP, delivery orders
- Buildings and a town grid: placement, timed construction, upgrades, and the primary coin sink
- Farming: fields as no-input producers, with auto-replant as a data-driven upgrade
- Order board pacing: per-slot cooldowns throttling the main coin faucet
- Expansion: land bought with deeds earned from orders, not coins
- Server-verified time with retry and offline fallback, so timers survive a player editing their clock
- A playable scene: isometric camera, tap to inspect, swipe-to-harvest, and a minimal HUD
- Editor tooling that generates the project settings, the sample content **and the scene itself**
- 217 EditMode tests covering the simulation, runnable **without Unity** in ten seconds

**The Unity project has not been created yet.** `Packages/` and `ProjectSettings/` are not in the
repo; generating them needs Unity Hub and the Universal 3D template on a workstation.
[docs/SETUP.md](docs/SETUP.md) has the exact steps.

## Quick start

```bash
git lfs install          # once per machine, before the first commit
git clone <repo> AlphaTown
```

Then follow [docs/SETUP.md](docs/SETUP.md) to create the Unity project and merge it in. Once it
opens, run **AlphaTown ▸ Setup ▸ Build Playable Project** — that applies the project settings,
generates the sample content and builds `Assets/AlphaTown/Scenes/Town.unity`. Open it and press
Play.

For a device build, that whole chain plus the APK is one command:

```bash
./tools/build-android.sh --install
```

To type-check everything and run the test suite **without Unity at all**:

```bash
sudo apt-get install -y mono-mcs libnunit-framework2.6.3-cil
./tools/headless/run.sh
```

See [docs/VERTICAL_SLICE.md](docs/VERTICAL_SLICE.md).

## Documentation

| | |
| --- | --- |
| [docs/SETUP.md](docs/SETUP.md) | Creating the project, first-open checklist, repo layout, LFS |
| [docs/VERTICAL_SLICE.md](docs/VERTICAL_SLICE.md) | Building and playing the slice, and where the loop is thin |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Assembly layers and the core systems |
| [docs/ECONOMY.md](docs/ECONOMY.md) | The economic loop, reason codes and the tuning levers |
| [docs/BUILDINGS_AND_GRID.md](docs/BUILDINGS_AND_GRID.md) | Placement, construction and upgrade design |
| [docs/FARMING_AND_PACING.md](docs/FARMING_AND_PACING.md) | Fields, auto-replant, and order slot cooldowns |
| [docs/EXPANSION.md](docs/EXPANSION.md) | Land deeds, regions and why land is not coin-gated |
| [docs/TIME_AND_ANTI_CHEAT.md](docs/TIME_AND_ANTI_CHEAT.md) | Threat model, trusted time, and what is still exposed |
| [docs/PROJECT_SETTINGS.md](docs/PROJECT_SETTINGS.md) | Every player and quality setting, and why |
| [docs/URP_MOBILE_PROFILE.md](docs/URP_MOBILE_PROFILE.md) | Every mobile rendering setting, and why |
| [tools/headless/README.md](tools/headless/README.md) | Compiling and testing without a Unity licence |

## Layout

```
Assets/AlphaTown/   first-party content — Art, Audio, Code, Content, Prefabs, Scenes, Tests
Assets/Settings/    URP assets, renderers, volume profiles
Assets/ThirdParty/  asset-store and vendor SDK imports, kept unmodified
docs/               setup, architecture and settings documentation
tools/              build scripts, and a headless compile + test harness
```

Code is split into layered assemblies — `Core → Data → Services → Gameplay → UI` — so references
only point downward and the simulation stays headless and testable.
