using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class DialogueSpeechMovementRegressionTests
{
    [Test]
    public void AllFourConfiguredEntrancePairsCompleteWithoutOddHallCounts()
    {
        GameObject controllerObject = new GameObject("Chapter1ArrivalController_AllPairsTest");

        try
        {
            Chapter1ArrivalController controller = controllerObject.AddComponent<Chapter1ArrivalController>();
            Type controllerType = typeof(Chapter1ArrivalController);
            Type guestType = controllerType.GetNestedType("GuestRuntimeState", BindingFlags.NonPublic);
            Type groupType = controllerType.GetNestedType("GuestGroupRuntimeState", BindingFlags.NonPublic);
            Assert.That(guestType, Is.Not.Null);
            Assert.That(groupType, Is.Not.Null);

            FieldInfo guestStatesField = controllerType.GetField("guestStates", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo guestGroupsField = controllerType.GetField("guestGroups", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo configField = guestType.GetField("Config", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo guestIndexField = guestType.GetField("GuestIndex", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo enteredHallField = guestType.GetField("EnteredEntranceHall", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo coatStoredField = guestType.GetField("CoatStored", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo movingField = guestType.GetField("MovingToDrawingRoom", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo seatedField = guestType.GetField("Seated", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo handledField = guestType.GetField("Handled", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo groupGuestsField = groupType.GetField("Guests", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo emptyRingField = groupType.GetField("EmptyRing", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo groupMovingField = groupType.GetField("MovingToDrawingRoom", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo groupCompleteField = groupType.GetField("Complete", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo buildGroups = controllerType.GetMethod("BuildGuestGroups", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo canMovePair = controllerType.GetMethod("CanMoveEntranceGroupToDrawingRoom", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo completePair = controllerType.GetMethod("CompleteEntranceGroupDrawingRoomArrival", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(guestStatesField, Is.Not.Null);
            Assert.That(guestGroupsField, Is.Not.Null);
            Assert.That(configField, Is.Not.Null);
            Assert.That(guestIndexField, Is.Not.Null);
            Assert.That(enteredHallField, Is.Not.Null);
            Assert.That(coatStoredField, Is.Not.Null);
            Assert.That(movingField, Is.Not.Null);
            Assert.That(seatedField, Is.Not.Null);
            Assert.That(handledField, Is.Not.Null);
            Assert.That(groupGuestsField, Is.Not.Null);
            Assert.That(emptyRingField, Is.Not.Null);
            Assert.That(groupMovingField, Is.Not.Null);
            Assert.That(groupCompleteField, Is.Not.Null);
            Assert.That(buildGroups, Is.Not.Null);
            Assert.That(canMovePair, Is.Not.Null);
            Assert.That(completePair, Is.Not.Null);

            IList guestStates = (IList)guestStatesField.GetValue(controller);

            for (int guestIndex = 0; guestIndex < 8; guestIndex++)
            {
                GuestArrivalConfig config = new GuestArrivalConfig();
                config.ConfigureRuntime(
                    $"guest_{guestIndex + 1}",
                    $"Guest {guestIndex + 1}",
                    null,
                    null,
                    null,
                    string.Empty,
                    null,
                    $"guest_{guestIndex + 1}_coat");
                object guest = Activator.CreateInstance(guestType, true);
                configField.SetValue(guest, config);
                guestIndexField.SetValue(guest, guestIndex);
                enteredHallField.SetValue(guest, true);
                coatStoredField.SetValue(guest, true);
                guestStates.Add(guest);
            }

            buildGroups.Invoke(controller, null);
            IList groups = (IList)guestGroupsField.GetValue(controller);
            Assert.That(groups.Count, Is.EqualTo(5), "Four guest pairs should be followed only by the authored empty doorbell ring.");
            Assert.That((bool)emptyRingField.GetValue(groups[4]), Is.True);

            for (int pairIndex = 0; pairIndex < 4; pairIndex++)
            {
                object pair = groups[pairIndex];
                IList pairGuests = (IList)groupGuestsField.GetValue(pair);
                Assert.That(pairGuests.Count, Is.EqualTo(2));
                Assert.That((int)guestIndexField.GetValue(pairGuests[0]), Is.EqualTo(pairIndex * 2));
                Assert.That((int)guestIndexField.GetValue(pairGuests[1]), Is.EqualTo(pairIndex * 2 + 1));
                Assert.That((bool)canMovePair.Invoke(controller, new[] { pair }), Is.True);

                movingField.SetValue(pairGuests[0], true);
                movingField.SetValue(pairGuests[1], true);
                groupMovingField.SetValue(pair, true);
                completePair.Invoke(controller, new[] { pair });

                Assert.That((bool)seatedField.GetValue(pairGuests[0]), Is.True);
                Assert.That((bool)seatedField.GetValue(pairGuests[1]), Is.True);
                Assert.That((bool)handledField.GetValue(pairGuests[0]), Is.True);
                Assert.That((bool)handledField.GetValue(pairGuests[1]), Is.True);
                Assert.That((bool)groupCompleteField.GetValue(pair), Is.True);

                int expectedDrawingRoomCount = (pairIndex + 1) * 2;
                int expectedHallCount = 8 - expectedDrawingRoomCount;
                string hudState = controller.BuildShortHudState("Test");
                Assert.That(
                    hudState,
                    Does.Contain($"Hall: {expectedHallCount}  Drawing Room: {expectedDrawingRoomCount}"),
                    $"Completing pair {pairIndex + 1} must change the hall count by exactly two.");
            }
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void LowerEntranceTargetUsesLargerCalibratedScale()
    {
        GameObject movementObject = new GameObject("EntranceTargetScaleTest");

        try
        {
            PointClickPlayerMovement movement = movementObject.AddComponent<PointClickPlayerMovement>();
            movement.SetButlerFrontFinalLocalScaleForRoom("Grand Entrance Hall", -381.67844f, 2.1461537f, false);
            movement.SetButlerBackFinalLocalScaleForRoom("Grand Entrance Hall", -98.47123f, 0.661823f, false);

            Assert.That(
                movement.TryEvaluateButlerCharacterScale(
                    "Grand Entrance Hall",
                    new Vector2(-704f, -116f),
                    out PointClickPlayerMovement.ButlerCharacterScaleSample oldTargetSample),
                Is.True);
            Assert.That(
                movement.TryEvaluateButlerCharacterScale(
                    "Grand Entrance Hall",
                    new Vector2(-704f, -210f),
                    out PointClickPlayerMovement.ButlerCharacterScaleSample floorTargetSample),
                Is.True);

            Assert.That(floorTargetSample.Depth01, Is.LessThan(oldTargetSample.Depth01));
            Assert.That(
                floorTargetSample.ButlerFinalLocalScaleY,
                Is.GreaterThan(oldTargetSample.ButlerFinalLocalScaleY * 1.5f),
                "Moving the passage target onto the foreground floor should materially enlarge characters through the existing Y-depth scale curve.");

            MethodInfo getPairOffset = typeof(Chapter1ArrivalController).GetMethod(
                "GetWorldGuestGridOffset",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(getPairOffset, Is.Not.Null);
            Vector2 firstOffset = (Vector2)getPairOffset.Invoke(
                movementObject.AddComponent<Chapter1ArrivalController>(),
                new object[] { 0, 2, 0.75f });
            Vector2 secondOffset = (Vector2)getPairOffset.Invoke(
                movementObject.GetComponent<Chapter1ArrivalController>(),
                new object[] { 1, 2, 0.75f });
            Assert.That(firstOffset.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(secondOffset.y, Is.EqualTo(0f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(movementObject);
        }
    }

    [Test]
    public void UnequalEntrancePairRoutesUseOneTravelDuration()
    {
        MethodInfo calculateSpeed = typeof(Chapter1ArrivalController).GetMethod(
            "CalculateSynchronizedMoveSpeed",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(calculateSpeed, Is.Not.Null);

        const float guestFiveDistance = 742.9f;
        const float guestSixDistance = 946.3f;
        const float defaultSpeed = 2.2f;
        float sharedDuration = guestSixDistance / defaultSpeed;
        float guestFiveSpeed = (float)calculateSpeed.Invoke(
            null,
            new object[] { guestFiveDistance, sharedDuration, defaultSpeed });
        float guestSixSpeed = (float)calculateSpeed.Invoke(
            null,
            new object[] { guestSixDistance, sharedDuration, defaultSpeed });

        Assert.That(guestFiveSpeed, Is.LessThan(guestSixSpeed), "The nearer guest should slow down instead of reaching the passage alone.");
        Assert.That(
            guestFiveDistance / guestFiveSpeed,
            Is.EqualTo(guestSixDistance / guestSixSpeed).Within(0.001f),
            "Guests 5 and 6 should reach the passage together despite unequal route lengths.");
    }

    [Test]
    public void EntrancePairWaitsUntilBothCoatsAreStored()
    {
        GameObject controllerObject = new GameObject("Chapter1ArrivalController_PairGateTest");

        try
        {
            Chapter1ArrivalController controller = controllerObject.AddComponent<Chapter1ArrivalController>();
            Type controllerType = typeof(Chapter1ArrivalController);
            Type guestType = controllerType.GetNestedType("GuestRuntimeState", BindingFlags.NonPublic);
            Type groupType = controllerType.GetNestedType("GuestGroupRuntimeState", BindingFlags.NonPublic);
            Assert.That(guestType, Is.Not.Null);
            Assert.That(groupType, Is.Not.Null);

            object firstGuest = Activator.CreateInstance(guestType, true);
            object secondGuest = Activator.CreateInstance(guestType, true);
            object pair = Activator.CreateInstance(groupType, true);
            FieldInfo coatStoredField = guestType.GetField("CoatStored", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo guestsField = groupType.GetField("Guests", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo canMovePair = controllerType.GetMethod(
                "CanMoveEntranceGroupToDrawingRoom",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(coatStoredField, Is.Not.Null);
            Assert.That(guestsField, Is.Not.Null);
            Assert.That(canMovePair, Is.Not.Null);

            IList pairGuests = (IList)guestsField.GetValue(pair);
            pairGuests.Add(firstGuest);
            coatStoredField.SetValue(firstGuest, true);

            Assert.That(
                (bool)canMovePair.Invoke(controller, new[] { pair }),
                Is.False,
                "A malformed one-guest group must never depart and leave an odd hall count.");

            pairGuests.Add(secondGuest);
            coatStoredField.SetValue(secondGuest, false);
            Assert.That(
                (bool)canMovePair.Invoke(controller, new[] { pair }),
                Is.False,
                "Returning only one coat must not release either member of the pair.");

            coatStoredField.SetValue(secondGuest, true);
            Assert.That(
                (bool)canMovePair.Invoke(controller, new[] { pair }),
                Is.True,
                "The pair should be released together once both coats are stored.");
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void EntrancePairExitBarrierWaitsForBothMovers()
    {
        GameObject controllerObject = new GameObject("Chapter1ArrivalController_PairBarrierTest");
        GameObject firstGuestObject = new GameObject("PairBarrierGuest01");
        GameObject secondGuestObject = new GameObject("PairBarrierGuest02");

        try
        {
            Chapter1ArrivalController controller = controllerObject.AddComponent<Chapter1ArrivalController>();
            NPCWaypointMover firstMover = firstGuestObject.AddComponent<NPCWaypointMover>();
            NPCWaypointMover secondMover = secondGuestObject.AddComponent<NPCWaypointMover>();
            Type controllerType = typeof(Chapter1ArrivalController);
            Type guestType = controllerType.GetNestedType("GuestRuntimeState", BindingFlags.NonPublic);
            Type groupType = controllerType.GetNestedType("GuestGroupRuntimeState", BindingFlags.NonPublic);
            Assert.That(guestType, Is.Not.Null);
            Assert.That(groupType, Is.Not.Null);

            object firstGuest = Activator.CreateInstance(guestType, true);
            object secondGuest = Activator.CreateInstance(guestType, true);
            object pair = Activator.CreateInstance(groupType, true);
            FieldInfo guestObjectField = guestType.GetField("GuestObject", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo moverField = guestType.GetField("Mover", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo movingField = guestType.GetField("MovingToDrawingRoom", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo guestsField = groupType.GetField("Guests", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo moverIsMovingField = typeof(NPCWaypointMover).GetField("isMoving", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo navigationManagerField = controllerType.GetField("navigationManager", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pairReachedExit = controllerType.GetMethod(
                "HasEntranceGroupReachedDrawingRoomExit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(guestObjectField, Is.Not.Null);
            Assert.That(moverField, Is.Not.Null);
            Assert.That(movingField, Is.Not.Null);
            Assert.That(guestsField, Is.Not.Null);
            Assert.That(moverIsMovingField, Is.Not.Null);
            Assert.That(navigationManagerField, Is.Not.Null);
            Assert.That(pairReachedExit, Is.Not.Null);

            navigationManagerField.SetValue(controller, null);

            guestObjectField.SetValue(firstGuest, firstGuestObject);
            guestObjectField.SetValue(secondGuest, secondGuestObject);
            moverField.SetValue(firstGuest, firstMover);
            moverField.SetValue(secondGuest, secondMover);
            movingField.SetValue(firstGuest, true);
            movingField.SetValue(secondGuest, true);
            IList pairGuests = (IList)guestsField.GetValue(pair);
            pairGuests.Add(firstGuest);
            pairGuests.Add(secondGuest);

            moverIsMovingField.SetValue(firstMover, false);
            moverIsMovingField.SetValue(secondMover, true);
            Assert.That(
                (bool)pairReachedExit.Invoke(controller, new[] { pair }),
                Is.False,
                "The faster guest reaching the exit must not transfer without its partner.");

            moverIsMovingField.SetValue(secondMover, false);
            Assert.That(
                (bool)pairReachedExit.Invoke(controller, new[] { pair }),
                Is.True,
                "The pair may transfer only after both movers have reached the exit.");
        }
        finally
        {
            Object.DestroyImmediate(secondGuestObject);
            Object.DestroyImmediate(firstGuestObject);
            Object.DestroyImmediate(controllerObject);
        }
    }

    [Test]
    public void GuestSpeakerResolutionDoesNotSelectDifferentGuest()
    {
        GameObject unrelatedGuestObject = new GameObject("Guest1");

        try
        {
            ActorRoomState unrelatedActor = unrelatedGuestObject.AddComponent<ActorRoomState>();
            unrelatedActor.SetActorId("Guest1");

            bool resolved = SpeakingCharacterIndicator.TryResolveGuestSpeakerTarget(
                "SUB_CH01_G9876_GREETING",
                "Guest9876",
                out Transform target,
                out ActorRoomState actor);

            Assert.That(resolved, Is.False, "Guest identity resolution must not fall back to a different visible guest.");
            Assert.That(target, Is.Null);
            Assert.That(actor, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(unrelatedGuestObject);
        }
    }

    [Test]
    public void QueuedGuestSpeechFreezesAndThenResumesExistingMovement()
    {
        GameObject guestObject = new GameObject("Guest99");
        GameObject targetObject = new GameObject("GuestSpeechMovementTarget");
        GameObject speechObject = new GameObject("DialogueSpeechService_Test");

        try
        {
            ActorRoomState actorState = guestObject.AddComponent<ActorRoomState>();
            actorState.SetActorId("Guest99");
            NPCWaypointMover mover = guestObject.AddComponent<NPCWaypointMover>();
            mover.MoveSpeed = 1f;
            targetObject.transform.position = new Vector3(10f, 0f, 0f);

            IEnumerator movementRoutine = mover.MoveToRoutine(targetObject.transform);
            Assert.That(movementRoutine.MoveNext(), Is.True);

            DialogueSpeechService speechService = speechObject.AddComponent<DialogueSpeechService>();
            SubtitleService subtitleService = speechObject.AddComponent<SubtitleService>();
            GuestVoiceLinePlayback voicePlayback = speechObject.AddComponent<GuestVoiceLinePlayback>();
            SpeakingCharacterIndicator speakingIndicator = speechObject.AddComponent<SpeakingCharacterIndicator>();
            SetPrivateField(speechService, "subtitleService", subtitleService);
            SetPrivateField(speechService, "voicePlayback", voicePlayback);
            SetPrivateField(speechService, "speakingIndicator", speakingIndicator);
            IEnumerator activeButlerSpeech = GetInnerSpeechRoutine(
                speechService,
                "TEST_BUTLER_QUEUE_HOLD",
                "Butler",
                "Please wait while this line holds the shared queue.");
            Assert.That(activeButlerSpeech.MoveNext(), Is.True);
            Assert.That(speechService.IsNormalSpeechActive, Is.True);

            IEnumerator queuedGuestSpeech = GetInnerSpeechRoutine(
                speechService,
                "SUB_CH01_G99_GREETING",
                "Guest99",
                "I will wait here until it is my turn.");
            Assert.That(queuedGuestSpeech.MoveNext(), Is.True, "The guest line should wait behind the active Butler line.");
            IEnumerator secondQueuedGuestSpeech = GetInnerSpeechRoutine(
                speechService,
                "SUB_CH01_G99_ANNOYED",
                "Guest99",
                "I also have another line waiting behind my first one.");
            Assert.That(secondQueuedGuestSpeech.MoveNext(), Is.True, "The second guest line should join the same shared queue.");

            Vector3 positionBeforeQueueWaitStep = guestObject.transform.position;
            Assert.That(movementRoutine.MoveNext(), Is.True);
            Assert.That(
                guestObject.transform.position,
                Is.EqualTo(positionBeforeQueueWaitStep),
                "A guest waiting in the shared speech queue must be frozen in place.");

            speechService.SkipCurrentSpeech();
            Assert.That(activeButlerSpeech.MoveNext(), Is.False);
            Assert.That(queuedGuestSpeech.MoveNext(), Is.True, "The queued guest line should begin after the Butler line completes.");

            Vector3 positionBeforeSpeakingStep = guestObject.transform.position;
            Assert.That(movementRoutine.MoveNext(), Is.True);
            Assert.That(
                guestObject.transform.position,
                Is.EqualTo(positionBeforeSpeakingStep),
                "The queued guest must remain frozen while delivering the line.");

            speechService.SkipCurrentSpeech();
            Assert.That(queuedGuestSpeech.MoveNext(), Is.False);

            Vector3 positionBetweenQueuedLines = guestObject.transform.position;
            Assert.That(movementRoutine.MoveNext(), Is.True);
            Assert.That(
                guestObject.transform.position,
                Is.EqualTo(positionBetweenQueuedLines),
                "A second queued line from the same guest must keep the movement pause leased.");

            Assert.That(secondQueuedGuestSpeech.MoveNext(), Is.True, "The second queued guest line should begin next.");
            Vector3 positionBeforeSecondSpeechStep = guestObject.transform.position;
            Assert.That(movementRoutine.MoveNext(), Is.True);
            Assert.That(
                guestObject.transform.position,
                Is.EqualTo(positionBeforeSecondSpeechStep),
                "The guest must remain frozen while delivering the second queued line.");

            speechService.SkipCurrentSpeech();
            Assert.That(secondQueuedGuestSpeech.MoveNext(), Is.False);

            Vector3 positionBeforeResumeStep = guestObject.transform.position;
            Assert.That(movementRoutine.MoveNext(), Is.True);
            Assert.That(
                Vector2.Distance(positionBeforeResumeStep, guestObject.transform.position),
                Is.GreaterThan(0.0001f),
                "The same guest movement must resume after the queued line completes.");

            IEnumerator cancellationHoldSpeech = GetInnerSpeechRoutine(
                speechService,
                "TEST_BUTLER_CANCELLATION_HOLD",
                "Butler",
                "Hold the queue once more so cancellation can be verified.");
            Assert.That(cancellationHoldSpeech.MoveNext(), Is.True);
            IEnumerator cancelledGuestSpeech = GetInnerSpeechRoutine(
                speechService,
                "SUB_CH01_G99_AMBIENT",
                "Guest99",
                "This queued line will be cancelled.");
            Assert.That(cancelledGuestSpeech.MoveNext(), Is.True);

            Vector3 positionBeforeCancellation = guestObject.transform.position;
            Assert.That(movementRoutine.MoveNext(), Is.True);
            Assert.That(guestObject.transform.position, Is.EqualTo(positionBeforeCancellation));

            speechService.CancelQueuedSpeech();
            Vector3 positionAfterCancellation = guestObject.transform.position;
            Assert.That(movementRoutine.MoveNext(), Is.True);
            Assert.That(
                Vector2.Distance(positionAfterCancellation, guestObject.transform.position),
                Is.GreaterThan(0.0001f),
                "Cancelling the shared queue must release guest movement immediately.");
        }
        finally
        {
            DialogueSpeechService speechService = speechObject != null
                ? speechObject.GetComponent<DialogueSpeechService>()
                : null;
            speechService?.CancelQueuedSpeech();
            Object.DestroyImmediate(speechObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(guestObject);
        }
    }

    [UnityTest]
    public IEnumerator GuestInterruptionPausesActiveLineAndPreservesQueuedDialogue()
    {
        yield return new EnterPlayMode();

        GameObject speechObject = new GameObject("DialogueSpeechService_InterruptionTest");
        DialogueSpeechService speechService = null;

        try
        {
            speechService = speechObject.AddComponent<DialogueSpeechService>();
            SubtitleService subtitleService = speechObject.AddComponent<SubtitleService>();
            GuestVoiceLinePlayback voicePlayback = speechObject.AddComponent<GuestVoiceLinePlayback>();
            SpeakingCharacterIndicator speakingIndicator = speechObject.AddComponent<SpeakingCharacterIndicator>();
            SetPrivateField(speechService, "subtitleService", subtitleService);
            SetPrivateField(speechService, "voicePlayback", voicePlayback);
            SetPrivateField(speechService, "speakingIndicator", speakingIndicator);

            List<string> startedLines = new List<string>();
            int originalCompletions = 0;

            speechService.BeginSpeakLine(
                "TEST_GUEST_ORIGINAL",
                "Guest01",
                "ORIGINAL",
                onComplete: () => originalCompletions++,
                showSubtitleOverlay: false,
                onSpeechLineStarted: (_, text) => startedLines.Add(text));
            yield return null;

            speechService.BeginSpeakLine(
                "TEST_GUEST_QUEUED",
                "Guest02",
                "QUEUED",
                showSubtitleOverlay: false,
                onSpeechLineStarted: (_, text) => startedLines.Add(text));
            yield return null;

            CollectionAssert.AreEqual(new[] { "ORIGINAL" }, startedLines);
            DialogueSpeechService.SpeechInterruption activeSpeech = speechService.GetCurrentSpeech();
            Assert.That(activeSpeech.HadActiveSpeech, Is.True);
            Assert.That(activeSpeech.HadQueuedSpeech, Is.True);
            Assert.That(activeSpeech.LineId, Is.EqualTo("TEST_GUEST_ORIGINAL"));
            Assert.That(activeSpeech.SpeakerId, Is.EqualTo("Guest01"));
            Assert.That(activeSpeech.SpeakerDisplayName, Is.EqualTo("Guest01"));
            Assert.That(activeSpeech.Text, Is.EqualTo("ORIGINAL"));

            bool interruptionStarted = speechService.InterruptCurrentSpeechAndResume(
                "TEST_GUEST_INTERRUPTED",
                "Guest01",
                "INTERRUPTED",
                showSubtitleOverlay: false,
                onInterruptionStarted: (_, text) => startedLines.Add(text));

            Assert.That(interruptionStarted, Is.True);
            Assert.That(speechService.IsSpeechInterruptionActive, Is.True);
            Assert.That(speechService.GetCurrentSpeech().LineId, Is.EqualTo("TEST_GUEST_ORIGINAL"));
            Assert.That(speechService.GetCurrentSpeech().SpeakerId, Is.EqualTo("Guest01"));
            CollectionAssert.AreEqual(new[] { "ORIGINAL", "INTERRUPTED" }, startedLines);

            yield return null;
            Assert.That(originalCompletions, Is.Zero, "Pausing for the interruption must not complete the original line.");
            CollectionAssert.AreEqual(
                new[] { "ORIGINAL", "INTERRUPTED" },
                startedLines,
                "Queued dialogue must remain blocked while the interruption line is active.");

            speechService.SkipCurrentSpeech();
            yield return null;

            Assert.That(speechService.IsSpeechInterruptionActive, Is.False);
            Assert.That(speechService.IsNormalSpeechActive, Is.True);
            Assert.That(speechService.GetCurrentSpeech().LineId, Is.EqualTo("TEST_GUEST_ORIGINAL"));
            Assert.That(originalCompletions, Is.Zero, "The original line must resume instead of restarting or completing.");

            speechService.SkipCurrentSpeech();

            for (int frame = 0; frame < 5 && !startedLines.Contains("QUEUED"); frame++)
            {
                yield return null;
            }

            Assert.That(originalCompletions, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { "ORIGINAL", "INTERRUPTED", "QUEUED" },
                startedLines,
                "The pending line must follow the resumed original line without being discarded.");

            speechService.CancelQueuedSpeech();
            yield return null;
            startedLines.Clear();

            speechService.BeginSpeakLine(
                "TEST_CANCELLED_ORIGINAL",
                "Guest01",
                "CANCELLED ORIGINAL",
                showSubtitleOverlay: false,
                onSpeechLineStarted: (_, text) => startedLines.Add(text));
            yield return null;
            speechService.BeginSpeakLine(
                "TEST_CANCELLED_QUEUED",
                "Guest02",
                "CANCELLED QUEUED",
                showSubtitleOverlay: false,
                onSpeechLineStarted: (_, text) => startedLines.Add(text));
            yield return null;

            Assert.That(
                speechService.InterruptCurrentSpeechAndResume(
                    "TEST_CANCELLED_INTERRUPTION",
                    "Guest01",
                    "CANCELLED INTERRUPTION",
                    showSubtitleOverlay: false,
                    onInterruptionStarted: (_, text) => startedLines.Add(text)),
                Is.True);
            MethodInfo roomChangeHandler = typeof(DialogueSpeechService).GetMethod(
                "HandleCurrentRoomChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(roomChangeHandler, Is.Not.Null);
            roomChangeHandler.Invoke(speechService, new object[] { "Drawing Room" });
            yield return null;
            yield return null;

            CollectionAssert.AreEqual(
                new[] { "CANCELLED ORIGINAL", "CANCELLED INTERRUPTION" },
                startedLines,
                "Changing rooms must not resurrect the paused or pending dialogue in the new room.");
            Assert.That(speechService.IsSpeechInterruptionActive, Is.False);
            Assert.That(speechService.IsNormalSpeechActive, Is.False);
        }
        finally
        {
            speechService?.CancelQueuedSpeech();
            Object.Destroy(speechObject);
        }

        yield return null;
        yield return new ExitPlayMode();
    }

    private static IEnumerator GetInnerSpeechRoutine(
        DialogueSpeechService service,
        string lineId,
        string speaker,
        string text)
    {
        IEnumerator wrapper = service.SpeakLine(
            lineId,
            speaker,
            text,
            allowOverlap: false,
            blockInput: false,
            showSubtitleOverlay: false);

        Assert.That(wrapper.MoveNext(), Is.True);
        Assert.That(wrapper.Current, Is.InstanceOf<IEnumerator>());
        return (IEnumerator)wrapper.Current;
    }

    private static void SetPrivateField<T>(DialogueSpeechService service, string fieldName, T value)
        where T : Component
    {
        FieldInfo field = typeof(DialogueSpeechService).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        field.SetValue(service, value);
    }
}
