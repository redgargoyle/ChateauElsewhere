using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class GameplayRuntimeState
{
    private const string MainMenuSceneName = "MainMenu";
    private const string GameplaySceneName = "Gameplay";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneReset()
    {
        ResetForSceneBoundary();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void ResetForNewGame()
    {
        ResetForSceneBoundary();
    }

    public static void ResetForGameplayStart()
    {
        ResetForSceneBoundary();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsKnownPlayableScene(scene))
        {
            ResetForSceneBoundary();
        }
    }

    private static bool IsKnownPlayableScene(Scene scene)
    {
        return string.Equals(scene.name, MainMenuSceneName, System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scene.name, GameplaySceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    private static void ResetForSceneBoundary()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        RuntimeSettingsMenu.ResetGlobalModalState();

#if UNITY_EDITOR
        EditorApplication.isPaused = false;
#endif
    }
}
