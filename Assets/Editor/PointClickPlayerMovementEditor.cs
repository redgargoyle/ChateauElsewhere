using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PointClickPlayerMovement))]
[CanEditMultipleObjects]
public sealed class PointClickPlayerMovementEditor : Editor
{
    private static readonly string[] HiddenButlerScaleFields =
    {
        "useButlerRoomScaleOverrides",
        "hasButlerCalibrationBaseLocalScale",
        "butlerCalibrationBaseLocalScale",
        "editorSelectedButlerScaleRoomId",
        "butlerRoomScaleOverrides"
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, HiddenButlerScaleFields);
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Butler Room Scale Calibration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use the step-based window for Butler/player room scale calibration. Guests are still calibrated through RoomProjectedEntity.",
            MessageType.Info);

        if (GUILayout.Button("Open Butler Room Scale Calibration Window"))
        {
            ButlerRoomScaleCalibrationWindow.Open();
        }
    }
}
