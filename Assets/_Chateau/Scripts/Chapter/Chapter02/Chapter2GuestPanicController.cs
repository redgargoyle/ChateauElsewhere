using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Chapter2GuestPanicController : MonoBehaviour
{
    private const string ClickTargetName = "Ch2_ClickTarget";

    [SerializeField] private Chapter2GuestSearchController guestSearch;
    [SerializeField] private Chapter2PanicAnimationLibrary animationLibrary;
    [SerializeField, Min(1f)] private float frameRate = 12f;
    [SerializeField, Min(0f)] private float runDistancePixels = 150f;
    [SerializeField, Min(1f)] private float panicMoveSpeedPixels = 300f;
    [SerializeField, Range(0.1f, 0.95f)] private float turnaroundDistanceScale = 0.68f;
    [SerializeField, Min(0f)] private float jitterPixels = 3f;
    [SerializeField, Min(0.0001f)] private float worldUnitsPerRoomPixel = 0.012f;
    [SerializeField] private bool logMissingFrames = true;

    private readonly List<PanicParticipant> participants = new List<PanicParticipant>();
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
        StepParticipantsToward(new Vector2(-runDistancePixels, 0f), GetSecondsPerFrame());
        ApplyActionFrame(PanicAction.PanicRunLeft, 0, 0f, true);
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
        participants.Clear();
        isRunning = false;
    }

    private void OnDisable()
    {
        StopPanic();
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
        Vector2 leftOffset = new Vector2(-runDistancePixels, 0f);
        Vector2 rightOffset = new Vector2(runDistancePixels, 0f);
        Vector2 leftTurnOffset = GetTurnaroundOffset(leftOffset);
        Vector2 rightTurnOffset = GetTurnaroundOffset(rightOffset);

        while (isRunning)
        {
            yield return MoveParticipantsToward(PanicAction.PanicRunLeft, leftOffset, true);
            yield return MoveParticipantsToward(PanicAction.PanicTurnaround, leftTurnOffset, true);
            yield return MoveParticipantsToward(PanicAction.PanicRunRight, rightOffset, true);
            yield return MoveParticipantsToward(PanicAction.PanicTurnaround, rightTurnOffset, true);
        }
    }

    private Vector2 GetTurnaroundOffset(Vector2 endpointOffset)
    {
        return endpointOffset * Mathf.Clamp(turnaroundDistanceScale, 0.1f, 0.95f);
    }

    private IEnumerator MoveParticipantsToward(PanicAction action, Vector2 targetOffset, bool jitter)
    {
        int frameCount = GetMaxFrameCount(action);

        if (frameCount <= 0)
        {
            yield break;
        }

        float secondsPerFrame = GetSecondsPerFrame();
        float frameElapsed = 0f;
        int frameIndex = 0;

        while (isRunning)
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                deltaTime = Time.unscaledDeltaTime;
            }

            if (deltaTime <= 0f)
            {
                deltaTime = secondsPerFrame;
            }

            bool allArrived = StepParticipantsToward(targetOffset, deltaTime);
            float frameProgress = secondsPerFrame <= 0f ? 1f : Mathf.Clamp01(frameElapsed / secondsPerFrame);
            float motionFrame = frameIndex + frameProgress;

            ApplyActionFrame(action, frameIndex, motionFrame, jitter);

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

    private bool StepParticipantsToward(Vector2 targetOffset, float deltaTime)
    {
        bool allArrived = true;

        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant != null &&
                !participant.MovePanicOffsetToward(targetOffset, panicMoveSpeedPixels, deltaTime))
            {
                allArrived = false;
            }
        }

        return allArrived;
    }

    private void ApplyActionFrame(PanicAction action, int frameIndex, float motionFrame, bool jitter)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant == null)
            {
                continue;
            }

            PanicAction visualAction = participant.GetVisualAction(action);
            participant.SetSprite(GetFrame(participant.Animation, visualAction, participant.GetClipFrameIndex(visualAction, frameIndex)));
            participant.ApplyVisualOffset(participant.GetPanicOffset(motionFrame, jitter, jitterPixels), worldUnitsPerRoomPixel);
        }
    }

    private int GetMaxFrameCount(PanicAction action)
    {
        int maxFrameCount = 0;

        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];
            Sprite[] frames = participant != null ? GetFrames(participant.Animation, action) : null;
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
            case PanicAction.PanicReactionDown:
                return animation.PanicReactionDown;
            case PanicAction.PanicShriekDown:
                return animation.PanicShriekDown;
            case PanicAction.PanicRunLeft:
                return animation.PanicRunLeft;
            case PanicAction.PanicRunRight:
                return animation.PanicRunRight;
            case PanicAction.PanicTurnaround:
                return animation.PanicTurnaround;
            case PanicAction.CoverFaceCower:
                return animation.CoverFaceCower;
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

    private enum PanicAction
    {
        PanicReactionDown,
        PanicShriekDown,
        PanicRunLeft,
        PanicRunRight,
        PanicTurnaround,
        CoverFaceCower,
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
        private string originalRoomId;
        private bool originalAvailable;
        private bool originalVisible;
        private bool originalInteractable;
        private bool originalSeated;
        private float runDirectionSign = 1f;
        private float runDistanceScale = 1f;
        private float moveSpeedScale = 1f;
        private int framePhaseOffset;
        private float jitterPhase;
        private float bobPixels = 2f;
        private Vector2 currentPanicOffset;

        public Chapter2PanicCharacterAnimation Animation => animation;
        public bool HasSpriteTarget => spriteRenderer != null || image != null;

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
            return participant;
        }

        public void ConfigureRunMotion(int participantIndex, int guestNumber)
        {
            int seed = Mathf.Abs((guestNumber + 1) * 37 + (participantIndex + 1) * 19);

            runDirectionSign = seed % 2 == 0 ? 1f : -1f;
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

            if (actorState != null)
            {
                actorState.SetInteractable(false);
                actorState.SetSeated(false);
                actorState.SetVisibleByChapterState(true);
                actorState.ApplyState();
            }
        }

        public PanicAction GetVisualAction(PanicAction action)
        {
            if (runDirectionSign >= 0f)
            {
                return action;
            }

            switch (action)
            {
                case PanicAction.PanicRunLeft:
                    return PanicAction.PanicRunRight;
                case PanicAction.PanicRunRight:
                    return PanicAction.PanicRunLeft;
                default:
                    return action;
            }
        }

        public int GetClipFrameIndex(PanicAction action, int frameIndex)
        {
            if (action == PanicAction.PanicRunLeft || action == PanicAction.PanicRunRight)
            {
                return frameIndex + framePhaseOffset;
            }

            return frameIndex;
        }

        public bool MovePanicOffsetToward(Vector2 sharedTargetOffset, float pixelsPerSecond, float deltaTime)
        {
            Vector2 targetOffset = GetDirectionalTargetOffset(sharedTargetOffset);
            float maxDistanceDelta = Mathf.Max(1f, pixelsPerSecond) * moveSpeedScale * Mathf.Max(0f, deltaTime);
            currentPanicOffset = Vector2.MoveTowards(currentPanicOffset, targetOffset, maxDistanceDelta);
            return Vector2.Distance(currentPanicOffset, targetOffset) <= 0.5f;
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

        private Vector2 GetDirectionalTargetOffset(Vector2 sharedTargetOffset)
        {
            return new Vector2(
                sharedTargetOffset.x * runDirectionSign * runDistanceScale,
                sharedTargetOffset.y);
        }

        public void SetSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = sprite;
            }

            if (image != null)
            {
                image.sprite = sprite;
            }
        }

        public void ApplyVisualOffset(Vector2 roomPixelOffset, float worldUnitsPerPixel)
        {
            if (usesProjection && projection != null)
            {
                projection.SetRoomLocalFootPoint(originalProjectionFootPoint + roomPixelOffset);
                return;
            }

            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = originalAnchoredPosition + roomPixelOffset;
                return;
            }

            if (targetTransform != null)
            {
                Vector3 worldOffset = new Vector3(roomPixelOffset.x * worldUnitsPerPixel, roomPixelOffset.y * worldUnitsPerPixel, 0f);
                targetTransform.position = originalPosition + worldOffset;
            }
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
