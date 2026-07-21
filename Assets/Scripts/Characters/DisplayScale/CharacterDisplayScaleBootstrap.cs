using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the single display-scale controller when a scene containing managed
/// Butler/Guest subjects loads. The controller persists across scene changes.
/// </summary>
public static class CharacterDisplayScaleBootstrap
{
    private const string ControllerObjectName = "Character Display Scale Controller";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureControllerForLoadedSubjects();
    }

    internal static void EnsureControllerForLoadedSubjects()
    {
        if (CharacterDisplayScaleController.ActiveController != null ||
            Object.FindAnyObjectByType<CharacterDisplayScaleSubject>(FindObjectsInactive.Include) == null)
        {
            return;
        }

        CharacterDisplayScaleCatalog catalog = CharacterDisplayScaleCatalog.LoadDefault();

        if (catalog == null)
        {
            Debug.LogError(
                $"Missing universal character display-scale catalog in Resources/{CharacterDisplayScaleCatalog.DefaultResourcePath}.");
            return;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        controllerObject.SetActive(false);
        CharacterDisplayScaleController controller =
            controllerObject.AddComponent<CharacterDisplayScaleController>();
        controller.Configure(catalog);
        Object.DontDestroyOnLoad(controllerObject);
        controllerObject.SetActive(true);
    }
}
