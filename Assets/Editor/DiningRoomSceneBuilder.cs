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
    private const string BaseRoomTexturePath = "Assets/Art/Final Images (DO NOT EDIT)/dining room.png";

    private static readonly string[] DiningFramePaths =
    {
        "Assets/Art/DiningTables/ChatGPT Image Jun 11, 2026, 02_44_46 PM (1).png",
        "Assets/Art/DiningTables/ChatGPT Image Jun 11, 2026, 02_44_46 PM (2).png",
        "Assets/Art/DiningTables/ChatGPT Image Jun 11, 2026, 02_44_46 PM (3).png",
        "Assets/Art/DiningTables/ChatGPT Image Jun 11, 2026, 02_44_46 PM (4).png",
    };

    private static readonly string[] LegacyIdleGuestNames =
    {
        "Guest 1",
        "Guest 2",
        "Guest 3",
        "Guest 4",
        "Guest 5",
        "Guest 6",
        "Guest 7",
        "Guest 8",
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

        Texture2D baseRoomTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseRoomTexturePath);

        ConfigureRoomContent(diningRoom, baseRoomTexture != null ? baseRoomTexture : diningFrames[0]);
        ConfigureAmbienceLoop(diningRoom.transform, diningFrames);
        DisableAutomaticButlerObserver(diningRoom.transform);
        DisableLegacyStandaloneDiningTable(diningRoom.transform);
        RestoreSceneLightingAndEffects(diningRoom.transform);
        DisableLegacyIdleGuests(scene);

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
        serializedDirector.FindProperty("holdSeconds").floatValue = 2.25f;
        serializedDirector.FindProperty("crossFadeSeconds").floatValue = 0.7f;
        serializedDirector.FindProperty("pingPong").boolValue = true;
        serializedDirector.FindProperty("useUnscaledTime").boolValue = true;
        serializedDirector.FindProperty("currentImage").objectReferenceValue = current;
        serializedDirector.FindProperty("nextImage").objectReferenceValue = next;
        serializedDirector.FindProperty("hideWhenNoFrames").boolValue = true;
        serializedDirector.ApplyModifiedPropertiesWithoutUndo();
        director.enabled = false;

        ConfigureAbominationAnimator(ambienceRoot, current, next, frames);

        RoomEnvironmentMarker marker = ambienceRoot.GetComponent<RoomEnvironmentMarker>();
        if (marker == null)
        {
            marker = ambienceRoot.gameObject.AddComponent<RoomEnvironmentMarker>();
        }

        marker.Configure(
            "Dining Room",
            RoomEnvironmentItemKind.PrerenderedPatch,
            "Chapter 3 Animated Dining Presentation",
            "Chapter 3 enables this no-butler full-room dinner frame loop and hides duplicate live guest sprites while it plays.",
            false);

        ambienceRoot.gameObject.SetActive(false);
        EditorUtility.SetDirty(ambienceRoot.gameObject);
    }

    private static void ConfigureAbominationAnimator(RectTransform ambienceRoot, RawImage current, RawImage next, Texture2D[] frames)
    {
        AbominationFullFrameAnimator animator = ambienceRoot.GetComponent<AbominationFullFrameAnimator>();
        if (animator == null)
        {
            animator = ambienceRoot.gameObject.AddComponent<AbominationFullFrameAnimator>();
        }

        SerializedObject serializedAnimator = new SerializedObject(animator);
        serializedAnimator.FindProperty("rawImage").objectReferenceValue = current;
        serializedAnimator.FindProperty("crossFadeImage").objectReferenceValue = next;
        AssignTextureArray(serializedAnimator.FindProperty("seatedIdleTextures"), frames);
        AssignTextureArray(serializedAnimator.FindProperty("coveredDinnerTextures"), frames);
        AssignTextureArray(serializedAnimator.FindProperty("eatingTextures"), frames);
        AssignTextureArray(serializedAnimator.FindProperty("finishedIdleTextures"), frames);
        serializedAnimator.FindProperty("seatedIdleFps").floatValue = 0.6f;
        serializedAnimator.FindProperty("coveredDinnerFps").floatValue = 0.6f;
        serializedAnimator.FindProperty("eatingFps").floatValue = 0.85f;
        serializedAnimator.FindProperty("finishedIdleFps").floatValue = 0.6f;
        serializedAnimator.FindProperty("loopSeatedIdle").boolValue = true;
        serializedAnimator.FindProperty("loopCoveredDinner").boolValue = true;
        serializedAnimator.FindProperty("loopEating").boolValue = true;
        serializedAnimator.FindProperty("loopFinishedIdle").boolValue = true;
        serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(animator);
    }

    private static void DisableAutomaticButlerObserver(Transform roomTransform)
    {
        Transform butler = FindChildRecursive(roomTransform, ButlerObjectName);

        if (butler == null)
        {
            return;
        }

        butler.gameObject.SetActive(false);
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

    private static void RestoreSceneLightingAndEffects(Transform roomTransform)
    {
        SetDirectChildActive(roomTransform, "AnimatedPatches", true);
        SetDirectChildActive(roomTransform, "Lighting", true);
    }

    private static void SetDirectChildActive(Transform parent, string childName, bool active)
    {
        Transform child = parent != null ? parent.Find(childName) : null;

        if (child == null)
        {
            return;
        }

        child.gameObject.SetActive(active);
        EditorUtility.SetDirty(child.gameObject);
    }

    private static void DisableLegacyIdleGuests(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform rootTransform = roots[i] != null ? roots[i].transform : null;

            if (rootTransform == null)
            {
                continue;
            }

            for (int guestIndex = 0; guestIndex < LegacyIdleGuestNames.Length; guestIndex++)
            {
                Transform guest = FindChildRecursive(rootTransform, LegacyIdleGuestNames[guestIndex]);

                if (guest == null)
                {
                    continue;
                }

                guest.gameObject.SetActive(false);
                EditorUtility.SetDirty(guest.gameObject);
            }
        }
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
