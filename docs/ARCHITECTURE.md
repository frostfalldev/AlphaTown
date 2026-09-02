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
        │ AlphaTown.UI            screens, HUD, views   │
        │   IsoCameraController · TownView              │
        │   PointerInput · TownGestures · Sickle…       │
        │   TownHud and its panels                      │
        └───────────────────┬──────────────────────────┘
        ┌───────────────────▼──────────────────────────┐
        │ AlphaTown.Gameplay      the simulation        │
        │   BarnInventory · Producer · Wallet           │
        │   TownProgression · OrderBoard                │
        │   TownGrid · TownBuildings · TownExpansion    │
        │   GameWorld · TownCommands                    │
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
        │   TownDefinition · ExpansionDefinition        │
        │   NewGameDefinition · presentation interfaces │
        │   reason-code enums                           │
        └───────────────────┬──────────────────────────┘
        ┌───────────────────▼──────────────────────────┐
        │ AlphaTown.Core          no dependencies       │
        │   EventBus · IGameClock · Guard · Log         │
        │   GridPosition · GridSize · GridRect          │
        │   IsoGridMath · DeterministicRoll             │
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

- `ServerTimeSource` — what a build runs on. Takes one authoritative instant from the backend and
  carries it forward on a monotonic counter, so the device clock is never read again during a
  session. Moving it achieves nothing.
- `DeviceTimeSource` — the raw device clock. Used as the fallback *inside* `ServerTimeSource`,
  never on its own in a build.
- `ManualTimeSource` — driven by hand, for tests and time-travel debugging.

One field on `GameRunner` picks between the three, and a failed sync retries on a doubling backoff
driven by the monotonic clock rather than a coroutine — so the retry path is testable by advancing
a number.

Every source reports a `TimeTrust`, surfaced up through `IGameClock.Trust`, so a system handing
out real value on a timer can ask whether this session's clock can be believed.

`Pause`/`Resume` keep simulation time continuous by absorbing the paused span into an offset —
resuming must not hand back the missing minutes, or every timer in town jumps.

Every gate in the game — crops, construction, upgrades, production, order expiry, slot cooldowns —
is a comparison against this clock, which makes it the most valuable thing in the game to lie
about. The threat model, the fallback behaviour, what remains exposed and the hardening order are
in [TIME_AND_ANTI_CHEAT.md](TIME_AND_ANTI_CHEAT.md).

> **Still open:** the time source reads an HTTP `Date` header, which defeats a player in Settings
> but is unsigned. A signed timestamp from your own backend is the next step, and only
> `HttpDateHeaderTimeProvider` changes when it lands.

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

The board is a fixed set of **slots**, each with its own authored cooldown. A slot that is
completed, expired or discarded goes quiet before offering anything new — the throttle on the
game's main coin faucet, and the reason free crops do not become free money. Cooldowns are
absolute timestamps and are persisted, so reloading cannot hand back a full board.

Fields are not a separate system: a Farming building whose producer runs recipes with no inputs.
Farming and pacing are covered together in [FARMING_AND_PACING.md](FARMING_AND_PACING.md).

Full detail, including the reason-code taxonomy and the tuning levers, is in
[ECONOMY.md](ECONOMY.md).

### Grid — `AlphaTown.Core.Spatial`, `AlphaTown.Gameplay.Grid`

`GridPosition`, `GridSize` and `GridRect` sit in Core because both Data (a building's footprint)
and Gameplay (placement) need them. Integers only: the simulation never deals in world units,
which keeps placement exact and save data stable across art changes.

`TownGrid` answers two questions — which building instance owns this cell, and whether the player
owns the land under it — and nothing else. It is deliberately not a tilemap engine; a grid that
also understood buildings would become a second copy of the building system and stop being testable
on its own.

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

### Expansion — `AlphaTown.Gameplay.Expansion`

`TownExpansion` owns which land the player has bought. The gate is **land deeds** — a non-storable
item earned from orders — not coins: coins already buy buildings, and a coin-gated town would grow
at whatever rate a player can grind the order board, which is no pacing at all.

State is a set of owned expansion ids, and the grid's unlocked mask is **rebuilt from that set**
rather than accumulated, so there is one source of truth and no way for the two to drift.

`TownGrid.IsUnlocked` was written as a stub in the grid phase for exactly this: placement,
validation and moving needed no change when land arrived, because `PlacementFailure.AreaLocked` was
already wired through. Detail in [EXPANSION.md](EXPANSION.md).

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

### Presentation data — `AlphaTown.Data.Presentation`

Art hangs off definitions through a **second interface**, never the simulation one.
`ItemDefinition` implements both `IItemDefinition` and `IItemVisuals`; `RecipeDefinition` adds
`IRecipeVisuals` (icon plus growth-stage frames); `BuildingDefinition` adds `IBuildingVisuals`
(icon, map sprite, placeholder tint). A view asks for the second one:

```csharp
if (definition is IRecipeVisuals visuals) renderer.sprite = visuals.StageFor(progress);
```

Two things fall out. The simulation interfaces stay free of `UnityEngine` types, so a test can
fake them with a plain object. And nothing in Gameplay *can* branch on a sprite, because Gameplay
never sees one.

Growth frames are presentation only. The simulation knows a start timestamp and a duration; which
picture that maps to is a question the view asks per frame. Adding art frames re-times the
animation and changes nothing about when the crop is ready.

### Variable yield — `AlphaTown.Core.Randomness`

`IRecipeDefinition.BonusOutputMax` lets a recipe yield more than it promises. Making that safe in a
timestamp-driven simulation needs one rule: **the roll is a hash of the completed order, not a draw
from a stream.**

```csharp
var seed = DeterministicRoll.Seed(InstanceId + "|" + recipe.Id, order.CompletesAtTicks);
var bonus = DeterministicRoll.Range(seed, 0, recipe.BonusOutputMax);
```

The answer is therefore fixed the moment the order starts. A harvest resolved on resume after a
week away yields what it would have yielded had someone watched it finish, and re-syncing the same
save twice cannot produce two answers. Including the instance id stops a row of fields planted in
one tap from all rolling identically.

Deliberately not `UnityEngine.Random`: that is global mutable state shared with every effect in the
project, so the sequence the simulation saw would depend on what the renderer did that frame.

### Commands — `AlphaTown.Gameplay.Commands`

`TownCommands` is everything the player can ask the town to do, phrased the way a screen asks it:
plant this field, harvest what is ready, build here, deliver that order. It is the only thing the
UI talks to.

A view holds a `TownCommands` and a read-only look at `GameWorld`. It does not know that planting
is really "enqueue a recipe on the producer attached to a Farming-category building", and it cannot
reach a state the simulation would refuse — every rule is still enforced underneath.

It is also where *why not?* is answered. The systems below return enums built for code;
`CommandResult` carries a sentence a player can read. Doing that translation here keeps it testable
and out of the MonoBehaviours.

> The messages are plain English rather than localisation keys — a deliberate limit of the slice,
> flagged with a `TODO(localisation)` where it lives.

### Presentation — `AlphaTown.UI`

Four pieces, none of which the simulation knows about:

- **`IsoCameraController`** — pan, pinch-zoom and clamping over the diamond projection in
  `IsoGridMath`.
- **`TownView`** — reconciles sprites against the building list every frame rather than listening
  for events. A view that is wrong is wrong for one frame, not until the next reload.
- **`PointerInput`** — where touches come from, whichever input backend the project is set to.
  Nothing else in the project names one. See *Input backends* below.
- **`TownGestures`** — the single place a touch is interpreted. Screen to world to cell is
  arithmetic, so a town of a thousand tiles needs no colliders and no raycasts.
- **`TownHud`** — UI Toolkit, built in C# with inline styles. No UXML or USS while the layout is
  still moving: one file to change, and no asset GUIDs to go missing. Its layout containers are
  `PickingMode.Ignore` so only real widgets block the world; a UI Toolkit root fills the screen and
  is pickable by default, which would otherwise swallow every touch in the game.

#### Input backends

Unity has two, and which is live is a project setting. Reading `UnityEngine.Input` directly
**throws at runtime** when that setting is "Input System Package (New)" — the Unity 6 default —
and compiles perfectly either way. That shipped once: pan, zoom and swipe were all dead in an APK
while the HUD kept working, because UI Toolkit speaks both and the game code spoke one.

So no gameplay code names a backend. `PointerInput` is the only thing that does:

| Active Input Handling | What serves pointers |
| --- | --- |
| Input Manager (Old) | `LegacyPointerSource`, compiled under `ENABLE_LEGACY_INPUT_MANAGER` |
| Both | `LegacyPointerSource` — one proven path rather than two arguing |
| Input System Package (New) | `InputSystemPointerSource`, which registers itself at startup |

The Input System source lives in its own assembly with
`defineConstraints: ["ENABLE_INPUT_SYSTEM", "!ENABLE_LEGACY_INPUT_MANAGER"]`. When those do not
hold, Unity skips the assembly and never tries to resolve its reference to `Unity.InputSystem` —
so a project without the package still compiles. It registers itself through
`PointerInput.SetSource` rather than being referenced, because `AlphaTown.UI` cannot depend on an
assembly that may not exist.

`IPointerSource` reports only *which pointers are down and where*, never began/moved/ended. Phase
enums differ between the backends in ways that are easy to mirror slightly wrong, and every
consumer has to track the previous frame anyway — so the phases are derived once, in
`TownGestures`.

#### One reader, one gesture

`TownGestures` is the only thing that polls input. Before, the camera, the tap handler and the
sickle each polled the same finger and each acted on it, so one drag panned the map *and*
harvested every crop it crossed.

A press now resolves to exactly one gesture and stays there until the finger lifts:

| Fingers | Sickle armed? | Started on | Becomes |
| --- | --- | --- | --- |
| one | — | a HUD widget | Nothing; the UI has it |
| one | yes | anywhere else | Sickle swipe, cutting from the moment it lands |
| one | no | anywhere else | Drag → camera pan; no movement → tap, select |
| two | — | — | Pinch zoom **and** pan, cancelling whatever the first finger was doing |

#### Tools are modes, and modes must be visible

`TownTool` holds what the player is carrying. It is separate from `TownSelection` because they
answer different questions — what is selected, versus what happens when you drag — and because
arming a tool must not clear the selection that armed it.

An earlier version inferred the sickle from a drag that *began* on a ripe crop. No mode, no extra
tap. It was worse: a gesture nobody tells you about reads as broken when it does not fire, there is
nothing on screen to explain why, and it quietly stole panning from every tile that happened to be
ready.

A mode earns its cost when it is impossible to enter by accident and obvious once you are in it.
Arming the sickle takes two deliberate taps, puts a banner on screen and a blade in the player's
hand, keeps two-finger pan and zoom live so nobody gets stranded, and disarms itself when there is
nothing left to cut.

`IsoCameraController` no longer reads input at all — it takes `BeginPan`/`PanByScreenDelta`/
`EndPan`/`ZoomByPinch` and keeps its damping, inertia and bounds clamping. That also means the
camera can be driven from a test, a tutorial or a "focus on this building" button without faking
input.

### Composition — `AlphaTown.Gameplay.Bootstrap`

`GameRunner` is the only MonoBehaviour in the simulation. It builds the services, owns the
`GameWorld`, pumps the clock and auto-saves. Everything it constructs is plain C#, so the same
object graph stands up in an EditMode test.

`ServiceRegistry` holds the long-lived services. **Composition root only** — a class that resolves
its own dependencies from it cannot be tested without standing up the whole game. Systems take
what they need through their constructors.

`GameRunner.RequestSave()` is the save point for player actions: debounced two seconds, because a
sickle swipe harvests a dozen fields in a second and serialising the whole town a dozen times would
show up as a stutter under the player's finger. `SaveGame()` stays immediate for pause and quit.

A new player is seeded from `INewGameDefinition` — barn level, starting goods and buildings that
are already standing. Those go through `TownBuildings.GrantBuilding`, which skips cost and unlock
checks but still validates against the grid, and deliberately touches neither wallet nor ledger: a
granted building is not a purchase, and recording it as one would make the coin-sink numbers lie.

The scene itself is **generated** by `MainSceneBuilder` rather than committed. See
[VERTICAL_SLICE.md](VERTICAL_SLICE.md).

## Testing

`Assets/AlphaTown/Tests/EditMode` covers the barn's capacity and atomicity rules, the production
chain (including a twenty-hour absence resolving in one `Sync`), clock pause/resume continuity,
wallet atomicity and ledger reconciliation, XP cascading and cap behaviour, order generation and
expiry, order slot pacing and its persistence, the farming loop including the offline auto-replant
bound, grid placement rules, building construction and both upgrade paths, land purchase with its
prerequisite chain and restore ordering, clock synchronisation with its offline and tamper paths,
a save round trip through the real serializer, the command layer the UI drives, deterministic
variable yields matching between offline and watched play, new-game seeding, and the full economic
loop end to end.

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
