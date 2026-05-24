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
        ApplyCameraSettings();
    }

    private void OnEnable()
    {
        ApplyCameraSettings();
    }

    private void OnValidate()
    {
        ApplyCameraSettings();
    }

    private void LateUpdate()
    {
        ApplyCameraSettings();
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
        UniversalAdditionalCameraData cameraData = GetOrAddCameraData(bypassCamera);
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

        cameraData.renderType = CameraRenderType.Overlay;
        cameraData.renderPostProcessing = false;

        if (excludeRenderLayersFromSourceCamera && attachedToStack)
        {
            sourceCamera.cullingMask &= ~renderLayers.value;
        }
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

        cameraStack.RemoveAll(camera => camera == null || camera == bypassCamera);
        cameraStack.Add(bypassCamera);
        return true;
    }
}
