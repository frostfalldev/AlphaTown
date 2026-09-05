using UnityEditor;
using UnityEngine;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Runs the whole project-configuration pass in the order the steps depend on each other:
    /// player settings, then quality levels (which create the three URP assets), then the URP
    /// profile (which tunes whatever assets now exist).
    /// </summary>
    internal static class AlphaTownSetupMenu
    {
        [MenuItem("AlphaTown/Setup/Apply All Project Settings", false, 0)]
        internal static void ApplyAll()
        {
            PlayerSettingsConfigurator.Apply();
            QualityLevelConfigurator.Apply();
            MobileUrpConfigurator.Run(dryRun: false);

            AssetDatabase.SaveAssets();

            if (Application.isBatchMode) return;

            EditorUtility.DisplayDialog(
                "AlphaTown — Project Setup",
                "Player settings, quality levels and the mobile URP profile have been applied.\n\n" +
                "The full breakdown is in the Console.\n\n" +
                "Now use File ▸ Save Project, then commit ProjectSettings/ and Assets/Settings/.",
                "OK");
        }

        /// <summary>
        /// Everything needed to go from a fresh clone to a scene you can press Play on: project
        /// settings, then the sample content, then the scene that binds to it.
        ///
        /// Ordered by dependency — the scene looks up the database the content step writes, so
        /// running them the other way round would produce a scene wired to nothing.
        /// </summary>
        [MenuItem("AlphaTown/Setup/Build Playable Project", false, 1)]
        public static void BuildPlayableProject()
        {
            PlayerSettingsConfigurator.Apply();
            QualityLevelConfigurator.Apply();
            MobileUrpConfigurator.Run(dryRun: false);
            SampleContentBuilder.Build();
            MainSceneBuilder.Build();

            AssetDatabase.SaveAssets();
        }

        /// <summary>CI entry point: -executeMethod AlphaTown.EditorTools.Setup.AlphaTownSetupMenu.ApplyAll</summary>
        public static void ApplyAllFromCommandLine() => ApplyAll();

        /// <summary>CI entry point for the whole playable setup.</summary>
        public static void BuildPlayableProjectFromCommandLine() => BuildPlayableProject();
    }
}
