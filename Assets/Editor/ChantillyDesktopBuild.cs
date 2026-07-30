using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class ChantillyDesktopBuild
{
    public const string CompanyName = "Chantilly";
    public const string ProductName = "Chantilly";
    public const string ApplicationIdentifier = "com.chantilly.chantilly";

    private const string PlatformArgument = "-chantillyPlatform";
    private const string OutputRootArgument = "-chantillyOutputRoot";

    internal sealed class Specification
    {
        public Specification(
            string key,
            string folderName,
            string launcherName,
            BuildTarget target,
            GraphicsDeviceType[] graphicsApis)
        {
            Key = key;
            FolderName = folderName;
            LauncherName = launcherName;
            Target = target;
            GraphicsApis = graphicsApis;
        }

        public string Key { get; }
        public string FolderName { get; }
        public string LauncherName { get; }
        public BuildTarget Target { get; }
        public GraphicsDeviceType[] GraphicsApis { get; }
    }

    internal static Specification GetSpecification(string key)
    {
        switch (key?.ToLowerInvariant())
        {
            case "windows":
                return new Specification(
                    "windows",
                    "Windows",
                    "Chantilly.exe",
                    BuildTarget.StandaloneWindows64,
                    new[] { GraphicsDeviceType.OpenGLCore });
            case "linux":
                return new Specification(
                    "linux",
                    "Linux",
                    "Chantilly.x86_64",
                    BuildTarget.StandaloneLinux64,
                    new[] { GraphicsDeviceType.OpenGLCore });
            case "macos":
                return new Specification(
                    "macos",
                    "macOS",
                    "Chantilly.app",
                    BuildTarget.StandaloneOSX,
                    new[] { GraphicsDeviceType.Metal });
            default:
                throw new ArgumentException($"Unsupported Chantilly platform: {key}");
        }
    }

    public static void BuildFromCommandLine()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        Specification specification =
            GetSpecification(GetRequiredArgument(arguments, PlatformArgument));
        string outputRoot = GetRequiredArgument(arguments, OutputRootArgument);

        if (!Path.IsPathRooted(outputRoot))
        {
            throw new ArgumentException(
                $"{OutputRootArgument} must be an absolute path: {outputRoot}");
        }

        ConfigurePlayerSettings(specification);

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException(
                "Cannot build Chantilly because no enabled scenes are configured.");
        }

        string stagingDirectory = Path.Combine(
            Path.GetFullPath(outputRoot),
            ".staging",
            specification.FolderName);

        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, true);
        }

        Directory.CreateDirectory(stagingDirectory);
        string launcherPath = Path.Combine(
            stagingDirectory,
            specification.LauncherName);

        Debug.Log(
            $"[ChantillyBuild] Starting {specification.Key}: " +
            $"target={specification.Target}, " +
            $"graphics={string.Join(",", specification.GraphicsApis)}, " +
            $"output={launcherPath}");

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = launcherPath,
            target = specification.Target,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.CleanBuildCache
        });

        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Chantilly {specification.Key} build failed: " +
                $"result={summary.result}, errors={summary.totalErrors}, " +
                $"warnings={summary.totalWarnings}");
        }

        bool launcherExists = specification.Target == BuildTarget.StandaloneOSX
            ? Directory.Exists(launcherPath)
            : File.Exists(launcherPath);

        if (!launcherExists)
        {
            throw new FileNotFoundException(
                "Unity reported a successful build but did not create the launcher.",
                launcherPath);
        }

        Debug.Log(
            $"[ChantillyBuild] Build completed: " +
            $"platform={specification.Key}, " +
            $"target={specification.Target}, " +
            $"graphics={string.Join(",", specification.GraphicsApis)}, " +
            $"output={launcherPath}, " +
            $"size={summary.totalSize}, warnings={summary.totalWarnings}, " +
            $"errors={summary.totalErrors}");
    }

    private static void ConfigurePlayerSettings(Specification specification)
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.SetApplicationIdentifier(
            NamedBuildTarget.Standalone,
            ApplicationIdentifier);
        PlayerSettings.SetUseDefaultGraphicsAPIs(specification.Target, false);
        PlayerSettings.SetGraphicsAPIs(
            specification.Target,
            specification.GraphicsApis);

        if (specification.Target == BuildTarget.StandaloneOSX)
        {
            // Unity uses 1 for a macOS universal Intel + Apple silicon player.
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1);
        }

        AssetDatabase.SaveAssets();
    }

    private static string GetRequiredArgument(
        IReadOnlyList<string> arguments,
        string argumentName)
    {
        List<int> matches = new List<int>();

        for (int index = 0; index < arguments.Count; index++)
        {
            if (string.Equals(
                    arguments[index],
                    argumentName,
                    StringComparison.Ordinal))
            {
                matches.Add(index);
            }
        }

        if (matches.Count != 1 || matches[0] + 1 >= arguments.Count)
        {
            throw new ArgumentException(
                $"Expected exactly one {argumentName} argument with a value.");
        }

        string value = arguments[matches[0] + 1];

        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Expected exactly one {argumentName} argument with a value.");
        }

        return value;
    }
}
