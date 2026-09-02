using System;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Presentation;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Commands;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// What can be built on the tapped cell, and what it costs.
    ///
    /// Scoped to a cell rather than being a free-floating catalogue: the player has already said
    /// where, so the menu can tell them exactly why a given building will not go there — locked
    /// land, no room for its footprint, not enough coins — instead of offering everything and
    /// failing after the choice.
    /// </summary>
    public sealed class BuildMenu
    {
        readonly TownCommands _commands;
        readonly IGameDatabase _database;
        readonly Action<CommandResult> _report;
        readonly Label _heading;
        readonly VisualElement _list;

        GridPosition _cell;

        public BuildMenu(TownCommands commands, IGameDatabase database, Action<CommandResult> report)
        {
            _commands = commands;
            _database = database;
            _report = report;

            var card = UiKit.Card();
            card.style.minWidth = 520f;
            card.style.maxHeight = 640f;

            _heading = UiKit.Text("Build", 30, true);
            card.Add(_heading);

            _list = new ScrollView(ScrollViewMode.Vertical);
            _list.style.flexGrow = 1f;
            _list.style.marginTop = 10f;
            card.Add(_list);

            Root = card;
        }

        public VisualElement Root { get; }

        public void Refresh(GridPosition cell)
        {
            _cell = cell;
            _heading.text = "Build at " + cell;
            _list.Clear();

            var buildings = _database?.Buildings;
            if (buildings == null) return;

            var level = _commands.World.Progression.TownLevel;

            for (var i = 0; i < buildings.Count; i++)
            {
                var definition = buildings[i];
                if (definition == null) continue;

                // Locked buildings are listed, greyed, with the level they need. Hiding them
                // hides the reason to keep playing.
                _list.Add(BuildRow(definition, definition.UnlockLevel <= level));
            }

            if (_list.childCount == 0) _list.Add(UiKit.Caption("No buildings are defined yet."));
        }

        VisualElement BuildRow(IBuildingDefinition definition, bool unlocked)
        {
            var row = UiKit.Row(12f);
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 10f;

            var left = UiKit.Row(10f);

            if (definition is IBuildingVisuals visuals && visuals.Icon != null)
            {
                var icon = new Image { sprite = visuals.Icon };
                icon.style.width = 44f;
                icon.style.height = 44f;
                left.Add(icon);
            }

            var text = UiKit.Column(2f);
            text.Add(UiKit.Text(DisplayNames.ForBuilding(_database, definition.Id), 24, true));
            text.Add(UiKit.Caption(unlocked
                ? DescribeCost(definition.GetLevel(1)) + "  ·  " + definition.Footprint
                : "Unlocks at town level " + definition.UnlockLevel));

            left.Add(text);
            row.Add(left);

            var validation = _commands.World.Buildings.ValidatePlacement(definition.Id, _cell);
            var canBuild = validation == BuildingActionResult.Success;

            var button = UiKit.Action(canBuild ? "Build" : Shorten(validation), () =>
            {
                _report?.Invoke(_commands.Build(definition.Id, _cell));
                Refresh(_cell);
            });

            UiKit.SetEnabled(button, canBuild);
            row.Add(button);

            return row;
        }

        string DescribeCost(IBuildingLevel level)
        {
            var text = string.Empty;

            for (var i = 0; i < level.CurrencyCost.Count; i++)
            {
                if (text.Length > 0) text += " + ";
                text += level.CurrencyCost[i].Amount + " " +
                        DisplayNames.ForCurrency(_database, level.CurrencyCost[i].CurrencyId);
            }

            for (var i = 0; i < level.ItemCost.Count; i++)
            {
                if (text.Length > 0) text += " + ";
                text += level.ItemCost[i].Count + " " +
                        DisplayNames.ForItem(_database, level.ItemCost[i].ItemId);
            }

            return text.Length == 0 ? "Free" : text;
        }

        /// <summary>Fits the reason onto a button. The full sentence is in the toast if they tap.</summary>
        static string Shorten(BuildingActionResult result)
        {
            switch (result)
            {
                case BuildingActionResult.Locked: return "Locked";
                case BuildingActionResult.InsufficientFunds: return "Too costly";
                case BuildingActionResult.InsufficientItems: return "No materials";
                case BuildingActionResult.Overlaps: return "Occupied";
                case BuildingActionResult.AreaLocked: return "Locked land";
                case BuildingActionResult.OutOfBounds: return "Off map";
                default: return "Can't";
            }
        }
    }
}
