using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(Image))]
public sealed class RoomLightOverlay : MonoBehaviour
{
    private RectTransform rectTransform;
    private Image image;
    private RoomLightDefinition definition;
    private RoomLightingController controller;

    public void Configure(RoomLightDefinition lightDefinition, RoomLightingController lightingController, Sprite softLightSprite)
    {
        definition = lightDefinition;
        controller = lightingController;

        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        image.sprite = softLightSprite;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;

        ApplyDefinitionLayout();
        UpdateVisual(true);
    }

    private void Update()
    {
        UpdateVisual(false);
    }

    private void ApplyDefinitionLayout()
    {
        if (definition == null || rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = definition.anchoredPosition;
        rectTransform.sizeDelta = definition.size;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, definition.rotationDegrees);
        rectTransform.localScale = Vector3.one;
    }

    private void UpdateVisual(bool forceLayout)
    {
        if (definition == null || controller == null || image == null)
        {
            return;
        }

        if (forceLayout || controller.LiveEditPreset)
        {
            ApplyDefinitionLayout();
        }

        float time = Application.isPlaying ? Time.time : 0f;
        float animation = EvaluateAnimation(time);
        float alpha = Mathf.Lerp(definition.offAlpha, definition.onAlpha * animation, controller.LightBlend);
        Color color = definition.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private float EvaluateAnimation(float time)
    {
        float speed = Mathf.Max(0.01f, definition.speed);
        float t = time * speed + definition.phase;

        switch (definition.animationStyle)
        {
            case RoomLightAnimationStyle.ChandelierBloom:
                return ClampAnimation(1f + 0.5f * definition.driftAmount * Mathf.Sin(t * 0.67f));

            case RoomLightAnimationStyle.HearthBreath:
                return ClampAnimation(1f + definition.flickerAmount * Mathf.Sin(t * 0.82f) + 0.35f * definition.driftAmount * Mathf.Sin(t * 1.9f));

            case RoomLightAnimationStyle.WindowGlow:
                return ClampAnimation(1f + definition.driftAmount * Mathf.Sin(t * 0.38f));

            case RoomLightAnimationStyle.CandleCluster:
                return ClampAnimation(1f + definition.flickerAmount * Mathf.Sin(t * 4.1f) + 0.45f * definition.flickerAmount * Mathf.Sin(t * 7.3f + 1.7f));

            default:
                return ClampAnimation(1f + definition.flickerAmount * Mathf.Sin(t * 2.4f) + 0.35f * definition.flickerAmount * Mathf.Sin(t * 5.7f + 0.8f));
        }
    }

    private static float ClampAnimation(float value)
    {
        return Mathf.Clamp(value, 0.15f, 1.35f);
    }
}
