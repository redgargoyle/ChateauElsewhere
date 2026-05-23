using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class IdeaGameplayUIBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureForInitialScene()
    {
        EnsureForLoadedScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForLoadedScene();
    }

    private static void EnsureForLoadedScene()
    {
        if (Object.FindAnyObjectByType<IdeaGameplayUI>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        CameraManager cameraManager = Object.FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
        GameObject backgroundCanvas = GameObject.Find("Canvas_Background");

        if (cameraManager == null || backgroundCanvas == null)
        {
            return;
        }

        GameObject uiObject = new GameObject(
            "IdeaGameplayUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        uiObject.AddComponent<IdeaGameplayUI>();
    }
}
