using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RoomContentGroup))]
[AddComponentMenu("Dreadforge/Characters/Character Scale Room")]
public sealed class CharacterScaleRoom : MonoBehaviour
{
    private const float MinimumStageScale = 0.0001f;

    [Header("Runtime Room Coordinates")]
    [SerializeField] private RoomContentGroup room;

#if UNITY_EDITOR
    [Header("Editor-Only Calibration Handles")]
    [Tooltip("Editor handle only. Runtime scale never reads this Transform.")]
    [SerializeField] private Transform front;
    [Tooltip("Editor handle only. Runtime scale never reads this Transform.")]
    [SerializeField] private Transform back;
#endif

    public string RoomName => room != null ? room.RoomName : string.Empty;
    public RoomContentGroup Room => room;
    public float CurrentStageScale => GetCurrentStageScale();

#if UNITY_EDITOR
    public Transform FrontHandle => front;
    public Transform BackHandle => back;
#endif

    private void Reset()
    {
        room = GetComponent<RoomContentGroup>();
    }

    private void OnValidate()
    {
        if (room == null)
        {
            room = GetComponent<RoomContentGroup>();
        }
    }

    public bool TryGetCharacterRoomY(Vector3 characterWorldPosition, out float characterRoomY)
    {
        characterRoomY = 0f;
        RectTransform roomRect = room != null ? room.transform as RectTransform : null;
        Canvas canvas = roomRect != null ? roomRect.GetComponentInParent<Canvas>() : null;
        Camera worldCamera = Camera.main;

        // Gameplay characters are world-space SpriteRenderers while rooms are
        // RectTransforms on a screen-space canvas. Convert through screen space so
        // the shared Y function reads the same room-local coordinate artists edit.
        if (roomRect != null &&
            canvas != null &&
            worldCamera != null &&
            worldCamera.pixelWidth > 1 &&
            worldCamera.pixelHeight > 1)
        {
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(characterWorldPosition);

            if (screenPoint.z <= 0f)
            {
                return false;
            }

            Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera != null
                    ? canvas.worldCamera
                    : worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                roomRect,
                screenPoint,
                canvasCamera,
                out Vector2 roomLocalPoint))
            {
                characterRoomY = roomLocalPoint.y;
                return true;
            }
        }

        // Supports world-space rooms and deterministic EditMode fixtures that
        // intentionally have no Canvas or render camera.
        if (room != null)
        {
            characterRoomY = room.transform.InverseTransformPoint(characterWorldPosition).y;
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    public void ConfigureHandles(
        RoomContentGroup roomContent,
        Transform frontHandle,
        Transform backHandle)
    {
        room = roomContent != null ? roomContent : GetComponent<RoomContentGroup>();
        front = frontHandle;
        back = backHandle;
    }

    public bool AreHandlesConfigured(out string reason)
    {
        if (room == null)
        {
            reason = "RoomContentGroup is missing.";
            return false;
        }

        if (front == null || back == null)
        {
            reason = "Front and Back editor handles are both required.";
            return false;
        }

        if (!front.IsChildOf(room.transform) || !back.IsChildOf(room.transform))
        {
            reason = "Front and Back handles must be children of this room.";
            return false;
        }

        float frontY = GetHandleRoomLocalPosition(front).y;
        float backY = GetHandleRoomLocalPosition(back).y;

        if (!IsFinite(frontY) || !IsFinite(backY))
        {
            reason = "Front and Back handle Y positions must be finite.";
            return false;
        }

        if (Mathf.Approximately(frontY, backY))
        {
            reason = "Front and Back handles need different Y positions.";
            return false;
        }

        if (!IsFinite(front.localScale.x) ||
            !IsFinite(front.localScale.y) ||
            !IsFinite(back.localScale.x) ||
            !IsFinite(back.localScale.y) ||
            front.localScale.x <= 0f ||
            front.localScale.y <= 0f ||
            back.localScale.x <= 0f ||
            back.localScale.y <= 0f)
        {
            reason = "Front and Back handle X/Y scales must be finite and positive.";
            return false;
        }

        if (!Mathf.Approximately(front.localScale.x, front.localScale.y) ||
            !Mathf.Approximately(back.localScale.x, back.localScale.y))
        {
            reason = "Front and Back handles must use uniform X/Y scales.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public Vector2 GetHandleRoomLocalPosition(Transform marker)
    {
        return marker == null || room == null
            ? Vector2.zero
            : (Vector2)room.transform.InverseTransformPoint(marker.position);
    }

    public float GetHandleUniformScale(Transform marker)
    {
        return marker == null ? 1f : Mathf.Max(MinimumStageScale, Mathf.Abs(marker.localScale.x));
    }

    private void OnDrawGizmosSelected()
    {
        DrawMarker(front, new Color(0.95f, 0.35f, 0.2f, 1f));
        DrawMarker(back, new Color(0.2f, 0.65f, 1f, 1f));

        if (front != null && back != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            Gizmos.DrawLine(front.position, back.position);
        }
    }

    private static void DrawMarker(Transform marker, Color color)
    {
        if (marker == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(marker.position, 0.12f);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
#endif

    private float GetCurrentStageScale()
    {
        Transform roomTransform = room != null ? room.transform : transform;
        // CameraManager zooms the room stage by changing this local scale. Parent
        // CanvasScaler changes are deliberately excluded; screen resolution must
        // not become a second character-size input.
        return Mathf.Max(MinimumStageScale, Mathf.Abs(roomTransform.localScale.x));
    }
}
