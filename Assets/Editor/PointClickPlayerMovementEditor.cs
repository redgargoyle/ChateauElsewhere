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
        EditorGUILayout.LabelField("Room Character Scale Calibration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use the room character scale window to calibrate the selected room profile. The Butler is only the measuring object; RoomPerspectiveProfile drives all characters.",
            MessageType.Info);

        if (GUILayout.Button("Open Room Character Scale Calibration Window"))
        {
            ButlerRoomScaleCalibrationWindow.Open();
        }
    }
}
