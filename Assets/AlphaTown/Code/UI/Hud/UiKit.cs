using AlphaTown.Core.Randomness;
using UnityEngine;
using UnityEngine.UIElements;

namespace AlphaTown.UI.Hud
{
    /// <summary>
    /// The handful of widgets the slice needs, built in C# with inline styles.
    ///
    /// No UXML and no USS on purpose. The layout is still moving, and a stylesheet split across
    /// three asset files is a cost you pay every time it moves; keeping it in one place means a
    /// change to the HUD is a change to one file. It also means the whole UI compiles and runs
    /// from source alone, with no asset GUIDs to go missing.
    ///
    /// TODO(polish): once the layout settles, lift this into a USS theme and the panels into UXML
    /// so a designer can edit them in the UI Builder without touching code.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.96f, 0.96f, 0.93f);
        public static readonly Color Muted = new Color(0.72f, 0.72f, 0.68f);
        public static readonly Color Panel = new Color(0.11f, 0.13f, 0.15f, 0.92f);
        public static readonly Color Accent = new Color(0.42f, 0.72f, 0.36f);
        public static readonly Color Warn = new Color(0.85f, 0.42f, 0.32f);
        public static readonly Color ButtonFace = new Color(0.22f, 0.26f, 0.29f);
        public static readonly Color ButtonDisabled = new Color(0.18f, 0.19f, 0.20f);

        /// <summary>
        /// Minimum edge of anything tappable, in reference pixels. Roughly a fingertip: below this
        /// the slice stops being testable on a phone, which is the only place it matters.
        /// </summary>
        public const float TouchTarget = 88f;

        public static VisualElement Row(float gap = 8f)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            SetGap(row, gap);
            return row;
        }

        public static VisualElement Column(float gap = 8f)
        {
            var column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            SetGap(column, gap);
            return column;
        }

        public static VisualElement Card(float padding = 12f)
        {
            var card = new VisualElement();
            card.style.backgroundColor = Panel;
            card.style.paddingLeft = padding;
            card.style.paddingRight = padding;
            card.style.paddingTop = padding;
            card.style.paddingBottom = padding;
            Round(card, 14f);
            return card;
        }

        public static Label Text(string value, int size = 26, bool bold = false)
        {
            var label = new Label(value);
            label.style.color = Ink;
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        public static Label Caption(string value)
        {
            var label = Text(value, 20);
            label.style.color = Muted;
            return label;
        }

        public static Button Action(string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.minHeight = TouchTarget;
            button.style.minWidth = TouchTarget * 1.4f;
            button.style.fontSize = 26;
            button.style.color = Ink;
            button.style.backgroundColor = ButtonFace;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 20f;
            button.style.paddingRight = 20f;
            Round(button, 12f);
            button.style.borderTopWidth = 0f;
            button.style.borderBottomWidth = 0f;
            button.style.borderLeftWidth = 0f;
            button.style.borderRightWidth = 0f;
            return button;
        }

        /// <summary>
        /// Greys a button out and blocks it. Kept as one call so "cannot afford" always looks the
        /// same, wherever it is decided.
        /// </summary>
        public static void SetEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
            button.style.backgroundColor = enabled ? ButtonFace : ButtonDisabled;
            button.style.color = enabled ? Ink : Muted;
        }

        /// <summary>A filled bar, 0..1. Used for growth and construction.</summary>
        public static VisualElement ProgressBar(out VisualElement fill, float height = 10f)
        {
            var track = new VisualElement();
            track.style.height = height;
            track.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            Round(track, height * 0.5f);

            fill = new VisualElement();
            fill.style.height = height;
            fill.style.width = new Length(0f, LengthUnit.Percent);
            fill.style.backgroundColor = Accent;
            Round(fill, height * 0.5f);

            track.Add(fill);
            return track;
        }

        public static void SetProgress(VisualElement fill, float progress01) =>
            fill.style.width = new Length(Mathf.Clamp01(progress01) * 100f, LengthUnit.Percent);

        public static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        /// <summary>
        /// A rounded, tinted square with an initial in it — what stands in for an item icon until
        /// there is art.
        ///
        /// A list of sixteen goods that are all the same grey shape is read by reading, which is
        /// exactly what an icon is for. The tint comes from the id, so a good keeps its colour
        /// everywhere it appears and across sessions, and two goods are only ever the same colour
        /// by coincidence rather than because nothing was authored.
        /// </summary>
        public static VisualElement Chip(string id, string label, float size = 40f, Color? tint = null)
        {
            var fill = tint ?? TintFor(id);

            var chip = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = size,
                    height = size,
                    backgroundColor = fill,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    flexShrink = 0f
                }
            };

            Round(chip, size * 0.28f);

            var initial = Text(Initial(label), Mathf.RoundToInt(size * 0.45f), true);
            initial.pickingMode = PickingMode.Ignore;

            // A caller can hand in any colour — a building's placeholder tint is authored per
            // building and some of them are mud — so the letter picks its own side rather than
            // assuming a pale chip.
            initial.style.color = Luminance(fill) > 0.55f
                ? new Color(0.10f, 0.11f, 0.12f)
                : Ink;

            chip.Add(initial);

            return chip;
        }

        static float Luminance(Color colour) => 0.299f * colour.r + 0.587f * colour.g + 0.114f * colour.b;

        /// <summary>
        /// A colour from an id: hashed to a hue, then held to one saturation and value so every
        /// chip sits at the same weight against the panel and none of them fights the text.
        /// </summary>
        public static Color TintFor(string id)
        {
            var seed = DeterministicRoll.Seed(id ?? string.Empty, 0);
            var hue = DeterministicRoll.Range(seed, 0, 359) / 360f;

            return Color.HSVToRGB(hue, 0.45f, 0.86f);
        }

        static string Initial(string label)
        {
            if (string.IsNullOrEmpty(label)) return "?";

            // Two letters for a two-word name — "Goat Cheese" reads as GC, which separates it from
            // cheese at a glance where a lone C would not.
            var space = label.IndexOf(' ');
            if (space > 0 && space + 1 < label.Length)
                return char.ToUpperInvariant(label[0]).ToString() + char.ToUpperInvariant(label[space + 1]);

            return char.ToUpperInvariant(label[0]).ToString();
        }

        /// <summary>
        /// Spacing between children. Written as margins rather than the <c>gap</c> properties so
        /// the HUD renders the same on every UI Toolkit version this project might be opened with.
        /// </summary>
        static void SetGap(VisualElement container, float gap)
        {
            container.RegisterCallback<GeometryChangedEvent>(_ => ApplyGap(container, gap));
            ApplyGap(container, gap);
        }

        static void ApplyGap(VisualElement container, float gap)
        {
            var horizontal = container.style.flexDirection.value == FlexDirection.Row;

            for (var i = 0; i < container.childCount; i++)
            {
                var child = container[i];
                if (horizontal) child.style.marginRight = i == container.childCount - 1 ? 0f : gap;
                else child.style.marginBottom = i == container.childCount - 1 ? 0f : gap;
            }
        }
    }
}
