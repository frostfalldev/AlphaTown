using System.IO;
using AlphaTown.Data.Catalog;
using AlphaTown.Gameplay.Bootstrap;
using AlphaTown.UI.CameraControl;
using AlphaTown.UI.Hud;
using AlphaTown.UI.Interaction;
using AlphaTown.UI.Selection;
using AlphaTown.UI.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AlphaTown.EditorTools.Setup
{
    /// <summary>
    /// Builds the playable scene from scratch: camera, runner, town view, input and HUD, wired to
    /// each other and to the sample content.
    ///
    /// The scene is generated rather than committed as a hand-edited asset because a .unity file
    /// is a wall of GUIDs — unreviewable in a diff, and a merge conflict every time two people
    /// touch it. Generating it means the scene's contents live in code that can be read, reviewed
    /// and re-run, and rebuilding after a rename is one menu item instead of an afternoon.
    ///
    /// Destructive by design: it replaces the scene outright. Anything hand-placed there will be
    /// lost, so hand-placed content belongs in a prefab this builder instantiates.
    /// </summary>
    internal static class MainSceneBuilder
    {
        const string ScenePath = "Assets/AlphaTown/Scenes/Town.unity";
        const string ContentRoot = "Assets/AlphaTown/Content";
        const string UiFolder = "Assets/AlphaTown/Content/UI";
        const string PanelSettingsPath = UiFolder + "/AlphaTownPanelSettings.asset";
        const string ThemePath = UiFolder + "/AlphaTownRuntimeTheme.tss";

        /// <summary>
        /// The design resolution the HUD's sizes are written against. UI Toolkit scales the whole
        /// panel from here, so a 26px label is 26px on a reference-sized screen and proportional
        /// everywhere else.
        /// </summary>
        static readonly Vector2Int ReferenceResolution = new Vector2Int(1080, 1920);

        [MenuItem("AlphaTown/Content/Build Playable Scene", false, 21)]
        internal static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorUtility.DisplayDialog(
                    "AlphaTown — Build Playable Scene",
                    "This replaces " + ScenePath + " entirely. Anything placed in it by hand will " +
                    "be lost.\n\nContinue?",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            var database = FindDatabase();
            if (database == null)
            {
                Debug.LogError("[AlphaTown] No GameDatabase found. Run AlphaTown ▸ Content ▸ " +
                               "Build Sample Content first.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(out var camera);
            CreateLight();

            var runner = CreateRunner(database);
            var selection = CreateInput(runner, camera);
            CreateTownView(runner, selection);
            CreateHud(runner, selection);

            AssetAuthoring.EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddToBuildSettings();
            Debug.Log("[AlphaTown] Playable scene written to " + ScenePath + ".");
        }

        static void CreateCamera(out Camera camera)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.20f, 0.26f);

            // Nothing in the slice is 3D, and depth sorting is done with sprite sorting orders.
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            go.AddComponent<AudioListener>();

            var control = go.AddComponent<IsoCameraController>();
            control.SetGridSize(new Vector2Int(24, 24));
        }

        /// <summary>
        /// A directional light so URP does not render an unlit scene. Sprites are unlit anyway;
        /// this is for whatever 3D props arrive later.
        /// </summary>
        static void CreateLight()
        {
            var go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
        }

        static GameRunner CreateRunner(GameDatabase database)
        {
            var go = new GameObject("GameRunner");
            var runner = go.AddComponent<GameRunner>();

            var serialized = AssetAuthoring.Edit(runner);
            AssetAuthoring.SetReference(serialized, "_database", database);
            AssetAuthoring.Apply(serialized);

            // The time source is left on Server with no URL, which is deliberate. That path still
            // builds a ServerTimeSource — so drift detection, the trust flag and the monotonic
            // baseline are all live — and simply runs unverified until an endpoint is configured.
            // Switching the scene to Device would quietly delete the anti-cheat wiring instead.

            return runner;
        }

        static TownSelection CreateInput(GameRunner runner, Camera camera)
        {
            var go = new GameObject("Input");

            var selection = go.AddComponent<TownSelection>();

            var tap = go.AddComponent<TownTapInput>();
            var tapSerialized = AssetAuthoring.Edit(tap);
            AssetAuthoring.SetReference(tapSerialized, "_runner", runner);
            AssetAuthoring.SetReference(tapSerialized, "_camera", camera);
            AssetAuthoring.Apply(tapSerialized);

            var sickle = go.AddComponent<SickleSwipeHarvestController>();
            var sickleSerialized = AssetAuthoring.Edit(sickle);
            AssetAuthoring.SetReference(sickleSerialized, "_runner", runner);
            AssetAuthoring.SetReference(sickleSerialized, "_camera", camera);
            AssetAuthoring.Apply(sickleSerialized);

            return selection;
        }

        static void CreateTownView(GameRunner runner, TownSelection selection)
        {
            var go = new GameObject("Town");
            var view = go.AddComponent<TownView>();

            var serialized = AssetAuthoring.Edit(view);
            AssetAuthoring.SetReference(serialized, "_runner", runner);
            AssetAuthoring.SetReference(serialized, "_selection", selection);
            AssetAuthoring.Apply(serialized);
        }

        static void CreateHud(GameRunner runner, TownSelection selection)
        {
            var go = new GameObject("HUD");

            var document = go.AddComponent<UIDocument>();
            document.panelSettings = CreatePanelSettings();

            var hud = go.AddComponent<TownHud>();
            var serialized = AssetAuthoring.Edit(hud);
            AssetAuthoring.SetReference(serialized, "_runner", runner);
            AssetAuthoring.SetReference(serialized, "_selection", selection);

            // Land deeds earn a slot on the top bar next to the currencies. Named explicitly
            // rather than left to the Special-category fallback so the bar's contents are visible
            // in the Inspector.
            AssetAuthoring.SetArray(serialized, "_trackedItemIds", 1,
                (element, _) => element.stringValue = "land_deed");

            AssetAuthoring.Apply(serialized);
        }

        /// <summary>
        /// Creates the panel the HUD renders into, and the runtime theme it needs.
        ///
        /// Unity normally writes the default theme the first time a UIDocument is added by hand.
        /// Generating the scene skips that, so the theme is written here — its entire contents are
        /// an import of Unity's built-in theme, which is exactly what the Editor would have made.
        /// </summary>
        static PanelSettings CreatePanelSettings()
        {
            AssetAuthoring.EnsureFolder(UiFolder);

            var settings = AssetAuthoring.CreateOrLoad<PanelSettings>(PanelSettingsPath);

            // Phones run from 720p to 1440p, so a constant pixel size would make the HUD
            // unreadable on a dense screen. Scaling from a reference resolution keeps the sizes in
            // UiKit meaningful on every device.
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.match = 0.5f;
            settings.themeStyleSheet = ResolveTheme();
            EditorUtility.SetDirty(settings);

            return settings;
        }

        static ThemeStyleSheet ResolveTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (existing != null) return existing;

            // Any theme already in the project is preferred: a project that has one has chosen it.
            var found = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            for (var i = 0; i < found.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(found[i]);
                var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                if (theme != null) return theme;
            }

            File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceSynchronousImport);

            var created = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (created == null)
            {
                Debug.LogWarning("[AlphaTown] Could not create a runtime theme. Assign one on " +
                                 PanelSettingsPath + " or the HUD will render unstyled.");
            }

            return created;
        }

        static GameDatabase FindDatabase()
        {
            var direct = AssetDatabase.LoadAssetAtPath<GameDatabase>(ContentRoot + "/GameDatabase.asset");
            if (direct != null) return direct;

            var found = AssetDatabase.FindAssets("t:GameDatabase");
            return found.Length == 0
                ? null
                : AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetDatabase.GUIDToAssetPath(found[0]));
        }

        /// <summary>Makes the scene the one a build starts on, so a device build is one click.</summary>
        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath) return;
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            updated[0] = new EditorBuildSettingsScene(ScenePath, true);
            for (var i = 0; i < scenes.Length; i++) updated[i + 1] = scenes[i];

            EditorBuildSettings.scenes = updated;
        }
    }
}
