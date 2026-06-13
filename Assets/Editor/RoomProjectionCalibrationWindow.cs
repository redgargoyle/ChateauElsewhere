using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class RoomProjectionCalibrationWindow : EditorWindow
{
    private const string RoomProfilesFolderPath = "Assets/ScriptableObjects/Rooms";
    private const string DrawingRoomProfilePath = "Assets/ScriptableObjects/Rooms/DrawingRoomPerspectiveProfile.asset";
    private const string StandardAdultProfilePath = "Assets/ScriptableObjects/Characters/StandardAdultVisualProfile.asset";

    private RoomPerspectiveProfile previewProfile;
    private Vector2 previewFootPoint = new Vector2(0f, -120f);
    private float previewAdultHeight = 290f;

    [MenuItem("Tools/Room Projection/Calibration Window")]
    public static void Open()
    {
        GetWindow<RoomProjectionCalibrationWindow>("Room Projection");
    }

    [MenuItem("Tools/Room Projection/Create Drawing Room Perspective Profile")]
    public static RoomPerspectiveProfile CreateDrawingRoomPerspectiveProfile()
    {
        EnsureAssetFolder("Assets/ScriptableObjects");
        EnsureAssetFolder("Assets/ScriptableObjects/Rooms");

        RoomPerspectiveProfile profile = AssetDatabase.LoadAssetAtPath<RoomPerspectiveProfile>(DrawingRoomProfilePath);

        if (profile == null)
        {
            profile = CreateInstance<RoomPerspectiveProfile>();
            AssetDatabase.CreateAsset(profile, DrawingRoomProfilePath);
        }

        profile.ConfigureDrawingRoomDefaults();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
        return profile;
    }

    [MenuItem("Tools/Room Projection/Create Perspective Profiles For Scene Rooms")]
    public static void CreatePerspectiveProfilesForSceneRooms()
    {
        EnsureAssetFolder("Assets/ScriptableObjects");
        EnsureAssetFolder(RoomProfilesFolderPath);

        int assignedCount = 0;
        RoomContentGroup[] rooms = Resources.FindObjectsOfTypeAll<RoomContentGroup>();

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomContentGroup room = rooms[i];

            if (room == null || EditorUtility.IsPersistent(room) || string.IsNullOrWhiteSpace(room.RoomName))
            {
                continue;
            }

            RoomPerspectiveProfile profile = room.PerspectiveProfile;

            if (profile == null)
            {
                string profilePath = $"{RoomProfilesFolderPath}/{GetSafeAssetName(room.RoomName)}PerspectiveProfile.asset";
                profile = AssetDatabase.LoadAssetAtPath<RoomPerspectiveProfile>(profilePath);

                if (profile == null)
                {
                    profile = CreateInstance<RoomPerspectiveProfile>();
                    ConfigureRoomProfileDefaults(profile, room);
                    AssetDatabase.CreateAsset(profile, profilePath);
                }
            }

            Undo.RecordObject(room, "Assign Room Perspective Profile");
            room.SetPerspectiveProfile(profile);
            EditorUtility.SetDirty(room);

            if (room.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            }

            assignedCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Assigned room perspective profiles to {assignedCount} room(s).");
    }

    [MenuItem("Tools/Room Projection/Create Standard Adult Visual Profile")]
    public static CharacterVisualProfile CreateStandardAdultVisualProfile()
    {
        EnsureAssetFolder("Assets/ScriptableObjects");
        EnsureAssetFolder("Assets/ScriptableObjects/Characters");

        CharacterVisualProfile profile = AssetDatabase.LoadAssetAtPath<CharacterVisualProfile>(StandardAdultProfilePath);

        if (profile == null)
        {
            profile = CreateInstance<CharacterVisualProfile>();
            AssetDatabase.CreateAsset(profile, StandardAdultProfilePath);
        }

        profile.Configure(
            "Standard Adult",
            1f,
            290f,
            220f,
            new Vector2(0.5f, 0f),
            0,
            1,
            -2);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
        return profile;
    }

    private void OnEnable()
    {
        if (previewProfile == null)
        {
            previewProfile = AssetDatabase.LoadAssetAtPath<RoomPerspectiveProfile>(DrawingRoomProfilePath);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Shared Room Perspective", EditorStyles.boldLabel);
        previewProfile = (RoomPerspectiveProfile)EditorGUILayout.ObjectField(
            "Profile",
            previewProfile,
            typeof(RoomPerspectiveProfile),
            false);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Drawing Room Profile"))
            {
                previewProfile = CreateDrawingRoomPerspectiveProfile();
            }

            if (GUILayout.Button("Create Adult Visual Profile"))
            {
                CreateStandardAdultVisualProfile();
            }
        }

        if (GUILayout.Button("Create/Assign Profiles For Scene Rooms"))
        {
            CreatePerspectiveProfilesForSceneRooms();
        }

        EditorGUILayout.Space();
        DrawPreviewControls();
        EditorGUILayout.Space();
        DrawSelectedEntitySummary();
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.LabelField("Foot Point Preview", EditorStyles.boldLabel);
        previewFootPoint = EditorGUILayout.Vector2Field("Room Local Foot", previewFootPoint);
        previewAdultHeight = Mathf.Max(1f, EditorGUILayout.FloatField("Adult Height", previewAdultHeight));

        if (previewProfile == null)
        {
            EditorGUILayout.HelpBox("Assign or create a room perspective profile to preview depth, scale, tint, and sorting.", MessageType.Info);
            return;
        }

        float depth = previewProfile.GetDepth01(previewFootPoint);
        float scale = previewProfile.GetScale(previewFootPoint);
        Color tint = previewProfile.GetTint(previewFootPoint);
        int sortingOrder = previewProfile.GetSortingOrder(previewFootPoint);

        EditorGUILayout.LabelField("Depth 0-1", depth.ToString("0.000"));
        EditorGUILayout.LabelField("Scale", scale.ToString("0.000"));
        EditorGUILayout.LabelField("Projected Adult Height", (previewAdultHeight * scale).ToString("0.0 px"));
        EditorGUILayout.ColorField("Tint", tint);
        EditorGUILayout.LabelField("Sorting Order", sortingOrder.ToString());
    }

    private void DrawSelectedEntitySummary()
    {
        EditorGUILayout.LabelField("Selected Entity", EditorStyles.boldLabel);
        RoomProjectedEntity selectedEntity = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent<RoomProjectedEntity>()
            : null;

        if (selectedEntity == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with RoomProjectedEntity to inspect its current projected values.", MessageType.None);
            return;
        }

        EditorGUILayout.ObjectField("Entity", selectedEntity, typeof(RoomProjectedEntity), true);
        EditorGUILayout.LabelField("Foot Point", selectedEntity.RoomLocalFootPoint.ToString("F1"));
        EditorGUILayout.LabelField("Has Profile", selectedEntity.HasUsableProfile ? "Yes" : "No");

        if (selectedEntity.HasUsableProfile)
        {
            EditorGUILayout.LabelField("Current Scale", selectedEntity.CurrentScale.ToString("0.000"));
            EditorGUILayout.LabelField("Current Sorting", selectedEntity.CurrentSortingOrder.ToString());
        }

        if (GUILayout.Button("Apply Projection Now"))
        {
            selectedEntity.RefreshVisualTargets();
            selectedEntity.ApplyProjection();
            EditorUtility.SetDirty(selectedEntity);
        }
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = System.IO.Path.GetFileName(folderPath);

        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static void ConfigureRoomProfileDefaults(RoomPerspectiveProfile profile, RoomContentGroup room)
    {
        RectTransform roomStage = room != null ? room.transform as RectTransform : null;
        Vector2 referenceSize = new Vector2(1366f, 768f);
        float nearY = -360f;
        float farY = 140f;

        if (roomStage != null)
        {
            Rect rect = roomStage.rect;
            referenceSize = new Vector2(Mathf.Max(1f, rect.width), Mathf.Max(1f, rect.height));
            nearY = rect.yMin;
            farY = rect.yMax;
        }

        profile.Configure(
            room != null ? room.RoomName : "Room",
            referenceSize,
            nearY,
            farY,
            AnimationCurve.EaseInOut(0f, 1f, 1f, 0.54f),
            null,
            1000,
            8000,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));
    }

    private static string GetSafeAssetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Room";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.Length > 0 ? builder.ToString() : "Room";
    }
}
