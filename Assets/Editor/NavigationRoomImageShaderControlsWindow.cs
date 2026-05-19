using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class NavigationRoomImageShaderControlsWindow : EditorWindow
{
    private const string WindowTitle = "Room Image Shader Controls";

    private CameraManager cameraManager;
    private CameraAreaController cameraArea;
    private bool followCapturedTriggerAnchors = true;
    private bool followSelection = true;
    private float horizontalPan;
    private float verticalPan;
    private float fov = 1f;

    [MenuItem("Dreadforge/Navigation/Open Room Image Shader Controls")]
    public static void OpenWindow()
    {
        NavigationRoomImageShaderControlsWindow window = GetWindow<NavigationRoomImageShaderControlsWindow>();
        window.titleContent = new GUIContent(WindowTitle);
        window.RefreshReferencesFromSelection();
        window.Show();
    }

    private void OnEnable()
    {
        Selection.selectionChanged += HandleSelectionChanged;
        RefreshReferencesFromSelection();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= HandleSelectionChanged;
    }

    private void OnGUI()
    {
        EnsureReferences();

        EditorGUILayout.LabelField("Room Preview", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        cameraManager = (CameraManager)EditorGUILayout.ObjectField("Camera Manager", cameraManager, typeof(CameraManager), true);
        cameraArea = (CameraAreaController)EditorGUILayout.ObjectField("Room Camera", cameraArea, typeof(CameraAreaController), true);
        followSelection = EditorGUILayout.Toggle("Follow Selection", followSelection);
        followCapturedTriggerAnchors = EditorGUILayout.Toggle("Move Captured Triggers", followCapturedTriggerAnchors);

        if (EditorGUI.EndChangeCheck())
        {
            SyncLookFieldsFromManager();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection"))
            {
                RefreshReferencesFromSelection();
            }

            using (new EditorGUI.DisabledScope(cameraArea == null))
            {
                if (GUILayout.Button("Preview Room"))
                {
                    PreviewSelectedRoom();
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(cameraArea == null || cameraManager == null))
            {
                if (GUILayout.Button("Full Image Placement"))
                {
                    PreviewFullImagePlacement();
                }

                if (GUILayout.Button("Runtime Shader View"))
                {
                    PreviewSelectedRoom();
                }
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Shader View", EditorStyles.boldLabel);

        bool canPreview = cameraManager != null;
        using (new EditorGUI.DisabledScope(!canPreview))
        {
            float horizontalLimit = cameraManager != null ? Mathf.Clamp01(cameraManager.maxRoomPan) : 1f;
            float verticalLimit = cameraManager != null ? Mathf.Clamp01(cameraManager.maxRoomVerticalPan) : 1f;
            float minFov = cameraManager != null ? Mathf.Min(cameraManager.minRoomFov, cameraManager.maxRoomFov) : 0.55f;
            float maxFov = cameraManager != null ? Mathf.Max(cameraManager.minRoomFov, cameraManager.maxRoomFov) : 1f;

            EditorGUI.BeginChangeCheck();
            horizontalPan = EditorGUILayout.Slider("Horizontal Pan", horizontalPan, -horizontalLimit, horizontalLimit);
            verticalPan = EditorGUILayout.Slider("Vertical Pan", verticalPan, -verticalLimit, verticalLimit);
            fov = EditorGUILayout.Slider("FOV", fov, minFov, maxFov);

            if (EditorGUI.EndChangeCheck())
            {
                ApplyPreviewLook();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Left"))
                {
                    SetPreviewLook(-horizontalLimit, verticalPan, fov);
                }

                if (GUILayout.Button("Center"))
                {
                    SetPreviewLook(0f, verticalPan, fov);
                }

                if (GUILayout.Button("Right"))
                {
                    SetPreviewLook(horizontalLimit, verticalPan, fov);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bottom"))
                {
                    SetPreviewLook(horizontalPan, -verticalLimit, fov);
                }

                if (GUILayout.Button("Wide"))
                {
                    SetPreviewLook(horizontalPan, verticalPan, minFov);
                }

                if (GUILayout.Button("Top"))
                {
                    SetPreviewLook(horizontalPan, verticalLimit, fov);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Runtime Default"))
                {
                    float defaultFov = cameraManager != null ? cameraManager.defaultRoomFov : 1f;
                    SetPreviewLook(0f, 0f, defaultFov);
                }

                if (GUILayout.Button("Apply Captured Anchors"))
                {
                    ApplyCapturedTriggerAnchorsForCurrentRoom(true);
                }
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Door Trigger Anchors", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(cameraManager == null))
        {
            if (GUILayout.Button("Capture Selected Trigger Anchor"))
            {
                CaptureSelectedTriggerAnchor();
            }

            using (new EditorGUI.DisabledScope(cameraArea == null))
            {
                if (GUILayout.Button("Capture All Room Trigger Anchors"))
                {
                    CaptureAllTriggerAnchorsForCurrentRoom();
                }
            }
        }
    }

    private void HandleSelectionChanged()
    {
        if (!followSelection)
        {
            return;
        }

        RefreshReferencesFromSelection();
        Repaint();
    }

    private void RefreshReferencesFromSelection()
    {
        CameraAreaController selectedCameraArea = NavigationEditorTools.FindSelectedCameraArea();

        if (selectedCameraArea != null)
        {
            cameraArea = selectedCameraArea;
        }

        if (cameraManager == null)
        {
            cameraManager = FindSceneCameraManager();
        }

        SyncLookFieldsFromManager();
    }

    private void EnsureReferences()
    {
        if (cameraManager == null)
        {
            cameraManager = FindSceneCameraManager();
        }
    }

    private void SyncLookFieldsFromManager()
    {
        if (cameraManager == null)
        {
            return;
        }

        horizontalPan = cameraManager.CurrentRoomHorizontalPan;
        verticalPan = cameraManager.CurrentRoomVerticalPan;
        fov = cameraManager.CurrentRoomFov;
    }

    private void PreviewSelectedRoom()
    {
        if (cameraArea == null)
        {
            return;
        }

        NavigationEditorTools.PreviewCameraForDoorEditing(cameraArea);
        EnsureReferences();

        Texture roomTexture = cameraArea.GetEffectiveRoomBackgroundTexture();

        if (cameraManager != null && roomTexture != null)
        {
            Undo.RecordObject(cameraManager, "Preview Room Background");
            cameraManager.PreviewRoomBackground(roomTexture);
            ApplyPreviewLook();
        }
    }

    private void PreviewFullImagePlacement()
    {
        if (cameraArea == null)
        {
            return;
        }

        NavigationEditorTools.PreviewCameraForDoorEditing(cameraArea);
        EnsureReferences();

        Texture roomTexture = cameraArea.GetEffectiveRoomBackgroundTexture();

        if (cameraManager == null || roomTexture == null)
        {
            return;
        }

        if (cameraManager.cameraBackground != null)
        {
            Undo.RecordObject(cameraManager.cameraBackground, "Preview Full Room Image");
        }

        Undo.RecordObject(cameraManager, "Preview Full Room Image");
        cameraManager.PreviewFullRoomImageForDoorEditing(roomTexture);

        // In full-image placement view, captured trigger anchors are source-image
        // UV rectangles. Applying them here makes existing doors appear in the
        // same full-image coordinate space you are editing.
        ApplyCapturedTriggerAnchorsForCurrentRoom(false);
        MarkSceneDirty(cameraManager.gameObject);
    }

    private void SetPreviewLook(float newHorizontalPan, float newVerticalPan, float newFov)
    {
        horizontalPan = newHorizontalPan;
        verticalPan = newVerticalPan;
        fov = newFov;
        ApplyPreviewLook();
    }

    private void ApplyPreviewLook()
    {
        if (cameraManager == null)
        {
            return;
        }

        Undo.RecordObject(cameraManager, "Preview Room Shader Look");
        cameraManager.EndFullImagePlacementPreview();
        cameraManager.SetRoomLookForPreview(horizontalPan, verticalPan, fov);

        // Already-captured triggers have a saved shader-space rectangle. Moving
        // them here lets the Scene view prove that the hitboxes are attached to
        // the picture, not to the static camera frame.
        if (followCapturedTriggerAnchors)
        {
            ApplyCapturedTriggerAnchorsForCurrentRoom(false);
        }

        MarkSceneDirty(cameraManager.gameObject);
    }

    private void CaptureSelectedTriggerAnchor()
    {
        DoorTriggerNavigation selectedTrigger = FindSelectedDoorTrigger();

        if (selectedTrigger == null)
        {
            Debug.LogWarning("Select a DoorTrigger_* object before capturing a trigger anchor.");
            return;
        }

        Undo.RecordObject(selectedTrigger, "Capture Door Trigger Shader Anchor");

        if (!selectedTrigger.CaptureCurrentShaderAnchor(cameraManager))
        {
            Debug.LogWarning($"Could not capture a shader anchor for '{selectedTrigger.name}'. Make sure the room background is visible.", selectedTrigger);
            return;
        }

        EditorUtility.SetDirty(selectedTrigger);
        MarkSceneDirty(selectedTrigger.gameObject);
        Debug.Log($"Captured shader anchor for '{selectedTrigger.name}'.");
    }

    private void CaptureAllTriggerAnchorsForCurrentRoom()
    {
        string roomName = GetCurrentRoomName();

        if (string.IsNullOrEmpty(roomName))
        {
            Debug.LogWarning("Select or assign a Button_* object under Map before capturing room trigger anchors.");
            return;
        }

        DoorTriggerNavigation[] triggers = FindDoorTriggersForRoom(roomName);
        int capturedCount = 0;

        for (int i = 0; i < triggers.Length; i++)
        {
            DoorTriggerNavigation trigger = triggers[i];

            if (trigger == null || !trigger.gameObject.activeInHierarchy)
            {
                continue;
            }

            Undo.RecordObject(trigger, "Capture Room Door Trigger Shader Anchors");

            if (trigger.CaptureCurrentShaderAnchor(cameraManager))
            {
                capturedCount++;
                EditorUtility.SetDirty(trigger);
            }
        }

        if (cameraManager != null)
        {
            MarkSceneDirty(cameraManager.gameObject);
        }

        Debug.Log($"Captured {capturedCount} door trigger anchor(s) for '{roomName}'.");
    }

    private void ApplyCapturedTriggerAnchorsForCurrentRoom(bool logResult)
    {
        string roomName = GetCurrentRoomName();

        if (string.IsNullOrEmpty(roomName))
        {
            return;
        }

        DoorTriggerNavigation[] triggers = FindDoorTriggersForRoom(roomName);
        int appliedCount = 0;

        for (int i = 0; i < triggers.Length; i++)
        {
            DoorTriggerNavigation trigger = triggers[i];

            if (trigger == null || !trigger.HasBackgroundShaderAnchor)
            {
                continue;
            }

            RectTransform triggerRect = trigger.transform as RectTransform;

            if (triggerRect != null)
            {
                Undo.RecordObject(triggerRect, "Apply Door Trigger Shader Anchor");
            }

            if (trigger.ApplyCapturedShaderAnchor(cameraManager))
            {
                appliedCount++;
                EditorUtility.SetDirty(trigger);

                if (triggerRect != null)
                {
                    EditorUtility.SetDirty(triggerRect);
                }
            }
        }

        if (cameraManager != null)
        {
            MarkSceneDirty(cameraManager.gameObject);
        }

        if (logResult)
        {
            Debug.Log($"Applied {appliedCount} captured door trigger anchor(s) for '{roomName}'.");
        }
    }

    private string GetCurrentRoomName()
    {
        if (cameraArea == null)
        {
            return string.Empty;
        }

        string cleanName = cameraArea.name;

        if (cleanName.StartsWith("Button_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Button_".Length);
        }
        else if (cleanName.StartsWith("Cam_", StringComparison.OrdinalIgnoreCase))
        {
            cleanName = cleanName.Substring("Cam_".Length);
        }

        return cleanName.Replace('_', ' ').Trim();
    }

    private static DoorTriggerNavigation FindSelectedDoorTrigger()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            return null;
        }

        return selectedObject.GetComponentInParent<DoorTriggerNavigation>(true);
    }

    private static DoorTriggerNavigation[] FindDoorTriggersForRoom(string roomName)
    {
        DoorTriggerNavigation[] allTriggers = FindSceneObjects<DoorTriggerNavigation>();
        List<DoorTriggerNavigation> roomTriggers = new List<DoorTriggerNavigation>();

        for (int i = 0; i < allTriggers.Length; i++)
        {
            DoorTriggerNavigation trigger = allTriggers[i];

            if (trigger != null &&
                string.Equals(trigger.SourceRoom, roomName, StringComparison.OrdinalIgnoreCase))
            {
                roomTriggers.Add(trigger);
            }
        }

        return roomTriggers.ToArray();
    }

    private static CameraManager FindSceneCameraManager()
    {
        CameraManager[] cameraManagers = FindSceneObjects<CameraManager>();

        for (int i = 0; i < cameraManagers.Length; i++)
        {
            if (cameraManagers[i] != null && cameraManagers[i].cameraBackground != null)
            {
                return cameraManagers[i];
            }
        }

        return cameraManagers.Length > 0 ? cameraManagers[0] : null;
    }

    private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        List<T> sceneObjects = new List<T>();

        for (int i = 0; i < objects.Length; i++)
        {
            T candidate = objects[i];

            if (candidate == null || EditorUtility.IsPersistent(candidate))
            {
                continue;
            }

            sceneObjects.Add(candidate);
        }

        return sceneObjects.ToArray();
    }

    private static void MarkSceneDirty(GameObject sceneObject)
    {
        if (sceneObject != null && sceneObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(sceneObject.scene);
        }

        SceneView.RepaintAll();
    }
}
