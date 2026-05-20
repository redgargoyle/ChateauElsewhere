using UnityEngine;

public static class IdeaGameFlow
{
    private const string TutorialPendingKey = "Dreadforge_IdeaTutorialPending";

    public static void MarkNewGameStarted()
    {
        PlayerPrefs.SetInt(TutorialPendingKey, 1);
        PlayerPrefs.Save();
    }

    public static bool ConsumeTutorialRequest()
    {
        if (PlayerPrefs.GetInt(TutorialPendingKey, 0) != 1)
        {
            return false;
        }

        PlayerPrefs.SetInt(TutorialPendingKey, 0);
        PlayerPrefs.Save();
        return true;
    }
}
