using UnityEngine;

public static class Chapter1PointerPriority
{
    public static bool IsPointerOverAction(Vector2 screenPosition)
    {
        return TryGetTarget(screenPosition, out _);
    }

    public static bool TryGetTarget(Vector2 screenPosition, out MonoBehaviour target)
    {
        if (Chapter1CoatPickup.TryGetCoatAtScreenPosition(
                screenPosition,
                out Chapter1CoatPickup coat))
        {
            target = coat;
            return true;
        }

        if (Chapter1SceneAction.TryGetSceneActionAtScreenPosition(
                screenPosition,
                out Chapter1SceneAction sceneAction))
        {
            target = sceneAction;
            return true;
        }

        target = null;
        return false;
    }
}
