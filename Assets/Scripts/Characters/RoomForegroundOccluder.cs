using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class RoomForegroundOccluder : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private Texture sourceTexture;
    [SerializeField] private Rect sourceUvRect = new Rect(0f, 0f, 1f, 1f);
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private bool disableRaycastTarget = true;

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

    private void ResolveReferences()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }
    }

    private void Apply()
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.texture = sourceTexture;
        targetImage.uvRect = ClampUvRect(sourceUvRect);
        targetImage.color = tint;

        if (disableRaycastTarget)
        {
            targetImage.raycastTarget = false;
        }
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
