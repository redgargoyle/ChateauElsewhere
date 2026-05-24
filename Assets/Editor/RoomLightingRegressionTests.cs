using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class RoomLightingRegressionTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string ControllerPath = "Assets/Scripts/Lighting/RoomLightingController.cs";
    private const string OverlayPath = "Assets/Scripts/Lighting/RoomLightOverlay.cs";
    private const string PresetScriptPath = "Assets/Scripts/Lighting/RoomLightingPreset.cs";
    private const string BootstrapPath = "Assets/Scripts/UrpPostProcessingBootstrap.cs";
    private const string NoPostLayerPath = "Assets/Scripts/Lighting/NoPostProcessRenderLayer.cs";
    private const string BypassCameraPath = "Assets/Scripts/Lighting/PostProcessBypassCamera.cs";
    private const string FlameToolPath = "Assets/Editor/FlameBloomSeparationTools.cs";
    private const string PresetPath = "Assets/Resources/Lighting/RoomLightingPreset.asset";
    private const string ReadmePath = "Assets/Scripts/Lighting/README.md";

    [Test]
    public void LightingControllerBuildsRoomLocalNonBlockingOverlays()
    {
        string controllerText = File.ReadAllText(ControllerPath);
        string overlayText = File.ReadAllText(OverlayPath);
        string presetScriptText = File.ReadAllText(PresetScriptPath);

        Assert.That(controllerText, Does.Contain("[ExecuteAlways]"), "Lighting should work in Edit mode, not only when Play mode starts.");
        Assert.That(controllerText, Does.Contain("RoomContentGroup"), "Lighting should attach to the existing room roots.");
        Assert.That(controllerText, Does.Contain("Resources.Load<RoomLightingPreset>"), "Lighting should be driven by a simple editable preset asset.");
        Assert.That(controllerText, Does.Contain("Create Missing Scene Lights From Preset"), "Artists should have a simple explicit way to create missing scene light objects.");
        Assert.That(controllerText, Does.Contain("CreateMissingSceneLight"), "The preset should create real scene children instead of runtime-only overlays.");
        Assert.That(controllerText, Does.Contain("Button_Lights"), "The player should have a small HUD control for lights.");
        Assert.That(controllerText, Does.Contain("HudSortingOrder = 7000"), "The light toggle should appear above gameplay UI.");
        Assert.That(controllerText, Does.Contain("KeyCode.L"), "The player should be able to toggle lights quickly while testing.");
        Assert.That(controllerText, Does.Contain("Debug.LogWarning"), "Lighting setup failures should show up in the Console.");
        Assert.That(overlayText, Does.Contain("[ExecuteAlways]"), "Individual lights should preview while editing the scene.");
        Assert.That(overlayText, Does.Contain("EditorApplication.update"), "Edit mode should repaint animated light previews.");
        Assert.That(overlayText, Does.Contain("animationStyle"), "Each scene light should own its animation style.");
        Assert.That(overlayText, Does.Contain("FireplaceSource"), "Fireplace sources should have a hotter animation distinct from the room spill.");
        Assert.That(overlayText, Does.Contain("GetSourceLightSprite"), "Fireplace sources should use a tighter generated sprite than soft room spills.");
        Assert.That(overlayText, Does.Contain("raycastTarget = false"), "Light overlays must never block room doors or interactables.");
        Assert.That(overlayText, Does.Contain("CaptureAuthoringScale"), "Edit-mode light resizing should not be overwritten by flicker scale animation.");
        Assert.That(overlayText, Does.Contain("if (!Application.isPlaying)"), "Transform scale animation should back off while artists resize scene lights in Edit mode.");
        Assert.That(overlayText, Does.Contain("ColorUsage(false, true)"), "Room lights should allow HDR color values so bloom can react to source glows.");
        Assert.That(presetScriptText, Does.Contain("ColorUsage(false, true)"), "Preset-authored room lights should also allow HDR color values.");
    }

    [Test]
    public void FlameParticlesCanBypassGlobalBloomWhileRoomOverlaysDriveIt()
    {
        string bootstrapText = File.ReadAllText(BootstrapPath);
        string layerText = File.ReadAllText(NoPostLayerPath);
        string cameraText = File.ReadAllText(BypassCameraPath);
        string toolText = File.ReadAllText(FlameToolPath);
        string readmeText = File.ReadAllText(ReadmePath);

        Assert.That(layerText, Does.Contain("NoPostProcessFlame"), "The flame particle should have a named layer that can be excluded from the post-processed camera.");
        Assert.That(layerText, Does.Contain("GetComponentsInChildren<Transform>"), "The no-post-process layer must apply to nested particle objects.");
        Assert.That(cameraText, Does.Contain("renderPostProcessing = false"), "The bypass camera must render without bloom/post-processing.");
        Assert.That(cameraText, Does.Contain("CameraRenderType.Overlay"), "The bypass camera must be a stacked URP overlay camera so it cannot clear over the room render.");
        Assert.That(cameraText, Does.Contain("cameraStack.Add(bypassCamera)"), "The bypass camera should render through the main camera stack.");
        Assert.That(cameraText, Does.Contain("sourceCamera.cullingMask &= ~renderLayers.value"), "The main camera should not also render the no-post-process flame layer.");
        Assert.That(bootstrapText, Does.Contain("PostProcessBypassCamera.EnsureForCamera"), "Runtime camera setup should survive scene loads.");
        Assert.That(toolText, Does.Contain("Setup Selected Flame Bloom Separation"), "Artists should have a single menu action for separating flame particles from bloom drivers.");
        Assert.That(toolText, Does.Contain("BloomDriver"), "The menu action should create a compact bloom driver overlay.");
        Assert.That(toolText, Does.Contain("PaintedReflection"), "The menu action should create a broader painted reflection overlay.");
        Assert.That(readmeText, Does.Contain("Flame Bloom Separation"), "The separation workflow should be documented for room-light tuning.");
    }

    [Test]
    public void GameplaySceneHasExplicitLightingController()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(sceneText, Does.Contain("m_Name: RoomLightingController"), "Lighting should be a visible scene object, not only a hidden runtime bootstrap.");
        Assert.That(sceneText, Does.Contain("guid: 1aeafe6c29b04bb5a9e6d98f06298e45"), "Gameplay should contain the RoomLightingController script.");
        Assert.That(sceneText, Does.Contain("guid: e9fdf1d89f634985b8b38e991ae5a1d2"), "Gameplay should directly reference the editable lighting preset.");
        Assert.That(sceneText, Does.Contain("toggleKey: 108"), "The L key should toggle lights in Play mode.");
        Assert.That(sceneText, Does.Contain("showHud: 1"), "The Lights HUD button should be enabled.");
        Assert.That(sceneText, Does.Contain("createMissingLightsFromPreset: 1"), "Gameplay should create any missing editable scene lights from the preset.");
        Assert.That(sceneText, Does.Contain("m_Name: Lighting"), "Rooms should contain visible Lighting children for edit-mode authoring.");
        Assert.That(sceneText, Does.Contain("m_Name: RoomLight_"), "Gameplay should contain editable RoomLight scene objects.");
    }

    [Test]
    public void LightingPresetShowsSeveralPrototypeTechniques()
    {
        string presetText = File.ReadAllText(PresetPath);
        string readmeText = File.ReadAllText(ReadmePath);

        Assert.That(Regex.Matches(presetText, "roomName:").Count, Is.GreaterThanOrEqualTo(10), "The preset should contain enough lights to compare rooms.");
        Assert.That(presetText, Does.Contain("roomName: Grand Entrance Hall"));
        Assert.That(presetText, Does.Contain("roomName: Library"));
        Assert.That(presetText, Does.Contain("roomName: Kitchen"));
        Assert.That(presetText, Does.Contain("roomName: Chapel"));
        Assert.That(presetText, Does.Contain("roomName: Conservatory"));
        Assert.That(presetText, Does.Contain("roomName: Drawing Room"));
        Assert.That(presetText, Does.Contain("roomName: Dining Room"));
        Assert.That(presetText, Does.Contain("roomName: Master Bedroom Suite"));
        Assert.That(presetText, Does.Contain("roomName: Billiard Room"));
        Assert.That(presetText, Does.Contain("roomName: Blue Bedroom"));
        Assert.That(presetText, Does.Contain("roomName: Upper Gallery"));
        Assert.That(presetText, Does.Contain("animationStyle: 0"), "Sconce flicker should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 1"), "Chandelier bloom should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 2"), "Hearth breathing should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 3"), "Window glow should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 4"), "Candle cluster flicker should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 5"), "Fireplace source flicker should be represented.");
        Assert.That(presetText, Does.Contain("lightName: Drawing Fireplace Source"), "The fireplace source/spill example should be easy to find.");
        Assert.That(presetText, Does.Contain("lightName: Drawing Fireplace Room Spill"), "The fireplace source/spill example should be easy to compare.");

        Assert.That(readmeText, Does.Contain("Soft overlay lights"));
        Assert.That(readmeText, Does.Contain("Hand-painted frame swaps"));
        Assert.That(readmeText, Does.Contain("Mask/shader lighting"));
        Assert.That(readmeText, Does.Contain("Fireplace Source Pattern"));
        Assert.That(readmeText, Does.Contain("Prototype Tour"));
    }
}
