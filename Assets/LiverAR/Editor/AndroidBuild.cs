using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace LiverAR.Editor
{
    public static class AndroidBuild
    {
        const string ScenePath = "Assets/Scenes/LiverARScene.unity";
        const string ApkPath = "Builds/LiverAR-debug.apk";
        const string ProductName = "Liver Anatomy";
        const string AndroidPackageName = "com.fyp.liverar";

        [MenuItem("Liver AR/Build Android APK")]
        public static void BuildAndRun()
        {
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidPackageName);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.SaveOpenScenes();

            LiverARProjectSetup.RepairAnatomyMaterials();
            LiverARProjectSetup.RebuildAnatomyPrefabIfSourceModelsChanged();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Android build failed: {report.summary.result}");

            UnityEngine.Debug.Log($"Android build succeeded: {report.summary.totalSize} bytes");
        }
    }
}
