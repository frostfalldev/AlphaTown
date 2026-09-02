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

        /// <summary>
        /// A 1x1 white sprite, so the town is visible before there is any art.
        ///
        /// Tinted per building rather than textured: placeholder squares that differ by colour are
        /// enough to tell a field from a bakery while the loop is what is being tested.
        /// </summary>
        public static Sprite CreateSolidSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sprite.name = "AlphaTown Placeholder";
            return sprite;
        }
    }
}
