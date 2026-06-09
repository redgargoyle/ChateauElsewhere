using UnityEditor;
using UnityEngine;

public sealed class RoomProjectionCalibrationWindow : EditorWindow
{
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
}
