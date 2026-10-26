#if UNITY_EDITOR
using System.IO;
using Suraksha.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Suraksha.Build
{
    /// <summary>
    /// One-click project setup. Run this once after the first import.
    ///
    /// It creates the boot scene, registers it in Build Settings, and applies the Player Settings
    /// the PS requires (Android 10 floor, ARM64, correct bundle id). Everything it does is
    /// idempotent — running it twice changes nothing.
    /// </summary>
    public static class ProjectSetup
    {
        private const string SceneFolder = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolder + "/00_Boot.unity";

        /// <summary>
        /// Runs once on first open, when the boot scene does not exist yet. Without this the
        /// project opens on an empty untitled scene and pressing Play does nothing, which reads
        /// as "the build is broken" rather than "run the setup menu item".
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoSetupOnFirstOpen()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(ScenePath)) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                Debug.Log("[Suraksha] First open — creating the boot scene.");
                Setup();
            };
        }

        [MenuItem("Suraksha/Set up project (run once)", false, 1)]
        public static void Setup()
        {
            EnsureFolders();
            ConfigurePlayerSettings();
            var created = CreateBootScene();
            RegisterScene();

            Debug.Log(
                "[Suraksha] Project setup complete." +
                (created ? " Boot scene created." : " Boot scene already existed.") +
                "\nNext: Project Settings > XR Plug-in Management > Android tab > enable ARCore." +
                "\nIf you changed the input backend, restart the editor once.");
        }

        private static void EnsureFolders()
        {
            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Team JKSS";
            PlayerSettings.productName = "Khan Suraksha AR";

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "in.jh.suraksha.ar");

            // Android 10 is the floor the problem statement asks for.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            SetInputHandlingToBoth();

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// AR Foundation depends on the Input System package. If the project flips to
        /// "new input only", anything reading the legacy Input class throws every frame.
        /// "Both" keeps legacy working and the Input System available.
        /// </summary>
        private static void SetInputHandlingToBoth()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[Suraksha] Could not open ProjectSettings.asset to set input handling.");
                return;
            }

            var settings = new SerializedObject(assets[0]);
            var handler = settings.FindProperty("activeInputHandler");

            if (handler == null)
            {
                Debug.LogWarning("[Suraksha] activeInputHandler not found; set Active Input Handling manually to 'Both'.");
                return;
            }

            if (handler.intValue != 2)
            {
                handler.intValue = 2;
                settings.ApplyModifiedProperties();
                Debug.Log("[Suraksha] Active Input Handling set to 'Both'. Restart the editor for it to take effect.");
            }
        }

        private static bool CreateBootScene()
        {
            if (File.Exists(ScenePath)) return false;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("GameRoot");
            root.AddComponent<GameRoot>();

            var launcher = new GameObject("AppLauncher");
            launcher.AddComponent<AppLauncher>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            return true;
        }

        private static void RegisterScene()
        {
            var scenes = EditorBuildSettings.scenes;

            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes;
                    return;
                }
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            updated[0] = new EditorBuildSettingsScene(ScenePath, true);
            for (var i = 0; i < scenes.Length; i++) updated[i + 1] = scenes[i];

            EditorBuildSettings.scenes = updated;
        }

        [MenuItem("Suraksha/Open boot scene", false, 2)]
        public static void OpenBootScene()
        {
            if (!File.Exists(ScenePath))
            {
                Setup();
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>Marks the current scene dirty and saves it. Used by the demo capture flow.</summary>
        public static void SaveCurrentScene()
        {
            var active = SceneManager.GetActiveScene();
            if (active.IsValid()) EditorSceneManager.SaveScene(active);
        }
    }
}
#endif
