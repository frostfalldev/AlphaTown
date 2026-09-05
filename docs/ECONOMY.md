# AlphaTown — the economic loop

Three systems close the first loop:

```
        ┌────────────────────────────────────────────────────────────┐
        │                                                            │
        ▼                                                            │
   ┌─────────┐   goods   ┌────────────┐  coins   ┌────────┐          │
   │ Producer│──────────▶│ OrderBoard │─────────▶│ Wallet │          │
   └─────────┘           └────────────┘          └────────┘          │
        ▲                       │                     ▲              │
        │                       │ XP                  │ level        │
        │                       ▼                     │ rewards      │
        │                ┌──────────────┐             │              │
        │   unlocks      │TownProgression│────────────┘              │
        └────────────────│  (IUnlockGate)│                           │
                         └──────────────┘                           │
                                 │  widens the order pool           │
                                 └──────────────────────────────────┘
```

Produce → deliver → earn coins and XP → level up → unlock more recipes → produce more.
`EconomicLoopTests.FullLoop_ProduceDeliverEarnUnlock` walks the whole circuit headlessly.

---

## Currency and the Wallet

**Currency is never an item and never enters the barn.** It has no storage cost, no stack
size, and — unlike items — every movement must carry an attribution reason.

`CurrencyDefinition` assets describe each currency: `Soft` (coins), `Hard` (gems), or `Event`.
The kind is not cosmetic: hard currency is bought with real money, so its sinks need auditing
and its faucets need reconciling against revenue.

### Reason codes are mandatory

There is no overload that moves currency anonymously. Every entry point takes a reason:

```csharp
wallet.Grant(coins, 250, CurrencySource.OrderReward, order.OrderId);
wallet.TrySpend(gems, 12, CurrencySink.ProductionSpeedUp, producer.InstanceId);
```

`CurrencySource` and `CurrencySink` are deliberately **separate enums** rather than one merged
reason type, so the compiler enforces the split — a sink cannot be passed to a grant. Source/sink
balance is the number economy tuning actually runs on.

Both live in `AlphaTown.Data.Economy`, not Gameplay, so the analytics service in Services can
speak the vocabulary without an upward reference.

An untagged movement logs a warning and is **still recorded**, under `Unknown`. Losing the
attribution is a bug; losing the money would be worse, and a faucet that vanishes from the
numbers is invisible.

### Source/sink tracking

`CurrencyLedger` keeps lifetime totals per (currency × reason). It is bounded by the number of
reasons the game actually uses — a few dozen — so it never grows with play time, which is why
it can be persisted while individual transactions cannot.

```csharp
ledger.TotalFrom(coins, CurrencySource.OrderReward);   // faucet size
ledger.TotalTo(coins, CurrencySink.BuildingPurchase);  // sink size
ledger.TotalEarned(coins) - ledger.TotalSpent(coins);  // must equal the balance
```

That last identity is asserted in the tests. If it ever drifts, currency is being created or
destroyed somewhere outside the wallet.

Individual transactions go out as `CurrencyTransactionEvent`. An analytics adapter in Gameplay
subscribes and forwards to a Services-side sink; the payload is all `Data` types, so nothing
in Services needs to see a Gameplay type.

### Events worth knowing

| Event | Why it exists |
| --- | --- |
| `CurrencyBalanceChangedEvent` | What UI binds to. |
| `CurrencyTransactionEvent` | Full attribution, for analytics. |
| `CurrencyCappedEvent` | A grant was clipped by the currency's cap; the excess is lost. |
| `CurrencySpendRejectedEvent` | The strongest purchase-intent signal in the game — the hook an offer surface listens on. |

---

## Progression

`ProgressionCurve` is a ScriptableObject: XP per level, plus what reaching each level pays out.
Never a formula in code — pacing is the biggest retention lever in this genre and gets retuned
constantly.

`TownProgression` grants XP with an `XpSource` reason code and cascades through as many levels as
the grant covers, firing one `TownLevelUpEvent` per level. A single grant raising two or three
levels is normal after a long absence, so anything celebrating a level up must cope with a burst.

**XP earned at the level cap is banked, not discarded.** Caps get raised in live-ops updates, and
raising one should immediately credit what players earned against it.

### The unlock gate

`ITownProgression` extends `IUnlockGate`, a deliberately narrow interface:

```csharp
bool IsRecipeUnlocked(IRecipeDefinition recipe);   // TownLevel >= recipe.UnlockLevel
```

`Producer` takes the gate, not the whole progression system — so production can be tested against
a fixed level with no curve, wallet or event bus in sight. The check runs **inside the
simulation**, not in a screen: an unlock enforced only in UI is one a replayed or crafted command
walks straight past.

---

## Orders

`OrderTemplateDefinition` describes the *shape* of an order — how many item types, what
quantities, the time limit, the payout multipliers — not which items. The generator fills those
in from **the outputs of recipes the player has unlocked**.

That is the design decision worth keeping: the player can only ever be asked for something they
can actually make, and the property holds automatically as content grows. No template needs
revisiting when a recipe ships.

### Rewards

```
coins = Σ(item.CoinValue × quantity) × template.CoinMultiplier
xp    = Σ(item.XpValue   × quantity) × template.XpMultiplier
```

`IItemDefinition.CoinValue` is therefore the single number that prices an item across the whole
economy. Change it and every order re-prices with it.

Rewards are **baked into the order at generation time**, not recomputed on completion. If item
values are retuned in a live-ops update, orders already on the board still pay what they promised
— a player who stockpiled goods for a visible reward has to get that reward.

A payout never rounds down to nothing: an order that asks for goods and pays zero coins reads as
a bug whatever the multiplier says.

### Time limits and expiry

Absolute timestamps, like everything else. A board left alone for a week resolves in a single
`Sync()`; nothing needs to have been running. Expiry fires `OrderExpiredEvent` on the next sync,
which after a long absence can mean an event for an order that ran out days ago.

### Order kinds

`OrderKind` names Helicopter, Train, Ship and Event. Only **Helicopter** is wired up —
one `OrderBoard` instance with a capacity of four. The others are named now so order data, save
data and analytics do not need reshaping when they land; each becomes another `OrderBoard`.

---

## Tuning levers

Everything below is data, changeable without a code change:

| Lever | Where |
| --- | --- |
| Item price and XP worth | `ItemDefinition.CoinValue` / `XpValue` |
| Level pacing and level rewards | `ProgressionCurve` |
| Recipe gating | `RecipeDefinition.UnlockLevel` |
| Order size, timers, payout scaling | `OrderTemplateDefinition` |
| Starting balances and currency caps | `CurrencyDefinition` |
| Build and upgrade costs, build times | `BuildingDefinition` levels |
| Buildable town size | `TownDefinition` |
| Order slot count, cooldowns and unlock level | `OrderBoardDefinition` |
| How many boards exist at all | add an `OrderBoardDefinition` to the database |
| Reroll floor and reward percentage | `OrderBoardDefinition` |
| Land deed drop rate | `OrderTemplateDefinition` bonus items and chance |
| Land cost and unlock order | `ExpansionDefinition` |
| Auto-replant, queue size, speed | `ProducerDefinition` levels |
| Barn capacity per level | `StorageDefinition` |
| Market price per item | `ItemDefinition.SellValue` (0 = a fraction of `CoinValue`) |
| Which barn level a building grants | `BuildingDefinition` level `StorageLevel` |
| XP for finishing a build | `BuildingDefinition` level `XpReward` |

Board capacity is still a constant (`GameWorld.HelicopterBoardCapacity`). It moves into data when
board upgrades exist.

## Storage as a sink

The barn is the loop's bottleneck: it fills, harvesting stops, and the way out is to deliver. That
only works if the bottleneck can be relieved, so a building level can grant a barn level through
`IBuildingLevel.StorageLevel`, and `GameWorld.ApplyStorageUpgrades` sizes the barn to the best one
standing.

Three rules make it behave:

- **A maximum, not a sum.** Storage is a tier the player reached. Ten cheap granaries would
  otherwise be the whole economy.
- **Recomputed every sync, not applied once.** A restored save, a level retuned in content, and a
  build that completed while the app was closed all reach the same answer with no special case and
  no migration.
- **It only ever raises.** Shrinking the barn below what is already in it would strand goods the
  player earned, so demolishing a granary keeps the space it bought.

It is a coin sink with a shape the others do not have: buying it does not add income, it removes a
reason to stop playing.

## The market

Surplus goods sell for coins at about a third of their base worth, against the roughly 1.7x an
order pays — so selling nets a fifth of delivering. **That gap is the design.** The barn filling is
what sends a player to the order board, and that only works while delivering is obviously the
better deal.

What it buys is that a barn full of the wrong goods is never a dead end. There is always a move,
and it always costs something.

Two things it will not take:

- **Anything that costs no barn space.** Land deeds are the expansion gate wearing an item's
  clothes; a market that bought them for a coin each would quietly delete that gate.
- **Anything worth nothing.** The one-coin minimum is there for rounding, not to conjure value
  that was never there.

It lives on the barn screen rather than a shop of its own, because there is no moment when a player
wants to sell that is not the moment they are staring at a full barn.

### Buying, and why it does not break the game

Buying is the other direction, and the loop's only real coin sink. It is priced at 250% of an
item's coin value — **above** the ~1.7x an order pays — so filling an order with bought goods loses
money. That single inequality is what keeps production from becoming optional: buying is a tax on
impatience, never a strategy.

What the player gets for the loss is the XP, the deed roll and the freed slot. Reasons enough to
skip a wait; not reasons to stop farming. Drop the markup below the order multiplier and the game
becomes a spreadsheet.

Three guards, all tested:

- **Land deeds cannot be bought.** Land is gated by deeds rather than coins by design — see
  [EXPANSION.md](EXPANSION.md) — and a market that sold deeds would turn expansion straight back
  into a coin purchase. They cost no barn space, which is what marks them as not merchandise.
- **No round trip can profit.** The buy price is forced above the sell price whatever the content
  says, so an item priced generously to sell cannot be bought back for less. An economy with a
  money printer in it has no other numbers worth tuning.
- **Nothing is charged for goods that will not fit.** Room, price and balance are all checked
  before either half of the exchange moves.

The only place to buy is the order card, on a request line the barn cannot cover, showing the
price on the button rather than behind it — because the answer to "should I buy this?" is nearly
always no, and the player deserves to see that before tapping.

## Two boards

One `OrderBoard` per authored `OrderBoardDefinition`, so a third is a content change rather than a
code change. The kind an order belongs to picks its templates, and everything else — pacing,
payout, reroll price, unlock level — is per board.

| | Helicopter | Train |
| --- | --- | --- |
| Opens at | level 1 | level 3 |
| Slots | 4 | 3 |
| Cooldowns | 2–5 min | 15–40 min |
| Asks for | 1–2 goods, 2–8 each | 2–3 goods, 6–14 each |
| Pays | 1.7x coins, 1x XP | 2.6x coins, 1.8x XP |
| Deed chance | 30%, one deed | 60%, two deeds |

The difference between them is the decision. The helicopter board is the everyday faucet: small,
quick, forgiving, and it will take whatever you have. The train asks for goods in bulk and pays
half again as well, on cooldowns long enough that you cannot live off it — which turns "dump
everything into helicopter orders" into "hold some back". It is also where most land deeds come
from, so expansion pace is tied to the board that rewards patience.

A locked board generates nothing and starts no cooldowns, so reaching level 3 hands over all three
slots at once. Order ids are scoped to their board (`train_board.order_4`), because two boards
counting from one would otherwise mint the same id twice — and an id matching two orders means
delivering one could complete the other.

## Rerolling: the recurring coin decision

Buildings and land are one-off purchases, and buying at the market is deliberately a bad deal — so
between them coins accumulated with no choice attached. Rerolling is the answer, and it is the only
coin sink here that **cannot bend the goods economy at all**: it moves no items, it only changes
which order is on offer.

What the coins buy is the slot's cooldown. Discarding an order has always been free and leaves the
slot cooling like any other, so the wait is the only thing left worth selling — which is also why
the price feels fair rather than arbitrary.

The price is a percentage of what the order would have paid, floored by a base cost:

```
cost = max(RerollBaseCost, order's coin reward × RerollCostPercent / 100)
```

Priced against the reward because that is what the player is giving up: dodging a lucrative order
they cannot fill should cost more than clearing a trivial one. The floor exists because a free
reroll turns the board into a slot machine you pull until it pays.

No new state is persisted — the cost is derived from the order, which is already saved.

### Why not a daily-deal shop

The obvious alternative was a rotating shop selling goods at a discount. It does not work here, and
the reason is worth recording: **every deal would have to stay priced above what an order pays**, or
buying stock and delivering it becomes profitable and production becomes optional. A shop whose
every offer is still a loss is not a shop, it is a menu of bad options.

Rerolling has no such tension, because it never introduces goods.

## What is deliberately missing

- **Rerolling is priced.** See below.
- **Sinks are buildings, land and the market.** `BuildingPurchase`, `BuildingUpgrade`,
  `ExpansionPurchase` and `MarketPurchase` are all wired up. Land stays gated by deeds rather than
  coins on purpose — see [EXPANSION.md](EXPANSION.md).
- **Slot pacing has landed.** Each board slot now cools before refilling, authored per slot in
  `OrderBoardDefinition` — see [FARMING_AND_PACING.md](FARMING_AND_PACING.md).
- **Speed-ups are not priced.** `Producer.TrySpeedUp` and `TryFinishNow` work; what they cost
  in gems is undesigned.
