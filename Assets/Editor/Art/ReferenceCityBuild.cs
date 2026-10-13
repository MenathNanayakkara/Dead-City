using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

/// <summary>Builds the enhanced scene while retaining the original ZombieMap file.</summary>
public static class ReferenceCityBuild
{
    public static void Build()
    {
        OpeningVideoSetup.ConfigureSceneFlow();
        EditorSceneManager.OpenScene(ExistingCityEnhancer.ScenePath);
        ExistingCityEnhancer.ValidateNavigation();
        Phase18Setup.Validate();
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Builds/Windows");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path == OutbreakEditor.Scene ? ExistingCityEnhancer.ScenePath : s.path).Distinct().ToArray();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Windows/DeadCity.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        string result = report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\nSize: " + report.summary.totalSize + "\nTime: " + report.summary.totalTime;
        File.WriteAllText("Logs/WindowsBuildSummary.txt", result);
        if(report.summary.result != BuildResult.Succeeded)throw new Exception(result);
    }
}
