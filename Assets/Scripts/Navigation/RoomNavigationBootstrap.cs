using UnityEngine;

public static class RoomNavigationBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureNavigationManagerExists()
    {
        RoomNavigationManager navigationManager = Object.FindObjectOfType<RoomNavigationManager>();
        DoorPromptSequenceController promptController = Object.FindObjectOfType<DoorPromptSequenceController>();

        if (navigationManager != null && promptController != null)
        {
            return;
        }

        bool sceneLooksNavigable =
            Object.FindObjectOfType<DoorButton>(true) != null ||
            Object.FindObjectOfType<DoorTriggerNavigation>(true) != null;

        if (!sceneLooksNavigable)
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
