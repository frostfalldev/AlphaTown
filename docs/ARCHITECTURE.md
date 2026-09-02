# AlphaTown — architecture

Scaffolding for a long-lived, live-ops mobile town builder. Foundations and extension points,
not features — every system here is deliberately unfinished in the places where a real design
decision is still owed.

## The one idea that shapes everything

**Time-gated state is derived from absolute timestamps, never accumulated by ticking.**

A production order stores `StartedAtTicks` and `CompletesAtTicks`. Whether the player returns in
two minutes or two weeks, catching up is the same comparison against the clock. That single
choice buys:

- **Offline progression for free.** Nothing simulates while the app is closed, because nothing
  needs to. `GameWorld.RestoreSave` calls `Sync` and the town is correct.
- **Cost proportional to what finished, not to elapsed time.** A fortnight away resolves in the
  same handful of operations as a coffee break.
- **Determinism.** The same save plus the same clock yields the same state, on any device.
- **Cheap tests.** A `ManualTimeSource` fast-forwards twenty hours in microseconds.

The corollary matters just as much: the player-facing "speed up" must shorten *one order*, not
advance the clock. `Producer.TrySpeedUp` does that, and `GameClock.Advance` is marked debug-only.

## Assembly layers

Enforced by asmdefs, not by convention. References only point downward — the compiler rejects
anything else.

```
        ┌──────────────────────────────────────────────┐
        │ AlphaTown.UI            screens, HUD          │   (empty — no gameplay UI yet)
        └───────────────────┬──────────────────────────┘
        ┌───────────────────▼──────────────────────────┐
        │ AlphaTown.Gameplay      the simulation        │
        │   BarnInventory · Producer · Wallet           │
        │   TownProgression · OrderBoard                │
        │   TownGrid · TownBuildings · GameWorld        │
        └───────────────────┬──────────────────────────┘
        ┌───────────────────▼──────────────────────────┐
        │ AlphaTown.Services      clock, save, remote   │
        │   GameClock · SaveService · FileSaveStore     │
        └───────────────────┬──────────────────────────┘
        ┌───────────────────▼──────────────────────────┐
        │ AlphaTown.Data          authored content      │
        │   ItemDefinition · RecipeDefinition           │
        │   CurrencyDefinition · ProgressionCurve       │
        │   OrderTemplateDefinition · BuildingDefinition│
        │   TownDefinition · reason-code enums          │
        └───────────────────┬──────────────────────────┘
        ┌───────────────────▼──────────────────────────┐
        │ AlphaTown.Core          no dependencies       │
        │   EventBus · IGameClock · Guard · Log         │
        │   GridPosition · GridSize · GridRect          │
        └──────────────────────────────────────────────┘

AlphaTown.Editor          Editor only, references all of the above
AlphaTown.Tests.EditMode  Editor only, UNITY_INCLUDE_TESTS
AlphaTown.Tests.PlayMode  all platforms, UNITY_INCLUDE_TESTS
```

Two things fall out of this. Touching a UI script recompiles only `AlphaTown.UI`. And Gameplay
*cannot* reach into UI, so the simulation stays headless — which is why every gameplay test runs
without a scene.

### Naming rule worth knowing

A class may not share its simple name with a sibling namespace segment: a class `Inventory` inside
`AlphaTown.Gameplay.Inventory` is unusable from `AlphaTown.Gameplay.World` (CS0118 — the namespace
wins name resolution). That is why the barn class is `BarnInventory`.

## Core systems

### Time — `AlphaTown.Core.Timing`, `AlphaTown.Services.Timing`

`IGameClock` is the single source of "now". Nothing in the simulation reads `DateTime.UtcNow` or
`UnityEngine.Time` directly, which is what makes offline progression, debug time travel and
deterministic tests share one code path.

`GameClock` sits on an `ITimeSource`:

- `DeviceTimeSource` — the device clock. Correct offline, and trusting.
- `ManualTimeSource` — driven by hand, for tests and the debug menu.

`Pause`/`Resume` keep simulation time continuous by absorbing the paused span into an offset —
resuming must not hand back the missing minutes, or every timer in town jumps.

> **TODO (must land before launch):** `ServerTimeSource`. Device time is trivially spoofable, so
> every timer is a cheat surface until an authoritative source exists. The interface is already
> the seam; only the implementation is missing.

### Events — `AlphaTown.Core.Events`

`IEventBus` decouples the layers: Gameplay publishes, UI and Services listen, neither knowing the
other. Events are structs, so publishing does not allocate.

`EventBus` guarantees three things that matter once hundreds of producers are publishing:
publishing to a type with no subscribers costs one dictionary lookup and no allocation;
subscribing or unsubscribing from inside a handler is safe (dispatch runs over a snapshot); and a
throwing handler is logged without stopping the others.

Subscriptions return `IDisposable`. Dispose them on teardown — a retained handler keeps its owner
alive.

### Content — `AlphaTown.Data`

ScriptableObject definitions behind interfaces:

| Interface | Asset | Holds |
| --- | --- | --- |
| `IItemDefinition` | `ItemDefinition` | category, storage cost, storable flag |
| `IRecipeDefinition` | `RecipeDefinition` | inputs, outputs, duration, unlock level |
| `IProducerDefinition` | `ProducerDefinition` | recipes, per-level queue/slots/speed |
| `IStorageDefinition` | `StorageDefinition` | barn capacity per level |
| `IGameDatabase` | `GameDatabase` | id → definition lookup |

Gameplay depends on the **interfaces**, never the assets, so a test builds a content set in three
lines (`Assets/AlphaTown/Tests/EditMode/TestDoubles.cs`) instead of importing a project.

`GameDefinition.Id` is a serialized field, not the asset name: ids are written into save data, and
renaming an asset must never invalidate a player's save. `GameDatabase` indexes on first use and
logs duplicates loudly.

Authored instances live in `Assets/AlphaTown/Content/`, kept out of the code folders so a live-ops
content drop never touches an assembly.

> **TODO (live-ops):** keep `IGameDatabase` and back it with an Addressables catalog plus a
> remote-config overlay, so tuning ships without a store release.

### Inventory — `AlphaTown.Gameplay.Inventory`

`BarnInventory` is **space**-limited, not slot-limited: each item costs
`IItemDefinition.StorageCost` from a shared pool, which is what lets a barn upgrade read as "more
room". Items marked non-storable (currencies) bypass it entirely.

The API separates intents that are easy to conflate: `Add` stores what fits and reports how much;
`TryAddExact` is all-or-nothing; `TryRemoveAll` is atomic, so a failed recipe payment can never
consume half the ingredients.

### Production — `AlphaTown.Gameplay.Production`

`Producer` is one placed building: a queue, N parallel slots, and a tray of finished goods.

- **Inputs are consumed on queue, not on start.** The player has committed the goods, and it stops
  a full queue being a free reservation on the barn.
- **Outputs wait in `Ready` until collected.** Production keeps running while the player is away;
  the tray is what they come back to.
- **A freed slot starts the next order at the moment the previous one finished**, not at "now" —
  otherwise a chain of offline orders would all start on resume and the player would lose hours.

`Sync()` is the whole catch-up algorithm and is safe to call at any time. `GameWorld` polls it
once a second; per-frame would burn battery for nothing, since state is timestamp-derived.

### Economy — `AlphaTown.Gameplay.Economy`

`Wallet` holds currency balances; `CurrencyLedger` holds lifetime source/sink totals. Currency is
never an item and never enters the barn.

Every entry point demands a reason code — there is no overload that moves currency anonymously —
and each movement records to the ledger and publishes a transaction event. The taxonomy
(`CurrencySource`, `CurrencySink`, `CurrencyTransaction`) lives in **Data**, not Gameplay, so the
analytics service in Services can speak it without an upward reference.

### Progression — `AlphaTown.Gameplay.Progression`

`TownProgression` owns town level and XP against an authored `ProgressionCurve`, cascading through
as many levels as one grant covers and paying the curve's rewards into the wallet.

It implements `IUnlockGate`, a narrow interface `Producer` takes instead of the whole progression
system — which is why production can be tested against a fixed level with no curve, wallet or
event bus in sight. Unlocks are enforced in the simulation, not only in UI.

### Orders — `AlphaTown.Gameplay.Orders`

`OrderBoard` is where the loop closes: goods leave the barn, coins and XP come back, XP raises the
level, and the level widens the pool the next order is drawn from.

`OrderGenerator` builds each order's request list from the outputs of **unlocked recipes**, so the
player can only ever be asked for something they can make — a property that holds automatically as
content grows. Rewards are baked in at generation time so a live-ops retune cannot retroactively
cut a reward a player is already working toward.

Full detail, including the reason-code taxonomy and the tuning levers, is in
[ECONOMY.md](ECONOMY.md).

### Grid — `AlphaTown.Core.Spatial`, `AlphaTown.Gameplay.Grid`

`GridPosition`, `GridSize` and `GridRect` sit in Core because both Data (a building's footprint)
and Gameplay (placement) need them. Integers only: the simulation never deals in world units,
which keeps placement exact and save data stable across art changes.

`TownGrid` answers one question — which building instance owns this cell — and nothing else. It is
deliberately not a tilemap engine; a grid that also understood buildings would become a second copy
of the building system and stop being testable on its own.

### Buildings — `AlphaTown.Gameplay.Buildings`

`TownBuildings` owns the placed buildings and the grid beneath them: validation, purchase, upgrade,
move, remove, and construction completion. **This is the primary coin sink** — every charge goes
through the wallet with `BuildingPurchase` or `BuildingUpgrade`, so what the town costs shows up in
the economy numbers next to what orders pay in.

Construction is an absolute timestamp like production and orders. "Busy" is derived from
`TargetLevel > Level` rather than stored, so it survives a save round trip with no second field to
keep in step, and a zero-second build still completes in the sync that started it.

Coins and materials are checked together before either is taken — charging coins and then failing on
planks would bill the player for a building they never got.

Buildings reach production through `IProducerHost`, implemented by `GameWorld`, so the two systems
stay independently testable.

Full detail, including both upgrade paths and the expansion hook, is in
[BUILDINGS_AND_GRID.md](BUILDINGS_AND_GRID.md).

### Save — `AlphaTown.Services.Save`

```
GameWorld.CaptureSave()  →  GameSaveData (DTO)
                              ↓  ISaveSerializer   (JsonUtility — AOT-safe under IL2CPP)
                            SaveEnvelope { SchemaVersion, SavedAtUtcTicks, AppVersion, Payload }
                              ↓  ISaveStore        (FileSaveStore → cloud later)
                            atomic write + .bak
```

Deliberate choices:

- **Atomic writes with a backup.** The OS can kill a mobile app mid-write; a half-written save
  replacing a good one is an account lost. Reads fall back to `.bak`.
- **The payload is a nested JSON string.** Migrations rewrite raw JSON, because by definition the
  old save has a shape the current DTOs cannot parse.
- **A save from a newer build is refused, not partially read.** Silently dropping fields a player
  has already paid for is worse than failing to load.
- **DTOs are separate from runtime types.** Runtime state is free to be restructured; save data is
  a contract with every installed build.
- **Enums persist as ints.** A value written by a newer build survives a round trip through an
  older one instead of collapsing onto the zero member and corrupting a lifetime total.

`SaveSchemaVersion` stays at 1 until the first breaking change *after* launch. Bumping it
pre-launch would only create migration debt for saves that do not exist.

`GameRunner` saves on `OnApplicationPause(true)` — the save point that actually matters, since
Android can kill a backgrounded app without ever calling `OnApplicationQuit`.

> **TODO (live-ops):** cloud save implements `ISaveStore`. Expect a composite that writes
> local-first and reconciles on login **with an explicit conflict policy** — silently taking the
> newer timestamp loses progress.

### Composition — `AlphaTown.Gameplay.Bootstrap`

`GameRunner` is the only MonoBehaviour in the simulation. It builds the services, owns the
`GameWorld`, pumps the clock and auto-saves. Everything it constructs is plain C#, so the same
object graph stands up in an EditMode test.

`ServiceRegistry` holds the long-lived services. **Composition root only** — a class that resolves
its own dependencies from it cannot be tested without standing up the whole game. Systems take
what they need through their constructors.

## Testing

`Assets/AlphaTown/Tests/EditMode` covers the barn's capacity and atomicity rules, the production
chain (including a twenty-hour absence resolving in one `Sync`), clock pause/resume continuity,
wallet atomicity and ledger reconciliation, XP cascading and cap behaviour, order generation and
expiry, grid placement rules, building construction and both upgrade paths, a save round trip
through the real serializer, and the full economic loop end to end.

`TestContent` is tuned so exactly one item is producible at town level 1, which makes generated
orders deterministic without depending on an RNG seed. Randomness is injected into `GameWorld`
for the same reason.

No scene, no assets, no play mode. If a new gameplay system cannot be tested that way, it has a
dependency it should not have.

## Conventions

- Simulation code is plain C#. MonoBehaviours are for scene glue only.
- Interfaces at layer boundaries; concrete types within a layer.
- `Log.Info` compiles out of release builds — the call site is removed, so the string is never
  built. Warnings and errors always ship.
- `Guard` at public seams. Failing loudly in a test beats a null two systems away.
- Data is authored, not hard-coded. A number a designer will want to tune belongs in a
  ScriptableObject.
