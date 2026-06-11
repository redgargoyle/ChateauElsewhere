using System;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum Chapter1SceneActionType
{
    FrontDoor,
    CoatCloset,
    GrandfatherClock,
    DrawingRoomExit
}

[DisallowMultipleComponent]
public class Chapter1SceneAction : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private const float FrontDoorClickScreenRadius = 90f;
    private const float FrontDoorReadyScreenDistance = 110f;

    [SerializeField] private Chapter1SceneActionType actionType;
    [SerializeField] private Chapter1ArrivalController arrivalController;
    [SerializeField] private GrandfatherClockInteraction clockInteraction;
    [SerializeField] private bool isActionAvailable = true;

    private int lastPerformedFrame = -1;
    private bool cursorHoverActive;
    private NavigationCursorController.HoverIcon cursorHoverIcon = NavigationCursorController.HoverIcon.Door;
    private PointClickPlayerMovement pendingFrontDoorApproachPlayer;

    public void Initialize(
        Chapter1SceneActionType nextActionType,
        Chapter1ArrivalController controller,
        GrandfatherClockInteraction clock)
    {
        actionType = nextActionType;
        arrivalController = controller;
        clockInteraction = clock;
    }

    public void SetAvailable(bool value)
    {
        isActionAvailable = value;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PerformAction();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UsesManualPointerPolling() &&
            TryGetPrimaryPointerPosition(out Vector2 screenPosition) &&
            IsPointerInsideActionBounds(screenPosition) &&
            IsActionCurrentlyAvailable())
        {
            SetDoorCursorHover(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetDoorCursorHover(false);
    }

    private void OnMouseDown()
    {
        PerformAction();
    }

    private void OnDisable()
    {
        CancelPendingFrontDoorApproach();
        SetDoorCursorHover(false);
    }

    private void Update()
    {
        if (!UsesManualPointerPolling())
        {
            return;
        }

        if (!TryGetPrimaryPointerPosition(out Vector2 screenPosition))
        {
            SetDoorCursorHover(false);
            return;
        }

        bool pointerInsideAction = IsPointerInsideActionBounds(screenPosition);
        SetDoorCursorHover(pointerInsideAction && IsActionCurrentlyAvailable());

        if (!TryGetPrimaryPointerDown())
        {
            return;
        }

        if (pointerInsideAction)
        {
            PerformAction();
            return;
        }

        if (actionType == Chapter1SceneActionType.FrontDoor)
        {
            CancelPendingFrontDoorApproach();
        }
    }

    private void PerformAction()
    {
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
            case Chapter1SceneActionType.GrandfatherClock:
                if (clockInteraction != null)
                {
                    clockInteraction.OpenCloseUp();
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

        if (!arrivalController.TryGetFrontDoorApproachDestination(playerMovement, out Vector2 approachDestination) &&
            !playerMovement.TryFindClosestReachableDestinationToWorldPoint(transform.position, out approachDestination))
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

        if (clockInteraction == null)
        {
            clockInteraction = FindAnyObjectByType<GrandfatherClockInteraction>(FindObjectsInactive.Include);
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

        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            return false;
        }

        Vector3 worldPosition = ScreenToWorldPointAtActionDepth(worldCamera, screenPosition);
        Collider2D[] colliders2D = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null &&
                colliders2D[i].enabled &&
                colliders2D[i].gameObject.activeInHierarchy &&
                colliders2D[i].OverlapPoint(worldPosition))
            {
                return true;
            }
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            Bounds bounds = spriteRenderers[i] != null ? spriteRenderers[i].bounds : default;

            if (spriteRenderers[i] != null &&
                spriteRenderers[i].enabled &&
                spriteRenderers[i].gameObject.activeInHierarchy &&
                worldPosition.x >= bounds.min.x &&
                worldPosition.x <= bounds.max.x &&
                worldPosition.y >= bounds.min.y &&
                worldPosition.y <= bounds.max.y)
            {
                return true;
            }
        }

        return false;
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

    private Vector3 ScreenToWorldPointAtActionDepth(Camera worldCamera, Vector2 screenPosition)
    {
        Ray pointerRay = worldCamera.ScreenPointToRay(screenPosition);
        Plane actionPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));

        if (actionPlane.Raycast(pointerRay, out float distance))
        {
            return pointerRay.GetPoint(distance);
        }

        float depth = Mathf.Abs(Vector3.Dot(transform.position - worldCamera.transform.position, worldCamera.transform.forward));
        depth = Mathf.Max(depth, worldCamera.nearClipPlane);
        return worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
    }

    private void SetDoorCursorHover(bool active)
    {
        if (!UsesManualPointerPolling())
        {
            active = false;
        }

        NavigationCursorController.HoverIcon nextIcon = GetActionHoverIcon();

        if (cursorHoverActive == active && cursorHoverIcon == nextIcon)
        {
            return;
        }

        cursorHoverActive = active;
        cursorHoverIcon = nextIcon;
        NavigationCursorController.SetDoorHover(this, nextIcon, active);
    }

    private bool UsesManualPointerPolling()
    {
        return actionType == Chapter1SceneActionType.FrontDoor ||
            actionType == Chapter1SceneActionType.CoatCloset;
    }

    private NavigationCursorController.HoverIcon GetActionHoverIcon()
    {
        if (actionType == Chapter1SceneActionType.CoatCloset)
        {
            ResolveReferences();
            return arrivalController != null && arrivalController.ButlerCarryingCoat
                ? NavigationCursorController.HoverIcon.Coat
                : NavigationCursorController.HoverIcon.BlockedCoat;
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
