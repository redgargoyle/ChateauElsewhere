using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class FlameBloomSeparationTools
{
    private const string MenuPath = "Dreadforge/Lighting/Setup Selected Flame Local Light";

    [MenuItem(MenuPath)]
    public static void SetupSelectedFlame()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            EditorUtility.DisplayDialog("Flame Local Light", "Select a flame ParticleSystem or its root object first.", "OK");
            return;
        }

        GameObject flameRoot = ResolveFlameRoot(selected);
        ParticleSystem particleSystem = flameRoot.GetComponentInChildren<ParticleSystem>(true);

        if (particleSystem == null)
        {
            EditorUtility.DisplayDialog("Flame Local Light", "The selected object does not contain a ParticleSystem.", "OK");
            return;
        }

        int noPostLayer = LayerMask.NameToLayer(NoPostProcessRenderLayer.DefaultLayerName);

        if (noPostLayer < 0)
        {
            EditorUtility.DisplayDialog("Flame Local Light", $"Layer '{NoPostProcessRenderLayer.DefaultLayerName}' is missing. Reopen the project so Unity imports the updated TagManager.", "OK");
            return;
        }

        NoPostProcessRenderLayer renderLayer = flameRoot.GetComponent<NoPostProcessRenderLayer>();

        if (renderLayer == null)
        {
            renderLayer = Undo.AddComponent<NoPostProcessRenderLayer>(flameRoot);
        }

        renderLayer.ApplyNow();
        EditorUtility.SetDirty(renderLayer);

        FlameLocalLight localLight = particleSystem.GetComponent<FlameLocalLight>();

        if (localLight == null)
        {
            localLight = Undo.AddComponent<FlameLocalLight>(particleSystem.gameObject);
        }

        RemoveLegacyRootLight(particleSystem);
        Light2D light2D = EnsureLight2D(particleSystem.transform);
        SpriteRenderer glowRenderer = EnsureGlowRenderer(particleSystem.transform);

        localLight.ConfigureNow();
        EditorUtility.SetDirty(localLight);
        EditorUtility.SetDirty(light2D);
        EditorUtility.SetDirty(glowRenderer);

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            LayerMask bypassLayers = new LayerMask
            {
                value = 1 << noPostLayer
            };
            PostProcessBypassCamera bypassCamera = PostProcessBypassCamera.EnsureForCamera(mainCamera, bypassLayers);

            if (bypassCamera != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(bypassCamera.gameObject, "Configure No Post Process Flame Camera");
                EditorUtility.SetDirty(bypassCamera.gameObject);
            }
        }
        else
        {
            Debug.LogWarning("No MainCamera was found, so the no-post-process flame camera could not be configured yet.");
        }

        MarkSceneDirty(flameRoot);
        Debug.Log($"Configured local flame light for '{flameRoot.name}'. The particle renders on '{NoPostProcessRenderLayer.DefaultLayerName}' while '{FlameLocalLight.LightObjectName}' stays on the main camera layer for room/object lighting.");
    }

    [MenuItem(MenuPath, true)]
    public static bool CanSetupSelectedFlame()
    {
        return Selection.activeGameObject != null;
    }

    private static GameObject ResolveFlameRoot(GameObject selected)
    {
        ParticleSystem particleSystem = selected.GetComponentInChildren<ParticleSystem>(true);

        if (particleSystem != null)
        {
            return selected;
        }

        ParticleSystem parentParticleSystem = selected.GetComponentInParent<ParticleSystem>(true);

        if (parentParticleSystem != null)
        {
            return parentParticleSystem.gameObject;
        }

        return selected;
    }

    private static SpriteRenderer EnsureGlowRenderer(Transform particleTransform)
    {
        Transform existing = particleTransform.Find(FlameLocalLight.GlowObjectName);
        GameObject glowObject;

        if (existing != null)
        {
            glowObject = existing.gameObject;
        }
        else
        {
            glowObject = new GameObject(FlameLocalLight.GlowObjectName);
            Undo.RegisterCreatedObjectUndo(glowObject, "Create Local Flame Glow");
            Undo.SetTransformParent(glowObject.transform, particleTransform, "Parent Local Flame Glow");
            glowObject.transform.localPosition = Vector3.zero;
            glowObject.transform.localRotation = Quaternion.identity;
            glowObject.transform.localScale = Vector3.one;
        }

        SpriteRenderer glowRenderer = glowObject.GetComponent<SpriteRenderer>();

        if (glowRenderer == null)
        {
            glowRenderer = Undo.AddComponent<SpriteRenderer>(glowObject);
        }

        return glowRenderer;
    }

    private static Light2D EnsureLight2D(Transform particleTransform)
    {
        Transform existing = particleTransform.Find(FlameLocalLight.LightObjectName);
        GameObject lightObject;

        if (existing != null)
        {
            lightObject = existing.gameObject;
        }
        else
        {
            lightObject = new GameObject(FlameLocalLight.LightObjectName);
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Local Flame Light2D");
            Undo.SetTransformParent(lightObject.transform, particleTransform, "Parent Local Flame Light2D");
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;
            lightObject.transform.localScale = Vector3.one;
        }

        int defaultLayer = LayerMask.NameToLayer("Default");
        lightObject.layer = defaultLayer >= 0 ? defaultLayer : 0;

        Light2D light2D = lightObject.GetComponent<Light2D>();

        if (light2D == null)
        {
            light2D = Undo.AddComponent<Light2D>(lightObject);
        }

        return light2D;
    }

    private static void RemoveLegacyRootLight(ParticleSystem particleSystem)
    {
        Light2D legacyLight = particleSystem.GetComponent<Light2D>();

        if (legacyLight == null)
        {
            return;
        }

        Undo.DestroyObjectImmediate(legacyLight);
    }

    private static void MarkSceneDirty(Object target)
    {
        if (target == null)
        {
            return;
        }

        EditorUtility.SetDirty(target);

        if (target is Component component)
        {
            MarkSceneDirty(component.gameObject);
            return;
        }

        if (target is GameObject gameObject)
        {
            Scene scene = gameObject.scene;

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
