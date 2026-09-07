using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Applies AlphaTown's Player Settings for Android (primary) and iOS (secondary).
    ///
    /// A handful of newer APIs are reached by reflection. They are genuine wins, but a hard
    /// reference to one that moved between Unity versions would fail the whole editor assembly
    /// and take the rest of the setup tooling down with it.
    /// </summary>
    internal static class PlayerSettingsConfigurator
    {
        [MenuItem("AlphaTown/Setup/Apply Player Settings", false, 120)]
        internal static void Apply()
        {
            var report = new StringBuilder();
            report.AppendLine("[AlphaTown] Player Settings");

            ApplyIdentity(report);
            ApplyInputHandling(report);
            ApplyOrientation(report);
            ApplyRenderingAndPerformance(report);
            ApplyAndroid(report);
            ApplyIos(report);

            AssetDatabase.SaveAssets();
            report.AppendLine();
            report.AppendLine("  Done. File ▸ Save Project to flush ProjectSettings to disk.");
            Debug.Log(report.ToString());
        }

        static void ApplyIdentity(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Identity");

            PlayerSettings.companyName = AlphaTownProjectProfile.CompanyName;
            PlayerSettings.productName = AlphaTownProjectProfile.ProductName;
            PlayerSettings.bundleVersion = AlphaTownProjectProfile.BundleVersion;

            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, AlphaTownProjectProfile.ApplicationIdentifier);
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.iOS, AlphaTownProjectProfile.ApplicationIdentifier);

            report.AppendLine("    company / product: " + AlphaTownProjectProfile.CompanyName +
                              " / " + AlphaTownProjectProfile.ProductName);
            report.AppendLine("    application id:    " + AlphaTownProjectProfile.ApplicationIdentifier +
                              "   (placeholder — confirm before first upload)");
            report.AppendLine("    version:           " + AlphaTownProjectProfile.BundleVersion);
        }

        /// <summary>
        /// Allows the legacy Input class alongside the new Input System.
        ///
        /// IsoCameraController reads Input.GetTouch. With Active Input Handling left on
        /// "Input System Package (New)" — the Unity 6 default — that throws at runtime rather than
        /// failing at compile time, so it looks like a camera bug rather than a settings one.
        ///
        /// Unity requires an editor restart for this to take effect.
        /// </summary>
        static void ApplyInputHandling(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Input");

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                report.AppendLine("    ? could not open ProjectSettings.asset — set Active Input " +
                                  "Handling to Both by hand");
                return;
            }

            // 0 = Input Manager (Old), 1 = Input System Package (New), 2 = Both.
            var serialized = new SerializedObject(assets[0]);
            if (!SerializedSettingWriter.TrySet(serialized, "activeInputHandler", 2, report)) return;

            serialized.ApplyModifiedProperties();
            report.AppendLine("    active input handling: Both   (restart the editor to apply)");
        }

        static void ApplyOrientation(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Orientation");

            // Auto-rotation limited to the allowed set, so tablets can flip but the game never
            // has to lay out an orientation it was not designed for.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = AlphaTownProjectProfile.AllowLandscape;
            PlayerSettings.allowedAutorotateToLandscapeRight = AlphaTownProjectProfile.AllowLandscape;
            PlayerSettings.allowedAutorotateToPortrait = AlphaTownProjectProfile.AllowPortrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.useAnimatedAutorotation = true;

            report.AppendLine("    landscape: " + AlphaTownProjectProfile.AllowLandscape +
                              "   portrait: " + AlphaTownProjectProfile.AllowPortrait);
        }

        static void ApplyRenderingAndPerformance(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Rendering and runtime");

            // Linear is required for URP to light correctly. Non-negotiable.
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.bakeCollisionMeshes = true;
            PlayerSettings.stripEngineCode = true;

            // A farm game is played with the player's own music on. Never seize the audio session.
            PlayerSettings.muteOtherAudioSources = false;

            // Nothing needs to run while backgrounded: offline progress is derived from
            // timestamps on resume, so burning battery in the background buys nothing.
            PlayerSettings.runInBackground = false;

            // Unused, and polling it costs power.
            PlayerSettings.accelerometerFrequency = 0;

            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.iOS, true);

            // Requires a Plus/Pro licence to take effect; harmless to set on Personal.
            PlayerSettings.SplashScreen.show = false;

            report.AppendLine("    colour space:        Linear");
            report.AppendLine("    GPU skinning:        on");
            report.AppendLine("    engine code strip:   on");
            report.AppendLine("    run in background:   off");
            report.AppendLine("    accelerometer:       disabled");
        }

        static void ApplyAndroid(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Android");

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);

            PlayerSettings.Android.minSdkVersion = AlphaTownProjectProfile.AndroidMinimumSdk;
            PlayerSettings.Android.targetSdkVersion = AlphaTownProjectProfile.AndroidTargetSdk;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = AlphaTownProjectProfile.AndroidBundleVersionCode;
            PlayerSettings.Android.androidIsGame = true;

            // Vulkan first on Unity 6; GLES3 stays as the fallback for older drivers.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                GraphicsDeviceType.Vulkan,
                GraphicsDeviceType.OpenGLES3
            });

            // Play distributes per-ABI splits from a single bundle, so shipping both
            // architectures costs the player nothing at download time.
            EditorUserBuildSettings.buildAppBundle = true;

            TrySetEnumByName("SetIl2CppCompilerConfiguration", NamedBuildTarget.Android, "Release", report);
            TrySetEnumByName("SetIl2CppCodeGeneration", NamedBuildTarget.Android, "OptimizeSize", report);
            TrySetAndroidProperty("optimizedFramePacing", true, report);

            report.AppendLine("    scripting backend:  IL2CPP");
            report.AppendLine("    api compatibility:  .NET Standard");
            report.AppendLine("    managed stripping:  Medium   (add link.xml if reflection breaks)");
            report.AppendLine("    architectures:      ARMv7 + ARM64");
            report.AppendLine("    min SDK:            " + AlphaTownProjectProfile.AndroidMinimumSdk);
            report.AppendLine("    graphics APIs:      Vulkan, OpenGLES3");
            report.AppendLine("    output:             App Bundle (.aab)");
        }

        static void ApplyIos(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  iOS");

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.iOS, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);

            PlayerSettings.iOS.targetOSVersionString = AlphaTownProjectProfile.IosMinimumVersion;
            PlayerSettings.iOS.buildNumber = AlphaTownProjectProfile.IosBuildNumber;
            PlayerSettings.iOS.requiresFullScreen = true;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });

            report.AppendLine("    minimum version:    iOS " + AlphaTownProjectProfile.IosMinimumVersion);
            report.AppendLine("    graphics API:       Metal");
            report.AppendLine("    NOTE: signing and capabilities are left untouched — they carry secrets.");
        }

        // --- Version-tolerant setters ---------------------------------------------------------

        /// <summary>
        /// Calls PlayerSettings.{method}(NamedBuildTarget, {enum value}) when it exists, resolving
        /// the enum type from the method signature so a namespace move cannot break the build.
        /// </summary>
        static void TrySetEnumByName(string methodName, NamedBuildTarget target, string valueName,
                                     StringBuilder report)
        {
            try
            {
                var method = typeof(PlayerSettings).GetMethod(
                    methodName, BindingFlags.Public | BindingFlags.Static);

                if (method == null)
                {
                    report.AppendLine("    ? PlayerSettings." + methodName + " not in this Unity version, skipped");
                    return;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 2 || !parameters[1].ParameterType.IsEnum)
                {
                    report.AppendLine("    ? PlayerSettings." + methodName + " has an unexpected signature, skipped");
                    return;
                }

                var enumType = parameters[1].ParameterType;
                if (!Enum.IsDefined(enumType, valueName))
                {
                    report.AppendLine("    ? " + enumType.Name + "." + valueName + " is not defined, skipped");
                    return;
                }

                method.Invoke(null, new object[] { target, Enum.Parse(enumType, valueName) });
                report.AppendLine("    " + methodName + ": " + valueName);
            }
            catch (Exception exception)
            {
                report.AppendLine("    ! " + methodName + " failed: " + exception.Message);
            }
        }

        static void TrySetAndroidProperty(string propertyName, object value, StringBuilder report)
        {
            try
            {
                var androidType = typeof(PlayerSettings).GetNestedType("Android", BindingFlags.Public);
                var property = androidType == null
                    ? null
                    : androidType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);

                if (property == null || !property.CanWrite)
                {
                    report.AppendLine("    ? PlayerSettings.Android." + propertyName +
                                      " not in this Unity version, skipped");
                    return;
                }

                property.SetValue(null, value);
                report.AppendLine("    " + propertyName + ": " + value);
            }
            catch (Exception exception)
            {
                report.AppendLine("    ! " + propertyName + " failed: " + exception.Message);
            }
        }
    }
}
