# The Vertical Slice

The thinnest playable version of AlphaTown: one scene, one town, and the full loop —
**plant → harvest → deliver → earn → build → unlock land** — running end to end on the same
simulation the tests exercise.

Nothing here is art-directed. Buildings are tinted squares and the HUD is grey panels. The
question the slice exists to answer is whether the *loop* is worth playing, and putting art on it
first would only make a bad loop harder to see.

---

## Getting it running

From a fresh clone, in the Unity Editor:

**AlphaTown ▸ Setup ▸ Build Playable Project**

That runs, in dependency order:

| Step | What it does |
| --- | --- |
| `PlayerSettingsConfigurator` | Android-first player settings |
| `QualityLevelConfigurator` | The three quality levels and their URP assets |
| `MobileUrpConfigurator` | Mobile shadow/AA tuning on those assets |
| `SampleContentBuilder` | ~25 content assets under `Assets/AlphaTown/Content` |
| `MainSceneBuilder` | `Assets/AlphaTown/Scenes/Town.unity`, wired to the database |

Then open `Town.unity` and press Play. The two content steps also have their own menu entries
under **AlphaTown ▸ Content** and can be re-run independently; both update assets in place, so ids
already written into a save stay valid.

> The scene is **generated, not committed**. A `.unity` file is a wall of GUIDs — unreviewable in a
> diff and a merge conflict every time two people touch it. Generating it means the scene's
> contents live in `MainSceneBuilder.cs`, where they can be read and reviewed. Rebuilding is
> destructive: anything placed in the scene by hand is lost, so hand-placed content belongs in a
> prefab the builder instantiates.

---

## What is in the scene

```
Main Camera         Camera (orthographic) + IsoCameraController   pan, pinch-zoom, edge clamping
Directional Light   Light
GameRunner          GameRunner                                    composition root; owns the world
Input               TownSelection, TownGestures,                  one gesture reader; drives the
                    SickleSwipeHarvestController                  camera and the sickle
Town                TownView                                      ground tiles, buildings, selection
HUD                 UIDocument + TownHud                          resource bar, context panel, screens
```

## What you can do

| Action | How |
| --- | --- |
| Look around | Drag with one finger; pinch or scroll to zoom |
| Inspect anything | Tap it — the context panel names it and offers what it can do |
| Plant a field | Tap an empty field ▸ **Plant** |
| Harvest one field | Tap a ripe field ▸ **Harvest** |
| Harvest many fields | Tap a ripe field ▸ **Sickle (n)**, then sweep across the plots |

### The sickle

Harvesting in bulk is a tool you pick up, not a gesture you have to know about.

1. Tap a field that is ready. The context panel offers **Sickle (n)** — *n* is how many fields are
   waiting, which is the whole argument for picking it up.
2. Tap it. Any open panel closes, a green banner appears, and the blade drops into your hand over
   the field you selected.
3. Sweep. Every ripe plot the blade crosses is cut, each at most once per sweep, and the path
   *between* frames is walked so a fast flick does not skip the middle of a row. A single tap cuts
   just that plot.
4. It puts itself away when the last crop is gone, or you tap **Done**.

While the sickle is out, one finger always swings it — **two fingers pan and zoom**, so the map
stays fully navigable and there is no way to get stranded in the mode.

> This replaced an earlier rule where a drag that *began* on a ripe crop became a swipe. It worked,
> but a gesture nobody tells you about reads as broken when it does not fire, and it quietly stole
> panning from every tile that happened to be ready.
| Build | Tap empty land ▸ **Build**, or the **Build** button |
| Upgrade | Tap a building ▸ **Upgrade** (shows its cost, greys out when unaffordable) |
| Deliver an order | **Orders** ▸ **Deliver** |
| Check storage | **Barn** |

Saving happens on every successful action (debounced two seconds), on the auto-save timer, on
app pause and on quit. Closing and reopening resumes exactly where you left off, with everything
that finished while you were away already finished.

---

## The starting town

Four field plots, 500 coins, 10 gems, 4 wheat, and a 50-space barn on an 8×8 patch of a 24×24 map.

| | |
| --- | --- |
| Crops | Wheat (60s), Corn (180s, town level 2) |
| Chains | Wheat ▸ Flour (mill, level 2) ▸ Bread (bakery, level 3) |
| Orders | 4 helicopter slots, cooldowns 120/180/240/300s, 30% chance of a land deed |
| Land | Three 8×8 parcels gated on 1, 2 and 3 deeds plus coins |
| Levels | Eight, at 60 / 150 / 320 / 620 / 1100 / 1900 / 3200 / 5000 XP |

Every number is a placeholder chosen to make the loop legible inside a few minutes rather than a
few days. Crops finish in a minute because a four-hour wheat field cannot be evaluated in a
sitting. **None of it is a balance pass**, and none of it is referenced from code — the database is
the only thing that names these ids.

---

## Where the loop is likely to feel thin

I have not played this. There is no Unity Editor in this environment, so the honest version of
"what felt good and what felt bad in testing" is that I cannot produce it — what follows is a
read of the design, and the first session on a device should be treated as the real test.

**Likely to work.** The sickle swipe is the strongest thing in the slice: sweeping a finger over
six ripe fields is a genuinely better verb than six taps, and it is the one interaction that is
about *feel* rather than information. Level-2 auto-replant compounds it — after the upgrade, one
sweep both harvests and re-sows, which is the moment the farm should start feeling like it runs
itself. Placing the resource bar above and the context panel below leaves the middle of the screen
clear, so the town stays draggable with one thumb.

**Likely to feel thin.**

1. **The first two minutes have nothing to do.** Four fields at 60 seconds means you plant, and
   then you wait a minute with no second verb. Township covers this gap with a tutorial and an
   instantly-completable first order. The slice has neither.
2. **Waiting is unpriced.** Nothing costs gems, so the hard currency is decoration and there is no
   speed-up. The one thing a player wants when a timer is running is a way to skip it.
3. **Orders are anonymous.** A helicopter order is a list of items and a payout with no character
   asking for it. Township's orders have faces and names, and that is not decoration — it is what
   makes a delivery feel like it went somewhere.
4. **Nothing is celebrated.** A level-up, a completed delivery and a failed tap all produce the
   same small grey toast. The three biggest moments in the loop are indistinguishable from an error
   message.
5. **The barn fills and the game just stops.** The bottleneck works — it pushes you towards orders
   — but it announces itself by silently refusing a harvest. The bar turns red, which is not
   enough.
6. **Coins have almost nowhere to go.** Buildings and land are the only sinks. Once you own the
   three buildings, coins accumulate with no decision attached to them.
7. **The map is bigger than the game.** A 24×24 town with three unlockable parcels around a used
   patch reads as mostly empty, which makes early expansion feel like buying more nothing.

## Most obvious next improvements

Roughly in order of value per hour spent:

1. **Feedback on the three good moments** — a coin burst on delivery, a level-up banner, a pop on
   each field the sickle cuts. Cheapest change with the largest effect on how the loop feels.
2. **Sprites for crops and buildings.** `RecipeDefinition` already carries `GrowthStageSprites` and
   `BuildingDefinition` carries `Icon`/`MapSprite`; both are wired through to the view and just need
   art dropped in. Watching wheat actually grow is most of what makes a field worth looking at.
3. **A first-run sequence** — one field pre-planted and about to ripen, and a first order the
   starting wheat already covers. Turns a minute of waiting into a minute of doing.
4. **Speed-ups for gems**, with the price derived from remaining time. `Producer.TryFinishNow` and
   `TrySpeedUp` already exist; they need a price and a button.
5. **Harvest All**, driven by `TownCommands.CollectHarvestable`, for when the town outgrows a
   comfortable swipe.
6. **A market stall** — sell surplus goods for coins at a poor rate. Gives the barn an escape valve
   and stops a full barn from being a dead end.
7. **Named order customers**, even as placeholder portraits and three lines of flavour text.
8. **Shrink the map, or seed more of it.** Either start on a smaller grid or place decorations and
   obstacles in the locked parcels so unlocking one reveals something.

---

## Input backends

The slice works whatever **Project Settings ▸ Player ▸ Active Input Handling** is set to. It did
not always: everything read the legacy `UnityEngine.Input` class, which throws at runtime — not at
compile time — when the setting is "Input System Package (New)", the Unity 6 default. In an APK
built that way, pan, zoom and swipe were all dead while the HUD kept working, because UI Toolkit
speaks both backends.

`PointerInput` is now the only thing that names a backend, and the Input System half lives in an
assembly Unity skips entirely when the package is absent. See
[ARCHITECTURE.md](ARCHITECTURE.md#input-backends).

If nothing responds to touch at all, look for one line in logcat:

```
adb logcat -s Unity | grep AlphaTown
```

`[AlphaTown][Input] No pointer source is available` means neither backend is usable — set Active
Input Handling to **Both** and restart the Editor.

## What the slice deliberately leaves out

No tutorial, no sound, no animation, no particles beyond an optional swipe trail hook, no
monetisation, no cloud save, no localisation table (`DisplayNames` prettifies keys instead), and
no UXML/USS — the HUD is built in C# with inline styles so it compiles and runs from source with
no asset GUIDs to go missing.

Each of those is a deliberate omission for a first playable, and each is called out with a `TODO`
at the point in the code where it would land.
