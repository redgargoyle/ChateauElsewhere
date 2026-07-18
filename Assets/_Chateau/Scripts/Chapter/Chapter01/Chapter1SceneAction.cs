using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum Chapter1SceneActionType
{
    FrontDoor = 0,
    CoatCloset = 1,
    DrawingRoomExit = 3
}

[DisallowMultipleComponent]
public class Chapter1SceneAction : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private const float FrontDoorClickScreenRadius = 90f;
    private const float FrontDoorReadyScreenDistance = 110f;
    private const float DefaultSceneActionClickScreenRadius = 54f;
    private const float CoatClosetClickScreenRadius = 90f;
    private static readonly System.Collections.Generic.List<Chapter1SceneAction> ActiveSceneActions =
        new System.Collections.Generic.List<Chapter1SceneAction>();

    [SerializeField] private Chapter1SceneActionType actionType;
    [SerializeField] private Chapter1ArrivalController arrivalController;
    [SerializeField] private bool isActionAvailable = true;

    private int lastPerformedFrame = -1;
    private bool cursorHoverActive;
    private NavigationCursorController.HoverIcon cursorHoverIcon = NavigationCursorController.HoverIcon.Door;
    private PointClickPlayerMovement pendingFrontDoorApproachPlayer;
    private Collider2D[] cachedActionColliders2D;
    private SpriteRenderer[] cachedActionSpriteRenderers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetActiveSceneActionsForPlayMode()
    {
        ActiveSceneActions.Clear();
    }

    public void Initialize(Chapter1SceneActionType nextActionType, Chapter1ArrivalController controller)
    {
        actionType = nextActionType;
        arrivalController = controller;
        Chapter1PointerPriority.InvalidateCache();
    }

    public void SetAvailable(bool value)
    {
        isActionAvailable = value;
        Chapter1PointerPriority.InvalidateCache();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryHandlePointerAction(eventData.position, false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TryHandlePointerAction(eventData.position, true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        TryHandlePointerAction(eventData.position, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        RefreshPointerHover();
    }

    private void OnMouseDown()
    {
        if (TryGetPrimaryPointerPosition(out Vector2 screenPosition))
        {
            TryHandlePointerAction(screenPosition, false);
        }
    }

    private void OnEnable()
    {
        InvalidateActionBoundsComponents();

        if (!ActiveSceneActions.Contains(this))
        {
            ActiveSceneActions.Add(this);
        }

        Chapter1PointerPriority.InvalidateCache();
    }

    private void OnDisable()
    {
        ActiveSceneActions.Remove(this);
        Chapter1PointerPriority.InvalidateCache();
        CancelPendingFrontDoorApproach();
        SetDoorCursorHover(false);
    }

    private void OnTransformChildrenChanged()
    {
        InvalidateActionBoundsComponents();
        Chapter1PointerPriority.InvalidateCache();
    }

    private void Update()
    {
        if (RuntimeSettingsMenu.BlocksGameInput)
        {
            SetDoorCursorHover(false);
            return;
        }

        if (!TryGetPrimaryPointerPosition(out Vector2 screenPosition))
        {
            SetDoorCursorHover(false);
            return;
        }

        TryHandlePointerAction(screenPosition, TryGetPrimaryPointerDown());
    }

    private void RefreshPointerHover()
    {
        if (TryGetPrimaryPointerPosition(out Vector2 screenPosition))
        {
            TryHandlePointerAction(screenPosition, false);
            return;
        }

        SetDoorCursorHover(false);
    }

    private void TryHandlePointerAction(Vector2 screenPosition, bool activate)
    {
        if (RuntimeSettingsMenu.BlocksGameInput ||
            PointClickPlayerMovement.IsPointerOverBlockingUi(screenPosition))
        {
            SetDoorCursorHover(false);
            return;
        }

        bool isSelectedTarget = Chapter1PointerPriority.TryGetTarget(
            screenPosition,
            out MonoBehaviour target) && target == this;
        SetDoorCursorHover(isSelectedTarget && IsActionCurrentlyAvailable());

        if (!activate ||
            !isSelectedTarget ||
            !NavigationCursorController.IsPrimaryHoverOwner(this))
        {
            return;
        }

        PerformAction();
    }

    private void PerformAction()
    {
        if (RuntimeSettingsMenu.BlocksGameInput)
        {
            return;
        }

        if (actionType == Chapter1SceneActionType.FrontDoor && !IsCurrentPointerOnFrontDoor())
        {
            return;
        }

        if (lastPerformedFrame == Time.frameCount)
        {
            return;
        }

        lastPerformedFrame = Time.frameCount;

        if (!IsActionCurrentlyAvailable())
        {
            return;
        }

        ResolveReferences();

        switch (actionType)
        {
            case Chapter1SceneActionType.FrontDoor:
                StartFrontDoorApproach();
                break;
            case Chapter1SceneActionType.CoatCloset:
                if (arrivalController != null)
                {
                    arrivalController.HandleClosetClicked();
                }
                break;
            case Chapter1SceneActionType.DrawingRoomExit:
                if (arrivalController != null)
                {
                    arrivalController.TryCompleteChapterFromDrawingRoomExit();
                }
                break;
        }
    }

    private void StartFrontDoorApproach()
    {
        if (arrivalController == null)
        {
            return;
        }

        PointClickPlayerMovement playerMovement = FindButlerPlayerMovement();

        if (playerMovement == null)
        {
            return;
        }

        CancelPendingFrontDoorApproach();

        if (!arrivalController.TryGetFrontDoorApproachDestination(playerMovement, out Vector2 approachDestination))
        {
            return;
        }

        if (!playerMovement.TrySetDestination(approachDestination, true))
        {
            return;
        }

        if (!playerMovement.HasDestination)
        {
            if (IsPlayerCloseToFrontDoor(playerMovement))
            {
                arrivalController.AnswerFrontDoor();
            }

            return;
        }

        pendingFrontDoorApproachPlayer = playerMovement;
        pendingFrontDoorApproachPlayer.MovementStopped += HandleFrontDoorApproachStopped;
    }

    private void HandleFrontDoorApproachStopped()
    {
        PointClickPlayerMovement playerMovement = pendingFrontDoorApproachPlayer;
        CancelPendingFrontDoorApproach();

        if (arrivalController != null &&
            arrivalController.IsFrontDoorActionAvailable &&
            IsPlayerCloseToFrontDoor(playerMovement))
        {
            arrivalController.AnswerFrontDoor();
        }
    }

    private bool IsPlayerCloseToFrontDoor(PointClickPlayerMovement playerMovement)
    {
        if (arrivalController != null && arrivalController.IsButlerCloseToFrontDoor(playerMovement))
        {
            return true;
        }

        if (playerMovement == null ||
            !playerMovement.TryGetScreenPointFromLogicalPosition(playerMovement.LogicalPosition, out Vector2 playerScreenPosition))
        {
            return false;
        }

        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            return false;
        }

        Vector2 doorScreenPosition = worldCamera.WorldToScreenPoint(transform.position);
        return Vector2.Distance(playerScreenPosition, doorScreenPosition) <= FrontDoorReadyScreenDistance;
    }

    private static PointClickPlayerMovement FindButlerPlayerMovement()
    {
        GameObject namedPlayer = GameObject.Find("Player");
        PointClickPlayerMovement namedPlayerMovement = namedPlayer != null
            ? namedPlayer.GetComponent<PointClickPlayerMovement>()
            : null;

        if (namedPlayerMovement != null)
        {
            return namedPlayerMovement;
        }

        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Exclude);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null &&
                candidate.gameObject.activeInHierarchy &&
                candidate.gameObject.name.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (arrivalController == null)
        {
            arrivalController = FindAnyObjectByType<Chapter1ArrivalController>(FindObjectsInactive.Include);
        }

    }

    private bool IsActionCurrentlyAvailable()
    {
        if (!isActionAvailable)
        {
            return false;
        }

        if (actionType != Chapter1SceneActionType.FrontDoor)
        {
            return true;
        }

        ResolveReferences();
        return arrivalController != null && arrivalController.IsFrontDoorActionAvailable;
    }

    private void CancelPendingFrontDoorApproach()
    {
        if (pendingFrontDoorApproachPlayer == null)
        {
            return;
        }

        pendingFrontDoorApproachPlayer.MovementStopped -= HandleFrontDoorApproachStopped;
        pendingFrontDoorApproachPlayer = null;
    }

    private bool IsPointerInsideActionBounds(Vector2 screenPosition)
    {
        if (actionType == Chapter1SceneActionType.FrontDoor)
        {
            return IsFrontDoorPointerHit(screenPosition);
        }

        RectTransform rectTransform = transform as RectTransform;

        if (rectTransform != null)
        {
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
        }

        return IsPointerInsideScreenBounds(screenPosition);
    }

    public static bool TryGetSceneActionAtScreenPosition(
        Vector2 screenPosition,
        out Chapter1SceneAction sceneAction)
    {
        sceneAction = null;
        for (int i = ActiveSceneActions.Count - 1; i >= 0; i--)
        {
            Chapter1SceneAction candidate = ActiveSceneActions[i];

            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                ActiveSceneActions.RemoveAt(i);
                continue;
            }

            if (!candidate.IsActionCurrentlyAvailable() ||
                !candidate.IsPointerInsideActionBounds(screenPosition))
            {
                continue;
            }

            int candidatePriority = candidate.GetPointerPriority();

            if (sceneAction == null ||
                candidatePriority > sceneAction.GetPointerPriority() ||
                (candidatePriority == sceneAction.GetPointerPriority() &&
                    candidate.GetInstanceID() < sceneAction.GetInstanceID()))
            {
                sceneAction = candidate;
            }
        }

        return sceneAction != null;
    }

    private bool IsCurrentPointerOnFrontDoor()
    {
        return TryGetPrimaryPointerPosition(out Vector2 screenPosition) && IsFrontDoorPointerHit(screenPosition);
    }

    private bool IsFrontDoorPointerHit(Vector2 screenPosition)
    {
        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            return false;
        }

        Vector2 doorScreenPosition = worldCamera.WorldToScreenPoint(transform.position);
        return Vector2.Distance(screenPosition, doorScreenPosition) <= FrontDoorClickScreenRadius;
    }

    private bool IsPointerInsideScreenBounds(Vector2 screenPosition)
    {
        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            return false;
        }

        if (TryGetActionScreenBounds(worldCamera, out Vector2 min, out Vector2 max))
        {
            if (screenPosition.x >= min.x &&
                screenPosition.x <= max.x &&
                screenPosition.y >= min.y &&
                screenPosition.y <= max.y)
            {
                return true;
            }

            Vector2 center = (min + max) * 0.5f;
            return Vector2.Distance(screenPosition, center) <= GetMinimumScreenClickRadius();
        }

        Vector2 fallbackCenter = worldCamera.WorldToScreenPoint(transform.position);
        return Vector2.Distance(screenPosition, fallbackCenter) <= GetMinimumScreenClickRadius();
    }

    private bool TryGetActionScreenBounds(Camera worldCamera, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool hasBounds = false;

        CacheActionBoundsComponents();

        for (int i = 0; i < cachedActionColliders2D.Length; i++)
        {
            Collider2D collider2D = cachedActionColliders2D[i];

            if (collider2D == null ||
                !collider2D.enabled ||
                !collider2D.gameObject.activeInHierarchy)
            {
                continue;
            }

            AddWorldBoundsToScreenBounds(collider2D.bounds, worldCamera, ref min, ref max);
            hasBounds = true;
        }

        for (int i = 0; i < cachedActionSpriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = cachedActionSpriteRenderers[i];

            if (spriteRenderer == null ||
                !spriteRenderer.enabled ||
                !spriteRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            AddWorldBoundsToScreenBounds(spriteRenderer.bounds, worldCamera, ref min, ref max);
            hasBounds = true;
        }

        return hasBounds;
    }

    private void CacheActionBoundsComponents()
    {
        if (cachedActionColliders2D == null)
        {
            cachedActionColliders2D = GetComponentsInChildren<Collider2D>(true);
        }

        if (cachedActionSpriteRenderers == null)
        {
            cachedActionSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void InvalidateActionBoundsComponents()
    {
        cachedActionColliders2D = null;
        cachedActionSpriteRenderers = null;
    }

    private static void AddWorldBoundsToScreenBounds(Bounds bounds, Camera worldCamera, ref Vector2 min, ref Vector2 max)
    {
        AddScreenPointToBounds(worldCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z)), ref min, ref max);
        AddScreenPointToBounds(worldCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z)), ref min, ref max);
        AddScreenPointToBounds(worldCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z)), ref min, ref max);
        AddScreenPointToBounds(worldCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)), ref min, ref max);
    }

    private static void AddScreenPointToBounds(Vector2 point, ref Vector2 min, ref Vector2 max)
    {
        min = Vector2.Min(min, point);
        max = Vector2.Max(max, point);
    }

    private float GetMinimumScreenClickRadius()
    {
        return actionType == Chapter1SceneActionType.CoatCloset
            ? CoatClosetClickScreenRadius
            : DefaultSceneActionClickScreenRadius;
    }

    private void SetDoorCursorHover(bool active)
    {
        if (!active)
        {
            if (!cursorHoverActive)
            {
                return;
            }

            cursorHoverActive = false;
            NavigationCursorController.ClearDoorHover(this);
            return;
        }

        NavigationCursorController.HoverIcon nextIcon = GetActionHoverIcon();

        if (cursorHoverActive && cursorHoverIcon == nextIcon)
        {
            return;
        }

        cursorHoverActive = true;
        cursorHoverIcon = nextIcon;
        NavigationCursorController.SetDoorHover(
            this,
            nextIcon,
            GetPointerPriority(),
            true);
    }

    public static void ApplyPointerSelection(Chapter1SceneAction selectedAction)
    {
        for (int i = ActiveSceneActions.Count - 1; i >= 0; i--)
        {
            Chapter1SceneAction candidate = ActiveSceneActions[i];

            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                ActiveSceneActions.RemoveAt(i);
                continue;
            }

            candidate.SetDoorCursorHover(candidate == selectedAction);
        }
    }

    private int GetPointerPriority()
    {
        return actionType == Chapter1SceneActionType.FrontDoor
            ? NavigationCursorController.NavigationHoverPriority
            : NavigationCursorController.SceneActionHoverPriority;
    }

    private NavigationCursorController.HoverIcon GetActionHoverIcon()
    {
        if (actionType == Chapter1SceneActionType.CoatCloset)
        {
            ResolveReferences();
            return arrivalController != null && arrivalController.ButlerCarryingCoat
                ? NavigationCursorController.HoverIcon.PlaceHangCoat
                : NavigationCursorController.HoverIcon.Locked;
        }

        if (actionType == Chapter1SceneActionType.DrawingRoomExit)
        {
            return NavigationCursorController.HoverIcon.ExitLeaveRoom;
        }

        return NavigationCursorController.HoverIcon.Door;
    }

    private static bool TryGetPrimaryPointerPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            screenPosition = Input.mousePosition;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
#else
        return false;
#endif
    }

    private static bool TryGetPrimaryPointerDown()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            return Input.GetMouseButtonDown(0);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
#else
        return false;
#endif
    }
}
