using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CharacterPresentationQaEditor
{
    private const string Argument = "-character-presentation-qa";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    // CLI entry point:
    // Unity -projectPath <project> -character-presentation-qa <absolute-dir>
    //       -executeMethod CharacterPresentationQaEditor.Start
    public static void Start()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int argumentIndex = Array.IndexOf(arguments, Argument);
        if (argumentIndex < 0 || argumentIndex + 1 >= arguments.Length)
        {
            throw new ArgumentException($"{Argument} requires an absolute output directory.");
        }

        string outputDirectory = Path.GetFullPath(arguments[argumentIndex + 1]);
        if (!Path.IsPathRooted(outputDirectory))
        {
            throw new ArgumentException($"{Argument} must be followed by an absolute directory.");
        }

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(
            Path.Combine(outputDirectory, "editor-launch.txt"),
            $"{DateTime.UtcNow:O} Opening {MainMenuScenePath}; Gameplay loads automatically after Play Mode starts.\n");

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.Log("[CharacterPresentationQA] Play Mode is already active or starting.");
            return;
        }

        Selection.activeObject = null;
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        EditorApplication.delayCall += EnterPlayMode;
    }

    private static void EnterPlayMode()
    {
        Debug.Log("[CharacterPresentationQA] Starting Play Mode; the opt-in runtime controller will load Gameplay.");
        EditorApplication.EnterPlaymode();
    }
}
