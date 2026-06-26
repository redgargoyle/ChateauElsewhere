using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UnityEditorAudioGuard
{
    private const double EnforcementIntervalSeconds = 3.0;
    private static readonly string[] GameViewAudioProperties = { "m_PlayAudio", "m_AudioPlay" };
    private static double nextEnforcementTime;
    private static bool loggedGameViewFailure;
    private static bool loggedMasterMuteFailure;

    static UnityEditorAudioGuard()
    {
        EditorApplication.delayCall += EnforceAudioEnabled;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= EnforceAudioPeriodically;
        EditorApplication.update += EnforceAudioPeriodically;
    }

    [MenuItem("Tools/Audio/Force Editor Audio On")]
    public static void EnforceAudioEnabled()
    {
        bool changedGameView = EnableGameViewAudio();
        bool changedMasterMute = DisableEditorMasterMute();

        if (changedGameView || changedMasterMute)
        {
            Debug.Log("Unity editor audio guard re-enabled Game view/editor audio.");
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            EnforceAudioEnabled();
        }
    }

    private static void EnforceAudioPeriodically()
    {
        if (EditorApplication.timeSinceStartup < nextEnforcementTime)
        {
            return;
        }

        nextEnforcementTime = EditorApplication.timeSinceStartup + EnforcementIntervalSeconds;
        EnforceAudioEnabled();
    }

    private static bool EnableGameViewAudio()
    {
        Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");

        if (gameViewType == null)
        {
            return false;
        }

        UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
        bool changedAny = false;

        foreach (UnityEngine.Object gameView in gameViews)
        {
            if (gameView == null)
            {
                continue;
            }

            try
            {
                SerializedObject serializedObject = new SerializedObject(gameView);
                bool changedView = false;

                foreach (string propertyName in GameViewAudioProperties)
                {
                    SerializedProperty property = serializedObject.FindProperty(propertyName);

                    if (property != null &&
                        property.propertyType == SerializedPropertyType.Boolean &&
                        !property.boolValue)
                    {
                        property.boolValue = true;
                        changedView = true;
                    }
                }

                if (changedView)
                {
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    changedAny = true;

                    if (gameView is EditorWindow window)
                    {
                        window.Repaint();
                    }
                }
            }
            catch (Exception exception)
            {
                if (!loggedGameViewFailure)
                {
                    loggedGameViewFailure = true;
                    Debug.LogWarning($"Unity editor audio guard could not inspect Game view audio state: {exception.Message}");
                }
            }
        }

        return changedAny;
    }

    private static bool DisableEditorMasterMute()
    {
        Type audioUtilType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AudioUtil");

        if (audioUtilType == null)
        {
            return false;
        }

        MethodInfo getMasterMute = audioUtilType.GetMethod("GetMasterMute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo setMasterMute = audioUtilType.GetMethod("SetMasterMute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        if (getMasterMute == null || setMasterMute == null)
        {
            return false;
        }

        object muteValue;

        try
        {
            muteValue = getMasterMute.Invoke(null, null);
        }
        catch (Exception exception)
        {
            if (!loggedMasterMuteFailure)
            {
                loggedMasterMuteFailure = true;
                Debug.LogWarning($"Unity editor audio guard could not read editor master mute state: {exception.Message}");
            }

            return false;
        }

        if (muteValue is bool muted && muted)
        {
            try
            {
                setMasterMute.Invoke(null, new object[] { false });
                return true;
            }
            catch (Exception exception)
            {
                if (!loggedMasterMuteFailure)
                {
                    loggedMasterMuteFailure = true;
                    Debug.LogWarning($"Unity editor audio guard could not disable editor master mute: {exception.Message}");
                }
            }
        }

        return false;
    }
}
