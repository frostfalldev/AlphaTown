# AlphaTown — buildings and the grid

Buildings are the primary coin sink. Until this phase the economy had a faucet (orders) and no
drain; construction and upgrades are what close that.

```
        coins ──▶ TownBuildings ──▶ BuildingInstance ──▶ Producer
                       │                   │
                       │                   └── construction timestamp
                       ▼
                   TownGrid  (which cells are taken)
```

---

## The grid

### Occupancy, not a tilemap

`TownGrid` answers exactly one question: *which building instance owns this cell*. It is a flat
`string[]` indexed `y * width + x`, and it knows nothing about buildings, costs or construction —
only ids and rectangles.

That narrowness is deliberate. A grid that also understood buildings would become a second copy of
the building system, and the placement rules would stop being testable on their own. `TownGridTests`
runs against the grid with no world, no clock and no content.

### Integer cells, axis-aligned rects

`GridPosition`, `GridSize` and `GridRect` live in **Core.Spatial** because both Data (a building's
footprint) and Gameplay (placement) need them. They are integers only — the simulation never deals
in world units, which keeps placement exact and save data stable when art changes scale.

There is no rotation in the simulation. A rotated 2x1 building is a 1x2 footprint as far as the
grid is concerned; how it is drawn is presentation's problem.

`MaxX` and `MaxY` are **inclusive**, so a 1x1 rect has `Min == Max`. Half-open ranges read better
in loops but worse in placement code, where "the last cell it covers" is the question being asked.

### Two rules worth knowing

**`Validate` takes an `ignoreInstanceId`.** Nudging a building one cell means its new footprint
overlaps its old one. Without the exemption nothing could ever move by less than its own width.

**`Release` only clears cells the caller actually owns.** Releasing a stale rect after a move
cannot silently delete a neighbour's occupancy — the grid checks the id before clearing each cell.

### The expansion hook

`TownGrid.IsUnlocked(cell)` currently returns "yes, if in bounds". Expansion will gate it on
purchased regions. Keeping the question inside the grid means placement validation needs no change
when that lands — only that one method, and `PlacementFailure.AreaLocked` is already wired through
to `BuildingActionResult`.

### The grid is not saved

Occupancy is rebuilt from building positions on load. Persisting it too would mean two sources of
truth that can disagree, and the derived one is free to recompute.

---

## Buildings

### Definition and instance

`BuildingDefinition` (ScriptableObject) is authored content: footprint, unlock level, an array of
levels each with its own cost and build time, an optional producer, and an optional definition it
upgrades into. `BuildingInstance` is runtime state: where it sits, what level it is, and when its
current construction finishes.

### Construction is a timestamp

Same as production and orders. `ConstructionCompletesAtTicks` is absolute, so a build started
before the app closed is simply finished by the next `Sync()` — no ticking, no catch-up loop, and
the same answer on any device. `BuildingSaveTests` covers a build completing across a one-day
absence.

### "Busy" is derived, not stored

```csharp
public bool IsBusy => TargetLevel > Level;
```

Not a flag and not a sentinel timestamp. A flag is a second thing to keep in step across a save
round trip; a sentinel (`completesAt != 0`) breaks for a zero-second build, which has to complete
in the very `Sync` that started it. Deriving it from the level pair is true in every case:

| | Level | TargetLevel | State |
| --- | --- | --- | --- |
| First build running | 0 | 1 | UnderConstruction |
| Built and idle | 1 | 1 | Operational |
| Upgrading | 1 | 2 | Upgrading |

`BuildingState` is computed from the same pair, so it cannot drift either.

### Two upgrade paths

**In place** — the next level within the same definition. The building **keeps running at its
current level** for the whole timer, and its producer keeps working. That is what makes an upgrade
a decision rather than a shutdown.

**Transform** — at its last level with an `UpgradesInto` set, the building *becomes* a different
definition: a hut becomes a villa. It swaps definition immediately, drops to level 0, and rebuilds.

The transform swaps **at the start, not on completion**, so the grid reserves the replacement's
footprint for the whole build. Reserving only the old footprint would leave the extra cells free
for something else to take, and the upgrade would fail at the finish line.

A transform replaces the machine, so it drops the producer and anything queued in it. That is a
real cost and it is currently silent — see the open items below.

### Costs

Coins go through the wallet with a reason code, materials come out of the barn:

| Action | Sink |
| --- | --- |
| Placing a new building | `CurrencySink.BuildingPurchase` |
| Any upgrade, in place or transform | `CurrencySink.BuildingUpgrade` |

**Both halves are checked before either is applied.** Charging coins and then discovering the
planks are missing would bill the player for a building they never got. `CheckCost` asks both
questions, `ChargeCost` performs both, and nothing runs in between — so neither can fail. There is
a test for exactly this: an unaffordable material cost must leave the coin balance untouched.

### Buildings and producers

A building with a `ProducerDefinitionId` gets a `Producer` when construction finishes, keyed by the
building's instance id, with the producer's level tracking the building's.

The wiring goes through `IProducerHost`, implemented by `GameWorld`. The seam exists so the two
systems stay independently testable — a building test can pass a recording stub and assert that a
finished factory got a producer at the right level without standing up a world.

### Placement results

`BuildingActionResult` is an enum rather than a bool because every failure has a different message
and a different call to action: "you cannot afford this" sells gems, "that spot is taken" does not.
`ValidatePlacement` runs the same checks as `TryPlace` and charges nothing, for a build-mode
preview later.

---

## Save

`BuildingSaveData` holds instance id, definition id, origin, level, target level and both
construction timestamps. Origin is two ints because JsonUtility has no struct shorthand.

Buildings restore **after** producers, so a restored building matches itself back to the producer
saved alongside it rather than creating a second one. Any operational building whose producer is
missing gets one — a save written before a building gained a producer heals itself on load.

**A building whose footprint no longer fits is dropped, loudly.** Only reachable if a definition's
footprint changed after the save was written, which must not happen post-launch. See the open items.

---

## Open items

- **Removal refunds nothing.** `CurrencySource.Refund` exists as the reason code; selling for a
  fraction of the build cost is undesigned.
- **A transform upgrade silently discards queued production.** Either refund the queued inputs or
  refuse the upgrade while orders are running.
- **A building that no longer fits on load is dropped rather than relocated.** A relocation pass
  that finds the nearest legal spot would be kinder, and is needed before footprints can ever change.
- **No road or access rules.** The grid has the hook (`IsUnlocked`) but no adjacency concept.
- **Grid size is town-wide and fixed.** `TownDefinition` supplies it; expansion turns it into a
  maximum extent with unlocked regions inside.
