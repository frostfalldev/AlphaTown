using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Rewrites every URP asset in the project to AlphaTown's mobile profile
    /// (see <see cref="UrpMobileProfile"/>), assigns them to the quality levels, and points
    /// GraphicsSettings at the balanced one.
    ///
    /// Assets are located by type *name* through the AssetDatabase and edited through
    /// SerializedObject, so this file has no compile-time dependency on the URP assembly and
    /// keeps compiling whether or not URP is installed.
    /// </summary>
    internal static class MobileUrpConfigurator
    {
        const string PipelineAssetTypeName = "UniversalRenderPipelineAsset";
        const string RendererDataTypeName = "UniversalRendererData";

        [MenuItem("AlphaTown/Setup/Apply Mobile URP Profile", false, 100)]
        static void ApplyMenuItem() => Run(dryRun: false);

        [MenuItem("AlphaTown/Setup/Audit Mobile URP Profile (dry run)", false, 101)]
        static void AuditMenuItem() => Run(dryRun: true);

        /// <summary>
        /// Entry point for CI:
        /// <c>-batchmode -executeMethod AlphaTown.EditorTools.Setup.MobileUrpConfigurator.ApplyFromCommandLine</c>
        /// </summary>
        public static void ApplyFromCommandLine() => Run(dryRun: false);

        internal static void Run(bool dryRun)
        {
            var report = new StringBuilder();
            report.AppendLine(dryRun
                ? "[AlphaTown] Mobile URP profile — DRY RUN, nothing written."
                : "[AlphaTown] Mobile URP profile — applying.");

            var pipelineAssets = LoadAll<RenderPipelineAsset>(PipelineAssetTypeName);
            if (pipelineAssets.Count == 0)
            {
                Debug.LogWarning(
                    "[AlphaTown] No UniversalRenderPipelineAsset found in this project. Create the " +
                    "project from the Universal 3D template (or install com.unity.render-pipelines.universal " +
                    "and add a pipeline asset), then run this again. See docs/SETUP.md.");
                return;
            }

            var changes = 0;
            RenderPipelineAsset balanced = null;

            foreach (var asset in pipelineAssets.OrderBy(a => a.name, StringComparer.Ordinal))
            {
                var tier = UrpMobileProfile.TierFor(asset.name);
                if (tier == UrpTier.Balanced && balanced == null) balanced = asset;

                report.AppendLine();
                report.AppendLine($"  {asset.name}  [{tier}]  {AssetDatabase.GetAssetPath(asset)}");
                changes += ApplyOverrides(asset, UrpMobileProfile.Pipeline(tier), dryRun, report);
            }

            foreach (var data in LoadAll<ScriptableObject>(RendererDataTypeName)
                         .OrderBy(a => a.name, StringComparer.Ordinal))
            {
                var tier = UrpMobileProfile.TierFor(data.name);
                report.AppendLine();
                report.AppendLine($"  {data.name}  [{tier}]  {AssetDatabase.GetAssetPath(data)}");
                changes += ApplyOverrides(data, UrpMobileProfile.Renderer(tier), dryRun, report);
            }

            // Prefer the balanced asset as the project default; fall back to whatever exists.
            var defaultAsset = balanced ?? pipelineAssets[0];
            changes += ApplyGraphicsDefault(defaultAsset, dryRun, report);
            changes += ApplyQualityLevels(defaultAsset, dryRun, report);

            if (!dryRun && changes > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            report.AppendLine();
            report.AppendLine(dryRun
                ? $"  {changes} setting(s) differ from the mobile profile."
                : $"  {changes} setting(s) written.");
            Debug.Log(report.ToString());

            if (!dryRun && !Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "AlphaTown — Mobile URP Profile",
                    $"{changes} setting(s) written across {pipelineAssets.Count} pipeline asset(s).\n\n" +
                    "Full breakdown is in the Console.\n\n" +
                    "Quality and Graphics settings are saved with File ▸ Save Project.",
                    "OK");
            }
        }

        // --- URP assets -----------------------------------------------------------------------

        static int ApplyOverrides(UnityEngine.Object target, IEnumerable<SettingOverride> overrides,
                                  bool dryRun, StringBuilder report)
        {
            var path = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                report.AppendLine("    (skipped: asset lives in a package and is not writable)");
                return 0;
            }

            var serialized = new SerializedObject(target);
            var changes = 0;

            foreach (var setting in overrides)
            {
                var property = serialized.FindProperty(setting.PropertyPath);
                if (property == null)
                {
                    report.AppendLine($"    ? {setting.PropertyPath}: not in this URP version, skipped");
                    continue;
                }

                var before = SerializedSettingWriter.Describe(property);
                if (!SerializedSettingWriter.TryWrite(property, setting.Value))
                {
                    report.AppendLine(
                        $"    ! {setting.PropertyPath}: unsupported type {property.propertyType}, skipped");
                    continue;
                }

                var after = SerializedSettingWriter.Describe(property);
                if (string.Equals(before, after, StringComparison.Ordinal)) continue;

                changes++;
                var note = string.IsNullOrEmpty(setting.Note) ? string.Empty : $"   ({setting.Note})";
                report.AppendLine($"    • {setting.PropertyPath}: {before} -> {after}{note}");
            }

            // In a dry run the SerializedObject is simply discarded, so nothing reaches the asset.
            if (changes > 0 && !dryRun) serialized.ApplyModifiedProperties();
            return changes;
        }

        // --- Project settings -----------------------------------------------------------------

        static int ApplyGraphicsDefault(RenderPipelineAsset target, bool dryRun, StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Graphics");

            var current = GraphicsSettings.defaultRenderPipeline;
            if (current == target)
            {
                report.AppendLine($"    default render pipeline already {target.name}");
                return 0;
            }

            var currentName = current == null ? "<none>" : current.name;
            report.AppendLine($"    • defaultRenderPipeline: {currentName} -> {target.name}");
            if (!dryRun) GraphicsSettings.defaultRenderPipeline = target;
            return 1;
        }

        static int ApplyQualityLevels(RenderPipelineAsset fallback, bool dryRun, StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("  Quality levels");

            var levels = QualitySettings.names;
            var originalLevel = QualitySettings.GetQualityLevel();
            var changes = 0;

            try
            {
                for (var i = 0; i < levels.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);

                    var assigned = QualitySettings.renderPipeline;
                    if (assigned == null)
                    {
                        report.AppendLine(
                            $"    • {levels[i]}: renderPipeline <none> -> {fallback.name}");
                        if (!dryRun) QualitySettings.renderPipeline = fallback;
                        assigned = fallback;
                        changes++;
                    }

                    // Tier comes from the assigned pipeline asset so the two stay consistent.
                    var tier = UrpMobileProfile.TierFor(assigned.name);
                    var profile = UrpMobileProfile.Quality(tier);
                    report.AppendLine($"    {levels[i]}  [{tier}]");

                    changes += Set(dryRun, report, "anisotropicFiltering",
                        QualitySettings.anisotropicFiltering, profile.Anisotropic,
                        v => QualitySettings.anisotropicFiltering = v);
                    changes += Set(dryRun, report, "skinWeights",
                        QualitySettings.skinWeights, profile.SkinWeights,
                        v => QualitySettings.skinWeights = v);
                    changes += Set(dryRun, report, "lodBias",
                        QualitySettings.lodBias, profile.LodBias,
                        v => QualitySettings.lodBias = v);
                    changes += Set(dryRun, report, "particleRaycastBudget",
                        QualitySettings.particleRaycastBudget, profile.ParticleRaycastBudget,
                        v => QualitySettings.particleRaycastBudget = v);
                    changes += Set(dryRun, report, "realtimeReflectionProbes",
                        QualitySettings.realtimeReflectionProbes, profile.RealtimeReflectionProbes,
                        v => QualitySettings.realtimeReflectionProbes = v);
                    // Frame rate is driven by Application.targetFrameRate, never by vsync.
                    changes += Set(dryRun, report, "vSyncCount",
                        QualitySettings.vSyncCount, 0,
                        v => QualitySettings.vSyncCount = v);
                    // Shadowmask is cheaper than distance shadowmask and enough for baked towns.
                    changes += Set(dryRun, report, "shadowmaskMode",
                        QualitySettings.shadowmaskMode, UnityEngine.ShadowmaskMode.Shadowmask,
                        v => QualitySettings.shadowmaskMode = v);
                    changes += Set(dryRun, report, "asyncUploadTimeSlice",
                        QualitySettings.asyncUploadTimeSlice, 2,
                        v => QualitySettings.asyncUploadTimeSlice = v);
                    changes += Set(dryRun, report, "asyncUploadBufferSize",
                        QualitySettings.asyncUploadBufferSize, 16,
                        v => QualitySettings.asyncUploadBufferSize = v);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalLevel, applyExpensiveChanges: false);
            }

            return changes;
        }

        static int Set<T>(bool dryRun, StringBuilder report, string label, T current, T target,
                          Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, target)) return 0;
            report.AppendLine($"      • {label}: {current} -> {target}");
            if (!dryRun) setter(target);
            return 1;
        }

        // --- Helpers --------------------------------------------------------------------------

        static List<T> LoadAll<T>(string typeName) where T : UnityEngine.Object
        {
            var found = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeName))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) found.Add(asset);
            }
            return found;
        }
    }
}
