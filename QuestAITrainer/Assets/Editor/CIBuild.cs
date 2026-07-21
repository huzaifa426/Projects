using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Command-line build entry point (batch mode). Avoids the MCP-triggered
// synchronous BuildPlayer path that deadlocks bee_backend on startup.
// Usage:
//   Unity.exe -batchmode -quit -projectPath <proj> -buildTarget Android \
//             -executeMethod CIBuild.PerformBuild -logFile <log>
public static class CIBuild
{
    public static void PerformBuild()
    {
        string[] scenes = { "Assets/Scenes/MainScene.unity" };
        const string outPath = "Builds/Android/QuestAITrainer.apk";

        System.IO.Directory.CreateDirectory("Builds/Android");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        System.Console.WriteLine("CIBUILD: starting Android build -> " + outPath);
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            System.Console.WriteLine("CIBUILD_RESULT: SUCCESS bytes=" + summary.totalSize +
                                     " time=" + summary.totalTime + " path=" + outPath);
            EditorApplication.Exit(0);
        }
        else
        {
            System.Console.WriteLine("CIBUILD_RESULT: FAILED result=" + summary.result +
                                     " errors=" + summary.totalErrors);
            EditorApplication.Exit(1);
        }
    }
}
