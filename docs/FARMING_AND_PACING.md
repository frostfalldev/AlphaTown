# AlphaTown — farming and order pacing

Two changes that belong together: fields make goods **free**, and slot cooldowns are what stops
free goods becoming free money.

---

## Fields

### A field is not a new system

```
Farming BuildingDefinition  →  Producer  →  recipe with no inputs
```

That is the whole design. A field is a building in the `Farming` category whose producer runs
recipes with an empty input list. Planting is `TryEnqueue`, harvesting is `CollectReady`, growth is
the same completion timestamp production has always used, and the unlock gate that stops you baking
cake at level 1 stops you planting corn there too.

Almost no code was added for this. That is the point: if farming had needed its own subsystem, the
producer abstraction would have been wrong.

A crop that costs seeds is just a recipe **with** inputs — the free case and the paid case are the
same machinery, so seeds need no new concept when the design wants them.

### After harvest: empty by default

A harvested field goes back to empty and waits to be re-planted, matching Township. Auto-replant
exists but is **opt-in per producer level**, so it can be sold as an upgrade:

```csharp
bool IProducerLevel.AutoRepeat { get; }
```

In the test content, the field building's level 2 turns it on. Upgrading the building raises the
producer's level, which switches on auto-replant — the upgrade is entirely data, no code path of
its own.

### Auto-replant triggers on collection, never on completion

This is the load-bearing decision. If a field re-sowed itself when a crop *finished*, it would keep
cycling while the app was closed, and a field left for a fortnight would bank a fortnight of
harvests. That is unbounded offline income, and it would arrive through the one system specifically
designed to make offline progression free.

Tying it to **collection** caps any absence at exactly one harvest, however long, and makes
auto-replant what a player actually wants it to be: the field starts growing again the moment you
take the crop, without a second tap.

`FieldFarmingTests.AutoReplant_DoesNotCycleWhileThePlayerIsAway` pins this: fourteen days away
yields one harvest.

### Harvesting into a full barn

`CollectReady` takes what fits and leaves the rest in the field. Nothing is destroyed, and the crop
is still there after a barn upgrade.

---

## Order board pacing

### Slots, not a list

The board is a fixed set of **slots**, each with its own cooldown. A slot whose order is completed,
expired or discarded goes quiet before offering anything new.

```
slot 0  [ order ]
slot 1  [ order ]
slot 2  [ cooling until T+10m ]
slot 3  [ order ]
```

Before this, the board refilled the instant it emptied. With fields producing for free that is an
unbounded coin faucet: harvest, deliver, repeat, with the only limit being how fast the player can
tap. Cooldowns are the throttle.

### Data-driven, per slot

`OrderBoardDefinition` authors one cooldown **per slot**, and the number of entries is the slot
count. That covers the obvious next design move — a board whose first slot refills quickly and whose
later slots are slow — without a code change, and it makes board capacity data too.

Cooldowns are absolute timestamps like everything else, so a board left for a week resolves in a
single `Sync()`.

### Cooldowns are saved

Without persistence, reloading would hand back a full board every launch — a free income multiplier
for anyone willing to restart the app. `OrderBoardSaveData` carries one timestamp per slot, and
each saved order carries its slot index so it returns to the slot it left.

Orders are stored as a flat array with a `SlotIndex` rather than nested inside slot objects,
because JsonUtility cannot round-trip a null nested object — an empty slot would come back as a
default-constructed order rather than nothing.

### Discarding costs a cooldown

Rerolling for free would let a player fish for a better payout indefinitely. A paid reroll is the
intended escape hatch: `CurrencySink.OrderReroll` exists for it, and skipping the cooldown is
exactly what the player would be buying. Not implemented yet.

### The fallback still paces

A project with no `OrderBoardDefinition` gets `FallbackOrderBoardDefinition` — four slots, five
minutes each. Deliberately not zero: an unpaced board is an unbounded faucet, and that should not
be what an unconfigured project quietly does. Authoring a definition is how you *tune* pacing, not
how you turn it on.

---

## The loop as it now stands

```
plant a field  →  wait (offline is fine)  →  harvest into the barn
      ↑                                              │
      │                                              ▼
   re-plant  ←──────────  deliver an order  →  coins + XP
   (or auto-replant)              │                  │
                                  ▼                  ▼
                        slot cools for 10m      town level rises
                                                     │
                                                     ▼
                                            more recipes, more buildings
```

Both throttles are real and they throttle different things. **Slot cooldowns** limit how fast coins
arrive. **Barn capacity** limits how much can be stockpiled between visits. Neither is a timer the
player stares at; both are reasons to come back later.

---

## Still open

- **`ServerTimeSource` does not exist.** Device time is spoofable, and it now gates crops,
  construction, production and order cooldowns. Every one of those is a cheat surface until an
  authoritative clock lands. The `ITimeSource` seam is ready; only the implementation is missing.
- **Cloud save has no conflict policy.** `ISaveStore` is the seam. Silently taking the newer
  timestamp loses progress, so the policy has to be designed rather than defaulted.
- **Paid reroll is unpriced**, as above.
- **No "plant all" affordance.** Fine headless; it is a UI concern when fields are numerous.
