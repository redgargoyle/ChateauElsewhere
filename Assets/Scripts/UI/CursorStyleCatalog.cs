using UnityEngine;

public static class CursorStyleCatalog
{
    public enum CursorAction
    {
        WalkMove,
        OpenDoor,
        ExitLeaveRoom,
        StairsUp,
        StairsDown,
        InspectLook,
        TalkConverse,
        PickUpTake,
        PickUpCoat,
        PlaceHangCoat,
        UseInteract,
        LockedCannotUse,
        NotAvailableDisabled
    }

    public const int DefaultStyleIndex = 1;
    public const int StyleCount = 10;
    public const string PlayerPrefsKey = "Dreadforge.CursorStyle";

    private const string ResourceRoot = "UI/Cursors/styles";

    public static readonly CursorAction[] ChooserPreviewActions =
    {
        CursorAction.WalkMove,
        CursorAction.OpenDoor,
        CursorAction.StairsUp,
        CursorAction.StairsDown,
        CursorAction.InspectLook,
        CursorAction.TalkConverse,
        CursorAction.PickUpTake,
        CursorAction.PickUpCoat,
        CursorAction.PlaceHangCoat,
        CursorAction.UseInteract,
        CursorAction.LockedCannotUse,
        CursorAction.NotAvailableDisabled
    };

    public static readonly string[] CursorActions =
    {
        "walk_move",
        "open_door",
        "exit_leave_room",
        "stairs_up",
        "stairs_down",
        "inspect_look",
        "talk_converse",
        "pick_up_take",
        "pick_up_coat",
        "place_hang_coat",
        "use_interact",
        "locked_cannot_use",
        "not_available_disabled"
    };

    public static int[] GetAvailableStyleIndices()
    {
        int[] styles = new int[StyleCount];

        for (int i = 0; i < styles.Length; i++)
        {
            styles[i] = i + 1;
        }

        return styles;
    }

    public static int GetSelectedStyleIndex()
    {
        return SanitizeStyleIndex(PlayerPrefs.GetInt(PlayerPrefsKey, DefaultStyleIndex));
    }

    public static bool SetSelectedStyleIndex(int styleIndex)
    {
        int cleanStyleIndex = SanitizeStyleIndex(styleIndex);

        if (GetSelectedStyleIndex() == cleanStyleIndex)
        {
            return false;
        }

        PlayerPrefs.SetInt(PlayerPrefsKey, cleanStyleIndex);
        PlayerPrefs.Save();
        return true;
    }

    public static int SanitizeStyleIndex(int styleIndex)
    {
        return styleIndex >= 1 && styleIndex <= StyleCount
            ? styleIndex
            : DefaultStyleIndex;
    }

    public static Texture2D LoadSelectedTexture(CursorAction action)
    {
        return LoadTexture(GetSelectedStyleIndex(), action);
    }

    public static Texture2D LoadTexture(int styleIndex, CursorAction action)
    {
        return Resources.Load<Texture2D>(GetCursorIconPath(styleIndex, action));
    }

    public static Texture2D LoadTexture(int styleIndex, string actionName)
    {
        return LoadTexture(styleIndex, GetActionFromName(actionName));
    }

    public static string GetCursorIconPath(int styleIndex, CursorAction action)
    {
        return $"{ResourceRoot}/style_{SanitizeStyleIndex(styleIndex):00}/{GetActionResourceName(action)}";
    }

    public static string GetCursorIconPath(int styleIndex, string actionName)
    {
        return GetCursorIconPath(styleIndex, GetActionFromName(actionName));
    }

    public static CursorAction GetActionFromName(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return CursorAction.UseInteract;
        }

        string cleanActionName = actionName.Trim();

        for (int i = 0; i < CursorActions.Length; i++)
        {
            if (string.Equals(cleanActionName, CursorActions[i], System.StringComparison.OrdinalIgnoreCase))
            {
                return (CursorAction)i;
            }
        }

        return CursorAction.UseInteract;
    }

    public static string GetActionResourceName(CursorAction action)
    {
        int index = (int)action;
        return index >= 0 && index < CursorActions.Length
            ? CursorActions[index]
            : CursorActions[(int)CursorAction.UseInteract];
    }
}
