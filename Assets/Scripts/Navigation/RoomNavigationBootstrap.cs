using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomNavigationBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        // RuntimeInitializeOnLoadMethod only fires for the initial scene. When
        // the player starts in MainMenu and then loads Gameplay, we need this
        // sceneLoaded hook so Gameplay gets its navigation manager too.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureNavigationManagerExists()
    {
        EnsureNavigationManagerExistsForLoadedScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureNavigationManagerExistsForLoadedScene();
    }

    private static void EnsureNavigationManagerExistsForLoadedScene()
    {
        RoomNavigationManager navigationManager = Object.FindObjectOfType<RoomNavigationManager>(true);
        DoorPromptSequenceController promptController = Object.FindObjectOfType<DoorPromptSequenceController>(true);
        CameraManager cameraManager = Object.FindObjectOfType<CameraManager>(true);

        if (navigationManager != null && promptController != null)
        {
            return;
        }

        bool sceneHasDoorControls =
            Object.FindObjectOfType<DoorButton>(true) != null ||
            Object.FindObjectOfType<DoorTriggerNavigation>(true) != null;

        if (!sceneHasDoorControls)
        {
            return;
        }

        // Door trigger objects can exist in non-gameplay scenes as disabled edit
        // leftovers. Only bootstrap navigation in a scene that also has the
        // camera background system, or already has a deliberate navigation manager.
        if (cameraManager == null && navigationManager == null)
        {
            return;
        }

        GameObject navigationObject = navigationManager != null
            ? navigationManager.gameObject
            : new GameObject("RoomNavigationManager");

        if (navigationManager == null)
        {
            navigationManager = navigationObject.AddComponent<RoomNavigationManager>();
        }

        if (promptController == null)
        {
            navigationObject.AddComponent<DoorPromptSequenceController>();
        }
    }
}
