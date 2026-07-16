using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PointClickPlayerMovement))]
[CanEditMultipleObjects]
public sealed class PointClickPlayerMovementEditor : Editor
{
    private static readonly string[] HiddenLegacyScaleFields =
    {
        "nearY",
        "farY",
        "nearScale",
        "farScale",
        "useRoomPerspectiveProfileScale",
        "useButlerRoomScaleOverrides",
        "hasButlerCalibrationBaseLocalScale",
        "butlerCalibrationBaseLocalScale",
        "editorSelectedButlerScaleRoomId",
        "butlerRoomScaleOverrides",
        "applyPerspectiveScale"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, HiddenLegacyScaleFields);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Character Display Size", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "PointClickPlayerMovement owns movement and supplies the Butler's room-local foot position. " +
            "CharacterRoomScaleController is the only system allowed to set the displayed character size.",
            MessageType.Info);

        if (GUILayout.Button("Open Character Room Scale Catalog"))
        {
            CharacterRoomScaleCatalogWindow.Open();
        }
    }
}
