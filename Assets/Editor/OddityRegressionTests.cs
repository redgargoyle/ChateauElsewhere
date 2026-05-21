using System.IO;
using NUnit.Framework;

public class OddityRegressionTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string OddityAnimatorPath = "Assets/Scripts/Oddities/OdditySpriteAnimator.cs";
    private const string OddityReadmePath = "Assets/Scripts/Oddities/README.md";

    [Test]
    public void OdditiesAreEditableRoomObjects()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string animatorText = File.ReadAllText(OddityAnimatorPath);
        string readmeText = File.ReadAllText(OddityReadmePath);

        Assert.That(animatorText, Does.Contain("[ExecuteAlways]"), "Oddities should preview while editing, not only in Play mode.");
        Assert.That(animatorText, Does.Contain("EditorApplication.update"), "Animated oddities should repaint in Edit mode.");
        Assert.That(animatorText, Does.Contain("SceneView.RepaintAll"), "Scene view previews should update without pressing Play.");
        Assert.That(animatorText, Does.Contain("targetImage.raycastTarget = false"), "Decorative oddities must not block door hitboxes.");
        Assert.That(animatorText, Does.Contain("pingPong"), "Portrait look animations should be able to look back and forth.");

        Assert.That(sceneText, Does.Contain("m_Name: Oddities"), "Rooms should have a visible Oddities authoring layer.");
        Assert.That(sceneText, Does.Contain("m_Name: Oddity_GEH_Rear_RightPortraitWatcher"), "The rear entrance portrait oddity should be a named scene object.");
        Assert.That(sceneText, Does.Contain("m_Name: PortraitWindow"), "The portrait oddity should be clipped into the painted frame.");
        Assert.That(sceneText, Does.Contain("guid: 3312d7739989d2b4e91e6319e9a96d76"), "The portrait oddity should use RectMask2D instead of floating over the wall.");
        Assert.That(sceneText, Does.Contain("m_Name: PortraitImage_LookingLady"), "The old floating Lady image should be renamed and owned by the portrait oddity.");
        Assert.That(sceneText, Does.Contain("m_Father: {fileID: 2506000022}"), "The lady animation should live inside the masked portrait window.");
        Assert.That(sceneText, Does.Contain("enableAnchoredBackgroundAnimation: 0"), "The old camera-composited floating head should be disabled for this scene.");
        Assert.That(sceneText, Does.Contain("anchoredAnimationReference: {fileID: 0}"), "CameraManager should not own the portrait oddity placement.");
        Assert.That(sceneText, Does.Not.Contain("m_Name: Lady"), "The old root-level Lady object name should not survive as an authoring target.");
        Assert.That(sceneText, Does.Not.Contain("targetSpriteRenderer: {fileID: 1453967421}"), "The portrait oddity should not keep the disabled legacy sprite-player setup.");

        Assert.That(readmeText, Does.Contain("real scene objects under a room's `Oddities` child"));
        Assert.That(readmeText, Does.Contain("Oddity_GEH_Rear_RightPortraitWatcher"));
    }
}
