using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(RoomPerspectiveProfile))]
public sealed class RoomPerspectiveProfileEditor : Editor
{
    private float uniformScaleMultiplier = 1f;

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
        EditorGUILayout.LabelField("Character Y Scale", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "These controls affect characters that use this room profile. Front/Near is depth 0; Back/Far is depth 1.",
            MessageType.Info);

        float currentNearY = profile.NearFootY;
        float currentFarY = profile.FarFootY;
        float currentNearScale = profile.NearScale;
        float currentFarScale = profile.FarScale;

        EditorGUI.BeginChangeCheck();
        float nearY = EditorGUILayout.FloatField("Front/Near Foot Y", currentNearY);
        float farY = EditorGUILayout.FloatField("Back/Far Foot Y", currentFarY);
        float nearScale = Mathf.Max(0.001f, EditorGUILayout.FloatField("Front/Near Scale", currentNearScale));
        float farScale = Mathf.Max(0.001f, EditorGUILayout.FloatField("Back/Far Scale", currentFarScale));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(profile, "Edit Room Character Y Scale");
            if (!Mathf.Approximately(currentNearY, nearY) || !Mathf.Approximately(currentFarY, farY))
            {
                profile.SetDepthYRange(nearY, farY);
            }

            if (!Mathf.Approximately(currentNearScale, nearScale) || !Mathf.Approximately(currentFarScale, farScale))
            {
                profile.SetScaleEndpoints(nearScale, farScale);
            }

            EditorUtility.SetDirty(profile);
            RefreshProjectedEntitiesUsing(profile);
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            uniformScaleMultiplier = Mathf.Max(0.001f, EditorGUILayout.FloatField("Uniform Multiplier", uniformScaleMultiplier));

            if (GUILayout.Button("Apply Multiplier"))
            {
                Undo.RecordObject(profile, "Scale Room Character Projection");
                profile.ApplyScaleMultiplier(uniformScaleMultiplier);
                EditorUtility.SetDirty(profile);
                RefreshProjectedEntitiesUsing(profile);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Scene Characters"))
            {
                RefreshProjectedEntitiesUsing(profile);
            }

            if (GUILayout.Button("Reset Multiplier"))
            {
                uniformScaleMultiplier = 1f;
            }
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

        RefreshPointClickMovementsUsing(profile);
        RefreshRoomPersonWalkersUsing(profile);
        SceneView.RepaintAll();
    }

    private static void RefreshPointClickMovementsUsing(RoomPerspectiveProfile profile)
    {
        PointClickPlayerMovement[] movements = Resources.FindObjectsOfTypeAll<PointClickPlayerMovement>();

        for (int i = 0; i < movements.Length; i++)
        {
            PointClickPlayerMovement movement = movements[i];

            if (movement == null ||
                EditorUtility.IsPersistent(movement) ||
                !movement.UsesPerspectiveProfile(profile))
            {
                continue;
            }

            Undo.RecordObject(movement.transform, "Refresh Room Perspective Scale");
            movement.RefreshPerspectiveScaleNow(true);
            EditorUtility.SetDirty(movement);
            EditorUtility.SetDirty(movement.transform);

            if (!Application.isPlaying && movement.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
            }
        }
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

            Undo.RecordObject(walker.transform, "Refresh Room Perspective Scale");
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
