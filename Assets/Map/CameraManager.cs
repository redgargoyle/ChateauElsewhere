using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class CameraManager : MonoBehaviour
{
    public RawImage cameraBackground;
    public AudioSource cameraSwitchSound;
    public StaticNoisePlayer staticScreen;
    public bool playStaticOnCameraSwitch = true;
    public float staticTransitionDuration = 1f;
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
    [Tooltip("The room camera only pans while the cursor is this close to the left or right screen edge.")]
    public float edgePanActivationPixels = 24f;
    [Range(0f, 1f)]
    public float maxRoomPan = 0.55f;
    public float roomPanSpeed = 3.5f;
    public float roomPanStartSpeed = 0.45f;
    public float roomPanAccelerationTime = 1.25f;
    public bool returnRoomPanToCenter;
    public bool moveRoomVerticallyWithMouseEdges;
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

    public float CurrentRoomHorizontalPan => currentRoomPan;
    public float CurrentRoomVerticalPan => currentRoomVerticalPan;
    public float CurrentRoomFov => currentRoomFov;
    public float CurrentRoomZoom => currentRoomZoom;
    public bool IsFullImagePlacementPreviewActive => fullImagePlacementPreviewActive;

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
        SetCameraBackground(navigationStartupTexture != null ? navigationStartupTexture : GetStartupBackgroundTexture());
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

    public void SelectCamera(CameraAreaController selected)
    {
        if (selected == null || cameraBackground == null)
        {
            return;
        }

        Texture texture = selected.GetEffectiveRoomBackgroundTexture();

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

    private Texture GetStartupBackgroundTexture()
    {
        if (!useStartupCameraOnStart)
        {
            return cameraBackground.texture;
        }

        Texture startupTexture = GetCameraAreaBackgroundTexture(startupCamera);

        if (startupTexture != null)
        {
            return startupTexture;
        }

        Texture anchoredStartupTexture = useAnchoredAnimationCameraAsStartup
            ? GetCameraAreaBackgroundTexture(anchoredAnimationCamera)
            : null;

        if (anchoredStartupTexture != null)
        {
            return anchoredStartupTexture;
        }

        return cameraBackground.texture;
    }

    private Texture GetNavigationStartupBackgroundTexture()
    {
        if (roomNavigationManager == null)
        {
            roomNavigationManager = FindObjectOfType<RoomNavigationManager>(true);
        }

        if (roomNavigationManager == null || string.IsNullOrWhiteSpace(roomNavigationManager.CurrentRoom))
        {
            return null;
        }

        return roomNavigationManager.FindRoomTexture(roomNavigationManager.CurrentRoom);
    }

    private Texture GetCameraAreaBackgroundTexture(CameraAreaController cameraArea)
    {
        if (cameraArea == null)
        {
            return null;
        }

        return cameraArea.GetEffectiveRoomBackgroundTexture();
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
        NavigationCursorController.SetEdgePanDirection(0);
        MarkRoomLayoutDirty();
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
            float previousPan = currentRoomPan;
            targetRoomPan = GetRoomHorizontalPanTargetFromMouse(out int edgeDirection);
            currentRoomPan = ClampHorizontalRoomPan(MoveValue(currentRoomPan, targetRoomPan, GetCurrentHorizontalPanSpeed(edgeDirection)));
            NavigationCursorController.SetEdgePanDirection(edgeDirection);
            if (!Mathf.Approximately(previousPan, currentRoomPan))
            {
                ApplyBackgroundLayout();
            }

            shouldApply = true;
        }
        else
        {
            ResetHorizontalEdgeHold();
            NavigationCursorController.SetEdgePanDirection(0);
        }

        if (moveRoomVerticallyWithMouseEdges)
        {
            float previousVerticalPan = currentRoomVerticalPan;
            targetRoomVerticalPan = GetRoomVerticalPanTargetFromMouse();
            currentRoomVerticalPan = ClampVerticalRoomPan(MoveValue(currentRoomVerticalPan, targetRoomVerticalPan, roomPanSpeed));
            if (!Mathf.Approximately(previousVerticalPan, currentRoomVerticalPan))
            {
                ApplyBackgroundLayout();
            }

            shouldApply = true;
        }
        else if (!Mathf.Approximately(currentRoomVerticalPan, 0f) || !Mathf.Approximately(targetRoomVerticalPan, 0f))
        {
            targetRoomVerticalPan = 0f;
            currentRoomVerticalPan = 0f;
            ApplyBackgroundLayout();
            shouldApply = true;
        }

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

        edgeDirection = GetHorizontalEdgeDirection(mousePosition.x);

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

    private int GetHorizontalEdgeDirection(float mouseX)
    {
        float edgePixels = Mathf.Max(1f, edgePanActivationPixels);

        if (mouseX <= edgePixels)
        {
            return -1;
        }

        if (mouseX >= Screen.width - edgePixels)
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

    private float ClampRoomZoom(float zoom)
    {
        float minZoom = Mathf.Max(1f, defaultRoomZoom);
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

        return FindObjectOfType<Camera>();
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
        float fitScale = GetFitScale(viewportSize, roomSize);
        float zoom = ClampRoomZoom(currentRoomZoom);
        float stageScale = fitScale * zoom;
        Vector2 scaledRoomSize = roomSize * stageScale;
        Vector2 maxOffset = new Vector2(
            Mathf.Max(0f, (scaledRoomSize.x - viewportSize.x) * 0.5f),
            Mathf.Max(0f, (scaledRoomSize.y - viewportSize.y) * 0.5f));

        // The room stage is the single moving surface. Background, doors, and
        // future room props all share this transform, so they cannot drift apart.
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

        if (!roomLayoutDirty && !HasRoomViewportSizeChanged())
        {
            return;
        }

        applyingCanvasPreRenderLayout = true;
        try
        {
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

        Canvas.ForceUpdateCanvases();
        rect = rectTransform.rect;
        size = rect.size;

        if (size.x > 0f && size.y > 0f)
        {
            return size;
        }

        return new Vector2(Mathf.Max(1f, Screen.width), Mathf.Max(1f, Screen.height));
    }

    private float GetFitScale(Vector2 viewportSize, Vector2 roomSize)
    {
        if (viewportSize.x <= 0f || viewportSize.y <= 0f || roomSize.x <= 0f || roomSize.y <= 0f)
        {
            return 1f;
        }

        return Mathf.Min(viewportSize.x / roomSize.x, viewportSize.y / roomSize.y);
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
        Stairway
    }

    private const int CursorSize = 32;
    private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
    private static readonly Color Ink = new Color(0.02f, 0.02f, 0.02f, 1f);
    private static readonly Color Paper = new Color(1f, 1f, 1f, 1f);
    private static readonly Vector2 ArrowHotspot = new Vector2(16f, 16f);
    private static readonly Vector2 DoorHotspot = new Vector2(9f, 6f);
    private static readonly Vector2 StairwayHotspot = new Vector2(10f, 7f);

    private static int edgePanDirection;
    private static object doorHoverOwner;
    private static HoverIcon doorHoverIcon;
    private static Texture2D leftArrowCursor;
    private static Texture2D rightArrowCursor;
    private static Texture2D doorCursor;
    private static Texture2D stairwayCursor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetForPlayMode()
    {
        edgePanDirection = 0;
        doorHoverOwner = null;
        doorHoverIcon = HoverIcon.Door;
        ApplyCursor();
    }

    public static void SetEdgePanDirection(int direction)
    {
        int cleanDirection = direction < 0 ? -1 : direction > 0 ? 1 : 0;

        if (edgePanDirection == cleanDirection)
        {
            return;
        }

        edgePanDirection = cleanDirection;
        ApplyCursor();
    }

    public static void SetDoorHover(object owner, bool active)
    {
        SetDoorHover(owner, HoverIcon.Door, active);
    }

    public static void SetDoorHover(object owner, HoverIcon icon, bool active)
    {
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

    private static void ApplyCursor()
    {
        // Door hover wins over edge panning because clicking the door is the more
        // specific action. When neither state is active, Unity's default cursor returns.
        if (doorHoverOwner != null)
        {
            if (doorHoverIcon == HoverIcon.Stairway)
            {
                Cursor.SetCursor(GetStairwayCursor(), StairwayHotspot, CursorMode.Auto);
                return;
            }

            Cursor.SetCursor(GetDoorCursor(), DoorHotspot, CursorMode.Auto);
            return;
        }

        if (edgePanDirection < 0)
        {
            Cursor.SetCursor(GetLeftArrowCursor(), ArrowHotspot, CursorMode.Auto);
            return;
        }

        if (edgePanDirection > 0)
        {
            Cursor.SetCursor(GetRightArrowCursor(), ArrowHotspot, CursorMode.Auto);
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

    private static Texture2D GetDoorCursor()
    {
        if (doorCursor == null)
        {
            doorCursor = CreateDoorCursor();
        }

        return doorCursor;
    }

    private static Texture2D GetStairwayCursor()
    {
        if (stairwayCursor == null)
        {
            stairwayCursor = CreateStairwayCursor();
        }

        return stairwayCursor;
    }

    private static Texture2D CreateArrowCursor(string cursorName, int direction)
    {
        Texture2D texture = CreateBlankCursor(cursorName);
        int tipX = direction < 0 ? 5 : 26;
        int backX = direction < 0 ? 20 : 11;

        DrawLine(texture, tipX, 16, backX, 5, Ink, 5);
        DrawLine(texture, tipX, 16, backX, 27, Ink, 5);
        DrawLine(texture, tipX, 16, direction < 0 ? 27 : 4, 16, Ink, 5);
        DrawLine(texture, tipX, 16, backX, 5, Paper, 2);
        DrawLine(texture, tipX, 16, backX, 27, Paper, 2);
        DrawLine(texture, tipX, 16, direction < 0 ? 27 : 4, 16, Paper, 2);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateDoorCursor()
    {
        Texture2D texture = CreateBlankCursor("Cursor_OpenDoor");

        // A tiny open-door icon: frame, swinging panel, and knob. It is generated
        // in code so the cursor works even before custom art assets are imported.
        FillRect(texture, 8, 5, 21, 28, Ink);
        FillRect(texture, 10, 7, 19, 26, Paper);
        DrawLine(texture, 10, 7, 24, 11, Ink, 3);
        DrawLine(texture, 24, 11, 24, 25, Ink, 3);
        DrawLine(texture, 24, 25, 10, 26, Ink, 3);
        DrawLine(texture, 10, 7, 10, 26, Ink, 3);
        DrawLine(texture, 12, 9, 22, 12, Paper, 1);
        DrawLine(texture, 22, 12, 22, 24, Paper, 1);
        DrawLine(texture, 22, 24, 12, 25, Paper, 1);
        FillRect(texture, 18, 16, 21, 19, Ink);
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateStairwayCursor()
    {
        Texture2D texture = CreateBlankCursor("Cursor_Stairway");

        // A compact stair-step icon, generated like the door cursor so it does
        // not need a separate imported texture to work in early builds.
        DrawLine(texture, 8, 24, 24, 24, Ink, 5);
        DrawLine(texture, 8, 24, 8, 19, Ink, 5);
        DrawLine(texture, 8, 19, 12, 19, Ink, 5);
        DrawLine(texture, 12, 19, 12, 15, Ink, 5);
        DrawLine(texture, 12, 15, 16, 15, Ink, 5);
        DrawLine(texture, 16, 15, 16, 11, Ink, 5);
        DrawLine(texture, 16, 11, 21, 11, Ink, 5);
        DrawLine(texture, 21, 11, 21, 7, Ink, 5);

        DrawLine(texture, 8, 24, 24, 24, Paper, 2);
        DrawLine(texture, 8, 24, 8, 19, Paper, 2);
        DrawLine(texture, 8, 19, 12, 19, Paper, 2);
        DrawLine(texture, 12, 19, 12, 15, Paper, 2);
        DrawLine(texture, 12, 15, 16, 15, Paper, 2);
        DrawLine(texture, 16, 15, 16, 11, Paper, 2);
        DrawLine(texture, 16, 11, 21, 11, Paper, 2);
        DrawLine(texture, 21, 11, 21, 7, Paper, 2);
        texture.Apply();
        return texture;
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
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
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

    private static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= CursorSize || y >= CursorSize)
        {
            return;
        }

        texture.SetPixel(x, y, color);
    }
}
