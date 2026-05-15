using UnityEngine;

public static class RoomNavigationBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureNavigationManagerExists()
    {
        if (Object.FindObjectOfType<RoomNavigationManager>() != null)
        {
            return;
        }

        bool sceneLooksNavigable = Object.FindObjectOfType<DoorButton>(true) != null;

        if (!sceneLooksNavigable)
        {
            return;
        }

        GameObject navigationObject = new GameObject("RoomNavigationManager");
        navigationObject.AddComponent<RoomNavigationManager>();
    }
}
