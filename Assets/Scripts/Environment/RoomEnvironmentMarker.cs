using UnityEngine;

public enum RoomEnvironmentItemKind
{
    ForegroundOccluder,
    OverlayLight,
    TrueParticleFire,
    PrerenderedPatch,
    AuthoringNote
}

[DisallowMultipleComponent]
public sealed class RoomEnvironmentMarker : MonoBehaviour
{
    [SerializeField] private string roomName;
    [SerializeField] private RoomEnvironmentItemKind kind;
    [SerializeField] private string itemName;
    [SerializeField] private bool needsPolish = true;
    [SerializeField, TextArea(3, 8)] private string authoringNotes;

    public string RoomName => roomName;
    public RoomEnvironmentItemKind Kind => kind;
    public string ItemName => itemName;
    public bool NeedsPolish => needsPolish;
    public string AuthoringNotes => authoringNotes;

    public void Configure(
        string room,
        RoomEnvironmentItemKind itemKind,
        string item,
        string notes,
        bool polishNeeded = true)
    {
        roomName = room;
        kind = itemKind;
        itemName = item;
        authoringNotes = notes;
        needsPolish = polishNeeded;
    }
}
