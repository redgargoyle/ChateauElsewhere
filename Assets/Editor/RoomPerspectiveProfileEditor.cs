using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RoomPerspectiveProfile))]
public sealed class RoomPerspectiveProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RoomPerspectiveProfile profile = (RoomPerspectiveProfile)target;

        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool defaultInspectorChanged = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        if (defaultInspectorChanged)
        {
            EditorUtility.SetDirty(profile);
            RefreshProjectedEntitiesUsing(profile);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Depth Range", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Front/Near is depth 0 and Back/Far is depth 1 for tint, sorting, and contact-shadow presentation.",
            MessageType.Info);

        float currentNearY = profile.NearFootY;
        float currentFarY = profile.FarFootY;

        EditorGUI.BeginChangeCheck();
        float nearY = EditorGUILayout.FloatField("Front/Near Foot Y", currentNearY);
        float farY = EditorGUILayout.FloatField("Back/Far Foot Y", currentFarY);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(profile, "Edit Room Depth Range");
            if (!Mathf.Approximately(currentNearY, nearY) || !Mathf.Approximately(currentFarY, farY))
            {
                profile.SetDepthYRange(nearY, farY);
            }

            EditorUtility.SetDirty(profile);
            RefreshProjectedEntitiesUsing(profile);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Refresh Scene Presentation"))
        {
            RefreshProjectedEntitiesUsing(profile);
        }
    }

    private static void RefreshProjectedEntitiesUsing(RoomPerspectiveProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        RoomProjectedEntity[] entities = Resources.FindObjectsOfTypeAll<RoomProjectedEntity>();

        for (int i = 0; i < entities.Length; i++)
        {
            RoomProjectedEntity entity = entities[i];

            if (entity == null ||
                entity.RoomProfile != profile ||
                EditorUtility.IsPersistent(entity))
            {
                continue;
            }

            Transform visualRoot = entity.VisualRoot;
            Undo.RecordObject(entity.transform, "Refresh Room Projection");
            if (visualRoot != null && visualRoot != entity.transform)
            {
                Undo.RecordObject(visualRoot, "Refresh Room Projection");
            }

            entity.RefreshVisualTargets();
            entity.ApplyProjection();
            EditorUtility.SetDirty(entity);
            EditorUtility.SetDirty(entity.transform);
            if (visualRoot != null)
            {
                EditorUtility.SetDirty(visualRoot);
            }

            if (!Application.isPlaying && entity.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(entity.gameObject.scene);
            }
        }

		RefreshRoomPersonWalkersUsing(profile);
		SceneView.RepaintAll();
	}

	private static void RefreshRoomPersonWalkersUsing(RoomPerspectiveProfile profile)
    {
        RoomPersonWalker2D[] walkers = Resources.FindObjectsOfTypeAll<RoomPersonWalker2D>();

        for (int i = 0; i < walkers.Length; i++)
        {
            RoomPersonWalker2D walker = walkers[i];

            if (walker == null ||
                EditorUtility.IsPersistent(walker) ||
                !walker.UsesPerspectiveProfile(profile))
            {
                continue;
            }

            Undo.RecordObject(walker.transform, "Refresh Room Perspective Presentation");
            walker.RefreshDepthVisualsNow();
            EditorUtility.SetDirty(walker);
            EditorUtility.SetDirty(walker.transform);

            if (!Application.isPlaying && walker.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(walker.gameObject.scene);
            }
        }
    }
}
