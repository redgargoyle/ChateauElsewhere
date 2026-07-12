using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class CameraManager : Chateau.Architecture.GameServiceBase
{
    public RawImage cameraBackground;
    public bool resizeBackgroundToScreen = true;
    public bool fitBackgroundToRoomAspect = true;
    public bool cropBackgroundToFill = true;
    public bool configureCanvasScaling = true;
    public bool enableCameraPostProcessing = true;
    public bool renderBackgroundCanvasThroughCamera = true;
    public Vector2 referenceResolution = new Vector2(1366f, 768f);
    [Range(0f, 1f)]
    public float matchWidthOrHeight = 0.5f;
    public float cameraShakeDuration = 1.6f;
    public float cameraShakeMagnitude = 22f;
    public float cameraShakeRotation = 2f;
    public float cameraShakeFrequency = 48f;
    public float cameraShakeZoom = 1.04f;
    [Header("Room Look")]
    public bool panRoomWithMouseEdges = true;
    [Range(0.01f, 0.5f)]
    public float mouseEdgePanZone = 0.12f;
    [Tooltip("The room camera only pans while the cursor is this close to the left or right screen edge.")]
    public float edgePanActivationPixels = 24f;
    [Range(0f, 1f)]
    public float maxRoomPan = 0.55f;
    public float roomPanSpeed = 3.5f;
    public float roomPanStartSpeed = 0.45f;
    public float roomPanAccelerationTime = 1.25f;
    public bool moveRoomVerticallyWithMouseEdges = true;
    [Tooltip("Keeps older scenes with vertical edge panning serialized off from trapping cropped room art above or below the viewport.")]
    public bool autoEnableVerticalRoomPan = true;
    [Range(0f, 1f)]
    public float maxRoomVerticalPan = 1f;
    public bool invertVerticalRoomPan;
    public bool zoomRoomWithMouseWheel = true;
    [Range(0f, 1f)]
    public float defaultRoomFov = 0.8f;
    [Range(0f, 1f)]
    public float minRoomFov = 0.55f;
    [Range(0f, 1f)]
    public float maxRoomFov = 1f;
    [Range(1f, 1.5f)]
    public float minRoomZoom = 1f;
    [Range(1f, 1.5f)]
    public float defaultRoomZoom = 1.06f;
    [Range(1f, 1.5f)]
    public float maxRoomZoom = 1.22f;
    [Range(0.01f, 0.5f)]
    public float mouseScrollZoomStep = 0.035f;
    [Range(0.01f, 0.5f)]
    public float roomZoomSmoothTime = 0.12f;
    public Vector2 roomZoomFocus = new Vector2(0.5f, 0.56f);
    [Range(0.1f, 5f)]
    public float maxMouseWheelStepsPerFrame = 1f;
    [Range(0f, 0.3f)]
    public float maxShaderPanAngle = 0.08f;
    [Header("Anchored Background Animation")]
    public bool enableAnchoredBackgroundAnimation = true;
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
    private static readonly int VerticalStrengthId = Shader.PropertyToID("_verticle_strength");
    private static readonly int OverlayTexId = Shader.PropertyToID("_OverlayTex");
    private static readonly int OverlayRectId = Shader.PropertyToID("_OverlayRect");
    private Coroutine cameraShakeRoutine;
    private RectTransform activeShakeTarget;
    private Vector2 shakeBasePosition;
    private Quaternion shakeBaseRotation;
    private Vector3 shakeBaseScale;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private Vector2 lastRoomViewportSize = new Vector2(-1f, -1f);
    private bool roomLayoutDirty = true;
    private bool applyingCanvasPreRenderLayout;
    private float currentRoomPan;
    private float targetRoomPan;
    private float currentRoomVerticalPan;
    private float targetRoomVerticalPan;
    private float currentRoomFov = 1f;
    private float currentRoomZoom = 1f;
    private float targetRoomZoom = 1f;
    private float roomZoomVelocity;
    private float horizontalEdgeHoldTime;
    private int lastHorizontalEdgeDirection;
    private int currentHorizontalEdgeDirection;
    private int currentVerticalEdgeDirection;
    private Texture currentBaseBackgroundTexture;
    private RenderTexture anchoredAnimationTexture;
    private Material anchoredAnimationCompositeMaterial;
    private Material runtimeBackgroundMaterial;
    private RoomNavigationManager roomNavigationManager;
    private int anchoredAnimationFrameIndex = -1;
    private int anchoredAnimationFrameDirection = 1;
    private float anchoredAnimationFrameTimer;
    private Rect lastAnchoredAnimationRect = new Rect(float.MinValue, float.MinValue, float.MinValue, float.MinValue);
    private Material materialBeforeFullImagePlacementPreview;
    private bool fullImagePlacementPreviewActive;
    private Rect baseBackgroundUvRect = new Rect(0f, 0f, 1f, 1f);
    private RectTransform activeRoomStage;
    private RoomContentGroup activeRoomContentGroup;
    private Transform originalBackgroundParent;
    private int originalBackgroundSiblingIndex = -1;
    private readonly Vector3[] anchoredReferenceCorners = new Vector3[4];
    private readonly Vector3[] viewportScreenCorners = new Vector3[4];

    public float CurrentRoomHorizontalPan => currentRoomPan;
    public float CurrentRoomVerticalPan => currentRoomVerticalPan;
    public float CurrentRoomFov => currentRoomFov;
    public float CurrentRoomZoom => currentRoomZoom;
    public bool IsFullImagePlacementPreviewActive => fullImagePlacementPreviewActive;

    public bool TryGetRoomStageWorldOffset(Camera worldCamera, out Vector3 worldOffset)
    {
        worldOffset = Vector3.zero;

        if (!HasUsableCameraViewport(worldCamera) ||
            !TryGetRoomStageScreenTransform(out Vector2 viewportCenter, out Vector2 stageCenter, out _))
        {
            return false;
        }

        Vector2 screenOffset = stageCenter - viewportCenter;

        if (screenOffset.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float depth = Mathf.Abs(worldCamera.transform.position.z);
        Vector3 worldCenter = worldCamera.ScreenToWorldPoint(new Vector3(viewportCenter.x, viewportCenter.y, depth));
        Vector3 shiftedWorldCenter = worldCamera.ScreenToWorldPoint(new Vector3(viewportCenter.x + screenOffset.x, viewportCenter.y + screenOffset.y, depth));
        worldOffset = shiftedWorldCenter - worldCenter;
        worldOffset.z = 0f;
        return true;
    }

    public bool TryGetRoomStageScreenTransform(
        out Vector2 viewportCenter,
        out Vector2 stageCenter,
        out float stageScale)
    {
        viewportCenter = Vector2.zero;
        stageCenter = Vector2.zero;
        stageScale = 1f;

        if (!UsesRoomStageLayout() || activeRoomStage == null)
        {
            return false;
        }

        RectTransform viewport = activeRoomStage.parent as RectTransform;
        if (viewport == null)
        {
            return false;
        }

        Canvas canvas = activeRoomStage.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        stageCenter = RectTransformUtility.WorldToScreenPoint(canvasCamera, activeRoomStage.TransformPoint(Vector3.zero));
        viewportCenter = RectTransformUtility.WorldToScreenPoint(canvasCamera, viewport.TransformPoint(Vector3.zero));
        stageScale = Mathf.Max(0.0001f, activeRoomStage.lossyScale.x);
        return true;
    }

    public bool TryGetActiveRoomStageLayoutScale(out float layoutScale)
    {
        layoutScale = 1f;

        if (!UsesRoomStageLayout() || activeRoomStage == null)
        {
            return false;
        }

        // CameraManager writes this normalized room-stage scale directly from
        // viewport fit * user zoom. Unlike lossyScale it does not include the
        // parent Canvas transform, so actors can follow the room deterministically
        // without capturing an Awake-order-dependent reference value.
        layoutScale = Mathf.Max(0.0001f, Mathf.Abs(activeRoomStage.localScale.x));
        return true;
    }

    public bool TryGetActiveRoomStageActorZoomRatio(out float zoomRatio)
    {
        zoomRatio = 1f;

        if (!UsesRoomStageLayout() || activeRoomStage == null)
        {
            return false;
        }

        // Authored actor calibration already represents the approved size at
        // the default room zoom. Only the player's relative wheel zoom belongs
        // here; viewport fitting and Canvas scaling must not rewrite that size.
        float referenceZoom = Mathf.Max(0.0001f, ClampRoomZoom(defaultRoomZoom));
        zoomRatio = CalculateActorZoomRatio(ClampRoomZoom(currentRoomZoom), referenceZoom);
        return true;
    }

    public static float CalculateActorZoomRatio(float currentZoom, float referenceZoom)
    {
        float safeReference = float.IsNaN(referenceZoom) ||
            float.IsInfinity(referenceZoom) ||
            referenceZoom <= 0f
                ? 1f
                : referenceZoom;
        float safeCurrent = float.IsNaN(currentZoom) ||
            float.IsInfinity(currentZoom) ||
            currentZoom <= 0f
                ? safeReference
                : currentZoom;
        return Mathf.Max(0.0001f, safeCurrent / safeReference);
    }

    public bool TryGetActiveRoomStageWorldPoint(Vector2 roomStageLocalPoint, float worldDepth, out Vector3 worldPoint)
    {
        return TryGetActiveRoomStageWorldPoint(roomStageLocalPoint, worldDepth, out worldPoint, out _);
    }

    public bool TryGetActiveRoomStageWorldPoint(
        Vector2 roomStageLocalPoint,
        float worldDepth,
        out Vector3 worldPoint,
        out float stageScale)
    {
        worldPoint = Vector3.zero;
        stageScale = 1f;

        Camera mainCamera = Camera.main;
        if (!UsesRoomStageLayout() || activeRoomStage == null || !HasUsableCameraViewport(mainCamera))
        {
            return false;
        }

        Canvas canvas = activeRoomStage.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector3 stageWorldPoint = activeRoomStage.TransformPoint(new Vector3(
            roomStageLocalPoint.x,
            roomStageLocalPoint.y,
            0f));
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, stageWorldPoint);

        float safeDepth = Mathf.Max(0.01f, worldDepth);
        worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, safeDepth));
        stageScale = Mathf.Max(0.0001f, activeRoomStage.lossyScale.x);
        return true;
    }

    public bool TryGetActiveRoomStageLocalPoint(Vector3 worldPoint, out Vector2 roomStageLocalPoint)
    {
        roomStageLocalPoint = Vector2.zero;

        Camera mainCamera = Camera.main;
        if (!UsesRoomStageLayout() || activeRoomStage == null || !HasUsableCameraViewport(mainCamera))
        {
            return false;
        }

        Canvas canvas = activeRoomStage.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            activeRoomStage,
            screenPoint,
            canvasCamera,
            out roomStageLocalPoint);
    }

    private static bool HasUsableCameraViewport(Camera camera)
    {
        return camera != null &&
            Screen.width > 0 &&
            Screen.height > 0 &&
            camera.pixelWidth > 0 &&
            camera.pixelHeight > 0;
    }

    private void Reset()
    {
        cameraBackground = FindAnyObjectByType<RawImage>();
    }

    private void Awake()
    {
        if (cameraBackground == null)
        {
            cameraBackground = FindAnyObjectByType<RawImage>();
        }

        RememberOriginalBackgroundParent();
        InitializeRoomLook();
        ConfigurePostProcessingRenderPath();
        EnsureBackgroundCanvasVisible();
        EnsureBackgroundMaterialAssigned();
        ConfigureCanvasScalers();
        HideAnchoredAnimationReferenceVisuals();
        EnsureBackgroundCanvasVisible();
        ApplyBackgroundLayout();
        ApplyRoomLookToMaterial();
    }

    private void OnEnable()
    {
        Canvas.willRenderCanvases -= HandleCanvasWillRender;
        Canvas.willRenderCanvases += HandleCanvasWillRender;
        MarkRoomLayoutDirty();
    }

    private void Start()
    {
        if (cameraBackground == null)
        {
            Debug.LogError("CameraManager needs a Camera Background RawImage assigned.", this);
            enabled = false;
            return;
        }

        EnsureBackgroundCanvasVisible();
        ConfigurePostProcessingRenderPath();
        Canvas.ForceUpdateCanvases();
        MarkRoomLayoutDirty();

        Texture navigationStartupTexture = GetNavigationStartupBackgroundTexture();
        SetCameraBackground(navigationStartupTexture != null ? navigationStartupTexture : cameraBackground.texture);
    }

    private void Update()
    {
        if (Screen.width != lastScreenWidth ||
            Screen.height != lastScreenHeight ||
            HasRoomViewportSizeChanged())
        {
            ApplyBackgroundLayout();
        }

        UpdateRoomLookFromInput();
        UpdateAnchoredBackgroundAnimation();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= HandleCanvasWillRender;
        NavigationCursorController.SetEdgePanDirection(0);
        RestoreBackgroundParentIfNeeded();
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

        RectTransform target = GetRoomViewTransform();

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

    public void SetActiveRoomContent(RoomContentGroup roomContentGroup, bool updateBackground = true)
    {
        bool roomStageChanged = activeRoomContentGroup != roomContentGroup;
        activeRoomContentGroup = roomContentGroup;
        activeRoomStage = roomContentGroup != null ? roomContentGroup.transform as RectTransform : null;

        if (roomStageChanged)
        {
            MarkRoomLayoutDirty();
            ResetRoomLookForRoomChange();
        }

        if (activeRoomStage == null)
        {
            RestoreBackgroundParentIfNeeded();
        }

        EnsureBackgroundMaterialAssigned();

        if (updateBackground &&
            roomContentGroup != null &&
            roomContentGroup.TryGetRoomBackgroundTexture(out Texture roomTexture) &&
            roomTexture != null)
        {
            SetCameraBackground(roomTexture);
            return;
        }

        ApplyBackgroundLayout();
        ApplyRoomLookToMaterial();
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
        EndFullImagePlacementPreview();

        if (cameraBackground.material == null)
        {
            cameraBackground.material = ResolveBackgroundMaterialForPreviewRestore();
        }

        SetCameraBackground(texture);
        MarkBackgroundPreviewDirty();
    }

    public void PreviewFullRoomImageForDoorEditing(Texture texture)
    {
        if (texture == null || cameraBackground == null)
        {
            return;
        }

        if (!fullImagePlacementPreviewActive)
        {
            materialBeforeFullImagePlacementPreview = ResolveBackgroundMaterialForPreviewRestore();
        }

        // This is an editor placement view, not the runtime camera view. It
        // deliberately bypasses the shader/crop material so the whole source
        // image is visible and door rectangles can be placed on hidden edges.
        fullImagePlacementPreviewActive = true;
        currentBaseBackgroundTexture = texture;
        cameraBackground.material = null;
        cameraBackground.texture = texture;
        cameraBackground.uvRect = new Rect(0f, 0f, 1f, 1f);
        MarkBackgroundPreviewDirty();
    }

    public void EndFullImagePlacementPreview()
    {
        if (!fullImagePlacementPreviewActive || cameraBackground == null)
        {
            return;
        }

        fullImagePlacementPreviewActive = false;

        Material restoreMaterial = materialBeforeFullImagePlacementPreview != null
            ? materialBeforeFullImagePlacementPreview
            : ResolveBackgroundMaterialForPreviewRestore();

        if (restoreMaterial != null)
        {
            cameraBackground.material = restoreMaterial;
        }

        Texture texture = currentBaseBackgroundTexture != null ? currentBaseBackgroundTexture : cameraBackground.texture;

        if (texture != null)
        {
            SetCameraBackground(texture);
        }
        else
        {
            MarkBackgroundPreviewDirty();
        }
    }

    private Material ResolveBackgroundMaterialForPreviewRestore()
    {
        if (cameraBackground != null && cameraBackground.material != null)
        {
            return cameraBackground.material;
        }

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Shader/BackgroundMaterial.mat");
#else
        return null;
#endif
    }

    private void EnsureBackgroundMaterialAssigned()
    {
        if (cameraBackground == null || fullImagePlacementPreviewActive)
        {
            return;
        }

        if (UsesRoomStageLayout())
        {
            ReleaseRuntimeBackgroundMaterial();
            cameraBackground.material = null;
            return;
        }

        Material sourceMaterial = cameraBackground.material != null
            ? cameraBackground.material
            : ResolveBackgroundMaterialForPreviewRestore();

        if (sourceMaterial == null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            cameraBackground.material = sourceMaterial;
            return;
        }

        // Room panning writes shader uniforms every frame, so Play mode needs a
        // private material instead of editing the shared BackgroundMaterial asset.
        if (runtimeBackgroundMaterial == null)
        {
            runtimeBackgroundMaterial = Instantiate(sourceMaterial);
            runtimeBackgroundMaterial.name = $"{sourceMaterial.name} (Runtime)";
            runtimeBackgroundMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        cameraBackground.material = runtimeBackgroundMaterial;
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
        currentRoomZoom = ClampRoomZoom(defaultRoomZoom);
        targetRoomZoom = currentRoomZoom;
        roomZoomVelocity = 0f;

        ApplyBackgroundLayout();
        ApplyRoomLookToMaterial();
        MarkBackgroundPreviewDirty();
    }

    public void ResetRoomLookForPreview()
    {
        SetRoomLookForPreview(0f, 0f, defaultRoomFov);
    }

    private Texture GetNavigationStartupBackgroundTexture()
    {
        if (roomNavigationManager == null)
        {
            roomNavigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }

        if (roomNavigationManager == null || string.IsNullOrWhiteSpace(roomNavigationManager.CurrentRoom))
        {
            return null;
        }

        return roomNavigationManager.FindRoomTexture(roomNavigationManager.CurrentRoom);
    }

    private void SetCameraBackground(Texture texture)
    {
        if (currentBaseBackgroundTexture != texture)
        {
            ResetRoomLookForRoomChange();
            ResetAnchoredAnimationPlayback();
            MarkRoomLayoutDirty();
        }

        currentBaseBackgroundTexture = texture;
        cameraBackground.texture = texture;
        EnsureBackgroundMaterialAssigned();
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

        return true;
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
        if (!enableAnchoredBackgroundAnimation || !hideAnchoredAnimationReference || anchoredAnimationReference == null)
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
        Canvas.willRenderCanvases -= HandleCanvasWillRender;
        ReleaseRuntimeBackgroundMaterial();
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

    private void ReleaseRuntimeBackgroundMaterial()
    {
        if (runtimeBackgroundMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeBackgroundMaterial);
        }
        else
        {
            DestroyImmediate(runtimeBackgroundMaterial);
        }

        runtimeBackgroundMaterial = null;
    }

    private void InitializeRoomLook()
    {
        currentRoomFov = ClampRoomFov(defaultRoomFov);
        currentRoomZoom = ClampRoomZoom(defaultRoomZoom);
        targetRoomZoom = currentRoomZoom;
        MarkRoomLayoutDirty();
    }

    private void ResetRoomLookForRoomChange()
    {
        targetRoomPan = 0f;
        currentRoomPan = 0f;
        targetRoomVerticalPan = 0f;
        currentRoomVerticalPan = 0f;
        currentRoomFov = ClampRoomFov(defaultRoomFov);
        currentRoomZoom = ClampRoomZoom(defaultRoomZoom);
        targetRoomZoom = currentRoomZoom;
        roomZoomVelocity = 0f;
        ResetHorizontalEdgeHold();
        currentHorizontalEdgeDirection = 0;
        currentVerticalEdgeDirection = 0;
        NavigationCursorController.SetEdgePanDirection(0, 0);
        MarkRoomLayoutDirty();
    }

    private void UpdateRoomLookFromInput()
    {
        if (cameraBackground == null)
        {
            return;
        }

        if (RuntimeSettingsMenu.BlocksGameInput)
        {
            ResetHorizontalEdgeHold();
            currentHorizontalEdgeDirection = 0;
            currentVerticalEdgeDirection = 0;
            NavigationCursorController.SetEdgePanDirection(0, 0);
            return;
        }

        bool shouldApply = false;

        if (panRoomWithMouseEdges)
        {
            float previousPan = currentRoomPan;
            targetRoomPan = GetRoomHorizontalPanTargetFromMouse(out currentHorizontalEdgeDirection);
            currentRoomPan = ClampHorizontalRoomPan(MoveValue(currentRoomPan, targetRoomPan, GetCurrentHorizontalPanSpeed(currentHorizontalEdgeDirection)));
            if (!Mathf.Approximately(previousPan, currentRoomPan))
            {
                ApplyBackgroundLayout();
            }

            shouldApply = true;
        }
        else
        {
            ResetHorizontalEdgeHold();
            currentHorizontalEdgeDirection = 0;
        }

        if (ShouldMoveRoomVerticallyWithMouseEdges())
        {
            float previousVerticalPan = currentRoomVerticalPan;
            targetRoomVerticalPan = GetRoomVerticalPanTargetFromMouse(out currentVerticalEdgeDirection);
            currentRoomVerticalPan = ClampVerticalRoomPan(MoveValue(currentRoomVerticalPan, targetRoomVerticalPan, roomPanSpeed));
            if (!Mathf.Approximately(previousVerticalPan, currentRoomVerticalPan))
            {
                ApplyBackgroundLayout();
            }

            shouldApply = true;
        }
        else
        {
            currentVerticalEdgeDirection = 0;
            targetRoomVerticalPan = currentRoomVerticalPan;
        }

        NavigationCursorController.SetEdgePanDirection(currentHorizontalEdgeDirection, currentVerticalEdgeDirection);

        if (zoomRoomWithMouseWheel)
        {
            float scrollDelta = ClampMouseWheelDelta(Input.mouseScrollDelta.y);

            if (!Mathf.Approximately(scrollDelta, 0f))
            {
                targetRoomZoom = ClampRoomZoom(targetRoomZoom + scrollDelta * mouseScrollZoomStep);
            }

            float previousZoom = currentRoomZoom;
            currentRoomZoom = SmoothRoomZoom(currentRoomZoom, targetRoomZoom);

            if (!Mathf.Approximately(previousZoom, currentRoomZoom))
            {
                ApplyBackgroundLayout();
            }

            shouldApply = true;
        }
        else if (!Mathf.Approximately(currentRoomZoom, ClampRoomZoom(defaultRoomZoom)) ||
            !Mathf.Approximately(targetRoomZoom, ClampRoomZoom(defaultRoomZoom)))
        {
            targetRoomZoom = ClampRoomZoom(defaultRoomZoom);
            currentRoomZoom = targetRoomZoom;
            roomZoomVelocity = 0f;
            ApplyBackgroundLayout();
            shouldApply = true;
        }

        if (shouldApply)
        {
            ApplyRoomLookToMaterial();
        }
    }

    private float ClampMouseWheelDelta(float scrollDelta)
    {
        float maxDelta = Mathf.Max(0.1f, maxMouseWheelStepsPerFrame);
        return Mathf.Clamp(scrollDelta, -maxDelta, maxDelta);
    }

    private float SmoothRoomZoom(float currentValue, float targetValue)
    {
        float smoothTime = Mathf.Max(0.01f, roomZoomSmoothTime);

        // Mouse wheels and trackpads report discrete bursts. SmoothDamp turns
        // those bursts into a small, continuous zoom instead of stepping the
        // painted room between visible sizes.
        return ClampRoomZoom(Mathf.SmoothDamp(
            currentValue,
            targetValue,
            ref roomZoomVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime));
    }

    private float GetRoomHorizontalPanTargetFromMouse(out int edgeDirection)
    {
        edgeDirection = 0;

        if (!TryGetMousePositionOnScreen(out Vector3 mousePosition))
        {
            return currentRoomPan;
        }

        edgeDirection = GetHorizontalEdgeDirection(mousePosition);

        if (edgeDirection == 0)
        {
            ResetHorizontalEdgeHold();
            return currentRoomPan;
        }

        // No recentering here: the room view only changes while the cursor is
        // touching the left or right edge. Once the cursor leaves the edge, the
        // current pan is held exactly where it is.
        return ClampHorizontalRoomPan(edgeDirection * Mathf.Clamp01(maxRoomPan));
    }

    private int GetHorizontalEdgeDirection(Vector3 mousePosition)
    {
        float edgePixels = Mathf.Max(1f, edgePanActivationPixels);
        Rect inputRect = GetRoomInputScreenRect();

        if (mousePosition.x <= inputRect.xMin + edgePixels)
        {
            return -1;
        }

        if (mousePosition.x >= inputRect.xMax - edgePixels)
        {
            return 1;
        }

        return 0;
    }

    private float GetCurrentHorizontalPanSpeed(int edgeDirection)
    {
        if (edgeDirection == 0)
        {
            return roomPanStartSpeed;
        }

        if (edgeDirection != lastHorizontalEdgeDirection)
        {
            horizontalEdgeHoldTime = 0f;
            lastHorizontalEdgeDirection = edgeDirection;
        }

        horizontalEdgeHoldTime += Time.unscaledDeltaTime;
        float accelerationTime = Mathf.Max(0.01f, roomPanAccelerationTime);
        float acceleration = Mathf.Clamp01(horizontalEdgeHoldTime / accelerationTime);

        // Panning starts gently for control, then ramps to the normal tutorial
        // speed while the player keeps holding the cursor on the screen edge.
        return Mathf.Lerp(Mathf.Max(0f, roomPanStartSpeed), Mathf.Max(0f, roomPanSpeed), acceleration);
    }

    private void ResetHorizontalEdgeHold()
    {
        horizontalEdgeHoldTime = 0f;
        lastHorizontalEdgeDirection = 0;
    }

    private float GetRoomVerticalPanTargetFromMouse(out int edgeDirection)
    {
        edgeDirection = 0;

        if (!TryGetMousePositionOnScreen(out Vector3 mousePosition))
        {
            return currentRoomVerticalPan;
        }

        edgeDirection = GetVerticalEdgeDirection(mousePosition);

        if (invertVerticalRoomPan)
        {
            edgeDirection = -edgeDirection;
        }

        if (edgeDirection == 0)
        {
            return currentRoomVerticalPan;
        }

        return ClampVerticalRoomPan(edgeDirection * Mathf.Clamp01(maxRoomVerticalPan));
    }

    private int GetVerticalEdgeDirection(Vector3 mousePosition)
    {
        float edgePixels = Mathf.Max(1f, edgePanActivationPixels);
        Rect inputRect = GetRoomInputScreenRect();

        if (mousePosition.y <= inputRect.yMin + edgePixels)
        {
            return -1;
        }

        if (mousePosition.y >= inputRect.yMax - edgePixels)
        {
            return 1;
        }

        return 0;
    }

    private bool ShouldMoveRoomVerticallyWithMouseEdges()
    {
        return moveRoomVerticallyWithMouseEdges || (autoEnableVerticalRoomPan && panRoomWithMouseEdges);
    }

    private Rect GetRoomInputScreenRect()
    {
        if (TryGetRoomViewportScreenRect(out Rect viewportRect) &&
            viewportRect.width > 0f &&
            viewportRect.height > 0f)
        {
            return viewportRect;
        }

        return new Rect(0f, 0f, Mathf.Max(1f, Screen.width), Mathf.Max(1f, Screen.height));
    }

    private bool TryGetRoomViewportScreenRect(out Rect screenRect)
    {
        screenRect = new Rect(0f, 0f, 0f, 0f);
        RectTransform viewport = null;

        if (UsesRoomStageLayout())
        {
            viewport = activeRoomStage.parent as RectTransform;
        }

        if (viewport == null && cameraBackground != null)
        {
            viewport = cameraBackground.rectTransform.parent as RectTransform;
        }

        if (viewport == null)
        {
            return false;
        }

        Canvas canvas = viewport.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        viewport.GetWorldCorners(viewportScreenCorners);

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        for (int i = 0; i < viewportScreenCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, viewportScreenCorners[i]);
            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }

        screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return screenRect.width > 0f && screenRect.height > 0f;
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

    private float ClampRoomZoom(float zoom)
    {
        float minZoom = Mathf.Max(1f, minRoomZoom);
        float maxZoom = Mathf.Max(minZoom, maxRoomZoom);

        return Mathf.Clamp(zoom, minZoom, maxZoom);
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
        bool roomStageOwnsMotion = UsesRoomStageLayout();
        SetFloatIfAvailable(material, CameraAngleId, roomStageOwnsMotion ? 0f : GetShaderCameraAngle());
        SetFloatIfAvailable(material, FovId, roomStageOwnsMotion ? 1f : currentRoomFov);
        SetFloatIfAvailable(material, VerticalStrengthId, roomStageOwnsMotion ? 0f : ClampVerticalRoomPan(currentRoomVerticalPan));
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

        CanvasScaler[] canvasScalers = FindObjectsByType<CanvasScaler>();

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

    private void EnsureBackgroundCanvasVisible()
    {
        if (cameraBackground == null)
        {
            return;
        }

        Canvas backgroundCanvas = cameraBackground.GetComponentInParent<Canvas>(true);

        // Door-editing previews can leave the background canvas scaled down or
        // hidden. The actual game view should recover every time Play starts.
        Transform current = cameraBackground.transform;

        while (current != null)
        {
            current.gameObject.SetActive(true);

            if (backgroundCanvas != null && current == backgroundCanvas.transform)
            {
                break;
            }

            current = current.parent;
        }

        if (backgroundCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = backgroundCanvas.transform as RectTransform;

        if (canvasRect == null)
        {
            return;
        }

        canvasRect.localScale = Vector3.one;

        if (backgroundCanvas.renderMode == RenderMode.ScreenSpaceOverlay ||
            backgroundCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    private void ConfigurePostProcessingRenderPath()
    {
        Camera targetCamera = ResolveRenderCamera();

        if (enableCameraPostProcessing && targetCamera != null)
        {
            targetCamera.allowHDR = true;

            UniversalAdditionalCameraData cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData != null)
            {
                cameraData.renderPostProcessing = true;
            }
        }

        if (!renderBackgroundCanvasThroughCamera || cameraBackground == null || targetCamera == null)
        {
            return;
        }

        Canvas backgroundCanvas = cameraBackground.GetComponentInParent<Canvas>(true);

        if (backgroundCanvas == null)
        {
            return;
        }

        if (backgroundCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            backgroundCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        }

        if (backgroundCanvas.renderMode != RenderMode.ScreenSpaceCamera)
        {
            return;
        }

        backgroundCanvas.worldCamera = targetCamera;

        if (backgroundCanvas.planeDistance <= targetCamera.nearClipPlane ||
            backgroundCanvas.planeDistance >= targetCamera.farClipPlane)
        {
            backgroundCanvas.planeDistance = Mathf.Clamp(
                100f,
                targetCamera.nearClipPlane + 0.01f,
                targetCamera.farClipPlane - 0.01f);
        }
    }

    private Camera ResolveRenderCamera()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            return mainCamera;
        }

        return FindAnyObjectByType<Camera>();
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
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;

        if (TryApplyRoomStageLayout(rectTransform))
        {
            roomLayoutDirty = false;
            return;
        }

        if (fitBackgroundToRoomAspect && rectTransform.parent is RectTransform parentRect)
        {
            Vector2 fitSize = GetBackgroundFitSize(parentRect.rect.size);
            Vector2 zoomedSize = fitSize * ClampRoomZoom(currentRoomZoom);
            Vector2 maxOffset = new Vector2(
                Mathf.Max(0f, (zoomedSize.x - parentRect.rect.width) * 0.5f),
                Mathf.Max(0f, (zoomedSize.y - parentRect.rect.height) * 0.5f));

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(
                -GetHorizontalPanAmount() * maxOffset.x,
                -GetVerticalPanAmount() * maxOffset.y);
            rectTransform.sizeDelta = zoomedSize;
        }
        else
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        ApplyBackgroundUvCrop(rectTransform);
        roomLayoutDirty = false;
    }

    private void ApplyBackgroundUvCrop(RectTransform rectTransform)
    {
        baseBackgroundUvRect = GetBaseBackgroundUvCrop(rectTransform);
        cameraBackground.uvRect = fitBackgroundToRoomAspect ? baseBackgroundUvRect : ApplyRoomZoomToUvRect(baseBackgroundUvRect);
    }

    private bool TryApplyRoomStageLayout(RectTransform backgroundRect)
    {
        if (!UsesRoomStageLayout() || backgroundRect == null)
        {
            return false;
        }

        RectTransform viewport = activeRoomStage.parent as RectTransform;

        if (viewport == null)
        {
            return false;
        }

        Vector2 roomSize = GetActiveRoomNativeSize();

        if (roomSize.x <= 0f || roomSize.y <= 0f)
        {
            return false;
        }

        if (Application.isPlaying)
        {
            AttachBackgroundToRoomStage(backgroundRect);
        }

        Vector2 viewportSize = GetUsableRectSize(viewport);
        if (viewportSize.x <= 0f || viewportSize.y <= 0f)
        {
            return false;
        }

        lastRoomViewportSize = viewportSize;
        float viewportScale = GetRoomStageViewportScale(viewportSize, roomSize);
        float zoom = ClampRoomZoom(currentRoomZoom);
        float stageScale = viewportScale * zoom;
        Vector2 scaledRoomSize = roomSize * stageScale;
        Vector2 maxOffset = new Vector2(
            Mathf.Max(0f, (scaledRoomSize.x - viewportSize.x) * 0.5f),
            Mathf.Max(0f, (scaledRoomSize.y - viewportSize.y) * 0.5f));

        // The room stage is the single moving surface. It is sized from the room
        // background texture, not child bounds, so props outside the painting do
        // not change the camera frame.
        activeRoomStage.anchorMin = new Vector2(0.5f, 0.5f);
        activeRoomStage.anchorMax = new Vector2(0.5f, 0.5f);
        activeRoomStage.pivot = new Vector2(0.5f, 0.5f);
        activeRoomStage.sizeDelta = roomSize;
        activeRoomStage.anchoredPosition = new Vector2(
            -GetHorizontalPanAmount() * maxOffset.x,
            -GetVerticalPanAmount() * maxOffset.y);
        activeRoomStage.localRotation = Quaternion.identity;
        activeRoomStage.localScale = new Vector3(stageScale, stageScale, 1f);

        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = roomSize;
        backgroundRect.localRotation = Quaternion.identity;
        backgroundRect.localScale = Vector3.one;

        baseBackgroundUvRect = new Rect(0f, 0f, 1f, 1f);
        cameraBackground.uvRect = baseBackgroundUvRect;
        return true;
    }

    private bool UsesRoomStageLayout()
    {
        return activeRoomStage != null && activeRoomContentGroup != null;
    }

    private void MarkRoomLayoutDirty()
    {
        roomLayoutDirty = true;
        lastRoomViewportSize = new Vector2(-1f, -1f);
    }

    private bool HasRoomViewportSizeChanged()
    {
        if (!UsesRoomStageLayout())
        {
            return false;
        }

        RectTransform viewport = activeRoomStage.parent as RectTransform;
        if (viewport == null)
        {
            return false;
        }

        Vector2 viewportSize = GetUsableRectSize(viewport);
        if (viewportSize.x <= 0f || viewportSize.y <= 0f)
        {
            return false;
        }

        return !ApproximatelySameLayoutSize(viewportSize, lastRoomViewportSize);
    }

    private bool ApproximatelySameLayoutSize(Vector2 a, Vector2 b)
    {
        const float layoutPixelTolerance = 0.5f;
        return Mathf.Abs(a.x - b.x) <= layoutPixelTolerance &&
               Mathf.Abs(a.y - b.y) <= layoutPixelTolerance;
    }

    private void HandleCanvasWillRender()
    {
        if (!isActiveAndEnabled ||
            cameraBackground == null ||
            applyingCanvasPreRenderLayout)
        {
            return;
        }

        applyingCanvasPreRenderLayout = true;
        try
        {
            if (!roomLayoutDirty && !HasRoomViewportSizeChanged())
            {
                return;
            }

            ApplyBackgroundLayout();
            ApplyRoomLookToMaterial();
        }
        finally
        {
            applyingCanvasPreRenderLayout = false;
        }
    }

    private void RememberOriginalBackgroundParent()
    {
        if (cameraBackground == null || originalBackgroundParent != null)
        {
            return;
        }

        originalBackgroundParent = cameraBackground.transform.parent;
        originalBackgroundSiblingIndex = cameraBackground.transform.GetSiblingIndex();
    }

    private void AttachBackgroundToRoomStage(RectTransform backgroundRect)
    {
        if (backgroundRect == null || activeRoomStage == null)
        {
            return;
        }

        if (originalBackgroundParent == null)
        {
            RememberOriginalBackgroundParent();
        }

        if (backgroundRect.parent != activeRoomStage)
        {
            backgroundRect.SetParent(activeRoomStage, false);
        }

        backgroundRect.SetAsFirstSibling();
    }

    private void RestoreBackgroundParentIfNeeded()
    {
        if (cameraBackground == null || originalBackgroundParent == null)
        {
            return;
        }

        if (cameraBackground.transform.parent == originalBackgroundParent)
        {
            return;
        }

        cameraBackground.transform.SetParent(originalBackgroundParent, false);

        if (originalBackgroundSiblingIndex >= 0)
        {
            cameraBackground.transform.SetSiblingIndex(Mathf.Min(originalBackgroundSiblingIndex, originalBackgroundParent.childCount - 1));
        }
    }

    private RectTransform GetRoomViewTransform()
    {
        if (UsesRoomStageLayout())
        {
            return activeRoomStage;
        }

        return cameraBackground != null ? cameraBackground.rectTransform : null;
    }

    private Vector2 GetActiveRoomNativeSize()
    {
        Texture texture = currentBaseBackgroundTexture;

        if (texture == null && cameraBackground != null)
        {
            texture = cameraBackground.texture;
        }

        if (texture != null && texture.width > 0 && texture.height > 0)
        {
            return new Vector2(texture.width, texture.height);
        }

        if (activeRoomStage != null)
        {
            Rect rect = activeRoomStage.rect;

            if (rect.width > 0f && rect.height > 0f)
            {
                return rect.size;
            }
        }

        return referenceResolution;
    }

    private Vector2 GetUsableRectSize(RectTransform rectTransform)
    {
        Rect rect = rectTransform.rect;
        Vector2 size = rect.size;

        if (size.x > 0f && size.y > 0f)
        {
            return size;
        }

        if (!applyingCanvasPreRenderLayout)
        {
            Canvas.ForceUpdateCanvases();
            rect = rectTransform.rect;
            size = rect.size;

            if (size.x > 0f && size.y > 0f)
            {
                return size;
            }
        }

        return new Vector2(Mathf.Max(1f, Screen.width), Mathf.Max(1f, Screen.height));
    }

    private float GetRoomStageViewportScale(Vector2 viewportSize, Vector2 roomSize)
    {
        if (viewportSize.x <= 0f || viewportSize.y <= 0f || roomSize.x <= 0f || roomSize.y <= 0f)
        {
            return 1f;
        }

        float widthScale = viewportSize.x / roomSize.x;
        float heightScale = viewportSize.y / roomSize.y;

        // Runtime room stages should always cover the camera viewport. The
        // legacy cropBackgroundToFill flag still applies to the fallback RawImage
        // path, but active rooms use stage scaling so objects, doors, and the
        // room painting stay locked together without exposing camera clear color.
        return Mathf.Max(widthScale, heightScale);
    }

    private Rect GetBaseBackgroundUvCrop(RectTransform rectTransform)
    {
        if (fitBackgroundToRoomAspect)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        if (!cropBackgroundToFill || cameraBackground.texture == null)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        Rect rect = rectTransform.rect;

        if (rect.width <= 0f || rect.height <= 0f)
        {
            return baseBackgroundUvRect;
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

        return uv;
    }

    private Rect ApplyRoomZoomToUvRect(Rect uv)
    {
        float zoom = ClampRoomZoom(currentRoomZoom);

        if (Mathf.Approximately(zoom, 1f))
        {
            return uv;
        }

        Vector2 focus = new Vector2(Mathf.Clamp01(roomZoomFocus.x), Mathf.Clamp01(roomZoomFocus.y));
        float zoomedWidth = uv.width / zoom;
        float zoomedHeight = uv.height / zoom;
        float x = uv.x + (uv.width - zoomedWidth) * focus.x;
        float y = uv.y + (uv.height - zoomedHeight) * focus.y;

        x = Mathf.Clamp(x, uv.xMin, uv.xMax - zoomedWidth);
        y = Mathf.Clamp(y, uv.yMin, uv.yMax - zoomedHeight);

        return new Rect(x, y, zoomedWidth, zoomedHeight);
    }

    private Vector2 GetBackgroundFitSize(Vector2 parentSize)
    {
        if (parentSize.x <= 0f || parentSize.y <= 0f)
        {
            return Vector2.zero;
        }

        float targetAspect = GetBackgroundTargetAspect();
        float parentAspect = parentSize.x / parentSize.y;
        float width = parentSize.x;
        float height = parentSize.y;

        if (parentAspect > targetAspect)
        {
            width = height * targetAspect;
        }
        else
        {
            height = width / targetAspect;
        }

        return new Vector2(width, height);
    }

    private float GetBackgroundTargetAspect()
    {
        Texture texture = cameraBackground != null ? cameraBackground.texture : null;

        if (texture != null && texture.width > 0 && texture.height > 0)
        {
            return (float)texture.width / texture.height;
        }

        return referenceResolution.y > 0f ? Mathf.Max(0.01f, referenceResolution.x / referenceResolution.y) : 16f / 9f;
    }

    private float GetHorizontalPanAmount()
    {
        float maxPan = Mathf.Max(0.0001f, Mathf.Clamp01(maxRoomPan));
        return Mathf.Clamp(ClampHorizontalRoomPan(currentRoomPan) / maxPan, -1f, 1f);
    }

    private float GetVerticalPanAmount()
    {
        float maxPan = Mathf.Max(0.0001f, Mathf.Clamp01(maxRoomVerticalPan));
        return Mathf.Clamp(ClampVerticalRoomPan(currentRoomVerticalPan) / maxPan, -1f, 1f);
    }

    private float GetShaderCameraAngle()
    {
        return GetHorizontalPanAmount() * Mathf.Clamp(maxShaderPanAngle, 0f, 0.3f);
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

public static class NavigationCursorController
{
    public enum HoverIcon
    {
        Door,
        ExitLeaveRoom,
        Stairway,
        StairsUp,
        StairsDown,
        Inspect,
        PickUpTake,
        PickUpCoat,
        Coat,
        PlaceHangCoat,
        BlockedCoat,
        Talk,
        Locked,
        Unavailable,
        Ui
    }

    private const int CursorDesignSize = 48;
    private const int CursorSize = 72;
    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
    private static readonly Color Ink = new Color(0.05f, 0.025f, 0.015f, 1f);
    private static readonly Color InkSoft = new Color(0.16f, 0.08f, 0.045f, 1f);
    private static readonly Color Shadow = new Color(0.02f, 0.01f, 0.006f, 0.55f);
    private static readonly Color ParchmentLight = new Color(1f, 0.91f, 0.68f, 1f);
    private static readonly Color Brass = new Color(0.88f, 0.58f, 0.19f, 1f);
    private static readonly Color Gold = new Color(1f, 0.76f, 0.25f, 1f);
    private static readonly Color DoorBlue = new Color(0.16f, 0.25f, 0.48f, 1f);
    private static readonly Color DoorPanel = new Color(0.25f, 0.38f, 0.63f, 1f);
    private static readonly Color StairTeal = new Color(0.12f, 0.48f, 0.48f, 1f);
    private static readonly Color StairHighlight = new Color(0.42f, 0.82f, 0.75f, 1f);
    private static readonly Color CoatWine = new Color(0.52f, 0.12f, 0.19f, 1f);
    private static readonly Color CoatHighlight = new Color(0.92f, 0.39f, 0.35f, 1f);
    private static readonly Color TalkPlum = new Color(0.42f, 0.19f, 0.52f, 1f);
    private static readonly Color TalkLavender = new Color(0.77f, 0.55f, 0.86f, 1f);
    private static readonly Color WalkGreen = new Color(0.2f, 0.52f, 0.27f, 1f);
    private static readonly Color WalkHighlight = new Color(0.62f, 0.86f, 0.46f, 1f);
    private static readonly Color UiIvory = new Color(0.99f, 0.86f, 0.62f, 1f);
    private static readonly Color UiBlue = new Color(0.18f, 0.31f, 0.56f, 1f);
    private static readonly Color Warning = new Color(0.92f, 0.05f, 0.04f, 1f);
    private static readonly Vector2 ArrowHotspot = ScaleCursorHotspot(24f, 24f);
    private static readonly Vector2 DoorHotspot = ScaleCursorHotspot(12f, 12f);
    private static readonly Vector2 StairwayHotspot = ScaleCursorHotspot(12f, 34f);
    private static readonly Vector2 CoatHotspot = ScaleCursorHotspot(12f, 15f);
    private static readonly Vector2 TalkHotspot = ScaleCursorHotspot(13f, 13f);
    private static readonly Vector2 WalkHotspot = ScaleCursorHotspot(22f, 37f);
    private static readonly Vector2 UiHotspot = ScaleCursorHotspot(13f, 10f);

    private static int edgePanHorizontalDirection;
    private static int edgePanVerticalDirection;
    private static bool gameplayHoverBlocked;
    private static object doorHoverOwner;
    private static HoverIcon doorHoverIcon;
    private static object walkHoverOwner;
    private static bool walkHoverCanMove;
    private static Texture2D leftArrowCursor;
    private static Texture2D rightArrowCursor;
    private static Texture2D upArrowCursor;
    private static Texture2D downArrowCursor;
    private static Texture2D doorCursor;
    private static Texture2D exitLeaveRoomCursor;
    private static Texture2D stairwayCursor;
    private static Texture2D stairsUpCursor;
    private static Texture2D stairsDownCursor;
    private static Texture2D inspectCursor;
    private static Texture2D pickUpTakeCursor;
    private static Texture2D pickUpCoatCursor;
    private static Texture2D coatCursor;
    private static Texture2D placeHangCoatCursor;
    private static Texture2D blockedCoatCursor;
    private static Texture2D talkCursor;
    private static Texture2D lockedCursor;
    private static Texture2D unavailableCursor;
    private static Texture2D uiCursor;
    private static Texture2D walkCursor;
    private static Texture2D blockedWalkCursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetForPlayMode()
    {
        edgePanHorizontalDirection = 0;
        edgePanVerticalDirection = 0;
        gameplayHoverBlocked = false;
        doorHoverOwner = null;
        doorHoverIcon = HoverIcon.Door;
        walkHoverOwner = null;
        walkHoverCanMove = false;
        ClearCursorTextureCache();
        ApplyCursor();
    }

    public static int[] GetAvailableCursorStyles()
    {
        return CursorStyleCatalog.GetAvailableStyleIndices();
    }

    public static int GetSelectedCursorStyle()
    {
        return CursorStyleCatalog.GetSelectedStyleIndex();
    }

    public static void SetCursorStyle(int styleIndex)
    {
        if (!CursorStyleCatalog.SetSelectedStyleIndex(styleIndex))
        {
            return;
        }

        ClearCursorTextureCache();
        ApplyCursor();
    }

    public static Texture2D LoadCursorStylePreview(int styleIndex, CursorStyleCatalog.CursorAction action)
    {
        return CursorStyleCatalog.LoadTexture(styleIndex, action);
    }

    public static void SetEdgePanDirection(int direction)
    {
        SetEdgePanDirection(direction, 0);
    }

    public static void SetEdgePanDirection(int horizontalDirection, int verticalDirection)
    {
        int cleanHorizontalDirection = horizontalDirection < 0 ? -1 : horizontalDirection > 0 ? 1 : 0;
        int cleanVerticalDirection = verticalDirection < 0 ? -1 : verticalDirection > 0 ? 1 : 0;

        if (gameplayHoverBlocked)
        {
            cleanHorizontalDirection = 0;
            cleanVerticalDirection = 0;
        }

        if (edgePanHorizontalDirection == cleanHorizontalDirection &&
            edgePanVerticalDirection == cleanVerticalDirection)
        {
            return;
        }

        edgePanHorizontalDirection = cleanHorizontalDirection;
        edgePanVerticalDirection = cleanVerticalDirection;
        ApplyCursor();
    }

    public static void SetGameplayHoverBlocked(bool blocked)
    {
        if (gameplayHoverBlocked == blocked)
        {
            return;
        }

        gameplayHoverBlocked = blocked;

        if (gameplayHoverBlocked)
        {
            edgePanHorizontalDirection = 0;
            edgePanVerticalDirection = 0;
            if (doorHoverIcon != HoverIcon.Ui)
            {
                doorHoverOwner = null;
            }
            walkHoverOwner = null;
            walkHoverCanMove = false;
        }

        ApplyCursor();
    }

    public static void SetDoorHover(object owner, bool active)
    {
        SetDoorHover(owner, HoverIcon.Door, active);
    }

    public static void SetDoorHover(object owner, HoverIcon icon, bool active)
    {
        if (gameplayHoverBlocked && icon != HoverIcon.Ui)
        {
            return;
        }

        if (active)
        {
            doorHoverOwner = owner;
            doorHoverIcon = icon;
            ApplyCursor();
            return;
        }

        ClearDoorHover(owner);
    }

    public static void ClearDoorHover(object owner)
    {
        if (doorHoverOwner != owner)
        {
            return;
        }

        doorHoverOwner = null;
        ApplyCursor();
    }

    public static void SetWalkHover(object owner, bool active, bool canMove)
    {
        if (gameplayHoverBlocked)
        {
            return;
        }

        if (!active)
        {
            ClearWalkHover(owner);
            return;
        }

        if (owner == null)
        {
            return;
        }

        if (walkHoverOwner == owner && walkHoverCanMove == canMove)
        {
            return;
        }

        walkHoverOwner = owner;
        walkHoverCanMove = canMove;
        ApplyCursor();
    }

    public static void ClearWalkHover(object owner)
    {
        if (walkHoverOwner != owner)
        {
            return;
        }

        walkHoverOwner = null;
        walkHoverCanMove = false;
        ApplyCursor();
    }

    private static void ApplyCursor()
    {
        if (gameplayHoverBlocked && (doorHoverOwner == null || doorHoverIcon != HoverIcon.Ui))
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        // Door hover wins because it is the most specific click action. Edge
        // panning comes next so the cursor matches the camera movement gesture.
        if (doorHoverOwner != null)
        {
            if (doorHoverIcon == HoverIcon.ExitLeaveRoom)
            {
                Cursor.SetCursor(GetExitLeaveRoomCursor(), DoorHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.StairsUp)
            {
                Cursor.SetCursor(GetStairsUpCursor(), StairwayHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.StairsDown)
            {
                Cursor.SetCursor(GetStairsDownCursor(), StairwayHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.Stairway)
            {
                Cursor.SetCursor(GetStairwayCursor(), StairwayHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.Inspect)
            {
                Cursor.SetCursor(GetInspectCursor(), UiHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.PickUpTake)
            {
                Cursor.SetCursor(GetPickUpTakeCursor(), CoatHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.PickUpCoat)
            {
                Cursor.SetCursor(GetPickUpCoatCursor(), CoatHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.Coat)
            {
                Cursor.SetCursor(GetCoatCursor(), CoatHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.PlaceHangCoat)
            {
                Cursor.SetCursor(GetPlaceHangCoatCursor(), CoatHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.BlockedCoat)
            {
                Cursor.SetCursor(GetBlockedCoatCursor(), CoatHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.Talk)
            {
                Cursor.SetCursor(GetTalkCursor(), TalkHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.Locked)
            {
                Cursor.SetCursor(GetLockedCursor(), CoatHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.Unavailable)
            {
                Cursor.SetCursor(GetUnavailableCursor(), WalkHotspot, CursorMode.Auto);
                return;
            }

            if (doorHoverIcon == HoverIcon.Ui)
            {
                Cursor.SetCursor(GetUiCursor(), UiHotspot, CursorMode.Auto);
                return;
            }

            Cursor.SetCursor(GetDoorCursor(), DoorHotspot, CursorMode.Auto);
            return;
        }

        if (edgePanHorizontalDirection < 0)
        {
            Cursor.SetCursor(GetLeftArrowCursor(), ArrowHotspot, CursorMode.Auto);
            return;
        }

        if (edgePanHorizontalDirection > 0)
        {
            Cursor.SetCursor(GetRightArrowCursor(), ArrowHotspot, CursorMode.Auto);
            return;
        }

        if (edgePanVerticalDirection > 0)
        {
            Cursor.SetCursor(GetUpArrowCursor(), ArrowHotspot, CursorMode.Auto);
            return;
        }

        if (edgePanVerticalDirection < 0)
        {
            Cursor.SetCursor(GetDownArrowCursor(), ArrowHotspot, CursorMode.Auto);
            return;
        }

        if (walkHoverOwner != null)
        {
            Cursor.SetCursor(
                walkHoverCanMove ? GetWalkCursor() : GetBlockedWalkCursor(),
                WalkHotspot,
                CursorMode.Auto);
            return;
        }

        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private static Texture2D GetLeftArrowCursor()
    {
        if (leftArrowCursor == null)
        {
            leftArrowCursor = CreateArrowCursor("Cursor_LeftEdgeArrow", -1);
        }

        return leftArrowCursor;
    }

    private static Texture2D GetRightArrowCursor()
    {
        if (rightArrowCursor == null)
        {
            rightArrowCursor = CreateArrowCursor("Cursor_RightEdgeArrow", 1);
        }

        return rightArrowCursor;
    }

    private static Texture2D GetUpArrowCursor()
    {
        if (upArrowCursor == null)
        {
            upArrowCursor = CreateVerticalArrowCursor("Cursor_UpEdgeArrow", 1);
        }

        return upArrowCursor;
    }

    private static Texture2D GetDownArrowCursor()
    {
        if (downArrowCursor == null)
        {
            downArrowCursor = CreateVerticalArrowCursor("Cursor_DownEdgeArrow", -1);
        }

        return downArrowCursor;
    }

    private static Texture2D GetDoorCursor()
    {
        return GetStyledCursorTexture(ref doorCursor, CursorStyleCatalog.CursorAction.OpenDoor, CreateDoorCursor);
    }

    private static Texture2D GetExitLeaveRoomCursor()
    {
        return GetStyledCursorTexture(ref exitLeaveRoomCursor, CursorStyleCatalog.CursorAction.ExitLeaveRoom, CreateDoorCursor);
    }

    private static Texture2D GetStairwayCursor()
    {
        return GetStyledCursorTexture(ref stairwayCursor, CursorStyleCatalog.CursorAction.StairsUp, CreateStairwayCursor);
    }

    private static Texture2D GetStairsUpCursor()
    {
        return GetStyledCursorTexture(ref stairsUpCursor, CursorStyleCatalog.CursorAction.StairsUp, CreateStairwayCursor);
    }

    private static Texture2D GetStairsDownCursor()
    {
        return GetStyledCursorTexture(ref stairsDownCursor, CursorStyleCatalog.CursorAction.StairsDown, CreateStairwayCursor);
    }

    private static Texture2D GetInspectCursor()
    {
        return GetStyledCursorTexture(ref inspectCursor, CursorStyleCatalog.CursorAction.InspectLook, CreateUiCursor);
    }

    private static Texture2D GetPickUpTakeCursor()
    {
        return GetStyledCursorTexture(ref pickUpTakeCursor, CursorStyleCatalog.CursorAction.PickUpTake, () => CreateCoatCursor("Cursor_PickUpTake", false));
    }

    private static Texture2D GetPickUpCoatCursor()
    {
        return GetStyledCursorTexture(ref pickUpCoatCursor, CursorStyleCatalog.CursorAction.PickUpCoat, () => CreateCoatCursor("Cursor_PickUpCoat", false));
    }

    private static Texture2D GetCoatCursor()
    {
        return GetStyledCursorTexture(ref coatCursor, CursorStyleCatalog.CursorAction.PickUpCoat, () => CreateCoatCursor("Cursor_Coat", false));
    }

    private static Texture2D GetPlaceHangCoatCursor()
    {
        return GetStyledCursorTexture(ref placeHangCoatCursor, CursorStyleCatalog.CursorAction.PlaceHangCoat, () => CreateCoatCursor("Cursor_PlaceHangCoat", false));
    }

    private static Texture2D GetBlockedCoatCursor()
    {
        return GetStyledCursorTexture(ref blockedCoatCursor, CursorStyleCatalog.CursorAction.LockedCannotUse, () => CreateCoatCursor("Cursor_CoatBlocked", true));
    }

    private static Texture2D GetTalkCursor()
    {
        return GetStyledCursorTexture(ref talkCursor, CursorStyleCatalog.CursorAction.TalkConverse, CreateTalkCursor);
    }

    private static Texture2D GetLockedCursor()
    {
        return GetStyledCursorTexture(ref lockedCursor, CursorStyleCatalog.CursorAction.LockedCannotUse, () => CreateCoatCursor("Cursor_Locked", true));
    }

    private static Texture2D GetUnavailableCursor()
    {
        return GetStyledCursorTexture(ref unavailableCursor, CursorStyleCatalog.CursorAction.NotAvailableDisabled, () => CreateWalkCursor("Cursor_Unavailable", true));
    }

    private static Texture2D GetUiCursor()
    {
        return GetStyledCursorTexture(ref uiCursor, CursorStyleCatalog.CursorAction.UseInteract, CreateUiCursor);
    }

    private static Texture2D GetWalkCursor()
    {
        return GetStyledCursorTexture(ref walkCursor, CursorStyleCatalog.CursorAction.WalkMove, () => CreateWalkCursor("Cursor_Walk", false));
    }

    private static Texture2D GetBlockedWalkCursor()
    {
        return GetStyledCursorTexture(ref blockedWalkCursor, CursorStyleCatalog.CursorAction.NotAvailableDisabled, () => CreateWalkCursor("Cursor_WalkBlocked", true));
    }

    private static Texture2D GetStyledCursorTexture(
        ref Texture2D cache,
        CursorStyleCatalog.CursorAction action,
        System.Func<Texture2D> fallbackFactory)
    {
        if (cache != null)
        {
            return cache;
        }

        Texture2D styledTexture = CursorStyleCatalog.LoadSelectedTexture(action);
        cache = styledTexture != null ? styledTexture : fallbackFactory();
        return cache;
    }

    private static void ClearCursorTextureCache()
    {
        leftArrowCursor = null;
        rightArrowCursor = null;
        upArrowCursor = null;
        downArrowCursor = null;
        doorCursor = null;
        exitLeaveRoomCursor = null;
        stairwayCursor = null;
        stairsUpCursor = null;
        stairsDownCursor = null;
        inspectCursor = null;
        pickUpTakeCursor = null;
        pickUpCoatCursor = null;
        coatCursor = null;
        placeHangCoatCursor = null;
        blockedCoatCursor = null;
        talkCursor = null;
        lockedCursor = null;
        unavailableCursor = null;
        uiCursor = null;
        walkCursor = null;
        blockedWalkCursor = null;
    }

    private static Texture2D CreateArrowCursor(string cursorName, int direction)
    {
        Texture2D texture = CreateBlankCursor(cursorName);
        int tipX = direction < 0 ? 7 : 40;
        int backX = direction < 0 ? 31 : 16;
        int tailX = direction < 0 ? 41 : 6;

        DrawLine(texture, tipX + direction, 24 + 2, backX + direction, 9 + 2, Shadow, 8);
        DrawLine(texture, tipX + direction, 24 + 2, backX + direction, 39 + 2, Shadow, 8);
        DrawLine(texture, tipX + direction, 24 + 2, tailX + direction, 24 + 2, Shadow, 8);
        DrawLine(texture, tipX, 24, backX, 9, Ink, 8);
        DrawLine(texture, tipX, 24, backX, 39, Ink, 8);
        DrawLine(texture, tipX, 24, tailX, 24, Ink, 8);
        DrawLine(texture, tipX, 24, backX, 9, Brass, 5);
        DrawLine(texture, tipX, 24, backX, 39, Brass, 5);
        DrawLine(texture, tipX, 24, tailX, 24, Brass, 5);
        DrawLine(texture, tipX + direction * 2, 23, backX, 13, Gold, 2);
        DrawLine(texture, tipX + direction * 2, 25, tailX - direction * 4, 25, ParchmentLight, 2);
        AddWatercolorTexture(texture, 17);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateVerticalArrowCursor(string cursorName, int direction)
    {
        Texture2D texture = CreateBlankCursor(cursorName);
        int tipY = direction > 0 ? 40 : 7;
        int backY = direction > 0 ? 16 : 31;
        int tailY = direction > 0 ? 6 : 41;

        DrawLine(texture, 24 + 2, tipY + direction, 9 + 2, backY + direction, Shadow, 8);
        DrawLine(texture, 24 + 2, tipY + direction, 39 + 2, backY + direction, Shadow, 8);
        DrawLine(texture, 24 + 2, tipY + direction, 24 + 2, tailY + direction, Shadow, 8);
        DrawLine(texture, 24, tipY, 9, backY, Ink, 8);
        DrawLine(texture, 24, tipY, 39, backY, Ink, 8);
        DrawLine(texture, 24, tipY, 24, tailY, Ink, 8);
        DrawLine(texture, 24, tipY, 9, backY, Brass, 5);
        DrawLine(texture, 24, tipY, 39, backY, Brass, 5);
        DrawLine(texture, 24, tipY, 24, tailY, Brass, 5);
        DrawLine(texture, 23, tipY + direction * 2, 13, backY, Gold, 2);
        DrawLine(texture, 25, tipY + direction * 2, 25, tailY - direction * 4, ParchmentLight, 2);
        AddWatercolorTexture(texture, 19);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateDoorCursor()
    {
        Texture2D texture = CreateBlankCursor("Cursor_OpenDoor");

        FillEllipse(texture, 24, 14, 17, 10, Shadow);
        FillRect(texture, 7, 14, 37, 42, Shadow);
        FillEllipse(texture, 22, 12, 17, 10, Ink);
        FillRect(texture, 5, 13, 35, 42, Ink);
        FillEllipse(texture, 22, 13, 13, 7, Brass);
        FillRect(texture, 9, 13, 31, 39, Brass);
        FillRect(texture, 13, 16, 28, 39, DoorBlue);
        FillRect(texture, 16, 19, 25, 37, DoorPanel);
        DrawLine(texture, 13, 16, 28, 20, Ink, 3);
        DrawLine(texture, 28, 20, 28, 38, Ink, 3);
        DrawLine(texture, 28, 38, 13, 39, Ink, 3);
        DrawLine(texture, 13, 16, 13, 39, Ink, 3);
        DrawLine(texture, 16, 18, 25, 21, ParchmentLight, 1);
        FillEllipse(texture, 23, 28, 2, 2, Gold);
        DrawLine(texture, 33, 13, 42, 8, Gold, 4);
        DrawLine(texture, 42, 8, 39, 15, Gold, 4);
        DrawLine(texture, 42, 8, 35, 8, Gold, 4);
        AddWatercolorTexture(texture, 29);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateStairwayCursor()
    {
        Texture2D texture = CreateBlankCursor("Cursor_Stairway");

        DrawLine(texture, 9, 39, 39, 39, Shadow, 8);
        DrawLine(texture, 9, 39, 9, 31, Shadow, 8);
        DrawLine(texture, 9, 31, 17, 31, Shadow, 8);
        DrawLine(texture, 17, 31, 17, 23, Shadow, 8);
        DrawLine(texture, 17, 23, 25, 23, Shadow, 8);
        DrawLine(texture, 25, 23, 25, 15, Shadow, 8);
        DrawLine(texture, 25, 15, 35, 15, Shadow, 8);
        DrawLine(texture, 35, 15, 35, 8, Shadow, 8);
        DrawLine(texture, 7, 37, 39, 37, Ink, 7);
        DrawLine(texture, 7, 37, 7, 29, Ink, 7);
        DrawLine(texture, 7, 29, 15, 29, Ink, 7);
        DrawLine(texture, 15, 29, 15, 21, Ink, 7);
        DrawLine(texture, 15, 21, 23, 21, Ink, 7);
        DrawLine(texture, 23, 21, 23, 13, Ink, 7);
        DrawLine(texture, 23, 13, 34, 13, Ink, 7);
        DrawLine(texture, 34, 13, 34, 6, Ink, 7);
        DrawLine(texture, 7, 37, 39, 37, StairTeal, 4);
        DrawLine(texture, 7, 29, 15, 29, StairTeal, 4);
        DrawLine(texture, 15, 21, 23, 21, StairTeal, 4);
        DrawLine(texture, 23, 13, 34, 13, StairTeal, 4);
        DrawLine(texture, 7, 37, 7, 29, StairHighlight, 2);
        DrawLine(texture, 15, 29, 15, 21, StairHighlight, 2);
        DrawLine(texture, 23, 21, 23, 13, StairHighlight, 2);
        DrawLine(texture, 34, 13, 34, 6, StairHighlight, 2);
        DrawLine(texture, 36, 9, 42, 15, Gold, 3);
        DrawLine(texture, 42, 15, 42, 9, Gold, 3);
        AddWatercolorTexture(texture, 43);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateTalkCursor()
    {
        Texture2D texture = CreateBlankCursor("Cursor_Talk");

        FillEllipse(texture, 24, 20, 18, 12, Shadow);
        DrawLine(texture, 15, 31, 9, 39, Shadow, 6);
        FillEllipse(texture, 22, 18, 18, 12, Ink);
        DrawLine(texture, 15, 29, 9, 37, Ink, 6);
        FillEllipse(texture, 22, 18, 15, 9, TalkPlum);
        DrawLine(texture, 15, 29, 11, 35, TalkPlum, 4);
        FillEllipse(texture, 22, 18, 11, 6, TalkLavender);
        FillEllipse(texture, 15, 18, 2, 2, Ink);
        FillEllipse(texture, 22, 18, 2, 2, Ink);
        FillEllipse(texture, 29, 18, 2, 2, Ink);
        DrawLine(texture, 34, 11, 42, 8, Gold, 3);
        DrawLine(texture, 36, 18, 44, 18, Gold, 3);
        AddWatercolorTexture(texture, 59);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateUiCursor()
    {
        Texture2D texture = CreateBlankCursor("Cursor_UiClick");

        DrawLine(texture, 14, 10, 14, 31, Shadow, 7);
        DrawLine(texture, 20, 18, 20, 33, Shadow, 7);
        DrawLine(texture, 26, 20, 26, 34, Shadow, 7);
        DrawLine(texture, 32, 23, 32, 35, Shadow, 7);
        FillEllipse(texture, 24, 36, 15, 8, Shadow);
        DrawLine(texture, 12, 8, 12, 30, Ink, 7);
        DrawLine(texture, 18, 17, 18, 32, Ink, 7);
        DrawLine(texture, 24, 19, 24, 33, Ink, 7);
        DrawLine(texture, 30, 22, 30, 34, Ink, 7);
        FillEllipse(texture, 22, 34, 15, 8, Ink);
        DrawLine(texture, 12, 8, 12, 30, UiIvory, 4);
        DrawLine(texture, 18, 17, 18, 32, UiIvory, 4);
        DrawLine(texture, 24, 19, 24, 33, UiIvory, 4);
        DrawLine(texture, 30, 22, 30, 34, UiIvory, 4);
        FillEllipse(texture, 22, 34, 12, 5, UiIvory);
        FillRect(texture, 12, 35, 31, 41, UiBlue);
        FillRect(texture, 14, 36, 29, 39, Brass);
        FillEllipse(texture, 38, 12, 4, 4, Gold);
        FillRect(texture, 37, 5, 39, 19, Gold);
        FillRect(texture, 31, 11, 45, 13, Gold);
        AddWatercolorTexture(texture, 71);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateWalkCursor(string cursorName, bool blocked)
    {
        Texture2D texture = CreateBlankCursor(cursorName);

        DrawFootprint(texture, 17, 10, false);
        DrawFootprint(texture, 28, 21, true);

        if (blocked)
        {
            DrawBlockedSlash(texture);
        }

        AddWatercolorTexture(texture, blocked ? 89 : 83);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateCoatCursor(string cursorName, bool blocked)
    {
        Texture2D texture = CreateBlankCursor(cursorName);

        DrawLine(texture, 10, 28, 16, 12, Shadow, 9);
        DrawLine(texture, 16, 12, 24, 15, Shadow, 9);
        DrawLine(texture, 24, 15, 32, 12, Shadow, 9);
        DrawLine(texture, 32, 12, 39, 28, Shadow, 9);
        DrawLine(texture, 39, 28, 30, 39, Shadow, 9);
        DrawLine(texture, 30, 39, 17, 38, Shadow, 9);
        DrawLine(texture, 17, 38, 10, 28, Shadow, 9);
        DrawLine(texture, 8, 26, 15, 10, Ink, 8);
        DrawLine(texture, 15, 10, 24, 13, Ink, 8);
        DrawLine(texture, 24, 13, 33, 10, Ink, 8);
        DrawLine(texture, 33, 10, 40, 26, Ink, 8);
        DrawLine(texture, 40, 26, 30, 38, Ink, 8);
        DrawLine(texture, 30, 38, 17, 37, Ink, 8);
        DrawLine(texture, 17, 37, 8, 26, Ink, 8);
        DrawLine(texture, 10, 26, 16, 13, CoatWine, 5);
        DrawLine(texture, 16, 13, 24, 16, CoatWine, 5);
        DrawLine(texture, 24, 16, 32, 13, CoatWine, 5);
        DrawLine(texture, 32, 13, 37, 26, CoatWine, 5);
        DrawLine(texture, 37, 26, 29, 35, CoatWine, 5);
        DrawLine(texture, 29, 35, 18, 34, CoatWine, 5);
        DrawLine(texture, 18, 34, 10, 26, CoatWine, 5);
        DrawLine(texture, 17, 17, 30, 31, CoatHighlight, 2);
        DrawLine(texture, 24, 16, 24, 34, InkSoft, 2);
        FillEllipse(texture, 21, 22, 2, 2, Gold);
        FillEllipse(texture, 25, 26, 2, 2, Gold);

        if (blocked)
        {
            DrawBlockedSlash(texture);
        }

        AddWatercolorTexture(texture, blocked ? 107 : 101);
        texture.Apply();
        return texture;
    }

    private static void DrawFootprint(Texture2D texture, int x, int y, bool mirrored)
    {
        int toeDirection = mirrored ? -1 : 1;
        FillEllipse(texture, x + 1, y + 10, 5, 8, Shadow);
        FillEllipse(texture, x, y + 8, 5, 8, Ink);
        FillEllipse(texture, x, y + 8, 3, 6, WalkGreen);
        FillEllipse(texture, x - toeDirection * 4, y + 16, 2, 3, Ink);
        FillEllipse(texture, x, y + 18, 2, 3, Ink);
        FillEllipse(texture, x + toeDirection * 4, y + 17, 2, 3, Ink);
        FillEllipse(texture, x - toeDirection * 4, y + 16, 1, 2, WalkHighlight);
        FillEllipse(texture, x, y + 18, 1, 2, WalkHighlight);
        FillEllipse(texture, x + toeDirection * 4, y + 17, 1, 2, WalkHighlight);
    }

    private static void DrawBlockedSlash(Texture2D texture)
    {
        DrawLine(texture, 8, 8, 40, 40, Shadow, 8);
        DrawLine(texture, 40, 8, 8, 40, Shadow, 8);
        DrawLine(texture, 7, 7, 39, 39, Ink, 7);
        DrawLine(texture, 39, 7, 7, 39, Ink, 7);
        DrawLine(texture, 7, 7, 39, 39, Warning, 4);
        DrawLine(texture, 39, 7, 7, 39, Warning, 4);
        DrawLine(texture, 9, 8, 37, 36, ParchmentLight, 1);
    }

    private static Texture2D CreateBlankCursor(string cursorName)
    {
        Texture2D texture = new Texture2D(CursorSize, CursorSize, TextureFormat.RGBA32, false)
        {
            name = cursorName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < CursorSize; y++)
        {
            for (int x = 0; x < CursorSize; x++)
            {
                texture.SetPixel(x, y, Clear);
            }
        }

        return texture;
    }

    private static void FillRect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color color)
    {
        int scaledMinX = ScaleCursorCoordinate(Mathf.Min(minX, maxX));
        int scaledMaxX = ScaleCursorCoordinate(Mathf.Max(minX, maxX));
        int scaledMinY = ScaleCursorCoordinate(Mathf.Min(minY, maxY));
        int scaledMaxY = ScaleCursorCoordinate(Mathf.Max(minY, maxY));

        for (int y = scaledMinY; y <= scaledMaxY; y++)
        {
            for (int x = scaledMinX; x <= scaledMaxX; x++)
            {
                SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static void FillEllipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        int scaledCenterX = ScaleCursorCoordinate(centerX);
        int scaledCenterY = ScaleCursorCoordinate(centerY);
        int scaledRadiusX = Mathf.Max(1, ScaleCursorLength(radiusX));
        int scaledRadiusY = Mathf.Max(1, ScaleCursorLength(radiusY));

        for (int y = scaledCenterY - scaledRadiusY; y <= scaledCenterY + scaledRadiusY; y++)
        {
            for (int x = scaledCenterX - scaledRadiusX; x <= scaledCenterX + scaledRadiusX; x++)
            {
                float dx = (x - scaledCenterX) / (float)scaledRadiusX;
                float dy = (y - scaledCenterY) / (float)scaledRadiusY;

                if (dx * dx + dy * dy <= 1f)
                {
                    SetPixelSafe(texture, x, y, color);
                }
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        x0 = ScaleCursorCoordinate(x0);
        y0 = ScaleCursorCoordinate(y0);
        x1 = ScaleCursorCoordinate(x1);
        y1 = ScaleCursorCoordinate(y1);
        thickness = ScaleCursorThickness(thickness);

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int stepX = x0 < x1 ? 1 : -1;
        int stepY = y0 < y1 ? 1 : -1;
        int error = dx - dy;

        while (true)
        {
            DrawThickPixel(texture, x0, y0, color, thickness);

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int doubledError = error * 2;

            if (doubledError > -dy)
            {
                error -= dy;
                x0 += stepX;
            }

            if (doubledError < dx)
            {
                error += dx;
                y0 += stepY;
            }
        }
    }

    private static void DrawThickPixel(Texture2D texture, int centerX, int centerY, Color color, int thickness)
    {
        int radius = Mathf.Max(1, thickness) / 2;

        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static Vector2 ScaleCursorHotspot(float x, float y)
    {
        return new Vector2(x * CursorSize / CursorDesignSize, y * CursorSize / CursorDesignSize);
    }

    private static int ScaleCursorCoordinate(int value)
    {
        return Mathf.RoundToInt(value * (CursorSize - 1f) / (CursorDesignSize - 1f));
    }

    private static int ScaleCursorThickness(int thickness)
    {
        return Mathf.Max(1, Mathf.RoundToInt(thickness * CursorSize / (float)CursorDesignSize));
    }

    private static int ScaleCursorLength(int value)
    {
        return Mathf.Max(1, Mathf.RoundToInt(value * CursorSize / (float)CursorDesignSize));
    }

    private static void AddWatercolorTexture(Texture2D texture, int seed)
    {
        for (int y = 0; y < CursorSize; y++)
        {
            for (int x = 0; x < CursorSize; x++)
            {
                Color color = texture.GetPixel(x, y);

                if (color.a <= 0f)
                {
                    continue;
                }

                int hash = (x * 73856093) ^ (y * 19349663) ^ (seed * 83492791);
                float tint = 0.9f + Mathf.Abs(hash % 23) / 100f;
                float paperLift = Mathf.Abs((hash / 23) % 5) * 0.01f;
                color.r = Mathf.Clamp01(color.r * tint + paperLift);
                color.g = Mathf.Clamp01(color.g * tint + paperLift);
                color.b = Mathf.Clamp01(color.b * tint + paperLift);
                texture.SetPixel(x, y, color);
            }
        }
    }

    private static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= CursorSize || y >= CursorSize)
        {
            return;
        }

        texture.SetPixel(x, y, color);
    }
}
