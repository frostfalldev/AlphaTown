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
    /// Generates a starting town's worth of content: two crops, a mill, a bakery, an order board,
    /// three parcels of land, and the database that ties them together.
    ///
    /// This exists because the slice needs numbers to be playable, and hand-authoring thirty
    /// interlinked assets before the loop can be tried once is the wrong order to find out the
    /// loop is wrong. Everything here is a placeholder chosen to make the loop legible in a few
    /// minutes of play, not a balance pass: crops finish in a minute rather than four hours, and
    /// the first order is affordable almost immediately.
    ///
    /// Re-running updates the same assets in place, so the ids in a save stay valid.
    ///
    /// TODO(content): replace wholesale once real crops, buildings and art exist. Nothing in code
    /// depends on these ids — the database is the only thing that names them.
    /// </summary>
    internal static class SampleContentBuilder
    {
        const string Root = "Assets/AlphaTown/Content";

        [MenuItem("AlphaTown/Content/Build Sample Content", false, 20)]
        internal static void Build()
        {
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
            }

            Debug.Log("[AlphaTown] Sample content written to " + Root + ".");
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

            // --- Storage and progression -------------------------------------------------------
            var barn = Storage("barn", new[] { 50, 75, 110, 160, 240 });
            var curve = Curve(coins, gems);

            // --- Buildings ---------------------------------------------------------------------
            var plot = Building("field_plot", BuildingCategory.Farming, 1, 1, field, unlockLevel: 1,
                new[]
                {
                    new BuildingTier(constructionSeconds: 0, coins: coins, coinCost: 50),
                    new BuildingTier(constructionSeconds: 30, coins: coins, coinCost: 250)
                },
                placeholder: new Color(0.45f, 0.34f, 0.22f));

            var millBuilding = Building("mill_house", BuildingCategory.Production, 2, 2, mill, unlockLevel: 2,
                new[]
                {
                    new BuildingTier(constructionSeconds: 60, coins: coins, coinCost: 300),
                    new BuildingTier(constructionSeconds: 180, coins: coins, coinCost: 1200)
                },
                placeholder: new Color(0.72f, 0.66f, 0.52f));

            var bakeryBuilding = Building("bakery", BuildingCategory.Production, 2, 2, bakery, unlockLevel: 3,
                new[]
                {
                    new BuildingTier(constructionSeconds: 120, coins: coins, coinCost: 900)
                },
                placeholder: new Color(0.78f, 0.48f, 0.36f));

            // --- Orders ------------------------------------------------------------------------
            // Deeds drop from orders and nowhere else, which is what keeps expansion paced by
            // playing rather than by saving up.
            var template = OrderTemplate("helicopter_basic", deed);
            var board = OrderBoard("helicopter_board", new[] { 120, 180, 240, 300 });

            // --- Land --------------------------------------------------------------------------
            var town = Town(width: 24, height: 24, startX: 8, startY: 8, startWidth: 8, startHeight: 8);

            var north = Expansion("land_north", 8, 16, 8, 8, deed, deedCost: 1, coins: coins, coinCost: 500,
                requires: null, unlockLevel: 2, sortOrder: 0);

            var east = Expansion("land_east", 16, 8, 8, 8, deed, deedCost: 2, coins: coins, coinCost: 1500,
                requires: null, unlockLevel: 3, sortOrder: 1);

            var northEast = Expansion("land_north_east", 16, 16, 8, 8, deed, deedCost: 3, coins: coins,
                coinCost: 4000, requires: north, unlockLevel: 4, sortOrder: 2);

            // --- New game ----------------------------------------------------------------------
            var newGame = NewGame(barnLevel: 1,
                items: new[] { new Ingredient(wheat, 4) },
                buildings: new[]
                {
                    new StartingSpot(plot, 9, 9), new StartingSpot(plot, 11, 9),
                    new StartingSpot(plot, 9, 11), new StartingSpot(plot, 11, 11)
                });

            // --- Database ----------------------------------------------------------------------
            var database = AssetAuthoring.CreateOrLoad<GameDatabase>(Root + "/GameDatabase.asset");
            var serialized = AssetAuthoring.Edit(database);

            AssetAuthoring.SetReferenceArray(serialized, "_items", new Object[] { wheat, corn, flour, bread, deed });
            AssetAuthoring.SetReferenceArray(serialized, "_recipes",
                new Object[] { growWheat, growCorn, millFlour, bakeBread });
            AssetAuthoring.SetReferenceArray(serialized, "_producers", new Object[] { field, mill, bakery });
            AssetAuthoring.SetReferenceArray(serialized, "_storages", new Object[] { barn });
            AssetAuthoring.SetReferenceArray(serialized, "_currencies", new Object[] { coins, gems });
            AssetAuthoring.SetReferenceArray(serialized, "_orderTemplates", new Object[] { template });
            AssetAuthoring.SetReferenceArray(serialized, "_buildings",
                new Object[] { plot, millBuilding, bakeryBuilding });
            AssetAuthoring.SetReferenceArray(serialized, "_orderBoards", new Object[] { board });
            AssetAuthoring.SetReferenceArray(serialized, "_expansions", new Object[] { north, east, northEast });

            AssetAuthoring.SetReference(serialized, "_defaultStorage", barn);
            AssetAuthoring.SetReference(serialized, "_softCurrency", coins);
            AssetAuthoring.SetReference(serialized, "_hardCurrency", gems);
            AssetAuthoring.SetReference(serialized, "_progressionCurve", curve);
            AssetAuthoring.SetReference(serialized, "_townDefinition", town);
            AssetAuthoring.SetReference(serialized, "_newGame", newGame);
            AssetAuthoring.Apply(serialized);
        }

        // --- Builders ---------------------------------------------------------------------------

        static CurrencyDefinition Currency(string id, CurrencyKind kind, int startingAmount)
        {
            var asset = AssetAuthoring.CreateOrLoad<CurrencyDefinition>(Root + "/Economy/Currency_" + id + ".asset");
            var serialized = AssetAuthoring.Edit(asset);

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
            var asset = AssetAuthoring.CreateOrLoad<ItemDefinition>(Root + folder + id + ".asset");
            var serialized = AssetAuthoring.Edit(asset);

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
            var asset = AssetAuthoring.CreateOrLoad<RecipeDefinition>(Root + "/Recipes/Recipe_" + id + ".asset");
            var serialized = AssetAuthoring.Edit(asset);

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
            var asset = AssetAuthoring.CreateOrLoad<ProducerDefinition>(
                Root + "/Recipes/Producer_" + id + ".asset");

            var serialized = AssetAuthoring.Edit(asset);

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
            var asset = AssetAuthoring.CreateOrLoad<StorageDefinition>(Root + "/Economy/Storage_" + id + ".asset");
            var serialized = AssetAuthoring.Edit(asset);

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
            var asset = AssetAuthoring.CreateOrLoad<ProgressionCurve>(
                Root + "/Progression/ProgressionCurve.asset");

            var thresholds = new[] { 60, 150, 320, 620, 1100, 1900, 3200, 5000 };
            var serialized = AssetAuthoring.Edit(asset);

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

            public BuildingTier(int constructionSeconds, CurrencyDefinition coins, int coinCost)
            {
                ConstructionSeconds = constructionSeconds;
                Coins = coins;
                CoinCost = coinCost;
            }
        }

        static BuildingDefinition Building(string id, BuildingCategory category, int width, int height,
                                           ProducerDefinition producer, int unlockLevel,
                                           BuildingTier[] tiers, Color placeholder)
        {
            var asset = AssetAuthoring.CreateOrLoad<BuildingDefinition>(
                Root + "/Buildings/Building_" + id + ".asset");

            var serialized = AssetAuthoring.Edit(asset);

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.Set(serialized, "_displayNameKey", "building." + id);
            AssetAuthoring.SetEnum(serialized, "_category", (int)category);
            AssetAuthoring.Set(serialized, "_unlockLevel", unlockLevel);
            AssetAuthoring.Set(serialized, "_footprintWidth", width);
            AssetAuthoring.Set(serialized, "_footprintHeight", height);
            AssetAuthoring.SetReference(serialized, "_producer", producer);
            AssetAuthoring.SetColour(serialized, "_placeholderColour", placeholder);

            AssetAuthoring.SetArray(serialized, "_levels", tiers.Length, (element, index) =>
            {
                AssetAuthoring.SetElement(element, "_constructionSeconds", tiers[index].ConstructionSeconds);

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
        static OrderTemplateDefinition OrderTemplate(string id, ItemDefinition deed)
        {
            var asset = AssetAuthoring.CreateOrLoad<OrderTemplateDefinition>(
                Root + "/Orders/OrderTemplate_" + id + ".asset");

            var serialized = AssetAuthoring.Edit(asset);

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.SetEnum(serialized, "_kind", (int)OrderKind.Helicopter);
            AssetAuthoring.Set(serialized, "_unlockLevel", 1);
            AssetAuthoring.Set(serialized, "_minItemTypes", 1);
            AssetAuthoring.Set(serialized, "_maxItemTypes", 2);
            AssetAuthoring.Set(serialized, "_minQuantityPerItem", 2);
            AssetAuthoring.Set(serialized, "_maxQuantityPerItem", 8);

            // No expiry in the slice. A timer on the only coin source turns a first session into a
            // race, and the per-slot cooldown already paces the board.
            AssetAuthoring.Set(serialized, "_timeLimitSeconds", 0);
            AssetAuthoring.Set(serialized, "_coinMultiplier", 1.7f);
            AssetAuthoring.Set(serialized, "_xpMultiplier", 1f);
            AssetAuthoring.Set(serialized, "_bonusHardCurrency", 0);
            AssetAuthoring.Set(serialized, "_bonusItemChance", 0.3f);
            WriteItemAmounts(serialized, "_bonusItems", new[] { new Ingredient(deed, 1) });
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static OrderBoardDefinition OrderBoard(string id, int[] slotCooldownSeconds)
        {
            var asset = AssetAuthoring.CreateOrLoad<OrderBoardDefinition>(
                Root + "/Orders/OrderBoard_" + id + ".asset");

            var serialized = AssetAuthoring.Edit(asset);

            AssetAuthoring.Set(serialized, "_id", id);
            AssetAuthoring.SetEnum(serialized, "_kind", (int)OrderKind.Helicopter);
            AssetAuthoring.SetIntArray(serialized, "_slotCooldownSeconds", slotCooldownSeconds);
            AssetAuthoring.Apply(serialized);
            return asset;
        }

        static TownDefinition Town(int width, int height, int startX, int startY,
                                   int startWidth, int startHeight)
        {
            var asset = AssetAuthoring.CreateOrLoad<TownDefinition>(Root + "/TownDefinition.asset");
            var serialized = AssetAuthoring.Edit(asset);

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
            var asset = AssetAuthoring.CreateOrLoad<ExpansionDefinition>(
                Root + "/Buildings/Expansion_" + id + ".asset");

            var serialized = AssetAuthoring.Edit(asset);

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
            var asset = AssetAuthoring.CreateOrLoad<NewGameDefinition>(Root + "/NewGame.asset");
            var serialized = AssetAuthoring.Edit(asset);

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
