using UnityEngine;

[DisallowMultipleComponent]
public class RoomAnchor : MonoBehaviour
{
    [SerializeField] private string anchorId;
    [SerializeField] private string roomId;

    public string AnchorId => string.IsNullOrWhiteSpace(anchorId) ? name : anchorId.Trim();
    public string RoomId => string.IsNullOrWhiteSpace(roomId) ? ResolveRoomIdFromHierarchy() : roomId.Trim();

    private void Reset()
    {
        RefreshFromHierarchy();
    }

    private void OnValidate()
    {
        RefreshFromHierarchy();
    }

    [ContextMenu("Refresh From Hierarchy")]
    public void RefreshFromHierarchy()
    {
        anchorId = name;
        roomId = ResolveRoomIdFromHierarchy();
    }

    private string ResolveRoomIdFromHierarchy()
    {
        RoomContentGroup roomContentGroup = GetComponentInParent<RoomContentGroup>(true);

        if (roomContentGroup != null)
        {
            return roomContentGroup.RoomName;
        }

        Transform current = transform.parent;

        while (current != null)
        {
            string objectName = current.name;

            if (!string.IsNullOrWhiteSpace(objectName) &&
                objectName.StartsWith("Room_", System.StringComparison.OrdinalIgnoreCase))
            {
                return objectName.Substring("Room_".Length).Replace('_', ' ').Trim();
            }

            current = current.parent;
        }

        return string.Empty;
    }
}
