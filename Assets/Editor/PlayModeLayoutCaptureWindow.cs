using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public sealed class PlayModeLayoutCaptureWindow : EditorWindow
{
    private const string PendingCaptureSessionKey = "Dreadforge.PlayModeLayoutCapture.PendingCapture";
    private const string PendingCaptureAutoApplySessionKey = "Dreadforge.PlayModeLayoutCapture.AutoApply";
    private const string DiningRoomId = "Dining Room";
    private const string DiningSeatPrefix = "Ch2_DiningSeat_";
    private const string ProtectedEntranceGuestSpotPrefix = "EntranceGuestSpot_";
    private const string PlayModeApplyBlockedMessage =
        "Captured layout is pending. Stop Play Mode to apply it to the edit-time scene; Unity does not allow scene writes while the game is running.";

    private Vector2 scrollPosition;
    private string statusMessage = string.Empty;

    static PlayModeLayoutCaptureWindow()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Dreadforge/Rooms/Play Mode Layout Capture")]
    public static void Open()
    {
        GetWindow<PlayModeLayoutCaptureWindow>("Layout Capture");
    }

    [MenuItem("Dreadforge/Rooms/Capture Dining Seat Play Mode Layout")]
    public static void CaptureDiningSeatAnchorsMenu()
    {
        CaptureDiningSeatAnchors();
    }

    [MenuItem("Dreadforge/Rooms/Apply Pending Play Mode Layout Capture")]
    public static void ApplyPendingCaptureMenu()
    {
        if (Application.isPlaying)
        {
            SessionState.SetInt(PendingCaptureAutoApplySessionKey, 1);
            Debug.LogWarning(PlayModeApplyBlockedMessage);
            return;
        }

        ApplyPendingCapture(true, false, true, out string message);
        Debug.Log(message);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Play Mode Layout Capture", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Tune anchors or selected layout objects while the game is running, capture their transforms, then stop Play Mode. " +
            "The captured values are written back to the real edit-time scene so a normal scene save keeps them.",
            MessageType.Info);

        EditorGUILayout.LabelField("Mode", Application.isPlaying ? "Play Mode" : "Edit Mode");

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode is for capture only. Move the anchors, click Capture, then stop Play Mode to write the captured values back to the saved scene.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);

        if (GUILayout.Button("Capture Dining Seat Anchors"))
        {
            statusMessage = CaptureDiningSeatAnchors();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Selected"))
            {
                statusMessage = CaptureSelectedTransforms(false);
            }

            if (GUILayout.Button("Capture Selected + Children"))
            {
                statusMessage = CaptureSelectedTransforms(true);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pending Capture", EditorStyles.boldLabel);
        PlayModeLayoutCapturePayload pendingPayload = LoadPendingCapture();

        if (pendingPayload == null || pendingPayload.Items == null || pendingPayload.Items.Count == 0)
        {
            EditorGUILayout.HelpBox("No pending play-mode layout capture.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"{pendingPayload.CaptureName}\nCaptured {pendingPayload.Items.Count} object(s) at {pendingPayload.CapturedAt}.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Pending Capture"))
                    {
                        ApplyPendingCapture(true, false, true, out statusMessage);
                    }

                    if (GUILayout.Button("Apply + Save Scenes"))
                    {
                        ApplyPendingCapture(true, true, true, out statusMessage);
                    }
                }
            }

            if (Application.isPlaying && GUILayout.Button("Stop Play Mode And Apply"))
            {
                SessionState.SetInt(PendingCaptureAutoApplySessionKey, 1);
                EditorApplication.isPlaying = false;
                statusMessage = "Stopping Play Mode. The pending capture will apply when Unity returns to Edit Mode.";
            }

            if (GUILayout.Button("Clear Pending Capture"))
            {
                ClearPendingCapture();
                statusMessage = "Cleared pending play-mode layout capture.";
            }
        }

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode ||
            SessionState.GetInt(PendingCaptureAutoApplySessionKey, 0) == 0)
        {
            return;
        }

        SessionState.SetInt(PendingCaptureAutoApplySessionKey, 0);
        EditorApplication.delayCall += AutoApplyPendingCaptureAfterPlayMode;
    }

    private static void AutoApplyPendingCaptureAfterPlayMode()
    {
        ApplyPendingCapture(false, false, true, out string message);
        Debug.Log(message);
    }

    private static string CaptureDiningSeatAnchors()
    {
        RoomAnchor[] anchors = Resources.FindObjectsOfTypeAll<RoomAnchor>();
        List<Transform> targets = new List<Transform>();

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor == null ||
                EditorUtility.IsPersistent(anchor) ||
                !anchor.gameObject.scene.IsValid())
            {
                continue;
            }

            bool isDiningSeat =
                StartsWithPrefix(anchor.name, DiningSeatPrefix) ||
                StartsWithPrefix(anchor.AnchorId, DiningSeatPrefix);

            if (!isDiningSeat || !SameRoom(anchor.RoomId, DiningRoomId))
            {
                continue;
            }

            targets.Add(anchor.transform);
        }

        targets.Sort(CompareTransformsByName);
        return CaptureTransforms(targets, "Dining room seat anchors");
    }

    private static string CaptureSelectedTransforms(bool includeChildren)
    {
        Transform[] selectedTransforms = Selection.transforms;

        if (selectedTransforms == null || selectedTransforms.Length == 0)
        {
            return "Select one or more scene objects to capture.";
        }

        List<Transform> targets = new List<Transform>();
        HashSet<Transform> seen = new HashSet<Transform>();

        for (int i = 0; i < selectedTransforms.Length; i++)
        {
            Transform selected = selectedTransforms[i];

            if (selected == null)
            {
                continue;
            }

            if (!includeChildren)
            {
                AddUniqueTransform(targets, seen, selected);
                continue;
            }

            Transform[] childTransforms = selected.GetComponentsInChildren<Transform>(true);
            for (int childIndex = 0; childIndex < childTransforms.Length; childIndex++)
            {
                AddUniqueTransform(targets, seen, childTransforms[childIndex]);
            }
        }

        return CaptureTransforms(targets, includeChildren ? "Selected transforms and children" : "Selected transforms");
    }

    private static string CaptureTransforms(List<Transform> targets, string captureName)
    {
        List<PlayModeLayoutCaptureItem> items = new List<PlayModeLayoutCaptureItem>();

        for (int i = 0; i < targets.Count; i++)
        {
            if (TryCreateCaptureItem(targets[i], out PlayModeLayoutCaptureItem item))
            {
                items.Add(item);
            }
        }

        if (items.Count == 0)
        {
            return "No valid scene transforms were found to capture.";
        }

        PlayModeLayoutCapturePayload payload = new PlayModeLayoutCapturePayload
        {
            Version = 1,
            CaptureName = captureName,
            CapturedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Items = items
        };

        SessionState.SetString(PendingCaptureSessionKey, JsonUtility.ToJson(payload));
        SessionState.SetInt(PendingCaptureAutoApplySessionKey, Application.isPlaying ? 1 : 0);

        string message = $"Captured {items.Count} object(s) for {captureName}.";
        Debug.Log(message);
        return message;
    }

    private static bool TryCreateCaptureItem(Transform target, out PlayModeLayoutCaptureItem item)
    {
        item = null;

        if (target == null ||
            EditorUtility.IsPersistent(target) ||
            !target.gameObject.scene.IsValid() ||
            string.IsNullOrWhiteSpace(target.gameObject.scene.path))
        {
            return false;
        }

        if (IsProtectedEntranceGuestSpot(target))
        {
            Debug.LogWarning($"Skipped protected entrance wait spot '{target.name}'. Its edit-time transform is the only source of truth.");
            return false;
        }

        RectTransform rectTransform = target as RectTransform;
        RoomAnchor roomAnchor = target.GetComponent<RoomAnchor>();

        item = new PlayModeLayoutCaptureItem
        {
            ObjectName = target.name,
            ScenePath = target.gameObject.scene.path,
            HierarchyPath = GetHierarchyPath(target),
            TransformIndexPath = GetTransformIndexPath(target),
            GlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString(),
            LocalPosition = target.localPosition,
            LocalRotation = target.localRotation,
            LocalScale = target.localScale,
            IsRectTransform = rectTransform != null
        };

        if (roomAnchor != null)
        {
            item.RoomAnchorId = roomAnchor.AnchorId;
            item.RoomId = roomAnchor.RoomId;
        }

        if (rectTransform != null)
        {
            item.AnchoredPosition = rectTransform.anchoredPosition;
            item.AnchorMin = rectTransform.anchorMin;
            item.AnchorMax = rectTransform.anchorMax;
            item.SizeDelta = rectTransform.sizeDelta;
            item.Pivot = rectTransform.pivot;
        }

        return true;
    }

    private static bool ApplyPendingCapture(
        bool openMissingScenes,
        bool saveScenes,
        bool clearOnComplete,
        out string message)
    {
        if (Application.isPlaying)
        {
            SessionState.SetInt(PendingCaptureAutoApplySessionKey, 1);
            message = PlayModeApplyBlockedMessage;
            return false;
        }

        PlayModeLayoutCapturePayload payload = LoadPendingCapture();

        if (payload == null || payload.Items == null || payload.Items.Count == 0)
        {
            message = "No pending play-mode layout capture to apply.";
            return false;
        }

        if (!EnsureCapturedScenesAreLoaded(payload, openMissingScenes, out string sceneMessage))
        {
            message = sceneMessage;
            return false;
        }

        int appliedCount = 0;
        int missingCount = 0;
        int protectedCount = 0;
        HashSet<Scene> dirtyScenes = new HashSet<Scene>();

        for (int i = 0; i < payload.Items.Count; i++)
        {
            PlayModeLayoutCaptureItem item = payload.Items[i];

            if (IsProtectedEntranceGuestSpot(item))
            {
                protectedCount++;
                continue;
            }

            Transform target = ResolveCapturedTransform(item);

            if (target == null)
            {
                missingCount++;
                Debug.LogWarning($"Could not resolve captured layout object '{item.ObjectName}' at '{item.HierarchyPath}'.");
                continue;
            }

            if (IsProtectedEntranceGuestSpot(target))
            {
                protectedCount++;
                continue;
            }

            Undo.RecordObject(target, "Apply Play Mode Layout Capture");
            ApplyCaptureItem(target, item);
            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(target.gameObject);

            if (target.gameObject.scene.IsValid())
            {
                dirtyScenes.Add(target.gameObject.scene);
            }

            appliedCount++;
        }

        foreach (Scene scene in dirtyScenes)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        if (saveScenes && appliedCount > 0)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        bool complete = missingCount == 0 && (appliedCount > 0 || protectedCount > 0);

        if (complete && clearOnComplete)
        {
            ClearPendingCapture();
        }

        string saveSuffix = saveScenes && appliedCount > 0 ? " Saved open scenes." : string.Empty;
        string protectedSuffix = protectedCount > 0
            ? $" Ignored {protectedCount} protected entrance wait spot(s)."
            : string.Empty;
        message = missingCount == 0
            ? $"Applied {appliedCount} captured layout object(s).{protectedSuffix}{saveSuffix}"
            : $"Applied {appliedCount} captured layout object(s), but {missingCount} could not be found.{protectedSuffix} Pending capture was kept.";

        return complete;
    }

    private static void ApplyCaptureItem(Transform target, PlayModeLayoutCaptureItem item)
    {
        if (IsProtectedEntranceGuestSpot(target))
        {
            return;
        }

        RectTransform rectTransform = target as RectTransform;

        if (item.IsRectTransform && rectTransform != null)
        {
            rectTransform.anchorMin = item.AnchorMin;
            rectTransform.anchorMax = item.AnchorMax;
            rectTransform.pivot = item.Pivot;
            rectTransform.sizeDelta = item.SizeDelta;
            rectTransform.anchoredPosition = item.AnchoredPosition;
            rectTransform.localRotation = item.LocalRotation;
            rectTransform.localScale = item.LocalScale;
            Vector3 localPosition = rectTransform.localPosition;
            localPosition.z = item.LocalPosition.z;
            rectTransform.localPosition = localPosition;
            return;
        }

        target.localPosition = item.LocalPosition;
        target.localRotation = item.LocalRotation;
        target.localScale = item.LocalScale;
    }

    private static Transform ResolveCapturedTransform(PlayModeLayoutCaptureItem item)
    {
        Transform roomAnchorTarget = ResolveRoomAnchor(item);

        if (roomAnchorTarget != null)
        {
            return roomAnchorTarget;
        }

        if (!string.IsNullOrWhiteSpace(item.GlobalObjectId) &&
            GlobalObjectId.TryParse(item.GlobalObjectId, out GlobalObjectId globalObjectId))
        {
            UnityEngine.Object resolvedObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);

            if (resolvedObject is Transform resolvedTransform &&
                IsTransformInScene(resolvedTransform, item.ScenePath))
            {
                return resolvedTransform;
            }
        }

        Transform indexTarget = ResolveByIndexPath(item);

        if (indexTarget != null)
        {
            return indexTarget;
        }

        return ResolveByHierarchyPath(item);
    }

    private static Transform ResolveRoomAnchor(PlayModeLayoutCaptureItem item)
    {
        if (string.IsNullOrWhiteSpace(item.RoomAnchorId))
        {
            return null;
        }

        RoomAnchor[] anchors = Resources.FindObjectsOfTypeAll<RoomAnchor>();

        for (int i = 0; i < anchors.Length; i++)
        {
            RoomAnchor anchor = anchors[i];

            if (anchor == null ||
                EditorUtility.IsPersistent(anchor) ||
                !anchor.gameObject.scene.IsValid() ||
                !IsSameScenePath(anchor.gameObject.scene.path, item.ScenePath) ||
                !SameRoom(anchor.AnchorId, item.RoomAnchorId))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.RoomId) || SameRoom(anchor.RoomId, item.RoomId))
            {
                return anchor.transform;
            }
        }

        return null;
    }

    private static Transform ResolveByIndexPath(PlayModeLayoutCaptureItem item)
    {
        Scene scene = SceneManager.GetSceneByPath(item.ScenePath);

        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(item.TransformIndexPath))
        {
            return null;
        }

        string[] parts = item.TransformIndexPath.Split('/');

        if (parts.Length == 0 || !int.TryParse(parts[0], out int rootIndex))
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        if (rootIndex < 0 || rootIndex >= roots.Length)
        {
            return null;
        }

        Transform current = roots[rootIndex].transform;

        for (int i = 1; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out int childIndex) ||
                childIndex < 0 ||
                childIndex >= current.childCount)
            {
                return null;
            }

            current = current.GetChild(childIndex);
        }

        return current;
    }

    private static Transform ResolveByHierarchyPath(PlayModeLayoutCaptureItem item)
    {
        Scene scene = SceneManager.GetSceneByPath(item.ScenePath);

        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(item.HierarchyPath))
        {
            return null;
        }

        string[] parts = item.HierarchyPath.Split('/');

        if (parts.Length == 0)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (!string.Equals(roots[i].name, parts[0], StringComparison.Ordinal))
            {
                continue;
            }

            Transform current = roots[i].transform;

            for (int partIndex = 1; partIndex < parts.Length; partIndex++)
            {
                current = FindDirectChildByName(current, parts[partIndex]);

                if (current == null)
                {
                    break;
                }
            }

            if (current != null)
            {
                return current;
            }
        }

        return null;
    }

    private static bool EnsureCapturedScenesAreLoaded(
        PlayModeLayoutCapturePayload payload,
        bool openMissingScenes,
        out string message)
    {
        List<string> missingScenePaths = new List<string>();

        for (int i = 0; i < payload.Items.Count; i++)
        {
            string scenePath = payload.Items[i].ScenePath;

            if (string.IsNullOrWhiteSpace(scenePath) ||
                IsSceneLoaded(scenePath) ||
                ContainsScenePath(missingScenePaths, scenePath))
            {
                continue;
            }

            missingScenePaths.Add(scenePath);
        }

        if (missingScenePaths.Count == 0)
        {
            message = string.Empty;
            return true;
        }

        if (!openMissingScenes)
        {
            message =
                $"Pending layout capture targets scene '{missingScenePaths[0]}', but that scene is not loaded. " +
                "Open the Layout Capture window and click Apply Pending Capture to load the scene and apply it.";
            return false;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            message = "Apply canceled before loading the captured scene.";
            return false;
        }

        for (int i = 0; i < missingScenePaths.Count; i++)
        {
            string scenePath = missingScenePaths[i];

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                message = $"Cannot load captured scene because it does not exist at '{scenePath}'.";
                return false;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        message = string.Empty;
        return true;
    }

    private static PlayModeLayoutCapturePayload LoadPendingCapture()
    {
        string json = SessionState.GetString(PendingCaptureSessionKey, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<PlayModeLayoutCapturePayload>(json);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static void ClearPendingCapture()
    {
        SessionState.EraseString(PendingCaptureSessionKey);
        SessionState.SetInt(PendingCaptureAutoApplySessionKey, 0);
    }

    private static void AddUniqueTransform(List<Transform> targets, HashSet<Transform> seen, Transform target)
    {
        if (target == null || seen.Contains(target))
        {
            return;
        }

        seen.Add(target);
        targets.Add(target);
    }

    private static bool IsTransformInScene(Transform transform, string scenePath)
    {
        return transform != null &&
               transform.gameObject.scene.IsValid() &&
               IsSameScenePath(transform.gameObject.scene.path, scenePath);
    }

    private static bool IsSceneLoaded(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        return scene.IsValid() && scene.isLoaded;
    }

    private static bool ContainsScenePath(List<string> scenePaths, string scenePath)
    {
        for (int i = 0; i < scenePaths.Count; i++)
        {
            if (IsSameScenePath(scenePaths[i], scenePath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameScenePath(string left, string right)
    {
        return string.Equals(
            string.IsNullOrWhiteSpace(left) ? string.Empty : left.Trim(),
            string.IsNullOrWhiteSpace(right) ? string.Empty : right.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static Transform FindDirectChildByName(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        Transform current = transform;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private static string GetTransformIndexPath(Transform transform)
    {
        Stack<string> indexes = new Stack<string>();
        Transform current = transform;

        while (current != null)
        {
            indexes.Push(current.GetSiblingIndex().ToString());
            current = current.parent;
        }

        return string.Join("/", indexes.ToArray());
    }

    private static int CompareTransformsByName(Transform left, Transform right)
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithPrefix(string value, string prefix)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProtectedEntranceGuestSpot(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        RoomAnchor roomAnchor = target.GetComponent<RoomAnchor>();
        return StartsWithPrefix(target.name, ProtectedEntranceGuestSpotPrefix) ||
               (roomAnchor != null && StartsWithPrefix(roomAnchor.AnchorId, ProtectedEntranceGuestSpotPrefix));
    }

    private static bool IsProtectedEntranceGuestSpot(PlayModeLayoutCaptureItem item)
    {
        return item != null &&
               (StartsWithPrefix(item.ObjectName, ProtectedEntranceGuestSpotPrefix) ||
                StartsWithPrefix(item.RoomAnchorId, ProtectedEntranceGuestSpotPrefix));
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(
            string.IsNullOrWhiteSpace(left) ? string.Empty : left.Trim(),
            string.IsNullOrWhiteSpace(right) ? string.Empty : right.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    private sealed class PlayModeLayoutCapturePayload
    {
        public int Version;
        public string CaptureName;
        public string CapturedAt;
        public List<PlayModeLayoutCaptureItem> Items = new List<PlayModeLayoutCaptureItem>();
    }

    [Serializable]
    private sealed class PlayModeLayoutCaptureItem
    {
        public string ObjectName;
        public string ScenePath;
        public string HierarchyPath;
        public string TransformIndexPath;
        public string GlobalObjectId;
        public string RoomAnchorId;
        public string RoomId;
        public bool IsRectTransform;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public Vector2 AnchoredPosition;
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 SizeDelta;
        public Vector2 Pivot;
    }
}
