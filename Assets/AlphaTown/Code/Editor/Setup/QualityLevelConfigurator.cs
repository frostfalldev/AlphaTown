using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Rebuilds the project's quality levels as exactly three — Low, Medium, High — each backed by
    /// its own URP asset.
    ///
    /// The Universal 3D template ships two pipeline assets under names that vary by Unity version,
    /// so rather than depend on those, this creates AlphaTown_Low/Medium/High by copying an
    /// existing asset. Copying the .asset file carries the script reference across without this
    /// tooling needing to reference the URP assembly at all.
    ///
    /// The names are load-bearing: MobileUrpConfigurator maps "low" to the Performance tier and
    /// "high" to Fidelity, so tuning follows automatically. Run this first, then the URP profile.
    /// </summary>
    internal static class QualityLevelConfigurator
    {
        const string SettingsFolder = "Assets/Settings";
        const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";

        static readonly string[] LevelNames = { "Low", "Medium", "High" };

        static readonly string[] PipelineAssetNames =
        {
            "AlphaTown_Low",
            "AlphaTown_Medium",
            "AlphaTown_High"
        };

        [MenuItem("AlphaTown/Setup/Apply Quality Levels", false, 130)]
        internal static void Apply()
        {
            var report = new StringBuilder();
            report.AppendLine("[AlphaTown] Quality levels");

            var pipelines = EnsurePipelineAssets(report);
            if (pipelines == null)
            {
                Debug.LogWarning(report + "\n  Aborted: no URP asset to work from. See docs/SETUP.md.");
                return;
            }

            if (!RebuildLevels(pipelines, report))
            {
                Debug.LogWarning(report.ToString());
                return;
            }

            AssetDatabase.SaveAssets();
            report.AppendLine();
            report.AppendLine("  Done. Run AlphaTown ▸ Setup ▸ Apply Mobile URP Profile next to tune each tier.");
            Debug.Log(report.ToString());
        }

        // --- URP assets -------------------------------------------------------------------------

        static RenderPipelineAsset[] EnsurePipelineAssets(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Pipeline assets");

            var candidates = new List<RenderPipelineAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                if (path.StartsWith("Packages/", StringComparison.Ordinal)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (asset != null) candidates.Add(asset);
            }

            if (candidates.Count == 0)
            {
                report.AppendLine("    no UniversalRenderPipelineAsset found in Assets/");
                return null;
            }

            var source = PickSource(candidates);
            var sourcePath = AssetDatabase.GetAssetPath(source);

            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                AssetDatabase.CreateFolder("Assets", "Settings");

            var pipelines = new RenderPipelineAsset[PipelineAssetNames.Length];
            for (var i = 0; i < PipelineAssetNames.Length; i++)
            {
                var targetPath = SettingsFolder + "/" + PipelineAssetNames[i] + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(targetPath);

                if (asset == null)
                {
                    if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                    {
                        report.AppendLine("    ! could not copy " + sourcePath + " to " + targetPath);
                        return null;
                    }

                    AssetDatabase.ImportAsset(targetPath);
                    asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(targetPath);
                    report.AppendLine("    created " + targetPath + "  (from " + sourcePath + ")");
                }
                else
                {
                    report.AppendLine("    reusing " + targetPath);
                }

                if (asset == null)
                {
                    report.AppendLine("    ! " + targetPath + " did not load after copy");
                    return null;
                }

                pipelines[i] = asset;
            }

            return pipelines;
        }

        /// <summary>Prefers the template's mobile asset; anything is better than nothing.</summary>
        static RenderPipelineAsset PickSource(List<RenderPipelineAsset> candidates)
        {
            RenderPipelineAsset fallback = null;

            for (var i = 0; i < candidates.Count; i++)
            {
                var name = candidates[i].name;
                if (Array.IndexOf(PipelineAssetNames, name) >= 0) continue; // One of ours.

                var lower = name.ToLowerInvariant();
                if (lower.Contains("mobile") || lower.Contains("balanced")) return candidates[i];
                if (fallback == null) fallback = candidates[i];
            }

            return fallback ?? candidates[0];
        }

        // --- Quality levels ---------------------------------------------------------------------

        static bool RebuildLevels(RenderPipelineAsset[] pipelines, StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Levels");

            var settingsAssets = AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath);
            if (settingsAssets == null || settingsAssets.Length == 0 || settingsAssets[0] == null)
            {
                report.AppendLine("    ! could not open " + QualitySettingsPath);
                return false;
            }

            var serialized = new SerializedObject(settingsAssets[0]);
            var levels = serialized.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
            {
                report.AppendLine("    ! m_QualitySettings is not an array in this Unity version");
                return false;
            }

            // Deliberately destructive: the template's levels are replaced by exactly these three.
            levels.arraySize = LevelNames.Length;

            for (var i = 0; i < LevelNames.Length; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                report.AppendLine("    " + LevelNames[i] + " -> " + pipelines[i].name);

                SerializedSettingWriter.TrySetRelative(level, "name", LevelNames[i], report);
                SerializedSettingWriter.TrySetRelative(level, "customRenderPipeline", pipelines[i], report);

                // Frame rate is owned by Application.targetFrameRate, never by vsync.
                SerializedSettingWriter.TrySetRelative(level, "vSyncCount", 0, report);

                // MSAA lives in the URP asset. Leaving it here as well double-books the setting.
                SerializedSettingWriter.TrySetRelative(level, "antiAliasing", 0, report);

                SerializedSettingWriter.TrySetRelative(level, "anisotropicTextures", i == 0 ? 0 : 1, report);
                SerializedSettingWriter.TrySetRelative(level, "skinWeights", i == 2 ? 4 : 2, report);
                SerializedSettingWriter.TrySetRelative(level, "lodBias", LodBiasFor(i), report);
                SerializedSettingWriter.TrySetRelative(level, "maximumLODLevel", 0, report);
                SerializedSettingWriter.TrySetRelative(level, "particleRaycastBudget", ParticleBudgetFor(i), report);
                SerializedSettingWriter.TrySetRelative(level, "realtimeReflectionProbes", i == 2, report);
                SerializedSettingWriter.TrySetRelative(level, "softParticles", false, report);
                SerializedSettingWriter.TrySetRelative(level, "billboardsFaceCameraPosition", true, report);

                // Shadowmask over distance shadowmask: cheaper, and enough for a baked town.
                SerializedSettingWriter.TrySetRelative(level, "shadowmaskMode", 0, report);

                // Small time slice keeps texture streaming off the frame budget.
                SerializedSettingWriter.TrySetRelative(level, "asyncUploadTimeSlice", 2, report);
                SerializedSettingWriter.TrySetRelative(level, "asyncUploadBufferSize", 16, report);
                SerializedSettingWriter.TrySetRelative(level, "resolutionScalingFixedDPIFactor", 1f, report);

                SetMipmapLimit(level, i == 0 ? 1 : 0, report);
                ClearExcludedPlatforms(level);
            }

            SetPerPlatformDefaults(serialized, report);
            SerializedSettingWriter.TrySet(serialized, "m_CurrentQuality",
                AlphaTownProjectProfile.DefaultQualityLevelIndex, report);

            serialized.ApplyModifiedProperties();
            return true;
        }

        static float LodBiasFor(int index) => index == 0 ? 0.7f : index == 1 ? 1.0f : 1.2f;

        static int ParticleBudgetFor(int index) => index == 0 ? 16 : index == 1 ? 64 : 256;

        /// <summary>Renamed in Unity 6; try the current name, then the legacy one.</summary>
        static void SetMipmapLimit(SerializedProperty level, int limit, StringBuilder report)
        {
            var property = level.FindPropertyRelative("globalTextureMipmapLimit")
                           ?? level.FindPropertyRelative("textureQuality");

            if (property == null)
            {
                report.AppendLine("      ? texture mipmap limit not present, skipped");
                return;
            }

            SerializedSettingWriter.TryWrite(property, limit);
        }

        static void ClearExcludedPlatforms(SerializedProperty level)
        {
            var excluded = level.FindPropertyRelative("excludedTargetPlatforms");
            if (excluded != null && excluded.isArray) excluded.arraySize = 0;
        }

        /// <summary>
        /// Points the mobile platforms at the Medium level. Serialized as a string-to-int map,
        /// which SerializedProperty exposes as an array of first/second pairs.
        /// </summary>
        static void SetPerPlatformDefaults(SerializedObject serialized, StringBuilder report)
        {
            var map = serialized.FindProperty("m_PerPlatformDefaultQuality");
            if (map == null || !map.isArray)
            {
                report.AppendLine("    ? m_PerPlatformDefaultQuality unavailable — set the per-platform " +
                                  "default by hand in Project Settings ▸ Quality.");
                return;
            }

            var target = Mathf.Clamp(AlphaTownProjectProfile.DefaultQualityLevelIndex, 0, LevelNames.Length - 1);

            for (var i = 0; i < map.arraySize; i++)
            {
                var entry = map.GetArrayElementAtIndex(i);
                var key = entry.FindPropertyRelative("first");
                var value = entry.FindPropertyRelative("second");
                if (key == null || value == null) continue;

                // Every platform is clamped: a stale index into the old level list would be invalid.
                value.intValue = Mathf.Clamp(value.intValue, 0, LevelNames.Length - 1);

                if (key.stringValue != "Android" && key.stringValue != "iPhone") continue;

                value.intValue = target;
                report.AppendLine("    default for " + key.stringValue + ": " + LevelNames[target]);
            }
        }
    }
}
