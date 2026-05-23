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
        Camera[] cameras = Object.FindObjectsByType<Camera>();

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

            cameraData.renderPostProcessing = true;
        }
    }
}
