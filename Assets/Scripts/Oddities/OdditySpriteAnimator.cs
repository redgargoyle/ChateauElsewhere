using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class OdditySpriteAnimator : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite[] frames = new Sprite[0];
    [SerializeField] private bool playAnimation = true;
    [SerializeField] private bool previewInEditMode = true;
    [SerializeField] private bool pingPong = true;
    [SerializeField] [Min(0.01f)] private float frameDuration = 0.11f;
    [SerializeField] [Min(0f)] private float endHoldSeconds = 0.25f;
    [SerializeField] private bool randomizeStartFrame;
    [SerializeField] private bool preserveAspect = true;
    [SerializeField] private bool disableRaycastTarget = true;
    [SerializeField] private Color tint = new Color(0.86f, 0.76f, 0.62f, 0.86f);
    [SerializeField] [Range(0f, 0.2f)] private float alphaFlicker = 0.025f;
    [SerializeField] [Range(0f, 0.08f)] private float scalePulse;

    private int frameIndex;
    private int frameDirection = 1;
    private float frameTimer;
    private float holdTimer;
    private float baseTime;
    private Vector3 baseScale = Vector3.one;

#if UNITY_EDITOR
    private double lastEditorTime;
#endif

    private void Reset()
    {
        targetImage = GetComponent<Image>();
        CaptureBaseScale();
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseScale();
        ApplyCurrentFrame();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureBaseScale();
        ResetPlayback();
        ApplyCurrentFrame();

#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
        lastEditorTime = EditorApplication.timeSinceStartup;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    private void OnValidate()
    {
        frameDuration = Mathf.Max(0.01f, frameDuration);
        endHoldSeconds = Mathf.Max(0f, endHoldSeconds);
        ResolveReferences();
        CaptureBaseScale();
        ApplyCurrentFrame();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Tick(Time.unscaledDeltaTime);
    }

    private void ResolveReferences()
    {
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }
    }

    private void CaptureBaseScale()
    {
        baseScale = transform.localScale;

        if (baseScale == Vector3.zero)
        {
            baseScale = Vector3.one;
        }
    }

    private void ResetPlayback()
    {
        frameIndex = 0;
        frameDirection = 1;
        frameTimer = 0f;
        holdTimer = 0f;
        baseTime = Random.value * 100f;

        if (randomizeStartFrame && HasFrames())
        {
            frameIndex = Random.Range(0, frames.Length);
        }
    }

    private void Tick(float deltaTime)
    {
        if (!playAnimation || !HasFrames())
        {
            ApplyCurrentFrame();
            return;
        }

        if (!Application.isPlaying && !previewInEditMode)
        {
            ApplyCurrentFrame();
            return;
        }

        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        baseTime += safeDeltaTime;

        if (holdTimer > 0f)
        {
            holdTimer = Mathf.Max(0f, holdTimer - safeDeltaTime);
            ApplyCurrentFrame();
            return;
        }

        frameTimer += safeDeltaTime;
        float safeFrameDuration = Mathf.Max(0.01f, frameDuration);

        while (frameTimer >= safeFrameDuration)
        {
            frameTimer -= safeFrameDuration;
            AdvanceFrame();
        }

        ApplyCurrentFrame();
    }

    private void AdvanceFrame()
    {
        if (!HasFrames() || frames.Length == 1)
        {
            frameIndex = 0;
            return;
        }

        if (!pingPong)
        {
            frameIndex = (frameIndex + 1) % frames.Length;
            return;
        }

        int nextFrameIndex = frameIndex + frameDirection;

        if (nextFrameIndex >= frames.Length)
        {
            frameDirection = -1;
            nextFrameIndex = frames.Length - 2;
            holdTimer = endHoldSeconds;
        }
        else if (nextFrameIndex < 0)
        {
            frameDirection = 1;
            nextFrameIndex = 1;
            holdTimer = endHoldSeconds;
        }

        frameIndex = Mathf.Clamp(nextFrameIndex, 0, frames.Length - 1);
    }

    private void ApplyCurrentFrame()
    {
        if (targetImage == null)
        {
            return;
        }

        if (HasFrames())
        {
            frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
            targetImage.sprite = frames[frameIndex];
        }

        targetImage.preserveAspect = preserveAspect;

        if (disableRaycastTarget)
        {
            targetImage.raycastTarget = false;
        }

        float flicker = alphaFlicker > 0f
            ? (Mathf.PerlinNoise(baseTime * 2.7f, 0.37f) * 2f - 1f) * alphaFlicker
            : 0f;

        Color animatedTint = tint;
        animatedTint.a = Mathf.Clamp01(tint.a + flicker);
        targetImage.color = animatedTint;

        if (scalePulse > 0f)
        {
            float pulse = 1f + (Mathf.PerlinNoise(0.73f, baseTime * 1.4f) * 2f - 1f) * scalePulse;
            transform.localScale = baseScale * pulse;
        }
    }

    private bool HasFrames()
    {
        return frames != null && frames.Length > 0;
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (Application.isPlaying || this == null)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Clamp((float)(now - lastEditorTime), 0f, 0.25f);
        lastEditorTime = now;

        Tick(deltaTime);
        SceneView.RepaintAll();
    }
#endif
}
