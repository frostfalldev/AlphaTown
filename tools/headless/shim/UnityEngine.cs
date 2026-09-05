// Minimal stand-ins for the UnityEngine API this project touches, so the simulation can be
// compiled and its tests run without a Unity Editor. Signatures mirror Unity's; behaviour is only
// as faithful as the tests need. See headless/README.md for what this does and does not prove.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name = string.Empty;
        public HideFlags hideFlags;
        public override string ToString() => name;
    }

    public enum HideFlags { None = 0, HideAndDontSave = 61 }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject =>
            (T)Activator.CreateInstance(typeof(T));
        public static ScriptableObject CreateInstance(Type type) => (ScriptableObject)Activator.CreateInstance(type);
    }

    public class Component : Object
    {
        public Transform transform { get; } = new Transform();
        public GameObject gameObject { get; } = new GameObject();
        public T GetComponent<T>() where T : Component => null;
    }

    public class Behaviour : Component { public bool enabled = true; }

    public class MonoBehaviour : Behaviour
    {
        public static T FindAnyObjectByType<T>() where T : Component => null;
        public static T FindFirstObjectByType<T>() where T : Component => null;
        public static void Destroy(Object target) { }
        public static void print(object message) { }
    }

    public class Transform : Object
    {
        public GameObject gameObject { get; } = new GameObject();
        public Vector3 position;
        public Vector3 localPosition;
        public Vector3 localScale = Vector3.one;
        public Quaternion rotation;
        public int childCount => 0;
        public Transform GetChild(int index) => null;
        public void SetParent(Transform parent, bool worldPositionStays) { }
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public string tag = "Untagged";
        public Transform transform { get; } = new Transform();
        public T AddComponent<T>() where T : Component, new() => new T();
    }

    // --- Attributes -------------------------------------------------------------------------

    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }
    public class PropertyAttribute : Attribute { }
    public sealed class MinAttribute : PropertyAttribute { public MinAttribute(float min) { } }
    public sealed class RangeAttribute : PropertyAttribute { public RangeAttribute(float min, float max) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName; public string fileName; public int order;
    }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class RequireComponent : Attribute
    {
        public RequireComponent(Type type) { }
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute
    {
        public DefaultExecutionOrder(int order) { }
    }
    public enum RuntimeInitializeLoadType { AfterSceneLoad = 0, BeforeSceneLoad = 1 }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }

    // --- Diagnostics ------------------------------------------------------------------------

    public enum LogType { Error = 0, Assert = 1, Warning = 2, Log = 3, Exception = 4 }

    public static class Debug
    {
        public static bool isDebugBuild => true;

        public static void Log(object message) => TestTools.LogCapture.Record(LogType.Log, message);
        public static void LogWarning(object message) => TestTools.LogCapture.Record(LogType.Warning, message);
        public static void LogError(object message) => TestTools.LogCapture.Record(LogType.Error, message);
        public static void LogException(Exception exception) => TestTools.LogCapture.Record(LogType.Exception, exception);
    }

    // --- Application ------------------------------------------------------------------------

    public static class Application
    {
        public static string version = "0.0.0-headless";
        public static string persistentDataPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AlphaTownHeadless");
        public static bool isBatchMode => true;
        public static int targetFrameRate { get; set; } = 60;
    }

    public static class QualitySettings
    {
        public static int vSyncCount { get; set; }
        public static string[] names { get; set; } = new string[0];
        public static Object asset { get; set; } = new Object();
        public static AnisotropicFiltering anisotropicFiltering { get; set; }
        public static ShadowmaskMode shadowmaskMode { get; set; }
        public static SkinWeights skinWeights { get; set; }
        public static float lodBias { get; set; }
        public static int particleRaycastBudget { get; set; }
        public static bool realtimeReflectionProbes { get; set; }
        public static int asyncUploadTimeSlice { get; set; }
        public static int asyncUploadBufferSize { get; set; }
        public static Rendering.RenderPipelineAsset renderPipeline { get; set; }
        public static int GetQualityLevel() => 0;
        public static void SetQualityLevel(int index) { }
        public static void SetQualityLevel(int index, bool applyExpensiveChanges) { }
    }

    public static class Time
    {
        public static float deltaTime { get; set; }
        public static float unscaledDeltaTime { get; set; }
        public static float unscaledTime { get; set; }
    }

    public static class Screen { public static int width = 1920; public static int height = 1080; }

    // --- Maths ------------------------------------------------------------------------------

    public static class Mathf
    {
        public const float Epsilon = 1.401298E-45f;
        public const float Deg2Rad = 0.0174532924f;
        public const float Rad2Deg = 57.29578f;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Min(float a, float b, float c, float d) => Min(Min(a, b), Min(c, d));
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b, float c, float d) => Max(Max(a, b), Max(c, d));
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
    }

    [Serializable]
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0f, 0f);
        public float magnitude => Mathf.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public override string ToString() => "(" + x + ", " + y + ")";
    }

    [Serializable]
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) : this(x, y, 0f) { }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0f);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) =>
            new Vector3(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.z, b.z, t));
        public override string ToString() => "(" + x + ", " + y + ", " + z + ")";
    }

    [Serializable]
    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public override string ToString() => "(" + x + ", " + y + ")";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion Euler(float x, float y, float z) => new Quaternion();
    }

    [Serializable]
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public override bool Equals(object obj) => obj is Color c && c.r == r && c.g == g && c.b == b && c.a == a;
        public override int GetHashCode() => r.GetHashCode() ^ g.GetHashCode() ^ b.GetHashCode() ^ a.GetHashCode();
        public static bool operator ==(Color a, Color b) => a.Equals(b);
        public static bool operator !=(Color a, Color b) => !a.Equals(b);
        public override string ToString() => "RGBA(" + r + ", " + g + ", " + b + ", " + a + ")";
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
    }

    // --- Sprites ----------------------------------------------------------------------------

    public enum TextureFormat { RGBA32 = 4 }
    public enum FilterMode { Point = 0, Bilinear = 1 }
    public enum TextureWrapMode { Repeat = 0, Clamp = 1 }

    public class Texture2D : Object
    {
        readonly Color[] _pixels;
        public int width { get; }
        public int height { get; }
        public FilterMode filterMode { get; set; }
        public TextureWrapMode wrapMode { get; set; }

        public Texture2D(int width, int height) : this(width, height, TextureFormat.RGBA32, false) { }
        public Texture2D(int width, int height, TextureFormat format, bool mipChain)
        {
            this.width = width; this.height = height;
            _pixels = new Color[width * height];
        }

        public void SetPixel(int x, int y, Color colour) => _pixels[y * width + x] = colour;
        public Color GetPixel(int x, int y) => _pixels[y * width + x];
        public void SetPixels(Color[] colours) => Array.Copy(colours, _pixels, colours.Length);
        public Color[] GetPixels() => (Color[])_pixels.Clone();
        public void Apply() { }
    }

    public class Sprite : Object
    {
        public Texture2D texture { get; private set; }
        public Rect rect { get; private set; }
        public Vector2 pivot { get; private set; }
        public float pixelsPerUnit { get; private set; }

        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
        {
            // Unity's pivot property is in pixels, not the 0..1 fraction Create takes.
            return new Sprite
            {
                texture = texture,
                rect = rect,
                pivot = new Vector2(pivot.x * rect.width, pivot.y * rect.height),
                pixelsPerUnit = pixelsPerUnit
            };
        }
    }
}
