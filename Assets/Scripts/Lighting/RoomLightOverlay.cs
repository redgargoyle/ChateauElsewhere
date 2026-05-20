using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(Image))]
public sealed class RoomLightOverlay : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private RoomLightAnimationStyle animationStyle = RoomLightAnimationStyle.SconceFlicker;
    [SerializeField] private Color color = new Color(1f, 0.72f, 0.34f, 1f);
    [SerializeField, Range(0f, 1f)] private float onAlpha = 0.32f;
    [SerializeField, Range(0f, 1f)] private float offAlpha;

    [Header("Animation")]
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.16f;
    [SerializeField, Range(0f, 1f)] private float driftAmount = 0.03f;
    [SerializeField] private float speed = 1f;
    [SerializeField] private float phase;

    private static Sprite sharedSoftLightSprite;

    private RectTransform rectTransform;
    private Image image;
    private float lightBlend = 1f;

    public void ApplyDefinition(RoomLightDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        CacheComponents();
        animationStyle = definition.animationStyle;
        color = definition.color;
        onAlpha = definition.onAlpha;
        offAlpha = definition.offAlpha;
        flickerAmount = definition.flickerAmount;
        driftAmount = definition.driftAmount;
        speed = definition.speed;
        phase = definition.phase;

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = definition.anchoredPosition;
        rectTransform.sizeDelta = definition.size;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, definition.rotationDegrees);
        rectTransform.localScale = Vector3.one;

        ConfigureImage();
        UpdateVisual(true);
    }

    public void SetLightBlend(float blend)
    {
        lightBlend = Mathf.Clamp01(blend);
    }

    private void OnEnable()
    {
        CacheComponents();
        ConfigureImage();
        UpdateVisual(true);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update += EditorUpdate;
        }
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
    }

    private void OnValidate()
    {
        CacheComponents();
        ConfigureImage();
        UpdateVisual(true);
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            UpdateVisual(false);
        }
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (this == null || Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        UpdateVisual(false);
        SceneView.RepaintAll();
    }
#endif

    private void CacheComponents()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (image == null)
        {
            image = GetComponent<Image>();
        }
    }

    private void ConfigureImage()
    {
        if (image == null)
        {
            return;
        }

        image.sprite = GetSoftLightSprite();
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
    }

    private void UpdateVisual(bool forceLayout)
    {
        if (image == null || rectTransform == null)
        {
            return;
        }

        LightFrame frame = EvaluateAnimation(GetPreviewTime());
        float alpha = Mathf.Lerp(offAlpha, onAlpha * frame.intensity, lightBlend);
        Color animatedColor = Color.Lerp(color, frame.tint, frame.tintBlend);
        animatedColor.a = Mathf.Clamp01(alpha);

        image.color = animatedColor;
        rectTransform.localScale = new Vector3(frame.scale.x, frame.scale.y, 1f);
    }

    private LightFrame EvaluateAnimation(float time)
    {
        float safeSpeed = Mathf.Max(0.01f, speed);
        float t = time * safeSpeed + phase;
        float flicker = Mathf.Clamp01(flickerAmount);
        float drift = Mathf.Clamp01(driftAmount);

        switch (animationStyle)
        {
            case RoomLightAnimationStyle.ChandelierBloom:
                return new LightFrame(
                    ClampAnimation(0.92f + 0.55f * drift * Wave(t * 0.58f)),
                    Vector2.one * (1f + 0.18f * drift * Wave(t * 0.58f)),
                    Color.white,
                    0.08f * drift);

            case RoomLightAnimationStyle.HearthBreath:
                return new LightFrame(
                    ClampAnimation(0.86f + 0.55f * drift * Wave(t * 0.75f) + 0.2f * flicker * Mathf.Sin(t * 4.7f)),
                    new Vector2(1f + 0.22f * drift * Wave(t * 0.75f), 1f + 0.1f * drift * Wave(t * 0.92f)),
                    new Color(1f, 0.33f, 0.12f, 1f),
                    0.28f * drift);

            case RoomLightAnimationStyle.WindowGlow:
                return new LightFrame(
                    ClampAnimation(0.78f + 0.35f * drift * Wave(t * 0.25f)),
                    new Vector2(1f + 0.18f * drift * Wave(t * 0.22f), 1f + 0.04f * drift * Wave(t * 0.39f)),
                    new Color(0.72f, 0.9f, 1f, 1f),
                    0.2f * drift);

            case RoomLightAnimationStyle.CandleCluster:
                float candle = Mathf.Sin(t * 7.1f) + 0.55f * Mathf.Sin(t * 13.7f + 1.7f) + 0.25f * Mathf.Sin(t * 23.3f);
                return new LightFrame(
                    ClampAnimation(0.9f + flicker * candle),
                    new Vector2(1f + 0.06f * flicker * Mathf.Sin(t * 9.3f), 1f + 0.08f * flicker * Mathf.Sin(t * 6.4f + 0.8f)),
                    new Color(1f, 0.48f, 0.18f, 1f),
                    0.18f * flicker);

            default:
                float sconce = 0.7f * Mathf.Sin(t * 3.7f) + 0.35f * Mathf.Sin(t * 8.1f + 0.8f);
                return new LightFrame(
                    ClampAnimation(0.94f + flicker * sconce),
                    Vector2.one * (1f + 0.05f * flicker * Mathf.Sin(t * 5.2f)),
                    new Color(1f, 0.62f, 0.28f, 1f),
                    0.12f * flicker);
        }
    }

    private static float GetPreviewTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return (float)EditorApplication.timeSinceStartup;
        }
#endif
        return Time.time;
    }

    private static float Wave(float value)
    {
        return 0.5f + 0.5f * Mathf.Sin(value);
    }

    private static float ClampAnimation(float value)
    {
        return Mathf.Clamp(value, 0.05f, 1.65f);
    }

    private static Sprite GetSoftLightSprite()
    {
        if (sharedSoftLightSprite != null)
        {
            return sharedSoftLightSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Generated_SoftRoomLight";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float center = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.12f, 1f, radius));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        sharedSoftLightSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return sharedSoftLightSprite;
    }

    private readonly struct LightFrame
    {
        public readonly float intensity;
        public readonly Vector2 scale;
        public readonly Color tint;
        public readonly float tintBlend;

        public LightFrame(float intensity, Vector2 scale, Color tint, float tintBlend)
        {
            this.intensity = intensity;
            this.scale = scale;
            this.tint = tint;
            this.tintBlend = tintBlend;
        }
    }
}
