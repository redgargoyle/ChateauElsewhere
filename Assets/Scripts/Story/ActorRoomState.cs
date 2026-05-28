using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ActorRoomState : MonoBehaviour
{
    [Header("Actor")]
    [SerializeField] private string actorId;
    [SerializeField] private GameObject actorObject;
    [SerializeField] private string currentRoomId;

    [Header("Chapter State")]
    [SerializeField] private bool isAvailableInCurrentChapter = true;
    [SerializeField] private bool isVisibleByChapterState = true;
    [SerializeField] private bool isInteractable = true;
    [SerializeField] private bool isSeated;

    [Header("Room Visibility")]
    [SerializeField] private bool restrictVisibilityToCurrentRoom = true;
    [SerializeField] private RoomNavigationManager navigationManager;

    private Renderer[] renderers = new Renderer[0];
    private Graphic[] graphics = new Graphic[0];
    private Collider[] colliders3D = new Collider[0];
    private Collider2D[] colliders2D = new Collider2D[0];
    private CanvasGroup[] canvasGroups = new CanvasGroup[0];
    private bool subscribedToRoomChanges;

    public string ActorId => string.IsNullOrWhiteSpace(actorId) ? name : actorId;
    public string CurrentRoomId => currentRoomId;
    public bool IsAvailableInCurrentChapter => isAvailableInCurrentChapter;
    public bool IsVisibleByChapterState => isVisibleByChapterState;
    public bool IsInteractable => isInteractable;
    public bool IsSeated => isSeated;
    public bool IsVisibleInCurrentRoom => ShouldBeVisible();

    private void Reset()
    {
        actorObject = gameObject;
        actorId = name;
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshComponentCache();
        SubscribeToRoomChanges();
        ApplyState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshComponentCache();
        SubscribeToRoomChanges();
        ApplyState();
    }

    private void OnDisable()
    {
        UnsubscribeFromRoomChanges();
    }

    private void OnValidate()
    {
        if (actorObject == null)
        {
            actorObject = gameObject;
        }

        if (string.IsNullOrWhiteSpace(actorId))
        {
            actorId = name;
        }
    }

    public void SetActorId(string value)
    {
        actorId = string.IsNullOrWhiteSpace(value) ? name : value.Trim();
    }

    public void SetCurrentRoom(string roomId)
    {
        currentRoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
        ApplyState();
    }

    public void SetAvailableInCurrentChapter(bool value)
    {
        isAvailableInCurrentChapter = value;
        ApplyState();
    }

    public void SetVisibleByChapterState(bool value)
    {
        isVisibleByChapterState = value;
        ApplyState();
    }

    public void SetInteractable(bool value)
    {
        isInteractable = value;
        ApplyState();
    }

    public void SetSeated(bool value)
    {
        isSeated = value;
    }

    public void PlaceAt(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"Actor '{ActorId}' missing required waypoint target for PlaceAt.", this);
            return;
        }

        Transform targetTransform = actorObject != null ? actorObject.transform : transform;
        targetTransform.position = target.position;
    }

    public void ApplyState()
    {
        RefreshComponentCache();

        bool shouldBeVisible = ShouldBeVisible();
        bool shouldBeInteractable = shouldBeVisible && isInteractable;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = shouldBeVisible;
            }
        }

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].enabled = shouldBeVisible;
                graphics[i].raycastTarget = shouldBeInteractable;
            }
        }

        for (int i = 0; i < colliders3D.Length; i++)
        {
            if (colliders3D[i] != null)
            {
                colliders3D[i].enabled = shouldBeInteractable;
            }
        }

        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null)
            {
                colliders2D[i].enabled = shouldBeInteractable;
            }
        }

        for (int i = 0; i < canvasGroups.Length; i++)
        {
            if (canvasGroups[i] == null)
            {
                continue;
            }

            canvasGroups[i].alpha = shouldBeVisible ? 1f : 0f;
            canvasGroups[i].interactable = shouldBeInteractable;
            canvasGroups[i].blocksRaycasts = shouldBeInteractable;
        }
    }

    private bool ShouldBeVisible()
    {
        if (!isAvailableInCurrentChapter || !isVisibleByChapterState)
        {
            return false;
        }

        if (!restrictVisibilityToCurrentRoom || string.IsNullOrWhiteSpace(currentRoomId))
        {
            return true;
        }

        ResolveReferences();

        if (navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom))
        {
            return true;
        }

        return SameRoom(currentRoomId, navigationManager.CurrentRoom);
    }

    private void ResolveReferences()
    {
        if (actorObject == null)
        {
            actorObject = gameObject;
        }

        if (navigationManager == null)
        {
            navigationManager = FindAnyObjectByType<RoomNavigationManager>(FindObjectsInactive.Include);
        }
    }

    private void RefreshComponentCache()
    {
        GameObject root = actorObject != null ? actorObject : gameObject;

        renderers = root.GetComponentsInChildren<Renderer>(true);
        graphics = root.GetComponentsInChildren<Graphic>(true);
        colliders3D = root.GetComponentsInChildren<Collider>(true);
        colliders2D = root.GetComponentsInChildren<Collider2D>(true);
        canvasGroups = root.GetComponentsInChildren<CanvasGroup>(true);
    }

    private void SubscribeToRoomChanges()
    {
        if (subscribedToRoomChanges)
        {
            return;
        }

        ResolveReferences();

        if (navigationManager == null)
        {
            return;
        }

        navigationManager.OnCurrentRoomChanged.AddListener(HandleRoomChanged);
        subscribedToRoomChanges = true;
    }

    private void UnsubscribeFromRoomChanges()
    {
        if (!subscribedToRoomChanges || navigationManager == null)
        {
            subscribedToRoomChanges = false;
            return;
        }

        navigationManager.OnCurrentRoomChanged.RemoveListener(HandleRoomChanged);
        subscribedToRoomChanges = false;
    }

    private void HandleRoomChanged(string roomName)
    {
        ApplyState();
    }

    private static bool SameRoom(string left, string right)
    {
        return string.Equals(NormalizeRoomName(left), NormalizeRoomName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoomName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty);
    }
}
