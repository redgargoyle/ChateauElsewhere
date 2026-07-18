using UnityEngine;

public static class Chapter1PointerPriority
{
    private static int cachedFrame = -1;
    private static Vector2 cachedScreenPosition;
    private static MonoBehaviour cachedTarget;
    private static bool hasCachedResult;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetForPlayMode()
    {
        InvalidateCache();
    }

    public static void InvalidateCache()
    {
        cachedFrame = -1;
        cachedTarget = null;
        hasCachedResult = false;
    }

    public static bool IsPointerOverAction(Vector2 screenPosition)
    {
        return TryGetTarget(screenPosition, out _);
    }

    public static bool TryGetTarget(Vector2 screenPosition, out MonoBehaviour target)
    {
        if (hasCachedResult &&
            cachedFrame == Time.frameCount &&
            cachedScreenPosition == screenPosition)
        {
            target = cachedTarget;

            if (target == null)
            {
                return false;
            }

            if (target.isActiveAndEnabled)
            {
                return true;
            }

            InvalidateCache();
        }

        if (Chapter1CoatPickup.TryGetCoatAtScreenPosition(
                screenPosition,
                out Chapter1CoatPickup coat))
        {
            CacheResult(screenPosition, coat);
            SynchronizePointerHover(coat);
            target = cachedTarget;
            return true;
        }

        if (Chapter1SceneAction.TryGetSceneActionAtScreenPosition(
                screenPosition,
                out Chapter1SceneAction sceneAction))
        {
            CacheResult(screenPosition, sceneAction);
            SynchronizePointerHover(sceneAction);
            target = cachedTarget;
            return true;
        }

        CacheResult(screenPosition, null);
        SynchronizePointerHover(null);
        target = null;
        return false;
    }

    private static void CacheResult(Vector2 screenPosition, MonoBehaviour target)
    {
        cachedFrame = Time.frameCount;
        cachedScreenPosition = screenPosition;
        cachedTarget = target;
        hasCachedResult = true;
    }

    private static void SynchronizePointerHover(MonoBehaviour target)
    {
        Chapter1CoatPickup.ApplyPointerSelection(target as Chapter1CoatPickup);
        Chapter1SceneAction.ApplyPointerSelection(target as Chapter1SceneAction);
    }
}
