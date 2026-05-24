using System.IO;
using NUnit.Framework;

public class CharacterRegressionTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string WalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string OccluderPath = "Assets/Scripts/Characters/RoomForegroundOccluder.cs";
    private const string CharactersReadmePath = "Assets/Scripts/Characters/README.md";
    private const string PlayerWalkUpClipPath = "Assets/Animation/Player_Walk_Up.anim";
    private const string AtlasFolder = "Assets/Art/Characters/Atlases";
    private const string SourceFolder = "Assets/Art/Characters/SourceSheets";

    [Test]
    public void RoomPeopleAreEditableDepthScaledSceneObjects()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string walkerText = File.ReadAllText(WalkerPath);
        string occluderText = File.ReadAllText(OccluderPath);
        string readmeText = File.ReadAllText(CharactersReadmePath);

        Assert.That(walkerText, Does.Contain("[ExecuteAlways]"), "People should preview in Edit mode, like lights and oddities.");
        Assert.That(walkerText, Does.Contain("RawImage"), "Walkers should use atlas UVs without needing Unity sprite slicing.");
        Assert.That(walkerText, Does.Contain("Vector2[] pathPoints"), "Walk paths should be simple editable room-local points.");
        Assert.That(walkerText, Does.Contain("previewPathInEditMode"), "Edit mode frame preview should not force people to walk while artists place them.");
        Assert.That(walkerText, Does.Contain("snapToWholePixels"), "Walkers can opt into whole-pixel snapping, but the shipped room people use smoother subpixel movement.");
        Assert.That(walkerText, Does.Contain("GetMotionOffset"), "Walkers should have subtle stride and idle motion instead of sliding static cards.");
        Assert.That(walkerText, Does.Contain("Mathf.InverseLerp(nearY, farY"), "Walkers should scale/tint from front to back of the painted room.");
        Assert.That(walkerText, Does.Contain("rectTransform.localScale = scale"), "Perspective scale should affect the whole character card.");
        Assert.That(walkerText, Does.Contain("targetImage.raycastTarget = false"), "Characters must not block door hitboxes.");

        Assert.That(occluderText, Does.Contain("[ExecuteAlways]"), "Foreground occluders should be tweakable in Edit mode.");
        Assert.That(occluderText, Does.Contain("sourceUvRect"), "Occluders should be room-art crops, not hand-painted duplicate textures.");
        Assert.That(occluderText, Does.Contain("targetImage.uvRect"), "Occluders should expose the exact crop rect.");
        Assert.That(occluderText, Does.Contain("targetImage.raycastTarget = false"), "Occluders must not block door hitboxes.");

        Assert.That(sceneText, Does.Contain("m_Name: People"));
        Assert.That(sceneText, Does.Contain("m_Name: Walker_GEH_GreenGentleman"));
        Assert.That(sceneText, Does.Contain("m_Name: Walker_GEH_GreenLady"));
        Assert.That(sceneText, Does.Contain("previewPathInEditMode: 0"), "Scene-authored walkers should not move through their path just because the editor is open.");
        Assert.That(sceneText, Does.Contain("snapToWholePixels: 0"), "The first walkers should use smooth subpixel motion while the room stage pans and scales.");
        Assert.That(sceneText, Does.Contain("endpointPauseSeconds: 0.75"), "Example walkers should briefly idle at path endpoints.");
        Assert.That(sceneText, Does.Contain("m_Pivot: {x: 0.5, y: 0.035}"), "Walker cards should pivot close to the normalized foot baseline.");
        Assert.That(sceneText, Does.Contain("farY: -90"), "The example people paths should stay on the Grand Entrance Hall floor plane.");
        Assert.That(sceneText, Does.Contain("guid: 1b45edb93a9b42e58fa4cad7d4de84ce"), "Gameplay walkers should use RoomPersonWalker2D.");
        Assert.That(sceneText, Does.Contain("guid: ca8806db799f4f2c92e6cc08b4287001"), "The gentleman atlas should be scene-referenced.");
        Assert.That(sceneText, Does.Contain("guid: ca8806db799f4f2c92e6cc08b4287002"), "The lady atlas should be scene-referenced.");
        Assert.That(sceneText, Does.Contain("m_Name: ForegroundOccluders"));
        Assert.That(sceneText, Does.Contain("m_Name: ForegroundOccluder_GEH_FrontRailingLeft"));
        Assert.That(sceneText, Does.Contain("guid: c75cbf61393f4dbb8907c884b5237cc0"), "Gameplay occluders should use RoomForegroundOccluder.");

        Assert.That(Directory.GetFiles(AtlasFolder, "*_atlas.png").Length, Is.EqualTo(8), "All generated character sheets should have project-owned transparent atlases.");
        Assert.That(Directory.GetFiles(SourceFolder, "*_source.png").Length, Is.EqualTo(8), "The original generated sheets should be kept for later reprocessing.");
        Assert.That(readmeText, Does.Contain("foot baseline"));
        Assert.That(readmeText, Does.Contain("foreground occluder cards"));
        Assert.That(readmeText, Does.Contain("People > Walker_GEH_GreenGentleman"));
    }

    [Test]
    public void ButlerWalkUpUsesFullBackStride()
    {
        string walkUpText = File.ReadAllText(PlayerWalkUpClipPath);

        Assert.That(walkUpText, Does.Contain("492e6bd14a7d45c985f22aebe6b7812a"), "Walk-up should include the back-facing neutral frame.");
        Assert.That(walkUpText, Does.Contain("f3bd9ee8373040feae610350d5ea456a"), "Walk-up should include the first back stride frame.");
        Assert.That(walkUpText, Does.Contain("5f1ad806fd3445738f9edcab6d346a4d"), "Walk-up should include the middle back stride frame.");
        Assert.That(walkUpText, Does.Contain("46b92049a26a4c72b9ad03f464e169ae"), "Walk-up should include the opposite back stride frame.");
        Assert.That(walkUpText, Does.Contain("m_StopTime: 0.8333334"), "Walk-up should run long enough to cycle through the full stride instead of a two-pose slide.");
    }
}
