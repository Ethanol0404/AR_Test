using UnityEditor;
using UnityEditor.Build.Reporting;

namespace LiverAR.Editor
{
    public static class AndroidBuild
    {
        public static void BuildAndRun()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/LiverARScene.unity" },
                locationPathName = "Builds/LiverAR-debug.apk",
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
