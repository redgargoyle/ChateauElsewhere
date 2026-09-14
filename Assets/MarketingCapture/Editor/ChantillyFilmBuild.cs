#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

// Explicit film-capture build only. It keeps the command-line capture helper
// while producing a release player with no Unity Development Build watermark.
// Saved PlayerSettings and scenes are not changed.
public static class ChantillyFilmBuild
{
    public static void BuildFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-chantillyFilmOutput");
        if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
            throw new ArgumentException("-chantillyFilmOutput requires a new absolute output directory.");
        string root = Path.GetFullPath(args[index + 1]);
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Refusing to overwrite " + root);
        string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes.");
        Directory.CreateDirectory(root);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes, locationPathName = Path.Combine(root, "Chantilly.x86_64"),
            target = BuildTarget.StandaloneLinux64,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.None,
            extraScriptingDefines = new[] { "CHANTILLY_CAPTURE" }
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Film build failed: " + report.summary.result + "; errors=" + report.summary.totalErrors);
        UnityEngine.Debug.Log("[ChantillyFilm] Watermark-free capture build succeeded at " + root);
    }
}
#endif
