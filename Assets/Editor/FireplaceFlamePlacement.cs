using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FireplaceFlamePlacement
{
    private const string FireplaceName = "Fireplace";
    private const string FlameName = "Flame";
    private const string SortingLayerName = "Background";
    private const int FlameSortingOrder = 10;

    [MenuItem("Dreadforge/Particles/Place Flame Above Fireplace")]
    public static void PlaceFlamesInOpenFireplaces()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        int placedCount = 0;
        foreach (Transform fireplace in FindSceneTransforms(FireplaceName))
        {
            SpriteRenderer fireplaceRenderer = FindFireplaceRenderer(fireplace);
            if (fireplaceRenderer == null)
            {
                continue;
            }

            List<ParticleSystem> flames = FindSceneFlames(fireplace);
            if (flames.Count == 0)
            {
                continue;
            }

            PlaceFlames(fireplace, fireplaceRenderer, flames);
            placedCount += flames.Count;
        }

        if (placedCount > 0)
        {
            Debug.Log($"Placed {placedCount} flame particle system(s) inside fireplace firebox.");
        }
    }

    [MenuItem("Dreadforge/Particles/Place Flame Above Fireplace", true)]
    private static bool CanPlaceFlamesInOpenFireplaces()
    {
        return FindFirstSceneTransform(FireplaceName) != null;
    }

    private static void PlaceFlames(Transform fireplace, SpriteRenderer fireplaceRenderer, List<ParticleSystem> flames)
    {
        Bounds bounds = fireplaceRenderer.bounds;
        Vector3[] targets = BuildFireboxTargets(bounds, flames.Count);

        for (int i = 0; i < flames.Count; i++)
        {
            ParticleSystem flame = flames[i];
            Transform flameTransform = flame.transform;
            ParticleSystemRenderer renderer = flame.GetComponent<ParticleSystemRenderer>();

            Undo.SetTransformParent(flameTransform, fireplace, "Parent Flame To Fireplace");
            Undo.RecordObject(flameTransform, "Place Flame In Firebox");
            Undo.RecordObject(flame, "Configure Firebox Flame");
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "Configure Firebox Flame Renderer");
            }

            Vector3 localTarget = fireplace.InverseTransformPoint(targets[i]);
            localTarget.z = 0f;

            flameTransform.localPosition = localTarget;
            flameTransform.localRotation = Quaternion.identity;
            flameTransform.localScale = Vector3.one;

            ParticleSystem.MainModule main = flame.main;
            main.playOnAwake = true;
            main.prewarm = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;

            ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = flame.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.45f, 1.2f);
            velocityOverLifetime.z = CreateZeroVelocityCurve();

            if (renderer != null)
            {
                renderer.sortingLayerName = HasSortingLayer(SortingLayerName) ? SortingLayerName : "Default";
                renderer.sortingOrder = FlameSortingOrder;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            }

            flame.Clear(true);
            flame.Simulate(0.35f, true, true, true);
            flame.Play(true);

            EditorUtility.SetDirty(flameTransform);
            EditorUtility.SetDirty(flame);
            if (renderer != null)
            {
                EditorUtility.SetDirty(renderer);
            }
        }

        Scene scene = fireplace.gameObject.scene;
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static Vector3[] BuildFireboxTargets(Bounds fireplaceBounds, int count)
    {
        Vector3[] targets = new Vector3[count];
        Vector3 center = fireplaceBounds.center;
        float width = fireplaceBounds.size.x;
        float height = fireplaceBounds.size.y;
        Vector3 baseCenter = new Vector3(center.x, fireplaceBounds.min.y + height * 0.28f, center.z - 0.05f);

        Vector2[] offsets =
        {
            new Vector2(0f, 0.03f),
            new Vector2(-0.055f, -0.015f),
            new Vector2(0.055f, -0.015f),
            new Vector2(-0.03f, 0.075f),
            new Vector2(0.03f, 0.075f),
            new Vector2(-0.09f, 0.04f),
            new Vector2(0.09f, 0.04f),
            new Vector2(0f, 0.12f)
        };

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = offsets[i % offsets.Length];
            targets[i] = baseCenter + new Vector3(offset.x * width, offset.y * height, 0f);
        }

        return targets;
    }

    private static SpriteRenderer FindFireplaceRenderer(Transform fireplace)
    {
        SpriteRenderer[] renderers = fireplace.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.sprite != null &&
                renderer.name.IndexOf("fireplace", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return renderer;
            }
        }

        return renderers.Length > 0 ? renderers[0] : null;
    }

    private static List<ParticleSystem> FindSceneFlames(Transform fireplace)
    {
        List<ParticleSystem> flames = new List<ParticleSystem>();
        foreach (ParticleSystem particleSystem in Resources.FindObjectsOfTypeAll<ParticleSystem>())
        {
            if (!IsSceneObject(particleSystem) ||
                particleSystem.name.IndexOf(FlameName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (particleSystem.gameObject.scene != fireplace.gameObject.scene)
            {
                continue;
            }

            if (particleSystem.transform.IsChildOf(fireplace) ||
                particleSystem.transform.parent == null)
            {
                flames.Add(particleSystem);
            }
        }

        flames.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        return flames;
    }

    private static IEnumerable<Transform> FindSceneTransforms(string name)
    {
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (IsSceneObject(transform) && transform.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                yield return transform;
            }
        }
    }

    private static Transform FindFirstSceneTransform(string name)
    {
        foreach (Transform transform in FindSceneTransforms(name))
        {
            return transform;
        }

        return null;
    }

    private static bool IsSceneObject(Component component)
    {
        return component != null &&
            component.gameObject.scene.IsValid() &&
            !EditorUtility.IsPersistent(component);
    }

    private static bool HasSortingLayer(string sortingLayerName)
    {
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name == sortingLayerName)
            {
                return true;
            }
        }

        return false;
    }

    private static ParticleSystem.MinMaxCurve CreateZeroVelocityCurve()
    {
        ParticleSystem.MinMaxCurve curve = new ParticleSystem.MinMaxCurve(0f, 0f);
        curve.mode = ParticleSystemCurveMode.TwoConstants;
        return curve;
    }
}
