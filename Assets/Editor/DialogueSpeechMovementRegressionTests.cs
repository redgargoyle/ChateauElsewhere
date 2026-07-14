using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class DialogueSpeechMovementRegressionTests
{
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
