#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Suraksha.Build
{
    /// <summary>
    /// Headless APK build. Matches the command in docs/BUILD_AND_SHIP.md:
    ///
    ///   Unity.exe -quit -batchmode -nographics -projectPath E:/Games/ar \
    ///     -executeMethod Suraksha.Editor.BuildPipeline.BuildAndroid \
    ///     -logFile E:/Games/ar/Builds/build.log
    ///
    /// Fails loudly rather than producing a broken APK. A build that silently ships with the
    /// wrong architecture or a missing ARCore loader costs a day of debugging on a phone.
    /// </summary>
    public static class BuildPipeline
    {
        private const string OutputDir = "Builds";
        private const string ApkName = "KhanSurakshaAR-v1.0.apk";

        [MenuItem("Suraksha/Build Android APK")]
        public static void BuildAndroidMenu()
        {
            BuildAndroid();
        }

        public static void BuildAndroid()
        {
            if (!Directory.Exists("Assets/_Project/Scenes"))
            {
                Fail("Assets/_Project/Scenes is missing. Is the project root correct?");
                return;
            }

            var scenes = CollectScenes();
            if (scenes.Length == 0)
            {
                Fail("No scenes in Build Settings. Add 00_Boot, 01_Menu, 02_Lesson, 03_Result.");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[Build] Switching active build target to Android...");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Fail("Could not switch to Android. Is Android Build Support installed " +
                         "(Unity Hub > Installs > 6000.3.6f1 > Add modules)?");
                    return;
                }
            }

            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            {
                Debug.LogWarning("[Build] Target architecture was not ARM64-only. Forcing ARM64 for release size.");
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }

            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
            {
                Debug.LogWarning("[Build] minSdk was below API 29. Forcing Android 10 floor (PS requirement).");
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            }

            Directory.CreateDirectory(OutputDir);
            var outputPath = Path.Combine(OutputDir, ApkName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log("[Build] Building " + outputPath);
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[Build] OK  " + outputPath +
                          "  size=" + (summary.totalSize / (1024f * 1024f)).ToString("0.0") + " MB" +
                          "  time=" + summary.totalTime);
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }

            Fail("Build " + summary.result + " with " + summary.totalErrors + " error(s). See the log.");
        }

        private static string[] CollectScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path)) list.Add(scene.path);
            }
            return list.ToArray();
        }

        private static void Fail(string message)
        {
            Debug.LogError("[Build] FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
#endif
