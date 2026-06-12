using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DiningRoomSceneBuilder
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string DiningRoomObjectName = "Room_Dining_Room";
    private const string AmbienceRootName = "DiningRoom_Demo_Ambience";
    private const string ButlerObjectName = "DiningRoom_ButlerObserver";

    private static readonly string[] DiningFramePaths =
    {
        "Assets/Art/DiningTables/ChatGPT Image Jun 11, 2026, 02_44_46 PM (1).png",
        "Assets/Art/DiningTables/ChatGPT Image Jun 11, 2026, 02_44_46 PM (2).png",
        "Assets/Art/DiningTables/ChatGPT Image Jun 11, 2026, 02_44_46 PM (3).png",
        "Assets/Art/DiningTables/ChatGPT Image Jun 11, 2026, 02_44_46 PM (4).png",
    };

    [MenuItem("Tools/Dreadforge/Build Dining Room Demo Scene")]
    public static void BuildGameplayDiningRoom()
    {
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        GameObject diningRoom = FindInScene(scene, DiningRoomObjectName);

        if (diningRoom == null)
        {
            Debug.LogError($"Could not find '{DiningRoomObjectName}' in {GameplayScenePath}.");
            return;
        }

        Texture2D[] diningFrames = LoadTextures(DiningFramePaths);

        if (diningFrames.Length == 0)
        {
            Debug.LogError("No dining room frames could be loaded from Assets/Art/DiningTables.");
            return;
        }

        ConfigureRoomContent(diningRoom, diningFrames[0]);
        ConfigureAmbienceLoop(diningRoom.transform, diningFrames);
        ConfigureButlerObserver(diningRoom.transform);
        DisableLegacyStandaloneDiningTable(diningRoom.transform);
        DisablePrerenderedSceneOverlays(diningRoom.transform);

        EditorUtility.SetDirty(diningRoom);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Dining room demo scene is ready in Gameplay.unity.");
    }

    private static void ConfigureRoomContent(GameObject diningRoom, Texture2D heroFrame)
    {
        RoomContentGroup contentGroup = diningRoom.GetComponent<RoomContentGroup>();

        if (contentGroup == null)
        {
            contentGroup = diningRoom.AddComponent<RoomContentGroup>();
        }

        contentGroup.SetRoomName("Dining Room");
        contentGroup.SetRoomBackgroundTexture(heroFrame);
        EditorUtility.SetDirty(contentGroup);
    }

    private static void ConfigureAmbienceLoop(Transform roomTransform, Texture2D[] frames)
    {
        RectTransform ambienceRoot = EnsureRectChild(roomTransform, AmbienceRootName);
        StretchToParent(ambienceRoot);
        ambienceRoot.SetSiblingIndex(0);

        RawImage current = EnsureRawImage(ambienceRoot, "DiningRoom_Frame_Current", 1f);
        RawImage next = EnsureRawImage(ambienceRoot, "DiningRoom_Frame_Next", 0f);

        DiningRoomAmbienceDirector director = ambienceRoot.GetComponent<DiningRoomAmbienceDirector>();
        if (director == null)
        {
            director = ambienceRoot.gameObject.AddComponent<DiningRoomAmbienceDirector>();
        }

        SerializedObject serializedDirector = new SerializedObject(director);
        AssignTextureArray(serializedDirector.FindProperty("frames"), frames);
        serializedDirector.FindProperty("holdSeconds").floatValue = 3.35f;
        serializedDirector.FindProperty("crossFadeSeconds").floatValue = 1.15f;
        serializedDirector.FindProperty("pingPong").boolValue = true;
        serializedDirector.FindProperty("useUnscaledTime").boolValue = true;
        serializedDirector.FindProperty("currentImage").objectReferenceValue = current;
        serializedDirector.FindProperty("nextImage").objectReferenceValue = next;
        serializedDirector.FindProperty("hideWhenNoFrames").boolValue = true;
        serializedDirector.ApplyModifiedPropertiesWithoutUndo();

        RoomEnvironmentMarker marker = ambienceRoot.GetComponent<RoomEnvironmentMarker>();
        if (marker == null)
        {
            marker = ambienceRoot.gameObject.AddComponent<RoomEnvironmentMarker>();
        }

        marker.Configure(
            "Dining Room",
            RoomEnvironmentItemKind.PrerenderedPatch,
            "CEO Demo Dining Room Idle Loop",
            "Crossfades through the no-duplicate-butler full-room dining frames for slow candlelight, eating, arm, and head movement.",
            false);

        EditorUtility.SetDirty(ambienceRoot.gameObject);
    }

    private static void ConfigureButlerObserver(Transform roomTransform)
    {
        RectTransform butler = EnsureRectChild(roomTransform, ButlerObjectName);
        butler.SetAsLastSibling();
        butler.anchorMin = new Vector2(0.5f, 0.5f);
        butler.anchorMax = new Vector2(0.5f, 0.5f);
        butler.pivot = new Vector2(0.5f, 0.035f);
        butler.sizeDelta = new Vector2(168f, 299f);
        butler.anchoredPosition = new Vector2(-720f, -220f);

        if (butler.GetComponent<CanvasRenderer>() == null)
        {
            butler.gameObject.AddComponent<CanvasRenderer>();
        }

        Image image = butler.GetComponent<Image>();
        if (image == null)
        {
            image = butler.gameObject.AddComponent<Image>();
        }

        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/butler/butler_classic_walk_01_r01_c01.png");
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(0.92f, 0.88f, 0.78f, 0.93f);

        Animator animator = butler.GetComponent<Animator>();
        if (animator == null)
        {
            animator = butler.gameObject.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/ButlerClassic/ButlerClassic.controller");
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        RoomPersonWalker2D walker = butler.GetComponent<RoomPersonWalker2D>();
        if (walker == null)
        {
            walker = butler.gameObject.AddComponent<RoomPersonWalker2D>();
        }

        SerializedObject serializedWalker = new SerializedObject(walker);
        serializedWalker.FindProperty("animator").objectReferenceValue = animator;
        serializedWalker.FindProperty("targetGraphic").objectReferenceValue = image;
        serializedWalker.FindProperty("previewInEditMode").boolValue = true;
        serializedWalker.FindProperty("previewPathInEditMode").boolValue = false;
        serializedWalker.FindProperty("snapToWholePixels").boolValue = false;
        serializedWalker.FindProperty("animationSpeed").floatValue = 40f;
        serializedWalker.FindProperty("horizontalDirectionThreshold").floatValue = 0.58f;
        serializedWalker.FindProperty("addStepMotion").boolValue = true;
        serializedWalker.FindProperty("pixelsPerWalkCycle").floatValue = 70f;
        serializedWalker.FindProperty("walkBobPixels").floatValue = 2.7f;
        serializedWalker.FindProperty("walkSwayPixels").floatValue = 0.8f;
        serializedWalker.FindProperty("animateIdlePose").boolValue = true;
        serializedWalker.FindProperty("idleBobPixels").floatValue = 0.7f;
        serializedWalker.FindProperty("idleSwayPixels").floatValue = 0.28f;
        serializedWalker.FindProperty("idleCycleSeconds").floatValue = 2.8f;
        serializedWalker.FindProperty("pointPauseSeconds").floatValue = 0.35f;
        serializedWalker.FindProperty("endpointPauseSeconds").floatValue = 2.2f;
        serializedWalker.FindProperty("mirrorWhenWalkingLeft").boolValue = true;
        AssignVector2Array(
            serializedWalker.FindProperty("pathPoints"),
            new[]
            {
                new Vector2(-720f, -220f),
                new Vector2(-600f, -292f),
                new Vector2(-365f, -352f),
                new Vector2(-40f, -378f),
                new Vector2(310f, -350f),
                new Vector2(560f, -278f),
                new Vector2(650f, -170f),
                new Vector2(612f, -84f),
            });
        serializedWalker.FindProperty("pixelsPerSecond").floatValue = 54f;
        serializedWalker.FindProperty("loopPath").boolValue = false;
        serializedWalker.FindProperty("pingPongPath").boolValue = true;
        serializedWalker.FindProperty("nearY").floatValue = -380f;
        serializedWalker.FindProperty("farY").floatValue = -70f;
        serializedWalker.FindProperty("nearScale").floatValue = 0.9f;
        serializedWalker.FindProperty("farScale").floatValue = 0.52f;
        serializedWalker.FindProperty("nearTint").colorValue = new Color(0.94f, 0.88f, 0.74f, 0.94f);
        serializedWalker.FindProperty("farTint").colorValue = new Color(0.72f, 0.70f, 0.62f, 0.76f);
        serializedWalker.FindProperty("disableRaycastTarget").boolValue = true;
        serializedWalker.ApplyModifiedPropertiesWithoutUndo();

        RoomEnvironmentMarker marker = butler.GetComponent<RoomEnvironmentMarker>();
        if (marker == null)
        {
            marker = butler.gameObject.AddComponent<RoomEnvironmentMarker>();
        }

        marker.Configure(
            "Dining Room",
            RoomEnvironmentItemKind.AuthoringNote,
            "Butler Observer Route",
            "Slow ping-pong patrol across the foreground and right side of the dining room, using the ButlerClassic animation controller.",
            false);

        EditorUtility.SetDirty(butler.gameObject);
    }

    private static void DisableLegacyStandaloneDiningTable(Transform roomTransform)
    {
        Transform table = FindChildRecursive(roomTransform, "correct_dining_table_0");
        if (table != null)
        {
            table.gameObject.SetActive(false);
            EditorUtility.SetDirty(table.gameObject);
        }

        Transform oldPatch = FindChildRecursive(roomTransform, "PatchCandidate_Dining_Table_Candle_Line_Frames");
        if (oldPatch != null)
        {
            oldPatch.gameObject.SetActive(false);
            EditorUtility.SetDirty(oldPatch.gameObject);
        }
    }

    private static void DisablePrerenderedSceneOverlays(Transform roomTransform)
    {
        DisableDirectChild(roomTransform, "AnimatedPatches");
        DisableDirectChild(roomTransform, "Lighting");
        DisableGuestVisuals(roomTransform);
    }

    private static void DisableDirectChild(Transform parent, string childName)
    {
        Transform child = parent != null ? parent.Find(childName) : null;

        if (child == null)
        {
            return;
        }

        child.gameObject.SetActive(false);
        EditorUtility.SetDirty(child.gameObject);
    }

    private static void DisableGuestVisuals(Transform roomTransform)
    {
        if (roomTransform == null)
        {
            return;
        }

        Transform[] children = roomTransform.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child == null || child == roomTransform || !LooksLikeGuestVisual(child.name))
            {
                continue;
            }

            child.gameObject.SetActive(false);
            EditorUtility.SetDirty(child.gameObject);
        }
    }

    private static bool LooksLikeGuestVisual(string objectName)
    {
        return !string.IsNullOrWhiteSpace(objectName) &&
            (objectName.StartsWith("Guest") ||
            objectName.StartsWith("Walker_Guest") ||
            objectName.Contains("_Guest"));
    }

    private static RawImage EnsureRawImage(RectTransform parent, string name, float alpha)
    {
        RectTransform rectTransform = EnsureRectChild(parent, name);
        StretchToParent(rectTransform);

        RawImage image = rectTransform.GetComponent<RawImage>();
        if (image == null)
        {
            image = rectTransform.gameObject.AddComponent<RawImage>();
        }

        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        image.color = new Color(1f, 1f, 1f, alpha);
        EditorUtility.SetDirty(image);
        return image;
    }

    private static RectTransform EnsureRectChild(Transform parent, string name)
    {
        Transform existing = FindChildRecursive(parent, name);
        RectTransform existingRectTransform = existing as RectTransform;
        if (existingRectTransform != null)
        {
            return existingRectTransform;
        }

        GameObject child = new GameObject(name, typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        child.layer = uiLayer >= 0 ? uiLayer : parent.gameObject.layer;
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    private static Texture2D[] LoadTextures(string[] paths)
    {
        Texture2D[] loaded = new Texture2D[paths.Length];
        int loadedCount = 0;

        for (int i = 0; i < paths.Length; i++)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
            if (texture == null)
            {
                Debug.LogWarning($"Dining room frame missing: {paths[i]}");
                continue;
            }

            loaded[loadedCount] = texture;
            loadedCount++;
        }

        if (loadedCount == loaded.Length)
        {
            return loaded;
        }

        Texture2D[] trimmed = new Texture2D[loadedCount];
        for (int i = 0; i < loadedCount; i++)
        {
            trimmed[i] = loaded[i];
        }

        return trimmed;
    }

    private static void AssignTextureArray(SerializedProperty property, Texture2D[] textures)
    {
        property.arraySize = textures.Length;
        for (int i = 0; i < textures.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = textures[i];
        }
    }

    private static void AssignVector2Array(SerializedProperty property, Vector2[] points)
    {
        property.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
        {
            property.GetArrayElementAtIndex(i).vector2Value = points[i];
        }
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform match = FindChildRecursive(roots[i].transform, name);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindChildRecursive(parent.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
