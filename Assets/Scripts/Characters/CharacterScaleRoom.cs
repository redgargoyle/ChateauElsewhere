using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RoomContentGroup))]
[AddComponentMenu("Dreadforge/Characters/Character Scale Room")]
public sealed class CharacterScaleRoom : MonoBehaviour
{
    private const float MinimumStageScale = 0.0001f;

    [Header("Room")]
    [SerializeField] private RoomContentGroup room;

    [Header("Character Scale")]
    [Tooltip("Manual Front object. Its room-local X/Y is the guide position and its uniform local scale is the character size.")]
    [SerializeField] private Transform front;
    [Tooltip("Manual Back object. Its room-local X/Y is the guide position and its uniform local scale is the character size.")]
    [SerializeField] private Transform back;

    [SerializeField, HideInInspector] private float referenceStageScale = 1f;

    public string RoomName => room != null ? room.RoomName : string.Empty;
    public RoomContentGroup Room => room;
    public Transform Front => front;
    public Transform Back => back;
    public float ReferenceStageScale => referenceStageScale;

    private void Reset()
    {
        room = GetComponent<RoomContentGroup>();
        CaptureReferenceStageScale();
    }

    private void OnValidate()
    {
        if (room == null)
        {
            room = GetComponent<RoomContentGroup>();
        }

        referenceStageScale = Mathf.Max(MinimumStageScale, referenceStageScale);
    }

    public void Configure(
        RoomContentGroup roomContent,
        Transform frontObject,
        Transform backObject,
        float authoredStageScale)
    {
        room = roomContent != null ? roomContent : GetComponent<RoomContentGroup>();
        front = frontObject;
        back = backObject;
        referenceStageScale = Mathf.Max(MinimumStageScale, Mathf.Abs(authoredStageScale));
    }

    public void CaptureReferenceStageScale()
    {
        referenceStageScale = GetCurrentStageScale();
    }

    public bool TryEvaluateScale(Vector3 characterWorldPosition, out float scale)
    {
        scale = 1f;

        if (!IsConfigured(out _) || !TryGetCharacterRoomY(characterWorldPosition, out float characterRoomY))
        {
            return false;
        }

        return TryEvaluateScaleAtRoomY(characterRoomY, out scale);
    }

    public bool TryEvaluateScaleAtRoomY(float characterRoomY, out float scale)
    {
        scale = 1f;

        if (!IsConfigured(out _))
        {
            return false;
        }

        float authoredScale = CharacterScaleFunction.Evaluate(
            characterRoomY,
            GetRoomLocalPosition(front).y,
            GetUniformScale(front),
            GetRoomLocalPosition(back).y,
            GetUniformScale(back));

        // CameraManager owns room-stage zoom. This conversion keeps a detached
        // world-space animation display visually attached to that room surface
        // without giving the camera or movement systems character-scale ownership.
        float stageRatio = GetCurrentStageScale() / Mathf.Max(MinimumStageScale, referenceStageScale);
        scale = Mathf.Max(MinimumStageScale, authoredScale * stageRatio);
        return true;
    }

    private bool TryGetCharacterRoomY(Vector3 characterWorldPosition, out float characterRoomY)
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

        // This path supports world-space rooms and deterministic EditMode fixtures
        // that intentionally have no Canvas or render camera.
        if (room != null)
        {
            characterRoomY = room.transform.InverseTransformPoint(characterWorldPosition).y;
            return true;
        }

        return false;
    }

    public bool IsConfigured(out string reason)
    {
        if (room == null)
        {
            reason = "RoomContentGroup is missing.";
            return false;
        }

        if (front == null || back == null)
        {
            reason = "Front and Back objects are both required.";
            return false;
        }

        if (!front.IsChildOf(room.transform) || !back.IsChildOf(room.transform))
        {
            reason = "Front and Back must be children of this room.";
            return false;
        }

        float frontY = GetRoomLocalPosition(front).y;
        float backY = GetRoomLocalPosition(back).y;

        if (Mathf.Approximately(frontY, backY))
        {
            reason = "Front and Back need different Y positions.";
            return false;
        }

        if (front.localScale.x <= 0f ||
            front.localScale.y <= 0f ||
            back.localScale.x <= 0f ||
            back.localScale.y <= 0f)
        {
            reason = "Front and Back scales must be positive.";
            return false;
        }

        if (!IsUniformScale(front.localScale) || !IsUniformScale(back.localScale))
        {
            reason = "Front and Back must use a uniform X/Y scale.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public Vector2 GetRoomLocalPosition(Transform marker)
    {
        return marker == null || room == null
            ? Vector2.zero
            : (Vector2)room.transform.InverseTransformPoint(marker.position);
    }

    public float GetUniformScale(Transform marker)
    {
        return marker == null ? 1f : Mathf.Max(MinimumStageScale, Mathf.Abs(marker.localScale.x));
    }

    private float GetCurrentStageScale()
    {
        Transform roomTransform = room != null ? room.transform : transform;
        // CameraManager zooms the room stage by changing this local scale. Parent
        // CanvasScaler changes are deliberately excluded; screen resolution must
        // not become a second character-size input.
        return Mathf.Max(MinimumStageScale, Mathf.Abs(roomTransform.localScale.x));
    }

    private static bool IsUniformScale(Vector3 scale)
    {
        return Mathf.Approximately(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
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
}
