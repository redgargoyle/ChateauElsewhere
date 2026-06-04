using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class CharacterRegressionTests
{
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string WalkerPath = "Assets/Scripts/Characters/RoomPersonWalker2D.cs";
    private const string Chapter1ArrivalControllerPath = "Assets/_Chateau/Scripts/Chapter/Chapter01/Chapter1ArrivalController.cs";
    private const string CharacterAnimatorDriverPath = "Assets/Scripts/Characters/CharacterAnimatorDriver.cs";
    private const string CharacterAnimationAssetBuilderPath = "Assets/Editor/CharacterAnimationAssetBuilder.cs";
    private const string CharactersReadmePath = "Assets/Scripts/Characters/README.md";
    private const string PlayerWalkUpClipPath = "Assets/Animation/Player/Player_Walk_Up.anim";
    private const string ButlerClassicControllerPath = "Assets/Animation/ButlerClassic/ButlerClassic.controller";
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
    private const string LadyDirectionalFolder = "Assets/Characters/Lady/walk/aligned";
    private const string LadyOverrideControllerMetaPath = "Assets/Animation/Lady/Lady.overrideController.meta";
    private const string LadyIdleClipPath = "Assets/Animation/Lady/Lady_Idle.anim";
    private const string LadyWalkDownClipPath = "Assets/Animation/Lady/Lady_Walk_Down.anim";
    private const string LadyWalkLeftClipPath = "Assets/Animation/Lady/Lady_Walk_Left.anim";
    private const string LadyWalkRightClipPath = "Assets/Animation/Lady/Lady_Walk_Right.anim";
    private const string LadyWalkUpClipPath = "Assets/Animation/Lady/Lady_Walk_Up.anim";
    private const string ButlerGuestSpriteMetaPath = "Assets/Art/Characters/butlersprite.png.meta";
    private const string ButlerGuestStandingSidePath = "Assets/Art/Characters/butler_guest_standing_arms_side_same_angle.png";
    private const string ButlerGuestStandingSideLeftPath = "Assets/Art/Characters/butler_guest_standing_arms_side_same_angle_left.png";
    private const string ButlerGuestOverrideControllerMetaPath = "Assets/Animation/ButlerGuest/ButlerGuest.overrideController.meta";
    private const string ButlerGuestIdleClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Idle.anim";
    private const string ButlerGuestWalkDownClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Walk_Down.anim";
    private const string ButlerGuestWalkLeftClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Walk_Left.anim";
    private const string ButlerGuestWalkRightClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Walk_Right.anim";
    private const string ButlerGuestWalkUpClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Walk_Up.anim";
    private const string MisterFlorianWalkFolder = "Assets/Characters/MisterFlorianKnell/walk/aligned";
    private const string MisterFlorianOverrideControllerPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell.overrideController";
    private const string MisterFlorianOverrideControllerMetaPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell.overrideController.meta";
    private const string MisterFlorianIdleClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle.anim";
    private const string MisterFlorianWalkDownClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Walk_Down.anim";
    private const string MisterFlorianWalkLeftClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Walk_Left.anim";
    private const string MisterFlorianWalkRightClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Walk_Right.anim";
    private const string MisterFlorianWalkUpClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Walk_Up.anim";
    private const string CountessWalkFolder = "Assets/Characters/CountessElowenDusk/walk/aligned";
    private const string CountessOverrideControllerMetaPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk.overrideController.meta";
    private const string CountessWalkDownClipPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Walk_Down.anim";
    private const string CountessWalkLeftClipPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Walk_Left.anim";
    private const string CountessWalkRightClipPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Walk_Right.anim";
    private const string CountessWalkUpClipPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Walk_Up.anim";
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
        Assert.That(animatorDriverText, Does.Contain("!Application.isPlaying && !animator.isInitialized"), "Edit-time validation must not query Animator parameters before Unity initializes the Animator.");
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
        Assert.That(readmeText, Does.Contain("prototype walking NPCs are currently disabled"));
    }

    [Test]
    public void FirstChapterGuestUsesLadyDirectionalAnimation()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string arrivalControllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string guestOneBlock = FindPrefabInstanceBlock(sceneText, "value: Guest 1");
        string guestTwoBlock = FindPrefabInstanceBlock(sceneText, "value: Guest 2");
        string firstLadyFrameGuid = ReadGuidFromMeta($"{LadyDirectionalFolder}/lady_walk_01_r01_c01.png.meta");
        string ladyControllerGuid = ReadGuidFromMeta(LadyOverrideControllerMetaPath);

        Assert.That(guestOneBlock, Is.Not.Null, "Guest 1 should remain a named scene prefab instance.");
        Assert.That(guestOneBlock, Does.Contain(firstLadyFrameGuid), "Guest 1 should preview with the root Lady frame instead of the player butler sprite.");
        Assert.That(guestOneBlock, Does.Contain(ladyControllerGuid), "Guest 1 should use the Lady override controller so the player-style direction booleans drive her frames.");
        Assert.That(guestTwoBlock, Is.Not.Null, "Guest 2 should remain available as the butler template for the first pair.");
        Assert.That(guestTwoBlock, Does.Not.Contain(ladyControllerGuid), "Guest 2 should not inherit the Lady override.");
        Assert.That(guestTwoBlock, Does.Not.Contain(firstLadyFrameGuid), "Guest 2 should not inherit the Lady preview sprite.");

        Assert.That(arrivalControllerText, Does.Contain("ShouldUseAuthoredLadyGuestAnimation"), "Runtime guest setup should have an explicit first-guest Lady exception.");
        Assert.That(arrivalControllerText, Does.Contain("index == 0 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[0])"), "Only the authored Guest 1 object should keep Lady animation.");
        Assert.That(arrivalControllerText, Does.Contain("for (int i = 1; i < ChapterGuestNameAliases.Length; i++)"), "Runtime guests should clone a non-Lady guest template before falling back to Guest 1.");
        Assert.That(arrivalControllerText, Does.Contain("guestAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController"), "Non-Lady guests should be forced back to the butler controller.");
        Assert.That(arrivalControllerText, Does.Contain("FindCharacterSpriteRenderer(guestObject)"), "Non-Lady guests should only reset the character sprite so coat sprites remain intact.");
        Assert.That(arrivalControllerText, Does.Contain("!IsCoatVisualTransform(renderer.transform)"), "Character sprite lookup should ignore coat renderers.");
        Assert.That(arrivalControllerText, Does.Not.Contain("guestRenderers[i].sprite = sourceRenderer.sprite"), "The butler reset must not overwrite every guest SpriteRenderer, or coats collapse to a tiny transparent corner.");

        AssertLadyFramesUseSingleSpriteImports();
        Assert.That(File.ReadAllText(LadyIdleClipPath), Does.Contain(firstLadyFrameGuid), "Lady idle should start on the same down-facing root Lady frame.");
        AssertClipUsesLadyRow(File.ReadAllText(LadyWalkDownClipPath), 1, "down");
        AssertClipUsesLadyRow(File.ReadAllText(LadyWalkLeftClipPath), 2, "left");
        AssertClipUsesLadyRow(File.ReadAllText(LadyWalkRightClipPath), 3, "right");
        AssertClipUsesLadyRow(File.ReadAllText(LadyWalkUpClipPath), 4, "up");
    }

    [Test]
    public void SecondChapterGuestUsesButlerSheetDirectionalAnimation()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string arrivalControllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string guestTwoBlock = FindPrefabInstanceBlock(sceneText, "value: Guest 2");
        string butlerSheetGuid = ReadGuidFromMeta(ButlerGuestSpriteMetaPath);
        string butlerControllerGuid = ReadGuidFromMeta(ButlerGuestOverrideControllerMetaPath);

        Assert.That(guestTwoBlock, Is.Not.Null, "Guest 2 should remain a named scene prefab instance.");
        Assert.That(guestTwoBlock, Does.Contain(butlerSheetGuid), "Guest 2 should preview with the new butler sheet instead of the player sprite.");
        Assert.That(guestTwoBlock, Does.Contain(butlerControllerGuid), "Guest 2 should use the ButlerGuest override controller.");

        Assert.That(arrivalControllerText, Does.Contain("ShouldUseAuthoredButlerGuestAnimation"), "Runtime guest setup should preserve Guest 2's authored butler animation.");
        Assert.That(arrivalControllerText, Does.Contain("index == 1 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[1])"), "Only the authored Guest 2 object should keep this butler animation.");

        Assert.That(File.ReadAllText(ButlerGuestIdleClipPath), Does.Contain("-8411666499919982919"), "Guest 2 idle should start on the forward-facing butler root frame.");
        Assert.That(File.ReadAllText(ButlerGuestSpriteMetaPath), Does.Contain("spritePixelsToUnits: 73.44827"), "Guest 2 butler sheet should import large enough to match Guest 1 Lady's visible height.");
        AssertClipUsesButlerSheetSprites(File.ReadAllText(ButlerGuestWalkDownClipPath), 0, 7, "forward");
        AssertClipUsesButlerSheetSideWalkWithStanding(File.ReadAllText(ButlerGuestWalkLeftClipPath), 8, 15, 12, ButlerGuestStandingSideLeftPath, "left");
        AssertClipUsesButlerSheetSideWalkWithStanding(File.ReadAllText(ButlerGuestWalkRightClipPath), 16, 23, 20, ButlerGuestStandingSidePath, "right");
        AssertClipUsesButlerSheetSprites(File.ReadAllText(ButlerGuestWalkUpClipPath), 24, 31, "away");
        AssertButlerGuestStandingSideFrame(ButlerGuestStandingSideLeftPath, 91, 199, "left");
        AssertButlerGuestStandingSideFrame(ButlerGuestStandingSidePath, 92, 200, "right");
    }

    [Test]
    public void ThirdChapterGuestUsesMisterFlorianDirectionalAnimation()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string arrivalControllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string misterFlorianOverrideControllerText = File.ReadAllText(MisterFlorianOverrideControllerPath);
        string guestThreeBlock = FindPrefabInstanceBlock(sceneText, "value: Guest 3");
        string firstMisterFlorianFrameGuid = ReadGuidFromMeta($"{MisterFlorianWalkFolder}/mister_florian_knell_walk_01_r01_c01.png.meta");
        string misterFlorianControllerGuid = ReadGuidFromMeta(MisterFlorianOverrideControllerMetaPath);
        string misterFlorianIdleClipGuid = ReadGuidFromMeta($"{MisterFlorianIdleClipPath}.meta");

        Assert.That(guestThreeBlock, Is.Not.Null, "Guest 3 should remain a named scene prefab instance.");
        Assert.That(guestThreeBlock, Does.Contain(firstMisterFlorianFrameGuid), "Guest 3 should preview with the forward-facing Mister Florian frame.");
        Assert.That(guestThreeBlock, Does.Contain(misterFlorianControllerGuid), "Guest 3 should use the Mister Florian override controller.");
        Assert.That(guestThreeBlock, Does.Contain("propertyPath: m_LocalScale.x"), "Guest 3 should keep the same root scale treatment as Guest 4.");

        Assert.That(arrivalControllerText, Does.Contain("ShouldUseAuthoredMisterFlorianGuestAnimation"), "Runtime guest setup should preserve Guest 3's authored Mister Florian animation.");
        Assert.That(arrivalControllerText, Does.Contain("index == 2 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[2])"), "Only the authored Guest 3 object should keep Mister Florian animation.");
        Assert.That(misterFlorianOverrideControllerText, Does.Contain(misterFlorianIdleClipGuid), "Mister Florian idle states should use the still idle clip.");
        Assert.That(misterFlorianOverrideControllerText, Does.Not.Contain(ReadGuidFromMeta("Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle_Down.anim.meta")), "Mister Florian should not wire the animated down idle sequence yet.");
        Assert.That(misterFlorianOverrideControllerText, Does.Not.Contain(ReadGuidFromMeta("Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle_Left.anim.meta")), "Mister Florian should not wire the animated left idle sequence yet.");
        Assert.That(misterFlorianOverrideControllerText, Does.Not.Contain(ReadGuidFromMeta("Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle_Right.anim.meta")), "Mister Florian should not wire the animated right idle sequence yet.");
        Assert.That(misterFlorianOverrideControllerText, Does.Not.Contain(ReadGuidFromMeta("Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle_Up.anim.meta")), "Mister Florian should not wire the animated up idle sequence yet.");

        AssertStillIdleClip(File.ReadAllText(MisterFlorianIdleClipPath), firstMisterFlorianFrameGuid, "Mister Florian");
        Assert.That(Directory.GetFiles(MisterFlorianWalkFolder, "mister_florian_knell_walk_*.png").Length, Is.EqualTo(28), "Mister Florian should keep seven generated walk frames for each of four directions.");
        AssertClipUsesMisterFlorianRow(File.ReadAllText(MisterFlorianWalkDownClipPath), 1, "down");
        AssertCustomSideWalkSequence(File.ReadAllText(MisterFlorianWalkLeftClipPath), MisterFlorianWalkFolder, "mister_florian_knell", "left", "Mister Florian");
        AssertCustomSideWalkSequence(File.ReadAllText(MisterFlorianWalkRightClipPath), MisterFlorianWalkFolder, "mister_florian_knell", "right", "Mister Florian");
        AssertClipUsesMisterFlorianRow(File.ReadAllText(MisterFlorianWalkUpClipPath), 4, "up");
    }

    [Test]
    public void FourthChapterGuestUsesCountessDirectionalAnimation()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string arrivalControllerText = File.ReadAllText(Chapter1ArrivalControllerPath);
        string guestFourBlock = FindPrefabInstanceBlock(sceneText, "value: Guest 4");
        string firstCountessFrameGuid = ReadGuidFromMeta($"{CountessWalkFolder}/countess_elowen_dusk_walk_01_r01_c01.png.meta");
        string countessControllerGuid = ReadGuidFromMeta(CountessOverrideControllerMetaPath);

        Assert.That(guestFourBlock, Is.Not.Null, "Guest 4 should remain a named scene prefab instance.");
        Assert.That(guestFourBlock, Does.Contain(firstCountessFrameGuid), "Guest 4 should preview with the forward-facing Countess frame.");
        Assert.That(guestFourBlock, Does.Contain(countessControllerGuid), "Guest 4 should use the Countess override controller.");

        Assert.That(arrivalControllerText, Does.Contain("ShouldUseAuthoredCountessGuestAnimation"), "Runtime guest setup should preserve Guest 4's authored Countess animation.");
        Assert.That(arrivalControllerText, Does.Contain("index == 3 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[3])"), "Only the authored Guest 4 object should keep Countess animation.");

        AssertClipUsesCountessRow(File.ReadAllText(CountessWalkDownClipPath), 1, "down");
        AssertClipUsesCountessRow(File.ReadAllText(CountessWalkLeftClipPath), 2, "left");
        AssertClipUsesCountessRow(File.ReadAllText(CountessWalkRightClipPath), 3, "right");
        AssertClipUsesCountessRow(File.ReadAllText(CountessWalkUpClipPath), 4, "up");
    }

    [Test]
    public void CustomMaleSideStandingFramesMatchWalkScale()
    {
        const string lordAmbroseWalkFolder = "Assets/Characters/LordAmbroseVeil/walk/aligned";

        AssertCustomSideStandingFrameMatchesWalkScale(MisterFlorianWalkFolder, "mister_florian_knell", "left", "Mister Florian");
        AssertCustomSideStandingFrameMatchesWalkScale(MisterFlorianWalkFolder, "mister_florian_knell", "right", "Mister Florian");
        AssertCustomSideStandingFrameMatchesWalkScale(lordAmbroseWalkFolder, "lord_ambrose_veil", "left", "Lord Ambrose Veil");
        AssertCustomSideStandingFrameMatchesWalkScale(lordAmbroseWalkFolder, "lord_ambrose_veil", "right", "Lord Ambrose Veil");
    }

    [Test]
    public void RemainingNamedGuestAnimationAssetsUseStillIdleAndDirectionalWalks()
    {
        AssertNamedGuestAnimationAssets("Baron Hector Glass", "BaronHectorGlass", "baron_hector_glass");
        AssertNamedGuestAnimationAssets("Lady Sabine Marrow", "LadySabineMarrow", "lady_sabine_marrow");
        AssertNamedGuestAnimationAssets("Lord Ambrose Veil", "LordAmbroseVeil", "lord_ambrose_veil");
        AssertNamedGuestAnimationAssets("Madame Coralie Thread", "MadameCoralieThread", "madame_coralie_thread");
        AssertNamedGuestAnimationAssets("Miss Isolde Wren", "MissIsoldeWren", "miss_isolde_wren");
        AssertNamedGuestAnimationAssets("Professor Lucien Vale", "ProfessorLucienVale", "professor_lucien_vale");
    }

    [Test]
    public void LaterChapterGuestsUseAuthoredNamedAnimations()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);
        string arrivalControllerText = File.ReadAllText(Chapter1ArrivalControllerPath);

        AssertSceneGuestUsesNamedAnimation(sceneText, 5, "Baron Hector Glass", "BaronHectorGlass", "baron_hector_glass");
        AssertSceneGuestUsesNamedAnimation(sceneText, 6, "Lady Sabine Marrow", "LadySabineMarrow", "lady_sabine_marrow");
        AssertSceneGuestUsesNamedAnimation(sceneText, 7, "Lord Ambrose Veil", "LordAmbroseVeil", "lord_ambrose_veil");
        AssertSceneGuestUsesNamedAnimation(sceneText, 8, "Madame Coralie Thread", "MadameCoralieThread", "madame_coralie_thread");

        Assert.That(arrivalControllerText, Does.Contain("ShouldUseAuthoredLaterGuestAnimation"), "Runtime guest setup should preserve the authored Guest 5-8 animations.");
        Assert.That(arrivalControllerText, Does.Contain("index >= 4 && index <= 7 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[index])"), "Only authored Guest 5-8 scene objects should keep their named animation.");
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

    private static string FindPrefabInstanceBlock(string sceneText, string marker)
    {
        foreach (Match match in Regex.Matches(sceneText, @"PrefabInstance:[\s\S]*?(?=\n--- !u!|\z)"))
        {
            if (match.Value.Contains(marker))
            {
                return match.Value;
            }
        }

        return null;
    }

    private static void AssertClipUsesLadyRow(string clipText, int row, string direction)
    {
        for (int column = 1; column <= 4; column++)
        {
            int frame = (row - 1) * 4 + column;
            string frameGuid = ReadGuidFromMeta($"{LadyDirectionalFolder}/lady_walk_{frame:00}_r{row:00}_c{column:00}.png.meta");
            Assert.That(clipText, Does.Contain(frameGuid), $"Lady walk {direction} should include frame row {row}, column {column}.");
        }
    }

    private static void AssertLadyFramesUseSingleSpriteImports()
    {
        for (int frame = 1; frame <= 16; frame++)
        {
            int row = ((frame - 1) / 4) + 1;
            int column = ((frame - 1) % 4) + 1;
            string metaText = File.ReadAllText($"{LadyDirectionalFolder}/lady_walk_{frame:00}_r{row:00}_c{column:00}.png.meta");

            Assert.That(metaText, Does.Contain("spriteMode: 1"), $"Lady frame {frame} should import as a single sprite for Animator clip references.");
            Assert.That(metaText, Does.Contain("alignment: 0"), $"Lady frame {frame} should use the same importer alignment as the butler frames.");
            Assert.That(metaText, Does.Contain("spritePivot: {x: 0.5, y: 0.0}"), $"Lady frame {frame} should serialize the same sprite pivot as the butler frames.");
        }
    }

    private static void AssertClipUsesButlerSheetSprites(string clipText, int firstSprite, int lastSprite, string direction)
    {
        for (int i = firstSprite; i <= lastSprite; i++)
        {
            string spriteFileId = ReadSpriteFileIdFromMeta(ButlerGuestSpriteMetaPath, $"butlersprite_{i}");
            Assert.That(clipText, Does.Contain(spriteFileId), $"Guest 2 walk {direction} should include butlersprite_{i}.");
        }

        Assert.That(clipText, Does.Contain("classID: 114"), "Guest 2 clips should animate UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Guest 2 clips should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(clipText, Does.Contain("m_StopTime: 0.666666667"), $"Guest 2 walk {direction} should play the full eight-frame row.");
    }

    private static void AssertClipUsesButlerSheetSideWalkWithStanding(string clipText, int firstSprite, int lastSprite, int replacedSprite, string standingFramePath, string direction)
    {
        for (int i = firstSprite; i <= lastSprite; i++)
        {
            string spriteFileId = ReadSpriteFileIdFromMeta(ButlerGuestSpriteMetaPath, $"butlersprite_{i}");
            if (i == replacedSprite)
            {
                Assert.That(clipText, Does.Not.Contain(spriteFileId), $"Guest 2 walk {direction} should replace butlersprite_{i} with a hands-at-side standing frame.");
                continue;
            }

            Assert.That(clipText, Does.Contain(spriteFileId), $"Guest 2 walk {direction} should keep butlersprite_{i}.");
        }

        string standingGuid = ReadGuidFromMeta($"{standingFramePath}.meta");
        string standingValue = $"{{fileID: 21300000, guid: {standingGuid}, type: 3}}";
        Assert.That(Regex.Matches(clipText, Regex.Escape(standingValue)).Count, Is.EqualTo(4), $"Guest 2 walk {direction} should use the standing side frame for both sprite keys and pointer mappings.");
        Assert.That(Regex.Matches(clipText, @"value: \{fileID: ").Count, Is.EqualTo(16), $"Guest 2 walk {direction} should keep eight sprite keys per binding.");
        Assert.That(Regex.Matches(clipText, @"^\s+- \{fileID: ", RegexOptions.Multiline).Count, Is.EqualTo(16), $"Guest 2 walk {direction} should keep eight pointer mappings per binding.");
        Assert.That(clipText, Does.Contain("classID: 114"), "Guest 2 clips should animate UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Guest 2 clips should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(clipText, Does.Contain("m_SampleRate: 12"), $"Guest 2 walk {direction} should keep its full-row walk timing.");
        Assert.That(clipText, Does.Contain("m_StopTime: 0.666666667"), $"Guest 2 walk {direction} should keep the full eight-frame row length.");
    }

    private static void AssertButlerGuestStandingSideFrame(string framePath, int expectedWidth, int expectedHeight, string direction)
    {
        string metaText = File.ReadAllText($"{framePath}.meta");
        RectInt visibleBounds = ReadVisibleSpriteBounds(framePath, expectedWidth, expectedHeight);

        Assert.That(visibleBounds.height, Is.GreaterThanOrEqualTo(195), $"Guest 2 {direction} standing side frame should match the walk-row height instead of using the small idle slice.");
        Assert.That(metaText, Does.Contain("spriteMode: 1"), $"Guest 2 {direction} standing side frame should import as a single sprite.");
        Assert.That(metaText, Does.Contain("spritePivot: {x: 0, y: 0}"), $"Guest 2 {direction} standing side frame should keep the butler sheet's bottom-left pivot behavior.");
        Assert.That(metaText, Does.Contain("spritePixelsToUnits: 73.44827"), $"Guest 2 {direction} standing side frame should keep the butler sheet pixels-per-unit.");
        Assert.That(metaText, Does.Contain("alphaIsTransparency: 1"), $"Guest 2 {direction} standing side frame should keep transparent sprite import behavior.");
        Assert.That(metaText, Does.Contain("filterMode: 1"), $"Guest 2 {direction} standing side frame should keep the butler sheet filter mode.");
    }

    private static void AssertClipUsesMisterFlorianRow(string clipText, int row, string direction)
    {
        for (int column = 1; column <= 7; column++)
        {
            string frameGuid = ReadGuidFromMeta($"{MisterFlorianWalkFolder}/mister_florian_knell_walk_{column:00}_r{row:00}_c{column:00}.png.meta");
            Assert.That(clipText, Does.Contain(frameGuid), $"Mister Florian walk {direction} should include frame row {row}, column {column}.");
        }

        Assert.That(clipText, Does.Contain("classID: 114"), "Mister Florian clips should animate UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Mister Florian clips should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(clipText, Does.Contain("m_StopTime: 0.583333333"), $"Mister Florian walk {direction} should play the full seven-frame row.");
    }

    private static void AssertStillIdleClip(string clipText, string frameGuid, string characterName)
    {
        string spriteKey = $"value: {{fileID: 21300000, guid: {frameGuid}, type: 3}}";
        string spriteMapping = $"- {{fileID: 21300000, guid: {frameGuid}, type: 3}}";

        Assert.That(clipText, Does.Contain("classID: 114"), $"{characterName} idle should bind UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), $"{characterName} idle should bind SpriteRenderers for prefab-stage reuse.");
        Assert.That(Regex.Matches(clipText, @"value: \{fileID: 21300000").Count, Is.EqualTo(2), $"{characterName} idle should have one sprite key each for Image and SpriteRenderer.");
        Assert.That(Regex.Matches(clipText, @"^\s+- \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}$", RegexOptions.Multiline).Count, Is.EqualTo(2), $"{characterName} idle should only keep two sprite pointer mappings.");
        Assert.That(Regex.Matches(clipText, Regex.Escape(spriteKey)).Count, Is.EqualTo(2), $"{characterName} idle should use the same forward-facing walk frame as the scene preview.");
        Assert.That(Regex.Matches(clipText, Regex.Escape(spriteMapping)).Count, Is.EqualTo(2), $"{characterName} idle should only map the still frame for both sprite bindings.");
        Assert.That(clipText, Does.Contain("m_StopTime: 0.083333333"), $"{characterName} idle should be a still one-frame clip, not a multi-frame idle sequence.");
    }

    private static void AssertSceneGuestUsesNamedAnimation(string sceneText, int guestNumber, string displayName, string assetName, string filePrefix)
    {
        string walkFolder = $"Assets/Characters/{assetName}/walk/aligned";
        string animationFolder = $"Assets/Animation/{assetName}";
        string guestBlock = FindPrefabInstanceBlock(sceneText, $"value: Guest {guestNumber}");
        string firstFrameGuid = ReadGuidFromMeta($"{walkFolder}/{filePrefix}_walk_01_r01_c01.png.meta");
        string controllerGuid = ReadGuidFromMeta($"{animationFolder}/{assetName}.overrideController.meta");

        Assert.That(guestBlock, Is.Not.Null, $"Guest {guestNumber} should exist as a named scene prefab instance for {displayName}.");
        Assert.That(guestBlock, Does.Contain(firstFrameGuid), $"Guest {guestNumber} should preview with the forward-facing {displayName} frame.");
        Assert.That(guestBlock, Does.Contain(controllerGuid), $"Guest {guestNumber} should use the {displayName} override controller.");
        Assert.That(guestBlock, Does.Contain("propertyPath: m_IsActive"), $"Guest {guestNumber} should keep the inactive scene-authored arrival setup.");
        Assert.That(guestBlock, Does.Contain("propertyPath: walkableFloor"), $"Guest {guestNumber} should keep the same walkable floor binding as earlier guests.");
        Assert.That(guestBlock, Does.Contain("objectReference: {fileID: 551531667}"), $"Guest {guestNumber} should use the same walkable floor object as earlier guests.");
        Assert.That(guestBlock, Does.Contain("propertyPath: m_LocalScale.x"), $"Guest {guestNumber} should keep the same root scale treatment as Guest 3 and Guest 4.");
        Assert.That(guestBlock, Does.Contain("m_AddedGameObjects:"), $"Guest {guestNumber} should keep the carried-coat child visual setup.");
    }

    private static void AssertNamedGuestAnimationAssets(string displayName, string assetName, string filePrefix)
    {
        string walkFolder = $"Assets/Characters/{assetName}/walk/aligned";
        string animationFolder = $"Assets/Animation/{assetName}";
        string overrideText = File.ReadAllText($"{animationFolder}/{assetName}.overrideController");
        string firstFrameGuid = ReadGuidFromMeta($"{walkFolder}/{filePrefix}_walk_01_r01_c01.png.meta");
        string idleClipGuid = ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle.anim.meta");

        Assert.That(Directory.GetFiles(walkFolder, $"{filePrefix}_walk_*.png").Length, Is.EqualTo(32), $"{displayName} should keep eight generated walk frames for each of four directions.");
        Assert.That(overrideText, Does.Contain(idleClipGuid), $"{displayName} override controller should wire generic idle states to the still idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Down.anim.meta")), $"{displayName} should not wire the animated down idle sequence yet.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Left.anim.meta")), $"{displayName} should not wire the animated left idle sequence yet.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Right.anim.meta")), $"{displayName} should not wire the animated right idle sequence yet.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Up.anim.meta")), $"{displayName} should not wire the animated up idle sequence yet.");

        AssertStillIdleClip(File.ReadAllText($"{animationFolder}/{assetName}_Idle.anim"), firstFrameGuid, displayName);
        AssertNamedGuestWalkRow(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Down.anim"), walkFolder, filePrefix, 1, "down", displayName);
        if (UsesCustomSideWalk(assetName))
        {
            AssertCustomSideWalkSequence(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Left.anim"), walkFolder, filePrefix, "left", displayName);
            AssertCustomSideWalkSequence(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Right.anim"), walkFolder, filePrefix, "right", displayName);
        }
        else
        {
            AssertNamedGuestWalkRow(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Left.anim"), walkFolder, filePrefix, 2, "left", displayName);
            AssertNamedGuestWalkRow(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Right.anim"), walkFolder, filePrefix, 3, "right", displayName);
        }
        AssertNamedGuestWalkRow(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Up.anim"), walkFolder, filePrefix, 4, "up", displayName);
    }

    private static bool UsesCustomSideWalk(string assetName)
    {
        return assetName == "BaronHectorGlass"
            || assetName == "LordAmbroseVeil"
            || assetName == "ProfessorLucienVale";
    }

    private static void AssertCustomSideWalkSequence(string clipText, string walkFolder, string filePrefix, string direction, string displayName)
    {
        bool isLeft = direction == "left";
        int row = isLeft ? 2 : 3;
        string firstGuid = ReadGuidFromMeta($"{walkFolder}/{filePrefix}_walk_02_r{row:00}_c02.png.meta");
        string secondGuid = ReadGuidFromMeta($"{walkFolder}/{filePrefix}_walk_01_r{row:00}_c01.png.meta");
        string standingName = $"{filePrefix}_standing_arms_side_same_angle{(isLeft ? "_left" : string.Empty)}";
        string standingMetaPath = $"{walkFolder}/{standingName}.png.meta";
        string standingGuid = ReadGuidFromMeta(standingMetaPath);
        string standingFileId = ReadSpriteFileIdFromMeta(standingMetaPath, $"{standingName}_0");
        string firstValue = $"value: {{fileID: 21300000, guid: {firstGuid}, type: 3}}";
        string secondValue = $"value: {{fileID: 21300000, guid: {secondGuid}, type: 3}}";
        string standingValue = $"value: {{fileID: {standingFileId}, guid: {standingGuid}, type: 3}}";
        string sequence =
            $"- time: 0\n      {firstValue}\n" +
            $"    - time: 0.1\n      {secondValue}\n" +
            $"    - time: 0.2\n      {standingValue}\n" +
            $"    - time: 0.3\n      {firstValue}";

        Assert.That(File.Exists($"{walkFolder}/{standingName}.png"), Is.True, $"{displayName} {direction} custom standing frame should exist.");
        Assert.That(Regex.Matches(clipText, Regex.Escape(sequence)).Count, Is.EqualTo(2), $"{displayName} {direction} walk should mirror the custom four-key side sequence for Image and SpriteRenderer.");
        Assert.That(Regex.Matches(clipText, @"value: \{fileID: ").Count, Is.EqualTo(8), $"{displayName} {direction} walk should have four sprite keys per binding.");
        Assert.That(Regex.Matches(clipText, @"^\s+- \{fileID: ", RegexOptions.Multiline).Count, Is.EqualTo(8), $"{displayName} {direction} walk should have four pointer mappings per binding.");
        Assert.That(clipText, Does.Contain("classID: 114"), $"{displayName} {direction} walk should animate UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), $"{displayName} {direction} walk should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(clipText, Does.Contain("m_SampleRate: 10"), $"{displayName} {direction} walk should keep the custom side-walk timing.");
        Assert.That(clipText, Does.Contain("m_StopTime: 0.4"), $"{displayName} {direction} walk should keep the custom four-key side-walk length.");
    }

    private static void AssertNamedGuestWalkRow(string clipText, string walkFolder, string filePrefix, int row, string direction, string displayName)
    {
        for (int column = 1; column <= 8; column++)
        {
            string frameGuid = ReadGuidFromMeta($"{walkFolder}/{filePrefix}_walk_{column:00}_r{row:00}_c{column:00}.png.meta");
            Assert.That(clipText, Does.Contain(frameGuid), $"{displayName} walk {direction} should include frame row {row}, column {column}.");
        }

        Assert.That(clipText, Does.Contain("classID: 114"), $"{displayName} walk {direction} should animate UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), $"{displayName} walk {direction} should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(clipText, Does.Contain("m_StopTime: 0.666666667"), $"{displayName} walk {direction} should play the full eight-frame row.");
    }

    private static void AssertClipUsesCountessRow(string clipText, int row, string direction)
    {
        for (int column = 1; column <= 8; column++)
        {
            string frameGuid = ReadGuidFromMeta($"{CountessWalkFolder}/countess_elowen_dusk_walk_{column:00}_r{row:00}_c{column:00}.png.meta");
            Assert.That(clipText, Does.Contain(frameGuid), $"Countess walk {direction} should include frame row {row}, column {column}.");
        }

        Assert.That(clipText, Does.Contain("classID: 114"), "Countess clips should animate UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Countess clips should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(clipText, Does.Contain("m_StopTime: 0.666666667"), $"Countess walk {direction} should play the full eight-frame row.");
    }

    private static string ReadSpriteFileIdFromMeta(string metaPath, string spriteName)
    {
        string metaText = File.ReadAllText(metaPath);
        Match match = Regex.Match(metaText, $@"^\s+{Regex.Escape(spriteName)}: (-?\d+)$", RegexOptions.Multiline);
        if (!match.Success && Regex.IsMatch(metaText, @"^\s+spriteMode: 1$", RegexOptions.Multiline))
        {
            return "21300000";
        }

        Assert.That(match.Success, Is.True, $"Could not find sprite fileID for {spriteName} in {metaPath}.");
        return match.Groups[1].Value;
    }

    private static void AssertCustomSideStandingFrameMatchesWalkScale(string walkFolder, string filePrefix, string direction, string displayName)
    {
        bool isLeft = direction == "left";
        int row = isLeft ? 2 : 3;
        RectInt firstWalkBounds = ReadVisibleSpriteBounds($"{walkFolder}/{filePrefix}_walk_02_r{row:00}_c02.png");
        RectInt secondWalkBounds = ReadVisibleSpriteBounds($"{walkFolder}/{filePrefix}_walk_01_r{row:00}_c01.png");
        RectInt standingBounds = ReadVisibleSpriteBounds($"{walkFolder}/{filePrefix}_standing_arms_side_same_angle{(isLeft ? "_left" : string.Empty)}.png");
        int expectedHeight = Math.Min(firstWalkBounds.height, secondWalkBounds.height);
        int expectedBaseline = Math.Max(firstWalkBounds.yMin, secondWalkBounds.yMin);

        Assert.That(standingBounds.height, Is.GreaterThanOrEqualTo(expectedHeight - 4), $"{displayName} {direction} standing frame should match the walk-frame visible height instead of shrinking.");
        Assert.That(standingBounds.yMin, Is.InRange(expectedBaseline - 2, expectedBaseline + 2), $"{displayName} {direction} standing frame should keep the same foot baseline as the walk frames.");
    }

    private static RectInt ReadVisibleSpriteBounds(string imagePath, int expectedWidth = 166, int expectedHeight = 297)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(imagePath)), Is.True, $"Could not load PNG sprite at {imagePath}.");
            Assert.That(texture.width, Is.EqualTo(expectedWidth), $"{imagePath} should keep its expected sprite canvas width.");
            Assert.That(texture.height, Is.EqualTo(expectedHeight), $"{imagePath} should keep its expected sprite canvas height.");

            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[(y * texture.width) + x].a == 0)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            Assert.That(maxX, Is.GreaterThanOrEqualTo(0), $"{imagePath} should have visible sprite pixels.");
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
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
