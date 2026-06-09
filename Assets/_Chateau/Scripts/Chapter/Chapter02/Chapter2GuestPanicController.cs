using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class Chapter2GuestPanicController : MonoBehaviour
{
    private const string ClickTargetName = "Ch2_ClickTarget";

    [SerializeField] private Chapter2GuestSearchController guestSearch;
    [SerializeField] private PointClickPlayerMovement playerMovement;
    [SerializeField] private Chapter2PanicAnimationLibrary animationLibrary;
    [SerializeField, Min(1f)] private float frameRate = 12f;
    [SerializeField, Min(0f)] private float runDistancePixels = 150f;
    [SerializeField, Min(1f)] private float panicRoamRadiusPixels = 190f;
    [SerializeField, Range(0.1f, 1f)] private float verticalRunDistanceScale = 0.55f;
    [SerializeField, Min(1f)] private float panicMoveSpeedPixels = 300f;
    [SerializeField, Min(0f)] private float jitterPixels = 3f;
    [SerializeField, Range(0f, 1f)] private float randomStopActionChance = 1f;
    [SerializeField, Min(0.0001f)] private float worldUnitsPerRoomPixel = 0.012f;
    [SerializeField] private bool logMissingFrames = true;

    private readonly List<PanicParticipant> participants = new List<PanicParticipant>();
    private readonly List<Vector2> routeQueryScratch = new List<Vector2>();
    private Coroutine panicRoutine;
    private bool isRunning;

    public bool IsRunning => isRunning || panicRoutine != null;

    public Coroutine BeginPanic()
    {
        if (panicRoutine != null)
        {
            return panicRoutine;
        }

        ResolveReferences();
        participants.Clear();
        BuildParticipants(participants);

        if (participants.Count == 0)
        {
            return null;
        }

        isRunning = true;
        ChooseRandomRunTargets();
        ApplyAssignedRunFrame(0, 0f, true);
        panicRoutine = StartCoroutine(RunPanicRoutine());
        return panicRoutine;
    }

    public void StopPanic()
    {
        if (panicRoutine != null)
        {
            StopCoroutine(panicRoutine);
            panicRoutine = null;
        }

        RestoreParticipants();

        if (Application.isPlaying)
        {
            Physics2D.SyncTransforms();
        }

        participants.Clear();
        isRunning = false;
    }

    private void OnDisable()
    {
        StopPanic();
    }

    private void LateUpdate()
    {
        if (!isRunning)
        {
            return;
        }

        ReapplyParticipantVisualOffsets();
    }

    private void ResolveReferences()
    {
        if (guestSearch == null)
        {
            guestSearch = GetComponent<Chapter2GuestSearchController>();
        }

        if (guestSearch == null)
        {
            guestSearch = FindAnyObjectByType<Chapter2GuestSearchController>(FindObjectsInactive.Include);
        }

        if (playerMovement == null)
        {
            playerMovement = FindPlayerMovement();
        }

        if (animationLibrary == null)
        {
            animationLibrary = Resources.Load<Chapter2PanicAnimationLibrary>(Chapter2PanicAnimationLibrary.ResourcesPath);
        }
    }

    private void BuildParticipants(List<PanicParticipant> targetParticipants)
    {
        if (guestSearch == null)
        {
            Debug.LogWarning("Chapter 2 guest panic requested, but Chapter2GuestSearchController is missing.", this);
            return;
        }

        if (animationLibrary == null)
        {
            Debug.LogError($"Chapter 2 guest panic requested, but Resources/{Chapter2PanicAnimationLibrary.ResourcesPath} is missing.", this);
            return;
        }

        List<ActorRoomState> orderedActors = guestSearch.GetGuestActorsInIdentityOrder();

        for (int i = 0; i < orderedActors.Count; i++)
        {
            ActorRoomState actorState = orderedActors[i];

            if (actorState == null || actorState.gameObject == null || !actorState.IsVisibleInCurrentRoom)
            {
                continue;
            }

            int guestNumber = TryGetGuestNumber(actorState, out int parsedGuestNumber)
                ? parsedGuestNumber
                : i + 1;

            if (!Chapter2PanicRoster.TryGetCharacterIdForGuestNumber(guestNumber, out string characterId))
            {
                continue;
            }

            string missingReport = "missing character entry";

            if (!animationLibrary.TryGetCharacter(characterId, out Chapter2PanicCharacterAnimation animation) ||
                !animation.HasRequiredFrames(out missingReport))
            {
                if (logMissingFrames)
                {
                    Debug.LogError($"Chapter 2 panic skipped guest {guestNumber} ({characterId}) because approved frames are incomplete: {missingReport}", this);
                }

                continue;
            }

            PanicParticipant participant = PanicParticipant.Create(actorState, animation);
            participant.ConfigureRunMotion(targetParticipants.Count, guestNumber);

            if (!participant.HasSpriteTarget)
            {
                Debug.LogWarning($"Chapter 2 panic skipped guest {guestNumber} ({characterId}) because no SpriteRenderer or Image target was found.", actorState);
                participant.Restore();
                continue;
            }

            participant.ApplyPanicState();
            targetParticipants.Add(participant);
        }
    }

    private IEnumerator RunPanicRoutine()
    {
        while (isRunning)
        {
            ChooseRandomRunTargets();
            yield return MoveParticipantsTowardAssignedTargets(true);
            yield return PlayRandomStopActions();
        }
    }

    private int ChooseRandomStopActions()
    {
        int maxFrameCount = 0;

        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant == null)
            {
                continue;
            }

            Sprite[] frames = GetFrames(participant.Animation, PanicAction.PanicHandsUp);

            if (frames == null || frames.Length == 0)
            {
                continue;
            }

            participant.SetStopFramePhase(UnityEngine.Random.Range(0, frames.Length));
            maxFrameCount = Mathf.Max(maxFrameCount, frames.Length);
        }

        return maxFrameCount;
    }

    private void ApplyRandomStopActionFrame(int frameIndex, float motionFrame)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant == null)
            {
                continue;
            }

            participant.SetSprite(GetFrame(participant.Animation, PanicAction.PanicHandsUp, participant.GetStopClipFrameIndex(frameIndex)));
            participant.ApplyPanicVisualOffset(participant.GetPanicOffset(motionFrame, false, 0f), worldUnitsPerRoomPixel);
        }
    }

    private IEnumerator PlayRandomStopActions()
    {
        if (participants.Count == 0 ||
            randomStopActionChance <= 0f ||
            UnityEngine.Random.value > randomStopActionChance)
        {
            yield break;
        }

        int maxFrameCount = ChooseRandomStopActions();

        if (maxFrameCount <= 0)
        {
            yield break;
        }

        float secondsPerFrame = GetSecondsPerFrame();

        for (int frameIndex = 0; frameIndex < maxFrameCount && isRunning; frameIndex++)
        {
            float frameElapsed = 0f;

            while (isRunning && frameElapsed < secondsPerFrame)
            {
                float frameProgress = secondsPerFrame <= 0f ? 1f : Mathf.Clamp01(frameElapsed / secondsPerFrame);
                float motionFrame = frameIndex + frameProgress;
                ApplyRandomStopActionFrame(frameIndex, motionFrame);
                frameElapsed += GetPlaybackDeltaTime(secondsPerFrame);
                yield return null;
            }
        }
    }

    private void ChooseRandomRunTargets()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            participants[i]?.ChooseNextRunTarget(
                playerMovement,
                runDistancePixels,
                panicRoamRadiusPixels,
                verticalRunDistanceScale,
                worldUnitsPerRoomPixel,
                routeQueryScratch);
        }
    }

    private IEnumerator MoveParticipantsTowardAssignedTargets(bool jitter)
    {
        int frameCount = GetMaxAssignedRunFrameCount();

        if (frameCount <= 0)
        {
            yield break;
        }

        float secondsPerFrame = GetSecondsPerFrame();
        float frameElapsed = 0f;
        int frameIndex = 0;

        while (isRunning)
        {
            float deltaTime = GetPlaybackDeltaTime(secondsPerFrame);
            bool allArrived = StepParticipantsTowardAssignedTargets(deltaTime);
            float frameProgress = secondsPerFrame <= 0f ? 1f : Mathf.Clamp01(frameElapsed / secondsPerFrame);
            float motionFrame = frameIndex + frameProgress;

            ApplyAssignedRunFrame(frameIndex, motionFrame, jitter);

            if (allArrived && frameElapsed >= secondsPerFrame * 0.5f)
            {
                yield break;
            }

            frameElapsed += deltaTime;

            while (frameElapsed >= secondsPerFrame)
            {
                frameElapsed -= secondsPerFrame;
                frameIndex = (frameIndex + 1) % frameCount;
            }

            yield return null;
        }
    }

    private float GetSecondsPerFrame()
    {
        return 1f / Mathf.Max(1f, frameRate);
    }

    private static float GetPlaybackDeltaTime(float fallbackSeconds)
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            deltaTime = Time.unscaledDeltaTime;
        }

        if (deltaTime <= 0f)
        {
            deltaTime = fallbackSeconds;
        }

        return deltaTime;
    }

    private bool StepParticipantsTowardAssignedTargets(float deltaTime)
    {
        bool allArrived = true;

        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant != null &&
                !participant.MovePanicOffsetTowardCurrentTarget(panicMoveSpeedPixels, deltaTime))
            {
                allArrived = false;
            }
        }

        return allArrived;
    }

    private void ApplyAssignedRunFrame(int frameIndex, float motionFrame, bool jitter)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant == null)
            {
                continue;
            }

            PanicAction visualAction = participant.CurrentRunAction;
            participant.SetSprite(GetFrame(participant.Animation, visualAction, participant.GetRunClipFrameIndex(frameIndex)));
            participant.ApplyPanicVisualOffset(participant.GetPanicOffset(motionFrame, jitter, jitterPixels), worldUnitsPerRoomPixel);
        }
    }

    private int GetMaxAssignedRunFrameCount()
    {
        int maxFrameCount = 0;

        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];
            Sprite[] frames = participant != null ? GetFrames(participant.Animation, participant.CurrentRunAction) : null;
            maxFrameCount = Mathf.Max(maxFrameCount, frames != null ? frames.Length : 0);
        }

        return maxFrameCount;
    }

    private void RestoreParticipants()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            participants[i]?.Restore();
        }
    }

    private void ReapplyParticipantVisualOffsets()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            participants[i]?.ReapplyPanicVisualOffset(worldUnitsPerRoomPixel);
        }
    }

    private static Sprite GetFrame(Chapter2PanicCharacterAnimation animation, PanicAction action, int frameIndex)
    {
        Sprite[] frames = GetFrames(animation, action);

        if (frames == null || frames.Length == 0)
        {
            return null;
        }

        return frames[Mathf.Abs(frameIndex) % frames.Length];
    }

    private static Sprite[] GetFrames(Chapter2PanicCharacterAnimation animation, PanicAction action)
    {
        if (animation == null)
        {
            return Array.Empty<Sprite>();
        }

        switch (action)
        {
            case PanicAction.PanicHandsUp:
                return animation.PanicHandsUp;
            case PanicAction.PanicRunDown:
                return animation.PanicRunDown;
            case PanicAction.PanicRunLeft:
                return animation.PanicRunLeft;
            case PanicAction.PanicRunRight:
                return animation.PanicRunRight;
            case PanicAction.PanicRunUp:
                return animation.PanicRunUp;
            default:
                return Array.Empty<Sprite>();
        }
    }

    private static bool TryGetGuestNumber(ActorRoomState actorState, out int guestNumber)
    {
        guestNumber = 0;

        if (TryGetTrailingNumber(actorState != null ? actorState.ActorId : string.Empty, out guestNumber))
        {
            return true;
        }

        return TryGetTrailingNumber(actorState != null && actorState.gameObject != null ? actorState.gameObject.name : string.Empty, out guestNumber);
    }

    private static bool TryGetTrailingNumber(string value, out int number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        int multiplier = 1;
        bool foundDigit = false;

        for (int i = value.Length - 1; i >= 0; i--)
        {
            char c = value[i];

            if (char.IsDigit(c))
            {
                foundDigit = true;
                number += (c - '0') * multiplier;
                multiplier *= 10;
                continue;
            }

            if (foundDigit)
            {
                return number > 0;
            }
        }

        return foundDigit && number > 0;
    }

    private static PointClickPlayerMovement FindPlayerMovement()
    {
        GameObject playerObject = GameObject.Find("Player");

        if (TryGetUsablePlayerMovement(playerObject, out PointClickPlayerMovement namedPlayerMovement))
        {
            return namedPlayerMovement;
        }

        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Exclude);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (IsUsablePlayerMovement(candidate))
            {
                return candidate;
            }
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryGetUsablePlayerMovement(GameObject candidateObject, out PointClickPlayerMovement movement)
    {
        movement = candidateObject != null ? candidateObject.GetComponent<PointClickPlayerMovement>() : null;
        return IsUsablePlayerMovement(movement);
    }

    private static bool IsUsablePlayerMovement(PointClickPlayerMovement candidate)
    {
        return candidate != null &&
            candidate.gameObject.activeInHierarchy &&
            !IsLikelyChapterGuest(candidate.gameObject);
    }

    private static bool IsLikelyChapterGuest(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        ActorRoomState actorState = candidate.GetComponentInParent<ActorRoomState>();

        if (actorState != null &&
            (LooksLikeGuestId(actorState.ActorId) || LooksLikeGuestId(actorState.gameObject.name)))
        {
            return true;
        }

        return LooksLikeGuestId(candidate.name);
    }

    private static bool LooksLikeGuestId(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.TrimStart().StartsWith("Guest", StringComparison.OrdinalIgnoreCase);
    }

    private enum PanicAction
    {
        PanicHandsUp,
        PanicRunDown,
        PanicRunLeft,
        PanicRunRight,
        PanicRunUp,
    }

    private sealed class PanicParticipant
    {
        private ActorRoomState actorState;
        private Chapter2PanicCharacterAnimation animation;
        private SpriteRenderer spriteRenderer;
        private Image image;
        private Animator[] animators;
        private bool[] animatorEnabledStates;
        private Behaviour[] motionDrivers;
        private bool[] motionDriverEnabledStates;
        private Rigidbody2D rigidbody2D;
        private RigidbodyType2D originalRigidbodyBodyType;
        private Vector2 originalRigidbodyPosition;
        private Vector2 originalRigidbodyLinearVelocity;
        private float originalRigidbodyAngularVelocity;
        private float originalRigidbodyGravityScale;
        private bool originalRigidbodySimulated;
        private Sprite originalRendererSprite;
        private Sprite originalImageSprite;
        private Transform targetTransform;
        private RectTransform rectTransform;
        private RoomProjectedEntity projection;
        private bool usesProjection;
        private Vector2 originalProjectionFootPoint;
        private Vector2 originalAnchoredPosition;
        private Vector3 originalPosition;
        private Vector3 originalLocalPosition;
        private Vector3 originalLocalScale;
        private Vector2 originalSpriteLocalSize;
        private bool hasOriginalSpriteLocalSize;
        private string originalRoomId;
        private bool originalAvailable;
        private bool originalVisible;
        private bool originalInteractable;
        private bool originalSeated;
        private float runDistanceScale = 1f;
        private float moveSpeedScale = 1f;
        private int framePhaseOffset;
        private int stopFramePhaseOffset;
        private float jitterPhase;
        private float bobPixels = 2f;
        private Vector2 currentPanicOffset;
        private Vector2 currentRunTargetOffset;
        private Vector2 currentVisualOffset;
        private readonly List<Vector2> currentRouteOffsets = new List<Vector2>();
        private int currentRouteOffsetIndex;
        private Sprite currentPanicSprite;
        private PanicAction currentRunAction = PanicAction.PanicRunDown;

        public Chapter2PanicCharacterAnimation Animation => animation;
        public bool HasSpriteTarget => spriteRenderer != null || image != null;
        public PanicAction CurrentRunAction => currentRunAction;

        public static PanicParticipant Create(ActorRoomState nextActorState, Chapter2PanicCharacterAnimation nextAnimation)
        {
            GameObject root = nextActorState != null ? nextActorState.gameObject : null;
            Transform rootTransform = root != null ? root.transform : null;
            RoomProjectedEntity nextProjection = nextActorState != null ? nextActorState.Projection : null;
            PanicParticipant participant = new PanicParticipant
            {
                actorState = nextActorState,
                animation = nextAnimation,
                spriteRenderer = FindPrimarySpriteRenderer(root),
                image = FindPrimaryImage(root),
                animators = root != null ? root.GetComponentsInChildren<Animator>(true) : Array.Empty<Animator>(),
                motionDrivers = FindMotionDrivers(root),
                rigidbody2D = root != null ? root.GetComponent<Rigidbody2D>() : null,
                targetTransform = rootTransform,
                rectTransform = rootTransform as RectTransform,
                projection = nextProjection,
                usesProjection = nextProjection != null && nextProjection.IsProjectionActive,
                originalProjectionFootPoint = nextProjection != null ? nextProjection.RoomLocalFootPoint : Vector2.zero,
                originalAnchoredPosition = rootTransform is RectTransform rt ? rt.anchoredPosition : Vector2.zero,
                originalPosition = rootTransform != null ? rootTransform.position : Vector3.zero,
                originalLocalPosition = rootTransform != null ? rootTransform.localPosition : Vector3.zero,
                originalLocalScale = rootTransform != null ? rootTransform.localScale : Vector3.one,
                originalRoomId = nextActorState != null ? nextActorState.CurrentRoomId : string.Empty,
                originalAvailable = nextActorState == null || nextActorState.IsAvailableInCurrentChapter,
                originalVisible = nextActorState == null || nextActorState.IsVisibleByChapterState,
                originalInteractable = nextActorState != null && nextActorState.IsInteractable,
                originalSeated = nextActorState != null && nextActorState.IsSeated,
            };

            participant.animatorEnabledStates = new bool[participant.animators.Length];

            for (int i = 0; i < participant.animators.Length; i++)
            {
                participant.animatorEnabledStates[i] = participant.animators[i] != null && participant.animators[i].enabled;
            }

            participant.motionDriverEnabledStates = new bool[participant.motionDrivers.Length];

            for (int i = 0; i < participant.motionDrivers.Length; i++)
            {
                participant.motionDriverEnabledStates[i] = participant.motionDrivers[i] != null && participant.motionDrivers[i].enabled;
            }

            participant.originalRendererSprite = participant.spriteRenderer != null ? participant.spriteRenderer.sprite : null;
            participant.originalImageSprite = participant.image != null ? participant.image.sprite : null;
            participant.CaptureOriginalSpriteLocalSize();

            if (participant.rigidbody2D != null)
            {
                participant.originalRigidbodyBodyType = participant.rigidbody2D.bodyType;
                participant.originalRigidbodyPosition = participant.rigidbody2D.position;
                participant.originalRigidbodyLinearVelocity = participant.rigidbody2D.linearVelocity;
                participant.originalRigidbodyAngularVelocity = participant.rigidbody2D.angularVelocity;
                participant.originalRigidbodyGravityScale = participant.rigidbody2D.gravityScale;
                participant.originalRigidbodySimulated = participant.rigidbody2D.simulated;
            }

            return participant;
        }

        public void ConfigureRunMotion(int participantIndex, int guestNumber)
        {
            int seed = Mathf.Abs((guestNumber + 1) * 37 + (participantIndex + 1) * 19);

            runDistanceScale = 0.82f + seed % 5 * 0.07f;
            moveSpeedScale = 0.9f + seed % 4 * 0.08f;
            framePhaseOffset = seed % 4;
            jitterPhase = seed * 0.618f;
            bobPixels = 2f + seed % 3 * 0.75f;
        }

        public void ApplyPanicState()
        {
            StopMotionDrivers();

            for (int i = 0; i < motionDrivers.Length; i++)
            {
                if (motionDrivers[i] != null)
                {
                    motionDrivers[i].enabled = false;
                }
            }

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                {
                    animators[i].enabled = false;
                }
            }

            if (rigidbody2D != null)
            {
                rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                rigidbody2D.gravityScale = 0f;
                rigidbody2D.linearVelocity = Vector2.zero;
                rigidbody2D.angularVelocity = 0f;
            }

            if (actorState != null)
            {
                actorState.SetInteractable(false);
                actorState.SetSeated(false);
                actorState.SetVisibleByChapterState(true);
                actorState.ApplyState();
            }
        }

        public void ChooseNextRunTarget(
            PointClickPlayerMovement routeSource,
            float runDistancePixels,
            float roamRadiusPixels,
            float verticalDistanceScale,
            float worldUnitsPerPixel,
            List<Vector2> routeScratch)
        {
            currentRouteOffsets.Clear();
            currentRouteOffsetIndex = 0;

            if (TryChooseReachableRouteTarget(
                routeSource,
                runDistancePixels,
                roamRadiusPixels,
                verticalDistanceScale,
                worldUnitsPerPixel,
                routeScratch))
            {
                return;
            }

            ChooseFallbackRunTarget(runDistancePixels, roamRadiusPixels, verticalDistanceScale);
        }

        private bool TryChooseReachableRouteTarget(
            PointClickPlayerMovement routeSource,
            float runDistancePixels,
            float roamRadiusPixels,
            float verticalDistanceScale,
            float worldUnitsPerPixel,
            List<Vector2> routeScratch)
        {
            if (routeSource == null || routeScratch == null)
            {
                return false;
            }

            float horizontalRadius = Mathf.Max(1f, roamRadiusPixels * runDistanceScale);
            float verticalRadius = Mathf.Max(1f, roamRadiusPixels * Mathf.Clamp(verticalDistanceScale, 0.1f, 1f) * runDistanceScale);
            float stepPixels = Mathf.Max(1f, runDistancePixels * runDistanceScale * UnityEngine.Random.Range(0.72f, 1.15f));

            if (!TryGetWorldPointForOffset(currentPanicOffset, worldUnitsPerPixel, out Vector2 startWorldPoint))
            {
                return false;
            }

            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 direction = GetRandomScatterDirection();
                Vector2 targetOffset = currentPanicOffset + new Vector2(
                    direction.x * stepPixels,
                    direction.y * stepPixels * Mathf.Clamp(verticalDistanceScale, 0.1f, 1f));
                targetOffset = ClampPanicOffset(targetOffset, horizontalRadius, verticalRadius);

                if (Vector2.Distance(currentPanicOffset, targetOffset) <= 1f ||
                    !TryGetWorldPointForOffset(targetOffset, worldUnitsPerPixel, out Vector2 targetWorldPoint) ||
                    !routeSource.TryBuildReachableWorldPath(startWorldPoint, targetWorldPoint, true, routeScratch))
                {
                    continue;
                }

                if (TrySetRouteFromWorldPath(routeScratch, worldUnitsPerPixel))
                {
                    return true;
                }
            }

            return false;
        }

        private void ChooseFallbackRunTarget(float runDistancePixels, float roamRadiusPixels, float verticalDistanceScale)
        {
            float horizontalRadius = Mathf.Max(1f, roamRadiusPixels * runDistanceScale);
            float verticalRadius = Mathf.Max(1f, roamRadiusPixels * Mathf.Clamp(verticalDistanceScale, 0.1f, 1f) * runDistanceScale);
            float stepPixels = Mathf.Max(1f, runDistancePixels * runDistanceScale * UnityEngine.Random.Range(0.72f, 1.15f));

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 direction = GetRandomScatterDirection();
                Vector2 targetOffset = currentPanicOffset + new Vector2(
                    direction.x * stepPixels,
                    direction.y * stepPixels * Mathf.Clamp(verticalDistanceScale, 0.1f, 1f));
                targetOffset = ClampPanicOffset(targetOffset, horizontalRadius, verticalRadius);

                if (TrySetDirectRouteTarget(targetOffset))
                {
                    return;
                }
            }

            currentRunTargetOffset = Vector2.zero;
            currentRunAction = GetRunActionForDirection(Vector2.zero - currentPanicOffset);
            currentRouteOffsets.Add(currentRunTargetOffset);
        }

        private bool TrySetRouteFromWorldPath(List<Vector2> worldPath, float worldUnitsPerPixel)
        {
            currentRouteOffsets.Clear();
            currentRouteOffsetIndex = 0;

            for (int i = 0; i < worldPath.Count; i++)
            {
                if (!TryGetOffsetForWorldPoint(worldPath[i], worldUnitsPerPixel, out Vector2 routeOffset))
                {
                    continue;
                }

                Vector2 previousOffset = currentRouteOffsets.Count > 0
                    ? currentRouteOffsets[currentRouteOffsets.Count - 1]
                    : currentPanicOffset;

                if (Vector2.Distance(previousOffset, routeOffset) <= 0.5f)
                {
                    continue;
                }

                currentRouteOffsets.Add(routeOffset);
            }

            if (currentRouteOffsets.Count == 0)
            {
                return false;
            }

            currentRunTargetOffset = currentRouteOffsets[currentRouteOffsets.Count - 1];
            currentRunAction = GetRunActionForDirection(currentRouteOffsets[0] - currentPanicOffset);
            return true;
        }

        private bool TrySetDirectRouteTarget(Vector2 targetOffset)
        {
            if (Vector2.Distance(currentPanicOffset, targetOffset) <= 1f)
            {
                return false;
            }

            currentRunTargetOffset = targetOffset;
            currentRunAction = GetRunActionForDirection(targetOffset - currentPanicOffset);
            currentRouteOffsets.Clear();
            currentRouteOffsetIndex = 0;
            currentRouteOffsets.Add(targetOffset);
            return true;
        }

        public int GetRunClipFrameIndex(int frameIndex)
        {
            return frameIndex + framePhaseOffset;
        }

        public void SetStopFramePhase(int framePhase)
        {
            stopFramePhaseOffset = Mathf.Max(0, framePhase);
        }

        public int GetStopClipFrameIndex(int frameIndex)
        {
            return frameIndex + stopFramePhaseOffset;
        }

        public bool MovePanicOffsetTowardCurrentTarget(float pixelsPerSecond, float deltaTime)
        {
            float remainingDistance = Mathf.Max(1f, pixelsPerSecond) * moveSpeedScale * Mathf.Max(0f, deltaTime);

            while (remainingDistance > 0f && currentRouteOffsetIndex < currentRouteOffsets.Count)
            {
                Vector2 targetOffset = currentRouteOffsets[currentRouteOffsetIndex];
                Vector2 previousOffset = currentPanicOffset;
                Vector2 nextOffset = Vector2.MoveTowards(previousOffset, targetOffset, remainingDistance);
                Vector2 movement = nextOffset - previousOffset;
                currentPanicOffset = nextOffset;

                if (movement.sqrMagnitude > 0.0001f)
                {
                    currentRunAction = GetRunActionForDirection(movement);
                }

                float movedDistance = movement.magnitude;
                remainingDistance = Mathf.Max(0f, remainingDistance - movedDistance);

                if (Vector2.Distance(currentPanicOffset, targetOffset) > 0.5f)
                {
                    break;
                }

                currentPanicOffset = targetOffset;
                currentRouteOffsetIndex++;

                if (movedDistance <= 0.0001f)
                {
                    break;
                }
            }

            return currentRouteOffsetIndex >= currentRouteOffsets.Count &&
                Vector2.Distance(currentPanicOffset, currentRunTargetOffset) <= 0.5f;
        }

        public Vector2 GetPanicOffset(float motionFrame, bool jitter, float maxJitterPixels)
        {
            float x = currentPanicOffset.x;
            float y = currentPanicOffset.y + Mathf.Abs(Mathf.Sin((motionFrame + framePhaseOffset) * 0.92f * Mathf.PI)) * bobPixels;

            if (jitter && maxJitterPixels > 0f)
            {
                x += Mathf.Sin(motionFrame * 1.73f + jitterPhase) * maxJitterPixels;
                y += Mathf.Abs(Mathf.Cos(motionFrame * 1.11f + jitterPhase)) * maxJitterPixels * 0.25f;
            }

            return new Vector2(x, y);
        }

        public void ApplyPanicVisualOffset(Vector2 roomPixelOffset, float worldUnitsPerPixel)
        {
            currentVisualOffset = roomPixelOffset;
            ApplyVisualOffset(roomPixelOffset, worldUnitsPerPixel);
        }

        public void ReapplyPanicVisualOffset(float worldUnitsPerPixel)
        {
            ApplySpriteScale(currentPanicSprite);
            ApplyVisualOffset(currentVisualOffset, worldUnitsPerPixel);
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            currentPanicSprite = sprite;

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
            }

            if (image != null)
            {
                image.sprite = sprite;
            }

            ApplySpriteScale(sprite);
        }

        private void CaptureOriginalSpriteLocalSize()
        {
            Sprite sprite = originalRendererSprite != null ? originalRendererSprite : originalImageSprite;

            if (TryGetSpriteLocalSize(sprite, out originalSpriteLocalSize))
            {
                hasOriginalSpriteLocalSize = true;
            }
        }

        private void ApplySpriteScale(Sprite sprite)
        {
            if (targetTransform == null ||
                !hasOriginalSpriteLocalSize ||
                !TryGetSpriteLocalSize(sprite, out Vector2 spriteLocalSize))
            {
                return;
            }

            float scale = GetSpriteScaleMultiplier(originalSpriteLocalSize, spriteLocalSize);
            targetTransform.localScale = new Vector3(
                originalLocalScale.x * scale,
                originalLocalScale.y * scale,
                originalLocalScale.z);
        }

        private static bool TryGetSpriteLocalSize(Sprite sprite, out Vector2 size)
        {
            size = Vector2.zero;

            if (sprite == null)
            {
                return false;
            }

            size = sprite.bounds.size;
            return size.x > 0.0001f || size.y > 0.0001f;
        }

        private static float GetSpriteScaleMultiplier(Vector2 originalSize, Vector2 nextSize)
        {
            if (originalSize.y > 0.0001f && nextSize.y > 0.0001f)
            {
                return originalSize.y / nextSize.y;
            }

            if (originalSize.x > 0.0001f && nextSize.x > 0.0001f)
            {
                return originalSize.x / nextSize.x;
            }

            return 1f;
        }

        public void ApplyVisualOffset(Vector2 roomPixelOffset, float worldUnitsPerPixel)
        {
            if (usesProjection && projection != null)
            {
                projection.SetRoomLocalFootPoint(originalProjectionFootPoint + roomPixelOffset);
                SyncRigidbodyToTransform();
                return;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = originalAnchoredPosition + roomPixelOffset;
                SyncRigidbodyToTransform();
                return;
            }

            if (targetTransform != null)
            {
                Vector3 worldOffset = new Vector3(roomPixelOffset.x * worldUnitsPerPixel, roomPixelOffset.y * worldUnitsPerPixel, 0f);
                Vector3 nextPosition = originalPosition + worldOffset;
                targetTransform.position = nextPosition;
                MoveRigidbodyTo(nextPosition);
            }
        }

        private bool TryGetWorldPointForOffset(Vector2 roomPixelOffset, float worldUnitsPerPixel, out Vector2 worldPoint)
        {
            worldPoint = Vector2.zero;

            if (usesProjection && projection != null)
            {
                if (TryGetProjectionRoomStage(out Transform roomStage))
                {
                    Vector3 projectedWorldPoint = roomStage.TransformPoint(originalProjectionFootPoint + roomPixelOffset);
                    worldPoint = projectedWorldPoint;
                    return true;
                }

                worldPoint = targetTransform != null ? targetTransform.position : projection.transform.position;
                return true;
            }

            if (rectTransform != null && rectTransform.parent != null)
            {
                Vector3 projectedWorldPoint = rectTransform.parent.TransformPoint(originalAnchoredPosition + roomPixelOffset);
                worldPoint = projectedWorldPoint;
                return true;
            }

            if (targetTransform != null)
            {
                Vector3 worldOffset = new Vector3(
                    roomPixelOffset.x * worldUnitsPerPixel,
                    roomPixelOffset.y * worldUnitsPerPixel,
                    0f);
                worldPoint = originalPosition + worldOffset;
                return true;
            }

            return false;
        }

        private bool TryGetOffsetForWorldPoint(Vector2 worldPoint, float worldUnitsPerPixel, out Vector2 roomPixelOffset)
        {
            roomPixelOffset = Vector2.zero;

            if (usesProjection && projection != null)
            {
                if (!TryGetProjectionRoomStage(out Transform roomStage))
                {
                    return false;
                }

                Vector3 roomLocalPoint = roomStage.InverseTransformPoint(worldPoint);
                roomPixelOffset = new Vector2(roomLocalPoint.x, roomLocalPoint.y) - originalProjectionFootPoint;
                return true;
            }

            if (rectTransform != null && rectTransform.parent != null)
            {
                Vector3 localPoint = rectTransform.parent.InverseTransformPoint(worldPoint);
                roomPixelOffset = new Vector2(localPoint.x, localPoint.y) - originalAnchoredPosition;
                return true;
            }

            if (targetTransform != null && worldUnitsPerPixel > 0.0001f)
            {
                Vector2 worldOffset = worldPoint - (Vector2)originalPosition;
                roomPixelOffset = worldOffset / worldUnitsPerPixel;
                return true;
            }

            return false;
        }

        private bool TryGetProjectionRoomStage(out Transform roomStage)
        {
            roomStage = null;

            if (projection == null)
            {
                return false;
            }

            RoomContentGroup roomContent = projection.GetComponentInParent<RoomContentGroup>(true);
            if (roomContent != null)
            {
                roomStage = roomContent.transform;
                return roomStage != null;
            }

            if (projection.transform.parent != null)
            {
                roomStage = projection.transform.parent;
                return true;
            }

            return false;
        }

        public void Restore()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = originalRendererSprite;
            }

            if (image != null)
            {
                image.sprite = originalImageSprite;
            }

            if (usesProjection && projection != null)
            {
                projection.SetRoomLocalFootPoint(originalProjectionFootPoint);
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = originalAnchoredPosition;
            }

            if (targetTransform != null)
            {
                targetTransform.position = originalPosition;
                targetTransform.localPosition = originalLocalPosition;
                targetTransform.localScale = originalLocalScale;
            }

            currentPanicOffset = Vector2.zero;
            currentRunTargetOffset = Vector2.zero;
            currentVisualOffset = Vector2.zero;
            currentRouteOffsets.Clear();
            currentRouteOffsetIndex = 0;
            currentPanicSprite = null;

            if (rigidbody2D != null)
            {
                rigidbody2D.position = originalRigidbodyPosition;
                rigidbody2D.linearVelocity = originalRigidbodyLinearVelocity;
                rigidbody2D.angularVelocity = originalRigidbodyAngularVelocity;
                rigidbody2D.gravityScale = originalRigidbodyGravityScale;
                rigidbody2D.bodyType = originalRigidbodyBodyType;
                rigidbody2D.simulated = originalRigidbodySimulated;
            }

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && i < animatorEnabledStates.Length)
                {
                    animators[i].enabled = animatorEnabledStates[i];
                }
            }

            for (int i = 0; i < motionDrivers.Length; i++)
            {
                if (motionDrivers[i] != null && i < motionDriverEnabledStates.Length)
                {
                    motionDrivers[i].enabled = motionDriverEnabledStates[i];
                }
            }

            if (actorState != null)
            {
                actorState.SetCurrentRoom(originalRoomId);
                actorState.SetAvailableInCurrentChapter(originalAvailable);
                actorState.SetVisibleByChapterState(originalVisible);
                actorState.SetInteractable(originalInteractable);
                actorState.SetSeated(originalSeated);
                actorState.ApplyState();
            }
        }

        private void MoveRigidbodyTo(Vector3 worldPosition)
        {
            if (rigidbody2D == null)
            {
                return;
            }

            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
            rigidbody2D.position = new Vector2(worldPosition.x, worldPosition.y);
        }

        private void SyncRigidbodyToTransform()
        {
            if (targetTransform == null)
            {
                return;
            }

            MoveRigidbodyTo(targetTransform.position);
        }

        private static Vector2 GetRandomScatterDirection()
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static Vector2 ClampPanicOffset(Vector2 offset, float horizontalRadius, float verticalRadius)
        {
            return new Vector2(
                Mathf.Clamp(offset.x, -horizontalRadius, horizontalRadius),
                Mathf.Clamp(offset.y, -verticalRadius, verticalRadius));
        }

        private static PanicAction GetRunActionForDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                return direction.x < 0f ? PanicAction.PanicRunLeft : PanicAction.PanicRunRight;
            }

            return direction.y < 0f ? PanicAction.PanicRunDown : PanicAction.PanicRunUp;
        }

        private static SpriteRenderer FindPrimarySpriteRenderer(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer best = null;
            float bestArea = -1f;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer == null || renderer.sprite == null || IsIgnoredVisualTransform(renderer.transform))
                {
                    continue;
                }

                Vector3 size = renderer.bounds.size;
                float area = Mathf.Max(0.001f, size.x * size.y);

                if (area > bestArea)
                {
                    best = renderer;
                    bestArea = area;
                }
            }

            return best;
        }

        private void StopMotionDrivers()
        {
            for (int i = 0; i < motionDrivers.Length; i++)
            {
                if (motionDrivers[i] is NPCWaypointMover waypointMover)
                {
                    waypointMover.StopMoving();
                }

                if (motionDrivers[i] is PointClickPlayerMovement pointClickMovement)
                {
                    pointClickMovement.SetInputEnabled(false);
                }
            }
        }

        private static Behaviour[] FindMotionDrivers(GameObject root)
        {
            if (root == null)
            {
                return Array.Empty<Behaviour>();
            }

            List<Behaviour> drivers = new List<Behaviour>();
            AppendMotionDrivers<RoomPersonWalker2D>(root, drivers);
            AppendMotionDrivers<NPCWaypointMover>(root, drivers);
            AppendMotionDrivers<PointClickPlayerMovement>(root, drivers);
            AppendMotionDrivers<PlayerMovement>(root, drivers);
            AppendMotionDrivers<CharacterController2D>(root, drivers);
            return drivers.ToArray();
        }

        private static void AppendMotionDrivers<T>(GameObject root, List<Behaviour> drivers) where T : Behaviour
        {
            T[] components = root.GetComponentsInChildren<T>(true);

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && !drivers.Contains(components[i]))
                {
                    drivers.Add(components[i]);
                }
            }
        }

        private static Image FindPrimaryImage(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            Image[] images = root.GetComponentsInChildren<Image>(true);
            Image best = null;
            float bestArea = -1f;

            for (int i = 0; i < images.Length; i++)
            {
                Image candidate = images[i];

                if (candidate == null || candidate.sprite == null || IsIgnoredVisualTransform(candidate.transform))
                {
                    continue;
                }

                RectTransform candidateRect = candidate.rectTransform;
                Vector2 size = candidateRect != null ? candidateRect.rect.size : Vector2.one;
                float area = Mathf.Max(0.001f, size.x * size.y);

                if (area > bestArea)
                {
                    best = candidate;
                    bestArea = area;
                }
            }

            return best;
        }

        private static bool IsIgnoredVisualTransform(Transform transform)
        {
            while (transform != null)
            {
                string transformName = transform.name;

                if (!string.IsNullOrWhiteSpace(transformName) &&
                    (transformName.IndexOf("coat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        transformName.IndexOf(ClickTargetName, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }
    }
}
