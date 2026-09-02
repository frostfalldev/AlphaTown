using System.Collections.Generic;
using AlphaTown.Gameplay.Bootstrap;
using AlphaTown.Gameplay.Commands;
using AlphaTown.UI.Selection;
using UnityEngine;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// The whole heads-up display: the resource bar, the context panel, and the three screens
    /// behind the bottom buttons.
    ///
    /// One component owns the layout so there is a single place that decides what is on screen.
    /// The panels themselves are plain C# classes, not components, which keeps them constructible
    /// in isolation and stops the HUD from becoming a hunt through a scene hierarchy.
    ///
    /// Refreshed on a timer rather than every frame. Everything shown is derived from timestamps,
    /// so four updates a second is indistinguishable from sixty and costs a fraction as much on a
    /// phone — the one thing that must not lag is the button press itself, and that is an event.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TownHud : MonoBehaviour
    {
        enum Overlay
        {
            None = 0,
            Barn = 1,
            Orders = 2,
            Build = 3
        }

        [SerializeField] GameRunner _runner;
        [SerializeField] TownSelection _selection;
        [SerializeField] TownTool _tool;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds between refreshes. Timers are timestamp-derived, so this only sets how " +
                 "soon a change is noticed.")]
        float _refreshIntervalSeconds = 0.25f;

        [SerializeField]
        [Tooltip("Barn items promoted to the top bar, such as land deeds. Empty falls back to " +
                 "every item in the Special category.")]
        List<string> _trackedItemIds = new List<string>();

        ResourceBar _resourceBar;
        ContextPanel _contextPanel;
        BarnPanel _barnPanel;
        OrderPanel _orderPanel;
        BuildMenu _buildMenu;

        VisualElement _overlay;
        VisualElement _toolBanner;
        Label _toolBannerText;
        Label _toast;
        Overlay _screen;
        float _secondsSinceRefresh;
        float _toastSecondsLeft;

        /// <summary>How long a message stays up. Long enough to read a short sentence.</summary>
        const float ToastSeconds = 2.5f;

        void Awake()
        {
            if (_runner == null) _runner = FindAnyObjectByType<GameRunner>();
            if (_selection == null) _selection = FindAnyObjectByType<TownSelection>();
            if (_tool == null) _tool = FindAnyObjectByType<TownTool>();
        }

        void OnEnable()
        {
            if (_selection != null) _selection.Changed += OnSelectionChanged;
            if (_tool != null) _tool.Changed += OnToolChanged;
        }

        void OnDisable()
        {
            if (_selection != null) _selection.Changed -= OnSelectionChanged;
            if (_tool != null) _tool.Changed -= OnToolChanged;
        }

        void Start()
        {
            if (_runner == null || _runner.Commands == null)
            {
                Debug.LogError("[AlphaTown] TownHud has no GameRunner. The HUD is disabled.");
                enabled = false;
                return;
            }

            Build(GetComponent<UIDocument>().rootVisualElement);
            OnToolChanged();
            Refresh();
        }

        void Build(VisualElement root)
        {
            // The root fills the screen and is pickable by default, which would make every touch
            // anywhere read as "on the HUD" and leave the town unable to be panned, tapped or
            // harvested. Only actual widgets should block the world, so the layout containers opt
            // out of hit-testing and the buttons and panels inside them keep theirs.
            root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 16f;
            root.style.paddingRight = 16f;
            root.style.paddingTop = 16f;
            root.style.paddingBottom = 16f;

            var commands = _runner.Commands;
            var database = _runner.Database;
            var clock = _runner.Clock;

            _resourceBar = new ResourceBar(database, _trackedItemIds);
            root.Add(_resourceBar.Root);

            // The middle is left empty on purpose: it is the town, and the player needs to be able
            // to see and drag it. The HUD lives at the edges.
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1f;
            root.Add(spacer);

            _overlay = new VisualElement();
            _overlay.style.alignItems = Align.Center;
            _overlay.style.marginBottom = 12f;
            root.Add(_overlay);

            root.Add(BuildToolBanner());

            _toast = UiKit.Text("", 24, true);
            _toast.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
            _toast.style.paddingLeft = 18f;
            _toast.style.paddingRight = 18f;
            _toast.style.paddingTop = 10f;
            _toast.style.paddingBottom = 10f;
            _toast.style.alignSelf = Align.Center;
            _toast.style.marginBottom = 12f;
            _toast.style.display = DisplayStyle.None;
            UiKit.Round(_toast, 10f);
            root.Add(_toast);

            _barnPanel = new BarnPanel(database);
            _orderPanel = new OrderPanel(commands, database, clock, Report);
            _buildMenu = new BuildMenu(commands, database, Report);
            _contextPanel = new ContextPanel(
                commands, database, clock, Report, () => Open(Overlay.Build), ArmSickle);

            _toast.pickingMode = PickingMode.Ignore;
            _overlay.pickingMode = PickingMode.Ignore;

            var bottom = UiKit.Row(12f);
            bottom.pickingMode = PickingMode.Ignore;
            bottom.style.justifyContent = Justify.SpaceBetween;
            bottom.style.alignItems = Align.FlexEnd;

            bottom.Add(_contextPanel.Root);

            var buttons = UiKit.Row(12f);
            buttons.pickingMode = PickingMode.Ignore;
            buttons.Add(UiKit.Action("Barn", () => Toggle(Overlay.Barn)));
            buttons.Add(UiKit.Action("Orders", () => Toggle(Overlay.Orders)));
            buttons.Add(UiKit.Action("Build", () => Toggle(Overlay.Build)));
            bottom.Add(buttons);

            root.Add(bottom);
        }

        void Update()
        {
            if (_runner == null || _runner.World == null) return;

            if (_toastSecondsLeft > 0f)
            {
                _toastSecondsLeft -= Time.unscaledDeltaTime;
                if (_toastSecondsLeft <= 0f) _toast.style.display = DisplayStyle.None;
            }

            _secondsSinceRefresh += Time.unscaledDeltaTime;
            if (_secondsSinceRefresh < _refreshIntervalSeconds) return;

            _secondsSinceRefresh = 0f;
            Refresh();
        }

        void Refresh()
        {
            var world = _runner.World;

            _resourceBar.Refresh(world);
            _contextPanel.Refresh(_selection);

            switch (_screen)
            {
                case Overlay.Barn: _barnPanel.Refresh(world); break;
                case Overlay.Orders: _orderPanel.Refresh(world); break;
                case Overlay.Build:
                    if (_selection != null && _selection.HasCell) _buildMenu.Refresh(_selection.Cell);
                    break;
            }
        }

        /// <summary>
        /// Picks the sickle up and gets out of the way. An open panel would cover the fields the
        /// player is about to sweep, which is the one thing the tool needs them to be able to see.
        /// </summary>
        void ArmSickle()
        {
            if (_tool == null)
            {
                Report(CommandResult.Fail("No sickle available."));
                return;
            }

            Open(Overlay.None);
            _tool.Select(TownToolKind.Sickle);
        }

        void OnToolChanged()
        {
            // Subscribed in OnEnable, which runs before the HUD is built.
            if (_toolBanner == null) return;

            var armed = _tool != null && _tool.IsSickleArmed;
            _toolBanner.style.display = armed ? DisplayStyle.Flex : DisplayStyle.None;

            if (armed) _toolBannerText.text = "Sickle in hand — sweep across your crops";
            Refresh();
        }

        /// <summary>
        /// The one thing on screen that says a mode is active, and the way out of it.
        ///
        /// Two fingers still pan and zoom while it is up, so the map stays reachable; the banner
        /// says what to do and the button puts the tool down without needing to find a crop first.
        /// </summary>
        VisualElement BuildToolBanner()
        {
            var banner = UiKit.Card(10f);
            banner.style.flexDirection = FlexDirection.Row;
            banner.style.alignItems = Align.Center;
            banner.style.justifyContent = Justify.SpaceBetween;
            banner.style.alignSelf = Align.Center;
            banner.style.marginBottom = 10f;
            banner.style.backgroundColor = new Color(0.16f, 0.30f, 0.16f, 0.94f);
            banner.style.display = DisplayStyle.None;

            _toolBannerText = UiKit.Text("", 24, true);
            _toolBannerText.style.marginRight = 16f;
            banner.Add(_toolBannerText);
            banner.Add(UiKit.Action("Done", () => _tool?.Clear()));

            _toolBanner = banner;
            return banner;
        }

        void OnSelectionChanged()
        {
            // Tapping a different building while the build menu is open means the player has moved
            // on; keeping a stale cell open is how you build in the wrong place.
            if (_screen == Overlay.Build && _selection != null && _selection.HasBuilding) Open(Overlay.None);

            Refresh();
        }

        void Toggle(Overlay screen) => Open(_screen == screen ? Overlay.None : screen);

        void Open(Overlay screen)
        {
            if (screen == Overlay.Build && (_selection == null || !_selection.HasCell))
            {
                Report(CommandResult.Fail("Tap an empty tile first."));
                return;
            }

            _screen = screen;
            _overlay.Clear();

            switch (screen)
            {
                case Overlay.Barn: _overlay.Add(_barnPanel.Root); break;
                case Overlay.Orders: _overlay.Add(_orderPanel.Root); break;
                case Overlay.Build: _overlay.Add(_buildMenu.Root); break;
            }

            Refresh();
        }

        /// <summary>
        /// Shows the outcome of a command and saves if it changed anything.
        ///
        /// Every player action routes through here, which is what makes "auto-save on actions" one
        /// line rather than a call scattered through every button.
        /// </summary>
        void Report(CommandResult result)
        {
            if (result.Success) _runner.RequestSave();

            if (!string.IsNullOrEmpty(result.Message))
            {
                _toast.text = result.Message;
                _toast.style.color = result.Success ? UiKit.Ink : UiKit.Warn;
                _toast.style.display = DisplayStyle.Flex;
                _toastSecondsLeft = ToastSeconds;
            }

            Refresh();
        }
    }
}
