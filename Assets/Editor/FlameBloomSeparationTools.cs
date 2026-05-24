using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FlameBloomSeparationTools
{
    private const string MenuPath = "Dreadforge/Lighting/Setup Selected Flame Bloom Separation";

    [MenuItem(MenuPath)]
    public static void SetupSelectedFlame()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            EditorUtility.DisplayDialog("Flame Bloom Separation", "Select a flame ParticleSystem or its root object first.", "OK");
            return;
        }

        GameObject flameRoot = ResolveFlameRoot(selected);
        int noPostLayer = LayerMask.NameToLayer(NoPostProcessRenderLayer.DefaultLayerName);

        if (noPostLayer < 0)
        {
            EditorUtility.DisplayDialog("Flame Bloom Separation", $"Layer '{NoPostProcessRenderLayer.DefaultLayerName}' is missing. Reopen the project so Unity imports the updated TagManager.", "OK");
            return;
        }

        NoPostProcessRenderLayer renderLayer = flameRoot.GetComponent<NoPostProcessRenderLayer>();

        if (renderLayer == null)
        {
            renderLayer = Undo.AddComponent<NoPostProcessRenderLayer>(flameRoot);
        }

        renderLayer.ApplyNow();
        EditorUtility.SetDirty(renderLayer);

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

        RoomContentGroup roomGroup = flameRoot.GetComponentInParent<RoomContentGroup>(true);

        if (roomGroup != null)
        {
            CreateMissingLightRig(roomGroup, flameRoot.transform);
        }
        else
        {
            Debug.LogWarning("The selected flame is not under a RoomContentGroup, so no reflected room-light overlays were created.", flameRoot);
        }

        MarkSceneDirty(flameRoot);
        Debug.Log($"Separated flame bloom for '{flameRoot.name}'. The particle renders on '{NoPostProcessRenderLayer.DefaultLayerName}', while room glow/reflection is handled by editable RoomLightOverlay objects.");
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

    private static void CreateMissingLightRig(RoomContentGroup roomGroup, Transform flameTransform)
    {
        RectTransform lightingRoot = EnsureLightingRoot(roomGroup.transform);
        Vector2 basePosition = GetRoomLocalPosition(roomGroup, flameTransform);
        string flameName = SanitizeName(flameTransform.name);

        CreateLightIfMissing(
            lightingRoot,
            roomGroup.RoomName,
            $"RoomLight_{flameName}_BloomDriver",
            "Flame Bloom Driver",
            RoomLightAnimationStyle.FireplaceSource,
            basePosition + new Vector2(0f, 18f),
            new Vector2(170f, 150f),
            new Color(2.4f, 0.74f, 0.18f, 1f),
            0.38f,
            0.22f,
            0.06f,
            1.25f,
            0.35f);

        CreateLightIfMissing(
            lightingRoot,
            roomGroup.RoomName,
            $"RoomLight_{flameName}_PaintedReflection",
            "Flame Painted Reflection",
            RoomLightAnimationStyle.HearthBreath,
            basePosition + new Vector2(22f, 74f),
            new Vector2(430f, 300f),
            new Color(1.45f, 0.62f, 0.18f, 1f),
            0.22f,
            0.1f,
            0.18f,
            0.72f,
            0.8f);
    }

    private static void CreateLightIfMissing(
        RectTransform lightingRoot,
        string roomName,
        string objectName,
        string lightName,
        RoomLightAnimationStyle style,
        Vector2 position,
        Vector2 size,
        Color color,
        float onAlpha,
        float flicker,
        float drift,
        float speed,
        float phase)
    {
        if (lightingRoot.Find(objectName) != null)
        {
            return;
        }

        GameObject lightObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RoomLightOverlay), typeof(RoomEnvironmentMarker));
        Undo.RegisterCreatedObjectUndo(lightObject, $"Create {objectName}");
        RectTransform rectTransform = lightObject.GetComponent<RectTransform>();
        Undo.SetTransformParent(rectTransform, lightingRoot, $"Parent {objectName}");
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;

        RoomLightOverlay overlay = lightObject.GetComponent<RoomLightOverlay>();
        overlay.ApplyDefinition(new RoomLightDefinition
        {
            roomName = roomName,
            lightName = lightName,
            animationStyle = style,
            anchoredPosition = position,
            size = size,
            rotationDegrees = 0f,
            color = color,
            onAlpha = onAlpha,
            offAlpha = 0f,
            flickerAmount = flicker,
            driftAmount = drift,
            speed = speed,
            phase = phase
        });

        RoomEnvironmentMarker marker = lightObject.GetComponent<RoomEnvironmentMarker>();
        marker.Configure(roomName, RoomEnvironmentItemKind.OverlayLight, lightName, "Created from a selected flame. This visible room-light overlay drives bloom/reflection on the painted image while the particle itself renders on the no-post-process flame camera.");

        Image image = lightObject.GetComponent<Image>();
        image.raycastTarget = false;

        MarkSceneDirty(lightObject);
    }

    private static RectTransform EnsureLightingRoot(Transform roomTransform)
    {
        Transform existing = roomTransform.Find("Lighting");

        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject rootObject = new GameObject("Lighting", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Lighting Root");
        RectTransform root = rootObject.GetComponent<RectTransform>();
        Undo.SetTransformParent(root, roomTransform, "Parent Lighting Root");
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
        root.localScale = Vector3.one;
        return root;
    }

    private static Vector2 GetRoomLocalPosition(RoomContentGroup roomGroup, Transform target)
    {
        RectTransform roomRect = roomGroup.transform as RectTransform;

        if (roomRect == null)
        {
            Vector3 localPosition = roomGroup.transform.InverseTransformPoint(target.position);
            return new Vector2(localPosition.x, localPosition.y);
        }

        Vector3 roomLocal = roomRect.InverseTransformPoint(target.position);
        return new Vector2(roomLocal.x, roomLocal.y);
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Flame";
        }

        char[] chars = value.Trim().ToCharArray();

        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars).Trim('_');
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
