using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class NavigationRegressionTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string NavigationManagerPath = "Assets/Scripts/Navigation/RoomNavigationManager.cs";
    private const string NavigationBootstrapPath = "Assets/Scripts/Navigation/RoomNavigationBootstrap.cs";
    private const string DoorTriggerNavigationPath = "Assets/Scripts/Navigation/DoorTriggerNavigation.cs";
    private const string PointClickPlayerMovementPath = "Assets/Scripts/PointClickPlayerMovement.cs";
    private const string RoomContentGroupPath = "Assets/Scripts/Navigation/RoomContentGroup.cs";
    private const string DoorOpenSoundCatalogPath = "Assets/Resources/Audio/DoorOpenSoundCatalog.asset";
    private const string StairwaySoundCatalogPath = "Assets/Resources/Audio/StairwaySoundCatalog.asset";
    private const string ClockTickingAmbienceControllerPath = "Assets/Scripts/Audio/ClockTickingAmbienceController.cs";
    private const string FireplaceAmbienceControllerPath = "Assets/Scripts/Audio/FireplaceAmbienceController.cs";
    private const string PlayerFootstepAudioPath = "Assets/Scripts/Audio/PlayerFootstepAudio.cs";
    private const string GuestFootstepAudioPath = "Assets/Scripts/Audio/GuestFootstepAudio.cs";
    private const string GuestVoiceLinePlaybackPath = "Assets/Scripts/Audio/GuestVoiceLinePlayback.cs";
    private const string StaticNoisePlayerPath = "Assets/Scripts/StaticNoisePlayer.cs";
    private const string ClockTickingAmbienceCatalogScriptPath = "Assets/Scripts/Audio/ClockTickingAmbienceCatalog.cs";
    private const string ClockTickingAmbienceCatalogPath = "Assets/Resources/Audio/ClockTickingAmbienceCatalog.asset";
    private const string DoorbellSystemPath = "Assets/Scripts/Story/DoorbellSystem.cs";
    private const string DoorPromptSequenceControllerPath = "Assets/Scripts/Navigation/DoorPromptSequenceController.cs";
    private const string CameraManagerPath = "Assets/Map/CameraManager.cs";
    private const string NavigationEditorToolsPath = "Assets/Editor/NavigationEditorTools.cs";
    private const string BackgroundShaderGraphPath = "Assets/Shader/Background.shadergraph";
    private const string BackgroundMaterialPath = "Assets/Shader/BackgroundMaterial.mat";
    private const string RoomPrefabPath = "Assets/Prefabs/Room.prefab";
    private const string YSortSolidObstaclePath = "Assets/Scripts/Characters/YSortSolidObstacle2D.cs";
    private const string GameTimeHUDPath = "Assets/_Chateau/Runtime/UI/GameTimeHUD.cs";
    private const string RuntimeSettingsMenuPath = "Assets/Scripts/UI/RuntimeSettingsMenu.cs";
    private const string GameAudioSettingsPath = "Assets/Scripts/Audio/GameAudioSettings.cs";
    private const string NavigationCursorHoverTargetPath = "Assets/Scripts/UI/NavigationCursorHoverTarget.cs";
    private const string MainMenuControllerPath = "Assets/Scripts/MainMenuController.cs";
    private const string CursorStyleCatalogPath = "Assets/Scripts/UI/CursorStyleCatalog.cs";
    private const string CursorIconImportPostprocessorPath = "Assets/Editor/CursorIconImportPostprocessor.cs";
    private const string CursorSourceSheetPath = "Assets/Art/UI/Cursors/source/vintage_cursor_icon_concept_sheet.png";
    private const string CursorPreviewSheetPath = "Assets/Art/UI/Cursors/preview/cursor_styles_contact_sheet.png";
    private const string CursorResourceRoot = "Assets/Resources/UI/Cursors/styles";
    private const string CursorExtractionScriptPath = "scripts/extract_cursor_icons.py";
    private const string ChapterManagerPath = "Assets/Scripts/Story/ChapterManager.cs";
    private const string ActorRoomStatePath = "Assets/Scripts/Story/ActorRoomState.cs";
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string Chapter1CoatPickupPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1CoatPickup.cs";
    private const string Chapter1SceneActionPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs";
    private const string Chapter1InteractionHUDPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1InteractionHUD.cs";
    private const string Chapter2GuestFindActionPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestFindAction.cs";
    private const string Chapter2InteractionHUDPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2InteractionHUD.cs";
    private const string Chapter2ControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2Controller.cs";
    private const string Chapter2GuestPanicControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestPanicController.cs";
    private const string Chapter2MonsterStingerControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2MonsterStingerController.cs";
    private const string GameplayPlayModeGuardPath = "Assets/Editor/GameplayPlayModeGuard.cs";
    private const string ButlerIdleFolderPath = "Assets/Art/Characters/butler/butler_idle";
    private const string PlayerIdleClipPath = "Assets/Animation/Player/Player_Idle.anim";
    private const string ButlerClassicIdleClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle.anim";
    private const string RoomContentGroupGuid = "d0ea47fd950844bcacb0fd5556a9d880";

    [Test]
    public void DoorTriggersUseInspectorDestinationsOnly()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);

        Assert.That(triggerText, Does.Contain("MoveThroughInspectorDoor"), "Door triggers should navigate through their Inspector destination.");
        Assert.That(triggerText, Does.Not.Contain("TryMoveThroughDoor"), "Door hitboxes should not consult doors.txt.");
    }

    [Test]
    public void DoorTriggersPlayRandomWoodCreaksFromCatalog()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string catalogText = File.ReadAllText(DoorOpenSoundCatalogPath);

        Assert.That(triggerText, Does.Contain("DoorOpenSoundCatalog"), "Door triggers should use the shared randomized door sound catalog.");
        Assert.That(triggerText, Does.Contain("TryGetRandomClip"), "Door clicks should pick one door-open clip at random.");
        Assert.That(triggerText, Does.Contain("TryPlayNavigationSoundNow"), "Door sound should start immediately when the door is clicked, before navigation work finishes.");
        Assert.That(triggerText, Does.Contain("StopCurrentNavigationSound"), "Starting a door creak should stop any previous navigation sound.");
        Assert.That(triggerText, Does.Not.Contain("PlayDoorOpenSoundIfSuccessful"), "Door audio should not wait for the full navigation transition before starting.");
        Assert.That(catalogText, Does.Not.Contain("a599035f4d65f7614a7cb90bfb65c96d"), "The stair climb noise should not be part of door-open randomization.");
        Assert.That(catalogText, Does.Not.Contain("a7718dd1d7db61a4490bf5be4b919568"), "The pot clang should not be part of door-open randomization.");
        Assert.That(catalogText, Does.Not.Contain("2cda7eb569e05e4ae87de22b60ce4fcf"), "Wood tapping should not be part of door-open randomization.");
        Assert.That(catalogText, Does.Not.Contain("95d9163c9d40da015a0afa4a2e8cb915"), "Typo-spaced @hamzak woodcreak files should not be part of door-open randomization.");
        Assert.That(Regex.Matches(catalogText, "fileID: 8300000").Count, Is.EqualTo(7), "Door-open randomization should only use active @hamzak - woodcreak* clips.");

        string[] woodClipMetaPaths = Directory.GetFiles("Assets/Audio/Sound Exports", "@hamzak - woodcreak*.wav.meta");
        Assert.That(woodClipMetaPaths.Length, Is.EqualTo(7), "Flatline clips should stay outside the active door-open export folder.");
    }

    [Test]
    public void StairwayTriggersPlayStairwaySoundCatalog()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string catalogText = File.ReadAllText(StairwaySoundCatalogPath);

        Assert.That(triggerText, Does.Contain("DefaultStairwaySoundCatalogResourcePath"), "Stairways should load their own sound catalog.");
        Assert.That(triggerText, Does.Contain("ResolveStairwaySoundCatalog"), "Stairway triggers should resolve stairway audio separately from door audio.");
        Assert.That(triggerText, Does.Contain("lastStairwayClipIndex"), "Stairway randomization should not share door clip history.");
        Assert.That(catalogText, Does.Contain("a599035f4d65f7614a7cb90bfb65c96d"), "The stairway catalog should use @hamzak - stair_climb_noise.wav.");
        Assert.That(Regex.Matches(catalogText, "fileID: 8300000").Count, Is.EqualTo(1), "Only the active @hamzak - stair* clip should be in the stairway catalog.");

        string[] stairClipMetaPaths = Directory.GetFiles("Assets/Audio/Sound Exports", "@hamzak - stair*.wav.meta");
        Assert.That(stairClipMetaPaths.Length, Is.EqualTo(1), "Only exact-prefix @hamzak - stair* clips should be treated as stairway clips.");
    }

    [Test]
    public void ClockTickingAmbienceLoopsRandomTicksInClockRooms()
    {
        Assert.That(File.Exists(ClockTickingAmbienceControllerPath), Is.True, "Clock ambience needs a room-aware controller.");
        Assert.That(File.Exists(ClockTickingAmbienceCatalogScriptPath), Is.True, "Clock ambience needs a runtime catalog type.");
        Assert.That(File.Exists(ClockTickingAmbienceCatalogPath), Is.True, "Clock ambience should load from Resources.");

        string navigationText = File.ReadAllText(NavigationManagerPath);
        string controllerText = File.ReadAllText(ClockTickingAmbienceControllerPath);
        string catalogScriptText = File.ReadAllText(ClockTickingAmbienceCatalogScriptPath);
        string catalogText = File.ReadAllText(ClockTickingAmbienceCatalogPath);
        string sceneText = File.ReadAllText(GameplayScenePath);
        string[] roomNames =
        {
            "Grand Entrance",
            "Grand Entrance Hall",
            "Drawing Room",
            "Library",
            "Music Room",
            "Billiard Room",
            "Upper Gallery"
        };
        string[] clockClipPaths =
        {
            "Assets/Audio/clock-ticking/015_soft_library_clock_ticks_seed1320574_raw16.wav",
            "Assets/Audio/clock-ticking/014_close_gear_clockwork_ticks_seed1320533_raw16.wav",
            "Assets/Audio/clock-ticking/12_distant_hall_clock_ticks_tangoflux_seed1221164_48khz.wav",
            "Assets/Audio/clock-ticking/09_thin_plastic_clock_ticks_tangoflux_seed1220873_48khz.wav",
            "Assets/Audio/clock-ticking/007_quiet_bedroom_clock_ticks_seed1320246_raw16.wav",
            "Assets/Audio/clock-ticking/04_wristwatch_tiny_ticks_audioldm2_seed1320123_48khz.wav"
        };

        Assert.That(navigationText, Does.Contain("[SerializeField] private FireplaceAmbienceController fireplaceAmbienceController"), "Room navigation should own its serialized fireplace ambience controller.");
        Assert.That(navigationText, Does.Contain("[SerializeField] private ClockTickingAmbienceController clockTickingAmbienceController"), "Room navigation should own its serialized clock ambience controller.");
        Assert.That(navigationText, Does.Match(@"EnsureFireplaceAmbienceController\(\);[\s\S]*EnsureClockTickingAmbienceController\(\);"), "Room changes should refresh fireplace and clock ambience together.");
        Assert.That(sceneText, Does.Contain("catalog: {fileID: 11400000, guid: d1c5479f74b94514cdf7a37d49f95fbe, type: 2}"), "Gameplay should bind the clock catalog explicitly.");
        Assert.That(controllerText, Does.Not.Contain("Resources.Load"), "Clock ambience should not repair a missing serialized catalog at runtime.");
        Assert.That(controllerText, Does.Contain("audioSource.loop = true"), "Clock ticking should loop while the player remains in a clock room.");
        Assert.That(controllerText, Does.Contain("OnCurrentRoomChanged"), "Clock ticking should react to normal travel and debug teleports.");
        Assert.That(controllerText, Does.Contain("GameAudioChannel.Atmosphere"), "Clock ticking should respect the Atmosphere slider.");
        Assert.That(controllerText, Does.Contain("TryGetRandomClip(ref lastClipIndex"), "Every clock-room entry should choose a random ticking clip.");
        Assert.That(catalogScriptText, Does.Contain("NormalizeRoomName"), "Clock rooms should match authored names robustly.");

        for (int i = 0; i < roomNames.Length; i++)
        {
            Assert.That(catalogText, Does.Contain($"- {roomNames[i]}"), $"{roomNames[i]} should be clock-enabled.");
        }

        Assert.That(Regex.Matches(catalogText, "fileID: 8300000").Count, Is.EqualTo(clockClipPaths.Length), "The clock catalog should include every provided clock ticking clip.");

        for (int i = 0; i < clockClipPaths.Length; i++)
        {
            string clipPath = clockClipPaths[i];
            string clipGuid = ReadGuid(clipPath + ".meta");

            Assert.That(File.Exists(clipPath), Is.True, $"{clipPath} should exist.");
            Assert.That(catalogText, Does.Contain($"guid: {clipGuid}"), $"{clipPath} should be in the clock ticking random pool.");
        }
    }

    [Test]
    public void AudioPlaybackGuardsAgainstDisabledSources()
    {
        string gameAudioSettingsText = File.ReadAllText(GameAudioSettingsPath);
        string mainMenuText = File.ReadAllText(MainMenuControllerPath);
        string runtimeSettingsText = File.ReadAllText(RuntimeSettingsMenuPath);

        Assert.That(gameAudioSettingsText, Does.Contain("public static bool TryPlay(AudioSource source)"));
        Assert.That(gameAudioSettingsText, Does.Contain("public static bool TryPlayOneShot(AudioSource source, AudioClip clip"));
        Assert.That(gameAudioSettingsText, Does.Contain("source.gameObject.activeInHierarchy"), "Audio playback should not call Unity Play on inactive scene objects.");
        Assert.That(gameAudioSettingsText, Does.Contain("source.enabled = true"), "Safe playback should recover sources disabled by editor/session state.");
        Assert.That(gameAudioSettingsText, Does.Contain("source.mute = false"), "Safe playback should recover sources muted by editor/session state.");
        Assert.That(gameAudioSettingsText, Does.Contain("source.spatialBlend = 0f"), "House/game UI audio should not disappear through 3D distance attenuation.");
        Assert.That(gameAudioSettingsText, Does.Contain("clip.LoadAudioData()"), "Playback should not depend on imported clips being preloaded.");
        Assert.That(gameAudioSettingsText, Does.Contain("source.clip == null"), "TryPlay should not report success when an AudioSource has no assigned clip.");
        Assert.That(mainMenuText, Does.Contain("GameAudioSettings.TryPlay(menuSoundscapeSource)"), "Main menu audio should use the safe playback path.");
        Assert.That(runtimeSettingsText, Does.Contain("GameAudioSettings.TryPlay(musicSource)"), "Gameplay should explicitly start exploration music instead of only relying on Play On Awake.");
        string[] playbackScriptPaths =
        {
            MainMenuControllerPath,
            ClockTickingAmbienceControllerPath,
            FireplaceAmbienceControllerPath,
            PlayerFootstepAudioPath,
            GuestFootstepAudioPath,
            GuestVoiceLinePlaybackPath,
            StaticNoisePlayerPath,
            DoorbellSystemPath,
            DoorTriggerNavigationPath,
            Chapter2ControllerPath,
            Chapter2GuestPanicControllerPath,
            Chapter2MonsterStingerControllerPath
        };

        for (int i = 0; i < playbackScriptPaths.Length; i++)
        {
            string scriptText = File.ReadAllText(playbackScriptPaths[i]);
            Assert.That(
                Regex.Matches(scriptText, @"(?<!Try)\.Play(?:OneShot)?\s*\(").Count,
                Is.EqualTo(0),
                $"{playbackScriptPaths[i]} should route AudioSource playback through GameAudioSettings.TryPlay/TryPlayOneShot.");
        }
    }

    [Test]
    public void AudioSafePlaybackSkipsInactiveSourceWithoutUnityWarning()
    {
        GameObject audioObject = new GameObject("AudioSafePlaybackSmoke");

        try
        {
            AudioSource source = audioObject.AddComponent<AudioSource>();
            audioObject.SetActive(false);
            source.enabled = false;
            source.mute = true;
            AudioListener.pause = true;
            AudioListener.volume = 0f;

            Assert.That(GameAudioSettings.TryPlay(source), Is.False);
            Assert.That(AudioListener.pause, Is.False, "Safe playback should clear sticky listener pause even when the source is inactive.");
            Assert.That(AudioListener.volume, Is.EqualTo(1f), "Safe playback should clear sticky listener volume even when the source is inactive.");
            Assert.That(source.enabled, Is.True, "Safe playback should recover disabled source components.");
            Assert.That(source.mute, Is.False, "Safe playback should recover muted source components.");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(audioObject);
            GameAudioSettings.ResetUnityAudioState();
        }
    }

    [Test]
    public void GameplayDoorTriggerLayerCanReceiveClicks()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: Canvas_Background"));
        Assert.That(sceneText, Does.Contain("m_Name: EventSystem"));
        Assert.That(sceneText, Does.Contain("m_Name: Rooms"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: RoomDoorTriggers_Edit"));

        // A zero-scale UI transform is the exact regression that makes visible
        // door triggers stop receiving clicks, so keep it out of the gameplay scene.
        Assert.That(sceneText, Does.Not.Contain("m_LocalScale: {x: 0, y: 0, z: 0}"));
        Assert.That(sceneText, Does.Not.Contain("m_UiScaleMode: 0"), "Gameplay canvases should scale consistently between Edit and Play mode.");
        Assert.That(sceneText, Does.Not.Contain("m_ReferenceResolution: {x: 800, y: 600}"), "Gameplay canvases should use the project reference resolution, not Unity defaults.");
    }

    [Test]
    public void ChapterClockUsesSingleBottomLeftHudReadout()
    {
        string gameTimeHudText = File.ReadAllText(GameTimeHUDPath);
        string gameplaySceneText = File.ReadAllText(GameplayScenePath);
        string gameTimeTextRect = ExtractUnityObjectBlock(gameplaySceneText, "--- !u!224 &1878887131");
        string runtimeSettingsText = File.ReadAllText(RuntimeSettingsMenuPath);
        string gameAudioSettingsText = File.ReadAllText(GameAudioSettingsPath);
        string chapter1HudText = File.ReadAllText(Chapter1InteractionHUDPath);
        string chapter2HudText = File.ReadAllText(Chapter2InteractionHUDPath);

        Assert.That(gameTimeHudText, Does.Contain("clockText.text = chapterClock.CurrentTimeLabel"));
        Assert.That(gameTimeHudText, Does.Not.Contain("GameObject.Find"));
        Assert.That(gameTimeHudText, Does.Not.Contain("new GameObject"));
        Assert.That(gameTimeHudText, Does.Not.Contain("AddComponent<"));
        Assert.That(gameplaySceneText, Does.Contain("m_Name: Canvas_GameTimeHUD"));
        Assert.That(gameplaySceneText, Does.Contain("m_Name: Text_CurrentGameTime"));
        Assert.That(gameTimeTextRect, Does.Contain("m_AnchorMin: {x: 0, y: 0}"));
        Assert.That(gameTimeTextRect, Does.Contain("m_AnchorMax: {x: 0, y: 0}"));
        Assert.That(gameTimeTextRect, Does.Contain("m_AnchoredPosition: {x: 18, y: 18}"));
        Assert.That(gameTimeTextRect, Does.Contain("m_SizeDelta: {x: 220, y: 36}"));
        Assert.That(gameTimeTextRect, Does.Contain("m_Pivot: {x: 0, y: 0}"));
        Assert.That(gameTimeHudText, Does.Not.Contain("Slider_SecondsPerGameMinute"), "The editable game-time speed slider should not be created in the always-visible clock HUD.");
        Assert.That(gameTimeHudText, Does.Not.Contain("Input_SecondsPerGameMinute"), "The editable game-time speed input should not be created in the always-visible clock HUD.");
        Assert.That(runtimeSettingsText, Does.Contain("Control_DebugGameTimeSpeed"), "The game-time speed control should live in the runtime debug menu.");
        Assert.That(runtimeSettingsText, Does.Contain("Slider_DebugSecondsPerGameMinute"), "The debug game-time control should include the speed slider.");
        Assert.That(runtimeSettingsText, Does.Contain("Input_DebugSecondsPerGameMinute"), "The debug game-time control should include the numeric speed input.");
        Assert.That(runtimeSettingsText, Does.Contain("Control_DebugMusicVolume"), "The music volume control should live next to the debug time slider.");
        Assert.That(runtimeSettingsText, Does.Contain("Slider_DebugMusicVolume"), "The debug menu should include a custom music volume slider.");
        Assert.That(runtimeSettingsText, Does.Contain("Input_DebugMusicVolume"), "The debug music control should include a compact numeric value.");
        Assert.That(runtimeSettingsText, Does.Contain("Control_DebugFxVolume"), "The FX volume control should live next to the debug time slider.");
        Assert.That(runtimeSettingsText, Does.Contain("Slider_DebugFxVolume"), "The debug menu should include a custom FX volume slider.");
        Assert.That(runtimeSettingsText, Does.Contain("Input_DebugFxVolume"), "The debug FX control should include a compact numeric value.");
        Assert.That(runtimeSettingsText, Does.Contain("[SerializeField] private RoomNavigationManager navigationManager"), "The serialized settings owner should receive navigation explicitly.");
        Assert.That(runtimeSettingsText, Does.Not.Contain("RuntimeSettingsMenu FindOrCreate"), "Settings should not create or globally select a replacement root owner.");
        Assert.That(runtimeSettingsText, Does.Not.Contain("typeof(Slider)"), "The debug game-time control should not use Unity's image-backed Slider template.");
        Assert.That(runtimeSettingsText, Does.Contain("DebugSliderDragTarget"), "The debug sliders should use solid custom drag targets.");
        Assert.That(runtimeSettingsText, Does.Contain("ConfigureSolidImage"), "The debug game-time control should force solid UI images instead of inherited sprites.");
        Assert.That(runtimeSettingsText, Does.Contain("SetSecondsPerGameMinute"), "The moved control should still update the chapter clock.");
        Assert.That(runtimeSettingsText, Does.Contain("Audio_ExplorationMusic"), "The music slider should target the gameplay exploration music source.");
        Assert.That(runtimeSettingsText, Does.Contain("unity_dreadforge_soundscape"), "The music slider should identify the Dreadforge soundscape clip.");
        Assert.That(runtimeSettingsText, Does.Contain("ignoreListenerVolume = true"), "The soundscape source should ignore the FX/global listener volume.");
        Assert.That(gameAudioSettingsText, Does.Contain("source.ignoreListenerVolume = true"), "Channel sliders should control bound source volume instead of Unity's fragile global listener volume.");
        Assert.That(gameAudioSettingsText, Does.Contain("source.volume = Mathf.Max(0f, baseVolume) * GetVolume(channel)"), "Channel sliders should scale each bound source by its saved channel volume.");

        Assert.That(chapter1HudText, Does.Not.Contain("BuildShortHudState(chapterClock.CurrentTimeLabel)"), "Chapter 1 status should not render a second clock label.");
        Assert.That(chapter2HudText, Does.Not.Contain("chapterClock.CurrentTimeLabel"), "Chapter 2 status should not render a second clock label.");
        Assert.That(chapter2HudText, Does.Not.Contain("$\"{timeLabel}\\n{phaseLabel}\""), "Chapter 2 status should not combine time and phase in the top-left HUD.");
    }

    [Test]
    public void LegacyGrandfatherClockInteractionIsRetiredWithoutChangingCanonicalClockOwners()
    {
        const string retiredClockInteractionPath = "Assets/Scripts/Story/GrandfatherClockInteraction.cs";
        const string retiredClockInteractionMetaPath = "Assets/Scripts/Story/GrandfatherClockInteraction.cs.meta";
        const string legacyClockGuid = "c6da9f56f65d9988ff5f7da0f8e59fb0";
        string chapter1ArrivalText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string chapter1ActionText = File.ReadAllText(Chapter1SceneActionPath);
        string chapter1HudText = File.ReadAllText(Chapter1InteractionHUDPath);
        string clockAmbienceText = File.ReadAllText(ClockTickingAmbienceControllerPath);
        string gameplayText = File.ReadAllText(GameplayScenePath);
        string drawingRoomPrefabText = File.ReadAllText("Assets/Prefabs/Room_Drawing_Room.prefab");
        string drawingRoomPerspectivePrefabText = File.ReadAllText("Assets/Prefabs/Room_Drawing_Room_Perspective.prefab");

        Assert.That(File.Exists(retiredClockInteractionPath), Is.False);
        Assert.That(File.Exists(retiredClockInteractionMetaPath), Is.False);

        string[] runtimeScriptPaths = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);

        for (int i = 0; i < runtimeScriptPaths.Length; i++)
        {
            string normalizedPath = runtimeScriptPaths[i].Replace('\\', '/');

            if (normalizedPath.Contains("/Editor/"))
            {
                continue;
            }

            string runtimeText = File.ReadAllText(runtimeScriptPaths[i]);
            Assert.That(runtimeText, Does.Not.Contain("GrandfatherClockInteraction"), normalizedPath);
            Assert.That(runtimeText, Does.Not.Contain("RuntimeGrandfatherClockTicking"), normalizedPath);
            Assert.That(runtimeText, Does.Not.Contain("Canvas_GrandfatherClockCloseUp"), normalizedPath);
            Assert.That(runtimeText, Does.Not.Contain("Button_InspectClock"), normalizedPath);
            Assert.That(runtimeText, Does.Not.Contain("Chapter1SceneActionType.GrandfatherClock"), normalizedPath);
        }

        Assert.That(chapter1ArrivalText, Does.Not.Contain("grandfatherClock"));
        Assert.That(chapter1ArrivalText, Does.Not.Contain("FindGameObjectByNormalizedName"));
        Assert.That(chapter1ArrivalText, Does.Not.Contain("AddComponent<GrandfatherClockInteraction>"));
        Assert.That(chapter1ArrivalText, Does.Contain("interactionHUD.Initialize(this);"));
        Assert.That(chapter1ArrivalText, Does.Contain("frontDoorSceneAction.Initialize(Chapter1SceneActionType.FrontDoor, this);"));
        Assert.That(chapter1ActionText, Does.Match(@"(?s)public enum Chapter1SceneActionType\s*\{\s*FrontDoor,\s*CoatCloset\s*\}"));
        Assert.That(chapter1ActionText, Does.Not.Contain("clockInteraction"));
        Assert.That(chapter1ActionText, Does.Not.Contain("HoverIcon.Inspect"));
        Assert.That(chapter1HudText, Does.Contain("public void Initialize(Chapter1ArrivalController controller)"));
        Assert.That(chapter1HudText, Does.Not.Contain("clockInteraction"));
        Assert.That(chapter1HudText, Does.Not.Contain("chapterClock"));
        Assert.That(gameplayText, Does.Not.Contain("grandfatherClock:"));
        Assert.That(gameplayText, Does.Not.Contain("clockInteraction:"));
        Assert.That(Regex.Matches(gameplayText, @"(?m)^  actionType: 0$").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(gameplayText, @"(?m)^  actionType: 1$").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(gameplayText, @"(?m)^  actionType: 2$").Count, Is.Zero);
        Assert.That(gameplayText, Does.Not.Contain(legacyClockGuid));
        Assert.That(drawingRoomPrefabText, Does.Not.Contain(legacyClockGuid));
        Assert.That(drawingRoomPerspectivePrefabText, Does.Not.Contain(legacyClockGuid));
        Assert.That(Regex.Matches(gameplayText, @"(?m)^  m_Name: GrandfatherClock$").Count, Is.EqualTo(1), "The authored Entrance placeholder remains separate world data.");
        Assert.That(Regex.Matches(gameplayText, @"(?m)^  m_Name: GrandfatherClock_Optional$").Count, Is.EqualTo(1), "The authored Drawing Room placeholder remains separate world data.");
        Assert.That(drawingRoomPrefabText, Does.Contain("m_Name: GrandfatherClock_Optional"));
        Assert.That(drawingRoomPerspectivePrefabText, Does.Contain("m_Name: GrandfatherClock_Optional"));
        Assert.That(gameplayText, Does.Contain("clockTickingAmbienceController: {fileID: 2201000034}"));
        Assert.That(gameplayText, Does.Contain("catalog: {fileID: 11400000, guid: d1c5479f74b94514cdf7a37d49f95fbe, type: 2}"));
        Assert.That(gameplayText, Does.Contain("audioSource: {fileID: 2201000033}"));
        Assert.That(gameplayText, Does.Contain("clockStrikeAudioSource: {fileID: 3301000012}"));
        Assert.That(gameplayText, Does.Contain("clockStrikeVolumeBinding: {fileID: 3301000013}"));
        Assert.That(gameplayText, Does.Contain("clockStrikeClockFaceSprite: {fileID: 3571343685731824843, guid: 941ff15a69f3e194b9048e9b67b21e20, type: 3}"));
        Assert.That(clockAmbienceText, Does.Not.Contain("AudioClip.Create"), "The canonical ambience owner must use its imported catalog instead of the legacy synthetic tick.");
        Assert.That(chapter1ArrivalText, Does.Not.Contain("Chapter1_ClickTarget_GrandfatherClock"));
        Assert.That(chapter1ArrivalText, Does.Not.Contain("Chapter1_ClickTarget_CoatCloset"));
        Assert.That(chapter1ArrivalText, Does.Not.Contain("Chapter1_ClickTarget_DrawingRoomExit"));
        Assert.That(chapter1ArrivalText, Does.Not.Contain("CreateClickTarget"));
        Assert.That(chapter1ArrivalText, Does.Not.Contain("RemoveClickTarget"));
    }

    [Test]
    public void StairwayTriggersUseStairwayNamesAndCursor()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        string promptText = File.ReadAllText(DoorPromptSequenceControllerPath);

        Assert.That(Regex.Matches(sceneText, "m_Name: StairwayTrigger_").Count, Is.EqualTo(4), "Only the hand-placed hitboxes over visible stairs should be named StairwayTrigger.");
        Assert.That(sceneText, Does.Contain("m_Name: StairwayTrigger_GEH_UpperGalleryLeft"));
        Assert.That(sceneText, Does.Contain("m_Name: StairwayTrigger_GEH_UpperGalleryRight"));
        Assert.That(sceneText, Does.Contain("m_Name: StairwayTrigger_UpperGallery_GEH"));
        Assert.That(sceneText, Does.Contain("m_Name: StairwayTrigger_SideStairMudroom_UpperSittingHall"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: DoorTrigger_GEH_UpperGalleryLeft_entrance"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: DoorTrigger_GEH_UpperGalleryRight_entrance"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: DoorTrigger_UpperGallery_GEH"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: DoorTrigger_SideStairMudroom_UpperSittingHall"));
        Assert.That(sceneText, Does.Contain("doorName: GEH_Stairway_UpperGalleryLeft"));
        Assert.That(sceneText, Does.Contain("doorName: GEH_Stairway_UpperGalleryRight"));
        Assert.That(sceneText, Does.Contain("doorName: UpperGallery_Stairway_GEH"));
        Assert.That(sceneText, Does.Contain("doorName: SideStairMudroom_Stairway_UpperSittingHall"));

        Assert.That(triggerText, Does.Contain("InteractionLabel"), "Stairway triggers should identify themselves to shared UI without a separate hitbox class.");
        Assert.That(triggerText, Does.Contain("HoverIcon.Stairway"), "Stairway triggers should request the stairway cursor through the existing cursor controller.");
        Assert.That(cameraManagerText, Does.Contain("CreateStairwayCursor"), "The cursor controller should generate a stairway cursor icon.");
        Assert.That(promptText, Does.Contain("Use Stairway"), "Hover prompt text should match stairway interactions.");
    }

    [Test]
    public void DoorAndStairwayTriggersRequirePlayerApproach()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);

        Assert.That(triggerText, Does.Contain("requirePlayerProximity"), "Door and stairway triggers should check player distance before navigating.");
        Assert.That(triggerText, Does.Contain("walkPlayerToTriggerWhenFar"), "Far trigger clicks should walk the player toward the trigger instead of instantly navigating.");
        Assert.That(triggerText, Does.Contain("TryStartPlayerApproach"), "Door triggers should share one approach flow for doors and stairways.");
        Assert.That(triggerText, Does.Contain("GetClosestTriggerScreenPoint"), "Wide hitboxes should measure to the door threshold, not only the center.");
        Assert.That(triggerText, Does.Contain("GetClosestApproachPointInTriggerBounds"), "Door approaches should prefer the lower threshold line so the butler's feet reach the door.");
        Assert.That(triggerText, Does.Contain("TryFindBestApproachDestination"), "Door approaches should sample the trigger and choose the closest reachable floor point.");
        Assert.That(triggerText, Does.Contain("CollectTriggerApproachSamples"), "Door approaches should consider trigger edges and bottom points, not a single screen point.");
        Assert.That(triggerText, Does.Contain("ActivateDoor(eventData.position)"), "Pointer clicks should pass their exact screen position into broad door/stair trigger approach routing.");
        Assert.That(triggerText, Does.Contain("triggerUnderPointer.ActivateDoor(screenPosition)"), "Fallback trigger clicks should also preserve the clicked screen position.");
        Assert.That(triggerText, Does.Contain("preferredScreenPosition"), "Broad hitboxes should try the clicked point before scanning the whole trigger rectangle.");
        Assert.That(triggerText, Does.Match(@"GetPlayerScreenPosition\s*\([^)]*\)[\s\S]*TryGetScreenPointFromLogicalPosition\(playerMovement\.LogicalPosition"), "Door proximity should measure the butler's visible feet, not the visual transform origin.");
        Assert.That(triggerText, Does.Contain("MovementStopped"), "Pending door approaches should clean up whether the player arrives or gets blocked.");
        Assert.That(triggerText, Does.Contain("ResetStaticState"), "Door trigger static state should reset between Play Mode sessions, including when domain reload is disabled.");
        Assert.That(triggerText, Does.Contain("LogApproachFailure"), "Failed door approaches should leave a useful console reason.");
        Assert.That(triggerText, Does.Contain("UpdateFallbackPointerHoverAndClick"), "Door triggers need a RectTransform fallback when UI pointer enter/click events are blocked.");
        Assert.That(triggerText, Does.Contain("ContainsScreenPoint"), "The fallback should test the authored door hitbox rect directly.");
        Assert.That(triggerText, Does.Contain("activeTriggers"), "The fallback should scan only active room triggers.");
        Assert.That(triggerText, Does.Match(@"GameObject\.Find\(cleanPlayerObjectName\)[\s\S]*FindObjectsByType<PointClickPlayerMovement>"), "Door triggers should prefer the named Player before scanning fallback movement components from guest clones.");
        Assert.That(triggerText, Does.Contain("IsLikelyChapterGuest"), "Door triggers should not cache active Chapter 1 guests as the player.");
        Assert.That(playerText, Does.Contain("TrySetDestinationFromScreenPoint"), "Navigation triggers need a public way to ask the player to walk toward a screen-space hitbox.");
        Assert.That(playerText, Does.Contain("TryEvaluateMovementAtScreenPoint"), "Cursor feedback and door approaches should use the same movement reachability query.");
        Assert.That(playerText, Does.Contain("TryGetScreenPointFromLogicalPosition"), "Door approaches need to score clamped floor points in screen space.");
        Assert.That(playerText, Does.Contain("HasUsableCameraViewport(mainCamera)"), "Player screen/world conversion must wait for a valid camera viewport.");
        Assert.That(playerText, Does.Contain("IsPointerInsideScreenBounds(screenPosition)"), "Player pointer queries must reject coordinates outside the game viewport.");
        Assert.That(triggerText, Does.Contain("IsPointerOverActiveTrigger"), "Door triggers should expose active hitbox priority for floor input.");
        Assert.That(playerText, Does.Contain("DoorTriggerNavigation.IsPointerOverActiveTrigger"), "Door UI clicks should not be overwritten by the floor click handler on the same frame.");
        Assert.That(playerText, Does.Contain("IsPointerOverBlockingUi"), "Floor clicks should ignore passive room visuals instead of relying on broad EventSystem UI blocking.");
        Assert.That(playerText, Does.Contain("GetComponentInParent<RoomContentGroup>()"), "Room-authored visual UI should not make a whole room unclickable.");
        Assert.That(playerText, Does.Contain("WalkableInsetAttempts"), "Clamped approach targets should move just inside the walkable polygon instead of sitting exactly on the collider edge.");
    }

    [Test]
    public void BottomEdgeRoomTransitionsDoNotUseHugeFloorHitboxes()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(triggerText, Does.Contain("useBottomScreenEdgeInteraction"), "Special rear/edge room exits should be authored as screen-edge interactions.");
        Assert.That(triggerText, Does.Contain("IsBottomScreenEdgePoint"), "Bottom-edge exits should test the screen edge instead of the broad door RectTransform.");
        Assert.That(triggerText, Does.Contain("image.raycastTarget = ShouldUseGraphicRaycast()"), "Bottom-edge exits must disable graphic raycasts so their old rectangles do not steal floor clicks.");
        Assert.That(triggerText, Does.Match(@"useBottomScreenEdgeInteraction[\s\S]*return IsCurrentRoomSourceForScreenEdgeInteraction\(\)\s*&&[\s\S]*IsBottomScreenEdgePoint"), "The fallback hover/click path should route current-room bottom-edge exits through screen-edge testing before rectangle hit testing.");

        AssertBottomEdgeTransition(sceneText, "DoorTrigger_GEH_toRearView", "Grand Entrance Hall", "Grand Entrance Hall Rear View");
        AssertBottomEdgeTransition(sceneText, "DoorTrigger_GEH_Rear_GEH_Front", "Grand Entrance Hall Rear view", "Grand Entrance Hall");
        AssertBottomEdgeTransition(sceneText, "DoorTrigger_Conservatory_GEH_Rear_View", "Conservatory", "Grand Entrance Hall Rear View");
    }

    [Test]
    public void DoorTransitionsPlacePlayerAtDestinationDoor()
    {
        string navigationManagerText = File.ReadAllText(NavigationManagerPath);
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);

        Assert.That(navigationManagerText, Does.Contain("PlacePlayerAtDestinationDoor"), "Room transitions should move the player to the matching destination doorway.");
        Assert.That(navigationManagerText, Does.Contain("FindArrivalDoorTrigger"), "Destination placement should use the reverse trigger already authored in the room.");
        Assert.That(navigationManagerText, Does.Match(@"GameObject\.Find\(\""Player\""\)[\s\S]*FindObjectsByType<PointClickPlayerMovement>"), "Room transitions should prefer the named butler Player before scanning movement components.");
        Assert.That(navigationManagerText, Does.Contain("IsLikelyChapterGuest"), "Room transitions should not accidentally warp Chapter guest clones that carry PointClickPlayerMovement.");
        Assert.That(triggerText, Does.Contain("TryFindArrivalDestination"), "Door hitboxes should expose the same reachable floor sampling for arrivals.");
        Assert.That(triggerText, Does.Match(@"(?s)TryFindArrivalDestination\s*\([^)]*\)\s*\{.*TryFindBestApproachDestination\(playerMovement,\s*false,\s*out destination\).*return TryFindClosestReachableArrivalDestination\(playerMovement,\s*out destination\)"), "Door arrivals should prefer the doorway approach sampler before falling back to raw trigger proximity.");
        Assert.That(triggerText, Does.Contain("TryFindClosestReachableArrivalDestination"), "Door arrivals should fall back to the nearest reachable floor point around the destination trigger.");
        Assert.That(triggerText, Does.Contain("Vector3.Lerp(triggerWorldCorners[0], triggerWorldCorners[3]"), "Door arrival fallback should sample the threshold edge before the trigger center.");
        Assert.That(triggerText, Does.Contain("TryFindClosestReachableDestinationToWorldPointTowardRoomCenter"), "Door arrivals should bias placement toward the destination room interior, not the player's stale pre-transition position.");
        Assert.That(triggerText, Does.Contain("TryFindClosestReachableDestinationToWorldPoint"), "Door arrivals should reuse the player movement boundary search when trigger screen samples miss the floor.");
        Assert.That(playerText, Does.Contain("TryWarpTo"), "Navigation needs an explicit non-walking placement path after a room change.");
        Assert.That(playerText, Does.Contain("RefreshWalkableFloorForCurrentRoom"), "Door arrivals must refresh the active room boundary before evaluating placement.");
        Assert.That(playerText, Does.Contain("TryFindClosestReachableDestinationToWorldPointTowardRoomCenter"), "Player movement should offer a room-center-biased doorway placement path for arrivals.");
    }

    [Test]
    public void GrandEntranceDrawingRoomPassagePairIsCharacterizedBeforeCanonicalMigration()
    {
        const string doorTriggerGuid = "7e419b0f8f26d4f2d8d03e567fef4c52";
        const string doorButtonGuid = "526d59741832df7afadeab75a481cf82";

        string sceneText = File.ReadAllText(GameplayScenePath);
        string navigationManagerText = File.ReadAllText(NavigationManagerPath);
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string legacyDoorDataText = File.ReadAllText("Assets/Resources/Navigation/doors.txt");

        Assert.That(ReadGuid(DoorTriggerNavigationPath + ".meta"), Is.EqualTo(doorTriggerGuid));
        Assert.That(ReadGuid("Assets/Scripts/Navigation/DoorButton.cs.meta"), Is.EqualTo(doorButtonGuid));
        Assert.That(Regex.Matches(sceneText, $"guid: {doorButtonGuid}").Count, Is.Zero,
            "Gameplay must not contain a legacy DoorButton that makes doors.txt authoritative again.");

        string navigationOwner = ExtractUnityObjectBlock(sceneText, "--- !u!114 &1878886997");
        Assert.That(navigationOwner, Does.Contain("roomVisualCatalog: {fileID: 0}"));
        Assert.That(navigationOwner, Does.Contain("doorCameraSequence: {fileID: 0}"));
        Assert.That(navigationOwner, Does.Contain("cameraManager: {fileID: 0}"));
        Assert.That(navigationOwner, Does.Contain("doorButtonRoot: {fileID: 0}"));
        Assert.That(navigationOwner, Does.Contain("roomContentRoot: {fileID: 0}"));
        Assert.That(navigationOwner, Does.Contain("autoFindReferences: 1"));

        string entranceRoomObject = ExtractUnityObjectBlock(sceneText, "--- !u!1 &567115833");
        string entranceRoomTransform = ExtractUnityObjectBlock(sceneText, "--- !u!224 &567115834");
        string entranceRoomContent = ExtractUnityObjectBlock(sceneText, "--- !u!114 &2102000002");
        string entranceDoorsObject = ExtractUnityObjectBlock(sceneText, "--- !u!1 &2103000020");
        string entranceDoorsTransform = ExtractUnityObjectBlock(sceneText, "--- !u!224 &2103000021");
        string outboundObject = ExtractUnityObjectBlock(sceneText, "--- !u!1 &109889176");
        string outboundTransform = ExtractUnityObjectBlock(sceneText, "--- !u!224 &109889177");
        string outboundTrigger = ExtractUnityObjectBlock(sceneText, "--- !u!114 &109889178");
        string outboundImage = ExtractUnityObjectBlock(sceneText, "--- !u!114 &109889179");
        string outboundCanvasRenderer = ExtractUnityObjectBlock(sceneText, "--- !u!222 &109889180");

        Assert.That(entranceRoomObject, Does.Contain("m_Name: Room_Grand_Entrance_Hall"));
        Assert.That(entranceRoomObject, Does.Contain("m_IsActive: 0"));
        Assert.That(entranceRoomObject, Does.Contain("- component: {fileID: 567115834}"));
        Assert.That(entranceRoomObject, Does.Contain("- component: {fileID: 2102000002}"));
        Assert.That(entranceRoomTransform, Does.Contain("m_Father: {fileID: 668915133}"));
        Assert.That(entranceRoomTransform, Does.Contain("- {fileID: 2103000021}"));
        Assert.That(entranceRoomContent, Does.Contain($"m_Script: {{fileID: 11500000, guid: {RoomContentGroupGuid}, type: 3}}"));
        Assert.That(entranceRoomContent, Does.Contain("m_GameObject: {fileID: 567115833}"));
        Assert.That(entranceRoomContent, Does.Contain("roomName: Grand Entrance Hall"));
        Assert.That(entranceRoomContent, Does.Contain("roomBackgroundTexture: {fileID: 2800000, guid: 3e163816317a638f5adedc338ec34d98, type: 3}"));
        Assert.That(entranceDoorsObject, Does.Contain("m_Name: Doors"));
        Assert.That(entranceDoorsObject, Does.Contain("- component: {fileID: 2103000021}"));
        Assert.That(entranceDoorsTransform, Does.Contain("m_Father: {fileID: 567115834}"));
        Assert.That(entranceDoorsTransform, Does.Contain("- {fileID: 109889177}"));

        Assert.That(outboundObject, Does.Contain("- component: {fileID: 109889177}"));
        Assert.That(outboundObject, Does.Contain("- component: {fileID: 109889180}"));
        Assert.That(outboundObject, Does.Contain("- component: {fileID: 109889179}"));
        Assert.That(outboundObject, Does.Contain("- component: {fileID: 109889178}"));
        Assert.That(outboundObject, Does.Contain("- component: {fileID: 4100000011}"));
        Assert.That(outboundObject, Does.Contain("m_Name: DoorTrigger_GEH_DrawingRoom"));
        Assert.That(outboundObject, Does.Contain("m_Layer: 5"));
        Assert.That(outboundTransform, Does.Contain("m_GameObject: {fileID: 109889176}"));
        Assert.That(outboundTransform, Does.Contain("m_Father: {fileID: 2103000021}"));
        Assert.That(outboundTransform, Does.Contain("m_AnchoredPosition: {x: -687.8042, y: 18.2886}"));
        Assert.That(outboundTransform, Does.Contain("m_SizeDelta: {x: 211.9224, y: 341.6918}"));
        Assert.That(outboundTrigger, Does.Contain($"m_Script: {{fileID: 11500000, guid: {doorTriggerGuid}, type: 3}}"));
        Assert.That(outboundTrigger, Does.Contain("m_GameObject: {fileID: 109889176}"));
        Assert.That(outboundTrigger, Does.Contain("sourceRoom: Grand Entrance Hall"));
        Assert.That(outboundTrigger, Does.Contain("doorName: GEH_Drawing_Room"));
        Assert.That(outboundTrigger, Does.Contain("destinationRoom: Drawing Room"));
        Assert.That(outboundTrigger, Does.Contain("requirePlayerInSourceRoom: 1"));
        Assert.That(outboundTrigger, Does.Contain("useCameraSequence: 0"));
        Assert.That(outboundTrigger, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(outboundTrigger, Does.Contain("canonicalPassage: {fileID: 4100000011}"));
        Assert.That(outboundTrigger, Does.Contain("image: {fileID: 109889179}"));
        Assert.That(outboundTrigger, Does.Contain("doorOpenAudioSource: {fileID: 2201000013}"));
        Assert.That(outboundTrigger, Does.Contain("player: {fileID: 81962843}"));
        Assert.That(outboundTrigger, Does.Contain("doorOpenSoundCatalog: {fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}"));
        Assert.That(outboundTrigger, Does.Contain("stairwaySoundCatalog: {fileID: 0}"));
        Assert.That(outboundImage, Does.Contain("m_GameObject: {fileID: 109889176}"));
        Assert.That(outboundImage, Does.Contain("m_RaycastTarget: 1"));
        Assert.That(outboundCanvasRenderer, Does.Contain("m_GameObject: {fileID: 109889176}"));

        string drawingRoomObject = ExtractUnityObjectBlock(sceneText, "--- !u!1 &2300000005");
        string drawingRoomTransform = ExtractUnityObjectBlock(sceneText, "--- !u!224 &2300000006");
        string drawingRoomContent = ExtractUnityObjectBlock(sceneText, "--- !u!114 &2300000007");
        string drawingDoorsObject = ExtractUnityObjectBlock(sceneText, "--- !u!1 &2300000008");
        string drawingDoorsTransform = ExtractUnityObjectBlock(sceneText, "--- !u!224 &2300000009");
        string reverseObject = ExtractUnityObjectBlock(sceneText, "--- !u!1 &2300000100");
        string reverseTransform = ExtractUnityObjectBlock(sceneText, "--- !u!224 &2300000101");
        string reverseCanvasRenderer = ExtractUnityObjectBlock(sceneText, "--- !u!222 &2300000102");
        string reverseImage = ExtractUnityObjectBlock(sceneText, "--- !u!114 &2300000103");
        string reverseTrigger = ExtractUnityObjectBlock(sceneText, "--- !u!114 &2300000104");
        string playerTransform = ExtractUnityObjectBlock(sceneText, "--- !u!4 &81962843 stripped");

        Assert.That(drawingRoomObject, Does.Contain("m_Name: Room_Drawing_Room"));
        Assert.That(drawingRoomObject, Does.Contain("m_IsActive: 0"));
        Assert.That(drawingRoomObject, Does.Contain("- component: {fileID: 2300000006}"));
        Assert.That(drawingRoomObject, Does.Contain("- component: {fileID: 2300000007}"));
        Assert.That(drawingRoomTransform, Does.Contain("m_Father: {fileID: 668915133}"));
        Assert.That(drawingRoomTransform, Does.Contain("- {fileID: 2300000009}"));
        Assert.That(drawingRoomContent, Does.Contain($"m_Script: {{fileID: 11500000, guid: {RoomContentGroupGuid}, type: 3}}"));
        Assert.That(drawingRoomContent, Does.Contain("m_GameObject: {fileID: 2300000005}"));
        Assert.That(drawingRoomContent, Does.Contain("roomName: Drawing Room"));
        Assert.That(drawingRoomContent, Does.Contain("roomBackgroundTexture: {fileID: 2800000, guid: 28c74b6dea1ed8e2c9c7d612355f9734, type: 3}"));
        Assert.That(drawingDoorsObject, Does.Contain("m_Name: Doors"));
        Assert.That(drawingDoorsObject, Does.Contain("- component: {fileID: 2300000009}"));
        Assert.That(drawingDoorsTransform, Does.Contain("m_Father: {fileID: 2300000006}"));
        Assert.That(drawingDoorsTransform, Does.Contain("- {fileID: 2300000101}"));

        Assert.That(reverseObject, Does.Contain("- component: {fileID: 2300000101}"));
        Assert.That(reverseObject, Does.Contain("- component: {fileID: 2300000102}"));
        Assert.That(reverseObject, Does.Contain("- component: {fileID: 2300000103}"));
        Assert.That(reverseObject, Does.Contain("- component: {fileID: 2300000104}"));
        Assert.That(reverseObject, Does.Contain("- component: {fileID: 4100000012}"));
        Assert.That(reverseObject, Does.Contain("m_Name: DoorTrigger_DrawingRoom_GEH"));
        Assert.That(reverseObject, Does.Contain("m_Layer: 5"));
        Assert.That(reverseTransform, Does.Contain("m_GameObject: {fileID: 2300000100}"));
        Assert.That(reverseTransform, Does.Contain("m_Father: {fileID: 2300000009}"));
        Assert.That(reverseTransform, Does.Contain("m_AnchoredPosition: {x: 582.52795, y: 53.43762}"));
        Assert.That(reverseTransform, Does.Contain("m_SizeDelta: {x: 345.5079, y: 363.6107}"));
        Assert.That(reverseTrigger, Does.Contain($"m_Script: {{fileID: 11500000, guid: {doorTriggerGuid}, type: 3}}"));
        Assert.That(reverseTrigger, Does.Contain("m_GameObject: {fileID: 2300000100}"));
        Assert.That(reverseTrigger, Does.Contain("sourceRoom: Drawing Room"));
        Assert.That(reverseTrigger, Does.Contain("doorName: DrawingRoom_GEH"));
        Assert.That(reverseTrigger, Does.Contain("destinationRoom: Grand Entrance Hall"));
        Assert.That(reverseTrigger, Does.Contain("requirePlayerInSourceRoom: 1"));
        Assert.That(reverseTrigger, Does.Contain("useCameraSequence: 0"));
        Assert.That(reverseTrigger, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(reverseTrigger, Does.Contain("canonicalPassage: {fileID: 4100000012}"));
        Assert.That(reverseTrigger, Does.Contain("image: {fileID: 2300000103}"));
        Assert.That(reverseTrigger, Does.Contain("doorOpenAudioSource: {fileID: 2201000013}"));
        Assert.That(reverseTrigger, Does.Contain("player: {fileID: 81962843}"));
        Assert.That(reverseTrigger, Does.Contain("doorOpenSoundCatalog: {fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}"));
        Assert.That(reverseTrigger, Does.Contain("stairwaySoundCatalog: {fileID: 0}"));
        Assert.That(reverseImage, Does.Contain("m_GameObject: {fileID: 2300000100}"));
        Assert.That(reverseImage, Does.Contain("m_RaycastTarget: 1"));
        Assert.That(reverseCanvasRenderer, Does.Contain("m_GameObject: {fileID: 2300000100}"));
        Assert.That(playerTransform, Does.Contain("m_CorrespondingSourceObject: {fileID: 7967904164350347880, guid: 3c2a23f8d68b2d05cace0338fba9a1d1, type: 3}"));
        Assert.That(playerTransform, Does.Contain("m_PrefabInstance: {fileID: 81962841}"));
        Assert.That(playerTransform, Does.Contain("m_PrefabAsset: {fileID: 0}"));

        string legacyLoadBody = ExtractMethodBody(navigationManagerText, "private void LoadLegacyDoorDataIfNeeded");
        Assert.That(legacyLoadBody, Does.Contain("if (cachedDoorButtons.Length == 0)"));
        Assert.That(legacyLoadBody, Does.Contain("routesByDoorId = new Dictionary<string, DoorRoute>"));
        Assert.That(legacyLoadBody, Does.Contain("return;"));
        string canTraverseBody = ExtractMethodBody(navigationManagerText, "public bool CanTraverse");
        string tryTraverseBody = ExtractMethodBody(navigationManagerText, "public bool TryTraverse");
        string currentDefinitionBody = ExtractMethodBody(navigationManagerText, "private CanonicalRoomDefinition FindRegisteredRoomDefinition");
        Assert.That(Regex.Matches(tryTraverseBody, @"\bMoveThroughInspectorDoor\s*\(").Count, Is.EqualTo(1));
        Assert.That(tryTraverseBody, Does.Not.Contain("SetCurrentRoom"));
        Assert.That(tryTraverseBody, Does.Not.Contain("onCurrentRoomChanged"));
        Assert.That(tryTraverseBody, Does.Not.Contain("currentRoom ="));
        Assert.That(tryTraverseBody, Does.Not.Contain("TryWarpTo"));
        Assert.That(tryTraverseBody, Does.Not.Contain("Play"));
        string[] forbiddenFacadeDiscovery =
        {
            "FindAnyObjectByType",
            "FindFirstObjectByType",
            "FindObjectsByType",
            "GameObject.Find",
            "Resources.Load",
            "new GameObject",
            "AddComponent<"
        };
        for (int i = 0; i < forbiddenFacadeDiscovery.Length; i++)
        {
            Assert.That(canTraverseBody, Does.Not.Contain(forbiddenFacadeDiscovery[i]));
            Assert.That(tryTraverseBody, Does.Not.Contain(forbiddenFacadeDiscovery[i]));
            Assert.That(currentDefinitionBody, Does.Not.Contain(forbiddenFacadeDiscovery[i]));
        }
        string triggerActivateBody = ExtractMethodBody(triggerText, "private void ActivateDoor(bool allowPlayerApproach");
        Assert.That(Regex.Matches(triggerActivateBody, @"\bTryTraverse\s*\(").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(triggerActivateBody, @"\bMoveThroughInspectorDoor\s*\(").Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(triggerActivateBody, @"\bOpenDoorFromCurrentRoom\s*\(").Count, Is.EqualTo(1));
        Assert.That(triggerActivateBody.IndexOf("if (canonicalPassage != null)", System.StringComparison.Ordinal),
            Is.LessThan(triggerActivateBody.IndexOf("if (useCameraSequence)", System.StringComparison.Ordinal)));
        Assert.That(triggerActivateBody, Does.Contain("INavigationService navigationService = navigationManager;"));
        Assert.That(triggerActivateBody, Does.Contain("bool didNavigate = navigationService.TryTraverse(canonicalPassage);"));
        Assert.That(triggerActivateBody, Does.Contain("StopNavigationSoundIfNavigationFailed(soundStarted, didNavigate);"));
        Assert.That(triggerActivateBody, Does.Not.Contain("SetCurrentRoom"));
        Assert.That(triggerText, Does.Contain("MoveThroughInspectorDoor(SourceRoom, DoorName, DestinationRoom, requirePlayerInSourceRoom)"));
        Assert.That(triggerText, Does.Not.Contain("TryMoveThroughDoor"));
        Assert.That(triggerText, Does.Contain("[SerializeField] private CanonicalPassage canonicalPassage;"));
        Assert.That(triggerText, Does.Not.Contain("[SerializeField] private INavigationService"));
        Assert.That(triggerText, Does.Not.Contain("GetComponent<CanonicalPassage>"));
        Assert.That(triggerText, Does.Not.Contain("FindObjectsByType<CanonicalPassage>"));
        Assert.That(legacyDoorDataText, Does.Not.Contain("GEH_Drawing_Room:"),
            "The forward passage must remain provably independent of the incomplete legacy doors.txt graph.");
        Assert.That(legacyDoorDataText, Does.Contain("DrawingRoom_GEH: Grand Entrance Hall"),
            "The reverse legacy entry is only compatibility data; the serialized reverse passage still owns its destination.");
    }

    [Test]
    public void PlayerCursorShowsWalkability()
    {
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);

        Assert.That(playerText, Does.Contain("UpdateWalkCursor"), "The player movement script should continuously describe what a floor click would do.");
        Assert.That(playerText, Does.Contain("SetWalkHover"), "Valid and invalid floor clicks should drive the shared cursor controller.");
        Assert.That(playerText, Does.Contain("CanShowWalkCursor => HasReachableDestination && ExactPointWalkable"), "The walk cursor should only show for the exact floor point under the pointer.");
        Assert.That(playerText, Does.Contain("movementQuery.CanShowWalkCursor"), "Walk-hover updates should use the shared movement query verdict.");
        Assert.That(cameraManagerText, Does.Contain("CreateWalkCursor"), "The cursor controller should generate a walk cursor without needing imported art.");
        Assert.That(cameraManagerText, Does.Contain("Cursor_WalkBlocked"), "Invalid movement should show a distinct blocked-walk cursor.");
        Assert.That(cameraManagerText, Does.Contain("private const int CursorSize = 72"), "Movement cursors should be large enough to read quickly.");
        Assert.That(cameraManagerText, Does.Contain("ScaleCursorHotspot"), "Generated cursor art and hotspots should scale together.");
        Assert.That(cameraManagerText, Does.Contain("AddWatercolorTexture"), "Generated cursors should keep the game's painted texture language instead of flat monochrome glyphs.");
        Assert.That(cameraManagerText, Does.Contain("DrawBlockedSlash"), "Blocked cursor states should stay visually distinct from valid click actions.");
    }

    [Test]
    public void PlayerMovementKeepsHoverPathingCheapAndReusesClickPath()
    {
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);
        string updateWalkCursorBody = ExtractMethodBody(playerText, "private void UpdateWalkCursor");
        string tryGetFloorClickBody = ExtractMethodBody(playerText, "private bool TryGetFloorClick");
        string setDestinationFromQueryBody = ExtractMethodBody(playerText, "private void SetDestinationFromMovementQuery");

        Assert.That(updateWalkCursorBody, Does.Contain("TryEvaluateMovementAtScreenPoint(screenPosition, false, false"), "Cursor hover should only do cheap exact-point walkability, not full route construction every frame.");
        Assert.That(tryGetFloorClickBody, Does.Contain("TryEvaluateMovementAtScreenPoint(screenPosition, false, true"), "Actual clicks should still require a full reachable route.");
        Assert.That(playerText, Does.Contain("SetDestinationFromMovementQuery(movementQuery)"), "The click path should reuse the route already computed by the click evaluation.");
        Assert.That(setDestinationFromQueryBody, Does.Contain("movementQueryPath"), "The cached query path should become the active movement path instead of being discarded.");
        Assert.That(playerText, Does.Not.Contain("SetDestination(clickPosition);"), "Click handling should not rebuild the same path immediately after evaluating it.");
    }

    [Test]
    public void UiControlsUseDedicatedCursorDuringModalPause()
    {
        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        string hoverTargetText = File.ReadAllText(NavigationCursorHoverTargetPath);
        string runtimeSettingsText = File.ReadAllText(RuntimeSettingsMenuPath);
        string mainMenuText = File.ReadAllText(MainMenuControllerPath);

        Assert.That(cameraManagerText, Does.Contain("HoverIcon.Ui"), "The shared cursor controller should have a dedicated UI/action icon.");
        Assert.That(cameraManagerText, Does.Contain("CreateUiCursor"), "UI clicks should have their own generated cursor instead of borrowing the door cursor.");
        Assert.That(cameraManagerText, Does.Contain("gameplayHoverBlocked && icon != HoverIcon.Ui"), "Settings should block gameplay hover cursors without suppressing settings controls.");
        Assert.That(cameraManagerText, Does.Contain("doorHoverIcon != HoverIcon.Ui"), "Opening settings should clear stale gameplay cursors while preserving hovered UI controls.");
        Assert.That(hoverTargetText, Does.Contain("HoverIcon.Ui"), "Generic UI hover targets should default to the UI cursor.");
        Assert.That(runtimeSettingsText, Does.Contain("ConfigureUiCursor(buttonRect, button)"), "Runtime settings buttons should advertise UI clicks.");
        Assert.That(runtimeSettingsText, Does.Contain("ConfigureUiCursor(rect, input)"), "Runtime settings inputs should advertise UI focus clicks.");
        Assert.That(runtimeSettingsText, Does.Contain("ConfigureUiCursor(rect, null)"), "Runtime settings sliders should advertise UI drag/click regions.");
        Assert.That(mainMenuText, Does.Contain("ConfigureControlCursor(sliderRect, slider)"), "Main menu audio sliders should share the UI cursor contract.");
        Assert.That(mainMenuText, Does.Contain("NavigationCursorController.HoverIcon.Ui"), "Main menu buttons should use the UI cursor, not the door cursor.");
    }

    [Test]
    public void NewGameShowsCursorStyleChooserBeforeGameplay()
    {
        string mainMenuText = File.ReadAllText(MainMenuControllerPath);
        string mainMenuSceneText = File.ReadAllText(MainMenuScenePath);
        string newGameBody = ExtractMethodBody(mainMenuText, "public void NewGame");
        string continueBody = ExtractMethodBody(mainMenuText, "public void ContinueGame");
        string selectStyleBody = ExtractMethodBody(mainMenuText, "private void SelectCursorStyleAndStart");

        Assert.That(mainMenuSceneText, Does.Contain("m_MethodName: NewGame"), "The authored New Game button should still call MainMenuController.NewGame.");
        Assert.That(newGameBody, Does.Contain("ShowCursorStyleChooser"), "New Game should open the cursor chooser before gameplay starts.");
        Assert.That(newGameBody, Does.Not.Contain("LoadGameScene"), "New Game should not bypass the cursor chooser.");
        Assert.That(continueBody, Does.Contain("LoadGameScene(\"Continue\")"), "Continue should keep the existing direct continue flow.");
        Assert.That(selectStyleBody, Does.Contain("NavigationCursorController.SetCursorStyle(styleIndex)"), "Selecting a style should persist it through the cursor controller.");
        Assert.That(selectStyleBody, Does.Contain("LoadGameScene(\"New Game\")"), "After selection, the original New Game scene-load path should run.");
        Assert.That(mainMenuText, Does.Contain("CursorStyleCatalog.ChooserPreviewActions"), "Each style card should preview the gameplay action icons.");
        Assert.That(mainMenuText, Does.Contain("GridLayoutGroup.Constraint.FixedColumnCount"), "The chooser should show all ten styles at once in a fixed grid.");
    }

    [Test]
    public void CursorStyleCatalogProvidesGeneratedRuntimeAssets()
    {
        Assert.That(File.Exists(CursorStyleCatalogPath), Is.True, "Cursor styles need one central runtime catalog.");
        Assert.That(File.Exists(CursorIconImportPostprocessorPath), Is.True, "Cursor PNGs should import as Unity cursor textures.");
        Assert.That(File.Exists(CursorExtractionScriptPath), Is.True, "The sheet slicing script should stay in the repo.");
        Assert.That(File.Exists(CursorSourceSheetPath), Is.True, "The original approved cursor sheet should stay with the project.");
        Assert.That(File.Exists(CursorPreviewSheetPath), Is.True, "The extraction script should generate a contact-sheet preview.");

        string catalogText = File.ReadAllText(CursorStyleCatalogPath);
        string importText = File.ReadAllText(CursorIconImportPostprocessorPath);
        string scriptText = File.ReadAllText(CursorExtractionScriptPath);
        string[] actions =
        {
            "walk_move",
            "open_door",
            "exit_leave_room",
            "stairs_up",
            "stairs_down",
            "inspect_look",
            "talk_converse",
            "pick_up_take",
            "pick_up_coat",
            "place_hang_coat",
            "use_interact",
            "locked_cannot_use",
            "not_available_disabled"
        };

        Assert.That(catalogText, Does.Contain("PlayerPrefsKey = \"Dreadforge.CursorStyle\""), "Cursor style should persist with the project's PlayerPrefs naming pattern.");
        Assert.That(catalogText, Does.Contain("SanitizeStyleIndex"), "Invalid style indices should safely fall back to style 1.");
        Assert.That(catalogText, Does.Contain("CursorAction.UseInteract"), "Unknown cursor actions should fall back to use_interact.");
        Assert.That(catalogText, Does.Contain("Resources.Load<Texture2D>"), "Gameplay cursors should load the selected sliced PNGs from Resources.");
        Assert.That(importText, Does.Contain("TextureImporterType.Cursor"), "Generated runtime PNGs should import as cursor textures.");
        Assert.That(scriptText, Does.Contain("9: 10"), "The current source sheet is missing column 9; style_09 should be explicitly documented as a column 10 duplicate.");

        for (int styleIndex = 1; styleIndex <= 10; styleIndex++)
        {
            string styleFolder = Path.Combine(CursorResourceRoot, $"style_{styleIndex:00}");
            Assert.That(Directory.Exists(styleFolder), Is.True, $"Missing cursor style folder {styleFolder}.");

            for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
            {
                string iconPath = Path.Combine(styleFolder, actions[actionIndex] + ".png");
                Assert.That(File.Exists(iconPath), Is.True, $"Missing cursor icon {iconPath}.");
                Assert.That(new FileInfo(iconPath).Length, Is.GreaterThan(0), $"Cursor icon {iconPath} should not be empty.");
            }
        }

        Assert.That(Directory.GetFiles(CursorResourceRoot, "*.png", SearchOption.AllDirectories).Length, Is.EqualTo(130), "The sheet should produce 10 styles x 13 runtime action icons.");
    }

    [Test]
    public void GameplayInteractionsMapToCursorStyleActions()
    {
        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        string doorTriggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string coatPickupText = File.ReadAllText(Chapter1CoatPickupPath);
        string sceneActionText = File.ReadAllText(Chapter1SceneActionPath);
        string guestFindText = File.ReadAllText(Chapter2GuestFindActionPath);
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);

        Assert.That(cameraManagerText, Does.Contain("CursorStyleCatalog.LoadSelectedTexture"), "The existing cursor controller should consume selected style assets.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.WalkMove"), "Walkable floor hover should use walk_move.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.NotAvailableDisabled"), "Blocked floor hover should use not_available_disabled.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.OpenDoor"), "Door hover should use open_door.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.ExitLeaveRoom"), "Room-exit hover should use exit_leave_room.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.StairsUp"), "Upstairs hover should use stairs_up.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.StairsDown"), "Downstairs hover should use stairs_down.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.InspectLook"), "Inspectable hover should use inspect_look.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.TalkConverse"), "Guest/talk hover should use talk_converse.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.PickUpTake"), "Generic pickup hover should have a pick_up_take action.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.PickUpCoat"), "Coat pickup hover should use pick_up_coat.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.PlaceHangCoat"), "Coat placement hover should use place_hang_coat.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.UseInteract"), "UI/generic interaction hover should use use_interact.");
        Assert.That(cameraManagerText, Does.Contain("CursorAction.LockedCannotUse"), "Progression-blocked interactions should use locked_cannot_use.");

        Assert.That(doorTriggerText, Does.Contain("StairwayDirection"), "Stairway triggers need metadata for up/down cursor selection.");
        Assert.That(doorTriggerText, Does.Contain("HoverIcon.StairsUp"));
        Assert.That(doorTriggerText, Does.Contain("HoverIcon.StairsDown"));
        Assert.That(coatPickupText, Does.Contain("HoverIcon.PickUpCoat"));
        Assert.That(coatPickupText, Does.Contain("HoverIcon.Locked"));
        Assert.That(sceneActionText, Does.Contain("HoverIcon.PlaceHangCoat"));
        Assert.That(sceneActionText, Does.Not.Contain("Chapter1SceneActionType.DrawingRoomExit"));
        Assert.That(guestFindText, Does.Contain("HoverIcon.Talk"));
        Assert.That(playerText, Does.Contain("SetWalkHover"), "Floor hover should keep driving selected walk/blocked cursor actions.");
    }

    [Test]
    public void MainMenuLayoutScalesToShortGameViews()
    {
        string mainMenuText = File.ReadAllText(MainMenuControllerPath);

        Assert.That(mainMenuText, Does.Contain("menuSafeMargin"), "The main menu needs a safe margin when the Game view is shorter than the reference frame.");
        Assert.That(mainMenuText, Does.Contain("minResponsiveLayoutScale"), "Responsive menu scaling should have a floor so the art buttons remain readable.");
        Assert.That(mainMenuText, Does.Contain("GetResponsiveMenuLayoutScale"), "The menu should compute layout scale from the resolved Canvas size.");
        Assert.That(mainMenuText, Does.Contain("GetReferenceMenuLayoutExtents"), "The menu should fit the whole authored button stack, including the bottom Exit button.");
        Assert.That(mainMenuText, Does.Contain("HasMenuLayoutSizeChanged"), "The menu should repair its layout when the Game view size changes after Awake.");
        Assert.That(mainMenuText, Does.Contain("buttonSpacing * 3f)) * layoutScale"), "The Exit button position must be scaled with the rest of the stack.");
        Assert.That(mainMenuText, Does.Contain("ApplyResponsiveTitleFont"), "The title should shrink with the menu instead of overflowing a reduced title rect.");
    }

    [Test]
    public void PlayerMovementUsesOnlyFloorBoundaryForWalkability()
    {
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);
        string obstacleText = File.ReadAllText(YSortSolidObstaclePath);
        string applyPerspectiveScaleBody = ExtractMethodBody(playerText, "private void ApplyPerspectiveScale");
        string logicalToWorldBody = ExtractMethodBody(playerText, "private Vector2 LogicalToWalkableWorldPoint");
        string worldToLogicalBody = ExtractMethodBody(playerText, "private Vector2 WalkableWorldToLogicalPoint");

        Assert.That(playerText, Does.Contain("TryEvaluateMovementAtScreenPoint(screenPosition, false"), "Regular floor clicks should test the exact hovered floor point.");
        Assert.That(playerText, Does.Contain("LogicalToWalkableWorldPoint"), "Walkability should follow the visible room stage while edge panning moves it.");
        Assert.That(playerText, Does.Contain("TryGetActiveRoomStageWorldPoint"), "Player visuals should follow the active room stage's pan and zoom, not only its center offset.");
        Assert.That(playerText, Does.Contain("currentRoomStageScaleRatio"), "Player position should apply the room-stage layout ratio.");
        Assert.That(playerText, Does.Contain("currentWorldActorScaleMultiplier"), "Player presentation should have a separate deterministic scale multiplier.");
        Assert.That(playerText, Does.Contain("roomStageReferenceWorldCenter"), "Player logical movement should be mapped from a stable room-stage reference center.");
        Assert.That(playerText, Does.Contain("ResetRoomStageVisualReference"), "Changing rooms should reset the player's room-stage pan/zoom baseline.");
        Assert.That(playerText, Does.Contain("TryBuildReachableWorldPath"), "NPC panic routes should reuse the same floor-boundary route query as point-click movement.");
        Assert.That(playerText, Does.Contain("IsWalkableWorldSegment"), "Point-click movement should reject straight-line routes that leave the walkable floor between two valid points.");
        Assert.That(playerText, Does.Contain("TryBuildPolygonMovementPath"), "Concave room boundaries should route through walkable polygon corner nodes instead of crossing banisters or stairwell voids.");
        Assert.That(playerText, Does.Contain("pathPreviousNodeIndices"), "The polygon route should choose a shortest reachable corner path, not the first authored vertex order.");
        Assert.That(playerText, Does.Contain("TryBuildGridMovementPath"), "Hand-authored complex boundaries need a sampled fallback route when corner visibility cannot connect both sides.");
        Assert.That(playerText, Does.Contain("GridRouteMaxNodeCount"), "The grid fallback should stay bounded enough for cursor hover checks.");
        Assert.That(playerText, Does.Contain("SmoothGridRouteWorldPath"), "Fallback routes should be simplified before the Butler walks them.");
        Assert.That(playerText, Does.Match(@"(?s)if\s*\(clampToWalkableArea\)\s*\{.*hasReachableDestination\s*=\s*TryResolveClosestReachableWalkDestination"), "Normal floor clicks should not project out-of-bounds clicks unless a caller explicitly asks for clamping.");
        Assert.That(playerText, Does.Contain("TryResolveClosestReachableWalkDestination"), "Explicitly clamped movement queries should still resolve off-floor points through one nearest reachable destination query.");
        Assert.That(playerText, Does.Contain("CollectPolygonColliderBoundaryAnchors"), "Explicit click projection should inspect actual polygon edges instead of collider bounds.");
        Assert.That(playerText, Does.Contain("ClosestPointOnSegment"), "Concave floor clicks should measure to real polygon segments, not the AABB.");
        Assert.That(playerText, Does.Contain("walkDestinationCandidates.Sort(CompareWalkDestinationCandidates)"), "Projected click candidates must be sorted by distance to the requested click before route testing.");
        Assert.That(playerText, Does.Contain("CollectBlockerBoundaryDestinationCandidates"), "Clicks inside PlayerBlocker colliders should resolve to nearby blocker edges.");
        Assert.That(playerText, Does.Not.Contain("TryFindReachableDestinationNear"), "The old first-reachable ring fallback should not decide click destinations.");
        Assert.That(playerText, Does.Contain("GetClickProjectionMaxWorldDistance"), "Click projection should be bounded by the active room boundary size.");
        Assert.That(playerText, Does.Contain("CanShowWalkCursor"), "The cursor should show blocked state for out-of-bounds pointer positions.");
        Assert.That(playerText, Does.Contain("TryBuildMovementPathFromNearbyStart"), "Routes should recover when the Butler is standing on a thin authored boundary edge.");
        Assert.That(playerText, Does.Match(@"LogicalToWalkableWorldPoint\s*\([^)]*\)[\s\S]*referenceOffset \* currentRoomStageScaleRatio"), "Player logical-to-world mapping must include room-stage scale, not translation only.");
        Assert.That(applyPerspectiveScaleBody, Does.Contain("currentWorldActorScaleMultiplier"), "Player sprite scale should use the dedicated presentation policy.");
        Assert.That(applyPerspectiveScaleBody, Does.Not.Contain("currentRoomStageScaleRatio"), "Viewport layout must not rewrite the approved Butler size.");
        Assert.That(logicalToWorldBody, Does.Not.Contain("currentWorldActorScaleMultiplier"), "Presentation scale must not change logical-to-world navigation.");
        Assert.That(worldToLogicalBody, Does.Not.Contain("currentWorldActorScaleMultiplier"), "Presentation scale must not change world-to-logical navigation.");
        Assert.That(playerText, Does.Contain("GetCurrentVisibleMovementWorldPoint"), "Player logical movement should anchor to the visible feet rather than the transform origin.");
        Assert.That(playerText, Does.Contain("GetVisibleFeetOffsetY"), "Applying the player visual position should offset the transform so the feet land on the clicked point.");
        Assert.That(playerText, Does.Contain("visualPoint.y - feetOffsetY"), "The butler's visible feet, not his chest pivot, should end at the movement destination.");
        Assert.That(playerText, Does.Not.Contain("IsPickupObjectAtPoint"), "Pickup or prop colliders should not decide whether the floor is walkable.");
        Assert.That(playerText, Does.Not.Contain("IsMovementPointBlocked"), "Object footprints should not block point-click movement.");
        Assert.That(playerText, Does.Not.Contain("TryRestartPathFrom"), "Movement should not rebuild obstacle routes based on the butler's current position.");
        Assert.That(playerText, Does.Not.Contain("pathProbeStep"), "Movement should not sample a heavyweight path segment to reject floor clicks.");
        Assert.That(obstacleText, Does.Not.Contain("BlockPlayerMovement"), "Prop footprint components should not expose movement-blocking controls.");
        Assert.That(obstacleText, Does.Not.Contain("TryGetMovementBounds"), "Prop footprint components should not provide movement blockers.");
    }

    [Test]
    public void PlayerMovementResolvesOutsideRectangleToNearestInsetEdge()
    {
        GameObject floor = null;
        GameObject player = null;

        try
        {
            PointClickPlayerMovement movement = CreateConfiguredPointClickMovement(
                new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(4f, 0f),
                    new Vector2(4f, 4f),
                    new Vector2(0f, 4f)
                },
                new Vector2(2f, 2f),
                out floor,
                out player);

            bool resolved = movement.TryFindClosestReachableDestinationToWorldPoint(new Vector2(5f, 2f), out Vector2 destination);

            Assert.That(resolved, Is.True, "A click just outside a simple room should resolve to the nearest in-bounds edge.");
            Assert.That(destination.x, Is.InRange(3.85f, 4.01f), "The destination should stay on the clicked edge, not jump toward the room center.");
            Assert.That(destination.y, Is.InRange(1.9f, 2.1f), "The destination should preserve the clicked edge's y position.");
        }
        finally
        {
            DestroyImmediateIfNeeded(player);
            DestroyImmediateIfNeeded(floor);
        }
    }

    [Test]
    public void PlayerMovementResolvesConcaveGapToNearestPolygonEdge()
    {
        GameObject floor = null;
        GameObject player = null;

        try
        {
            PointClickPlayerMovement movement = CreateConfiguredPointClickMovement(
                new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(4f, 0f),
                    new Vector2(4f, 4f),
                    new Vector2(3f, 4f),
                    new Vector2(3f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 4f),
                    new Vector2(0f, 4f)
                },
                new Vector2(0.5f, 0.5f),
                out floor,
                out player);

            bool resolved = movement.TryFindClosestReachableDestinationToWorldPoint(new Vector2(2f, 1.2f), out Vector2 destination);

            Assert.That(resolved, Is.True, "A click inside a concave void should resolve to the nearest actual floor edge.");
            Assert.That(destination.x, Is.InRange(1.85f, 2.15f), "The resolver should choose the nearby bottom edge of the concavity.");
            Assert.That(destination.y, Is.InRange(0.85f, 1.01f), "The destination should be slightly inset inside the floor polygon.");
        }
        finally
        {
            DestroyImmediateIfNeeded(player);
            DestroyImmediateIfNeeded(floor);
        }
    }

    [Test]
    public void PlayerMovementResolvesBlockerClickToNearestWalkableBlockerEdge()
    {
        GameObject floor = null;
        GameObject player = null;
        GameObject blockerObject = null;

        try
        {
            PointClickPlayerMovement movement = CreateConfiguredPointClickMovement(
                new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(4f, 0f),
                    new Vector2(4f, 4f),
                    new Vector2(0f, 4f)
                },
                new Vector2(0.5f, 2f),
                out floor,
                out player);

            PolygonCollider2D blocker = CreatePolygonCollider(
                "PlayerBlocker_Test",
                new[]
                {
                    new Vector2(1.5f, 1.5f),
                    new Vector2(2.5f, 1.5f),
                    new Vector2(2.5f, 2.5f),
                    new Vector2(1.5f, 2.5f)
                },
                out blockerObject);

            AddWalkableBlocker(movement, blocker);

            bool resolved = movement.TryFindClosestReachableDestinationToWorldPoint(new Vector2(2f, 2f), out Vector2 destination);

            Assert.That(resolved, Is.True, "A click inside a PlayerBlocker should resolve to the nearest reachable point outside it.");
            Assert.That(destination.x, Is.InRange(1.35f, 1.5f), "The destination should hug the nearest blocker edge from the current side.");
            Assert.That(destination.y, Is.InRange(1.9f, 2.1f), "The destination should preserve the click's y position around the blocker.");
        }
        finally
        {
            DestroyImmediateIfNeeded(blockerObject);
            DestroyImmediateIfNeeded(player);
            DestroyImmediateIfNeeded(floor);
        }
    }

    [Test]
    public void UpperGalleryBoundaryKeepsRearHallwayRouteAroundStairwell()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string upperGalleryBoundary = ExtractUnityObjectBlock(sceneText, "--- !u!60 &580370978");
        string upperGalleryBlocker = ExtractUnityObjectBlock(sceneText, "--- !u!60 &580370981");
        int pointCount = Regex.Matches(upperGalleryBoundary, @"(?m)^      - \{|^    - - \{").Count;
        int pathCount = Regex.Matches(upperGalleryBoundary, @"(?m)^    - - \{").Count;
        int blockerPointCount = Regex.Matches(upperGalleryBlocker, @"(?m)^      - \{|^    - - \{").Count;
        GetColliderPathYExtents(upperGalleryBoundary, out float minY, out float maxY);
        GetColliderPathXExtents(upperGalleryBoundary, out float minX, out float maxX);
        GetColliderPathYExtents(upperGalleryBlocker, out float blockerMinY, out float blockerMaxY);
        GetColliderPathXExtents(upperGalleryBlocker, out float blockerMinX, out float blockerMaxX);

        Assert.That(upperGalleryBoundary, Does.Contain("m_GameObject: {fileID: 580370976}"), "This assertion should target the Upper Gallery PlayerBoundary.");
        Assert.That(sceneText, Does.Contain("m_Name: PlayerBlocker_UpperGallery_Stairwell"), "Upper Gallery should block the center stairwell with an explicit no-walk collider.");
        Assert.That(pathCount, Is.EqualTo(1), "Upper Gallery walkable floor should be one broad editable outer floor polygon.");
        Assert.That(pointCount, Is.InRange(10, 14), "Upper Gallery floor coverage should stay simple and broad, not a jagged route around the stairwell.");
        Assert.That(maxY, Is.GreaterThan(-0.25f), "The boundary should include the rear hallway lane behind the stair railing.");
        Assert.That(minY, Is.LessThan(-0.8f), "The boundary should keep the front walkway available below the stair railing.");
        Assert.That(minX, Is.LessThan(-2.2f), "The boundary should include the lower-left open floor corner.");
        Assert.That(maxX, Is.GreaterThan(2.2f), "The boundary should include the lower-right open floor corner.");
        Assert.That(blockerPointCount, Is.GreaterThanOrEqualTo(14), "The stairwell blocker should be a tight rounded polygon around the visible center hole.");
        Assert.That(blockerMaxY, Is.GreaterThan(0.12f), "The blocker should cover the rear curve of the open stairwell so arrivals cannot land inside the hole.");
        Assert.That(blockerMinY, Is.InRange(-0.5f, -0.35f), "The blocker should stop at the front lip of the opening without swallowing the front carpet.");
        Assert.That(blockerMinX, Is.GreaterThan(-1.25f), "The blocker should not cover the left potted-plant floor area.");
        Assert.That(blockerMaxX, Is.LessThan(1.1f), "The blocker should not cover the right potted-plant floor area.");
    }

    [Test]
    public void PlayerMovementCachesPolygonRouteVisibilityGraph()
    {
        string playerText = File.ReadAllText(PointClickPlayerMovementPath);
        string polygonRouteBody = ExtractMethodBody(playerText, "private bool TryBuildPolygonMovementPath");
        string graphBuildBody = ExtractMethodBody(playerText, "private void BuildPolygonRouteGraph");
        string cachedRouteBody = ExtractMethodBody(playerText, "private bool TryBuildCachedPolygonRoute");

        Assert.That(playerText, Does.Contain("polygonRouteLocalNodes"), "Polygon route nodes should be cached per walkable boundary instead of recreated for every hover query.");
        Assert.That(playerText, Does.Contain("polygonRouteConnections"), "Static polygon visibility edges should be cached once per boundary.");
        Assert.That(playerText, Does.Contain("InvalidatePolygonRouteGraph"), "Changing rooms must discard the cached boundary graph.");
        Assert.That(playerText, Does.Contain("roomBoundaryBlockerNamePrefix = \"PlayerBlocker\""), "Rooms should be able to mark no-walk holes separately from the editable floor boundary.");
        Assert.That(playerText, Does.Contain("RefreshWalkableBlockersForCurrentRoom"), "Changing rooms should collect the current room's no-walk hole colliders.");
        Assert.That(playerText, Does.Contain("IsWalkableWorldPoint"), "Every route query should use blocker-aware walkability.");
        Assert.That(polygonRouteBody, Does.Contain("EnsurePolygonRouteGraph(polygon)"), "Polygon routes should build or reuse a cached visibility graph.");
        Assert.That(polygonRouteBody, Does.Contain("TryBuildCachedPolygonRoute"), "Per-query route work should only connect the current start and target to the cached graph.");
        Assert.That(polygonRouteBody, Does.Not.Contain("TryFindShortestWalkableRoute"), "Movement hover should not rebuild all pairwise polygon visibility every frame.");
        Assert.That(graphBuildBody, Does.Contain("PolygonWorldPointToLocal"), "Cached polygon nodes should survive room-stage panning and zooming by storing collider-local points.");
        Assert.That(graphBuildBody, Does.Contain("CollectPolygonBlockerPathNodes"), "Route graphs should include no-walk hole vertices so paths can hug blocker edges.");
        Assert.That(graphBuildBody, Does.Contain("polygonRouteConnections.Add(new PolygonRouteConnection"), "The expensive static visibility checks should be done while building the cache.");
        Assert.That(playerText, Does.Not.Contain("localToWorldMatrix.GetHashCode()"), "Room-stage pan or zoom should not invalidate the route graph when the authored collider has not changed.");
        Assert.That(cachedRouteBody, Does.Contain("CollectDynamicPolygonRouteConnections"), "Each hover/click should only test the live start and target against cached nodes.");
        Assert.That(cachedRouteBody, Does.Not.Contain("CollectPolygonPathNodes"), "Per-query routing should not recreate polygon corner nodes.");
    }

    [Test]
    public void ActorRoomStateBindsWorldActorsToRoomStageLocalPoints()
    {
        string actorRoomStateText = File.ReadAllText(ActorRoomStatePath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        string chapter1ArrivalText = File.ReadAllText(Chapter1ArrivalControllerPath);

        Assert.That(actorRoomStateText, Does.Contain("followRoomStageMotion"), "Room-scoped actors should opt into following the active room stage pan.");
        Assert.That(actorRoomStateText, Does.Contain("LateUpdate"), "Actor room-state visuals should be corrected after CameraManager updates the room stage.");
        Assert.That(actorRoomStateText, Does.Contain("TryGetRoomStageScreenTransform"), "Actor room-state visuals should use the room stage's screen transform, not only world pan.");
        Assert.That(actorRoomStateText, Does.Contain("lastRoomStageScreenScale"), "Actor room-state visuals must apply room zoom scale as well as pan.");
        Assert.That(cameraManagerText, Does.Contain("TryGetActiveRoomStageWorldPoint"), "CameraManager should expose active room-stage local point conversion for world actors.");
        Assert.That(cameraManagerText, Does.Contain("activeRoomStage.TransformPoint"), "The conversion should start from active room-stage local space.");
        Assert.That(cameraManagerText, Does.Contain("RectTransformUtility.WorldToScreenPoint"), "The conversion should go through screen space so Canvas resize and camera setup are respected.");
        Assert.That(cameraManagerText, Does.Contain("mainCamera.ScreenToWorldPoint"), "The conversion should end at the actor's world depth.");
        Assert.That(cameraManagerText, Does.Contain("HasUsableCameraViewport(mainCamera)"), "Room-stage conversions must wait for a valid camera viewport before using screen coordinates.");
        Assert.That(actorRoomStateText, Does.Contain("hasRoomStageLocalBinding"), "World actors need an explicit room-stage local binding instead of inferred screen drift.");
        Assert.That(actorRoomStateText, Does.Contain("roomStageLocalPoint"), "ActorRoomState should store the room-stage local coordinate it is locked to.");
        Assert.That(actorRoomStateText, Does.Contain("boundWorldZ"), "ActorRoomState should preserve the actor's world depth while following the room stage.");
        Assert.That(actorRoomStateText, Does.Contain("boundLocalScale"), "ActorRoomState should preserve the actor's baseline scale when binding to a room-stage point.");
        Assert.That(actorRoomStateText, Does.Contain("boundRoomStageScale"), "ActorRoomState should scale bound world actors by the room-stage zoom ratio.");
        Assert.That(actorRoomStateText, Does.Contain("BindToRoomStagePoint"), "Chapter systems need a public API to bind world actors to room-stage targets.");
        Assert.That(actorRoomStateText, Does.Contain("TryApplyRoomStageLocalBindingIfNeeded"), "Bound actors should use the local binding path before legacy delta fallback.");
        Assert.That(actorRoomStateText, Does.Contain("ScaleXY"), "World actors should visually scale with the room stage instead of shrinking against the painted room.");
        Assert.That(actorRoomStateText, Does.Match(@"hasRoomStageLocalBinding[\s\S]*TryApplyRoomStageLocalBindingIfNeeded\(\)[\s\S]*return;"), "Bound actors must not fall through to the translation/scale delta fallback.");
        Assert.That(actorRoomStateText, Does.Contain("GetComponentInParent<RoomContentGroup>(true)"), "Actors already under a RoomContentGroup must not receive duplicate world-space following.");
        Assert.That(chapter1ArrivalText, Does.Contain("BindGuestToRoomStagePoint(guestState, target)"), "Chapter 1 placement should bind world-space guests to their room-stage target.");
        Assert.That(chapter1ArrivalText, Does.Contain("guestState.ActorState.BindToRoomStagePoint(target)"), "Chapter 1 should use the ActorRoomState binding API, not guest-specific animation exceptions.");
        Assert.That(chapter1ArrivalText, Does.Contain("ClearGuestRoomStagePointBinding"), "Raw world placements should clear stale room-stage bindings.");
        Assert.That(actorRoomStateText, Does.Not.Contain("targetTransform.position +="), "Bound actors should recompute from room-stage local points, not accumulate position deltas.");
        Assert.That(actorRoomStateText, Does.Not.Contain("SetParent("), "Guests should not be fixed by reparenting them under room presentation roots.");
    }

    [Test]
    public void RoomPropDepthDoesNotDependOnViewportBounds()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string roomContentGroupText = File.ReadAllText(RoomContentGroupPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);

        Assert.That(roomContentGroupText, Does.Contain("flattenChildRendererDepthAtRuntime"), "Room renderers should be flattened at runtime instead of relying on deep authored Z values.");
        Assert.That(roomContentGroupText, Does.Contain("NormalizeRendererDepth"), "Room renderer depth normalization should be centralized on RoomContentGroup.");
        Assert.That(roomContentGroupText, Does.Not.Contain("ApplyDynamicPropSorting"), "Room prop sorting should stay authored, not recomputed from viewport-dependent renderer bounds.");
        Assert.That(roomContentGroupText, Does.Not.Contain("spriteRenderer.bounds.min.y"), "Room prop sorting must not use world bounds that change with Free Aspect scaling.");
        Assert.That(sceneText, Does.Contain("far clip plane: 1000"), "The camera should use a normal clip range; deep prop Z is fixed at the room renderer level.");
        Assert.That(sceneText, Does.Not.Contain("minimumRoomRenderFarClipPlane"), "Room rendering should not depend on a giant far-clip workaround.");
        Assert.That(cameraManagerText, Does.Not.Contain("EnsureRoomRenderCameraClipRange"), "CameraManager should not hide room prop depth bugs by stretching the far clip plane.");
    }

    [Test]
    public void ServiceCorridorKeepsAuthoredProps()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(File.Exists("Assets/Art/Objects/service_corridor_left_table.png"), Is.True, "The service corridor left table sprite asset should stay in the project.");
        Assert.That(File.Exists("Assets/Art/Objects/service_corridor_right_desk.png"), Is.True, "The service corridor right desk sprite asset should stay in the project.");
        AssertScenePropSorting(sceneText, "service_corridor_left_table_0", 1480);
        AssertScenePropSorting(sceneText, "service_corridor_right_desk_0", 1512);
    }

    [Test]
    public void BilliardRoomKeepsForegroundCutouts()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(File.Exists("Assets/Art/Objects/green_armchair.png"), Is.True, "The billiard left armchair cutout should stay in the project.");
        Assert.That(File.Exists("Assets/Art/Objects/round_lamp_table.png"), Is.True, "The billiard left lamp table cutout should stay in the project.");
        AssertScenePropSorting(sceneText, "billiard_left_armchair", 1685);
        AssertScenePropSprite(sceneText, "billiard_left_armchair", "8017e9546fbe40beaaa61662f1e8191a");
        AssertScenePropSorting(sceneText, "billiard_left_lamp_table", 1695);
        AssertScenePropSprite(sceneText, "billiard_left_lamp_table", "dfcaa33b0d194eb0a42a334d83b90fb8");
    }

    [Test]
    public void DrawingRoomDoesNotKeepStaleGreenChairReference()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Not.Contain("353851a98d8c825dead3a8bdc3654973"), "The deleted drawingroomchair3 sprite should not remain in the scene.");
        Assert.That(sceneText, Does.Not.Contain("m_Name: green_armchair"), "The stale duplicate Drawing Room chair object should stay removed.");
        AssertScenePropSorting(sceneText, "drawingroomgreenchair_0", 1614);
        AssertScenePropSprite(sceneText, "drawingroomgreenchair_0", "5d2f4c79f6c75e4c69dbbafeaff8b2c2");
    }

    [Test]
    public void ImportantRoomPropsKeepStableAuthoredSorting()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        AssertScenePropSorting(sceneText, "nursery_chair_0", 1517);
        AssertScenePropSorting(sceneText, "nursery_chest_0", 1557);
        AssertScenePropSorting(sceneText, "nursery_table_0", 1524);
        AssertScenePropSorting(sceneText, "dog_toy_nursery_0", 1605);
        AssertScenePropSorting(sceneText, "Grand_entrance_railing_left_0", 1601);
        AssertScenePropSorting(sceneText, "grand_entrance_railing_right_0", 1616);
        AssertScenePropSorting(sceneText, "upper_gallery_railing", 1300);
        AssertScenePropSorting(sceneText, "upper_gallery_left_plant_0", 1500);
        AssertScenePropSorting(sceneText, "upper_gallery_right_plant_0", 2000);
    }

    [Test]
    public void GameplayDoesNotKeepMapDropdownUi()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Not.Match(@"(?m)^\s*m_Name: Map$"), "Gameplay should not keep the old map dropdown panel.");
        Assert.That(sceneText, Does.Not.Match(@"(?m)^\s*m_Name: MapTrigger$"), "Gameplay should not keep the old map dropdown opener.");
        Assert.That(sceneText, Does.Not.Contain("MapAnimator"), "Gameplay should not reference the old map dropdown animator.");
        Assert.That(sceneText, Does.Not.Contain("CameraAreaController"), "Gameplay should not reference old map camera-area buttons.");
        Assert.That(File.Exists("Assets/Map/MapAnimator.cs"), Is.False, "The old map dropdown animator script should stay deleted.");
        Assert.That(File.Exists("Assets/Map/CameraAreaController.cs"), Is.False, "The old map camera-area button script should stay deleted.");
        Assert.That(File.Exists("Assets/Art/UI/map_labeled_transparent.png"), Is.False, "The old map dropdown art should stay deleted.");
    }

    [Test]
    public void RuntimeSettingsOwnsChapter2DebugSkipButtonAndManagerUsesSerializedController()
    {
        string chapterManagerText = File.ReadAllText(ChapterManagerPath);
        string runtimeSettingsText = File.ReadAllText(RuntimeSettingsMenuPath);

        Assert.That(chapterManagerText, Does.Contain("SkipToChapter2ForTesting"));
        Assert.That(chapterManagerText, Does.Not.Contain("ResolveChapter2Controller"));
        Assert.That(chapterManagerText, Does.Not.Contain("Canvas_ChapterDebug"));
        Assert.That(chapterManagerText, Does.Not.Contain("Button_SkipToChapter2"));
        Assert.That(chapterManagerText, Does.Not.Contain("AddComponent<Chapter2Controller>"));
        Assert.That(runtimeSettingsText, Does.Contain("FindOrCreateButton(debugButtonRow, \"Button_SkipToChapter2\", \"Skip to Chapter 2\", SkipToChapter2)"));
        Assert.That(chapterManagerText, Does.Match(@"PrepareGuestsForChapter2Skip\s*\(\s*\)[\s\S]*BeginChapter2\(this\)"), "Skipping to Chapter 2 should stage Chapter 1 guests before Chapter 2 fades into the Drawing Room.");
        Assert.That(chapterManagerText, Does.Match(@"if \(chapter2Controller != null\)[\s\S]*DebugResetForChapter2Skip\(this\)[\s\S]*BeginChapter2\(this\)"), "Debug skip should reset the serialized Chapter 2 controller before replaying Chapter 2.");
        Assert.That(chapterManagerText, Does.Match(@"BeginChapter2\(this\)[\s\S]*RefreshChapter2SkipGuestVisibilityAfterRoomChange\s*\(\s*\)"), "Skipping to Chapter 2 should recheck staged guest visibility after Chapter 2 moves to the Drawing Room.");
        Assert.That(chapterManagerText, Does.Contain("BeginChapter2(this)"));
    }

    [Test]
    public void SettingsDebugChapterSkipsClosePausedModalBeforeRunning()
    {
        string runtimeSettingsText = File.ReadAllText(RuntimeSettingsMenuPath);
        string skipChapter2Body = ExtractMethodBody(runtimeSettingsText, "private void SkipToChapter2");
        string skipChapter3Body = ExtractMethodBody(runtimeSettingsText, "private void SkipToChapter3");
        string skipSevenBody = ExtractMethodBody(runtimeSettingsText, "private void SkipToSevenPM");
        string teleportBody = ExtractMethodBody(runtimeSettingsText, "private void TeleportToRoom");
        string closeBody = ExtractMethodBody(runtimeSettingsText, "CloseSettingsForGameplayCommand");

        Assert.That(skipChapter2Body, Does.Match(@"CloseSettingsForGameplayCommand\(\)[\s\S]*manager\.SkipToChapter2ForTesting\(\)"), "Chapter 2 debug skip should unpause/close settings before starting scaled-time title fades.");
        Assert.That(skipChapter3Body, Does.Match(@"CloseSettingsForGameplayCommand\(\)[\s\S]*manager\.SkipToChapter3ForTesting\(\)"), "Chapter 3 debug skip should also leave settings modal state before running gameplay commands.");
        Assert.That(skipSevenBody, Does.Match(@"CloseSettingsForGameplayCommand\(\)[\s\S]*manager\.SkipToSevenPMForTesting\(\)"), "Seven-PM debug skip should also leave settings modal state before running gameplay commands.");
        Assert.That(runtimeSettingsText, Does.Not.Contain("GuestVoiceLinePlayback.StopAnyCurrentLine"));
        Assert.That(teleportBody, Does.Match(@"StopActiveDialogueForDebugTransition\(\)[\s\S]*DebugTeleportToRoom\(roomName\)"), "Room teleport should clear dialogue through ChapterManager before changing rooms.");
        Assert.That(closeBody, Does.Contain("settingsOpen = false"), "Closing for a gameplay command should clear the modal-open state.");
        Assert.That(closeBody, Does.Contain("debugOpen = false"), "Closing for a gameplay command should clear nested debug UI state.");
        Assert.That(closeBody, Does.Contain("roomListOpen = false"), "Closing for a gameplay command should clear nested room-list UI state.");
        Assert.That(closeBody, Does.Contain("RefreshOpenState()"), "Closing for a gameplay command should restore Time.timeScale through the normal modal-state path.");
    }

    [Test]
    public void GameplayHasManualRoomStageRoot()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: Rooms"));
        Assert.That(sceneText, Does.Contain("m_Name: Room_Grand_Entrance_Hall"));
        Assert.That(sceneText, Does.Contain("m_Name: Doors"));
    }

    [Test]
    public void GameplayRoomsOwnBackgroundsAndDoorGroups()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string navigationManagerText = File.ReadAllText(NavigationManagerPath);
        string roomPrefabText = File.ReadAllText(RoomPrefabPath);

        Assert.That(sceneText, Does.Contain($"guid: {RoomContentGroupGuid}"), "Gameplay room objects should have RoomContentGroup components.");
        Assert.That(sceneText, Does.Contain("roomBackgroundTexture: {fileID: 2800000"), "RoomContentGroup should own each room background texture.");
        Assert.That(sceneText, Does.Not.Contain("m_Name: Button_Grand_Entrance_Hall"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: Button_Library"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: Button_Ballroom"));
        Assert.That(Regex.Matches(sceneText, @"m_Name: Doors").Count, Is.GreaterThanOrEqualTo(18), "Each room object should have a Doors child.");
        Assert.That(sceneText, Does.Not.Contain("m_Name: Cam_"));
        Assert.That(sceneText, Does.Not.Contain("m_Name: MapButton_"));
        Assert.That(sceneText, Does.Not.Contain("DoorTrigger_K1"));
        Assert.That(sceneText, Does.Not.Contain("DoorTrigger_K2"));
        Assert.That(sceneText, Does.Not.Contain("006acc238c9c2e26f8e9e7ec33e82a09"));
        Assert.That(sceneText, Does.Not.Contain("a8335d5d820eabc44a82824f60fc64c6"));
        Assert.That(navigationManagerText, Does.Contain("NormalizeComparableName"), "Clean hierarchy names still need to match display room names with apostrophes or ampersands.");

        Assert.That(roomPrefabText, Does.Contain("m_Name: Room_NewRoom"));
        Assert.That(roomPrefabText, Does.Contain("m_Name: Doors"));
        Assert.That(roomPrefabText, Does.Contain($"guid: {RoomContentGroupGuid}"));
        Assert.That(roomPrefabText, Does.Contain("roomBackgroundTexture: {fileID: 0}"));
    }

    [Test]
    public void GameplaySerializedRootOwnsNavigationStartup()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(File.Exists(NavigationBootstrapPath), Is.False, "The runtime navigation repair bootstrap must stay deleted.");
        Assert.That(Regex.Matches(sceneText, "guid: 88f4088eff8696ab181615a79b7e114c").Count, Is.EqualTo(1), "Gameplay must serialize exactly one navigation service.");
        Assert.That(Regex.Matches(sceneText, "guid: 3f9bb60e65b04160aa752c2b3fcfdb4d").Count, Is.EqualTo(1), "Gameplay must serialize exactly one door-prompt service.");
        Assert.That(Regex.Matches(sceneText, "guid: bc887e2e5e4f5cc594cd3d8920eb9f90").Count, Is.EqualTo(1), "Gameplay must serialize exactly one GameRoot.");
    }

    [Test]
    public void GameplaySceneCannotBeStartedDirectlyFromEditorPlayMode()
    {
        string guardText = File.ReadAllText(GameplayPlayModeGuardPath);

        Assert.That(guardText, Does.Contain("PlayModeStateChange.ExitingEditMode"), "The guard should run before Unity finishes entering Play Mode.");
        Assert.That(guardText, Does.Contain("EditorApplication.isPlaying = false"), "The guard should cancel Play Mode instead of allowing partial gameplay startup.");
        Assert.That(guardText, Does.Contain(GameplayScenePath), "The guard should explicitly block Gameplay.unity.");
        Assert.That(guardText, Does.Contain(MainMenuScenePath), "The guard should point developers back to MainMenu.unity.");
    }

    [Test]
    public void GameplayCameraUsesTutorialStyleRoomLook()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        string shaderGraphText = File.ReadAllText(BackgroundShaderGraphPath);
        string backgroundMaterialText = File.ReadAllText(BackgroundMaterialPath);

        Assert.That(sceneText, Does.Contain("edgePanActivationPixels: 24"), "Gameplay should use a tiny pixel edge zone, not broad screen regions.");
        Assert.That(sceneText, Does.Contain("zoomRoomWithMouseWheel: 1"), "Mouse wheel should use a regular image zoom, not the old vertical shader distortion.");
        Assert.That(sceneText, Does.Contain("defaultRoomFov: 0.8"), "Room art should start less cropped than the old tutorial placeholder framing.");
        Assert.That(sceneText, Does.Contain("fitBackgroundToRoomAspect: 1"), "Room art should keep a stable aspect plane when the Game view is resized.");
        Assert.That(sceneText, Does.Contain("cropBackgroundToFill: 0"), "The legacy RawImage crop toggle can stay off because active room stages cover the viewport in code.");
        Assert.That(sceneText, Does.Contain("roomPanStartSpeed: 0.45"), "Edge panning should start gently before accelerating.");
        Assert.That(sceneText, Does.Contain("defaultRoomZoom: 1.06"), "The room should start slightly zoomed so edge panning has room to move.");
        Assert.That(sceneText, Does.Contain("maxRoomZoom: 1.22"), "Wheel zoom should stay strong enough for panning but not feel like teleporting.");
        Assert.That(sceneText, Does.Contain("roomZoomFocus: {x: 0.5, y: 0.56}"), "Regular zoom should aim near the room vanishing point so it reads as stepping closer.");
        Assert.That(sceneText, Does.Not.Contain("scrollRoomVerticallyWithMouseWheel"), "Mouse wheel should not drive vertical shader strength; that smeared room art into stripes.");
        Assert.That(sceneText, Does.Not.Contain("scrollRoomFovWithMouseWheel"), "Mouse wheel should not drive FOV zoom; that caused the sideways drift regression.");
        Assert.That(cameraManagerText, Does.Contain("minRoomZoom"), "Mouse-wheel zoom should be allowed to zoom back out toward the full room image.");
        Assert.That(cameraManagerText, Does.Contain("autoEnableVerticalRoomPan"), "Old scenes serialized with vertical pan off should still allow cropped room art to be reached.");
        Assert.That(cameraManagerText, Does.Contain("ShouldMoveRoomVerticallyWithMouseEdges"), "Vertical edge panning should be part of the room-look input path.");
        Assert.That(cameraManagerText, Does.Contain("GetVerticalEdgeDirection"), "Vertical panning should use edge direction, not proportional drift back toward center.");
        Assert.That(cameraManagerText, Does.Contain("GetRoomInputScreenRect"), "Edge panning should use the actual room viewport rect, not assume the raw screen always matches the Canvas.");
        Assert.That(cameraManagerText, Does.Contain("return currentRoomPan;"), "Leaving the edge should hold the current pan instead of recentering.");
        Assert.That(cameraManagerText, Does.Contain("return currentRoomVerticalPan;"), "Leaving the top or bottom edge should hold the current vertical pan instead of recentering.");
        Assert.That(cameraManagerText, Does.Contain("NavigationCursorController.SetEdgePanDirection(currentHorizontalEdgeDirection, currentVerticalEdgeDirection)"), "Edge panning should publish both horizontal and vertical cursor directions.");
        Assert.That(cameraManagerText, Does.Contain("GetUpArrowCursor"), "Top-edge panning should show an up arrow cursor.");
        Assert.That(cameraManagerText, Does.Contain("GetDownArrowCursor"), "Bottom-edge panning should show a down arrow cursor.");
        Assert.That(cameraManagerText, Does.Not.Contain("returnRoomPanToCenter"), "Runtime camera panning should not expose any automatic recentering path.");
        Assert.That(cameraManagerText, Does.Contain("NavigationCursorController.SetEdgePanDirection"), "Edge panning should update the cursor state.");
        Assert.That(cameraManagerText, Does.Contain("SetActiveRoomContent"), "CameraManager should know which room stage owns the current background and hitboxes.");
        Assert.That(cameraManagerText, Does.Contain("TryApplyRoomStageLayout"), "Runtime panning must move the active room stage, not reproject door rectangles.");
        Assert.That(cameraManagerText, Does.Contain("AttachBackgroundToRoomStage"), "The background image should become a child of the active room stage in Play mode.");
        Assert.That(cameraManagerText, Does.Contain("GetRoomStageViewportScale"), "Room-stage framing should be computed from the room image and viewport.");
        Assert.That(cameraManagerText, Does.Contain("return Mathf.Max(widthScale, heightScale);"), "Room stages should always cover the whole Game view so no gray outline is visible.");
        Assert.That(cameraManagerText, Does.Contain("GetCurrentHorizontalPanSpeed"), "Edge panning should accelerate while the player holds the cursor at the edge.");
        Assert.That(cameraManagerText, Does.Contain("SmoothRoomZoom"), "Mouse-wheel zoom should be damped instead of stepping between crop values.");
        Assert.That(cameraManagerText, Does.Contain("activeRoomStage.localScale = new Vector3(stageScale, stageScale, 1f)"), "Regular zoom should scale the whole room stage so hitboxes and art share one transform.");
        Assert.That(cameraManagerText, Does.Contain("ResetRoomLookForRoomChange"), "Each new room should enter from a centered default view instead of inheriting the previous room's pan/zoom.");
        Assert.That(cameraManagerText, Does.Contain("Canvas.willRenderCanvases"), "The room stage must get a final pre-render layout pass after the Canvas resolves its true viewport size.");
        Assert.That(cameraManagerText, Does.Contain("HasRoomViewportSizeChanged"), "Room-stage layout must react to Canvas viewport changes, not only Screen.width and Screen.height.");
        Assert.That(cameraManagerText, Does.Match(@"applyingCanvasPreRenderLayout = true;\r?\n\s*try\r?\n\s*\{\r?\n\s*if \(!roomLayoutDirty && !HasRoomViewportSizeChanged\(\)\)"), "The pre-render recursion guard must be active before viewport checks can force another canvas update.");
        Assert.That(cameraManagerText, Does.Contain("if (!applyingCanvasPreRenderLayout)"), "GetUsableRectSize must not call Canvas.ForceUpdateCanvases from inside Canvas.willRenderCanvases.");
        Assert.That(cameraManagerText, Does.Match(@"EnsureBackgroundMaterialAssigned\(\);\r?\n\s*if \(updateBackground"), "Switching rooms should restore the correct background material before applying room-stage layout.");
        Assert.That(cameraManagerText, Does.Contain("roomStageOwnsMotion ? 0f"), "The shader must stay neutral when the room stage owns panning and zooming.");
        Assert.That(cameraManagerText, Does.Not.Contain("TryApplySourceImageRect"), "Door hitboxes should not be reprojected separately from the room image.");
        Assert.That(cameraManagerText, Does.Not.Contain("return 1.5f - curvedScale"), "The old signed vertical projection jumped across zero on a single mouse-wheel tick.");
        Assert.That(shaderGraphText, Does.Not.Match(@"(?s)""m_OutputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id"": ""f02995c1a6a74ca897aad1adcdadc881""\s*\}\s*,\s*""m_SlotId"": 2\s*\}\s*,\s*""m_InputSlot""\s*:\s*\{\s*""m_Node""\s*:\s*\{\s*""m_Id"": ""3b2c5930216346678c0347817b27b12a""\s*\}\s*,\s*""m_SlotId"": 2"), "The shader Step node must not switch vertical projection branches at zero.");
        Assert.That(backgroundMaterialText, Does.Contain("- _verticle_strength: 0"), "The shared background material should not save a warped zoom state.");
        Assert.That(backgroundMaterialText, Does.Contain("- _MainTex:\n        m_Texture: {fileID: 2800000, guid: f233ee9a18ce3e78bb0a642637f2d2d0, type: 3}"), "The shared background material should keep its preview texture assigned.");
    }

    [Test]
    public void GameplayHasExplorationAudioHooks()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: Audio_ExplorationMusic"), "Gameplay should start house exploration ambience without depending on MainMenu.");
        Assert.That(sceneText, Does.Contain("m_Name: Audio_DoorOpen"), "Door triggers need a shared AudioSource for the door opening sound.");
        Assert.That(sceneText, Does.Contain("guid: 5cd6bd3d35aa8e1ebae11661918fd66a"), "Exploration music should use the existing dreadforge soundscape clip.");
        Assert.That(sceneText, Does.Contain("guid: 700538fbae21acc4dae7d01a518aad25"), "Door opening should use an existing short click-style clip until final door audio exists.");
    }

    [Test]
    public void BackgroundShaderUsesFullRoomArtWidth()
    {
        string shaderGraphText = File.ReadAllText(BackgroundShaderGraphPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);

        Assert.That(shaderGraphText, Does.Not.Contain("0.20000000298023225"), "The old tutorial art crop cut off the new room images.");
        Assert.That(shaderGraphText, Does.Not.Contain("0.800000011920929"), "The old tutorial art crop cut off the new room images.");
        Assert.That(cameraManagerText, Does.Not.Contain("SourceUvXMin"), "Door hitboxes should not depend on a separate source-UV projection bridge.");
        Assert.That(cameraManagerText, Does.Not.Contain("SourceUvXRange"), "Door hitboxes should not depend on a separate source-UV projection bridge.");
    }

    [Test]
    public void DoorHitboxesUseVisibleRectTransformsAsSourceOfTruth()
    {
        string triggerText = File.ReadAllText(DoorTriggerNavigationPath);
        string cameraManagerText = File.ReadAllText(CameraManagerPath);
        string editorToolsText = File.ReadAllText(NavigationEditorToolsPath);
        string gameplaySceneText = File.ReadAllText(GameplayScenePath);
        string mainMenuSceneText = File.ReadAllText(MainMenuScenePath);

        Assert.That(triggerText, Does.Not.Contain("[ExecuteAlways]"), "Door hitboxes should not run edit-mode scripts that silently rewrite placement.");
        Assert.That(triggerText, Does.Not.Contain("backgroundShaderUvRect"), "Door hitboxes should not keep a second hidden placement coordinate.");
        Assert.That(triggerText, Does.Not.Contain("CaptureCurrentShaderAnchor"), "Manual RectTransforms should be the authoring data, not captured anchors.");
        Assert.That(triggerText, Does.Not.Contain("TryCaptureAuthoredSourceImageRect"), "Runtime should not derive a second UV coordinate from the visible RectTransform.");
        Assert.That(triggerText, Does.Not.Contain("LateUpdate"), "Door hitboxes should not chase the camera every frame.");
        Assert.That(triggerText, Does.Contain("InferSourceRoomFromHierarchy(transform)"), "Door source rooms should come from the Room_* hierarchy by default.");
        Assert.That(cameraManagerText, Does.Contain("AttachBackgroundToRoomStage"), "CameraManager should put the background under the same room stage as the hitboxes.");
        Assert.That(editorToolsText, Does.Match(@"RoomAnchor selectedRoomAnchor[\s\S]*FindRoomContentGroupForRoom\(selectedRoomAnchor\.RoomId\)"), "Selecting Ch2_Hide_* RoomAnchor objects should preview their authored room for placement.");
        Assert.That(editorToolsText, Does.Contain("ShouldForcePreviewSelectedRoom"), "Selecting a Room_* object should switch the edit preview to that room even if generic auto-preview is off.");
        Assert.That(editorToolsText, Does.Match(@"GetComponent<RoomContentGroup>\(\) != null[\s\S]*IsSelectedChapter2HideAnchor\(\)"), "Explicit room selections and Ch2_Hide_* anchors should force room preview.");
        Assert.That(editorToolsText, Does.Contain("IsSelectedChapter2HideAnchor"), "Chapter 2 hide-anchor placement should not depend on the generic room auto-preview preference.");
        Assert.That(editorToolsText, Does.Match(@"!NavigationEditorTools\.AutoPreviewSelectedRoom && !forcePreviewSelection"), "Selecting Room_* objects or Ch2_Hide_* anchors should force room preview even if generic auto-preview is off.");
        Assert.That(editorToolsText, Does.Match(@"EditorApplication\.delayCall \+= QueuePreviewForCurrentSelection"), "Reopening Unity should preview the currently selected Ch2_Hide_* anchor room after scripts reload.");
        Assert.That(editorToolsText, Does.Contain("lastAutoPreviewSelectionObject"), "Selection auto-preview should remember the last previewed selection so hierarchy changes from the preview do not spam the console.");
        Assert.That(editorToolsText, Does.Contain("ResetLastAutoPreview"), "Changing editor selection should allow a new preview while repeated hierarchy changes for the same selection stay quiet.");
        Assert.That(editorToolsText, Does.Match(@"if\s*\(\s*pingRoom\s*\)[\s\S]*Debug\.Log\(\$""Previewing"), "Automatic background room previews should stay silent so they do not bury diagnostic logs.");
        Assert.That(cameraManagerText, Does.Not.Contain("TryCaptureShaderAnchoredRect"), "CameraManager should not expose old capture APIs.");
        Assert.That(cameraManagerText, Does.Not.Contain("TryApplySourceImageRect"), "CameraManager should not expose a projection bridge for door hitboxes.");
        Assert.That(editorToolsText, Does.Not.Contain("CaptureVisibleDoorTriggerAnchorsForCurrentPreview"), "Editor previews should not save hitbox locations as a side effect.");
        Assert.That(editorToolsText, Does.Not.Contain("AutoSyncDoorTriggers"), "Door trigger sync should be an explicit menu action, not an automatic editor task.");
        Assert.That(editorToolsText, Does.Contain("Door trigger sync from doors.txt is disabled"), "doors.txt must not be able to move or recreate hand-placed door triggers.");
        Assert.That(editorToolsText, Does.Not.Contain("SetTransformParent(trigger.transform"), "Editor preview/sync code should not move existing door triggers between rooms.");
        Assert.That(editorToolsText, Does.Contain("FitToTextureWithUndo"), "Editor room previews should show the source image at native size so door placement matches runtime UVs.");
        Assert.That(gameplaySceneText, Does.Not.Contain("backgroundShaderUvRect"), "Gameplay scene should not carry stale hidden hitbox anchors.");
        Assert.That(mainMenuSceneText, Does.Not.Contain("backgroundShaderUvRect"), "MainMenu scene should not carry stale hidden hitbox anchors.");
    }

    [Test]
    public void ButlerPlayerIdleUsesStableBreathingFrameSequence()
    {
        string[] expectedFrameGuids =
        {
            "b49b0a0dc361e586fa285412dbdd72b4",
            "4d26ab12e717ccc6a5b4a91412b5b697",
            "73060248c52573dbaf7343add2a2db79",
            "77e98f788d22b7ee6113943ad6be8e25",
            "39156dcb18a071e8dc8a083ed45c7dc8",
            "dc13fb2936eab6d3aa4c87bc025ce375",
            "f7507df042b49451d40940407b12237d",
            "d07d0b91e1599fad8475de6075308575",
            "41ebd6fd2e5a42f6c3453349b099ccda",
            "2bc378ca18b1cc45b1c5722f1b1ce956",
            "7b1951ac111b05ca8c1b342aa5bf5a03",
            "9f9c4ccf52828f6f36d73b99e6e80bf6"
        };

        for (int index = 0; index < expectedFrameGuids.Length; index++)
        {
            string framePath = $"{ButlerIdleFolderPath}/butler_idle_{index + 1:00}.png";
            string frameMetaPath = framePath + ".meta";

            Assert.That(File.Exists(framePath), Is.True, $"{framePath} should exist.");
            Assert.That(File.Exists(frameMetaPath), Is.True, $"{frameMetaPath} should exist.");

            ReadPngDimensions(framePath, out int width, out int height);
            Assert.That(width, Is.EqualTo(168), $"{framePath} should keep the normal butler sprite canvas width.");
            Assert.That(height, Is.EqualTo(299), $"{framePath} should keep the normal butler sprite canvas height.");

            string frameMetaText = File.ReadAllText(frameMetaPath);
            Assert.That(frameMetaText, Does.Contain($"guid: {expectedFrameGuids[index]}"), $"{framePath} should keep its expected sprite GUID.");
            Assert.That(frameMetaText, Does.Contain("textureType: 8"), $"{framePath} should import as a Sprite.");
            Assert.That(frameMetaText, Does.Contain("spriteMode: 1"), $"{framePath} should import as a single sprite.");
            Assert.That(frameMetaText, Does.Contain("spritePixelsToUnits: 100"), $"{framePath} should match the existing butler PPU.");
            Assert.That(frameMetaText, Does.Contain("spritePivot: {x: 0.5, y: 0}"), $"{framePath} should stay bottom-centered to keep feet anchored.");
            Assert.That(frameMetaText, Does.Contain("filterMode: 1"), $"{framePath} should keep the existing point-filtered pixel-art import.");
            Assert.That(frameMetaText, Does.Contain("alphaIsTransparency: 1"), $"{framePath} should preserve transparent-background import behavior.");
        }

        AssertButlerIdleClipReferences(PlayerIdleClipPath, expectedFrameGuids, 1);
        AssertButlerIdleClipReferences(ButlerClassicIdleClipPath, expectedFrameGuids, 2);
    }

    private static PointClickPlayerMovement CreateConfiguredPointClickMovement(
        Vector2[] floorPath,
        Vector2 startPosition,
        out GameObject floorObject,
        out GameObject playerObject)
    {
        PolygonCollider2D floor = CreatePolygonCollider("PlayerBoundary_Test", floorPath, out floorObject);
        playerObject = new GameObject("Player");
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<Animator>();
        PointClickPlayerMovement movement = playerObject.AddComponent<PointClickPlayerMovement>();

        SetPrivateField(movement, "useCurrentRoomBoundary", false);
        SetPrivateField(movement, "walkableFloor", floor);
        SetPrivateField(movement, "isReady", true);
        SetPrivateField(movement, "logicalPosition", startPosition);
        SetPrivateField(movement, "destination", startPosition);
        SetPrivateField(movement, "finalDestination", startPosition);
        SetPrivateField(movement, "currentVisualOffset", Vector3.zero);
        SetPrivateField(movement, "hasRoomStageVisualReference", false);

        Physics2D.SyncTransforms();
        return movement;
    }

    private static PolygonCollider2D CreatePolygonCollider(string name, Vector2[] path, out GameObject owner)
    {
        owner = new GameObject(name);
        PolygonCollider2D collider = owner.AddComponent<PolygonCollider2D>();
        collider.pathCount = 1;
        collider.SetPath(0, path);
        Physics2D.SyncTransforms();
        return collider;
    }

    private static void AddWalkableBlocker(PointClickPlayerMovement movement, Collider2D blocker)
    {
        FieldInfo field = GetPointClickField("walkableBlockers");
        List<Collider2D> blockers = (List<Collider2D>)field.GetValue(movement);
        blockers.Clear();
        blockers.Add(blocker);
        Physics2D.SyncTransforms();
    }

    private static void SetPrivateField<T>(PointClickPlayerMovement movement, string fieldName, T value)
    {
        GetPointClickField(fieldName).SetValue(movement, value);
    }

    private static FieldInfo GetPointClickField(string fieldName)
    {
        FieldInfo field = typeof(PointClickPlayerMovement).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"PointClickPlayerMovement should have private field '{fieldName}'.");
        return field;
    }

    private static void DestroyImmediateIfNeeded(GameObject target)
    {
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void AssertScenePropSorting(string sceneText, string propName, int sortingOrder)
    {
        string escapedName = Regex.Escape(propName);
        string pattern = $@"m_Name: {escapedName}[\s\S]*?m_SortingLayer: 2[\s\S]*?m_SortingOrder: {sortingOrder}\b";

        Assert.That(sceneText, Does.Match(pattern), $"{propName} should keep its authored People-layer sorting order.");
    }

    private static string ExtractMethodBody(string sourceText, string methodName)
    {
        int methodIndex = sourceText.IndexOf(methodName, System.StringComparison.Ordinal);
        Assert.That(methodIndex, Is.GreaterThanOrEqualTo(0), $"Expected to find method '{methodName}'.");

        int bodyStart = sourceText.IndexOf('{', methodIndex);
        Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Expected method '{methodName}' to have a body.");

        int depth = 0;

        for (int i = bodyStart; i < sourceText.Length; i++)
        {
            if (sourceText[i] == '{')
            {
                depth++;
            }
            else if (sourceText[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return sourceText.Substring(bodyStart, i - bodyStart + 1);
                }
            }
        }

        Assert.Fail($"Could not extract method body for '{methodName}'.");
        return string.Empty;
    }

    private static string ExtractUnityObjectBlock(string sceneText, string objectHeader)
    {
        int blockStart = sceneText.IndexOf(objectHeader, System.StringComparison.Ordinal);
        Assert.That(blockStart, Is.GreaterThanOrEqualTo(0), $"Expected to find Unity object '{objectHeader}'.");

        int nextBlockStart = sceneText.IndexOf("\n--- !u!", blockStart + objectHeader.Length, System.StringComparison.Ordinal);
        return nextBlockStart >= 0
            ? sceneText.Substring(blockStart, nextBlockStart - blockStart)
            : sceneText.Substring(blockStart);
    }

    private static void GetColliderPathYExtents(string colliderText, out float minY, out float maxY)
    {
        MatchCollection matches = Regex.Matches(colliderText, @"(?m)^\s*- (?:- )?\{x: -?\d+(?:\.\d+)?, y: (-?\d+(?:\.\d+)?)\}");
        Assert.That(matches.Count, Is.GreaterThan(0), "Collider path should contain y coordinates.");

        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;

        for (int i = 0; i < matches.Count; i++)
        {
            float value = float.Parse(matches[i].Groups[1].Value, CultureInfo.InvariantCulture);
            minY = System.Math.Min(minY, value);
            maxY = System.Math.Max(maxY, value);
        }
    }

    private static void GetColliderPathXExtents(string colliderText, out float minX, out float maxX)
    {
        MatchCollection matches = Regex.Matches(colliderText, @"(?m)^\s*- (?:- )?\{x: (-?\d+(?:\.\d+)?), y: -?\d+(?:\.\d+)?\}");
        Assert.That(matches.Count, Is.GreaterThan(0), "Collider path should contain x coordinates.");

        minX = float.PositiveInfinity;
        maxX = float.NegativeInfinity;

        for (int i = 0; i < matches.Count; i++)
        {
            float value = float.Parse(matches[i].Groups[1].Value, CultureInfo.InvariantCulture);
            minX = System.Math.Min(minX, value);
            maxX = System.Math.Max(maxX, value);
        }
    }

    private static string ReadGuid(string metaPath)
    {
        string metaText = File.ReadAllText(metaPath);
        Match match = Regex.Match(metaText, @"(?m)^guid: ([0-9a-f]{32})$");

        Assert.That(match.Success, Is.True, $"{metaPath} should contain a Unity GUID.");
        return match.Groups[1].Value;
    }

    private static void AssertScenePropSprite(string sceneText, string propName, string spriteGuid)
    {
        string escapedName = Regex.Escape(propName);
        string escapedGuid = Regex.Escape(spriteGuid);
        string pattern = $@"m_Name: {escapedName}[\s\S]*?m_Sprite: \{{fileID: [-\d]+, guid: {escapedGuid}, type: 3\}}";

        Assert.That(sceneText, Does.Match(pattern), $"{propName} should keep its intended sprite asset.");
    }

    private static void AssertBottomEdgeTransition(string sceneText, string triggerName, string sourceRoom, string destinationRoom)
    {
        int blockStart = sceneText.IndexOf($"m_Name: {triggerName}", System.StringComparison.Ordinal);
        Assert.That(blockStart, Is.GreaterThanOrEqualTo(0), $"Expected to find bottom-edge transition '{triggerName}'.");

        int nextGameObjectStart = sceneText.IndexOf("\n--- !u!1 &", blockStart + triggerName.Length, System.StringComparison.Ordinal);
        string triggerBlock = nextGameObjectStart >= 0
            ? sceneText.Substring(blockStart, nextGameObjectStart - blockStart)
            : sceneText.Substring(blockStart);

        Assert.That(triggerBlock, Does.Contain($"sourceRoom: {sourceRoom}"), $"{triggerName} should stay connected to its source room.");
        Assert.That(triggerBlock, Does.Contain($"destinationRoom: {destinationRoom}"), $"{triggerName} should keep its destination room.");
        Assert.That(triggerBlock, Does.Contain("useBottomScreenEdgeInteraction: 1"), $"{triggerName} should use the bottom screen edge instead of its floor rectangle.");
        Assert.That(triggerBlock, Does.Contain("disableGraphicRaycastForScreenEdgeInteraction: 1"), $"{triggerName} should not raycast through its old broad UI rectangle.");
        Assert.That(triggerBlock, Does.Contain("m_RaycastTarget: 0"), $"{triggerName} should not receive pointer hits through the old floor-sized Image.");
        Assert.That(triggerBlock, Does.Contain("requirePlayerProximity: 0"), $"{triggerName} should behave like a room-edge exit, not a walk-to-floor-zone door.");
        Assert.That(triggerBlock, Does.Contain("walkPlayerToTriggerWhenFar: 0"), $"{triggerName} should not path the butler to the old floor-covering rectangle.");
    }

    private static void AssertButlerIdleClipReferences(string clipPath, string[] expectedFrameGuids, int expectedCurveCount)
    {
        string clipText = File.ReadAllText(clipPath);

        Assert.That(clipText, Does.Contain("m_SampleRate: 12"), $"{clipPath} should play the soft idle loop at 12 fps.");
        Assert.That(clipText, Does.Contain("m_StopTime: 1"), $"{clipPath} should cover the full 12-frame loop.");
        Assert.That(clipText, Does.Contain("m_LoopTime: 1"), $"{clipPath} should loop cleanly.");

        for (int index = 0; index < expectedFrameGuids.Length; index++)
        {
            string expectedReference = $"{{fileID: 21300000, guid: {expectedFrameGuids[index]}, type: 3}}";
            int referenceCount = Regex.Matches(clipText, Regex.Escape(expectedReference)).Count;

            Assert.That(referenceCount, Is.EqualTo(expectedCurveCount * 2), $"{clipPath} should reference frame {index + 1:00} in each sprite curve and its clip mapping.");
        }
    }

    private static void ReadPngDimensions(string path, out int width, out int height)
    {
        byte[] header = new byte[24];

        using (FileStream stream = File.OpenRead(path))
        {
            int bytesRead = stream.Read(header, 0, header.Length);
            Assert.That(bytesRead, Is.EqualTo(header.Length), $"{path} should have a complete PNG header.");
        }

        Assert.That(header[0], Is.EqualTo((byte)0x89), $"{path} should be a PNG file.");
        Assert.That(header[1], Is.EqualTo((byte)0x50), $"{path} should be a PNG file.");
        Assert.That(header[2], Is.EqualTo((byte)0x4E), $"{path} should be a PNG file.");
        Assert.That(header[3], Is.EqualTo((byte)0x47), $"{path} should be a PNG file.");

        width = ReadBigEndianInt32(header, 16);
        height = ReadBigEndianInt32(header, 20);
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24)
            | (bytes[offset + 1] << 16)
            | (bytes[offset + 2] << 8)
            | bytes[offset + 3];
    }

}
