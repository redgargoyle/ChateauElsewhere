using System;
using UnityEditor;
using UnityEngine;

[Obsolete("GuestButlerScaleTool has been replaced by Tools > Characters > Guest Size Master.")]
public sealed class GuestButlerScaleTool : EditorWindow
{
    [MenuItem("Tools/Characters/Guest Butler Scale (Obsolete)")]
    private static void Open()
    {
        GetWindow<GuestButlerScaleTool>("Guest Butler Scale");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "This tool is obsolete. Use Tools > Characters > Guest Size Master.",
            MessageType.Warning);

        if (GUILayout.Button("Open Guest Size Master"))
        {
            EditorApplication.ExecuteMenuItem("Tools/Characters/Guest Size Master");
        }
    }
}
