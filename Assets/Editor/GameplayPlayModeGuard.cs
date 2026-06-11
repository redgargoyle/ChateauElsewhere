using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameplayPlayModeGuard
{
    private const string GameplaySceneName = "Gameplay";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string BlockedPlayMessage =
        "Play mode blocked: do not start directly from Gameplay.unity. " +
        "Open MainMenu.unity and use the main menu so gameplay bootstrap runs correctly.";

    static GameplayPlayModeGuard()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Play Mode/Open Main Menu Scene")]
    private static void OpenMainMenuScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode || !ShouldBlockPlayMode())
        {
            return;
        }

        Debug.LogError(BlockedPlayMessage);
        EditorApplication.isPlaying = false;
    }

    private static bool ShouldBlockPlayMode()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (IsGameplayScene(activeScene.name, activeScene.path))
        {
            return true;
        }

        UnityEngine.Object selectedObject = Selection.activeObject;
        if (selectedObject == null)
        {
            return false;
        }

        string selectedAssetPath = AssetDatabase.GetAssetPath(selectedObject);
        if (IsGameplayScene(selectedObject.name, selectedAssetPath))
        {
            return true;
        }

        if (selectedObject is GameObject selectedGameObject)
        {
            Scene selectedScene = selectedGameObject.scene;
            return selectedScene.IsValid() && IsGameplayScene(selectedScene.name, selectedScene.path);
        }

        return false;
    }

    private static bool IsGameplayScene(string sceneName, string scenePath)
    {
        return string.Equals(sceneName, GameplaySceneName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scenePath, GameplayScenePath, StringComparison.OrdinalIgnoreCase);
    }
}
