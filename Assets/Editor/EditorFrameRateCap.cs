using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EditorFrameRateCap
{
    private const int TargetFrameRate = 60;
    private const int DisabledVSyncCount = 0;
    private const double EnforcementIntervalSeconds = 5.0;

    private static double nextEnforcementTime;

    static EditorFrameRateCap()
    {
        ApplyCap();

        EditorApplication.delayCall += ApplyCap;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += EnforceCapOccasionally;
    }

    [MenuItem("Tools/Performance/Reapply 60 FPS Editor Cap")]
    private static void ReapplyCap()
    {
        ApplyCap();
        Debug.Log("Editor frame rate cap set to 60 FPS.");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode ||
            state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredPlayMode)
        {
            ApplyCap();
        }
    }

    private static void EnforceCapOccasionally()
    {
        if (EditorApplication.timeSinceStartup < nextEnforcementTime)
        {
            return;
        }

        nextEnforcementTime = EditorApplication.timeSinceStartup + EnforcementIntervalSeconds;

        if (QualitySettings.vSyncCount != DisabledVSyncCount ||
            Application.targetFrameRate != TargetFrameRate)
        {
            ApplyCap();
        }
    }

    private static void ApplyCap()
    {
        QualitySettings.vSyncCount = DisabledVSyncCount;
        Application.targetFrameRate = TargetFrameRate;
    }
}
