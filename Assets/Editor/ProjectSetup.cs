#if UNITY_EDITOR
using System;
using System.IO;
using Karmaan.Bootstrap;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

namespace Karmaan.Build
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
        private const string XrFolder = "Assets/XR";
        private const string XrSettingsPath = XrFolder + "/XRGeneralSettingsPerBuildTarget.asset";

        /// <summary>
        /// Runs once on first open, when either the boot scene or the XR settings are missing.
        /// Without this the project opens on an empty untitled scene and pressing Play does
        /// nothing, which reads as "the build is broken" rather than "run the setup menu item".
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoSetupOnFirstOpen()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                var needsScene = !File.Exists(ScenePath);
                var needsXr = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath) == null;

                if (!needsScene && !needsXr) return;

                Debug.Log("[Karmaan] First open — creating boot scene / XR settings.");
                Setup();
            };
        }

        [MenuItem("Karmaan/Set up project (run once)", false, 1)]
        public static void Setup()
        {
            EnsureFolders();

            // Scene first. It is the only thing here the user cannot work around: without it there
            // is nothing to press Play on. Player settings and XR are conveniences, and on a
            // machine without Android Build Support the Android-specific ones can throw — which
            // must not be allowed to take the scene down with them.
            var created = CreateBootScene();
            RegisterScene();

            TryStep("Player Settings", ConfigurePlayerSettings);
            TryStep("XR", ConfigureXr);

            Debug.Log(
                "[Karmaan] Project setup complete." +
                (created ? " Boot scene created." : " Boot scene already existed.") +
                "\nXR: ARCore loader assigned for Android (check Project Settings > XR Plug-in Management)." +
                "\nIf you changed the input backend, restart the editor once.");
        }

        private static void TryStep(string label, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Debug.LogError("[Karmaan] " + label + " setup failed. The project still runs; " +
                               "only that configuration is missing.\n" + e);
            }
        }

        private static void EnsureFolders()
        {
            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);
            if (!Directory.Exists(XrFolder)) Directory.CreateDirectory(XrFolder);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Team JKSS";
            PlayerSettings.productName = "Karmaan";

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "in.jh.karmaan.ar");

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
                Debug.LogWarning("[Karmaan] Could not open ProjectSettings.asset to set input handling.");
                return;
            }

            var settings = new SerializedObject(assets[0]);
            var handler = settings.FindProperty("activeInputHandler");

            if (handler == null)
            {
                Debug.LogWarning("[Karmaan] activeInputHandler not found; set Active Input Handling manually to 'Both'.");
                return;
            }

            if (handler.intValue != 2)
            {
                handler.intValue = 2;
                settings.ApplyModifiedProperties();
                Debug.Log("[Karmaan] Active Input Handling set to 'Both'. Restart the editor for it to take effect.");
            }
        }

        // ------------------------------------------------------------------ xr

        /// <summary>
        /// Assign the ARCore loader to the Android build target.
        ///
        /// This is the checkbox under Project Settings > XR Plug-in Management. Without it ARSession
        /// never gets a subsystem, ARSession.state stays None, and the app silently drops to 3D on
        /// every phone — the worst possible failure, because it looks like AR "just isn't supported".
        /// Everything below uses the public XR Management editor API rather than writing
        /// ProjectSettings YAML by hand.
        /// </summary>
        private static void ConfigureXr()
        {
            var settings = LoadOrCreateXrSettings();
            if (settings == null)
            {
                Debug.LogWarning("[Karmaan] Could not create XR settings. Enable ARCore manually: " +
                                 "Project Settings > XR Plug-in Management > Android.");
                return;
            }

            if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }

            var general = settings.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (general == null)
            {
                Debug.LogWarning("[Karmaan] No XR settings for Android after creation.");
                return;
            }

            general.InitManagerOnStart = true;

            var manager = settings.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (manager == null)
            {
                Debug.LogWarning("[Karmaan] No XR manager settings for Android after creation.");
                return;
            }

            var loader = FindLoaderTypeName("ARCoreLoader");
            if (loader == null)
            {
                Debug.LogWarning("[Karmaan] ARCore loader type not found. Is com.unity.xr.arcore installed?");
                return;
            }

            if (!XRPackageMetadataStore.AssignLoader(manager, loader, BuildTargetGroup.Android))
            {
                Debug.LogWarning("[Karmaan] XR Management refused the ARCore loader. Enable it manually.");
                return;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[Karmaan] ARCore loader assigned for Android.");
        }

        private static XRGeneralSettingsPerBuildTarget LoadOrCreateXrSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, XrSettingsPath);

            // XR Management finds its settings through a config object in EditorBuildSettings, not
            // by scanning the project. Without this line the asset exists and is invisible.
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, created, true);

            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>
        /// Find a loader by simple type name across every loaded assembly, rather than hardcoding
        /// "UnityEngine.XR.ARCore.ARCoreLoader" and breaking silently on a package rename.
        /// </summary>
        private static string FindLoaderTypeName(string simpleName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (var i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try { types = assemblies[i].GetTypes(); }
                catch { continue; }

                for (var t = 0; t < types.Length; t++)
                {
                    if (types[t].Name == simpleName) return types[t].FullName;
                }
            }

            return null;
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

        [MenuItem("Karmaan/Open boot scene", false, 2)]
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
