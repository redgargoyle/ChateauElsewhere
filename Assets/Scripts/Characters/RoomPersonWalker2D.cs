using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class RoomPersonWalker2D : MonoBehaviour
{
    [SerializeField] private RawImage targetImage;
    [SerializeField] private Texture2D spriteAtlas;
    [SerializeField] [Min(1)] private int columns = 4;
    [SerializeField] [Min(1)] private int rows = 2;
    [SerializeField] [Min(0)] private int firstFrame;
    [SerializeField] [Min(1)] private int frameCount = 8;
    [SerializeField] [Min(0.01f)] private float secondsPerFrame = 0.12f;
    [SerializeField] private bool previewInEditMode = true;
    [SerializeField] private bool animateFrames = true;
    [SerializeField] private bool mirrorWhenWalkingLeft = true;
    [SerializeField] private Vector2[] pathPoints = new Vector2[0];
    [SerializeField] [Min(1f)] private float pixelsPerSecond = 95f;
    [SerializeField] private bool loopPath = true;
    [SerializeField] private bool pingPongPath;
    [Header("Painted-Room Depth")]
    [SerializeField] private float nearY = -360f;
    [SerializeField] private float farY = 150f;
    [SerializeField] [Min(0.01f)] private float nearScale = 1f;
    [SerializeField] [Min(0.01f)] private float farScale = 0.42f;
    [SerializeField] private Color nearTint = new Color(0.92f, 0.88f, 0.78f, 0.93f);
    [SerializeField] private Color farTint = new Color(0.70f, 0.72f, 0.66f, 0.72f);
    [SerializeField] private bool disableRaycastTarget = true;

    private RectTransform rectTransform;
    private int frameOffset;
    private float frameTimer;
    private int targetPathIndex = 1;
    private int pathDirection = 1;
    private Vector2 currentPosition;
    private int facingSign = 1;

#if UNITY_EDITOR
    private double lastEditorTime;
#endif

    private void Reset()
    {
        ResolveReferences();
        currentPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
    }

    private void Awake()
    {
        ResolveReferences();
        ResetPathPositionIfNeeded();
        ApplyVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResetPathPositionIfNeeded();
        ApplyVisuals();

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
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        frameCount = Mathf.Max(1, frameCount);
        secondsPerFrame = Mathf.Max(0.01f, secondsPerFrame);
        pixelsPerSecond = Mathf.Max(1f, pixelsPerSecond);
        nearScale = Mathf.Max(0.01f, nearScale);
        farScale = Mathf.Max(0.01f, farScale);
        ResolveReferences();
        ApplyVisuals();
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
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }
    }

    private void ResetPathPositionIfNeeded()
    {
        if (pathPoints == null || pathPoints.Length == 0)
        {
            currentPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
            return;
        }

        if (currentPosition == Vector2.zero || rectTransform == null)
        {
            currentPosition = pathPoints[0];
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = currentPosition;
        }

        targetPathIndex = Mathf.Clamp(targetPathIndex, 0, pathPoints.Length - 1);

        if (pathPoints.Length > 1 && targetPathIndex == 0)
        {
            targetPathIndex = 1;
        }
    }

    private void Tick(float deltaTime)
    {
        float safeDeltaTime = Mathf.Max(0f, deltaTime);

        if (animateFrames)
        {
            AdvanceFrame(safeDeltaTime);
        }

        AdvanceAlongPath(safeDeltaTime);
        ApplyVisuals();
    }

    private void AdvanceFrame(float deltaTime)
    {
        frameTimer += deltaTime;

        while (frameTimer >= secondsPerFrame)
        {
            frameTimer -= secondsPerFrame;
            frameOffset = (frameOffset + 1) % Mathf.Max(1, frameCount);
        }
    }

    private void AdvanceAlongPath(float deltaTime)
    {
        if (pathPoints == null || pathPoints.Length < 2 || rectTransform == null)
        {
            return;
        }

        float remainingDistance = pixelsPerSecond * deltaTime;

        while (remainingDistance > 0f)
        {
            Vector2 target = pathPoints[Mathf.Clamp(targetPathIndex, 0, pathPoints.Length - 1)];
            Vector2 toTarget = target - currentPosition;
            float distanceToTarget = toTarget.magnitude;

            if (distanceToTarget <= 0.01f)
            {
                AdvancePathTarget();
                continue;
            }

            if (Mathf.Abs(toTarget.x) > 0.01f)
            {
                facingSign = toTarget.x < 0f && mirrorWhenWalkingLeft ? -1 : 1;
            }

            float stepDistance = Mathf.Min(remainingDistance, distanceToTarget);
            currentPosition += toTarget / distanceToTarget * stepDistance;
            remainingDistance -= stepDistance;

            if (stepDistance >= distanceToTarget - 0.01f)
            {
                AdvancePathTarget();
            }
        }

        rectTransform.anchoredPosition = currentPosition;
    }

    private void AdvancePathTarget()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            return;
        }

        if (pingPongPath)
        {
            targetPathIndex += pathDirection;

            if (targetPathIndex >= pathPoints.Length)
            {
                pathDirection = -1;
                targetPathIndex = Mathf.Max(0, pathPoints.Length - 2);
            }
            else if (targetPathIndex < 0)
            {
                pathDirection = 1;
                targetPathIndex = Mathf.Min(1, pathPoints.Length - 1);
            }

            return;
        }

        targetPathIndex++;

        if (targetPathIndex >= pathPoints.Length)
        {
            targetPathIndex = loopPath ? 0 : pathPoints.Length - 1;
        }
    }

    private void ApplyVisuals()
    {
        ResolveReferences();

        if (targetImage != null)
        {
            targetImage.texture = spriteAtlas;
            targetImage.uvRect = GetFrameUvRect();
            targetImage.color = GetDepthTint();

            if (disableRaycastTarget)
            {
                targetImage.raycastTarget = false;
            }
        }

        if (rectTransform != null)
        {
            Vector3 scale = Vector3.one * GetDepthScale();
            scale.x *= facingSign;
            rectTransform.localScale = scale;
        }
    }

    private Rect GetFrameUvRect()
    {
        int totalFrames = Mathf.Max(1, columns * rows);
        int frame = (firstFrame + frameOffset) % totalFrames;
        int column = frame % columns;
        int rowFromTop = frame / columns;
        float width = 1f / columns;
        float height = 1f / rows;

        return new Rect(column * width, 1f - ((rowFromTop + 1) * height), width, height);
    }

    private float GetDepth01()
    {
        float y = rectTransform != null ? rectTransform.anchoredPosition.y : currentPosition.y;
        return Mathf.Clamp01(Mathf.InverseLerp(nearY, farY, y));
    }

    private float GetDepthScale()
    {
        return Mathf.Lerp(nearScale, farScale, GetDepth01());
    }

    private Color GetDepthTint()
    {
        return Color.Lerp(nearTint, farTint, GetDepth01());
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (Application.isPlaying || this == null)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Clamp((float)(now - lastEditorTime), 0f, 0.2f);
        lastEditorTime = now;

        if (previewInEditMode)
        {
            Tick(deltaTime);
            SceneView.RepaintAll();
            return;
        }

        ApplyVisuals();
    }
#endif
}
