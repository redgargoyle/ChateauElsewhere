using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class PostProcessBypassCamera : MonoBehaviour
{
    public const string DefaultCameraName = "Camera_NoPostProcessFlame";

    [SerializeField] private Camera sourceCamera;
    [SerializeField] private LayerMask renderLayers;
    [SerializeField] private float depthOffset = 1f;
    [SerializeField] private bool excludeRenderLayersFromSourceCamera = true;

    private Camera bypassCamera;
    private UniversalAdditionalCameraData bypassCameraData;
    private SourceCameraState lastSourceCameraState;
    private bool hasSourceCameraState;

    public Camera SourceCamera => sourceCamera;
    public LayerMask RenderLayers => renderLayers;

    public static PostProcessBypassCamera EnsureForCamera(Camera source, LayerMask layers)
    {
        if (source == null)
        {
            return null;
        }

        PostProcessBypassCamera rig = FindExisting(source);

        if (rig == null)
        {
            GameObject cameraObject = new GameObject(DefaultCameraName, typeof(Camera), typeof(UniversalAdditionalCameraData), typeof(PostProcessBypassCamera));
            rig = cameraObject.GetComponent<PostProcessBypassCamera>();
        }

        rig.Configure(source, layers);
        return rig;
    }

    public void Configure(Camera source, LayerMask layers)
    {
        sourceCamera = source;
        renderLayers = layers;
        hasSourceCameraState = false;
        ApplyCameraSettings();
    }

    private void OnEnable()
    {
        hasSourceCameraState = false;
        ApplyCameraSettings();
    }

    private void OnValidate()
    {
        hasSourceCameraState = false;
        ApplyCameraSettings();
    }

    private void LateUpdate()
    {
        if (HasSourceCameraSettingsChanged())
        {
            ApplyCameraSettings();
        }
    }

    private static PostProcessBypassCamera FindExisting(Camera source)
    {
        PostProcessBypassCamera[] rigs = FindObjectsByType<PostProcessBypassCamera>(FindObjectsInactive.Include);

        for (int i = 0; i < rigs.Length; i++)
        {
            if (rigs[i] != null && (rigs[i].sourceCamera == source || rigs[i].name == DefaultCameraName))
            {
                return rigs[i];
            }
        }

        return null;
    }

    private void ApplyCameraSettings()
    {
        if (bypassCamera == null)
        {
            bypassCamera = GetComponent<Camera>();
        }

        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        if (bypassCamera == null || sourceCamera == null || sourceCamera == bypassCamera)
        {
            return;
        }

        transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
        transform.localScale = Vector3.one;

        UniversalAdditionalCameraData sourceData = GetOrAddCameraData(sourceCamera);
        bypassCameraData = GetOrAddCameraData(bypassCamera);
        bool attachedToStack = AttachToSourceCameraStack(sourceData);

        bypassCamera.enabled = sourceCamera.enabled && attachedToStack;
        bypassCamera.clearFlags = CameraClearFlags.Nothing;
        bypassCamera.backgroundColor = Color.clear;
        bypassCamera.cullingMask = renderLayers.value;
        bypassCamera.depth = sourceCamera.depth + depthOffset;
        bypassCamera.orthographic = sourceCamera.orthographic;
        bypassCamera.orthographicSize = sourceCamera.orthographicSize;
        bypassCamera.fieldOfView = sourceCamera.fieldOfView;
        bypassCamera.nearClipPlane = sourceCamera.nearClipPlane;
        bypassCamera.farClipPlane = sourceCamera.farClipPlane;
        bypassCamera.rect = sourceCamera.rect;
        bypassCamera.targetTexture = sourceCamera.targetTexture;
        bypassCamera.targetDisplay = sourceCamera.targetDisplay;
        bypassCamera.allowHDR = sourceCamera.allowHDR;
        bypassCamera.allowMSAA = sourceCamera.allowMSAA;
        bypassCamera.useOcclusionCulling = sourceCamera.useOcclusionCulling;

        bypassCameraData.renderType = CameraRenderType.Overlay;
        bypassCameraData.renderPostProcessing = false;

        if (excludeRenderLayersFromSourceCamera && attachedToStack)
        {
            sourceCamera.cullingMask &= ~renderLayers.value;
        }

        lastSourceCameraState = SourceCameraState.Capture(sourceCamera);
        hasSourceCameraState = true;
    }

    private static UniversalAdditionalCameraData GetOrAddCameraData(Camera camera)
    {
        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();

        if (cameraData == null)
        {
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }

        return cameraData;
    }

    private bool AttachToSourceCameraStack(UniversalAdditionalCameraData sourceData)
    {
        if (sourceData == null)
        {
            return false;
        }

        sourceData.renderType = CameraRenderType.Base;
        System.Collections.Generic.List<Camera> cameraStack = sourceData.cameraStack;

        if (cameraStack == null)
        {
            return false;
        }

        for (int i = cameraStack.Count - 1; i >= 0; i--)
        {
            if (cameraStack[i] == null)
            {
                cameraStack.RemoveAt(i);
            }
        }

        bool keepLastEntry = cameraStack.Count > 0 && cameraStack[cameraStack.Count - 1] == bypassCamera;
        int keptIndex = keepLastEntry ? cameraStack.Count - 1 : -1;

        for (int i = cameraStack.Count - 1; i >= 0; i--)
        {
            if (cameraStack[i] == bypassCamera && i != keptIndex)
            {
                cameraStack.RemoveAt(i);
            }
        }

        if (!keepLastEntry)
        {
            cameraStack.Add(bypassCamera);
        }

        return true;
    }

    private bool HasSourceCameraSettingsChanged()
    {
        if (bypassCamera == null)
        {
            bypassCamera = GetComponent<Camera>();
        }

        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        if (bypassCamera == null || sourceCamera == null || sourceCamera == bypassCamera)
        {
            return false;
        }

        UniversalAdditionalCameraData sourceData = sourceCamera.GetComponent<UniversalAdditionalCameraData>();

        if (!IsBypassCameraStackValid(sourceData))
        {
            return true;
        }

        SourceCameraState currentState = SourceCameraState.Capture(sourceCamera);
        return !hasSourceCameraState ||
            !lastSourceCameraState.Matches(currentState) ||
            !BypassCameraMatchesSource();
    }

    private bool IsBypassCameraStackValid(UniversalAdditionalCameraData sourceData)
    {
        if (sourceData == null ||
            sourceData.renderType != CameraRenderType.Base ||
            sourceData.cameraStack == null)
        {
            return false;
        }

        System.Collections.Generic.List<Camera> cameraStack = sourceData.cameraStack;

        if (cameraStack.Count == 0 || cameraStack[cameraStack.Count - 1] != bypassCamera)
        {
            return false;
        }

        for (int i = 0; i < cameraStack.Count - 1; i++)
        {
            if (cameraStack[i] == null || cameraStack[i] == bypassCamera)
            {
                return false;
            }
        }

        return true;
    }

    private bool BypassCameraMatchesSource()
    {
        if (bypassCameraData == null)
        {
            bypassCameraData = bypassCamera.GetComponent<UniversalAdditionalCameraData>();
        }

        return bypassCameraData != null &&
            transform.position == sourceCamera.transform.position &&
            transform.rotation == sourceCamera.transform.rotation &&
            transform.localScale == Vector3.one &&
            bypassCamera.enabled == sourceCamera.enabled &&
            bypassCamera.clearFlags == CameraClearFlags.Nothing &&
            bypassCamera.backgroundColor == Color.clear &&
            bypassCamera.cullingMask == renderLayers.value &&
            Mathf.Approximately(bypassCamera.depth, sourceCamera.depth + depthOffset) &&
            bypassCamera.orthographic == sourceCamera.orthographic &&
            Mathf.Approximately(bypassCamera.orthographicSize, sourceCamera.orthographicSize) &&
            Mathf.Approximately(bypassCamera.fieldOfView, sourceCamera.fieldOfView) &&
            Mathf.Approximately(bypassCamera.nearClipPlane, sourceCamera.nearClipPlane) &&
            Mathf.Approximately(bypassCamera.farClipPlane, sourceCamera.farClipPlane) &&
            bypassCamera.rect == sourceCamera.rect &&
            bypassCamera.targetTexture == sourceCamera.targetTexture &&
            bypassCamera.targetDisplay == sourceCamera.targetDisplay &&
            bypassCamera.allowHDR == sourceCamera.allowHDR &&
            bypassCamera.allowMSAA == sourceCamera.allowMSAA &&
            bypassCamera.useOcclusionCulling == sourceCamera.useOcclusionCulling &&
            bypassCameraData.renderType == CameraRenderType.Overlay &&
            !bypassCameraData.renderPostProcessing;
    }

    private readonly struct SourceCameraState
    {
        private SourceCameraState(Camera camera)
        {
            Position = camera.transform.position;
            Rotation = camera.transform.rotation;
            Enabled = camera.enabled;
            CullingMask = camera.cullingMask;
            Depth = camera.depth;
            Orthographic = camera.orthographic;
            OrthographicSize = camera.orthographicSize;
            FieldOfView = camera.fieldOfView;
            NearClipPlane = camera.nearClipPlane;
            FarClipPlane = camera.farClipPlane;
            Rect = camera.rect;
            TargetTexture = camera.targetTexture;
            TargetDisplay = camera.targetDisplay;
            AllowHdr = camera.allowHDR;
            AllowMsaa = camera.allowMSAA;
            UseOcclusionCulling = camera.useOcclusionCulling;
        }

        private Vector3 Position { get; }
        private Quaternion Rotation { get; }
        private bool Enabled { get; }
        private int CullingMask { get; }
        private float Depth { get; }
        private bool Orthographic { get; }
        private float OrthographicSize { get; }
        private float FieldOfView { get; }
        private float NearClipPlane { get; }
        private float FarClipPlane { get; }
        private Rect Rect { get; }
        private RenderTexture TargetTexture { get; }
        private int TargetDisplay { get; }
        private bool AllowHdr { get; }
        private bool AllowMsaa { get; }
        private bool UseOcclusionCulling { get; }

        public static SourceCameraState Capture(Camera camera)
        {
            return new SourceCameraState(camera);
        }

        public bool Matches(SourceCameraState other)
        {
            return Position == other.Position &&
                Rotation == other.Rotation &&
                Enabled == other.Enabled &&
                CullingMask == other.CullingMask &&
                Mathf.Approximately(Depth, other.Depth) &&
                Orthographic == other.Orthographic &&
                Mathf.Approximately(OrthographicSize, other.OrthographicSize) &&
                Mathf.Approximately(FieldOfView, other.FieldOfView) &&
                Mathf.Approximately(NearClipPlane, other.NearClipPlane) &&
                Mathf.Approximately(FarClipPlane, other.FarClipPlane) &&
                Rect == other.Rect &&
                TargetTexture == other.TargetTexture &&
                TargetDisplay == other.TargetDisplay &&
                AllowHdr == other.AllowHdr &&
                AllowMsaa == other.AllowMsaa &&
                UseOcclusionCulling == other.UseOcclusionCulling;
        }
    }
}
