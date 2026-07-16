using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ActorRoomState : MonoBehaviour
{
    private const string DiagnosticPrefix = "[Ch2ClickDiag]";
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

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
    [SerializeField] private bool followRoomStageMotion = true;

    private Renderer[] renderers = new Renderer[0];
    private Graphic[] graphics = new Graphic[0];
    private Collider[] colliders3D = new Collider[0];
    private Collider2D[] colliders2D = new Collider2D[0];
    private CanvasGroup[] canvasGroups = new CanvasGroup[0];
    private Animator[] animators = new Animator[0];
    private RoomProjectedEntity roomProjection;
    private CameraManager cameraManager;
    private Vector2 lastRoomStageScreenCenter;
    private float lastRoomStageScreenScale = 1f;
    private bool hasRoomStageScreenTransform;
    private bool hasRoomStageLocalBinding;
    private Vector2 roomStageLocalPoint;
    private float boundWorldZ;
    private string boundRoomId;
    private bool subscribedToRoomChanges;
    private bool hasDiagnosticApplyState;
    private bool lastDiagnosticShouldBeVisible;
    private bool lastDiagnosticShouldBeInteractable;

    public string ActorId => string.IsNullOrWhiteSpace(actorId) ? name : actorId;
    public string CurrentRoomId => currentRoomId;
    public bool IsAvailableInCurrentChapter => isAvailableInCurrentChapter;
    public bool IsVisibleByChapterState => isVisibleByChapterState;
    public bool IsInteractable => isInteractable;
    public bool IsSeated => isSeated;
    public bool IsVisibleInCurrentRoom => ShouldBeVisible();
    public RoomProjectedEntity Projection => GetRoomProjection();

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
        ClearRoomStageMotionBaseline();
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
        string cleanRoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();

        if (!SameRoom(currentRoomId, cleanRoomId))
        {
            ClearRoomStageMotionBaseline();
        }

        currentRoomId = cleanRoomId;
        ApplyState();
        GetRoomProjection()?.ApplyProjection();
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
        RefreshComponentCache();
        ApplySeatedAnimatorState();
    }

    public void ResetAnimatorToAuthoredState()
    {
        RefreshComponentCache();

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];

            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
            {
                continue;
            }

            animator.Rebind();
            animator.Update(0f);
        }
    }

    public void PlaceAt(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"Actor '{ActorId}' missing required waypoint target for PlaceAt.", this);
            return;
        }

        RoomProjectedEntity projection = GetRoomProjection();
        if (projection != null)
        {
            projection.UseProfileFromRoomTarget(target);

            if (projection.CanProjectTarget(target) &&
                projection.TrySetRoomLocalFootPointFromTarget(target))
            {
                if (projection.IsProjectionActive)
                {
                    ClearRoomStagePointBinding();
                    return;
                }
            }
        }

        Transform targetTransform = actorObject != null ? actorObject.transform : transform;
        Vector3 targetPosition = target.position;
        targetPosition.z = targetTransform.position.z;
        targetTransform.position = targetPosition;
        BindToRoomStagePoint(target);

        if (!hasRoomStageLocalBinding)
        {
            RegisterRoomStageMotionBaseline();
            return;
        }

        TryApplyRoomStageLocalBindingIfNeeded();
    }

    public void BindToRoomStagePoint(Transform roomTarget)
    {
        ResolveReferences();

        Transform targetTransform = actorObject != null ? actorObject.transform : transform;

        if (targetTransform == null || targetTransform is RectTransform)
        {
            return;
        }

        if (roomTarget == null)
        {
            ClearRoomStagePointBinding();
            return;
        }

        RoomContentGroup roomContentGroup = roomTarget.GetComponentInParent<RoomContentGroup>(true);
        RectTransform roomStage = roomContentGroup != null ? roomContentGroup.transform as RectTransform : null;

        if (roomStage == null)
        {
            ClearRoomStagePointBinding();
            return;
        }

        Vector3 localPoint = roomStage.InverseTransformPoint(roomTarget.position);
        roomStageLocalPoint = new Vector2(localPoint.x, localPoint.y);
        boundWorldZ = targetTransform.position.z;
        boundRoomId = roomContentGroup.RoomName;
        hasRoomStageLocalBinding = true;
        ClearRoomStageMotionBaseline();
    }

    public void ClearRoomStagePointBinding()
    {
        hasRoomStageLocalBinding = false;
        roomStageLocalPoint = Vector2.zero;
        boundWorldZ = 0f;
        boundRoomId = string.Empty;
        ClearRoomStageMotionBaseline();
    }

    public void ApplyState()
    {
        RefreshComponentCache();

        bool shouldBeVisible = ShouldBeVisible();
        bool shouldBeInteractable = shouldBeVisible && isInteractable;

        ApplySeatedAnimatorState();

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

        LogGuestApplyStateChangeIfNeeded(shouldBeVisible, shouldBeInteractable);

        if (shouldBeVisible)
        {
            RegisterRoomStageMotionBaselineIfMissing();
        }
        else
        {
            ClearRoomStageMotionBaseline();
        }
    }

    private void LateUpdate()
    {
        ApplyRoomStageMotionDeltaIfNeeded();
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

        if (cameraManager == null)
        {
            cameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);
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
        animators = root.GetComponentsInChildren<Animator>(true);
        roomProjection = root.GetComponentInChildren<RoomProjectedEntity>(true);
    }

    private void ApplySeatedAnimatorState()
    {
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];

            if (!CanSetAnimatorBool(animator, IsCrouchingHash))
            {
                continue;
            }

            animator.SetBool(IsCrouchingHash, isSeated);
        }
    }

    private static bool CanSetAnimatorBool(Animator animator, int parameterHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                return true;
            }
        }

        return false;
    }

    private void LogGuestApplyStateChangeIfNeeded(bool shouldBeVisible, bool shouldBeInteractable)
    {
        if (!IsGuestDiagnosticActor())
        {
            return;
        }

        if (hasDiagnosticApplyState &&
            lastDiagnosticShouldBeVisible == shouldBeVisible &&
            lastDiagnosticShouldBeInteractable == shouldBeInteractable)
        {
            return;
        }

        hasDiagnosticApplyState = true;
        lastDiagnosticShouldBeVisible = shouldBeVisible;
        lastDiagnosticShouldBeInteractable = shouldBeInteractable;

        Debug.Log(
            $"{DiagnosticPrefix} ActorRoomState ApplyState result-changed frame={Time.frameCount} " +
            $"actor={ActorId} currentRoomId={FormatDiagnosticValue(currentRoomId)} " +
            $"navigationCurrentRoom={GetNavigationCurrentRoomForDiagnostic()} " +
            $"available={isAvailableInCurrentChapter} visibleByChapter={isVisibleByChapterState} " +
            $"interactable={isInteractable} shouldBeVisible={shouldBeVisible} " +
            $"shouldBeInteractable={shouldBeInteractable} enabledCollider2DCount={CountEnabledCollider2D()}",
            this);
    }

    private bool IsGuestDiagnosticActor()
    {
        return ContainsGuest(ActorId) ||
            ContainsGuest(name) ||
            (actorObject != null && ContainsGuest(actorObject.name));
    }

    private static bool ContainsGuest(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf("Guest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private int CountEnabledCollider2D()
    {
        int count = 0;

        for (int i = 0; i < colliders2D.Length; i++)
        {
            if (colliders2D[i] != null && colliders2D[i].enabled)
            {
                count++;
            }
        }

        return count;
    }

    private string GetNavigationCurrentRoomForDiagnostic()
    {
        return navigationManager == null || string.IsNullOrWhiteSpace(navigationManager.CurrentRoom)
            ? "<none>"
            : navigationManager.CurrentRoom;
    }

    private static string FormatDiagnosticValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value.Trim();
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
        ClearRoomStageMotionBaseline();
        ApplyState();
    }

    private void ApplyRoomStageMotionDeltaIfNeeded()
    {
        if (!ShouldFollowRoomStageMotion())
        {
            ClearRoomStageMotionBaseline();
            return;
        }

        if (hasRoomStageLocalBinding)
        {
            if (TryApplyRoomStageLocalBindingIfNeeded())
            {
                ClearRoomStageMotionBaseline();
            }

            return;
        }

        Camera mainCamera = Camera.main;

        if (!TryGetCurrentRoomStageScreenTransform(out Vector2 currentCenter, out float currentScale) ||
            mainCamera == null)
        {
            ClearRoomStageMotionBaseline();
            return;
        }

        if (!hasRoomStageScreenTransform)
        {
            lastRoomStageScreenCenter = currentCenter;
            lastRoomStageScreenScale = currentScale;
            hasRoomStageScreenTransform = true;
            return;
        }

        Transform targetTransform = actorObject != null ? actorObject.transform : transform;

        if (targetTransform != null)
        {
            Vector3 actorScreen = mainCamera.WorldToScreenPoint(targetTransform.position);
            Vector2 previousActorScreenPosition = new Vector2(actorScreen.x, actorScreen.y);
            Vector2 previousRoomLocalScreenOffset = previousActorScreenPosition - lastRoomStageScreenCenter;
            float scaleRatio = currentScale / Mathf.Max(0.0001f, lastRoomStageScreenScale);
            Vector2 currentActorScreenPosition = currentCenter + previousRoomLocalScreenOffset * scaleRatio;
            Vector2 screenDelta = currentActorScreenPosition - previousActorScreenPosition;

            if (screenDelta.sqrMagnitude > 0.0001f)
            {
                Vector3 correctedWorldPosition = mainCamera.ScreenToWorldPoint(new Vector3(
                    currentActorScreenPosition.x,
                    currentActorScreenPosition.y,
                    actorScreen.z));

                correctedWorldPosition.z = targetTransform.position.z;
                targetTransform.position = correctedWorldPosition;
            }

        }

        lastRoomStageScreenCenter = currentCenter;
        lastRoomStageScreenScale = currentScale;
    }

    private void RegisterRoomStageMotionBaselineIfMissing()
    {
        if (hasRoomStageScreenTransform)
        {
            return;
        }

        RegisterRoomStageMotionBaseline();
    }

    private void RegisterRoomStageMotionBaseline()
    {
        if (!ShouldFollowRoomStageMotion() ||
            !TryGetCurrentRoomStageScreenTransform(out Vector2 currentCenter, out float currentScale))
        {
            ClearRoomStageMotionBaseline();
            return;
        }

        lastRoomStageScreenCenter = currentCenter;
        lastRoomStageScreenScale = currentScale;
        hasRoomStageScreenTransform = true;
    }

    private void ClearRoomStageMotionBaseline()
    {
        lastRoomStageScreenCenter = Vector2.zero;
        lastRoomStageScreenScale = 1f;
        hasRoomStageScreenTransform = false;
    }

    private bool ShouldFollowRoomStageMotion()
    {
        if (!Application.isPlaying || !followRoomStageMotion || !ShouldBeVisible())
        {
            return false;
        }

        Transform targetTransform = actorObject != null ? actorObject.transform : transform;
        return targetTransform != null &&
            targetTransform is not RectTransform &&
            !IsActorUnderRoomStage(targetTransform) &&
            !HasActiveProjection();
    }

    private RoomProjectedEntity GetRoomProjection()
    {
        if (roomProjection != null)
        {
            return roomProjection;
        }

        GameObject root = actorObject != null ? actorObject : gameObject;
        roomProjection = root != null ? root.GetComponentInChildren<RoomProjectedEntity>(true) : null;
        return roomProjection;
    }

    private bool HasActiveProjection()
    {
        RoomProjectedEntity projection = GetRoomProjection();
        return projection != null && projection.IsProjectionActive;
    }

    private bool TryApplyRoomStageLocalBindingIfNeeded()
    {
        if (!hasRoomStageLocalBinding)
        {
            return false;
        }

        GameObject targetObject = actorObject != null ? actorObject : gameObject;
        Transform targetTransform = targetObject != null ? targetObject.transform : transform;

        if (targetTransform == null ||
            targetTransform is RectTransform ||
            IsActorUnderRoomStage(targetTransform) ||
            (!string.IsNullOrWhiteSpace(boundRoomId) && !SameRoom(currentRoomId, boundRoomId)))
        {
            return false;
        }

        ResolveReferences();

        Camera mainCamera = Camera.main;
        if (cameraManager == null || mainCamera == null)
        {
            return false;
        }

        float depth = boundWorldZ - mainCamera.transform.position.z;

        if (depth <= 0.01f)
        {
            depth = Mathf.Abs(targetTransform.position.z - mainCamera.transform.position.z);
        }

        if (depth <= 0.01f)
        {
            depth = Mathf.Abs(transform.position.z - mainCamera.transform.position.z);
        }

        if (depth <= 0.01f)
        {
            depth = 10f;
        }

        if (!cameraManager.TryGetActiveRoomStageWorldPoint(
            roomStageLocalPoint,
            depth,
            out Vector3 worldPoint,
            out _))
        {
            return false;
        }

        worldPoint.z = boundWorldZ;
        targetTransform.position = worldPoint;

        if (CharacterFootPositionUtility.TryGetWorldPoint(targetObject, true, false, out Vector3 feetWorldPoint))
        {
            Vector3 footCorrection = worldPoint - feetWorldPoint;
            footCorrection.z = 0f;
            targetTransform.position += footCorrection;
        }

        return true;
    }

    public bool TryGetRoomLocalFootPoint(out string roomId, out Vector2 roomLocalFootPoint)
    {
        roomId = string.Empty;
        roomLocalFootPoint = Vector2.zero;

        if (hasRoomStageLocalBinding && !string.IsNullOrWhiteSpace(boundRoomId))
        {
            roomId = boundRoomId;
            roomLocalFootPoint = roomStageLocalPoint;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(currentRoomId))
        {
            roomId = currentRoomId;
        }

        RoomContentGroup parentRoom = GetComponentInParent<RoomContentGroup>(true);

        if (string.IsNullOrWhiteSpace(roomId) &&
            parentRoom != null &&
            !string.IsNullOrWhiteSpace(parentRoom.RoomName))
        {
            roomId = parentRoom.RoomName;
        }

        GameObject targetObject = actorObject != null ? actorObject : gameObject;
        Transform targetTransform = targetObject != null ? targetObject.transform : transform;
        Vector3 footWorldPoint;

        if (!CharacterFootPositionUtility.TryGetWorldPoint(targetObject, true, false, out footWorldPoint))
        {
            footWorldPoint = targetTransform != null ? targetTransform.position : transform.position;
        }

        if (parentRoom != null)
        {
            Vector3 localPoint = parentRoom.transform.InverseTransformPoint(footWorldPoint);
            roomLocalFootPoint = new Vector2(localPoint.x, localPoint.y);
            return !string.IsNullOrWhiteSpace(roomId);
        }

        ResolveReferences();

        if (cameraManager != null &&
            targetTransform != null &&
            cameraManager.TryGetActiveRoomStageLocalPoint(footWorldPoint, out roomLocalFootPoint))
        {
            return !string.IsNullOrWhiteSpace(roomId);
        }

        return false;
    }

    private static bool IsActorUnderRoomStage(Transform targetTransform)
    {
        return targetTransform != null &&
            targetTransform.GetComponentInParent<RoomContentGroup>(true) != null;
    }

    private bool TryGetCurrentRoomStageScreenTransform(out Vector2 stageCenter, out float stageScale)
    {
        stageCenter = Vector2.zero;
        stageScale = 1f;
        ResolveReferences();

        return cameraManager != null &&
            cameraManager.TryGetRoomStageScreenTransform(out _, out stageCenter, out stageScale);
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
