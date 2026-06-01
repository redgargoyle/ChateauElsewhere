using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class NavigationRoomImageShaderControlsWindow : EditorWindow
{
    private const string WindowTitle = "Room Image Shader Controls";

    private CameraManager cameraManager;
    private RoomContentGroup roomContentGroup;
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
        roomContentGroup = (RoomContentGroup)EditorGUILayout.ObjectField("Room", roomContentGroup, typeof(RoomContentGroup), true);
        followSelection = EditorGUILayout.Toggle("Follow Selection", followSelection);

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

            using (new EditorGUI.DisabledScope(roomContentGroup == null))
            {
                if (GUILayout.Button("Preview Room"))
                {
                    PreviewSelectedRoom();
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(roomContentGroup == null || cameraManager == null))
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
        RoomContentGroup selectedRoomContentGroup = NavigationEditorTools.FindSelectedRoomContentGroup();

        if (selectedRoomContentGroup != null)
        {
            roomContentGroup = selectedRoomContentGroup;
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
        if (roomContentGroup == null)
        {
            return;
        }

        NavigationEditorTools.PreviewRoomForDoorEditing(roomContentGroup);
        EnsureReferences();

        Texture roomTexture = GetRoomContentTexture(roomContentGroup);

        if (cameraManager != null && roomTexture != null)
        {
            Undo.RecordObject(cameraManager, "Preview Room Background");
            cameraManager.PreviewRoomBackground(roomTexture);
            ApplyPreviewLook();
        }
    }

    private void PreviewFullImagePlacement()
    {
        if (roomContentGroup == null)
        {
            return;
        }

        NavigationEditorTools.PreviewRoomForDoorEditing(roomContentGroup);
        EnsureReferences();

        Texture roomTexture = GetRoomContentTexture(roomContentGroup);

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

        MarkSceneDirty(cameraManager.gameObject);
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

    private static Texture GetRoomContentTexture(RoomContentGroup selectedRoomContentGroup)
    {
        if (selectedRoomContentGroup != null && selectedRoomContentGroup.TryGetRoomBackgroundTexture(out Texture roomTexture))
        {
            return roomTexture;
        }

        return null;
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
