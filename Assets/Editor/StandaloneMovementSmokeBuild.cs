using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class StandaloneMovementSmokeBuild
{
    public static void BuildLinuxDevelopmentPlayer()
    {
        const string outputDirectory = "/tmp/ChateauStandaloneMovementSmoke";
        const string executablePath = outputDirectory + "/ChateauMovementSmoke.x86_64";
        Directory.CreateDirectory(outputDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Gameplay.unity"
            },
            locationPathName = executablePath,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Standalone movement smoke build failed: {report.summary.result} " +
                $"({report.summary.totalErrors} errors)." );
        }
    }
}
