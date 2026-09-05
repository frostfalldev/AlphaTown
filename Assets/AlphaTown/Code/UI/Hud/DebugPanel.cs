#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.World;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// The tester's shortcut to the parts of the game that are hours away.
    ///
    /// The ship board opens at town level 5 and refills every few hours. Reaching it honestly is
    /// an evening's play, so without this nobody would ever look at it — and content nobody looks
    /// at is content nobody has checked.
    ///
    /// Compiled out of release builds along with everything it calls, so this cannot become a
    /// cheat menu somebody finds in a shipped binary.
    /// </summary>
    public sealed class DebugPanel
    {
        readonly DebugCommands _debug;
        readonly Action<CommandResult> _report;
        readonly Label _state;

        public DebugPanel(DebugCommands debug, Action<CommandResult> report)
        {
            _debug = debug;
            _report = report;

            var card = UiKit.Card();
            card.style.minWidth = 520f;

            var heading = UiKit.Text("Debug", 30, true);
            heading.style.color = UiKit.Warn;
            card.Add(heading);
            card.Add(UiKit.Caption("Development builds only. None of this ships."));

            _state = UiKit.Caption("");
            card.Add(_state);

            var rows = UiKit.Column(10f);
            rows.style.marginTop = 12f;

            rows.Add(Row(
                ("Level up", () => _debug.LevelUp()),
                ("+1000 coins", () => _debug.GrantCoins(1000)),
                ("+25 gems", () => _debug.GrantGems(25))));

            rows.Add(Row(
                ("Fill barn", () => _debug.FillBarn(25)),
                ("+5 deeds", () => _debug.GrantSpecialItems(5))));

            // Time travel is the most useful of these: every timer in the game is an absolute
            // timestamp, so a skip resolves production, building work, cooldowns and expiry
            // through exactly the code path a player returning in the morning takes.
            rows.Add(Row(
                ("Skip 1h", () => _debug.SkipTime(TimeSpan.FromHours(1))),
                ("Skip 8h", () => _debug.SkipTime(TimeSpan.FromHours(8))),
                ("Skip 3d", () => _debug.SkipTime(TimeSpan.FromDays(3)))));

            card.Add(rows);
            Root = card;
        }

        public VisualElement Root { get; }

        VisualElement Row(params (string Label, Func<CommandResult> Action)[] buttons)
        {
            var row = UiKit.Row(10f);
            row.style.flexWrap = Wrap.Wrap;

            foreach (var button in buttons)
            {
                var action = button.Action;
                row.Add(UiKit.Action(button.Label, () => _report?.Invoke(action())));
            }

            return row;
        }

        /// <summary>Shows where the player actually is, so a skip's effect is legible.</summary>
        public void Refresh(GameWorld world)
        {
            if (world == null) return;

            _state.text = "Level " + world.Progression.TownLevel +
                          "  ·  barn " + world.Barn.UsedSpace + "/" + world.Barn.Capacity +
                          "  ·  " + world.OrderBoards.Count + " boards";
        }
    }
}
#endif
