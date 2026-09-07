using UnityEngine;
using System;
using Object = UnityEngine.Object;

namespace UnityEngine.Rendering
{
    public enum GraphicsDeviceType { OpenGLES3 = 11, Vulkan = 21, Metal = 16, Direct3D11 = 2 }
    public class RenderPipelineAsset : ScriptableObject { }

    public static class GraphicsSettings
    {
        public static RenderPipelineAsset defaultRenderPipeline { get; set; }
        public static RenderPipelineAsset renderPipelineAsset { get; set; }
    }
}

namespace UnityEngine
{
    public enum AnisotropicFiltering { Disable = 0, Enable = 1, ForceEnable = 2 }
    public enum ShadowmaskMode { Shadowmask = 0, DistanceShadowmask = 1 }
    public enum SkinWeights { OneBone = 1, TwoBones = 2, FourBones = 4, Unlimited = 255 }
    public enum ColorSpace { Gamma = 0, Linear = 1 }
    public enum ShadowResolution { Low = 0, Medium = 1, High = 2, VeryHigh = 3 }
}

namespace UnityEditor
{
    public enum BuildTarget { NoTarget = -2, Android = 13, iOS = 9, StandaloneLinux64 = 24 }
    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, iOS = 4, Android = 13 }
    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1 }
    public enum ApiCompatibilityLevel { NET_Standard_2_0 = 6, NET_Unity_4_8 = 3, NET_Standard = 6 }
    public enum ManagedStrippingLevel { Disabled = 0, Low = 1, Medium = 2, High = 3 }
    public enum AndroidSdkVersions { AndroidApiLevelAuto = 0, AndroidApiLevel24 = 24, AndroidApiLevel33 = 33 }
    [Flags] public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, All = 3 }
    public enum UIOrientation { Portrait = 0, PortraitUpsideDown = 1, LandscapeRight = 2, LandscapeLeft = 3, AutoRotation = 4 }
    [Flags] public enum BuildOptions { None = 0, Development = 1, ConnectWithProfiler = 2, AllowDebugging = 32 }
    public enum ImportAssetOptions { Default = 0, ForceSynchronousImport = 8 }

    public struct NamedBuildTarget
    {
        public string TargetName { get; private set; }
        public static NamedBuildTarget Android => new NamedBuildTarget { TargetName = "Android" };
        public static NamedBuildTarget iOS => new NamedBuildTarget { TargetName = "iOS" };
        public static NamedBuildTarget Standalone => new NamedBuildTarget { TargetName = "Standalone" };
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
    }

    public enum SerializedPropertyType
    {
        Generic = -1, Integer = 0, Boolean = 1, Float = 2, String = 3, Color = 4,
        ObjectReference = 5, LayerMask = 6, Enum = 7, Vector2 = 8, Vector3 = 9
    }

    public class SerializedProperty
    {
        public SerializedPropertyType propertyType { get; set; }
        public string stringValue { get; set; }
        public int intValue { get; set; }
        public long longValue { get; set; }
        public float floatValue { get; set; }
        public bool boolValue { get; set; }
        public int enumValueIndex { get; set; }
        public Object objectReferenceValue { get; set; }
        public Color colorValue { get; set; }
        public Vector2 vector2Value { get; set; }
        public Vector3 vector3Value { get; set; }
        public bool isArray { get; set; }
        public int arraySize { get; set; }
        public string propertyPath => string.Empty;

        public SerializedProperty GetArrayElementAtIndex(int index) => new SerializedProperty();
        public SerializedProperty FindPropertyRelative(string path) => new SerializedProperty();
    }

    public class SerializedObject
    {
        public SerializedObject(Object target) { targetObject = target; }
        public SerializedObject(Object[] targets) { targetObject = targets.Length > 0 ? targets[0] : null; }

        public Object targetObject { get; }

        public SerializedProperty FindProperty(string path) => new SerializedProperty();
        public bool ApplyModifiedProperties() => true;
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
        public void Update() { }
    }

    public static class AssetDatabase
    {
        public static bool IsValidFolder(string path) => false;
        public static string CreateFolder(string parent, string name) => string.Empty;
        public static void CreateAsset(Object asset, string path) { }
        public static bool CopyAsset(string from, string to) => true;
        public static string[] FindAssets(string filter) => Array.Empty<string>();
        public static string GUIDToAssetPath(string guid) => string.Empty;
        public static string GetAssetPath(Object asset) => string.Empty;
        public static T LoadAssetAtPath<T>(string path) where T : Object => null;
        public static Object LoadAssetAtPath(string path, Type type) => null;
        public static Object[] LoadAllAssetsAtPath(string path) => Array.Empty<Object>();
        public static void ImportAsset(string path, ImportAssetOptions options = ImportAssetOptions.Default) { }
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
    }

    public static class EditorUtility
    {
        public static void SetDirty(Object target) { }
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => true;
    }

    public static class EditorApplication { public static void Exit(int code) { } }

    public sealed class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
        public string path { get; }
        public bool enabled { get; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; } = Array.Empty<EditorBuildSettingsScene>();
    }

    public static class EditorUserBuildSettings
    {
        public static BuildTarget activeBuildTarget => BuildTarget.Android;
        public static bool buildAppBundle { get; set; }
        public static bool SwitchActiveBuildTarget(BuildTargetGroup group, BuildTarget target) => true;
    }

    public static class PlayerSettings
    {
        public static string companyName { get; set; }
        public static string productName { get; set; }
        public static string bundleVersion { get; set; }
        public static ColorSpace colorSpace { get; set; }
        public static bool runInBackground { get; set; }
        public static bool muteOtherAudioSources { get; set; }
        public static bool bakeCollisionMeshes { get; set; }
        public static bool gpuSkinning { get; set; }
        public static bool stripEngineCode { get; set; }
        public static bool useAnimatedAutorotation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static int accelerometerFrequency { get; set; }

        public static void SetApplicationIdentifier(NamedBuildTarget target, string identifier) { }
        public static void SetScriptingBackend(NamedBuildTarget target, ScriptingImplementation backend) { }
        public static void SetApiCompatibilityLevel(NamedBuildTarget target, ApiCompatibilityLevel level) { }
        public static void SetManagedStrippingLevel(NamedBuildTarget target, ManagedStrippingLevel level) { }
        public static void SetMobileMTRendering(NamedBuildTarget target, bool enable) { }
        public static void SetUseDefaultGraphicsAPIs(BuildTarget target, bool automatic) { }
        public static void SetGraphicsAPIs(BuildTarget target, UnityEngine.Rendering.GraphicsDeviceType[] apis) { }

        public static class Android
        {
            public static int bundleVersionCode { get; set; }
            public static AndroidSdkVersions minSdkVersion { get; set; }
            public static AndroidSdkVersions targetSdkVersion { get; set; }
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static bool androidIsGame { get; set; }
            public static bool optimizedFramePacing { get; set; }
        }

        public static class iOS
        {
            public static string buildNumber { get; set; }
            public static string targetOSVersionString { get; set; }
            public static bool requiresFullScreen { get; set; }
        }

        public static class SplashScreen { public static bool show { get; set; } }
    }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }

    public struct BuildSummary
    {
        public BuildResult result;
        public ulong totalSize;
        public TimeSpan totalTime;
        public int totalErrors;
    }

    public class BuildReport : UnityEngine.Object { public BuildSummary summary { get; set; } }
}

namespace UnityEditor
{
    public struct BuildPlayerOptions
    {
        public string[] scenes;
        public string locationPathName;
        public BuildTarget target;
        public BuildTargetGroup targetGroup;
        public BuildOptions options;
    }

    public static class BuildPipeline
    {
        public static bool IsBuildTargetSupported(BuildTargetGroup group, BuildTarget target) => true;
        public static Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions options) =>
            new Build.Reporting.BuildReport();
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name; public string path; }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene = 0, DefaultGameObjects = 1 }
    public enum NewSceneMode { Single = 0, Additive = 1 }

    public static class EditorSceneManager
    {
        public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup setup, NewSceneMode mode) =>
            new UnityEngine.SceneManagement.Scene();

        public static bool SaveScene(UnityEngine.SceneManagement.Scene scene, string path) => true;
    }
}
