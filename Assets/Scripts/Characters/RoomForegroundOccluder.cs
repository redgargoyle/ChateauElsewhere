using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class RoomForegroundOccluder : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private Texture sourceTexture;
    [SerializeField] private bool useRoomBackgroundTexture = true;
    [SerializeField] private bool autoCropFromRect = true;
    [SerializeField] private Rect sourceUvRect = new Rect(0f, 0f, 1f, 1f);
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private bool disableRaycastTarget = true;

    private RectTransform rectTransform;
    private RoomContentGroup roomContentGroup;

    public void Configure(Texture texture, bool cropFromRect)
    {
        Configure(texture, cropFromRect, cropFromRect ? sourceUvRect : new Rect(0f, 0f, 1f, 1f));
    }

    public void Configure(Texture texture, bool cropFromRect, Rect uvRect)
    {
        sourceTexture = texture;
        useRoomBackgroundTexture = cropFromRect;
        autoCropFromRect = cropFromRect;
        sourceUvRect = uvRect;
        ResolveReferences();
        Apply();
    }

    private void Reset()
    {
        ResolveReferences();
        Apply();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Apply();
    }

    private void OnValidate()
    {
        ResolveReferences();
        Apply();
    }

    private void LateUpdate()
    {
        if (autoCropFromRect)
        {
            Apply();
        }
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }

        if (roomContentGroup == null)
        {
            roomContentGroup = GetComponentInParent<RoomContentGroup>(true);
        }
    }

    private void Apply()
    {
        if (targetImage == null)
        {
            return;
        }

        Texture texture = ResolveSourceTexture();

        if (texture == null)
        {
            return;
        }

        Rect uvRect = autoCropFromRect ? GetUvRectFromRectTransform(texture) : sourceUvRect;

        targetImage.texture = texture;
        targetImage.uvRect = ClampUvRect(uvRect);
        targetImage.color = tint;

        if (disableRaycastTarget)
        {
            targetImage.raycastTarget = false;
        }
    }

    private Texture ResolveSourceTexture()
    {
        if (useRoomBackgroundTexture && roomContentGroup != null && roomContentGroup.RoomBackgroundTexture != null)
        {
            return roomContentGroup.RoomBackgroundTexture;
        }

        return sourceTexture;
    }

    private Rect GetUvRectFromRectTransform(Texture texture)
    {
        RectTransform roomRect = roomContentGroup != null ? roomContentGroup.transform as RectTransform : null;
        Vector2 roomSize = roomRect != null && roomRect.rect.size.sqrMagnitude > 1f
            ? roomRect.rect.size
            : new Vector2(texture.width, texture.height);

        if (rectTransform == null || roomSize.x <= 0f || roomSize.y <= 0f)
        {
            return sourceUvRect;
        }

        Vector2 size = rectTransform.rect.size;
        Vector2 anchoredPosition = rectTransform.anchoredPosition;
        Vector2 roomPivot = roomRect != null ? roomRect.pivot : new Vector2(0.5f, 0.5f);

        float localLeft = anchoredPosition.x - rectTransform.pivot.x * size.x;
        float localBottom = anchoredPosition.y - rectTransform.pivot.y * size.y;
        float uvX = (localLeft + roomPivot.x * roomSize.x) / roomSize.x;
        float uvY = (localBottom + roomPivot.y * roomSize.y) / roomSize.y;

        return new Rect(uvX, uvY, size.x / roomSize.x, size.y / roomSize.y);
    }

    private static Rect ClampUvRect(Rect rect)
    {
        float xMin = Mathf.Clamp01(rect.xMin);
        float yMin = Mathf.Clamp01(rect.yMin);
        float xMax = Mathf.Clamp01(rect.xMax);
        float yMax = Mathf.Clamp01(rect.yMax);

        return Rect.MinMaxRect(
            Mathf.Min(xMin, xMax),
            Mathf.Min(yMin, yMax),
            Mathf.Max(xMin, xMax),
            Mathf.Max(yMin, yMax));
    }
}
