using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class AudioMixSafetyRegressionTests
{
    private const float ExpectedWalkingHighPassCutoff = 180f;
    private const float MaximumPanicScreamVolume = 0.08f;

    [Test]
    public void SharedAudioServiceCreatesConfiguredSafetyFilters()
    {
        GameObject audioObject = new GameObject("SharedAudioSafetyFilterTest");

        try
        {
            AudioSource source = audioObject.AddComponent<AudioSource>();
            MethodInfo method = typeof(GameAudioSettings).GetMethod(
                "EnsureSafetyFilters",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(method, Is.Not.Null, "GameAudioSettings should own the shared filter setup.");

            object[] arguments = { source, 180f, 1.1f, 8000f, 1f, null, null };
            method.Invoke(null, arguments);

            AudioHighPassFilter highPass = arguments[5] as AudioHighPassFilter;
            AudioLowPassFilter lowPass = arguments[6] as AudioLowPassFilter;

            Assert.That(highPass, Is.Not.Null);
            Assert.That(lowPass, Is.Not.Null);
            Assert.That(highPass.enabled, Is.True);
            Assert.That(lowPass.enabled, Is.True);
            Assert.That(highPass.cutoffFrequency, Is.EqualTo(180f).Within(0.1f));
            Assert.That(lowPass.cutoffFrequency, Is.EqualTo(8000f).Within(0.1f));
        }
        finally
        {
            Object.DestroyImmediate(audioObject);
        }
    }

    [Test]
    public void PlayerAndGuestWalkingSourcesUseMixFriendlyHighPassFilters()
    {
        AudioClip testClip = AudioClip.Create("WalkingSafetyTest", 128, 1, 44100, false);
        GameObject playerObject = new GameObject("PlayerWalkingSafetyTest");
        GameObject guestObject = new GameObject("GuestWalkingSafetyTest");

        try
        {
            PlayerFootstepAudio player = playerObject.AddComponent<PlayerFootstepAudio>();
            SetPrivateField(player, "clip", testClip);
            InvokePrivate(player, "ApplyConfiguration");

            GuestFootstepCatalog catalog = Resources.Load<GuestFootstepCatalog>("Audio/GuestFootstepCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryGetFootstepsForGuest(1, out _, out float volume, out float cutoff, out float resonance),
                Is.True);

            GuestFootstepAudio guest = guestObject.AddComponent<GuestFootstepAudio>();
            guest.Configure(testClip, volume, cutoff, resonance, 9000f);

            AssertWalkingFilters(playerObject.transform.Find("Audio_ButlerFootsteps"));
            AssertWalkingFilters(guestObject.transform.Find("Audio_GuestFootsteps"));
        }
        finally
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(guestObject);
            Object.DestroyImmediate(testClip);
        }
    }

    [Test]
    public void DoorCatalogAppliesAQuietBandLimitedMixToSharedSource()
    {
        DoorOpenSoundCatalog catalog = Resources.Load<DoorOpenSoundCatalog>("Audio/DoorOpenSoundCatalog");
        GameObject audioObject = new GameObject("DoorMixSafetyTest");

        try
        {
            Assert.That(catalog, Is.Not.Null);
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.volume = 0.8f;

            MethodInfo method = typeof(DoorOpenSoundCatalog).GetMethod(
                "ApplyMixTo",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(method, Is.Not.Null, "The existing door catalog should own the shared door-source mix.");
            method.Invoke(catalog, new object[] { source });

            GameAudioSourceVolume binding = audioObject.GetComponent<GameAudioSourceVolume>();
            AudioHighPassFilter highPass = audioObject.GetComponent<AudioHighPassFilter>();
            AudioLowPassFilter lowPass = audioObject.GetComponent<AudioLowPassFilter>();

            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.BaseVolume, Is.LessThanOrEqualTo(0.4f));
            Assert.That(highPass, Is.Not.Null);
            Assert.That(highPass.cutoffFrequency, Is.GreaterThanOrEqualTo(150f));
            Assert.That(lowPass, Is.Not.Null);
            Assert.That(lowPass.cutoffFrequency, Is.LessThanOrEqualTo(10000f));
        }
        finally
        {
            Object.DestroyImmediate(audioObject);
        }
    }

    [Test]
    public void DoorFallbackStillAppliesConservativeGainAndFilters()
    {
        GameObject triggerObject = new GameObject("DoorFallbackMixSafetyTest");
        GameObject audioObject = new GameObject("DoorFallbackAudioSourceTest");

        try
        {
            DoorTriggerNavigation trigger = triggerObject.AddComponent<DoorTriggerNavigation>();
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.volume = 0.8f;
            SetPrivateField(trigger, "doorOpenAudioSource", source);
            SetPrivateField(trigger, "doorOpenSoundCatalog", (DoorOpenSoundCatalog)null);
            SetPrivateField(trigger, "doorOpenSoundCatalogResourcePath", "Audio/DoesNotExist");

            InvokePrivate(trigger, "ApplyNavigationAudioMix");

            GameAudioSourceVolume binding = audioObject.GetComponent<GameAudioSourceVolume>();
            AudioHighPassFilter highPass = audioObject.GetComponent<AudioHighPassFilter>();
            AudioLowPassFilter lowPass = audioObject.GetComponent<AudioLowPassFilter>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.BaseVolume, Is.LessThanOrEqualTo(0.4f));
            Assert.That(highPass, Is.Not.Null);
            Assert.That(highPass.cutoffFrequency, Is.GreaterThanOrEqualTo(150f));
            Assert.That(lowPass, Is.Not.Null);
            Assert.That(lowPass.cutoffFrequency, Is.LessThanOrEqualTo(10000f));
        }
        finally
        {
            Object.DestroyImmediate(triggerObject);
            Object.DestroyImmediate(audioObject);
        }
    }

    [Test]
    public void FireplaceAmbienceIsHighAndLowPassFiltered()
    {
        GameObject audioObject = new GameObject("FireplaceMixSafetyTest");

        try
        {
            audioObject.AddComponent<AudioSource>();
            FireplaceAmbienceController controller = audioObject.AddComponent<FireplaceAmbienceController>();
            InvokePrivate(controller, "ResolveReferences");

            AudioHighPassFilter highPass = audioObject.GetComponent<AudioHighPassFilter>();
            AudioLowPassFilter lowPass = audioObject.GetComponent<AudioLowPassFilter>();

            Assert.That(highPass, Is.Not.Null);
            Assert.That(highPass.cutoffFrequency, Is.GreaterThanOrEqualTo(180f));
            Assert.That(lowPass, Is.Not.Null, "Fireplace hiss should have a controlled upper-frequency ceiling.");
            Assert.That(lowPass.cutoffFrequency, Is.LessThanOrEqualTo(10000f));
        }
        finally
        {
            Object.DestroyImmediate(audioObject);
        }
    }

    [Test]
    public void FireplaceLowPassHonorsStrongerFilteringAndCapsUnsafeValues()
    {
        GameObject audioObject = new GameObject("FireplaceLowPassClampTest");

        try
        {
            audioObject.AddComponent<AudioSource>();
            FireplaceAmbienceController controller = audioObject.AddComponent<FireplaceAmbienceController>();

            SetPrivateField(controller, "lowPassCutoffFrequency", 6000f);
            InvokePrivate(controller, "ResolveReferences");
            AudioLowPassFilter lowPass = audioObject.GetComponent<AudioLowPassFilter>();
            Assert.That(lowPass, Is.Not.Null);
            Assert.That(lowPass.cutoffFrequency, Is.EqualTo(6000f).Within(0.1f));

            SetPrivateField(controller, "lowPassCutoffFrequency", 20000f);
            InvokePrivate(controller, "ResolveReferences");
            Assert.That(lowPass.cutoffFrequency, Is.LessThanOrEqualTo(8500f));
        }
        finally
        {
            Object.DestroyImmediate(audioObject);
        }
    }

    [Test]
    public void EveryPanicScreamIsSharplyCappedAndFilterProtected()
    {
        Chapter2PanicScreamCatalog catalog = Resources.Load<Chapter2PanicScreamCatalog>("Audio/Chapter2PanicScreamCatalog");

        Assert.That(catalog, Is.Not.Null);

        for (int guestNumber = 1; guestNumber <= 8; guestNumber++)
        {
            Assert.That(
                catalog.TryGetScreamForGuest(guestNumber, out AudioClip clip, out float volume),
                Is.True,
                $"Guest {guestNumber} should retain an assigned panic scream.");
            Assert.That(clip, Is.Not.Null);
            Assert.That(volume, Is.GreaterThan(0f));
            Assert.That(
                volume,
                Is.LessThanOrEqualTo(MaximumPanicScreamVolume),
                $"Guest {guestNumber}'s scream must stay below the simultaneous-voice safety cap.");
        }

        PropertyInfo highPassProperty = typeof(Chapter2PanicScreamCatalog).GetProperty("HighPassCutoffFrequency");
        PropertyInfo lowPassProperty = typeof(Chapter2PanicScreamCatalog).GetProperty("LowPassCutoffFrequency");
        Assert.That(highPassProperty, Is.Not.Null);
        Assert.That(lowPassProperty, Is.Not.Null);
        Assert.That((float)highPassProperty.GetValue(catalog), Is.GreaterThanOrEqualTo(150f));
        Assert.That((float)lowPassProperty.GetValue(catalog), Is.LessThanOrEqualTo(7000f));

        string panicControllerText = File.ReadAllText(
            "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs");
        Assert.That(panicControllerText, Does.Contain("EnsureSafetyFilters"));
        Assert.That(panicControllerText, Does.Contain("HighPassCutoffFrequency"));
        Assert.That(panicControllerText, Does.Contain("LowPassCutoffFrequency"));
    }

    [Test]
    public void ConfiguredPanicScreamSourceHasCappedGainAndFilters()
    {
        AudioClip testClip = AudioClip.Create("PanicScreamSafetyTest", 128, 1, 44100, false);
        GameObject guestObject = new GameObject("Guest99");

        try
        {
            ActorRoomState actorState = guestObject.AddComponent<ActorRoomState>();
            actorState.SetActorId("Guest99");
            System.Type participantType = typeof(Chapter2GuestPanicController).GetNestedType(
                "PanicParticipant",
                BindingFlags.NonPublic);
            Assert.That(participantType, Is.Not.Null);

            MethodInfo createMethod = participantType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            Assert.That(createMethod, Is.Not.Null);
            object participant = createMethod.Invoke(null, new object[] { actorState, null });

            MethodInfo configureMethod = participantType.GetMethod(
                "ConfigurePanicScream",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(configureMethod, Is.Not.Null);
            configureMethod.Invoke(
                participant,
                new object[] { testClip, 1f, "Audio_Ch2PanicScream", 180f, 0.8f, 6000f, 0.8f });

            Transform audioTransform = guestObject.transform.Find("Audio_Ch2PanicScream");
            Assert.That(audioTransform, Is.Not.Null);
            GameAudioSourceVolume binding = audioTransform.GetComponent<GameAudioSourceVolume>();
            AudioHighPassFilter highPass = audioTransform.GetComponent<AudioHighPassFilter>();
            AudioLowPassFilter lowPass = audioTransform.GetComponent<AudioLowPassFilter>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.BaseVolume, Is.LessThanOrEqualTo(MaximumPanicScreamVolume));
            Assert.That(highPass, Is.Not.Null);
            Assert.That(highPass.cutoffFrequency, Is.EqualTo(180f).Within(0.1f));
            Assert.That(lowPass, Is.Not.Null);
            Assert.That(lowPass.cutoffFrequency, Is.EqualTo(6000f).Within(0.1f));
        }
        finally
        {
            Object.DestroyImmediate(guestObject);
            Object.DestroyImmediate(testClip);
        }
    }

    private static void AssertWalkingFilters(Transform audioTransform)
    {
        Assert.That(audioTransform, Is.Not.Null);
        AudioHighPassFilter highPass = audioTransform.GetComponent<AudioHighPassFilter>();
        AudioLowPassFilter lowPass = audioTransform.GetComponent<AudioLowPassFilter>();
        Assert.That(highPass, Is.Not.Null);
        Assert.That(lowPass, Is.Not.Null);
        Assert.That(highPass.cutoffFrequency, Is.EqualTo(ExpectedWalkingHighPassCutoff).Within(0.1f));
        Assert.That(lowPass.cutoffFrequency, Is.LessThanOrEqualTo(9000f));
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
    }
}
