# AlphaTown — expansion and land deeds

Land is the one thing money cannot rush.

```
order completed ──▶ land deed (item) ──▶ TownExpansion.TryUnlock ──▶ TownGrid mask
                                                                          │
                                                                          ▼
                                                              placement stops saying AreaLocked
```

---

## Why deeds and not coins

Coins already buy buildings, and coins are earned from orders. If land were also coin-gated, the
town would grow at whatever rate a player can grind the order board — which is no pacing at all,
just arithmetic. Worse, it would make the coin faucet's tuning carry two jobs at once.

**Land deeds are a separate item, dropped from orders at a rate the designer sets.** That decouples
"how rich am I" from "how big is my town", and it gives the design a lever that is not money.

Coins are supported as an **optional secondary cost** on an expansion — a "and 500 coins" on top of
the deeds — but they are never the gate on their own.

## Deeds are an item, not a currency

The cleanest home turned out to be `BarnInventory`, as an `ItemDefinition` with
`IsStorable = false`.

Non-storable items already cost zero barn space and have no capacity limit, so a deed does not
compete with wheat for room in the barn. Nothing new was needed — `StorageCostOf` returns 0,
`RoomFor` returns unbounded, and `TryRemoveAll` spends them atomically like any other item cost.

Making deeds a *currency* would have been worse: the wallet's whole contract is source/sink
attribution against real money, and a deed is a token you hold a handful of, not a balance you
model an economy on.

Deeds are never *requested* by orders, because `OrderGenerator` builds its candidate pool from
storable recipe outputs only. A non-storable token can never sit in the barn as cargo, so it can
never be asked for — that falls out of the existing rule rather than needing a new one.

## Regions

`ExpansionDefinition` is one buyable rectangle:

| Field | Purpose |
| --- | --- |
| `Region` | The `GridRect` it unlocks |
| `ItemCost` | The real gate — land deeds |
| `CurrencyCost` | Optional secondary cost, usually empty |
| `UnlockLevel` | Town level floor |
| `RequiresExpansionId` | The plot it grows from, or empty |
| `SortOrder` | Presentation only |

`RequiresExpansionId` is what makes land spread **outward**. Without it a player could buy a far
corner first and end up with a disconnected town; with it, each plot names the one it grows from
and the chain enforces itself. It is a prerequisite, not a strict sequence — a plot can have two
successors, so the land menu can branch.

## The single hook

`TownGrid.IsUnlocked(cell)` was written as a stub in the grid phase precisely so this phase would
not have to touch placement. It now reads a `bool[]` mask, and **nothing about placement,
validation, moving or building changed** — `PlacementFailure.AreaLocked` was already wired end to
end through `BuildingActionResult`.

That is the payoff of putting the question in the grid rather than leaving it implicit.

## One source of truth

State is a `HashSet<string>` of owned expansion ids. The grid's mask is **rebuilt from that set**
every time it changes, never accumulated:

```csharp
void ApplyToGrid()
{
    regions = [startingArea] + [each owned expansion's region]
    grid.SetUnlockedRegions(regions);
}
```

Accumulating would mean two representations that can disagree — and a save that restored ids
without replaying the unlocks would silently produce a smaller town. Rebuilding makes the id set
authoritative and the mask a pure function of it.

The starting area comes from `TownDefinition.StartingArea`. A zero-sized rect means "the whole
grid", so a project with no expansion content behaves exactly as it did before land existed.

## Costs are checked together

Same rule as buildings: deeds and coins are both verified before either is taken. Charging coins
and then failing on deeds would take payment for land the player never got.
`MissingCoins_LeaveTheDeedsUnspent` pins it.

## Failure reasons

`ExpansionResult` is an enum because each failure needs a different answer — missing deeds points
at the order board, a missing prerequisite points at the neighbouring plot, a level requirement
points at the whole game:

`Success` · `UnknownExpansion` · `AlreadyUnlocked` · `PrerequisiteNotMet` · `Locked` ·
`InsufficientItems` · `InsufficientFunds` · `InvalidRegion`

`CanUnlock` runs the same checks and charges nothing, for a land menu. `CollectAvailable` lists
what is buyable now — prerequisite and level met, not yet owned — and deliberately **does not**
filter on affordability, because the plot a player is saving deeds for is exactly the one the menu
should show.

## Save and load

`TownSaveData.UnlockedExpansionIds` is the whole persisted state. Land is permanent, so it only
ever grows.

**Land restores before buildings**, and that ordering is load-bearing: `TownBuildings.RestoreState`
validates each building against the grid, so a building standing on bought land would fail its
placement check and be dropped if the land had not been restored first.
`BuildingsOnBoughtLand_SurviveALoad` is the regression test.

Order rewards are saved with their rolled bonus items, so a deed that appeared on an order cannot
be re-rolled by reloading.

## Open items

- **Regions may not overlap.** Nothing enforces it; overlapping rects would double-charge for the
  same cells. A content validator belongs in the editor tooling.
- **No refund or sale of land**, by design.
- **Deeds have no other sink.** If a second use appears (decorations, say), the drop rate becomes a
  shared lever and needs re-tuning.
- **Deed drop rate is per template.** A pity timer — "guaranteed deed every N orders" — is the
  usual next step once real pacing data exists.
- **Non-rectangular regions** are not supported. A `GridRect` per expansion covers a Township-style
  town; irregular coastlines would want a cell list.
- **Deed payouts are not gated on trusted time.** `TimeTrust` is exposed but nothing refuses to pay
  on an unverified clock — see [TIME_AND_ANTI_CHEAT.md](TIME_AND_ANTI_CHEAT.md).
