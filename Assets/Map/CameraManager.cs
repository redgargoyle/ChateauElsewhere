using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CameraManager : MonoBehaviour
{
    public RawImage cameraBackground;
    public AudioSource cameraSwitchSound;
    public StaticNoisePlayer staticScreen;
    public bool playStaticOnCameraSwitch = true;
    public float staticTransitionDuration = 1f;
    public bool resizeBackgroundToScreen = true;
    public bool cropBackgroundToFill = true;
    public bool configureCanvasScaling = true;
    public Vector2 referenceResolution = new Vector2(1366f, 768f);
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;
    public float cameraShakeDuration = 1.6f;
    public float cameraShakeMagnitude = 22f;
    public float cameraShakeRotation = 2f;
    public float cameraShakeFrequency = 48f;
    public float cameraShakeZoom = 1.04f;
    [Header("Map Room Buttons")]
    public bool allowMapButtonsToSwitchCameraViews = true;
    public bool blinkMapButtonWhenClicked = true;
    [Header("Startup")]
    public bool useStartupCameraOnStart = true;
    public CameraAreaController startupCamera;
    public bool useAnchoredAnimationCameraAsStartup = true;
    [Header("Room Look")]
    public bool panRoomWithMouseEdges = true;
    [Range(0.01f, 0.5f)]
    public float mouseEdgePanZone = 0.12f;
    [Range(0f, 1f)]
    public float maxRoomPan = 0.55f;
    public float roomPanSpeed = 3.5f;
    public bool returnRoomPanToCenter;
    public bool moveRoomVerticallyWithMouseEdges;
    [Range(0f, 1f)]
    public float maxRoomVerticalPan = 1f;
    public bool invertVerticalRoomPan;
    public bool scrollRoomVerticallyWithMouseWheel = true;
    [Range(0f, 1f)]
    public float defaultRoomFov = 1f;
    [Range(0f, 1f)]
    public float minRoomFov = 0.55f;
    [Range(0f, 1f)]
    public float maxRoomFov = 1f;
    [Range(0.01f, 0.5f)]
    public float mouseScrollVerticalStep = 0.08f;
    public float roomVerticalScrollSpeed = 5f;
    [Header("Anchored Background Animation")]
    public bool enableAnchoredBackgroundAnimation = true;
    public CameraAreaController anchoredAnimationCamera;
    public StaticFrameGroup anchoredAnimationFrames;
    public RectTransform anchoredAnimationReference;
    public bool hideAnchoredAnimationReference = true;
    public bool pingPongAnchoredAnimation = true;
    public Shader anchoredAnimationCompositeShader;
    public Vector4 anchoredAnimationUvRect = new Vector4(0.78f, 0.35f, 0.18f, 0.33f);

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BackgroundTexId = Shader.PropertyToID("_background");
    private static readonly int BackgroundTexUpperId = Shader.PropertyToID("_Background");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int CameraAngleId = Shader.PropertyToID("_camera_angle");
    private static readonly int FovId = Shader.PropertyToID("_fov");
    private static readonly int MarginId = Shader.PropertyToID("_margin");
    private static readonly int VerticalStrengthId = Shader.PropertyToID("_verticle_strength");
    private static readonly int OverlayTexId = Shader.PropertyToID("_OverlayTex");
    private static readonly int OverlayRectId = Shader.PropertyToID("_OverlayRect");

    private CameraAreaController lastSelected;
    private Coroutine cameraSwitchRoutine;
    private Coroutine cameraShakeRoutine;
    private RectTransform activeShakeTarget;
    private Vector2 shakeBasePosition;
    private Quaternion shakeBaseRotation;
    private Vector3 shakeBaseScale;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float currentRoomPan;
    private float targetRoomPan;
    private float currentRoomVerticalPan;
    private float targetRoomVerticalPan;
    private float currentRoomFov = 1f;
    private Texture currentBaseBackgroundTexture;
    private RenderTexture anchoredAnimationTexture;
    private Material anchoredAnimationCompositeMaterial;
    private RoomNavigationManager roomNavigationManager;
    private int anchoredAnimationFrameIndex = -1;
    private int anchoredAnimationFrameDirection = 1;
    private float anchoredAnimationFrameTimer;
    private Rect lastAnchoredAnimationRect = new Rect(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
    private readonly Vector3[] anchoredReferenceCorners = new Vector3[4];
    private readonly Vector3[] shaderAnchorCorners = new Vector3[4];

    public float CurrentRoomHorizontalPan => currentRoomPan;
    public float CurrentRoomVerticalPan => currentRoomVerticalPan;
    public float CurrentRoomFov => currentRoomFov;

    private void Reset()
    {
        cameraBackground = FindObjectOfType<RawImage>();
        cameraSwitchSound = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (cameraBackground == null)
        {
            cameraBackground = FindObjectOfType<RawImage>();
        }

        if (cameraSwitchSound == null)
        {
            cameraSwitchSound = GetComponent<AudioSource>();
        }

        InitializeRoomLook();
        ConfigureCanvasScalers();
        HideAnchoredAnimationReferenceVisuals();
        ApplyBackgroundLayout();
    }

    private void Start()
    {
        if (cameraBackground == null)
        {
            Debug.LogError("CameraManager needs a Camera Background RawImage assigned.", this);
            enabled = false;
            return;
        }

        SetCameraBackground(GetStartupBackgroundTexture());
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyBackgroundLayout();
        }

        UpdateRoomLookFromInput();
        UpdateAnchoredBackgroundAnimation();
    }

    public void SelectCamera(CameraAreaController selected)
    {
        if (selected == null || cameraBackground == null)
        {
            return;
        }

        Texture texture = selected.roomBackgroundTexture;

        if (texture == null)
        {
            Debug.LogWarning("Selected camera has no room background texture assigned.", selected);
            return;
        }

        if (lastSelected != null && lastSelected != selected)
        {
            lastSelected.StopBlinking();
        }

        if (blinkMapButtonWhenClicked)
        {
            selected.StartBlinking();
        }

        lastSelected = selected;

        if (!allowMapButtonsToSwitchCameraViews)
        {
            return;
        }

        NotifyRoomNavigation(selected);

        if (cameraSwitchSound != null)
        {
            cameraSwitchSound.Play();
        }

        if (cameraSwitchRoutine != null)
        {
            StopCoroutine(cameraSwitchRoutine);
        }

        if (playStaticOnCameraSwitch && staticScreen != null && staticScreen.CanPlay && staticTransitionDuration > 0f)
        {
            cameraSwitchRoutine = StartCoroutine(SwitchCameraAfterStatic(texture));
        }
        else
        {
            if (staticScreen != null)
            {
                staticScreen.Stop();
            }

            SetCameraBackground(texture);
        }
    }

    private IEnumerator SwitchCameraAfterStatic(Texture texture)
    {
        staticScreen.Play();

        yield return new WaitForSecondsRealtime(staticTransitionDuration);

        SetCameraBackground(texture);
        staticScreen.Stop();
        cameraSwitchRoutine = null;
    }

    public void CancelCameraSwitchTransition()
    {
        if (cameraSwitchRoutine != null)
        {
            StopCoroutine(cameraSwitchRoutine);
            cameraSwitchRoutine = null;
        }

        if (staticScreen != null)
        {
            staticScreen.Stop();
        }
    }

    public void PlayCameraShake()
    {
        PlayCameraShake(cameraShakeDuration);
    }

    public void PlayCameraShake(float duration)
    {
        PlayCameraShake(duration, cameraShakeMagnitude, cameraShakeRotation, cameraShakeFrequency, cameraShakeZoom);
    }

    public void PlayCameraShake(float duration, float magnitude, float rotation, float frequency, float zoom)
    {
        if (cameraBackground == null)
        {
            return;
        }

        RectTransform target = cameraBackground.rectTransform;

        if (target == null)
        {
            return;
        }

        StopCameraShake();
        cameraShakeRoutine = StartCoroutine(ShakeCameraView(target, duration, magnitude, rotation, frequency, zoom));
    }

    public void StopCameraShake()
    {
        if (cameraShakeRoutine != null)
        {
            StopCoroutine(cameraShakeRoutine);
            cameraShakeRoutine = null;
        }

        RestoreCameraShakeTarget();
    }

    public void SetRoomBackground(Texture texture)
    {
        if (texture == null || cameraBackground == null)
        {
            return;
        }

        SetCameraBackground(texture);
    }

    public void PreviewRoomBackground(Texture texture)
    {
        if (texture == null || cameraBackground == null)
        {
            return;
        }

        // Editor tools use the same background assignment path as Play mode so
        // the RawImage, crop UVs, and shader texture properties all match what
        // the player will actually see.
        SetCameraBackground(texture);
        MarkBackgroundPreviewDirty();
    }

    public void SetRoomLookForPreview(float horizontalPan, float verticalPan, float fov)
    {
        // These values are normally driven by runtime mouse input. The door
        // editing tools call this in Edit mode so we can inspect the same shader
        // panning pipeline without pressing Play.
        currentRoomPan = ClampHorizontalRoomPan(horizontalPan);
        targetRoomPan = currentRoomPan;
        currentRoomVerticalPan = ClampVerticalRoomPan(verticalPan);
        targetRoomVerticalPan = currentRoomVerticalPan;
        currentRoomFov = ClampRoomFov(fov);

        ApplyRoomLookToMaterial();
        MarkBackgroundPreviewDirty();
    }

    public void ResetRoomLookForPreview()
    {
        SetRoomLookForPreview(0f, 0f, defaultRoomFov);
    }

    public bool TryCaptureShaderAnchoredRect(RectTransform sourceRect, out Rect shaderUvRect)
    {
        return TryCaptureShaderAnchoredRect(
            sourceRect,
            out shaderUvRect,
            ClampHorizontalRoomPan(currentRoomPan),
            currentRoomFov,
            ClampVerticalRoomPan(currentRoomVerticalPan));
    }

    public bool TryCaptureDefaultShaderAnchoredRect(RectTransform sourceRect, out Rect shaderUvRect)
    {
        return TryCaptureShaderAnchoredRect(
            sourceRect,
            out shaderUvRect,
            0f,
            ClampRoomFov(defaultRoomFov),
            0f);
    }

    private bool TryCaptureShaderAnchoredRect(
        RectTransform sourceRect,
        out Rect shaderUvRect,
        float cameraAngle,
        float fov,
        float verticalStrength)
    {
        shaderUvRect = new Rect();

        if (sourceRect == null || cameraBackground == null)
        {
            return false;
        }

        RectTransform backgroundTransform = cameraBackground.rectTransform;

        if (backgroundTransform == null || !BackgroundRectIsUsable(backgroundTransform))
        {
            return false;
        }

        sourceRect.GetWorldCorners(shaderAnchorCorners);

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < shaderAnchorCorners.Length; i++)
        {
            Vector2 backgroundLocalPoint = backgroundTransform.InverseTransformPoint(shaderAnchorCorners[i]);
            Vector2 meshUv = BackgroundLocalPointToMeshUv(backgroundTransform, backgroundLocalPoint);
            Vector2 shaderUv = MeshUvToShaderUv(meshUv, cameraAngle, fov, verticalStrength);

            min = Vector2.Min(min, shaderUv);
            max = Vector2.Max(max, shaderUv);
        }

        shaderUvRect = NormalizeRect(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        return shaderUvRect.width > 0f && shaderUvRect.height > 0f;
    }

    public bool TryApplyShaderAnchoredRect(RectTransform targetRect, Rect shaderUvRect)
    {
        if (targetRect == null || cameraBackground == null || targetRect.parent == null)
        {
            return false;
        }

        RectTransform backgroundTransform = cameraBackground.rectTransform;
        RectTransform parentRect = targetRect.parent as RectTransform;

        if (backgroundTransform == null || parentRect == null || !BackgroundRectIsUsable(backgroundTransform))
        {
            return false;
        }

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        Vector2 shaderMin = shaderUvRect.min;
        Vector2 shaderMax = shaderUvRect.max;

        Vector2[] shaderCorners =
        {
            new Vector2(shaderMin.x, shaderMin.y),
            new Vector2(shaderMin.x, shaderMax.y),
            new Vector2(shaderMax.x, shaderMax.y),
            new Vector2(shaderMax.x, shaderMin.y)
        };

        for (int i = 0; i < shaderCorners.Length; i++)
        {
            Vector2 meshUv = ShaderUvToMeshUv(shaderCorners[i]);
            Vector2 backgroundLocalPoint = MeshUvToBackgroundLocalPoint(backgroundTransform, meshUv);
            Vector3 worldPoint = backgroundTransform.TransformPoint(backgroundLocalPoint);
            Vector2 parentLocalPoint = parentRect.InverseTransformPoint(worldPoint);

            min = Vector2.Min(min, parentLocalPoint);
            max = Vector2.Max(max, parentLocalPoint);
        }

        Vector2 size = max - min;

        if (size.x <= 0f || size.y <= 0f)
        {
            return false;
        }

        Vector2 center = (min + max) * 0.5f;
        Vector2 anchor = new Vector2(0.5f, 0.5f);
        Vector2 parentSize = parentRect.rect.size;
        Vector2 anchorReference = new Vector2(
            (anchor.x - parentRect.pivot.x) * parentSize.x,
            (anchor.y - parentRect.pivot.y) * parentSize.y);

        // The hitbox is a normal UI object, but its position comes from the same
        // shader-space coordinates that the background image uses. That is the
        // important bridge: the shader moves the picture; this moves the raycast
        // rectangle to the picture's new on-screen position.
        targetRect.anchorMin = anchor;
        targetRect.anchorMax = anchor;
        targetRect.pivot = new Vector2(0.5f, 0.5f);
        targetRect.anchoredPosition = center - anchorReference;
        targetRect.sizeDelta = size;
        targetRect.localRotation = Quaternion.identity;
        targetRect.localScale = Vector3.one;
        return true;
    }

    private Texture GetStartupBackgroundTexture()
    {
        if (!useStartupCameraOnStart)
        {
            return cameraBackground.texture;
        }

        if (startupCamera != null && startupCamera.roomBackgroundTexture != null)
        {
            return startupCamera.roomBackgroundTexture;
        }

        if (useAnchoredAnimationCameraAsStartup &&
            anchoredAnimationCamera != null &&
            anchoredAnimationCamera.roomBackgroundTexture != null)
        {
            return anchoredAnimationCamera.roomBackgroundTexture;
        }

        return cameraBackground.texture;
    }

    private void NotifyRoomNavigation(CameraAreaController selected)
    {
        if (selected == null)
        {
            return;
        }

        if (roomNavigationManager == null)
        {
            roomNavigationManager = FindObjectOfType<RoomNavigationManager>();
        }

        if (roomNavigationManager != null)
        {
            roomNavigationManager.SetCurrentRoomFromCameraArea(selected, false);
        }
    }

    private void SetCameraBackground(Texture texture)
    {
        if (currentBaseBackgroundTexture != texture)
        {
            ResetAnchoredAnimationPlayback();
        }

        currentBaseBackgroundTexture = texture;
        cameraBackground.texture = texture;
        ApplyBackgroundLayout();

        Texture displayTexture = GetBackgroundDisplayTexture(texture);

        ApplyCameraBackgroundTexture(displayTexture);
    }

    private void ApplyCameraBackgroundTexture(Texture texture)
    {
        cameraBackground.texture = texture;
        ApplyBackgroundLayout();

        Material material = cameraBackground.material;

        if (material != null)
        {
            SetTextureIfAvailable(material, MainTexId, texture);
            SetTextureIfAvailable(material, BackgroundTexId, texture);
            SetTextureIfAvailable(material, BackgroundTexUpperId, texture);
            SetTextureIfAvailable(material, BaseMapId, texture);
            ApplyRoomLookToMaterial(material);
        }

        cameraBackground.SetMaterialDirty();
        cameraBackground.SetVerticesDirty();
    }

    private Texture GetBackgroundDisplayTexture(Texture texture)
    {
        if (!ShouldUseAnchoredAnimation(texture))
        {
            return texture;
        }

        Texture compositedTexture = BuildAnchoredAnimationTexture(texture);

        return compositedTexture != null ? compositedTexture : texture;
    }

    private bool ShouldUseAnchoredAnimation(Texture texture)
    {
        if (!enableAnchoredBackgroundAnimation || texture == null ||
            anchoredAnimationFrames == null || !anchoredAnimationFrames.IsValid())
        {
            return false;
        }

        return anchoredAnimationCamera == null ||
            anchoredAnimationCamera.roomBackgroundTexture == texture;
    }

    private void UpdateAnchoredBackgroundAnimation()
    {
        if (!ShouldUseAnchoredAnimation(currentBaseBackgroundTexture))
        {
            return;
        }

        if (anchoredAnimationFrameIndex < 0)
        {
            anchoredAnimationFrameIndex = 0;
        }

        float frameDuration = Mathf.Max(0.001f, anchoredAnimationFrames.frameDuration);
        anchoredAnimationFrameTimer += Time.unscaledDeltaTime;
        bool shouldRebuild = false;

        while (anchoredAnimationFrameTimer >= frameDuration)
        {
            anchoredAnimationFrameTimer -= frameDuration;
            AdvanceAnchoredAnimationFrame();
            shouldRebuild = true;
        }

        Rect currentRect = GetAnchoredAnimationRect();

        if (!RectsApproximatelyEqual(currentRect, lastAnchoredAnimationRect))
        {
            shouldRebuild = true;
        }

        if (!shouldRebuild)
        {
            return;
        }

        Texture displayTexture = BuildAnchoredAnimationTexture(currentBaseBackgroundTexture);

        if (displayTexture != null)
        {
            ApplyCameraBackgroundTexture(displayTexture);
        }
    }

    private void AdvanceAnchoredAnimationFrame()
    {
        int frameCount = anchoredAnimationFrames.frames.Length;

        if (!pingPongAnchoredAnimation || frameCount <= 2)
        {
            anchoredAnimationFrameIndex = (anchoredAnimationFrameIndex + 1) % frameCount;
            return;
        }

        if (anchoredAnimationFrameDirection == 0)
        {
            anchoredAnimationFrameDirection = 1;
        }

        int nextFrameIndex = anchoredAnimationFrameIndex + anchoredAnimationFrameDirection;

        if (nextFrameIndex >= frameCount)
        {
            anchoredAnimationFrameDirection = -1;
            nextFrameIndex = frameCount - 2;
        }
        else if (nextFrameIndex < 0)
        {
            anchoredAnimationFrameDirection = 1;
            nextFrameIndex = 1;
        }

        anchoredAnimationFrameIndex = Mathf.Clamp(nextFrameIndex, 0, frameCount - 1);
    }

    private void ResetAnchoredAnimationPlayback()
    {
        anchoredAnimationFrameIndex = -1;
        anchoredAnimationFrameDirection = 1;
        anchoredAnimationFrameTimer = 0f;
        lastAnchoredAnimationRect = new Rect(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
    }

    private Texture BuildAnchoredAnimationTexture(Texture baseTexture)
    {
        if (baseTexture == null)
        {
            return null;
        }

        Material compositeMaterial = GetAnchoredAnimationCompositeMaterial();

        if (compositeMaterial == null)
        {
            return null;
        }

        if (anchoredAnimationFrameIndex < 0)
        {
            anchoredAnimationFrameIndex = 0;
        }

        Texture2D frame = anchoredAnimationFrames.GetFrame(anchoredAnimationFrameIndex);

        if (frame == null)
        {
            return null;
        }

        EnsureAnchoredAnimationTexture(baseTexture.width, baseTexture.height);

        if (anchoredAnimationTexture == null)
        {
            return null;
        }

        Rect overlayRect = GetAnchoredAnimationRect();
        lastAnchoredAnimationRect = overlayRect;
        compositeMaterial.SetTexture(OverlayTexId, frame);
        compositeMaterial.SetVector(OverlayRectId, RectToVector(overlayRect));

        Graphics.Blit(baseTexture, anchoredAnimationTexture, compositeMaterial);

        return anchoredAnimationTexture;
    }

    private Material GetAnchoredAnimationCompositeMaterial()
    {
        if (anchoredAnimationCompositeMaterial != null)
        {
            return anchoredAnimationCompositeMaterial;
        }

        Shader shader = anchoredAnimationCompositeShader;

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Dreadforge/AnchoredOverlayComposite");
        }

        if (shader == null)
        {
            Debug.LogWarning("CameraManager could not find the anchored overlay composite shader.", this);
            return null;
        }

        anchoredAnimationCompositeMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        return anchoredAnimationCompositeMaterial;
    }

    private void EnsureAnchoredAnimationTexture(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            ReleaseAnchoredAnimationTexture();
            return;
        }

        if (anchoredAnimationTexture != null &&
            anchoredAnimationTexture.width == width &&
            anchoredAnimationTexture.height == height)
        {
            return;
        }

        ReleaseAnchoredAnimationTexture();

        anchoredAnimationTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = "Anchored Background Animation",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };

        anchoredAnimationTexture.Create();
    }

    private Rect GetAnchoredAnimationRect()
    {
        if (anchoredAnimationReference == null || cameraBackground == null)
        {
            return VectorToRect(anchoredAnimationUvRect);
        }

        RectTransform backgroundTransform = cameraBackground.rectTransform;

        if (backgroundTransform == null)
        {
            return VectorToRect(anchoredAnimationUvRect);
        }

        anchoredAnimationReference.GetWorldCorners(anchoredReferenceCorners);

        Rect backgroundRect = backgroundTransform.rect;

        if (backgroundRect.width <= 0f || backgroundRect.height <= 0f)
        {
            return VectorToRect(anchoredAnimationUvRect);
        }

        Vector2 localMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 localMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < anchoredReferenceCorners.Length; i++)
        {
            Vector3 localPoint = backgroundTransform.InverseTransformPoint(anchoredReferenceCorners[i]);
            localMin = Vector2.Min(localMin, localPoint);
            localMax = Vector2.Max(localMax, localPoint);
        }

        Rect visibleUv = cameraBackground.uvRect;
        float normalizedXMin = Mathf.InverseLerp(backgroundRect.xMin, backgroundRect.xMax, localMin.x);
        float normalizedXMax = Mathf.InverseLerp(backgroundRect.xMin, backgroundRect.xMax, localMax.x);
        float normalizedYMin = Mathf.InverseLerp(backgroundRect.yMin, backgroundRect.yMax, localMin.y);
        float normalizedYMax = Mathf.InverseLerp(backgroundRect.yMin, backgroundRect.yMax, localMax.y);

        Rect rect = new Rect(
            visibleUv.x + normalizedXMin * visibleUv.width,
            visibleUv.y + normalizedYMin * visibleUv.height,
            (normalizedXMax - normalizedXMin) * visibleUv.width,
            (normalizedYMax - normalizedYMin) * visibleUv.height);

        return NormalizeRect(rect);
    }

    private Rect NormalizeRect(Rect rect)
    {
        if (rect.width < 0f)
        {
            rect.x += rect.width;
            rect.width = -rect.width;
        }

        if (rect.height < 0f)
        {
            rect.y += rect.height;
            rect.height = -rect.height;
        }

        return rect;
    }

    private bool RectsApproximatelyEqual(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x) &&
            Mathf.Approximately(a.y, b.y) &&
            Mathf.Approximately(a.width, b.width) &&
            Mathf.Approximately(a.height, b.height);
    }

    private Vector4 RectToVector(Rect rect)
    {
        return new Vector4(rect.x, rect.y, rect.width, rect.height);
    }

    private Rect VectorToRect(Vector4 vector)
    {
        return NormalizeRect(new Rect(vector.x, vector.y, vector.z, vector.w));
    }

    private void HideAnchoredAnimationReferenceVisuals()
    {
        if (!hideAnchoredAnimationReference || anchoredAnimationReference == null)
        {
            return;
        }

        StaticSetImagePlayer imagePlayer = anchoredAnimationReference.GetComponent<StaticSetImagePlayer>();

        if (imagePlayer != null)
        {
            imagePlayer.enabled = false;
        }

        Graphic[] graphics = anchoredAnimationReference.GetComponents<Graphic>();

        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].enabled = false;
        }

        Renderer[] renderers = anchoredAnimationReference.GetComponents<Renderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }
    }

    private void OnDestroy()
    {
        ReleaseAnchoredAnimationTexture();

        if (anchoredAnimationCompositeMaterial != null)
        {
            Destroy(anchoredAnimationCompositeMaterial);
            anchoredAnimationCompositeMaterial = null;
        }
    }

    private void ReleaseAnchoredAnimationTexture()
    {
        if (anchoredAnimationTexture == null)
        {
            return;
        }

        anchoredAnimationTexture.Release();
        Destroy(anchoredAnimationTexture);
        anchoredAnimationTexture = null;
    }

    private void InitializeRoomLook()
    {
        currentRoomFov = ClampRoomFov(defaultRoomFov);
    }

    private void UpdateRoomLookFromInput()
    {
        if (cameraBackground == null)
        {
            return;
        }

        bool shouldApply = false;

        if (panRoomWithMouseEdges)
        {
            targetRoomPan = GetRoomHorizontalPanTargetFromMouse();
            currentRoomPan = ClampHorizontalRoomPan(MoveValue(currentRoomPan, targetRoomPan, roomPanSpeed));
            shouldApply = true;
        }

        if (moveRoomVerticallyWithMouseEdges)
        {
            targetRoomVerticalPan = GetRoomVerticalPanTargetFromMouse();
            currentRoomVerticalPan = ClampVerticalRoomPan(MoveValue(currentRoomVerticalPan, targetRoomVerticalPan, roomPanSpeed));
            shouldApply = true;
        }

        if (scrollRoomVerticallyWithMouseWheel)
        {
            float scrollDelta = Input.mouseScrollDelta.y;

            if (!Mathf.Approximately(scrollDelta, 0f))
            {
                float direction = invertVerticalRoomPan ? -1f : 1f;
                targetRoomVerticalPan = ClampVerticalRoomPan(targetRoomVerticalPan + scrollDelta * mouseScrollVerticalStep * direction);
            }

            currentRoomVerticalPan = ClampVerticalRoomPan(MoveValue(currentRoomVerticalPan, targetRoomVerticalPan, roomVerticalScrollSpeed));
            shouldApply = true;
        }
        else if (!moveRoomVerticallyWithMouseEdges &&
            (!Mathf.Approximately(currentRoomVerticalPan, 0f) || !Mathf.Approximately(targetRoomVerticalPan, 0f)))
        {
            targetRoomVerticalPan = 0f;
            currentRoomVerticalPan = 0f;
            shouldApply = true;
        }

        if (shouldApply)
        {
            ApplyRoomLookToMaterial();
        }
    }

    private float GetRoomHorizontalPanTargetFromMouse()
    {
        if (!TryGetMousePositionOnScreen(out Vector3 mousePosition))
        {
            return returnRoomPanToCenter ? 0f : currentRoomPan;
        }

        float edgePixels = Mathf.Max(1f, Screen.width * Mathf.Clamp(mouseEdgePanZone, 0.01f, 0.5f));
        float leftAmount = Mathf.Clamp01((edgePixels - mousePosition.x) / edgePixels);
        float rightAmount = Mathf.Clamp01((mousePosition.x - (Screen.width - edgePixels)) / edgePixels);
        float pan = rightAmount - leftAmount;

        if (Mathf.Approximately(pan, 0f) && !returnRoomPanToCenter)
        {
            return currentRoomPan;
        }

        return ClampHorizontalRoomPan(pan);
    }

    private float GetRoomVerticalPanTargetFromMouse()
    {
        if (!TryGetMousePositionOnScreen(out Vector3 mousePosition))
        {
            return returnRoomPanToCenter ? 0f : targetRoomVerticalPan;
        }

        float edgePixels = Mathf.Max(1f, Screen.height * Mathf.Clamp(mouseEdgePanZone, 0.01f, 0.5f));
        float bottomAmount = Mathf.Clamp01((edgePixels - mousePosition.y) / edgePixels);
        float topAmount = Mathf.Clamp01((mousePosition.y - (Screen.height - edgePixels)) / edgePixels);
        float pan = topAmount - bottomAmount;

        if (invertVerticalRoomPan)
        {
            pan = -pan;
        }

        if (Mathf.Approximately(pan, 0f) && !returnRoomPanToCenter)
        {
            return targetRoomVerticalPan;
        }

        return ClampVerticalRoomPan(pan);
    }

    private bool TryGetMousePositionOnScreen(out Vector3 mousePosition)
    {
        mousePosition = Input.mousePosition;

        return Screen.width > 0 &&
            Screen.height > 0 &&
            mousePosition.x >= 0f &&
            mousePosition.x <= Screen.width &&
            mousePosition.y >= 0f &&
            mousePosition.y <= Screen.height;
    }

    private float MoveValue(float currentValue, float targetValue, float speed)
    {
        float safeSpeed = Mathf.Max(0f, speed);

        if (safeSpeed <= 0f)
        {
            return targetValue;
        }

        return Mathf.MoveTowards(currentValue, targetValue, safeSpeed * Time.unscaledDeltaTime);
    }

    private float ClampRoomFov(float fov)
    {
        float minFov = Mathf.Min(minRoomFov, maxRoomFov);
        float maxFov = Mathf.Max(minRoomFov, maxRoomFov);

        return Mathf.Clamp(fov, minFov, maxFov);
    }

    private float ClampHorizontalRoomPan(float pan)
    {
        float safePanLimit = Mathf.Clamp01(maxRoomPan);

        return Mathf.Clamp(pan, -safePanLimit, safePanLimit);
    }

    private float ClampVerticalRoomPan(float pan)
    {
        float safePanLimit = Mathf.Clamp01(maxRoomVerticalPan);

        return Mathf.Clamp(pan, -safePanLimit, safePanLimit);
    }

    private void ApplyRoomLookToMaterial()
    {
        Material material = cameraBackground != null ? cameraBackground.material : null;

        if (material == null)
        {
            return;
        }

        ApplyRoomLookToMaterial(material);
        cameraBackground.SetMaterialDirty();
    }

    private void ApplyRoomLookToMaterial(Material material)
    {
        SetFloatIfAvailable(material, CameraAngleId, ClampHorizontalRoomPan(currentRoomPan));
        SetFloatIfAvailable(material, FovId, currentRoomFov);
        SetFloatIfAvailable(material, VerticalStrengthId, ClampVerticalRoomPan(currentRoomVerticalPan));
    }

    private IEnumerator ShakeCameraView(RectTransform target, float duration, float magnitude, float rotation, float frequency, float zoom)
    {
        activeShakeTarget = target;
        shakeBasePosition = target.anchoredPosition;
        shakeBaseRotation = target.localRotation;
        shakeBaseScale = target.localScale;

        float safeDuration = Mathf.Max(0.01f, duration);
        float safeFrequency = Mathf.Max(0.01f, frequency);
        float safeZoom = Mathf.Max(1f, zoom);
        float seed = Random.value * 100f;
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float strength = 1f - Mathf.Clamp01(elapsed / safeDuration);
            float noiseTime = Time.unscaledTime * safeFrequency;
            float offsetX = (Mathf.PerlinNoise(seed, noiseTime) * 2f - 1f) * magnitude * strength;
            float offsetY = (Mathf.PerlinNoise(seed + 31f, noiseTime) * 2f - 1f) * magnitude * strength;
            float angle = (Mathf.PerlinNoise(seed + 67f, noiseTime) * 2f - 1f) * rotation * strength;

            target.anchoredPosition = shakeBasePosition + new Vector2(offsetX, offsetY);
            target.localRotation = shakeBaseRotation * Quaternion.Euler(0f, 0f, angle);
            target.localScale = shakeBaseScale * Mathf.Lerp(1f, safeZoom, strength);

            yield return null;
        }

        RestoreCameraShakeTarget();
        cameraShakeRoutine = null;
    }

    private void RestoreCameraShakeTarget()
    {
        if (activeShakeTarget == null)
        {
            return;
        }

        activeShakeTarget.anchoredPosition = shakeBasePosition;
        activeShakeTarget.localRotation = shakeBaseRotation;
        activeShakeTarget.localScale = shakeBaseScale;
        activeShakeTarget = null;
    }

    private void ConfigureCanvasScalers()
    {
        if (!configureCanvasScaling)
        {
            return;
        }

        CanvasScaler[] canvasScalers = FindObjectsOfType<CanvasScaler>();

        foreach (CanvasScaler canvasScaler in canvasScalers)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = referenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = matchWidthOrHeight;

            RectTransform canvasRect = canvasScaler.transform as RectTransform;

            if (canvasRect != null)
            {
                canvasRect.localScale = Vector3.one;
            }
        }
    }

    private void ApplyBackgroundLayout()
    {
        if (!resizeBackgroundToScreen || cameraBackground == null)
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        RectTransform rectTransform = cameraBackground.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;

        ApplyBackgroundUvCrop(rectTransform);
    }

    private void ApplyBackgroundUvCrop(RectTransform rectTransform)
    {
        if (!cropBackgroundToFill || cameraBackground.texture == null)
        {
            cameraBackground.uvRect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        Rect rect = rectTransform.rect;

        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        float rectAspect = rect.width / rect.height;
        float textureAspect = (float)cameraBackground.texture.width / cameraBackground.texture.height;
        Rect uv = new Rect(0f, 0f, 1f, 1f);

        if (rectAspect > textureAspect)
        {
            uv.height = textureAspect / rectAspect;
            uv.y = (1f - uv.height) * 0.5f;
        }
        else
        {
            uv.width = rectAspect / textureAspect;
            uv.x = (1f - uv.width) * 0.5f;
        }

        cameraBackground.uvRect = uv;
    }

    private bool BackgroundRectIsUsable(RectTransform backgroundTransform)
    {
        Rect rect = backgroundTransform.rect;

        return rect.width > 0f && rect.height > 0f;
    }

    private Vector2 BackgroundLocalPointToMeshUv(RectTransform backgroundTransform, Vector2 localPoint)
    {
        Rect backgroundRect = backgroundTransform.rect;
        Rect visibleUv = cameraBackground.uvRect;

        float normalizedX = Mathf.InverseLerp(backgroundRect.xMin, backgroundRect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(backgroundRect.yMin, backgroundRect.yMax, localPoint.y);

        return new Vector2(
            visibleUv.x + normalizedX * visibleUv.width,
            visibleUv.y + normalizedY * visibleUv.height);
    }

    private Vector2 MeshUvToBackgroundLocalPoint(RectTransform backgroundTransform, Vector2 meshUv)
    {
        Rect backgroundRect = backgroundTransform.rect;
        Rect visibleUv = cameraBackground.uvRect;

        float normalizedX = SafeInverseLerp(visibleUv.x, visibleUv.x + visibleUv.width, meshUv.x);
        float normalizedY = SafeInverseLerp(visibleUv.y, visibleUv.y + visibleUv.height, meshUv.y);

        return new Vector2(
            Mathf.LerpUnclamped(backgroundRect.xMin, backgroundRect.xMax, normalizedX),
            Mathf.LerpUnclamped(backgroundRect.yMin, backgroundRect.yMax, normalizedY));
    }

    private Vector2 MeshUvToShaderUv(Vector2 meshUv)
    {
        return MeshUvToShaderUv(
            meshUv,
            ClampHorizontalRoomPan(currentRoomPan),
            currentRoomFov,
            ClampVerticalRoomPan(currentRoomVerticalPan));
    }

    private Vector2 MeshUvToShaderUv(Vector2 meshUv, float cameraAngle, float fov, float verticalStrength)
    {
        cameraAngle = ClampHorizontalRoomPan(cameraAngle);
        fov = Mathf.Max(0.0001f, ClampRoomFov(fov));
        verticalStrength = ClampVerticalRoomPan(verticalStrength);
        float margin = GetShaderMargin();

        float sourceX = 0.2f + 0.6f *
            (0.5f + Mathf.Tan(cameraAngle + (meshUv.x - 0.5f) * fov) / Mathf.Tan(fov));

        float verticalScale = GetShaderVerticalScale(meshUv.x, verticalStrength);
        float verticalT = 0.5f + (meshUv.y - 0.5f) * verticalScale;
        float verticalMargin = margin * Mathf.Abs(verticalStrength);
        float sourceY = Mathf.LerpUnclamped(verticalMargin, 1f - verticalMargin, verticalT);

        return new Vector2(sourceX, sourceY);
    }

    private Vector2 ShaderUvToMeshUv(Vector2 shaderUv)
    {
        float fov = Mathf.Max(0.0001f, currentRoomFov);
        float cameraAngle = ClampHorizontalRoomPan(currentRoomPan);
        float verticalStrength = ClampVerticalRoomPan(currentRoomVerticalPan);
        float margin = GetShaderMargin();
        float tanFov = Mathf.Tan(fov);

        float horizontalT = (shaderUv.x - 0.2f) / 0.6f;
        float meshX = 0.5f + (Mathf.Atan((horizontalT - 0.5f) * tanFov) - cameraAngle) / fov;

        float verticalMargin = margin * Mathf.Abs(verticalStrength);
        float verticalT = SafeInverseLerp(verticalMargin, 1f - verticalMargin, shaderUv.y);
        float verticalScale = GetShaderVerticalScale(meshX, verticalStrength);
        float meshY = 0.5f + (verticalT - 0.5f) / Mathf.Max(0.0001f, verticalScale);

        return new Vector2(meshX, meshY);
    }

    private float GetShaderVerticalScale(float meshX, float verticalStrength)
    {
        // This mirrors the vertical squash/stretch portion of Background.shadergraph.
        // Door hitboxes need the same math because the background pixels move in
        // shader UV space, not by physically moving the RawImage RectTransform.
        float verticalCurve = 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (meshX + 0.5f));
        float curvedScale = Mathf.LerpUnclamped(1f, verticalCurve, Mathf.Abs(verticalStrength));

        if (verticalStrength > 0f)
        {
            return curvedScale;
        }

        return 1.5f - curvedScale;
    }

    private float GetShaderMargin()
    {
        Material material = cameraBackground != null ? cameraBackground.material : null;

        if (material == null || !material.HasProperty(MarginId))
        {
            return 0f;
        }

        return Mathf.Clamp01(material.GetFloat(MarginId));
    }

    private float SafeInverseLerp(float from, float to, float value)
    {
        if (Mathf.Approximately(from, to))
        {
            return 0f;
        }

        return (value - from) / (to - from);
    }

    private void SetTextureIfAvailable(Material material, int propertyId, Texture texture)
    {
        if (material.HasProperty(propertyId))
        {
            material.SetTexture(propertyId, texture);
        }
    }

    private void SetFloatIfAvailable(Material material, int propertyId, float value)
    {
        if (material.HasProperty(propertyId))
        {
            material.SetFloat(propertyId, value);
        }
    }

    private void MarkBackgroundPreviewDirty()
    {
        if (cameraBackground == null)
        {
            return;
        }

        cameraBackground.SetMaterialDirty();
        cameraBackground.SetVerticesDirty();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(cameraBackground);
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
