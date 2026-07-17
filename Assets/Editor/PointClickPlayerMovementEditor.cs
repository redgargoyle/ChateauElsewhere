using UnityEditor;

[CustomEditor(typeof(PointClickPlayerMovement))]
[CanEditMultipleObjects]
public sealed class PointClickPlayerMovementEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
