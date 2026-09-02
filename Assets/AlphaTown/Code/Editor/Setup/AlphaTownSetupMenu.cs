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

        /// <summary>CI entry point: -executeMethod AlphaTown.EditorTools.Setup.AlphaTownSetupMenu.ApplyAll</summary>
        public static void ApplyAllFromCommandLine() => ApplyAll();
    }
}
