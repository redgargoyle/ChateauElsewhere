using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class UrpPostProcessingBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnableAfterSceneLoad()
    {
        EnablePostProcessingOnCameras();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnablePostProcessingOnCameras();
    }

    private static void EnablePostProcessingOnCameras()
    {
        int noPostProcessFlameLayer = LayerMask.NameToLayer(NoPostProcessRenderLayer.DefaultLayerName);
        ConfigureSceneFlames(noPostProcessFlameLayer);

        LayerMask bypassLayers = new LayerMask
        {
            value = noPostProcessFlameLayer >= 0 ? 1 << noPostProcessFlameLayer : 0
        };
        Camera mainCamera = Camera.main;

        if (mainCamera != null && bypassLayers.value != 0)
        {
            PostProcessBypassCamera.EnsureForCamera(mainCamera, bypassLayers);
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>();

        foreach (Camera camera in cameras)
        {
            if (camera == null)
            {
                continue;
            }

            camera.allowHDR = true;

            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            if (camera.GetComponent<PostProcessBypassCamera>() != null)
            {
                cameraData.renderPostProcessing = false;
                continue;
            }

            if (bypassLayers.value != 0)
            {
                camera.cullingMask &= ~bypassLayers.value;
            }

            cameraData.renderPostProcessing = true;
        }
    }

    private static void ConfigureSceneFlames(int layer)
    {
        ParticleSystem[] particleSystems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];

            if (!FlameLocalLight.IsLikelyFlame(particleSystem))
            {
                continue;
            }

            FlameLocalLight localLight = FlameLocalLight.EnsureFor(particleSystem);

            if (localLight == null && layer >= 0)
            {
                SetLayerRecursivelyExceptLocalLight(particleSystem.transform, layer);
            }
        }
    }

    private static void SetLayerRecursivelyExceptLocalLight(Transform root, int layer)
    {
        if (root == null || root.name == FlameLocalLight.LightObjectName)
        {
            return;
        }

        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursivelyExceptLocalLight(root.GetChild(i), layer);
        }
    }
}
