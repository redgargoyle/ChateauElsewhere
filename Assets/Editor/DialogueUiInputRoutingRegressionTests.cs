using System.IO;
using System.Reflection;
using NUnit.Framework;

public sealed class DialogueUiInputRoutingRegressionTests
{
    private const string CoatPickupPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1CoatPickup.cs";
    private const string SceneActionPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1SceneAction.cs";
    private const string GuestFindActionPath = "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestFindAction.cs";
    private const string DoorTriggerPath = "Assets/Scripts/Navigation/DoorTriggerNavigation.cs";
    private const string ClockHandsPath = "Assets/Scripts/Story/GameClockHandsDisplay.cs";

    [Test]
    public void DialogueSkipUiUsesTheSharedWorldInputGate()
    {
        MethodInfo blockingUiMethod = typeof(PointClickPlayerMovement).GetMethod(
            "IsPointerOverBlockingUi",
            BindingFlags.Public | BindingFlags.Static);
        string movementText = File.ReadAllText("Assets/Scripts/PointClickPlayerMovement.cs");
        string subtitleServiceText = File.ReadAllText("Assets/Scripts/UI/SubtitleService.cs");

        Assert.That(
            blockingUiMethod,
            Is.Not.Null,
            "World interactions need one public UI-raycast gate so a dialogue Skip click cannot leak into the room.");
        Assert.That(
            movementText,
            Does.Contain("eventSystem.RaycastAll(pointerEventData, uiRaycastResults)"),
            "The shared world-input gate must raycast the UI at the pointer position.");
        Assert.That(
            subtitleServiceText,
            Does.Contain("image.raycastTarget = true"),
            "The subtitle Skip button must remain a raycastable UI target.");
    }

    [Test]
    public void WorldClickFallbacksUseTheSharedBlockingUiGate()
    {
        string coatPickupText = File.ReadAllText(CoatPickupPath);
        string sceneActionText = File.ReadAllText(SceneActionPath);
        string guestFindActionText = File.ReadAllText(GuestFindActionPath);
        string doorTriggerText = File.ReadAllText(DoorTriggerPath);
        string clockHandsText = File.ReadAllText(ClockHandsPath);

        Assert.That(
            coatPickupText,
            Does.Contain("PointClickPlayerMovement.IsPointerOverBlockingUi"),
            "The coat picker must ignore pointer input already claimed by dialogue UI.");
        Assert.That(
            sceneActionText,
            Does.Contain("PointClickPlayerMovement.IsPointerOverBlockingUi"),
            "Chapter 1 scene actions must ignore pointer input already claimed by dialogue UI.");
        Assert.That(
            guestFindActionText,
            Does.Contain("PointClickPlayerMovement.IsPointerOverBlockingUi"),
            "Guest interaction fallbacks must ignore pointer input already claimed by dialogue UI.");
        Assert.That(
            doorTriggerText,
            Does.Contain("PointClickPlayerMovement.IsPointerOverBlockingUi"),
            "Door and stair fallback clicks must ignore pointer input already claimed by dialogue UI.");
        Assert.That(
            clockHandsText,
            Does.Contain("PointClickPlayerMovement.IsPointerOverBlockingUi"),
            "Clock click handling must ignore pointer input already claimed by dialogue UI.");
    }
}
