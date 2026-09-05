using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements
{
    public enum PickingMode { Position = 0, Ignore = 1 }
    public enum FlexDirection { Column = 0, ColumnReverse = 1, Row = 2, RowReverse = 3 }
    public enum Align { Auto = 0, FlexStart = 1, Center = 2, FlexEnd = 3, Stretch = 4 }
    public enum Justify { FlexStart = 0, Center = 1, FlexEnd = 2, SpaceBetween = 3, SpaceAround = 4 }
    public enum Wrap { NoWrap = 0, Wrap = 1, WrapReverse = 2 }
    public enum DisplayStyle { Flex = 0, None = 1 }
    public enum WhiteSpace { Normal = 0, NoWrap = 1 }
    public enum ScrollViewMode { Vertical = 0, Horizontal = 1, VerticalAndHorizontal = 2 }
    public enum LengthUnit { Pixel = 0, Percent = 1 }
    public enum PanelScaleMode { ConstantPixelSize = 0, ScaleWithScreenSize = 1, ConstantPhysicalSize = 2 }

    public struct Length
    {
        public float value; public LengthUnit unit;
        public Length(float value) { this.value = value; unit = LengthUnit.Pixel; }
        public Length(float value, LengthUnit unit) { this.value = value; this.unit = unit; }
        public static implicit operator Length(float value) => new Length(value);
    }

    // Style values wrap their payload in Unity so a property can also be "keyword" or unset.
    // Only the implicit conversions and .value are needed here.
    public struct StyleFloat { public float value; public static implicit operator StyleFloat(float v) => new StyleFloat { value = v }; }
    public struct StyleInt { public int value; public static implicit operator StyleInt(int v) => new StyleInt { value = v }; }
    public struct StyleColor { public Color value; public static implicit operator StyleColor(Color v) => new StyleColor { value = v }; }
    public struct StyleLength
    {
        public Length value;
        public static implicit operator StyleLength(Length v) => new StyleLength { value = v };
        public static implicit operator StyleLength(float v) => new StyleLength { value = new Length(v) };
    }
    public struct StyleEnum<T> where T : struct
    {
        public T value;
        public static implicit operator StyleEnum<T>(T v) => new StyleEnum<T> { value = v };
    }

    public interface IStyle
    {
        StyleEnum<FlexDirection> flexDirection { get; set; }
        StyleEnum<Align> alignItems { get; set; }
        StyleEnum<Align> alignSelf { get; set; }
        StyleEnum<Justify> justifyContent { get; set; }
        StyleEnum<Wrap> flexWrap { get; set; }
        StyleEnum<DisplayStyle> display { get; set; }
        StyleEnum<WhiteSpace> whiteSpace { get; set; }
        StyleEnum<FontStyle> unityFontStyleAndWeight { get; set; }
        StyleColor backgroundColor { get; set; }
        StyleColor color { get; set; }
        StyleLength width { get; set; }
        StyleLength height { get; set; }
        StyleLength minWidth { get; set; }
        StyleLength minHeight { get; set; }
        StyleLength maxWidth { get; set; }
        StyleLength maxHeight { get; set; }
        StyleFloat flexGrow { get; set; }
        StyleLength fontSize { get; set; }
        StyleLength paddingLeft { get; set; }
        StyleLength paddingRight { get; set; }
        StyleLength paddingTop { get; set; }
        StyleLength paddingBottom { get; set; }
        StyleLength marginLeft { get; set; }
        StyleLength marginRight { get; set; }
        StyleLength marginTop { get; set; }
        StyleLength marginBottom { get; set; }
        StyleLength borderTopLeftRadius { get; set; }
        StyleLength borderTopRightRadius { get; set; }
        StyleLength borderBottomLeftRadius { get; set; }
        StyleLength borderBottomRightRadius { get; set; }
        StyleFloat borderTopWidth { get; set; }
        StyleFloat borderBottomWidth { get; set; }
        StyleFloat borderLeftWidth { get; set; }
        StyleFloat borderRightWidth { get; set; }
    }

    sealed class Style : IStyle
    {
        public StyleEnum<FlexDirection> flexDirection { get; set; }
        public StyleEnum<Align> alignItems { get; set; }
        public StyleEnum<Align> alignSelf { get; set; }
        public StyleEnum<Justify> justifyContent { get; set; }
        public StyleEnum<Wrap> flexWrap { get; set; }
        public StyleEnum<DisplayStyle> display { get; set; }
        public StyleEnum<WhiteSpace> whiteSpace { get; set; }
        public StyleEnum<FontStyle> unityFontStyleAndWeight { get; set; }
        public StyleColor backgroundColor { get; set; }
        public StyleColor color { get; set; }
        public StyleLength width { get; set; }
        public StyleLength height { get; set; }
        public StyleLength minWidth { get; set; }
        public StyleLength minHeight { get; set; }
        public StyleLength maxWidth { get; set; }
        public StyleLength maxHeight { get; set; }
        public StyleFloat flexGrow { get; set; }
        public StyleLength fontSize { get; set; }
        public StyleLength paddingLeft { get; set; }
        public StyleLength paddingRight { get; set; }
        public StyleLength paddingTop { get; set; }
        public StyleLength paddingBottom { get; set; }
        public StyleLength marginLeft { get; set; }
        public StyleLength marginRight { get; set; }
        public StyleLength marginTop { get; set; }
        public StyleLength marginBottom { get; set; }
        public StyleLength borderTopLeftRadius { get; set; }
        public StyleLength borderTopRightRadius { get; set; }
        public StyleLength borderBottomLeftRadius { get; set; }
        public StyleLength borderBottomRightRadius { get; set; }
        public StyleFloat borderTopWidth { get; set; }
        public StyleFloat borderBottomWidth { get; set; }
        public StyleFloat borderLeftWidth { get; set; }
        public StyleFloat borderRightWidth { get; set; }
    }

    public class EventBase { }
    public class GeometryChangedEvent : EventBase { }
    public delegate void EventCallback<in TEvent>(TEvent evt);

    public interface IPanel { VisualElement Pick(Vector2 point); }

    public class VisualElement
    {
        readonly List<VisualElement> _children = new List<VisualElement>();

        public IStyle style { get; } = new Style();
        public PickingMode pickingMode { get; set; } = PickingMode.Position;
        public object userData { get; set; }
        public string name { get; set; } = string.Empty;
        public IPanel panel { get; set; }

        public int childCount => _children.Count;
        public VisualElement this[int index] => _children[index];

        public void Add(VisualElement child) => _children.Add(child);
        public void Clear() => _children.Clear();
        public void SetEnabled(bool enabled) => Enabled = enabled;
        public bool Enabled { get; private set; } = true;

        public void RegisterCallback<TEvent>(EventCallback<TEvent> callback) where TEvent : EventBase { }
    }

    public class TextElement : VisualElement { public string text { get; set; } = string.Empty; }
    public class Label : TextElement { public Label() { } public Label(string text) { this.text = text; } }

    public class Clickable { public event Action clicked; public void Invoke() => clicked?.Invoke(); }

    public class Button : TextElement
    {
        public Clickable clickable { get; } = new Clickable();
        public Button() { }
        public Button(Action onClick) { if (onClick != null) clickable.clicked += onClick; }
        public event Action clicked { add { clickable.clicked += value; } remove { clickable.clicked -= value; } }
    }

    public class Image : VisualElement { public Sprite sprite { get; set; } }

    public class ScrollView : VisualElement
    {
        public ScrollView() { }
        public ScrollView(ScrollViewMode mode) { }
    }

    public static class RuntimePanelUtils
    {
        public static Vector2 ScreenToPanel(IPanel panel, Vector2 screenPosition) => screenPosition;
    }

    public class StyleSheet : ScriptableObject { }
    public class ThemeStyleSheet : StyleSheet { }

    public class PanelSettings : ScriptableObject
    {
        public PanelScaleMode scaleMode { get; set; }
        public Vector2Int referenceResolution { get; set; }
        public float match { get; set; }
        public ThemeStyleSheet themeStyleSheet { get; set; }
    }

    public class UIDocument : MonoBehaviour
    {
        public VisualElement rootVisualElement { get; } = new VisualElement();
        public PanelSettings panelSettings { get; set; }
    }
}

namespace UnityEngine
{
    public enum FontStyle { Normal = 0, Bold = 1, Italic = 2, BoldAndItalic = 3 }
    public enum CameraClearFlags { Skybox = 1, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum LightType { Spot = 0, Directional = 1, Point = 2 }
    public enum TouchPhase { Began = 0, Moved = 1, Stationary = 2, Ended = 3, Canceled = 4 }

    public struct Touch
    {
        public int fingerId;
        public Vector2 position;
        public Vector2 deltaPosition;
        public TouchPhase phase;
    }

    /// <summary>
    /// The legacy input class. Present so the legacy pointer source compiles; it reports nothing,
    /// which is correct for a headless run.
    /// </summary>
    public static class Input
    {
        public static int touchCount => 0;
        public static Touch GetTouch(int index) => new Touch();
        public static bool mousePresent => false;
        public static Vector3 mousePosition => Vector3.zero;
        public static Vector2 mouseScrollDelta => Vector2.zero;
        public static bool GetMouseButton(int button) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static bool GetMouseButtonUp(int button) => false;
        public static float GetAxis(string name) => 0f;
    }

    public class Renderer : Component { public bool enabled { get; set; } = true; public int sortingOrder { get; set; } }
    public class SpriteRenderer : Renderer { public Sprite sprite { get; set; } public Color color { get; set; } = Color.white; }
    public class TrailRenderer : Renderer { public bool emitting { get; set; } public void Clear() { } }
    public class ParticleSystem : Component { public bool isPlaying => false; public void Play() { } }
    public class Light : Behaviour { public LightType type { get; set; } public float intensity { get; set; } }
    public class AudioListener : Behaviour { }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public float aspect { get; set; } = 16f / 9f;
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public Vector3 ScreenToWorldPoint(Vector3 position) => position;
    }
}
