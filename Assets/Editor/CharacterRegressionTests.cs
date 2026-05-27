using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

public class CharacterRegressionTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string WalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string CharacterAnimatorDriverPath = "Assets/Scripts/Characters/CharacterAnimatorDriver.cs";
    private const string CharacterSelectionMenuPath = "Assets/Scripts/Characters/CharacterSelectionMenu.cs";
    private const string CharacterAnimationAssetBuilderPath = "Assets/Editor/CharacterAnimationAssetBuilder.cs";
    private const string CharactersReadmePath = "Assets/Scripts/Characters/README.md";
    private const string PlayerWalkUpClipPath = "Assets/Animation/Player/Player_Walk_Up.anim";
    private const string ButlerClassicWalkDownClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Walk_Down.anim";
    private const string ButlerClassicControllerPath = "Assets/Animation/ButlerClassic/ButlerClassic.controller";
    private const string ButlerClassicControllerMetaPath = "Assets/Animation/ButlerClassic/ButlerClassic.controller.meta";
    private const string ButlerClassicIdleFolder = "Assets/Characters/ButlerClassic/idle/aligned";
    private const string ButlerClassicIdleDownClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Down.anim";
    private const string ButlerClassicIdleLeftClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Left.anim";
    private const string ButlerClassicIdleRightClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Right.anim";
    private const string ButlerClassicIdleUpClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Up.anim";
    private const string GentlemanBlackDirectionalFolder = "Assets/Characters/GentlemanBlack/directional/aligned";
    private const string GentlemanBlackIdleClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Idle.anim";
    private const string GentlemanBlackWalkDownClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Walk_Down.anim";
    private const string GentlemanBlackWalkLeftClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Walk_Left.anim";
    private const string GentlemanBlackWalkRightClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Walk_Right.anim";
    private const string GentlemanBlackWalkUpClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Walk_Up.anim";
    private const string AnimationFolder = "Assets/Animation";
    private const string AtlasFolder = "Assets/Art/Characters/Atlases";
    private const string SourceFolder = "Assets/Art/Characters/SourceSheets";

    [Test]
    public void RoomPeopleAreEditableDepthScaledSceneObjects()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string walkerText = File.ReadAllText(WalkerPath);
        string animatorDriverText = File.ReadAllText(CharacterAnimatorDriverPath);
        string readmeText = File.ReadAllText(CharactersReadmePath);

        Assert.That(walkerText, Does.Contain("[ExecuteAlways]"), "People should preview in Edit mode, like lights and oddities.");
        Assert.That(walkerText, Does.Contain("Animator"), "Walkers should use regular Unity Animator clips, not their own atlas frame stepping.");
        Assert.That(walkerText, Does.Contain("Graphic"), "Walkers should still work as room-stage UI characters.");
        Assert.That(walkerText, Does.Not.Contain("RawImage"), "RoomPersonWalker2D should not own RawImage atlas animation anymore.");
        Assert.That(walkerText, Does.Not.Contain("uvRect"), "Animation clips should switch frames; the walker should not animate atlas UVs.");
        Assert.That(walkerText, Does.Not.Contain("AdvanceFrame"), "Frame stepping belongs in Animation clips, not RoomPersonWalker2D.");
        Assert.That(walkerText, Does.Contain("Vector2[] pathPoints"), "Walk paths should be simple editable room-local points.");
        Assert.That(walkerText, Does.Contain("previewPathInEditMode"), "Edit mode frame preview should not force people to walk while artists place them.");
        Assert.That(walkerText, Does.Contain("snapToWholePixels"), "Walkers can opt into whole-pixel snapping, but the shipped room people use smoother subpixel movement.");
        Assert.That(walkerText, Does.Contain("GetMotionOffset"), "Walkers should have subtle stride and idle motion instead of sliding static cards.");
        Assert.That(walkerText, Does.Contain("CharacterAnimatorDriver"), "NPC walkers should feed the same Animator parameter protocol as the player.");
        Assert.That(animatorDriverText, Does.Contain("IsWalkingUp"), "The shared character animation driver should expose the same directional booleans as the player controller.");
        Assert.That(animatorDriverText, Does.Contain("IsFacingUp"), "The shared character animation driver should also expose persistent facing booleans for directional idle states.");
        Assert.That(animatorDriverText, Does.Contain("DetermineDirection"), "Player and NPCs should share averaged direction selection.");
        Assert.That(walkerText, Does.Contain("Mathf.InverseLerp(nearY, farY"), "Walkers should scale/tint from front to back of the painted room.");
        Assert.That(walkerText, Does.Contain("rectTransform.localScale = scale"), "Perspective scale should affect the whole character card.");
        Assert.That(walkerText, Does.Contain("targetGraphic.raycastTarget = false"), "Characters must not block door hitboxes.");

        Assert.That(sceneText, Does.Contain("m_Name: People"));
        Assert.That(sceneText, Does.Contain("m_Name: Walker_GEH_GreenGentleman"));
        Assert.That(sceneText, Does.Contain("m_Name: Walker_GEH_GreenLady"));
        Assert.That(sceneText, Does.Contain("m_Controller: {fileID: 22100000"), "Scene walkers should use Animator override controllers.");
        Assert.That(sceneText, Does.Contain("guid: ceaa3c01ace045088969052643a77d55"), "The gentleman walker should use the generated GentlemanGreen override controller.");
        Assert.That(sceneText, Does.Contain("guid: 7c2d7e99830f4335a94987e1faf0e442"), "The lady walker should use the generated LadyGreen override controller.");
        Assert.That(sceneText, Does.Contain("previewPathInEditMode: 0"), "Scene-authored walkers should not move through their path just because the editor is open.");
        Assert.That(sceneText, Does.Contain("snapToWholePixels: 0"), "The first walkers should use smooth subpixel motion while the room stage pans and scales.");
        Assert.That(sceneText, Does.Contain("endpointPauseSeconds: 0.75"), "Example walkers should briefly idle at path endpoints.");
        Assert.That(sceneText, Does.Contain("m_Pivot: {x: 0.5, y: 0.035}"), "Walker cards should pivot close to the normalized foot baseline.");
        Assert.That(sceneText, Does.Contain("farY: -90"), "The example people paths should stay on the Grand Entrance Hall floor plane.");
        Assert.That(sceneText, Does.Contain("guid: 1b45edb93a9b42e58fa4cad7d4de84ce"), "Gameplay walkers should use RoomPersonWalker2D.");
        Assert.That(sceneText, Does.Contain("guid: 8f8728ad492a40d08efef615688bea56"), "The gentleman Image should start on a generated sprite frame.");
        Assert.That(sceneText, Does.Contain("guid: 5b37355315364217b2e5185b619c748d"), "The lady Image should start on a generated sprite frame.");
        Assert.That(Directory.GetFiles(AnimationFolder, "*.overrideController", SearchOption.AllDirectories).Length, Is.GreaterThanOrEqualTo(8), "Each character folder should have a generated Animator override controller.");
        Assert.That(Directory.GetFiles(AnimationFolder, "*_Walk_*.anim", SearchOption.AllDirectories).Length, Is.GreaterThanOrEqualTo(32), "Characters should expose editable directional walk clips under Assets/Animation.");
        Assert.That(Directory.GetFiles(AtlasFolder, "*_atlas.png").Length, Is.EqualTo(8), "All generated character sheets should have project-owned transparent atlases.");
        Assert.That(Directory.GetFiles(SourceFolder, "*_source.png").Length, Is.EqualTo(8), "The original generated sheets should be kept for later reprocessing.");
        Assert.That(readmeText, Does.Contain("Unity Animator"));
        Assert.That(readmeText, Does.Contain("SpriteRenderer"));
        Assert.That(readmeText, Does.Contain("foot baseline"));
        Assert.That(readmeText, Does.Contain("People > Walker_GEH_GreenGentleman"));
    }

    [Test]
    public void GameplayHasCharacterAnimationTestSelector()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string menuText = File.ReadAllText(CharacterSelectionMenuPath);
        string clipText = File.ReadAllText(ButlerClassicWalkDownClipPath);

        Assert.That(menuText, Does.Contain("OnGUI"), "The test selector should be a lightweight IMGUI menu, not another gameplay Canvas layer.");
        Assert.That(menuText, Does.Contain("RuntimeAnimatorController"), "Selections should swap the player's Animator override controller.");
        Assert.That(menuText, Does.Contain("playerAnimator.Rebind"), "Changing character controllers should immediately reset the Animator onto the selected clips.");
        Assert.That(menuText, Does.Contain("RefreshAnimatorParameters"), "Swapping controllers should refresh cached Animator parameters before directional idle is evaluated.");
        Assert.That(menuText, Does.Contain("IsBlockingGameplayInput"), "The selector should keep menu clicks from becoming floor or door clicks.");

        Assert.That(sceneText, Does.Contain("m_Name: UI_CharacterSelectionMenu"));
        Assert.That(sceneText, Does.Contain("guid: c4f61fdc7a9646f1b7f011b6b8d65a9d"), "Gameplay should include the character selector component.");
        Assert.That(sceneText, Does.Contain("displayName: ButlerClassic"));
        Assert.That(sceneText, Does.Contain("displayName: ButlerYoung"));
        Assert.That(sceneText, Does.Contain("displayName: GentlemanBlack"));
        Assert.That(sceneText, Does.Contain($"animatorController: {{fileID: 9100000, guid: {ReadGuidFromMeta(ButlerClassicControllerMetaPath)}, type: 2}}"), "ButlerClassic should use its dedicated directional-idle Animator controller.");
        Assert.That(sceneText, Does.Contain(ReadGuidFromMeta($"{ButlerClassicIdleFolder}/butler_classic_idle_right_01.png.meta")), "ButlerClassic should start on the right-facing idle sprite that matches the player's initial facing direction.");
        Assert.That(sceneText, Does.Contain("guid: badec0a2b39e42d9822349537505d13b"), "ButlerYoung should use its generated override controller.");
        Assert.That(sceneText, Does.Contain("guid: bfcadf76b04b4b9081d862b0afcd8024"), "GentlemanBlack should use its generated override controller.");
        Assert.That(sceneText, Does.Contain(ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_01_r01_c01.png.meta")), "GentlemanBlack should start on the directional front-facing sprite, not the old side-only frame.");
        Assert.That(sceneText, Does.Contain("showOnStart: 1"), "The selector should appear immediately in Play mode for testing.");

        Assert.That(clipText, Does.Contain("classID: 114"), "Generated clips should animate UI Images for room NPCs.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Generated clips should also animate SpriteRenderers for the controllable player selector.");
    }

    [Test]
    public void ButlerClassicHasFourDirectionalIdleStates()
    {
        string controllerText = File.ReadAllText(ButlerClassicControllerPath);
        string driverText = File.ReadAllText(CharacterAnimatorDriverPath);
        string downClipText = File.ReadAllText(ButlerClassicIdleDownClipPath);
        string leftClipText = File.ReadAllText(ButlerClassicIdleLeftClipPath);
        string rightClipText = File.ReadAllText(ButlerClassicIdleRightClipPath);
        string upClipText = File.ReadAllText(ButlerClassicIdleUpClipPath);

        Assert.That(Directory.GetFiles(ButlerClassicIdleFolder, "*.png").Length, Is.EqualTo(16), "ButlerClassic should have a four-frame idle cycle for each direction.");
        Assert.That(controllerText, Does.Contain("m_Name: ButlerClassic_Idle_Down"));
        Assert.That(controllerText, Does.Contain("m_Name: ButlerClassic_Idle_Left"));
        Assert.That(controllerText, Does.Contain("m_Name: ButlerClassic_Idle_Right"));
        Assert.That(controllerText, Does.Contain("m_Name: ButlerClassic_Idle_Up"));
        Assert.That(controllerText, Does.Contain("IsFacingDown"));
        Assert.That(controllerText, Does.Contain("IsFacingLeft"));
        Assert.That(controllerText, Does.Contain("IsFacingRight"));
        Assert.That(controllerText, Does.Contain("IsFacingUp"));
        Assert.That(controllerText, Does.Contain("m_DefaultState: {fileID: 1500000000000000003}"), "The ButlerClassic controller should begin facing right to match PointClickPlayerMovement.");

        Assert.That(driverText, Does.Contain("direction == CharacterWalkDirection.Up, hasFacingUp"));
        Assert.That(driverText, Does.Contain("direction == CharacterWalkDirection.Down, hasFacingDown"));
        Assert.That(driverText, Does.Contain("direction == CharacterWalkDirection.Left, hasFacingLeft"));
        Assert.That(driverText, Does.Contain("direction == CharacterWalkDirection.Right, hasFacingRight"));

        AssertDirectionalIdleClip(downClipText, "down");
        AssertDirectionalIdleClip(leftClipText, "left");
        AssertDirectionalIdleClip(rightClipText, "right");
        AssertDirectionalIdleClip(upClipText, "up");
    }

    [Test]
    public void GentlemanBlackUsesDirectionalAnimationRows()
    {
        string builderText = File.ReadAllText(CharacterAnimationAssetBuilderPath);
        string idleText = File.ReadAllText(GentlemanBlackIdleClipPath);
        string walkDownText = File.ReadAllText(GentlemanBlackWalkDownClipPath);
        string walkLeftText = File.ReadAllText(GentlemanBlackWalkLeftClipPath);
        string walkRightText = File.ReadAllText(GentlemanBlackWalkRightClipPath);
        string walkUpText = File.ReadAllText(GentlemanBlackWalkUpClipPath);

        Assert.That(builderText, Does.Contain("directional/aligned"), "Explicit directional fixes should survive a future animation-asset rebuild.");
        Assert.That(Directory.GetFiles(GentlemanBlackDirectionalFolder, "*.png").Length, Is.EqualTo(24), "GentlemanBlack should have down/up rows plus 8-frame left/right side cycles.");

        string downIdleGuid = ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_01_r01_c01.png.meta");
        Assert.That(idleText, Does.Contain(downIdleGuid));
        Assert.That(walkDownText, Does.Contain(downIdleGuid));
        Assert.That(walkDownText, Does.Contain(ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_04_r01_c04.png.meta")));
        Assert.That(walkDownText, Does.Contain("m_StopTime: 0.333333333"));

        Assert.That(walkLeftText, Does.Contain(ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_05_r02_c01.png.meta")), "Left should use mirrored GentlemanBlack frames.");
        Assert.That(walkLeftText, Does.Contain(ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_12_r02_c08.png.meta")), "Left should use the whole mirrored 8-frame cycle.");
        Assert.That(walkLeftText, Does.Contain("m_StopTime: 0.666666667"));

        Assert.That(walkRightText, Does.Contain(ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_13_r03_c01.png.meta")), "Right should use the original GentlemanBlack side cycle.");
        Assert.That(walkRightText, Does.Contain(ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_20_r03_c08.png.meta")), "Right should use the whole 8-frame cycle.");
        Assert.That(walkRightText, Does.Contain("m_StopTime: 0.666666667"));

        Assert.That(walkUpText, Does.Contain(ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_21_r04_c01.png.meta")));
        Assert.That(walkUpText, Does.Contain(ReadGuidFromMeta($"{GentlemanBlackDirectionalFolder}/gentleman_black_directional_24_r04_c04.png.meta")));
        Assert.That(walkUpText, Does.Contain("m_StopTime: 0.333333333"));

        Assert.That(walkDownText, Does.Not.Contain("9a7c7a52683e4f65b5517bf7d63557e7"), "Down should not be the old side-only walk-cycle frame.");
        Assert.That(walkLeftText, Does.Not.Contain("9a7c7a52683e4f65b5517bf7d63557e7"), "Left should not reuse the unmirrored right-facing frame.");
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

    private static string ReadGuidFromMeta(string metaPath)
    {
        Match match = Regex.Match(File.ReadAllText(metaPath), @"^guid: ([a-f0-9]{32})$", RegexOptions.Multiline);
        Assert.That(match.Success, Is.True, $"Could not find a Unity guid in {metaPath}.");
        return match.Groups[1].Value;
    }

    private static void AssertDirectionalIdleClip(string clipText, string direction)
    {
        Assert.That(clipText, Does.Contain("classID: 114"), "Directional idle clips should animate UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Directional idle clips should animate SpriteRenderers for the player.");
        Assert.That(clipText, Does.Contain("m_SampleRate: 4"), "Directional idle should breathe slowly, not step like a walk.");
        Assert.That(clipText, Does.Contain("m_StopTime: 1"), "Directional idle should loop over a full one-second breathing cycle.");

        for (int i = 1; i <= 4; i++)
        {
            string framePath = $"{ButlerClassicIdleFolder}/butler_classic_idle_{direction}_{i:00}.png.meta";
            Assert.That(clipText, Does.Contain(ReadGuidFromMeta(framePath)), $"Idle {direction} should include frame {i}.");
        }
    }
}
