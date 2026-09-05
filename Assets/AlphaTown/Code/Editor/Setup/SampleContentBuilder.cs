using AlphaTown.Data.Buildings;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Expansion;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Production;
using AlphaTown.Data.Progression;
using AlphaTown.Data.Recipes;
using AlphaTown.Data.Storage;
using AlphaTown.Data.Town;
using UnityEditor;
using UnityEngine;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Generates a starting town's worth of content: two crops and a hen coop, a mill, a bakery
    /// and a patisserie, a granary, two decorations, a helicopter and a train board, three parcels
    /// of land, and the database that ties them together.
    ///
    /// This exists because the slice needs numbers to be playable, and hand-authoring thirty
    /// interlinked assets before the loop can be tried once is the wrong order to find out the
    /// loop is wrong. Everything here is a placeholder chosen to make the loop legible in a few
    /// minutes of play, not a balance pass: crops finish in a minute rather than four hours, and
    /// the first order is affordable almost immediately.
    ///
    /// <b>An asset that already exists is left alone.</b> Once a designer has opened one of these
    /// and tuned it, it is theirs, and a build script silently reverting that tuning is how people
    /// learn not to trust the tooling. Only what is missing gets written, so the generator becomes
    /// a way to fill gaps rather than a thing to be afraid of. Use the overwrite entry point to
    /// deliberately reset to the shipped defaults.
    ///
    /// Ids are never regenerated for an existing asset, so a save file's references stay valid.
    ///
    /// TODO(content): replace wholesale once real crops, buildings and art exist. Nothing in code
    /// depends on these ids — the database is the only thing that names them.
    /// </summary>
    internal static class SampleContentBuilder
    {
        const string Root = "Assets/AlphaTown/Content";

        /// <summary>
        /// Set for the duration of one run. Editor-only and single-threaded, so a static is
        /// honest here and saves threading a flag through every builder below.
        /// </summary>
        static bool _overwriteExisting;

        static int _written;
        static int _skipped;

        [MenuItem("AlphaTown/Content/Build Sample Content", false, 20)]
        internal static void Build() => Run(overwriteExisting: false);

        /// <summary>
        /// Resets every sample asset to the values in this file, discarding hand-authored changes.
        /// Behind a confirmation because it is the one entry point that can lose work.
        /// </summary>
        [MenuItem("AlphaTown/Content/Rebuild Sample Content (overwrite)", false, 22)]
        internal static void Rebuild()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "AlphaTown — Rebuild Sample Content",
                    "This resets every generated asset under " + Root + " to the values in " +
                    "SampleContentBuilder.cs.\n\nAny tuning done in the Inspector will be lost.\n\n" +
                    "Assets you created yourself are not touched.",
                    "Overwrite", "Cancel"))
            {
                return;
            }

            Run(overwriteExisting: true);
        }

        static void Run(bool overwriteExisting)
        {
            _overwriteExisting = overwriteExisting;
            _written = 0;
            _skipped = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                Generate();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _overwriteExisting = false;
            }

            Debug.Log("[AlphaTown] Sample content: " + _written + " asset(s) written, " +
                      _skipped + " left alone, under " + Root + "." +
                      (_skipped > 0
                          ? "\n  Existing assets are never overwritten. Use AlphaTown ▸ Content ▸ " +
                            "Rebuild Sample Content (overwrite) to reset them to the shipped defaults."
                          : string.Empty));
        }

        /// <summary>
        /// Opens an asset for writing, creating it if missing.
        ///
        /// Returns null when the asset already existed and is being left alone — every builder
        /// below returns early on null, which is what keeps the skip logic in one place instead of
        /// repeated fifteen times.
        /// </summary>
        static SerializedObject BeginAuthoring<TAsset>(string path, out TAsset asset)
            where TAsset : ScriptableObject
        {
            asset = AssetAuthoring.CreateOrLoad<TAsset>(path, out var created);

            if (!created && !_overwriteExisting)
            {
                _skipped++;
                return null;
            }

            _written++;
            return AssetAuthoring.Edit(asset);
        }

        static void Generate()
        {
            // --- Currency ----------------------------------------------------------------------
            var coins = Currency("coins", CurrencyKind.Soft, startingAmount: 500);
            var gems = Currency("gems", CurrencyKind.Hard, startingAmount: 10);

            // --- Items -------------------------------------------------------------------------
            var wheat = Item("wheat", ItemCategory.Crop, coinValue: 3, xpValue: 2);
            var corn = Item("corn", ItemCategory.Crop, coinValue: 6, xpValue: 4);
            var flour = Item("flour", ItemCategory.Ingredient, coinValue: 14, xpValue: 7);
            var bread = Item("bread", ItemCategory.FinishedGood, coinValue: 34, xpValue: 16);
            var eggs = Item("eggs", ItemCategory.AnimalProduce, coinValue: 9, xpValue: 5);
            var cake = Item("cake", ItemCategory.FinishedGood, coinValue: 88, xpValue: 38);

            // Deeds are unstorable on purpose: they are a currency wearing an item's clothes, and
            // charging barn space for the thing that buys more land would be a cruel joke.
            var deed = Item("land_deed", ItemCategory.Special, coinValue: 0, xpValue: 0, storable: false);

            // --- Recipes -----------------------------------------------------------------------
            // Crops take no inputs — a field is a producer whose input list is empty, which is the
            // whole reason farming needed no system of its own.
            var growWheat = Recipe("grow_wheat", 60, unlockLevel: 1,
                outputs: new[] { new Ingredient(wheat, 2) }, bonusOutputMax: 1);

            var growCorn = Recipe("grow_corn", 180, unlockLevel: 2,
                outputs: new[] { new Ingredient(corn, 2) }, bonusOutputMax: 2);

            var millFlour = Recipe("mill_flour", 120, unlockLevel: 1,
                inputs: new[] { new Ingredient(wheat, 3) },
                outputs: new[] { new Ingredient(flour, 1) });

            var bakeBread = Recipe("bake_bread", 300, unlockLevel: 3,
                inputs: new[] { new Ingredient(flour, 2), new Ingredient(corn, 1) },
                outputs: new[] { new Ingredient(bread, 1) });

            // Eggs need no input either — a coop is a field that happens to hold chickens. Slower
            // than wheat, and worth three times as much, so it is the reason to want a second
            // kind of plot rather than a fifth field.
            var collectEggs = Recipe("collect_eggs", 240, unlockLevel: 2,
                outputs: new[] { new Ingredient(eggs, 2) }, bonusOutputMax: 1);

            // The deepest chain in the sample: two farms and a mill all feed this one building.
            var bakeCake = Recipe("bake_cake", 480, unlockLevel: 4,
                inputs: new[] { new Ingredient(flour, 2), new Ingredient(eggs, 3) },
                outputs: new[] { new Ingredient(cake, 1) });

            // --- Producers ---------------------------------------------------------------------
            // Level 2 is where auto-replant arrives: the field keeps sowing itself once the player
            // has emptied it, which is the upgrade that turns a chore into a routine.
            var field = Producer("field", new[] { growWheat, growCorn }, new[]
            {
                new ProducerTier(queueCapacity: 1, parallelSlots: 1, speed: 1f, autoRepeat: false),
                new ProducerTier(queueCapacity: 1, parallelSlots: 1, speed: 1.25f, autoRepeat: true)
            });

            var mill = Producer("mill", new[] { millFlour }, new[]
            {
                new ProducerTier(queueCapacity: 2, parallelSlots: 1, speed: 1f, autoRepeat: false),
                new ProducerTier(queueCapacity: 4, parallelSlots: 2, speed: 1.2f, autoRepeat: false)
            });

            var bakery = Producer("bakery", new[] { bakeBread }, new[]
            {
                new ProducerTier(queueCapacity: 2, parallelSlots: 1, speed: 1f, autoRepeat: false)
            });

            // Like a field, the coop gets auto-collect at level 2 — the upgrade that stops a
            // second kind of plot from doubling the tapping.
            var coop = Producer("coop", new[] { collectEggs }, new[]
            {
                new ProducerTier(queueCapacity: 1, parallelSlots: 1, speed: 1f, autoRepeat: false),
                new ProducerTier(queueCapacity: 1, parallelSlots: 1, speed: 1.3f, autoRepeat: true)
            });

            var patisserie = Producer("patisserie", new[] { bakeCake }, new[]
            {
                new ProducerTier(queueCapacity: 2, parallelSlots: 1, speed: 1f, autoRepeat: false),
                new ProducerTier(queueCapacity: 3, parallelSlots: 2, speed: 1.15f, autoRepeat: false)
            });

            // --- Storage and progression -------------------------------------------------------
            var barn = Storage("barn", new[] { 50, 75, 110, 160, 240 });
            var curve = Curve(coins, gems);

            // --- Buildings ---------------------------------------------------------------------
            var plot = Building("field_plot", BuildingCategory.Farming, 1, 1, field, unlockLevel: 1,
                new[]
                {
                    new BuildingTier(constructionSeconds: 0, coins: coins, coinCost: 50, xpReward: 5),
                    new BuildingTier(constructionSeconds: 30, coins: coins, coinCost: 250, xpReward: 30)
                },
                placeholder: new Color(0.45f, 0.34f, 0.22f));

            var millBuilding = Building("mill_house", BuildingCategory.Production, 2, 2, mill, unlockLevel: 2,
                new[]
                {
                    new BuildingTier(constructionSeconds: 60, coins: coins, coinCost: 300, xpReward: 30),
                    new BuildingTier(constructionSeconds: 180, coins: coins, coinCost: 1200, xpReward: 110)
                },
                placeholder: new Color(0.72f, 0.66f, 0.52f));

            var bakeryBuilding = Building("bakery", BuildingCategory.Production, 2, 2, bakery, unlockLevel: 3,
                new[]
                {
                    new BuildingTier(constructionSeconds: 120, coins: coins, coinCost: 900, xpReward: 60)
                },
                placeholder: new Color(0.78f, 0.48f, 0.36f));

            var coopBuilding = Building("chicken_coop", BuildingCategory.Farming, 2, 2, coop, unlockLevel: 2,
                new[]
                {
                    new BuildingTier(constructionSeconds: 60, coins: coins, coinCost: 400, xpReward: 25),
                    new BuildingTier(constructionSeconds: 300, coins: coins, coinCost: 1400, xpReward: 90)
                },
                placeholder: new Color(0.86f, 0.74f, 0.46f));

            var patisserieBuilding = Building("patisserie", BuildingCategory.Production, 3, 2, patisserie, unlockLevel: 4,
                new[]
                {
                    new BuildingTier(constructionSeconds: 300, coins: coins, coinCost: 3500, xpReward: 150),
                    new BuildingTier(constructionSeconds: 900, coins: coins, coinCost: 9000, xpReward: 400)
                },
                placeholder: new Color(0.82f, 0.60f, 0.72f));

            // Decorations produce nothing and store nothing. They exist to be somewhere for coins
            // to go, and they pay XP for it — without that reward there would be no reason to
            // raise one, which is why construction XP had to exist before these could.
            //
            // The fountain is built first because the flower bed upgrades into it: a small bed
            // becoming a centrepiece is the one place the sample content exercises the
            // transform-into-another-definition path that BuildingDefinition already supports.
            // The granary is the only thing that grows the barn, and the barn filling is what
            // sends the player to the order board. Its four levels walk the storage definition's
            // capacities from 75 up to 240; without it the player is stuck on the 50 they start
            // with however long they play.
            //
            // Level 2 unlock, because the squeeze arrives early and the answer to it should too.
            var granary = Building("granary", BuildingCategory.Storage, 2, 2, null, unlockLevel: 2,
                new[]
                {
                    new BuildingTier(60, coins, coinCost: 600, xpReward: 40, storageLevel: 2),
                    new BuildingTier(300, coins, coinCost: 2200, xpReward: 120, storageLevel: 3),
                    new BuildingTier(900, coins, coinCost: 7000, xpReward: 320, storageLevel: 4),
                    new BuildingTier(1800, coins, coinCost: 18000, xpReward: 800, storageLevel: 5)
                },
                placeholder: new Color(0.64f, 0.52f, 0.36f));

            var fountain = Building("fountain", BuildingCategory.Decoration, 2, 2, null, unlockLevel: 3,
                new[]
                {
                    new BuildingTier(constructionSeconds: 60, coins: coins, coinCost: 2000, xpReward: 120)
                },
                placeholder: new Color(0.62f, 0.74f, 0.86f));

            var flowerBed = Building("flower_bed", BuildingCategory.Decoration, 1, 1, null, unlockLevel: 1,
                new[]
                {
                    new BuildingTier(constructionSeconds: 0, coins: coins, coinCost: 150, xpReward: 10)
                },
                placeholder: new Color(0.80f, 0.44f, 0.56f),
                upgradesInto: fountain);

            // --- Orders ------------------------------------------------------------------------
            // Deeds drop from orders and nowhere else, which is what keeps expansion paced by
            // playing rather than by saving up.
            //
            // Two boards, and the difference between them is the decision. The helicopter is the
            // everyday faucet: small, quick, forgiving. The train asks for two or three goods in
            // bulk and pays half again as well, on cooldowns long enough that you cannot live off
            // it — which turns "dump everything into helicopter orders" into "hold some back".
            var template = OrderTemplate("helicopter_basic", deed, OrderKind.Helicopter,
                unlockLevel: 1, minTypes: 1, maxTypes: 2, minQuantity: 2, maxQuantity: 8,
                coinMultiplier: 1.7f, xpMultiplier: 1f, bonusChance: 0.3f);

            var board = OrderBoard("helicopter_board", OrderKind.Helicopter,
                new[] { 120, 180, 240, 300 }, unlockLevel: 1, rerollBaseCost: 40);

            var trainTemplate = OrderTemplate("train_bulk", deed, OrderKind.Train,
                unlockLevel: 3, minTypes: 2, maxTypes: 3, minQuantity: 6, maxQuantity: 14,
                coinMultiplier: 2.6f, xpMultiplier: 1.8f, bonusChance: 0.6f, deedCount: 2);

            var trainBoard = OrderBoard("train_board", OrderKind.Train,
                new[] { 900, 1500, 2400 }, unlockLevel: 3, rerollBaseCost: 250);

            // --- Land --------------------------------------------------------------------------
            var town = Town(width: 24, height: 24, startX: 8, startY: 8, startWidth: 8, startHeight: 8);

            var north = Expansion("land_north", 8, 16, 8, 8, deed, deedCost: 1, coins: coins, coinCost: 500,
                requires: null, unlockLevel: 2, sortOrder: 0);

            var east = Expansion("land_east", 16, 8, 8, 8, deed, deedCost: 2, coins: coins, coinCost: 1500,
                requires: null, unlockLevel: 3, sortOrder: 1);

            var northEast = Expansion("land_north_east", 16, 16, 8, 8, deed, deedCost: 3, coins: coins,
                coinCost: 4000, requires: north, unlockLevel: 4, sortOrder: 2);

            // --- New game ----------------------------------------------------------------------
            // Back to the smallest barn now that the granary exists to grow it. Starting a level
            // up was a workaround for having no upgrade path; with one, the early squeeze is the
            // point — it is what makes the first granary feel like a relief rather than a
            // formality.
            var newGame = NewGame(barnLevel: 1,
                items: new[] { new Ingredient(wheat, 4) },
                buildings: new[]
                {
                    new StartingSpot(plot, 9, 9), new StartingSpot(plot, 11, 9),
                    new StartingSpot(plot, 9, 11), new StartingSpot(plot, 11, 11)
                });

            // --- Database ----------------------------------------------------------------------
            // The one asset that is never skipped. It is an index, not content: skipping it would
            // leave a freshly created definition sitting on disk that the game cannot see, which
            // looks exactly like the generator not having run at all.
            //
            // So it merges instead. Anything already listed stays listed, in its existing order,
            // and only what is missing is appended — a building added by hand survives, and a
            // generated one that was deleted comes back.
            var database = AssetAuthoring.CreateOrLoad<GameDatabase>(Root + "/GameDatabase.asset");
            var serialized = AssetAuthoring.Edit(database);

            Register(serialized, "_items", new Object[] { wheat, corn, eggs, flour, bread, cake, deed });
            Register(serialized, "_recipes",
                new Object[] { growWheat, growCorn, collectEggs, millFlour, bakeBread, bakeCake });
            Register(serialized, "_producers", new Object[] { field, coop, mill, bakery, patisserie });
            Register(serialized, "_storages", new Object[] { barn });
            Register(serialized, "_currencies", new Object[] { coins, gems });
            Register(serialized, "_orderTemplates", new Object[] { template, trainTemplate });
            Register(serialized, "_buildings", new Object[]
            {
                plot, coopBuilding, millBuilding, bakeryBuilding, patisserieBuilding,
                granary, flowerBed, fountain
            });
            Register(serialized, "_orderBoards", new Object[] { board, trainBoard });
            Register(serialized, "_expansions", new Object[] { north, east, northEast });

            // The well-known slots name which of the above the game reaches for by default. An
            // author who has pointed one somewhere else has made a decision; filling only the
            // empty ones respects it while still healing a slot nothing occupies.
            Nominate(serialized, "_defaultStorage", barn);
            Nominate(serialized, "_softCurrency", coins);
            Nominate(serialized, "_hardCurrency", gems);
            Nominate(serialized, "_progressionCurve", curve);
            Nominate(serialized, "_townDefinition", town);
            Nominate(serialized, "_newGame", newGame);
            AssetAuthoring.Apply(serialized);
        }

        static void Register(SerializedObject serialized, string field, Object[] entries)
        {
            if (_overwriteExisting) AssetAuthoring.SetReferenceArray(serialized, field, entries);
            else AssetAuthoring.MergeReferenceArray(serialized, field, entries);
        }

        static void Nominate(SerializedObject serialized, string field, Object value)
        {
            if (_overwriteExisting) AssetAuthoring.SetReference(serialized, field, value);
            else AssetAuthoring.SetReferenceIfEmpty(serialized, field, value);
        }

        // --- Builders ---------------------------------------------------------------------------

        static CurrencyDefinition Currency(string id, CurrencyKind kind, int startingAmount)
        {
            var serialized = BeginAuthoring<CurrencyDefinition>(
                Root + "/Economy/Currency_" + id + ".asset", out var asset);

            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.Set(serialized, "_displayNameKey", "currency." + id);
            AssetAuthoring.SetEnum(serialized, "_kind", (int)kind);
            AssetAuthoring.Set(serialized, "_startingAmount", startingAmount);
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static ItemDefinition Item(string id, ItemCategory category, int coinValue, int xpValue,
                                   bool storable = true)
        {
            var folder = category == ItemCategory.Crop ? "/Crops/Item_" : "/Goods/Item_";
            var serialized = BeginAuthoring<ItemDefinition>(Root + folder + id + ".asset", out var asset);
            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.Set(serialized, "_displayNameKey", "item." + id);
            AssetAuthoring.SetEnum(serialized, "_category", (int)category);
            AssetAuthoring.Set(serialized, "_storageCost", 1);
            AssetAuthoring.Set(serialized, "_isStorable", storable);
            AssetAuthoring.Set(serialized, "_coinValue", coinValue);
            AssetAuthoring.Set(serialized, "_xpValue", xpValue);
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static RecipeDefinition Recipe(string id, int durationSeconds, int unlockLevel,
                                       Ingredient[] outputs,
                                       Ingredient[] inputs = null,
                                       int bonusOutputMax = 0)
        {
            var serialized = BeginAuthoring<RecipeDefinition>(
                Root + "/Recipes/Recipe_" + id + ".asset", out var asset);

            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.Set(serialized, "_durationSeconds", durationSeconds);
            AssetAuthoring.Set(serialized, "_unlockLevel", unlockLevel);
            AssetAuthoring.Set(serialized, "_bonusOutputMax", bonusOutputMax);
            WriteItemAmounts(serialized, "_inputs", inputs);
            WriteItemAmounts(serialized, "_outputs", outputs);
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        /// <summary>
        /// An item and a quantity. A named struct rather than a tuple, matching
        /// <see cref="ProducerTier"/> and <see cref="BuildingTier"/> below — and because arrays of
        /// named tuples are one of the few C# 7 constructs Mono's compiler still mishandles, which
        /// would put this file out of reach of the headless build in tools/headless.
        /// </summary>
        readonly struct Ingredient
        {
            public readonly ItemDefinition Item;
            public readonly int Count;

            public Ingredient(ItemDefinition item, int count)
            {
                Item = item;
                Count = count;
            }
        }

        /// <summary>A building and where it stands when a new town is seeded.</summary>
        readonly struct StartingSpot
        {
            public readonly BuildingDefinition Building;
            public readonly int X;
            public readonly int Y;

            public StartingSpot(BuildingDefinition building, int x, int y)
            {
                Building = building;
                X = x;
                Y = y;
            }
        }

        readonly struct ProducerTier
        {
            public readonly int QueueCapacity;
            public readonly int ParallelSlots;
            public readonly float Speed;
            public readonly bool AutoRepeat;

            public ProducerTier(int queueCapacity, int parallelSlots, float speed, bool autoRepeat)
            {
                QueueCapacity = queueCapacity;
                ParallelSlots = parallelSlots;
                Speed = speed;
                AutoRepeat = autoRepeat;
            }
        }

        static ProducerDefinition Producer(string id, RecipeDefinition[] recipes, ProducerTier[] tiers)
        {
            var serialized = BeginAuthoring<ProducerDefinition>(
                Root + "/Recipes/Producer_" + id + ".asset", out var asset);

            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.Set(serialized, "_displayNameKey", "producer." + id);
            AssetAuthoring.SetReferenceArray(serialized, "_recipes", recipes);

            AssetAuthoring.SetArray(serialized, "_levels", tiers.Length, (element, index) =>
            {
                AssetAuthoring.SetElement(element, "_queueCapacity", tiers[index].QueueCapacity);
                AssetAuthoring.SetElement(element, "_parallelSlots", tiers[index].ParallelSlots);
                AssetAuthoring.SetElement(element, "_speedMultiplier", tiers[index].Speed);
                AssetAuthoring.SetElement(element, "_autoRepeat", tiers[index].AutoRepeat);
            });

            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static StorageDefinition Storage(string id, int[] capacityPerLevel)
        {
            var serialized = BeginAuthoring<StorageDefinition>(
                Root + "/Economy/Storage_" + id + ".asset", out var asset);

            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.SetIntArray(serialized, "_capacityPerLevel", capacityPerLevel);
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        /// <summary>
        /// Eight levels, each about twice the last. Steep enough that levelling means something,
        /// shallow enough that the first three arrive inside one session.
        /// </summary>
        static ProgressionCurve Curve(CurrencyDefinition coins, CurrencyDefinition gems)
        {
            var serialized = BeginAuthoring<ProgressionCurve>(
                Root + "/Progression/ProgressionCurve.asset", out var asset);

            if (serialized == null) return asset;

            var thresholds = new[] { 60, 150, 320, 620, 1100, 1900, 3200, 5000 };

            AssetAuthoring.Set(serialized, "_id", "progression");
            AssetAuthoring.SetArray(serialized, "_levels", thresholds.Length, (element, index) =>
            {
                AssetAuthoring.SetElement(element, "_xpToAdvance", thresholds[index]);

                var rewards = element.FindPropertyRelative("_rewards");
                if (rewards == null) return;

                // Level 1 is where the player starts, so it pays nothing.
                if (index == 0)
                {
                    rewards.arraySize = 0;
                    return;
                }

                rewards.arraySize = 2;
                WriteCurrencyEntry(rewards.GetArrayElementAtIndex(0), coins, 100 * index);
                WriteCurrencyEntry(rewards.GetArrayElementAtIndex(1), gems, 2);
            });

            AssetAuthoring.Apply(serialized);
            return asset;
        }

        readonly struct BuildingTier
        {
            public readonly int ConstructionSeconds;
            public readonly CurrencyDefinition Coins;
            public readonly int CoinCost;
            public readonly int XpReward;

            public readonly int StorageLevel;

            public BuildingTier(int constructionSeconds, CurrencyDefinition coins, int coinCost,
                                int xpReward = 0, int storageLevel = 0)
            {
                ConstructionSeconds = constructionSeconds;
                Coins = coins;
                CoinCost = coinCost;
                XpReward = xpReward;
                StorageLevel = storageLevel;
            }
        }

        static BuildingDefinition Building(string id, BuildingCategory category, int width, int height,
                                           ProducerDefinition producer, int unlockLevel,
                                           BuildingTier[] tiers, Color placeholder,
                                           BuildingDefinition upgradesInto = null)
        {
            var serialized = BeginAuthoring<BuildingDefinition>(
                Root + "/Buildings/Building_" + id + ".asset", out var asset);

            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.Set(serialized, "_displayNameKey", "building." + id);
            AssetAuthoring.SetEnum(serialized, "_category", (int)category);
            AssetAuthoring.Set(serialized, "_unlockLevel", unlockLevel);
            AssetAuthoring.Set(serialized, "_footprintWidth", width);
            AssetAuthoring.Set(serialized, "_footprintHeight", height);
            AssetAuthoring.SetReference(serialized, "_producer", producer);
            AssetAuthoring.SetReference(serialized, "_upgradesInto", upgradesInto);
            AssetAuthoring.SetColour(serialized, "_placeholderColour", placeholder);

            AssetAuthoring.SetArray(serialized, "_levels", tiers.Length, (element, index) =>
            {
                AssetAuthoring.SetElement(element, "_constructionSeconds", tiers[index].ConstructionSeconds);
                AssetAuthoring.SetElement(element, "_xpReward", tiers[index].XpReward);
                AssetAuthoring.SetElement(element, "_storageLevel", tiers[index].StorageLevel);

                var costs = element.FindPropertyRelative("_currencyCost");
                if (costs == null) return;

                costs.arraySize = 1;
                WriteCurrencyEntry(costs.GetArrayElementAtIndex(0), tiers[index].Coins, tiers[index].CoinCost);
            });

            AssetAuthoring.Apply(serialized);
            return asset;
        }

        /// <summary>
        /// One template covers the whole board for now. Orders differ by what they ask for, which
        /// the generator rolls from whatever the player has unlocked, so a single template already
        /// produces a varied board.
        /// </summary>
        static OrderTemplateDefinition OrderTemplate(string id, ItemDefinition deed, OrderKind kind,
                                                     int unlockLevel, int minTypes, int maxTypes,
                                                     int minQuantity, int maxQuantity,
                                                     float coinMultiplier, float xpMultiplier,
                                                     float bonusChance, int deedCount = 1)
        {
            var serialized = BeginAuthoring<OrderTemplateDefinition>(
                Root + "/Orders/OrderTemplate_" + id + ".asset", out var asset);

            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.SetEnum(serialized, "_kind", (int)kind);
            AssetAuthoring.Set(serialized, "_unlockLevel", unlockLevel);
            AssetAuthoring.Set(serialized, "_minItemTypes", minTypes);
            AssetAuthoring.Set(serialized, "_maxItemTypes", maxTypes);
            AssetAuthoring.Set(serialized, "_minQuantityPerItem", minQuantity);
            AssetAuthoring.Set(serialized, "_maxQuantityPerItem", maxQuantity);

            // No expiry in the slice. A timer on the only coin source turns a first session into a
            // race, and the per-slot cooldown already paces the board.
            AssetAuthoring.Set(serialized, "_timeLimitSeconds", 0);
            AssetAuthoring.Set(serialized, "_coinMultiplier", coinMultiplier);
            AssetAuthoring.Set(serialized, "_xpMultiplier", xpMultiplier);
            AssetAuthoring.Set(serialized, "_bonusHardCurrency", 0);
            AssetAuthoring.Set(serialized, "_bonusItemChance", bonusChance);
            WriteItemAmounts(serialized, "_bonusItems", new[] { new Ingredient(deed, deedCount) });
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static OrderBoardDefinition OrderBoard(string id, OrderKind kind, int[] slotCooldownSeconds,
                                               int unlockLevel, int rerollBaseCost)
        {
            var serialized = BeginAuthoring<OrderBoardDefinition>(
                Root + "/Orders/OrderBoard_" + id + ".asset", out var asset);

            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.SetEnum(serialized, "_kind", (int)kind);
            AssetAuthoring.Set(serialized, "_unlockLevel", unlockLevel);
            AssetAuthoring.SetIntArray(serialized, "_slotCooldownSeconds", slotCooldownSeconds);

            // A reroll buys the slot's cooldown. Priced against what the order would have paid,
            // with a floor so clearing a worthless order still costs something.
            AssetAuthoring.Set(serialized, "_rerollBaseCost", rerollBaseCost);
            AssetAuthoring.Set(serialized, "_rerollCostPercent", 45);
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static TownDefinition Town(int width, int height, int startX, int startY,
                                   int startWidth, int startHeight)
        {
            var serialized = BeginAuthoring<TownDefinition>(Root + "/TownDefinition.asset", out var asset);
            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", "town");
            AssetAuthoring.Set(serialized, "_width", width);
            AssetAuthoring.Set(serialized, "_height", height);
            AssetAuthoring.Set(serialized, "_startX", startX);
            AssetAuthoring.Set(serialized, "_startY", startY);
            AssetAuthoring.Set(serialized, "_startWidth", startWidth);
            AssetAuthoring.Set(serialized, "_startHeight", startHeight);
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static ExpansionDefinition Expansion(string id, int x, int y, int width, int height,
                                             ItemDefinition deed, int deedCost,
                                             CurrencyDefinition coins, int coinCost,
                                             ExpansionDefinition requires, int unlockLevel, int sortOrder)
        {
            var serialized = BeginAuthoring<ExpansionDefinition>(
                Root + "/Buildings/Expansion_" + id + ".asset", out var asset);

            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.Set(serialized, "_displayNameKey", "land." + id);
            AssetAuthoring.Set(serialized, "_x", x);
            AssetAuthoring.Set(serialized, "_y", y);
            AssetAuthoring.Set(serialized, "_width", width);
            AssetAuthoring.Set(serialized, "_height", height);
            AssetAuthoring.Set(serialized, "_unlockLevel", unlockLevel);
            AssetAuthoring.Set(serialized, "_sortOrder", sortOrder);
            AssetAuthoring.SetReference(serialized, "_requires", requires);
            WriteItemAmounts(serialized, "_itemCost", new[] { new Ingredient(deed, deedCost) });

            AssetAuthoring.SetArray(serialized, "_currencyCost", 1, (element, _) =>
                WriteCurrencyEntry(element, coins, coinCost));

            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static NewGameDefinition NewGame(int barnLevel, Ingredient[] items,
                                         StartingSpot[] buildings)
        {
            var serialized = BeginAuthoring<NewGameDefinition>(Root + "/NewGame.asset", out var asset);
            if (serialized == null) return asset;

            AssetAuthoring.Set(serialized, "_id", "new_game");
            AssetAuthoring.Set(serialized, "_startingBarnLevel", barnLevel);
            WriteItemAmounts(serialized, "_startingItems", items);

            AssetAuthoring.SetArray(serialized, "_startingBuildings", buildings.Length, (element, index) =>
            {
                AssetAuthoring.SetElement(element, "_building", buildings[index].Building);
                AssetAuthoring.SetElement(element, "_x", buildings[index].X);
                AssetAuthoring.SetElement(element, "_y", buildings[index].Y);
            });

            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static void WriteItemAmounts(SerializedObject serialized, string field,
                                     Ingredient[] amounts)
        {
            var count = amounts?.Length ?? 0;
            AssetAuthoring.SetArray(serialized, field, count, (element, index) =>
            {
                AssetAuthoring.SetElement(element, "_item", amounts[index].Item);
                AssetAuthoring.SetElement(element, "_count", amounts[index].Count);
            });
        }

        static void WriteCurrencyEntry(SerializedProperty element, CurrencyDefinition currency, int amount)
        {
            AssetAuthoring.SetElement(element, "_currency", currency);
            AssetAuthoring.SetElement(element, "_amount", amount);
        }
    }
}
