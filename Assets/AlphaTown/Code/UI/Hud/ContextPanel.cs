using System;
using System.Collections.Generic;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Catalog;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.Production;
using AlphaTown.UI.Selection;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// The panel that answers "what did I just tap?" and offers what can be done with it.
    ///
    /// One panel for every kind of target rather than a screen each: a field, a factory, a
    /// building under construction and a bare tile are all "a thing at a cell", and the slice is
    /// clearer — and far quicker to change — when they share a body and differ only in the buttons
    /// along the bottom.
    /// </summary>
    public sealed class ContextPanel
    {
        readonly TownCommands _commands;
        readonly IGameDatabase _database;
        readonly IGameClock _clock;
        readonly Action<CommandResult> _report;
        readonly Action _onBuildRequested;
        readonly Action _onSickleRequested;
        readonly List<BuildingInstance> _harvestable = new List<BuildingInstance>(32);

        readonly Label _title;
        readonly Label _detail;
        readonly VisualElement _progressTrack;
        readonly VisualElement _progressFill;
        readonly Button _primary;
        readonly Button _secondary;
        readonly Button _tertiary;

        public ContextPanel(
            TownCommands commands,
            IGameDatabase database,
            IGameClock clock,
            Action<CommandResult> report,
            Action onBuildRequested,
            Action onSickleRequested)
        {
            _commands = commands;
            _database = database;
            _clock = clock;
            _report = report;
            _onBuildRequested = onBuildRequested;
            _onSickleRequested = onSickleRequested;

            var card = UiKit.Card();
            card.style.minWidth = 420f;

            _title = UiKit.Text("Nothing selected", 30, true);
            _detail = UiKit.Caption("Tap the town to inspect it.");

            _progressTrack = UiKit.ProgressBar(out _progressFill);
            _progressTrack.style.display = DisplayStyle.None;

            // The three buttons are built once and re-pointed through userData. Rebuilding them
            // on every refresh would drop a press that landed in the same frame as a tick.
            //
            // Always in the same order — main action, tool, upgrade — so the button under the
            // thumb does not change meaning between one selection and the next. Wrapping, because
            // three touch targets and a long cost label do not fit a narrow phone in one row.
            var buttons = UiKit.Row();
            buttons.style.flexWrap = Wrap.Wrap;

            _primary = UiKit.Action("", () => Invoke(_primary));
            _secondary = UiKit.Action("", () => Invoke(_secondary));
            _tertiary = UiKit.Action("", () => Invoke(_tertiary));
            buttons.Add(_primary);
            buttons.Add(_secondary);
            buttons.Add(_tertiary);

            var body = UiKit.Column(10f);
            body.Add(_title);
            body.Add(_detail);
            body.Add(_progressTrack);
            body.Add(buttons);
            card.Add(body);

            Root = card;
        }

        public VisualElement Root { get; }

        /// <summary>
        /// Rebuilds the panel from the current selection. Called every refresh rather than only on
        /// change, because the same selection keeps changing underneath — a crop ripens, a build
        /// finishes, coins arrive and an upgrade becomes affordable.
        /// </summary>
        public void Refresh(TownSelection selection)
        {
            var world = _commands.World;

            if (selection == null || !selection.HasCell)
            {
                Show("Nothing selected", "Tap the town to inspect it.", null, null);
                return;
            }

            if (!world.Buildings.TryGetBuilding(selection.BuildingInstanceId, out var building))
            {
                ShowEmptyLand(selection);
                return;
            }

            if (building.IsBusy)
            {
                ShowUnderConstruction(building);
                return;
            }

            if (world.TryGetProducer(building.InstanceId, out var producer))
            {
                ShowProducer(building, producer);
                return;
            }

            ShowIdleBuilding(building);
        }

        void ShowEmptyLand(TownSelection selection)
        {
            var grid = _commands.World.Buildings.Grid;
            var owned = grid.IsUnlocked(selection.Cell);

            SetProgress(-1f);
            _title.text = owned ? "Empty land" : "Locked land";
            _detail.text = owned
                ? "Cell " + selection.Cell + ". Nothing built here yet."
                : "Cell " + selection.Cell + ". Unlock this land with deeds before building on it.";

            SetButton(_primary, owned ? "Build" : "", _onBuildRequested, owned);
            SetButton(_secondary, "", null, false);
            SetButton(_tertiary, "", null, false);
        }

        void ShowUnderConstruction(BuildingInstance building)
        {
            var remaining = building.RemainingTicks(_clock.UtcNowTicks);

            _title.text = DisplayNames.ForBuilding(_database, building.DefinitionId);
            _detail.text = building.Level <= 0
                ? "Building — " + DisplayNames.DurationFromTicks(remaining) + " left"
                : "Upgrading to level " + building.TargetLevel + " — " +
                  DisplayNames.DurationFromTicks(remaining) + " left";

            SetProgress(building.Progress(_clock.UtcNowTicks));
            SetButton(_primary, "", null, false);
            SetButton(_secondary, "", null, false);
            SetButton(_tertiary, "", null, false);
        }

        void ShowProducer(BuildingInstance building, Producer producer)
        {
            var name = DisplayNames.ForBuilding(_database, building.DefinitionId);
            _title.text = name + "  ·  Lv " + building.Level;

            if (producer.HasReadyGoods)
            {
                _detail.text = "Ready: " + DescribeReady(producer);
                SetProgress(1f);
                SetButton(_primary, "Harvest", () => Run(_commands.Harvest(building.InstanceId)), true);
            }
            else if (producer.TryGetActiveOrder(out var order))
            {
                _detail.text = DisplayNames.ForItem(_database, FirstOutputOf(order.RecipeId)) + " — " +
                               DisplayNames.DurationFromTicks(order.RemainingTicks(_clock.UtcNowTicks)) + " left";

                SetProgress(order.Progress01(_clock.UtcNowTicks));
                SetButton(_primary, "Growing…", null, false);
            }
            else
            {
                var recipeId = _commands.DefaultRecipeFor(producer.DefinitionId, producer.LastRecipeId);
                var label = string.IsNullOrEmpty(recipeId)
                    ? "Nothing to plant"
                    : "Plant " + DisplayNames.ForItem(_database, FirstOutputOf(recipeId));

                _detail.text = "Empty. " + (string.IsNullOrEmpty(recipeId)
                    ? "No crop is unlocked or affordable."
                    : "Ready to sow.");

                SetProgress(-1f);
                SetButton(_primary, label, () => Run(_commands.Plant(building.InstanceId)),
                    !string.IsNullOrEmpty(recipeId));
            }

            AddSickleButton(producer.HasReadyGoods);
            AddUpgradeButton(building);
        }

        void ShowIdleBuilding(BuildingInstance building)
        {
            _title.text = DisplayNames.ForBuilding(_database, building.DefinitionId) +
                          "  ·  Lv " + building.Level;

            _detail.text = DescribeNonProducer(building);
            SetProgress(-1f);
            SetButton(_primary, "", null, false);
            SetButton(_secondary, "", null, false);
            AddUpgradeButton(building);
        }

        /// <summary>
        /// What a building that makes nothing is actually for.
        ///
        /// "Nothing is produced here" is true of a granary and useless: it stores, which is the
        /// whole reason it was bought, and a panel that will not say so makes the most expensive
        /// building in the town look like a mistake.
        /// </summary>
        string DescribeNonProducer(BuildingInstance building)
        {
            var storageLevel = building.Definition.GetLevel(building.Level).StorageLevel;
            if (storageLevel <= 0) return "Nothing is produced here.";

            var storage = _database?.DefaultStorage;
            return storage == null
                ? "Holds the barn at level " + storageLevel + "."
                : "Holds the barn at " + storage.GetCapacity(storageLevel) + " slots.";
        }

        /// <summary>
        /// Offers the sickle, and says how many fields are waiting.
        ///
        /// The count is the whole argument for picking the tool up. "Sickle" alone is a mode with
        /// no stated benefit; "Sickle (6)" tells the player exactly what it saves them.
        /// </summary>
        void AddSickleButton(bool hasReadyGoods)
        {
            if (!hasReadyGoods || _onSickleRequested == null)
            {
                SetButton(_secondary, "", null, false);
                return;
            }

            _commands.CollectHarvestable(_harvestable);
            var label = _harvestable.Count > 1 ? "Sickle (" + _harvestable.Count + ")" : "Sickle";

            SetButton(_secondary, label, _onSickleRequested, true);
        }

        void AddUpgradeButton(BuildingInstance building)
        {
            var next = NextLevelOf(building);
            if (next == null)
            {
                SetButton(_tertiary, "Max level", null, false);
                return;
            }

            var world = _commands.World;
            var affordable = world.Wallet.CanAffordAll(next.CurrencyCost) &&
                             world.Barn.ContainsAll(next.ItemCost);

            SetButton(_tertiary, "Upgrade  " + DescribeCost(next),
                () => Run(_commands.Upgrade(building.InstanceId)), affordable);
        }

        /// <summary>
        /// What the next improvement costs: the following level, or the first level of whatever
        /// this building turns into. Null once there is nowhere left to go.
        /// </summary>
        IBuildingLevel NextLevelOf(BuildingInstance building)
        {
            if (building.Level < building.Definition.MaxLevel)
                return building.Definition.GetLevel(building.Level + 1);

            var nextId = building.Definition.UpgradesIntoId;
            if (string.IsNullOrEmpty(nextId)) return null;

            return _database.TryGetBuilding(nextId, out var next) ? next.GetLevel(1) : null;
        }

        string DescribeCost(IBuildingLevel level)
        {
            var text = string.Empty;

            var currencies = level.CurrencyCost;
            for (var i = 0; i < currencies.Count; i++)
            {
                if (text.Length > 0) text += " ";
                text += currencies[i].Amount + " " + DisplayNames.ForCurrency(_database, currencies[i].CurrencyId);
            }

            var items = level.ItemCost;
            for (var i = 0; i < items.Count; i++)
            {
                if (text.Length > 0) text += " ";
                text += items[i].Count + " " + DisplayNames.ForItem(_database, items[i].ItemId);
            }

            return text.Length == 0 ? "(free)" : "(" + text + ")";
        }

        string DescribeReady(Producer producer)
        {
            var ready = producer.Ready;
            if (ready.Count == 0) return "nothing";

            var text = string.Empty;
            for (var i = 0; i < ready.Count; i++)
            {
                if (i > 0) text += ", ";
                text += ready[i].Count + " x " + DisplayNames.ForItem(_database, ready[i].ItemId);
            }

            return text;
        }

        string FirstOutputOf(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return string.Empty;
            if (!_database.TryGetRecipe(recipeId, out var recipe)) return recipeId;

            return recipe.Outputs.Count > 0 ? recipe.Outputs[0].ItemId : recipeId;
        }

        void Run(CommandResult result) => _report?.Invoke(result);

        void Show(string title, string detail, Action primary, Action secondary)
        {
            _title.text = title;
            _detail.text = detail;
            SetProgress(-1f);
            SetButton(_primary, primary == null ? "" : "OK", primary, primary != null);
            SetButton(_secondary, secondary == null ? "" : "OK", secondary, secondary != null);
            SetButton(_tertiary, "", null, false);
        }

        /// <summary>Negative hides the bar entirely — there is nothing timed to show.</summary>
        void SetProgress(float progress01)
        {
            if (progress01 < 0f)
            {
                _progressTrack.style.display = DisplayStyle.None;
                return;
            }

            _progressTrack.style.display = DisplayStyle.Flex;
            UiKit.SetProgress(_progressFill, progress01);
        }

        /// <summary>
        /// An empty label hides the button rather than leaving a dead rectangle. Buttons are
        /// re-pointed in place instead of rebuilt so a refresh mid-tap cannot swallow the press.
        /// </summary>
        static void SetButton(Button button, string text, Action action, bool enabled)
        {
            if (string.IsNullOrEmpty(text))
            {
                button.style.display = DisplayStyle.None;
                button.userData = null;
                return;
            }

            button.style.display = DisplayStyle.Flex;
            button.text = text;
            button.userData = action;
            UiKit.SetEnabled(button, enabled && action != null);
        }

        static void Invoke(Button button) => (button.userData as Action)?.Invoke();
    }
}
