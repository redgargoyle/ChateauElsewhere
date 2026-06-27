using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class YAxisDepthSortingValidator
{
    private const string LogPrefix = "[YAxisDepthSortingValidator]";

    [MenuItem("Dreadforge/Depth Sorting/Validate Active Scene")]
    public static void ValidateActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        int issueCount = 0;

        foreach (SpriteRenderer renderer in FindSceneObjects<SpriteRenderer>())
        {
            if (renderer == null)
            {
                continue;
            }

            if (IsUnderRoom(renderer.transform) &&
                LooksDynamic(renderer.gameObject) &&
                !IsDepthSorted(renderer.transform))
            {
                Warn(renderer, "Dynamic-looking SpriteRenderer under RoomContentGroup has no RoomProjectedEntity or WorldYSortSpriteRenderer.");
                issueCount++;
            }

            if (renderer.sortingLayerName == "Background" &&
                renderer.sortingOrder == 20 &&
                LooksDynamic(renderer.gameObject))
            {
                Warn(renderer, "Dynamic-looking prop is still on Background/order 20.");
                issueCount++;
            }

            if (LooksLargeOrDiagonal(renderer) &&
                renderer.GetComponentInParent<WorldYSortSpriteRenderer>(true) != null &&
                renderer.GetComponentInParent<YSortOcclusionFootprint2D>(true) == null)
            {
                Warn(renderer, "Large/diagonal y-sorted prop may need YSortOcclusionFootprint2D/depth line.");
                issueCount++;
            }
        }

        foreach (Graphic graphic in FindSceneObjects<Graphic>())
        {
            RoomProjectedEntity projection = graphic.GetComponentInParent<RoomProjectedEntity>(true);

            if (projection != null && projection.SortingCanvas == null)
            {
                Warn(graphic, "UI Graphic under RoomProjectedEntity has no local sorting Canvas yet.");
                issueCount++;
            }
        }

        foreach (StaticSetImagePlayer imagePlayer in FindSceneObjects<StaticSetImagePlayer>())
        {
            if (imagePlayer == null || !IsDepthSorted(imagePlayer.transform))
            {
                continue;
            }

            if (imagePlayer.bringImageToFront || imagePlayer.overrideSpriteSorting)
            {
                Warn(imagePlayer, "StaticSetImagePlayer under depth-sorted root has bring-to-front or override sorting enabled.");
                issueCount++;
            }
        }

        Debug.Log($"{LogPrefix} {scene.name}: validation complete, {issueCount} issue(s) reported.");
    }

    [MenuItem("Dreadforge/Depth Sorting/Normalize Active Scene")]
    public static void NormalizeActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        int changeCount = 0;

        foreach (RoomProjectedEntity projection in FindSceneObjects<RoomProjectedEntity>())
        {
            if (projection == null)
            {
                continue;
            }

            Undo.RecordObject(projection, "Repair projected UI sorting canvas");
            projection.RefreshVisualTargets();
            projection.ApplyProjection();
            EditorUtility.SetDirty(projection);
            changeCount++;
            Debug.Log($"{LogPrefix} Repaired projected sorting targets on {GetPath(projection.transform)}.", projection);
        }

        foreach (StaticSetImagePlayer imagePlayer in FindSceneObjects<StaticSetImagePlayer>())
        {
            if (imagePlayer == null || !IsDepthSorted(imagePlayer.transform))
            {
                continue;
            }

            if (!imagePlayer.bringImageToFront && !imagePlayer.overrideSpriteSorting)
            {
                continue;
            }

            Undo.RecordObject(imagePlayer, "Disable static image depth overrides");
            imagePlayer.bringImageToFront = false;
            imagePlayer.overrideSpriteSorting = false;
            EditorUtility.SetDirty(imagePlayer);
            changeCount++;
            Debug.Log($"{LogPrefix} Disabled StaticSetImagePlayer sorting overrides on {GetPath(imagePlayer.transform)}.", imagePlayer);
        }

        if (changeCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log($"{LogPrefix} {scene.name}: normalization complete, {changeCount} conservative change(s).");
    }

    private static IEnumerable<T> FindSceneObjects<T>() where T : UnityEngine.Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();

        for (int i = 0; i < objects.Length; i++)
        {
            T candidate = objects[i];

            if (candidate == null || EditorUtility.IsPersistent(candidate))
            {
                continue;
            }

            Component component = candidate as Component;
            GameObject gameObject = candidate as GameObject;
            Scene scene = component != null ? component.gameObject.scene : gameObject != null ? gameObject.scene : default;

            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            yield return candidate;
        }
    }

    private static bool IsUnderRoom(Transform transform)
    {
        return transform != null && transform.GetComponentInParent<RoomContentGroup>(true) != null;
    }

    private static bool IsDepthSorted(Transform transform)
    {
        return transform != null &&
            (transform.GetComponentInParent<RoomProjectedEntity>(true) != null ||
                transform.GetComponentInParent<WorldYSortSpriteRenderer>(true) != null ||
                transform.GetComponentInParent<YSortSolidObstacle2D>(true) != null ||
                transform.GetComponentInParent<YSortOcclusionFootprint2D>(true) != null);
    }

    private static bool LooksDynamic(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        string name = gameObject.name;
        return ContainsAny(name, "Guest", "Butler", "Player", "NPC", "Coat", "Chair", "Table", "Desk", "Cart", "Bed", "Pool");
    }

    private static bool LooksLargeOrDiagonal(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        Bounds bounds = renderer.bounds;
        bool wide = bounds.size.x > bounds.size.y * 1.35f && bounds.size.x > 0.5f;
        return wide || ContainsAny(renderer.name, "PoolTable", "Pool Table", "Bed", "Table", "Cart", "Desk");
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < needles.Length; i++)
        {
            if (value.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void Warn(UnityEngine.Object context, string message)
    {
        Debug.LogWarning($"{LogPrefix} {message}", context);
    }

    private static string GetPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        List<string> parts = new List<string>();
        Transform cursor = transform;

        while (cursor != null)
        {
            parts.Add(cursor.name);
            cursor = cursor.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}
