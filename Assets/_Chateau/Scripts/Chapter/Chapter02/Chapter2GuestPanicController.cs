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
    private const int ScriptedGuestNumber = 1;

    [SerializeField] private Chapter2GuestSearchController guestSearch;
    [SerializeField] private Chapter2PanicAnimationLibrary animationLibrary;
    [SerializeField, Min(1f)] private float frameRate = 12f;
    [SerializeField, Min(0f)] private float runDistancePixels = 150f;
    [SerializeField, Min(1f)] private float panicRoamRadiusPixels = 190f;
    [SerializeField, Range(0.1f, 1f)] private float verticalRunDistanceScale = 0.55f;
    [SerializeField, Min(1f)] private float panicMoveSpeedPixels = 300f;
    [SerializeField, Min(0f)] private float jitterPixels = 3f;
    [SerializeField, Range(0f, 1f)] private float randomStopActionChance = 1f;
    [SerializeField, Range(0f, 1f)] private float randomPopActionChance = 0.45f;
    [SerializeField] private bool useScriptedGuest1Panic = true;
    [SerializeField, Min(0.1f)] private float scriptedGuestRunSeconds = 1f;
    [SerializeField, Min(0.1f)] private float scriptedGuestHoldSeconds = 1f;
    [SerializeField, Min(1f)] private float scriptedGuestRunDistancePixels = 360f;
    [SerializeField, Min(1f)] private float scriptedGuestMoveSpeedPixels = 380f;
    [SerializeField, Min(0.1f)] private float scriptedGuestWalkAnimationSpeed = 1.35f;
    [SerializeField, Min(0f)] private float scriptedGuestShakePixels = 5f;
    [SerializeField, Min(0.1f)] private float scriptedGuestShakeCyclesPerSecond = 8f;
    [SerializeField] private PointClickPlayerMovement routePlanner;
    [SerializeField] private string routePlannerObjectName = "Player";
    [SerializeField] private Transform leftExitTarget;
    [SerializeField] private Transform rightExitTarget;
    [SerializeField] private string leftExitTargetName = "DoorTrigger_DrawingRoom_MusicRoom";
    [SerializeField] private string rightExitTargetName = "DoorTrigger_DrawingRoom_GEH";
    [SerializeField, Min(1f)] private float exitMoveSpeedPixels = 520f;
    [SerializeField, Min(0f)] private float exitOvershootPixels = 90f;
    [SerializeField, Min(0.1f)] private float exitTimeoutSeconds = 4f;
    [SerializeField, Min(0.0001f)] private float worldUnitsPerRoomPixel = 0.012f;
    [SerializeField] private bool logMissingFrames = true;

    private readonly List<PanicParticipant> participants = new List<PanicParticipant>();
    private Coroutine panicRoutine;
    private Coroutine scriptedGuestRoutine;
    private bool isRunning;

    public bool IsRunning => isRunning || panicRoutine != null || scriptedGuestRoutine != null;

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

        PanicParticipant scriptedGuest = FindScriptedGuestParticipant();

        if (scriptedGuest != null)
        {
            scriptedGuest.SetControlledByScript(true);
            scriptedGuestRoutine = StartCoroutine(RunScriptedGuest1PanicRoutine(scriptedGuest));
        }

        ChooseRandomRunTargets();
        ApplyAssignedRunFrame(0, 0f, true);
        panicRoutine = StartCoroutine(RunPanicRoutine());
        return panicRoutine;
    }

    public IEnumerator RunExitToDoorsThenRestoreRoutine()
    {
        Coroutine exitRoutine = BeginExitToDoors();

        if (exitRoutine != null)
        {
            float elapsedSeconds = 0f;
            float timeoutSeconds = GetExitWaitTimeoutSeconds();

            while (IsRunning && elapsedSeconds < timeoutSeconds)
            {
                elapsedSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            if (IsRunning)
            {
                Debug.LogWarning($"Chapter 2 guest panic exit timed out after {timeoutSeconds:0.##} seconds; continuing to guest search.", this);
            }
        }

        StopPanic();
    }

    public Coroutine BeginExitToDoors()
    {
        ResolveReferences();
        ResolveExitTargets();
        StopPanicRoutineOnly();

        if (participants.Count == 0)
        {
            BuildParticipants(participants);
        }

        if (participants.Count == 0)
        {
            return null;
        }

        isRunning = true;
        ChooseDoorExitTargets();
        ApplyAssignedRunFrame(0, 0f, false);
        panicRoutine = StartCoroutine(RunExitToDoorsRoutine());
        return panicRoutine;
    }

    public void StopPanic()
    {
        StopPanicRoutineOnly(true);

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

        if (animationLibrary == null)
        {
            animationLibrary = Resources.Load<Chapter2PanicAnimationLibrary>(Chapter2PanicAnimationLibrary.ResourcesPath);
        }

        if (routePlanner == null || !IsUsableRoutePlanner(routePlanner))
        {
            routePlanner = FindRoutePlanner(routePlannerObjectName);
        }
    }

    private void ResolveExitTargets()
    {
        if (leftExitTarget == null)
        {
            leftExitTarget = FindSceneTransform(leftExitTargetName);
        }

        if (rightExitTarget == null)
        {
            rightExitTarget = FindSceneTransform(rightExitTargetName);
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

    private PanicParticipant FindScriptedGuestParticipant()
    {
        if (!useScriptedGuest1Panic)
        {
            return null;
        }

        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant != null && participant.GuestNumber == ScriptedGuestNumber)
            {
                Sprite[] frames = GetFrames(participant.Animation, PanicAction.PanicPop);

                if (frames != null && frames.Length >= 8)
                {
                    return participant;
                }

                if (logMissingFrames)
                {
                    Debug.LogError("Chapter 2 scripted Guest 1 panic needs eight panic_pop frames.", this);
                }

                return null;
            }
        }

        return null;
    }

    private float GetExitWaitTimeoutSeconds()
    {
        float timeoutSeconds = Mathf.Max(0.1f, exitTimeoutSeconds);

        if (useScriptedGuest1Panic)
        {
            timeoutSeconds += GetScriptedGuest1PanicSeconds();
        }

        return timeoutSeconds;
    }

    private float GetScriptedGuest1PanicSeconds()
    {
        return 8f * (Mathf.Max(0.1f, scriptedGuestRunSeconds) + Mathf.Max(0.1f, scriptedGuestHoldSeconds)) +
            Mathf.Max(0.1f, exitTimeoutSeconds);
    }

    private void StopPanicRoutineOnly(bool stopScriptedGuest = false)
    {
        if (panicRoutine != null)
        {
            StopCoroutine(panicRoutine);
            panicRoutine = null;
        }

        if (stopScriptedGuest && scriptedGuestRoutine != null)
        {
            StopCoroutine(scriptedGuestRoutine);
            scriptedGuestRoutine = null;
        }
    }

    private IEnumerator RunScriptedGuest1PanicRoutine(PanicParticipant participant)
    {
        Sprite[] panicFrames = GetFrames(participant.Animation, PanicAction.PanicPop);

        if (panicFrames == null || panicFrames.Length < 8)
        {
            participant.SetControlledByScript(false);
            scriptedGuestRoutine = null;

            if (panicRoutine == null)
            {
                isRunning = false;
            }

            yield break;
        }

        for (int frameIndex = 0; frameIndex < 8 && isRunning; frameIndex++)
        {
            bool runRight = frameIndex % 2 == 0;
            yield return RunScriptedGuestDirectionalRun(
                participant,
                runRight ? Vector2.right : Vector2.left,
                runRight ? PanicAction.PanicRunRight : PanicAction.PanicRunLeft);
            yield return HoldScriptedGuestPanicFrame(participant, panicFrames[frameIndex], frameIndex);
        }

        if (isRunning)
        {
            yield return RunScriptedGuestToExit(participant);
        }

        scriptedGuestRoutine = null;

        if (panicRoutine == null)
        {
            isRunning = false;
        }
    }

    private IEnumerator RunScriptedGuestDirectionalRun(
        PanicParticipant participant,
        Vector2 direction,
        PanicAction runAction)
    {
        if (participant == null)
        {
            yield break;
        }

        float durationSeconds = Mathf.Max(0.1f, scriptedGuestRunSeconds);
        float moveSpeedPixels = Mathf.Max(1f, scriptedGuestMoveSpeedPixels);
        float distancePixels = Mathf.Max(1f, scriptedGuestRunDistancePixels);

        if (routePlanner == null ||
            !participant.TryChooseRoutedDirectionalRunTarget(routePlanner, direction, distancePixels, worldUnitsPerRoomPixel))
        {
            participant.ChooseDirectionalRunTarget(direction, distancePixels);
        }

        participant.SetCurrentRunAction(runAction);
        yield return RunScriptedGuestMoveForSeconds(participant, durationSeconds, moveSpeedPixels, true, runAction);
    }

    private IEnumerator RunScriptedGuestMoveForSeconds(
        PanicParticipant participant,
        float durationSeconds,
        float moveSpeedPixels,
        bool jitter,
        PanicAction lockedRunAction)
    {
        float secondsPerFrame = GetSecondsPerFrame();
        float elapsedSeconds = 0f;
        int frameIndex = 0;
        bool usingAnimator = participant.BeginScriptedAnimatorWalk(lockedRunAction, scriptedGuestWalkAnimationSpeed);

        while (isRunning && elapsedSeconds < durationSeconds)
        {
            float deltaTime = GetPlaybackDeltaTime(secondsPerFrame);
            participant.MovePanicOffsetTowardCurrentTarget(moveSpeedPixels, deltaTime);

            float frameProgress = secondsPerFrame <= 0f
                ? 1f
                : Mathf.Clamp01((elapsedSeconds % secondsPerFrame) / secondsPerFrame);
            float motionFrame = frameIndex + frameProgress;
            participant.SetCurrentRunAction(lockedRunAction);

            if (usingAnimator)
            {
                participant.UpdateScriptedAnimatorWalk(lockedRunAction, scriptedGuestWalkAnimationSpeed);
            }
            else
            {
                participant.SetSprite(GetFrame(participant.Animation, lockedRunAction, participant.GetRunClipFrameIndex(frameIndex)));
            }

            participant.ApplyPanicVisualOffset(participant.GetPanicOffset(motionFrame, jitter, jitter ? jitterPixels : 0f), worldUnitsPerRoomPixel);

            elapsedSeconds += deltaTime;

            while (elapsedSeconds >= (frameIndex + 1) * secondsPerFrame)
            {
                frameIndex++;
            }

            yield return null;
        }

        participant.StopScriptedAnimatorWalk(lockedRunAction);
    }

    private IEnumerator HoldScriptedGuestPanicFrame(PanicParticipant participant, Sprite panicSprite, int frameIndex)
    {
        if (participant == null || panicSprite == null)
        {
            yield break;
        }

        float durationSeconds = Mathf.Max(0.1f, scriptedGuestHoldSeconds);
        float elapsedSeconds = 0f;
        Vector2 baseOffset = participant.CurrentPanicOffset;
        participant.StopScriptedAnimatorWalk(participant.CurrentRunAction);

        while (isRunning && elapsedSeconds < durationSeconds)
        {
            float deltaTime = GetPlaybackDeltaTime(durationSeconds);
            float shake = Mathf.Sin(elapsedSeconds * scriptedGuestShakeCyclesPerSecond * Mathf.PI * 2f) *
                Mathf.Max(0f, scriptedGuestShakePixels);

            participant.SetSprite(panicSprite);
            participant.ApplyPanicVisualOffset(baseOffset + new Vector2(shake, 0f), worldUnitsPerRoomPixel);
            elapsedSeconds += deltaTime;
            yield return null;
        }

        participant.ApplyPanicVisualOffset(baseOffset, worldUnitsPerRoomPixel);
    }

    private IEnumerator RunScriptedGuestToExit(PanicParticipant participant)
    {
        ResolveExitTargets();

        Transform exitTarget = ChooseNearestScriptedGuestExitTarget(participant);

        if (exitTarget == null ||
            !participant.TryChooseExitTarget(exitTarget, routePlanner, worldUnitsPerRoomPixel, exitOvershootPixels))
        {
            yield break;
        }

        int frameCount = Mathf.Max(1, GetFrames(participant.Animation, participant.CurrentRunAction).Length);
        float secondsPerFrame = GetSecondsPerFrame();
        float elapsedSeconds = 0f;
        float timeoutSeconds = Mathf.Max(0.1f, exitTimeoutSeconds);
        int frameIndex = 0;
        bool arrived = false;
        bool usingAnimator = participant.BeginScriptedAnimatorWalk(participant.CurrentRunAction, scriptedGuestWalkAnimationSpeed);

        while (isRunning && elapsedSeconds < timeoutSeconds && !arrived)
        {
            float deltaTime = GetPlaybackDeltaTime(secondsPerFrame);
            arrived = participant.MovePanicOffsetTowardCurrentTarget(exitMoveSpeedPixels, deltaTime);
            float frameProgress = secondsPerFrame <= 0f
                ? 1f
                : Mathf.Clamp01((elapsedSeconds % secondsPerFrame) / secondsPerFrame);
            float motionFrame = frameIndex + frameProgress;
            PanicAction visualAction = participant.CurrentRunAction;

            if (usingAnimator)
            {
                participant.UpdateScriptedAnimatorWalk(visualAction, scriptedGuestWalkAnimationSpeed);
            }
            else
            {
                participant.SetSprite(GetFrame(participant.Animation, visualAction, participant.GetRunClipFrameIndex(frameIndex % frameCount)));
            }

            participant.ApplyPanicVisualOffset(participant.GetPanicOffset(motionFrame, false, 0f), worldUnitsPerRoomPixel);
            elapsedSeconds += deltaTime;

            while (elapsedSeconds >= (frameIndex + 1) * secondsPerFrame)
            {
                frameIndex++;
            }

            yield return null;
        }

        participant.StopScriptedAnimatorWalk(participant.CurrentRunAction);
    }

    private Transform ChooseNearestScriptedGuestExitTarget(PanicParticipant participant)
    {
        if (leftExitTarget == null)
        {
            return rightExitTarget;
        }

        if (rightExitTarget == null)
        {
            return leftExitTarget;
        }

        Vector3 currentPosition = participant != null ? participant.CurrentWorldFootPosition : Vector3.zero;
        float leftDistance = Mathf.Abs(GetExitWorldX(leftExitTarget) - currentPosition.x);
        float rightDistance = Mathf.Abs(GetExitWorldX(rightExitTarget) - currentPosition.x);
        return leftDistance <= rightDistance ? leftExitTarget : rightExitTarget;
    }

    private static float GetExitWorldX(Transform exitTarget)
    {
        if (exitTarget is RectTransform exitRectTransform)
        {
            Rect rect = exitRectTransform.rect;
            return exitRectTransform.TransformPoint(new Vector3(rect.center.x, rect.yMin, 0f)).x;
        }

        return exitTarget != null ? exitTarget.position.x : 0f;
    }

    private IEnumerator RunPanicRoutine()
    {
        while (isRunning)
        {
            if (!HasSharedPanicParticipants())
            {
                yield return null;
                continue;
            }

            ChooseRandomRunTargets();
            yield return MoveParticipantsTowardAssignedTargets(true);
            yield return PlayRandomStopActions();
        }
    }

    private bool HasSharedPanicParticipants()
    {
        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant != null && !participant.IsControlledByScript)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator RunExitToDoorsRoutine()
    {
        yield return MoveParticipantsTowardAssignedTargets(false, exitMoveSpeedPixels);

        panicRoutine = null;

        if (scriptedGuestRoutine == null)
        {
            isRunning = false;
        }
    }

    private int ChooseRandomStopActions()
    {
        int maxFrameCount = 0;

        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant == null || participant.IsControlledByScript)
            {
                continue;
            }

            PanicAction stopAction = GetRandomStopAction(participant.Animation);
            Sprite[] frames = GetFrames(participant.Animation, stopAction);

            if (frames == null || frames.Length == 0)
            {
                continue;
            }

            participant.SetStopAction(stopAction, UnityEngine.Random.Range(0, frames.Length));
            maxFrameCount = Mathf.Max(maxFrameCount, frames.Length);
        }

        return maxFrameCount;
    }

    private PanicAction GetRandomStopAction(Chapter2PanicCharacterAnimation animation)
    {
        Sprite[] popFrames = GetFrames(animation, PanicAction.PanicPop);

        if (popFrames != null &&
            popFrames.Length > 0 &&
            randomPopActionChance > 0f &&
            UnityEngine.Random.value < randomPopActionChance)
        {
            return PanicAction.PanicPop;
        }

        return PanicAction.PanicHandsUp;
    }

    private void ApplyRandomStopActionFrame(int frameIndex, float motionFrame)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant == null || participant.IsControlledByScript)
            {
                continue;
            }

            participant.SetSprite(GetFrame(participant.Animation, participant.CurrentStopAction, participant.GetStopClipFrameIndex(frameIndex)));
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
            PanicParticipant participant = participants[i];

            if (participant == null || participant.IsControlledByScript)
            {
                continue;
            }

            if (routePlanner != null &&
                participant.TryChooseRoutedRunTarget(routePlanner, runDistancePixels, panicRoamRadiusPixels, verticalRunDistanceScale, worldUnitsPerRoomPixel))
            {
                continue;
            }

            participant.ChooseNextRunTarget(runDistancePixels, panicRoamRadiusPixels, verticalRunDistanceScale);
        }
    }

    private IEnumerator MoveParticipantsTowardAssignedTargets(bool jitter)
    {
        yield return MoveParticipantsTowardAssignedTargets(jitter, panicMoveSpeedPixels);
    }

    private IEnumerator MoveParticipantsTowardAssignedTargets(bool jitter, float moveSpeedPixels)
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
            bool allArrived = StepParticipantsTowardAssignedTargets(deltaTime, moveSpeedPixels);
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
        return StepParticipantsTowardAssignedTargets(deltaTime, panicMoveSpeedPixels);
    }

    private bool StepParticipantsTowardAssignedTargets(float deltaTime, float moveSpeedPixels)
    {
        bool allArrived = true;

        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant != null &&
                !participant.IsControlledByScript &&
                !participant.MovePanicOffsetTowardCurrentTarget(moveSpeedPixels, deltaTime))
            {
                allArrived = false;
            }
        }

        return allArrived;
    }

    private void ChooseDoorExitTargets()
    {
        if (leftExitTarget == null && rightExitTarget == null)
        {
            Debug.LogWarning("Chapter 2 guest panic exit requested, but no Drawing Room door targets were found.", this);
            ChooseRandomRunTargets();
            return;
        }

        List<PanicParticipant> sortedParticipants = new List<PanicParticipant>(participants);
        sortedParticipants.Sort((left, right) =>
            left.GetCurrentHorizontalPosition(worldUnitsPerRoomPixel).CompareTo(right.GetCurrentHorizontalPosition(worldUnitsPerRoomPixel)));

        int leftExitCount = sortedParticipants.Count / 2;

        for (int i = 0; i < sortedParticipants.Count; i++)
        {
            PanicParticipant participant = sortedParticipants[i];

            if (participant == null || participant.IsControlledByScript)
            {
                continue;
            }

            Transform exitTarget = i < leftExitCount
                ? leftExitTarget != null ? leftExitTarget : rightExitTarget
                : rightExitTarget != null ? rightExitTarget : leftExitTarget;

            if (!participant.TryChooseExitTarget(exitTarget, routePlanner, worldUnitsPerRoomPixel, exitOvershootPixels))
            {
                participant.ChooseNextRunTarget(runDistancePixels, panicRoamRadiusPixels, verticalRunDistanceScale);
            }
        }
    }

    private void ApplyAssignedRunFrame(int frameIndex, float motionFrame, bool jitter)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            PanicParticipant participant = participants[i];

            if (participant == null || participant.IsControlledByScript)
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

            if (participant != null && participant.IsControlledByScript)
            {
                continue;
            }

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
            case PanicAction.PanicPop:
                return animation.PanicPop;
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

    private static Transform FindSceneTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject activeObject = GameObject.Find(objectName.Trim());

        if (activeObject != null)
        {
            return activeObject.transform;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate != null && string.Equals(candidate.name, objectName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static PointClickPlayerMovement FindRoutePlanner(string playerObjectName)
    {
        string cleanPlayerObjectName = string.IsNullOrWhiteSpace(playerObjectName) ? "Player" : playerObjectName.Trim();
        GameObject playerObject = GameObject.Find(cleanPlayerObjectName);

        if (TryGetUsableRoutePlanner(playerObject, out PointClickPlayerMovement namedPlanner))
        {
            return namedPlanner;
        }

        PointClickPlayerMovement[] candidates = FindObjectsByType<PointClickPlayerMovement>(FindObjectsInactive.Exclude);

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (candidate != null &&
                string.Equals(candidate.gameObject.name, cleanPlayerObjectName, StringComparison.OrdinalIgnoreCase) &&
                IsUsableRoutePlanner(candidate))
            {
                return candidate;
            }
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            PointClickPlayerMovement candidate = candidates[i];

            if (IsUsableRoutePlanner(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryGetUsableRoutePlanner(GameObject candidateObject, out PointClickPlayerMovement planner)
    {
        planner = candidateObject != null ? candidateObject.GetComponent<PointClickPlayerMovement>() : null;
        return IsUsableRoutePlanner(planner);
    }

    private static bool IsUsableRoutePlanner(PointClickPlayerMovement candidate)
    {
        return candidate != null &&
            candidate.enabled &&
            candidate.gameObject.activeInHierarchy &&
            !IsLikelyChapterGuest(candidate.gameObject);
    }

    private static bool IsLikelyChapterGuest(GameObject candidateObject)
    {
        if (candidateObject == null)
        {
            return false;
        }

        string candidateName = candidateObject.name.Trim();
        return candidateName.StartsWith("Guest", StringComparison.OrdinalIgnoreCase);
    }

    private enum PanicAction
    {
        PanicHandsUp,
        PanicPop,
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
        private float[] animatorSpeedStates;
        private CharacterAnimatorDriver.ParameterCache[] animatorParameterCaches;
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
        private PointClickPlayerMovement routePlanner;
        private Vector2 currentRouteLogicalPosition;
        private Vector2 targetRouteLogicalPosition;
        private float routeWorldUnitsPerPixel = 0.012f;
        private bool useRouteLogicalMotion;
        private Sprite currentPanicSprite;
        private PanicAction currentRunAction = PanicAction.PanicRunDown;
        private PanicAction currentStopAction = PanicAction.PanicHandsUp;
        private int guestNumber;
        private bool controlledByScript;

        public Chapter2PanicCharacterAnimation Animation => animation;
        public bool HasSpriteTarget => spriteRenderer != null || image != null;
        public PanicAction CurrentRunAction => currentRunAction;
        public PanicAction CurrentStopAction => currentStopAction;
        public int GuestNumber => guestNumber;
        public bool IsControlledByScript => controlledByScript;
        public Vector2 CurrentPanicOffset => currentPanicOffset;
        public Vector3 CurrentWorldFootPosition => GetCurrentWorldFootPosition(routeWorldUnitsPerPixel);

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
            participant.animatorSpeedStates = new float[participant.animators.Length];
            participant.animatorParameterCaches = new CharacterAnimatorDriver.ParameterCache[participant.animators.Length];

            for (int i = 0; i < participant.animators.Length; i++)
            {
                participant.animatorEnabledStates[i] = participant.animators[i] != null && participant.animators[i].enabled;
                participant.animatorSpeedStates[i] = participant.animators[i] != null ? participant.animators[i].speed : 1f;
                participant.animatorParameterCaches[i] = CharacterAnimatorDriver.ParameterCache.FromAnimator(participant.animators[i]);
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
            this.guestNumber = guestNumber;
            int seed = Mathf.Abs((guestNumber + 1) * 37 + (participantIndex + 1) * 19);

            runDistanceScale = 0.82f + seed % 5 * 0.07f;
            moveSpeedScale = 0.9f + seed % 4 * 0.08f;
            framePhaseOffset = seed % 4;
            jitterPhase = seed * 0.618f;
            bobPixels = 2f + seed % 3 * 0.75f;
        }

        public void SetControlledByScript(bool controlled)
        {
            controlledByScript = controlled;
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

        public void ChooseNextRunTarget(float runDistancePixels, float roamRadiusPixels, float verticalDistanceScale)
        {
            useRouteLogicalMotion = false;
            float horizontalRadius = Mathf.Max(1f, roamRadiusPixels * runDistanceScale);
            float verticalRadius = Mathf.Max(1f, roamRadiusPixels * Mathf.Clamp(verticalDistanceScale, 0.1f, 1f) * runDistanceScale);
            float stepPixels = Mathf.Max(1f, runDistancePixels * runDistanceScale * UnityEngine.Random.Range(0.72f, 1.15f));

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 direction = GetRandomCardinalDirection();
                Vector2 targetOffset = currentPanicOffset + new Vector2(
                    direction.x * stepPixels,
                    direction.y * stepPixels * Mathf.Clamp(verticalDistanceScale, 0.1f, 1f));
                targetOffset = ClampPanicOffset(targetOffset, horizontalRadius, verticalRadius);

                if (Vector2.Distance(currentPanicOffset, targetOffset) > 1f)
                {
                    currentRunTargetOffset = targetOffset;
                    currentRunAction = GetRunActionForDirection(targetOffset - currentPanicOffset);
                    return;
                }
            }

            currentRunTargetOffset = Vector2.zero;
            currentRunAction = GetRunActionForDirection(Vector2.zero - currentPanicOffset);
        }

        public int GetRunClipFrameIndex(int frameIndex)
        {
            return frameIndex + framePhaseOffset;
        }

        public void SetStopAction(PanicAction stopAction, int framePhase)
        {
            currentStopAction = stopAction;
            stopFramePhaseOffset = Mathf.Max(0, framePhase);
        }

        public void SetCurrentRunAction(PanicAction runAction)
        {
            currentRunAction = runAction;
        }

        public bool BeginScriptedAnimatorWalk(PanicAction runAction, float animationSpeed)
        {
            currentPanicSprite = null;
            SetCurrentRunAction(runAction);

            if (targetTransform != null)
            {
                targetTransform.localScale = originalLocalScale;
            }

            return UpdateScriptedAnimatorWalk(runAction, animationSpeed);
        }

        public bool UpdateScriptedAnimatorWalk(PanicAction runAction, float animationSpeed)
        {
            CharacterWalkDirection direction = GetWalkDirectionForRunAction(runAction);
            bool applied = false;

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];

                if (animator == null || !WasAnimatorOriginallyEnabled(i))
                {
                    continue;
                }

                animator.enabled = true;
                animator.speed = Mathf.Max(0.1f, animationSpeed);

                CharacterAnimatorDriver.ParameterCache parameterCache = i < animatorParameterCaches.Length
                    ? animatorParameterCaches[i]
                    : CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
                parameterCache.ApplyMovement(animator, true, direction, 1f);
                applied = true;
            }

            SetCurrentRunAction(runAction);
            return applied;
        }

        public void StopScriptedAnimatorWalk(PanicAction facingAction)
        {
            CharacterWalkDirection direction = GetWalkDirectionForRunAction(facingAction);

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];

                if (animator == null || !WasAnimatorOriginallyEnabled(i))
                {
                    continue;
                }

                CharacterAnimatorDriver.ParameterCache parameterCache = i < animatorParameterCaches.Length
                    ? animatorParameterCaches[i]
                    : CharacterAnimatorDriver.ParameterCache.FromAnimator(animator);
                parameterCache.ApplyMovement(animator, false, direction, 0f);
                animator.speed = i < animatorSpeedStates.Length ? animatorSpeedStates[i] : 1f;
                animator.enabled = false;
            }
        }

        public void ChooseDirectionalRunTarget(Vector2 direction, float distancePixels)
        {
            useRouteLogicalMotion = false;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            Vector2 normalizedDirection = direction.normalized;
            currentRunTargetOffset = currentPanicOffset + normalizedDirection * Mathf.Max(1f, distancePixels);
            currentRunAction = GetRunActionForDirection(normalizedDirection);
        }

        public bool TryChooseRoutedDirectionalRunTarget(
            PointClickPlayerMovement planner,
            Vector2 direction,
            float distancePixels,
            float worldUnitsPerPixel)
        {
            if (direction.sqrMagnitude <= 0.0001f ||
                !TryGetRouteCurrentLogicalPosition(planner, worldUnitsPerPixel, out Vector2 startLogical))
            {
                return false;
            }

            float unitsPerPixel = Mathf.Max(0.0001f, worldUnitsPerPixel);
            Vector2 targetLogical = startLogical + direction.normalized * Mathf.Max(1f, distancePixels) * unitsPerPixel;
            return TryBeginRouteMotion(planner, startLogical, targetLogical, worldUnitsPerPixel);
        }

        public bool TryChooseRoutedRunTarget(
            PointClickPlayerMovement planner,
            float runDistancePixels,
            float roamRadiusPixels,
            float verticalDistanceScale,
            float worldUnitsPerPixel)
        {
            if (!TryGetRouteCurrentLogicalPosition(planner, worldUnitsPerPixel, out Vector2 startLogical))
            {
                return false;
            }

            float unitsPerPixel = Mathf.Max(0.0001f, worldUnitsPerPixel);
            float horizontalRadius = Mathf.Max(1f, roamRadiusPixels * runDistanceScale) * unitsPerPixel;
            float verticalRadius = Mathf.Max(1f, roamRadiusPixels * Mathf.Clamp(verticalDistanceScale, 0.1f, 1f) * runDistanceScale) * unitsPerPixel;
            float stepUnits = Mathf.Max(1f, runDistancePixels * runDistanceScale * UnityEngine.Random.Range(0.72f, 1.15f)) * unitsPerPixel;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 direction = GetRandomCardinalDirection();
                Vector2 candidateLogical = startLogical + new Vector2(
                    direction.x * stepUnits,
                    direction.y * stepUnits * Mathf.Clamp(verticalDistanceScale, 0.1f, 1f));
                Vector2 relative = candidateLogical - startLogical;

                candidateLogical = startLogical + new Vector2(
                    Mathf.Clamp(relative.x, -horizontalRadius, horizontalRadius),
                    Mathf.Clamp(relative.y, -verticalRadius, verticalRadius));

                if (TryBeginRouteMotion(planner, startLogical, candidateLogical, worldUnitsPerPixel))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryChooseExitTarget(
            Transform exitTarget,
            PointClickPlayerMovement planner,
            float worldUnitsPerPixel,
            float overshootPixels)
        {
            if (planner != null && TryChooseRoutedExitTarget(exitTarget, planner, worldUnitsPerPixel, overshootPixels))
            {
                return true;
            }

            useRouteLogicalMotion = false;

            if (!TryGetRoomPixelOffsetToTarget(exitTarget, worldUnitsPerPixel, out Vector2 targetOffset))
            {
                return false;
            }

            Vector2 movement = targetOffset - currentPanicOffset;

            if (overshootPixels > 0f && movement.sqrMagnitude > 0.001f)
            {
                targetOffset += movement.normalized * overshootPixels;
            }

            currentRunTargetOffset = targetOffset;
            currentRunAction = GetRunActionForDirection(currentRunTargetOffset - currentPanicOffset);
            return true;
        }

        public float GetCurrentHorizontalPosition(float worldUnitsPerPixel)
        {
            if (usesProjection)
            {
                return originalProjectionFootPoint.x + currentPanicOffset.x;
            }

            if (rectTransform != null)
            {
                return originalAnchoredPosition.x + currentPanicOffset.x;
            }

            float unitsPerPixel = Mathf.Max(0.0001f, worldUnitsPerPixel);
            return originalPosition.x + currentPanicOffset.x * unitsPerPixel;
        }

        private bool TryChooseRoutedExitTarget(
            Transform exitTarget,
            PointClickPlayerMovement planner,
            float worldUnitsPerPixel,
            float overshootPixels)
        {
            if (exitTarget == null ||
                !TryGetRouteCurrentLogicalPosition(planner, worldUnitsPerPixel, out Vector2 startLogical))
            {
                return false;
            }

            Vector3 currentWorldPosition = GetCurrentWorldFootPosition(worldUnitsPerPixel);
            Vector3 exitWorldPosition = GetExitFootWorldPosition(exitTarget);
            Vector3 targetWorldPosition = exitWorldPosition;
            Vector3 exitDirection = exitWorldPosition - currentWorldPosition;

            if (overshootPixels > 0f && exitDirection.sqrMagnitude > 0.001f)
            {
                targetWorldPosition += exitDirection.normalized * overshootPixels * Mathf.Max(0.0001f, worldUnitsPerPixel);
            }

            if (!planner.TryGetLogicalPositionFromWorldPoint(
                    targetWorldPosition,
                    true,
                    currentWorldPosition,
                    out Vector2 targetLogical))
            {
                return false;
            }

            return TryBeginRouteMotion(planner, startLogical, targetLogical, worldUnitsPerPixel);
        }

        private bool TryGetRouteCurrentLogicalPosition(
            PointClickPlayerMovement planner,
            float worldUnitsPerPixel,
            out Vector2 logicalPosition)
        {
            logicalPosition = Vector2.zero;

            if (planner == null)
            {
                return false;
            }

            Vector3 currentWorldPosition = GetCurrentWorldFootPosition(worldUnitsPerPixel);
            return planner.TryGetLogicalPositionFromWorldPoint(currentWorldPosition, true, currentWorldPosition, out logicalPosition);
        }

        private bool TryBeginRouteMotion(
            PointClickPlayerMovement planner,
            Vector2 startLogical,
            Vector2 targetLogical,
            float worldUnitsPerPixel)
        {
            if (planner == null ||
                Vector2.Distance(startLogical, targetLogical) <= 0.01f ||
                !planner.TryGetWorldPointFromLogicalPosition(targetLogical, out Vector2 requestedTargetWorldPoint) ||
                !planner.TryGetLogicalPositionFromWorldPoint(
                    requestedTargetWorldPoint,
                    true,
                    GetCurrentWorldFootPosition(worldUnitsPerPixel),
                    out Vector2 reachableTargetLogical) ||
                Vector2.Distance(startLogical, reachableTargetLogical) <= 0.01f ||
                !planner.TryGetWorldPointFromLogicalPosition(reachableTargetLogical, out Vector2 targetWorldPoint) ||
                !TryGetRoomPixelOffsetFromWorldPoint(targetWorldPoint, worldUnitsPerPixel, out Vector2 targetOffset))
            {
                return false;
            }

            routePlanner = planner;
            currentRouteLogicalPosition = startLogical;
            targetRouteLogicalPosition = reachableTargetLogical;
            routeWorldUnitsPerPixel = Mathf.Max(0.0001f, worldUnitsPerPixel);
            currentRunTargetOffset = targetOffset;
            currentRunAction = GetRunActionForDirection(targetRouteLogicalPosition - currentRouteLogicalPosition);
            useRouteLogicalMotion = true;
            return true;
        }

        private bool MoveRouteLogicalPositionTowardCurrentTarget(float pixelsPerSecond, float deltaTime)
        {
            if (routePlanner == null)
            {
                useRouteLogicalMotion = false;
                return MovePanicOffsetTowardCurrentTarget(pixelsPerSecond, deltaTime);
            }

            Vector2 previousLogicalPosition = currentRouteLogicalPosition;
            float unitsPerPixel = Mathf.Max(0.0001f, routeWorldUnitsPerPixel);
            float maxDistanceDelta = Mathf.Max(1f, pixelsPerSecond) * moveSpeedScale * unitsPerPixel * Mathf.Max(0f, deltaTime);
            currentRouteLogicalPosition = routePlanner.MoveLogicalPointToward(
                currentRouteLogicalPosition,
                targetRouteLogicalPosition,
                maxDistanceDelta);
            Vector2 logicalMovement = currentRouteLogicalPosition - previousLogicalPosition;

            if (logicalMovement.sqrMagnitude > 0.0001f)
            {
                currentRunAction = GetRunActionForDirection(logicalMovement);
            }

            if (routePlanner.TryGetWorldPointFromLogicalPosition(currentRouteLogicalPosition, out Vector2 worldPoint) &&
                TryGetRoomPixelOffsetFromWorldPoint(worldPoint, unitsPerPixel, out Vector2 nextOffset))
            {
                currentPanicOffset = nextOffset;
            }

            return Vector2.Distance(currentRouteLogicalPosition, targetRouteLogicalPosition) <= 0.03f;
        }

        public int GetStopClipFrameIndex(int frameIndex)
        {
            return frameIndex + stopFramePhaseOffset;
        }

        public bool MovePanicOffsetTowardCurrentTarget(float pixelsPerSecond, float deltaTime)
        {
            if (useRouteLogicalMotion)
            {
                return MoveRouteLogicalPositionTowardCurrentTarget(pixelsPerSecond, deltaTime);
            }

            float maxDistanceDelta = Mathf.Max(1f, pixelsPerSecond) * moveSpeedScale * Mathf.Max(0f, deltaTime);
            currentPanicOffset = Vector2.MoveTowards(currentPanicOffset, currentRunTargetOffset, maxDistanceDelta);
            return Vector2.Distance(currentPanicOffset, currentRunTargetOffset) <= 0.5f;
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

        private Vector3 GetCurrentWorldFootPosition(float worldUnitsPerPixel)
        {
            if (targetTransform != null)
            {
                return targetTransform.position;
            }

            if (usesProjection)
            {
                Vector2 roomPoint = originalProjectionFootPoint + currentPanicOffset;
                return new Vector3(roomPoint.x, roomPoint.y, originalPosition.z);
            }

            if (rectTransform != null)
            {
                Vector2 anchoredPoint = originalAnchoredPosition + currentPanicOffset;
                return new Vector3(anchoredPoint.x, anchoredPoint.y, originalPosition.z);
            }

            float unitsPerPixel = Mathf.Max(0.0001f, worldUnitsPerPixel);
            return originalPosition + new Vector3(currentPanicOffset.x * unitsPerPixel, currentPanicOffset.y * unitsPerPixel, 0f);
        }

        private bool TryGetRoomPixelOffsetFromWorldPoint(Vector2 worldPoint, float worldUnitsPerPixel, out Vector2 offset)
        {
            offset = Vector2.zero;
            Vector3 worldPosition = new Vector3(worldPoint.x, worldPoint.y, targetTransform != null ? targetTransform.position.z : originalPosition.z);

            if (usesProjection && projection != null)
            {
                Transform projectionTransform = projection.transform;
                RoomContentGroup roomContent = projectionTransform != null
                    ? projectionTransform.GetComponentInParent<RoomContentGroup>(true)
                    : null;
                RectTransform roomStage = roomContent != null ? roomContent.transform as RectTransform : null;

                if (roomStage != null)
                {
                    Vector3 localPoint = roomStage.InverseTransformPoint(worldPosition);
                    offset = new Vector2(localPoint.x, localPoint.y) - originalProjectionFootPoint;
                    return true;
                }

                CameraManager cameraManager = FindAnyObjectByType<CameraManager>(FindObjectsInactive.Include);

                if (cameraManager != null &&
                    cameraManager.TryGetActiveRoomStageLocalPoint(worldPosition, out Vector2 activeRoomLocalPoint))
                {
                    offset = activeRoomLocalPoint - originalProjectionFootPoint;
                    return true;
                }
            }

            if (rectTransform != null)
            {
                RectTransform parentRect = rectTransform.parent as RectTransform;

                if (parentRect != null)
                {
                    Vector3 localPoint = parentRect.InverseTransformPoint(worldPosition);
                    offset = new Vector2(localPoint.x, localPoint.y) - originalAnchoredPosition;
                    return true;
                }
            }

            float unitsPerPixel = Mathf.Max(0.0001f, worldUnitsPerPixel);
            Vector3 worldDelta = worldPosition - originalPosition;
            offset = new Vector2(worldDelta.x / unitsPerPixel, worldDelta.y / unitsPerPixel);
            return true;
        }

        private bool TryGetRoomPixelOffsetToTarget(Transform exitTarget, float worldUnitsPerPixel, out Vector2 offset)
        {
            offset = Vector2.zero;

            if (exitTarget == null)
            {
                return false;
            }

            Vector3 exitFootWorldPosition = GetExitFootWorldPosition(exitTarget);

            if (usesProjection && projection != null)
            {
                if (TryGetRoomLocalPoint(exitTarget, exitFootWorldPosition, out Vector2 targetFootPoint))
                {
                    offset = targetFootPoint - originalProjectionFootPoint;
                    return true;
                }

                if (projection.TryGetRoomLocalFootPointForTarget(exitTarget, out targetFootPoint))
                {
                    offset = targetFootPoint - originalProjectionFootPoint;
                    return true;
                }
            }

            if (rectTransform != null && exitTarget is RectTransform exitRectTransform)
            {
                Vector3 localExitFoot = exitRectTransform.InverseTransformPoint(exitFootWorldPosition);
                Vector2 exitAnchoredOffset = exitRectTransform.anchoredPosition + new Vector2(localExitFoot.x, localExitFoot.y);
                offset = exitAnchoredOffset - originalAnchoredPosition;
                return true;
            }

            float unitsPerPixel = Mathf.Max(0.0001f, worldUnitsPerPixel);
            Vector3 worldDelta = exitFootWorldPosition - originalPosition;
            offset = new Vector2(worldDelta.x / unitsPerPixel, worldDelta.y / unitsPerPixel);
            return true;
        }

        private static bool TryGetRoomLocalPoint(Transform target, Vector3 worldPosition, out Vector2 roomLocalPoint)
        {
            roomLocalPoint = Vector2.zero;

            if (target == null)
            {
                return false;
            }

            RoomContentGroup targetRoom = target.GetComponentInParent<RoomContentGroup>(true);
            RectTransform targetRoomStage = targetRoom != null ? targetRoom.transform as RectTransform : null;

            if (targetRoomStage == null)
            {
                return false;
            }

            Vector3 localPoint = targetRoomStage.InverseTransformPoint(worldPosition);
            roomLocalPoint = new Vector2(localPoint.x, localPoint.y);
            return true;
        }

        private static Vector3 GetExitFootWorldPosition(Transform exitTarget)
        {
            if (exitTarget is RectTransform exitRectTransform)
            {
                Rect rect = exitRectTransform.rect;
                return exitRectTransform.TransformPoint(new Vector3(rect.center.x, rect.yMin, 0f));
            }

            return exitTarget.position;
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
            routePlanner = null;
            currentRouteLogicalPosition = Vector2.zero;
            targetRouteLogicalPosition = Vector2.zero;
            routeWorldUnitsPerPixel = 0.012f;
            useRouteLogicalMotion = false;
            currentPanicSprite = null;
            controlledByScript = false;

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
                    animators[i].speed = i < animatorSpeedStates.Length ? animatorSpeedStates[i] : 1f;
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

        private static Vector2 GetRandomCardinalDirection()
        {
            switch (UnityEngine.Random.Range(0, 4))
            {
                case 0:
                    return Vector2.down;
                case 1:
                    return Vector2.left;
                case 2:
                    return Vector2.right;
                default:
                    return Vector2.up;
            }
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

        private static CharacterWalkDirection GetWalkDirectionForRunAction(PanicAction runAction)
        {
            switch (runAction)
            {
                case PanicAction.PanicRunLeft:
                    return CharacterWalkDirection.Left;
                case PanicAction.PanicRunRight:
                    return CharacterWalkDirection.Right;
                case PanicAction.PanicRunUp:
                    return CharacterWalkDirection.Up;
                case PanicAction.PanicRunDown:
                default:
                    return CharacterWalkDirection.Down;
            }
        }

        private bool WasAnimatorOriginallyEnabled(int index)
        {
            return index >= 0 &&
                index < animatorEnabledStates.Length &&
                animatorEnabledStates[index];
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

                // Panic owns guest motion only; it must not lock the global point-click cursor/input state.
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
