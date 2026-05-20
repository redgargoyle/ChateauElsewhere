using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class RoomLightingRegressionTests
{
    private const string ControllerPath = "Assets/Scripts/Lighting/RoomLightingController.cs";
    private const string OverlayPath = "Assets/Scripts/Lighting/RoomLightOverlay.cs";
    private const string PresetPath = "Assets/Resources/Lighting/RoomLightingPreset.asset";
    private const string ReadmePath = "Assets/Scripts/Lighting/README.md";

    [Test]
    public void LightingControllerBuildsRoomLocalNonBlockingOverlays()
    {
        string controllerText = File.ReadAllText(ControllerPath);
        string overlayText = File.ReadAllText(OverlayPath);

        Assert.That(controllerText, Does.Contain("RuntimeInitializeOnLoadMethod"), "Lighting should bootstrap itself in gameplay scenes.");
        Assert.That(controllerText, Does.Contain("RoomContentGroup"), "Lighting should attach to the existing room roots.");
        Assert.That(controllerText, Does.Contain("Resources.Load<RoomLightingPreset>"), "Lighting should be driven by a simple editable preset asset.");
        Assert.That(controllerText, Does.Contain("Button_Lights"), "The player should have a small HUD control for lights.");
        Assert.That(controllerText, Does.Contain("KeyCode.L"), "The player should be able to toggle lights quickly while testing.");
        Assert.That(overlayText, Does.Contain("raycastTarget = false"), "Light overlays must never block room doors or interactables.");
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
        Assert.That(presetText, Does.Contain("animationStyle: 0"), "Sconce flicker should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 1"), "Chandelier bloom should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 2"), "Hearth breathing should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 3"), "Window glow should be represented.");
        Assert.That(presetText, Does.Contain("animationStyle: 4"), "Candle cluster flicker should be represented.");

        Assert.That(readmeText, Does.Contain("Soft overlay lights"));
        Assert.That(readmeText, Does.Contain("Hand-painted frame swaps"));
        Assert.That(readmeText, Does.Contain("Mask/shader lighting"));
    }
}
