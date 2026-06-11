using UnityEngine;

[DisallowMultipleComponent]
public class RoomAnchor : MonoBehaviour
{
    [SerializeField] private string anchorId;
    [SerializeField] private string roomId;
    [Header("Scene Gizmo")]
    [SerializeField] private bool showSceneGizmo;
    [SerializeField] private Color sceneGizmoColor = new Color(0.1f, 0.85f, 1f, 0.85f);
    [SerializeField, Min(0.01f)] private float sceneGizmoRadius = 0.25f;

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

    private void OnDrawGizmos()
    {
        if (!showSceneGizmo)
        {
            return;
        }

        float radius = Mathf.Max(0.01f, sceneGizmoRadius);
        Gizmos.color = sceneGizmoColor;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.DrawLine(transform.position + Vector3.left * radius, transform.position + Vector3.right * radius);
        Gizmos.DrawLine(transform.position + Vector3.down * radius, transform.position + Vector3.up * radius);
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
