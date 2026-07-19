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

    public const int DefaultStyleIndex = 9;

    private const string ResourceRoot = "UI/Cursors/styles";
    private static readonly string[] CursorActions =
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

    public static Texture2D LoadTexture(CursorAction action)
    {
        return Resources.Load<Texture2D>(GetCursorIconPath(action));
    }

    public static string GetCursorIconPath(CursorAction action)
    {
        return $"{ResourceRoot}/style_{DefaultStyleIndex:00}/{GetActionResourceName(action)}";
    }

    private static string GetActionResourceName(CursorAction action)
    {
        int index = (int)action;
        return index >= 0 && index < CursorActions.Length
            ? CursorActions[index]
            : CursorActions[(int)CursorAction.UseInteract];
    }
}
