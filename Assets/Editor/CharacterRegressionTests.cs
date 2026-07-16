using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    private const string CharacterArtRoot = "Assets/Art/Characters";
    private const string PlayerWalkUpClipPath = "Assets/Animation/Player/Player_Walk_Up.anim";
    private const string ButlerClassicControllerPath = "Assets/Animation/ButlerClassic/ButlerClassic.controller";
    private const string ButlerClassicIdleFolder = "Assets/Art/Characters/butler";
    private const string ButlerClassicIdleDownClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Down.anim";
    private const string ButlerClassicIdleLeftClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Left.anim";
    private const string ButlerClassicIdleRightClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Right.anim";
    private const string ButlerClassicIdleUpClipPath = "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Up.anim";
    private const string GentlemanBlackDirectionalFolder = "Assets/Art/Library/LegacyCharacters/GentlemanBlack/directional/aligned";
    private const string GentlemanBlackIdleClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Idle.anim";
    private const string GentlemanBlackWalkDownClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Walk_Down.anim";
    private const string GentlemanBlackWalkLeftClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Walk_Left.anim";
    private const string GentlemanBlackWalkRightClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Walk_Right.anim";
    private const string GentlemanBlackWalkUpClipPath = "Assets/Animation/GentlemanBlack/GentlemanBlack_Walk_Up.anim";
    private const string LadyDirectionalFolder = "Assets/Art/Characters/guest1";
    private const string LadyIdleNeutralFramePath = "Assets/Art/Characters/guest1/lady_walk_01_r01_c01.png";
    private const string LadyIdleShiftRightPath = "Assets/Art/Characters/guest1/lady_idle_shift_right.png";
    private const string LadyIdleShiftLeftPath = "Assets/Art/Characters/guest1/lady_idle_shift_left.png";
    private const string LadySittingFramePrefix = "Assets/Art/Characters/guest1/lady_sitting";
    private const string LadyOverrideControllerPath = "Assets/Animation/Lady/Lady.overrideController";
    private const string LadyOverrideControllerMetaPath = "Assets/Animation/Lady/Lady.overrideController.meta";
    private const string LadyIdleClipPath = "Assets/Animation/Lady/Lady_Idle.anim";
    private const string LadySittingClipPath = "Assets/Animation/Lady/Lady_Sitting.anim";
    private const string LadyWalkDownClipPath = "Assets/Animation/Lady/Lady_Walk_Down.anim";
    private const string LadyWalkLeftClipPath = "Assets/Animation/Lady/Lady_Walk_Left.anim";
    private const string LadyWalkRightClipPath = "Assets/Animation/Lady/Lady_Walk_Right.anim";
    private const string LadyWalkUpClipPath = "Assets/Animation/Lady/Lady_Walk_Up.anim";
    private const string ButlerGuestSpriteMetaPath = "Assets/Art/Characters/guest2/butlersprite.png.meta";
    private const string ButlerGuestStandingSidePath = "Assets/Art/Characters/guest2/butler_guest_standing_arms_side_same_angle.png";
    private const string ButlerGuestStandingSideLeftPath = "Assets/Art/Characters/guest2/butler_guest_standing_arms_side_same_angle_left.png";
    private const string ButlerGuestIdleFramePrefix = "Assets/Art/Characters/guest2/butler_guest_idle_down";
    private const string ButlerGuestSittingSpriteMetaPath = "Assets/Art/Characters/guest2/butlerspritesit.png.meta";
    private const string ButlerGuestOverrideControllerPath = "Assets/Animation/ButlerGuest/ButlerGuest.overrideController";
    private const string ButlerGuestOverrideControllerMetaPath = "Assets/Animation/ButlerGuest/ButlerGuest.overrideController.meta";
    private const string ButlerGuestIdleClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Idle.anim";
    private const string ButlerGuestSittingClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Sitting.anim";
    private const string ButlerGuestWalkDownClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Walk_Down.anim";
    private const string ButlerGuestWalkLeftClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Walk_Left.anim";
    private const string ButlerGuestWalkRightClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Walk_Right.anim";
    private const string ButlerGuestWalkUpClipPath = "Assets/Animation/ButlerGuest/ButlerGuest_Walk_Up.anim";
    private const string MisterFlorianWalkFolder = "Assets/Art/Characters/guest3";
    private const string MisterFlorianOverrideControllerPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell.overrideController";
    private const string MisterFlorianOverrideControllerMetaPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell.overrideController.meta";
    private const string MisterFlorianIdleClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle.anim";
    private const string MisterFlorianWalkDownClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Walk_Down.anim";
    private const string MisterFlorianWalkLeftClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Walk_Left.anim";
    private const string MisterFlorianWalkRightClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Walk_Right.anim";
    private const string MisterFlorianWalkUpClipPath = "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Walk_Up.anim";
    private const string CountessWalkFolder = "Assets/Art/Characters/guest4";
    private const string CountessOverrideControllerMetaPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk.overrideController.meta";
    private const string CountessWalkDownClipPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Walk_Down.anim";
    private const string CountessWalkLeftClipPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Walk_Left.anim";
    private const string CountessWalkRightClipPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Walk_Right.anim";
    private const string CountessWalkUpClipPath = "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Walk_Up.anim";
    private const string AnimationFolder = "Assets/Animation";

    [Test]
    public void RoomPeopleAreEditableAnimatedSceneObjects()
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
        Assert.That(walkerText, Does.Contain("Mathf.InverseLerp(nearY, farY"), "Walkers should retain front-to-back tint depth.");
        Assert.That(walkerText, Does.Not.Contain("rectTransform.localScale"), "Walker movement and tint must not resize the character card.");
        Assert.That(walkerText, Does.Contain("ApplyPresentationFacing"), "Walker facing should remain a presentation concern without changing body size.");
        Assert.That(walkerText, Does.Contain("facingVisual.localRotation"), "UI walker mirroring should use presentation rotation instead of scale magnitude.");
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
        AssertCharacterArtRootIsGuestAndButlerFoldersOnly();
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
        string ladySittingClipGuid = ReadGuidFromMeta($"{LadySittingClipPath}.meta");
        string ladyOverrideControllerText = File.ReadAllText(LadyOverrideControllerPath);

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

        Assert.That(ladyOverrideControllerText, Does.Match($@"(?s)m_OriginalClip: \{{fileID: 7400000, guid: ae2b75cd2fa12a2a990986dc14eee676, type: 2\}}\s+m_OverrideClip: \{{fileID: 7400000, guid: {ladySittingClipGuid}, type: 2\}}"), "Guest 1 should use her sitting loop when ActorRoomState drives the shared crouch/seated animator state.");
        AssertLadyFramesUseSingleSpriteImports();
        AssertLadySubtleShiftIdleClip(File.ReadAllText(LadyIdleClipPath));
        AssertLadySittingClip(File.ReadAllText(LadySittingClipPath));
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
        string butlerSheetMetaText = File.ReadAllText(ButlerGuestSpriteMetaPath);
        string butlerControllerGuid = ReadGuidFromMeta(ButlerGuestOverrideControllerMetaPath);
        string butlerSittingClipGuid = ReadGuidFromMeta($"{ButlerGuestSittingClipPath}.meta");
        string butlerOverrideControllerText = File.ReadAllText(ButlerGuestOverrideControllerPath);

        Assert.That(guestTwoBlock, Is.Not.Null, "Guest 2 should remain a named scene prefab instance.");
        Assert.That(guestTwoBlock, Does.Contain(butlerSheetGuid), "Guest 2 should preview with the new butler sheet instead of the player sprite.");
        Assert.That(guestTwoBlock, Does.Contain(butlerControllerGuid), "Guest 2 should use the ButlerGuest override controller.");

        Assert.That(arrivalControllerText, Does.Contain("ShouldUseAuthoredButlerGuestAnimation"), "Runtime guest setup should preserve Guest 2's authored butler animation.");
        Assert.That(arrivalControllerText, Does.Contain("index == 1 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[1])"), "Only the authored Guest 2 object should keep this butler animation.");
        Assert.That(butlerOverrideControllerText, Does.Match($@"(?s)m_OriginalClip: \{{fileID: 7400000, guid: ae2b75cd2fa12a2a990986dc14eee676, type: 2\}}\s+m_OverrideClip: \{{fileID: 7400000, guid: {butlerSittingClipGuid}, type: 2\}}"), "Guest 2 should use his sitting loop when ActorRoomState drives the shared crouch/seated animator state.");

        AssertButlerGuestIdleClipUsesStandaloneFrames(File.ReadAllText(ButlerGuestIdleClipPath));
        AssertButlerGuestSittingClip(File.ReadAllText(ButlerGuestSittingClipPath));
        Assert.That(butlerSheetMetaText, Does.Contain("spritePixelsToUnits: 73.44827"), "Guest 2 butler sheet should import large enough to match Guest 1 Lady's visible height.");
        Assert.That(butlerSheetMetaText, Does.Contain("second: butlersprite_0"), "Guest 2 sheet slices should keep the stable names used by animation clips.");
        Assert.That(butlerSheetMetaText, Does.Contain("second: butlersprite_44"), "Guest 2 sheet slices should keep the full stable slice table.");
        Assert.That(butlerSheetMetaText, Does.Not.Contain("butlersprite 1_"), "Replacing the sheet should not leave Unity-regenerated copy suffix sprite names.");
        AssertButlerGuestExportedFramesMatchSheetSlices(butlerSheetMetaText);
        AssertClipUsesButlerExportedFrames(File.ReadAllText(ButlerGuestWalkDownClipPath), Enumerable.Range(0, 8), "forward", 16, 16, "12", "0.666666667", true);
        AssertButlerGuestSideWalkClipsUseFullScaleFrames(File.ReadAllText(ButlerGuestWalkLeftClipPath), File.ReadAllText(ButlerGuestWalkRightClipPath));
        AssertClipUsesButlerExportedFrames(File.ReadAllText(ButlerGuestWalkUpClipPath), Enumerable.Range(24, 8), "away", 16, 16, "12", "0.666666667", true);
        AssertButlerGuestStandingSideFrame(ButlerGuestStandingSideLeftPath, 91, 199, "left");
        AssertButlerGuestStandingSideFrame(ButlerGuestStandingSidePath, 91, 199, "right");
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
        Assert.That(sceneText, Does.Not.Contain("m_EditorClassIdentifier: Assembly-CSharp::RoomProjectedEntity"), "Gameplay's authored Player/Guest scene instances should not carry RoomProjectedEntity components; ActorRoomState and PointClickPlayerMovement retain room-stage positioning without character-size ownership.");

        Assert.That(arrivalControllerText, Does.Contain("ShouldUseAuthoredMisterFlorianGuestAnimation"), "Runtime guest setup should preserve Guest 3's authored Mister Florian animation.");
        Assert.That(arrivalControllerText, Does.Contain("index == 2 && MatchesSceneGuestName(guestObject, ChapterGuestNameAliases[2])"), "Only the authored Guest 3 object should keep Mister Florian animation.");
        Assert.That(misterFlorianOverrideControllerText, Does.Contain(misterFlorianIdleClipGuid), "Mister Florian idle states should use the main forward idle clip.");
        Assert.That(misterFlorianOverrideControllerText, Does.Not.Contain(ReadGuidFromMeta("Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle_Down.anim.meta")), "Mister Florian should not wire the separate directional down idle clip.");
        Assert.That(misterFlorianOverrideControllerText, Does.Not.Contain(ReadGuidFromMeta("Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle_Left.anim.meta")), "Mister Florian should not wire the directional left idle clip.");
        Assert.That(misterFlorianOverrideControllerText, Does.Not.Contain(ReadGuidFromMeta("Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle_Right.anim.meta")), "Mister Florian should not wire the directional right idle clip.");
        Assert.That(misterFlorianOverrideControllerText, Does.Not.Contain(ReadGuidFromMeta("Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Idle_Up.anim.meta")), "Mister Florian should not wire the directional up idle clip.");

        AssertForwardIdleClip(File.ReadAllText(MisterFlorianIdleClipPath), "Assets/Art/Characters/guest3/mister_florian_knell_idle_down", "Mister Florian", 166, 297, "100", "0.5");
        AssertForwardIdleMatchesWalkScale($"{MisterFlorianWalkFolder}/mister_florian_knell_walk_01_r01_c01.png", "Assets/Art/Characters/guest3/mister_florian_knell_idle_down", "Mister Florian");
        Assert.That(Directory.GetFiles(MisterFlorianWalkFolder, "mister_florian_knell_walk_*.png").Length, Is.EqualTo(28), "Guest 3 should keep the full June 3 Mister Florian source walk frame set in the organized guest folder.");
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

        AssertForwardIdleClip(File.ReadAllText("Assets/Animation/CountessElowenDusk/CountessElowenDusk_Idle.anim"), "Assets/Art/Characters/guest4/countess_elowen_dusk_idle_down", "Countess", 166, 297, "100", "0.5");
        AssertForwardIdleMatchesWalkScale($"{CountessWalkFolder}/countess_elowen_dusk_walk_01_r01_c01.png", "Assets/Art/Characters/guest4/countess_elowen_dusk_idle_down", "Countess");
        AssertClipUsesCountessRow(File.ReadAllText(CountessWalkDownClipPath), 1, "down");
        AssertClipUsesCountessRow(File.ReadAllText(CountessWalkLeftClipPath), 2, "left");
        AssertClipUsesCountessRow(File.ReadAllText(CountessWalkRightClipPath), 3, "right");
        AssertClipUsesCountessRow(File.ReadAllText(CountessWalkUpClipPath), 4, "up");
    }

    [Test]
    public void CustomMaleSideStandingFramesMatchWalkScale()
    {
        const string baronHectorWalkFolder = "Assets/Art/Characters/guest5";
        const string lordAmbroseWalkFolder = "Assets/Art/Characters/guest7";

        AssertCustomSideStandingFrameMatchesWalkScale(MisterFlorianWalkFolder, "mister_florian_knell", "left", "Mister Florian");
        AssertCustomSideStandingFrameMatchesWalkScale(MisterFlorianWalkFolder, "mister_florian_knell", "right", "Mister Florian");
        AssertCustomSideStandingFrameMatchesWalkScale(baronHectorWalkFolder, "baron_hector_glass", "left", "Baron Hector");
        AssertCustomSideStandingFrameMatchesWalkScale(baronHectorWalkFolder, "baron_hector_glass", "right", "Baron Hector");
        AssertGuestPair02ManWalkSpritesExist(lordAmbroseWalkFolder);
    }

    [Test]
    public void NamedGuestAnimationAssetsUseExpectedIdleAndDirectionalWalks()
    {
        AssertNamedGuestAnimationAssets("Baron Hector Glass", "BaronHectorGlass", "baron_hector_glass", true);
        AssertNamedGuestAnimationAssets("Lady Sabine Marrow", "LadySabineMarrow", "lady_sabine_marrow", true);
        AssertNamedGuestAnimationAssets("Lord Ambrose Veil", "LordAmbroseVeil", "lord_ambrose_veil", true);
        AssertNamedGuestAnimationAssets("Madame Coralie Thread", "MadameCoralieThread", "madame_coralie_thread", true);
        AssertNamedGuestAnimationAssets("Miss Isolde Wren", "MissIsoldeWren", "miss_isolde_wren", false);
        AssertNamedGuestAnimationAssets("Professor Lucien Vale", "ProfessorLucienVale", "professor_lucien_vale", false);
        AssertForwardIdleMatchesWalkScale("Assets/Art/Characters/guest6/lady_sabine_marrow_walk_01_r01_c01.png", "Assets/Art/Characters/guest6/lady_sabine_marrow_idle_down", "Lady Sabine Marrow");
        AssertGuestPair02ManStandingIdleFramesAreValid();
        AssertForwardIdleMatchesWalkScale("Assets/Art/Characters/guest8/madame_coralie_thread_walk_01_r01_c01.png", "Assets/Art/Characters/guest8/madame_coralie_thread_idle_down", "Madame Coralie Thread");
    }

    [Test]
    public void LaterGuestEntranceStandingIdlesUseComparableWorldHeight()
    {
        AssertStandingIdleSequenceWorldHeight(
            "Baron Hector Glass",
            $"{CharacterArtRoot}/guest5/guest5standidle",
            "baron_hector_glass_idle_down",
            166,
            297);
        AssertStandingIdleSequenceWorldHeight(
            "Lord Ambrose Veil",
            $"{CharacterArtRoot}/guest7/guest7standidle",
            "GuestPair02Man_standing_idle",
            248,
            304);
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

        Assert.That(Directory.GetFiles(ButlerClassicIdleFolder, "butler_classic_idle_*.png").Length, Is.EqualTo(8), "ButlerClassic should keep the unique idle breathe frames while reusing duplicate neutral walk frames.");
        Assert.That(Directory.GetFiles(ButlerClassicIdleFolder, "butler_classic_walk_*.png").Length, Is.EqualTo(16), "ButlerClassic should keep the canonical directional walk frames.");
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
        metaPath = ResolveConsolidatedSpriteMetaPath(metaPath);
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

    private static void AssertLadySubtleShiftIdleClip(string clipText)
    {
        string neutralGuid = ReadGuidFromMeta($"{LadyIdleNeutralFramePath}.meta");
        string shiftRightGuid = ReadGuidFromMeta($"{LadyIdleShiftRightPath}.meta");
        string shiftLeftGuid = ReadGuidFromMeta($"{LadyIdleShiftLeftPath}.meta");
        string neutralReference = $"{{fileID: 21300000, guid: {neutralGuid}, type: 3}}";
        string shiftRightReference = $"{{fileID: 21300000, guid: {shiftRightGuid}, type: 3}}";
        string shiftLeftReference = $"{{fileID: 21300000, guid: {shiftLeftGuid}, type: 3}}";
        RectInt neutralBounds = ReadVisibleSpriteBounds(LadyIdleNeutralFramePath, 180, 290);
        RectInt shiftRightBounds = ReadVisibleSpriteBounds(LadyIdleShiftRightPath, 180, 290);
        RectInt shiftLeftBounds = ReadVisibleSpriteBounds(LadyIdleShiftLeftPath, 180, 290);

        Assert.That(clipText, Does.Contain("classID: 114"), "Lady idle should bind UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Lady idle should bind SpriteRenderers for prefab-stage reuse.");
        Assert.That(Regex.Matches(clipText, @"value: \{fileID: 21300000").Count, Is.EqualTo(8), "Lady idle should have four sprite keys for Image and four for SpriteRenderer.");
        Assert.That(Regex.Matches(clipText, @"^\s+- \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}$", RegexOptions.Multiline).Count, Is.EqualTo(8), "Lady idle should keep four pointer mappings per binding.");
        Assert.That(clipText, Does.Contain("m_SampleRate: 4"), "Lady idle should shift slowly, not step like a walk.");
        Assert.That(clipText, Does.Contain("m_StopTime: 1"), "Lady idle should loop over a full one-second cycle.");
        Assert.That(clipText, Does.Contain("m_LoopTime: 1"), "Lady idle should loop.");
        Assert.That(clipText, Does.Contain("time: 0.25"));
        Assert.That(clipText, Does.Contain("time: 0.75"));
        Assert.That(clipText, Does.Not.Contain("2efcc528b4bb42d0b0b0bb79702d77b0"), "Lady idle should not reference the deleted old idle frame 02.");
        Assert.That(clipText, Does.Not.Contain("48ee9944f96844c888069f7d0eae2ead"), "Lady idle should not reference the deleted old idle frame 03.");
        Assert.That(Regex.Matches(clipText, Regex.Escape(neutralReference)).Count, Is.EqualTo(8), "Lady idle should return to the loaded neutral frame twice per binding.");
        Assert.That(Regex.Matches(clipText, Regex.Escape(shiftRightReference)).Count, Is.EqualTo(4), "Lady idle should include the subtle right-shift frame.");
        Assert.That(Regex.Matches(clipText, Regex.Escape(shiftLeftReference)).Count, Is.EqualTo(4), "Lady idle should include the subtle left-shift frame.");

        AssertLadyShiftFrameImport(LadyIdleShiftRightPath, "right");
        AssertLadyShiftFrameImport(LadyIdleShiftLeftPath, "left");
        Assert.That(shiftRightBounds.yMin, Is.EqualTo(neutralBounds.yMin), "Lady right-shift idle should keep the same foot baseline.");
        Assert.That(shiftLeftBounds.yMin, Is.EqualTo(neutralBounds.yMin), "Lady left-shift idle should keep the same foot baseline.");
        Assert.That(shiftRightBounds.height, Is.EqualTo(neutralBounds.height), "Lady right-shift idle should keep the same visible height.");
        Assert.That(shiftLeftBounds.height, Is.EqualTo(neutralBounds.height), "Lady left-shift idle should keep the same visible height.");
    }

    private static void AssertLadyShiftFrameImport(string framePath, string direction)
    {
        string metaText = File.ReadAllText($"{framePath}.meta");

        Assert.That(metaText, Does.Contain("spriteMode: 1"), $"Lady idle shift {direction} should import as a single sprite.");
        Assert.That(metaText, Does.Contain("spritePixelsToUnits: 100"), $"Lady idle shift {direction} should keep the standing Lady pixels-per-unit.");
        Assert.That(metaText, Does.Contain("spritePivot: {x: 0.5, y: 0"), $"Lady idle shift {direction} should keep the bottom-center pivot.");
        Assert.That(metaText, Does.Contain("alphaIsTransparency: 1"), $"Lady idle shift {direction} should keep transparent sprite import behavior.");
        Assert.That(metaText, Does.Contain("filterMode: 1"), $"Lady idle shift {direction} should keep point filtering.");
    }

    private static void AssertLadySittingClip(string clipText)
    {
        Assert.That(clipText, Does.Contain("classID: 114"), "Lady sitting should bind UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Lady sitting should bind SpriteRenderers for prefab-stage reuse.");
        Assert.That(Regex.Matches(clipText, @"value: \{fileID: 21300000").Count, Is.EqualTo(8), "Lady sitting should have four sprite keys for Image and four for SpriteRenderer.");
        Assert.That(Regex.Matches(clipText, @"^\s+- \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}$", RegexOptions.Multiline).Count, Is.EqualTo(8), "Lady sitting should keep four pointer mappings per binding.");
        Assert.That(clipText, Does.Contain("m_SampleRate: 4"), "Lady sitting should animate as a slow seated idle.");
        Assert.That(clipText, Does.Contain("m_StopTime: 1"), "Lady sitting should loop over a full one-second cycle.");
        Assert.That(clipText, Does.Contain("m_LoopTime: 1"), "Lady sitting should loop.");

        for (int i = 1; i <= 4; i++)
        {
            string framePath = $"{LadySittingFramePrefix}_{i:00}.png";
            string metaPath = $"{framePath}.meta";
            string frameGuid = ReadGuidFromMeta(metaPath);
            string metaText = File.ReadAllText(metaPath);

            Assert.That(ReadVisibleSpriteBounds(framePath, 365, 599).height, Is.GreaterThanOrEqualTo(570), $"Lady sitting frame {i} should keep the full seated figure.");
            Assert.That(metaText, Does.Contain("spriteMode: 1"), $"Lady sitting frame {i} should import as a single sprite.");
            Assert.That(metaText, Does.Contain("spritePixelsToUnits: 206.55172"), $"Lady sitting frame {i} should render at the same world scale as the standing Lady frames.");
            Assert.That(metaText, Does.Contain("spritePivot: {x: 0.5, y: 0"), $"Lady sitting frame {i} should keep a bottom-center pivot.");
            Assert.That(metaText, Does.Contain("alphaIsTransparency: 1"), $"Lady sitting frame {i} should keep transparent sprite import behavior.");
            Assert.That(metaText, Does.Contain("filterMode: 1"), $"Lady sitting frame {i} should keep point filtering.");
            Assert.That(clipText, Does.Contain(frameGuid), $"Lady sitting clip should include frame {i}.");
        }
    }

    private static void AssertButlerGuestSittingClip(string clipText)
    {
        string metaText = File.ReadAllText(ButlerGuestSittingSpriteMetaPath);
        string sittingSpriteGuid = ReadGuidFromMeta(ButlerGuestSittingSpriteMetaPath);

        Assert.That(File.Exists(ButlerGuestSittingSpriteMetaPath.Replace(".meta", string.Empty)), Is.True, "Guest 2 sitting source sheet should exist.");
        Assert.That(metaText, Does.Contain("spriteMode: 2"), "Guest 2 sitting source should stay imported as a multi-sprite sheet.");
        Assert.That(metaText, Does.Contain("spritePixelsToUnits: 197.93102"), "Guest 2 sitting source should render at the same world height as the standing ButlerGuest frames.");
        Assert.That(metaText, Does.Contain("pivot: {x: 0.5, y: 0}"), "Guest 2 sitting slices should use bottom-center pivots for room seat anchors.");
        Assert.That(metaText, Does.Contain("alphaIsTransparency: 1"), "Guest 2 sitting source should preserve transparent sprite import behavior.");
        Assert.That(metaText, Does.Contain("filterMode: 1"), "Guest 2 sitting source should keep point filtering.");
        Assert.That(metaText, Does.Not.Contain("ChatGPT Image"), "Guest 2 sitting source should not keep stale generated-image slice names after being renamed.");

        Assert.That(clipText, Does.Contain("classID: 114"), "Guest 2 sitting should bind UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Guest 2 sitting should bind SpriteRenderers for prefab-stage reuse.");
        Assert.That(Regex.Matches(clipText, @"value: \{fileID: ").Count, Is.EqualTo(8), "Guest 2 sitting should have four sprite keys for Image and four for SpriteRenderer.");
        Assert.That(Regex.Matches(clipText, @"^\s+- \{fileID: -?\d+, guid: [0-9a-f]{32}, type: 3\}$", RegexOptions.Multiline).Count, Is.EqualTo(8), "Guest 2 sitting should keep four pointer mappings per binding.");
        Assert.That(clipText, Does.Contain("m_SampleRate: 4"), "Guest 2 sitting should animate as a slow seated idle.");
        Assert.That(clipText, Does.Contain("m_StopTime: 1"), "Guest 2 sitting should loop over a full one-second cycle.");
        Assert.That(clipText, Does.Contain("m_LoopTime: 1"), "Guest 2 sitting should loop.");
        Assert.That(clipText, Does.Contain("m_PositionCurves: []"), "Guest 2 sitting should not move transforms.");
        Assert.That(clipText, Does.Contain("m_ScaleCurves: []"), "Guest 2 sitting should not scale transforms.");

        for (int i = 0; i <= 3; i++)
        {
            string spriteName = $"butlerspritesit_{i}";
            string spriteFileId = ReadSpriteFileIdFromMeta(ButlerGuestSittingSpriteMetaPath, spriteName);
            RectInt spriteRect = ReadNamedSpriteRect(metaText, spriteName);
            string spriteReference = $"{{fileID: {spriteFileId}, guid: {sittingSpriteGuid}, type: 3}}";

            Assert.That(spriteRect.height, Is.GreaterThanOrEqualTo(570), $"Guest 2 sitting frame {i} should keep the full seated figure.");
            Assert.That(metaText, Does.Contain($"second: {spriteName}"), $"Guest 2 sitting source should keep a clean internal name entry for {spriteName}.");
            Assert.That(clipText, Does.Contain(spriteReference), $"Guest 2 sitting clip should include {spriteName}.");
        }
    }

    private static void AssertButlerGuestIdleClipUsesStandaloneFrames(string clipText)
    {
        for (int i = 1; i <= 2; i++)
        {
            string framePath = $"{ButlerGuestIdleFramePrefix}_{i:00}.png";
            string frameGuid = ReadGuidFromMeta($"{framePath}.meta");
            string metaText = File.ReadAllText($"{framePath}.meta");

            Assert.That(File.Exists(framePath), Is.True, $"Guest 2 idle frame {i} should remain an editable standalone PNG.");
            Assert.That(ReadVisibleSpriteBounds(framePath, 77, 213).height, Is.GreaterThanOrEqualTo(190), $"Guest 2 idle frame {i} should keep the full standing figure.");
            Assert.That(metaText, Does.Contain("spriteMode: 1"), $"Guest 2 idle frame {i} should import as a single sprite.");
            Assert.That(metaText, Does.Contain("spritePixelsToUnits: 73.44827"), $"Guest 2 idle frame {i} should keep the butler sheet pixels-per-unit.");
            Assert.That(metaText, Does.Contain("spritePivot: {x: 0, y: 0}"), $"Guest 2 idle frame {i} should keep the butler sheet pivot.");
            Assert.That(clipText, Does.Contain($"{{fileID: 21300000, guid: {frameGuid}, type: 3}}"), $"Guest 2 idle should reference standalone frame {i}.");
        }

        Assert.That(clipText, Does.Not.Contain(ReadGuidFromMeta(ButlerGuestSpriteMetaPath)), "Guest 2 idle should not depend on the butler sheet sub-sprites.");
        Assert.That(clipText, Does.Contain("classID: 212"), "Guest 2 idle should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(clipText, Does.Contain("m_SampleRate: 4"), "Guest 2 idle should keep its four-key idle timing.");
        Assert.That(clipText, Does.Contain("m_StopTime: 1"), "Guest 2 idle should keep its one-second idle loop.");
    }

    private static void AssertButlerGuestExportedFramesMatchSheetSlices(string butlerSheetMetaText)
    {
        var exportedGuids = new HashSet<string>();

        for (int i = 0; i <= 44; i++)
        {
            string framePath = GetButlerGuestExportedFramePath(i);
            string metaPath = $"{framePath}.meta";
            string frameGuid = ReadGuidFromMeta(metaPath);
            string metaText = File.ReadAllText(metaPath);
            RectInt sheetRect = i >= 16 && i <= 23
                ? ReadButlerSheetSpriteRect(butlerSheetMetaText, i - 8)
                : ReadButlerSheetSpriteRect(butlerSheetMetaText, i);

            Assert.That(File.Exists(framePath), Is.True, $"Exported Guest 2 frame {i:00} should exist as an editable PNG.");
            Assert.That(exportedGuids.Add(frameGuid), Is.True, $"Exported Guest 2 frame {i:00} should have a unique GUID.");
            Assert.That(ReadVisibleSpriteBounds(framePath, sheetRect.width, sheetRect.height).height, Is.GreaterThan(0), $"Exported Guest 2 frame {i:00} should not be empty.");
            Assert.That(metaText, Does.Contain("spriteMode: 1"), $"Exported Guest 2 frame {i:00} should import as a single sprite.");
            Assert.That(metaText, Does.Contain("spritePixelsToUnits: 73.44827"), $"Exported Guest 2 frame {i:00} should keep the sheet pixels-per-unit.");
            Assert.That(metaText, Does.Contain("spritePivot: {x: 0, y: 0}"), $"Exported Guest 2 frame {i:00} should keep the sheet pivot.");
            Assert.That(metaText, Does.Contain("alphaIsTransparency: 1"), $"Exported Guest 2 frame {i:00} should preserve transparent sprite import behavior.");
            Assert.That(metaText, Does.Contain("filterMode: 1"), $"Exported Guest 2 frame {i:00} should keep point filtering.");
        }
    }

    private static void AssertButlerGuestMirroredSideWalkClips(string leftClipText, string rightClipText)
    {
        AssertButlerGuestRightWalkFramesMirrorLeftWalkFrames();

        Dictionary<string, string> mirroredGuids = BuildButlerGuestMirroredSideWalkGuidMap();
        string[] leftSpriteGuids = ReadSpriteGuidSequence(leftClipText);
        string[] rightSpriteGuids = ReadSpriteGuidSequence(rightClipText);
        string[] expectedRightSpriteGuids = leftSpriteGuids.Select(guid =>
        {
            Assert.That(mirroredGuids.ContainsKey(guid), Is.True, "Guest 2 walk left should only reference left-facing frames that have mirrored right-facing counterparts.");
            return mirroredGuids[guid];
        }).ToArray();

        Assert.That(leftSpriteGuids.Length, Is.GreaterThan(0), "Guest 2 walk left should reference standalone sprite frames.");
        Assert.That(rightSpriteGuids, Is.EqualTo(expectedRightSpriteGuids), "Guest 2 walk right should reuse the walk-left timing with mirrored frame GUIDs.");
        Assert.That(leftClipText, Does.Not.Contain(ReadGuidFromMeta(ButlerGuestSpriteMetaPath)), "Guest 2 walk left should not reference the sheet sub-sprites.");
        Assert.That(rightClipText, Does.Not.Contain(ReadGuidFromMeta(ButlerGuestSpriteMetaPath)), "Guest 2 walk right should not reference the sheet sub-sprites.");
        Assert.That(leftClipText, Does.Contain("classID: 212"), "Guest 2 walk left should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(rightClipText, Does.Contain("classID: 212"), "Guest 2 walk right should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(ReadAnimationScalar(leftClipText, "m_SampleRate"), Is.EqualTo(ReadAnimationScalar(rightClipText, "m_SampleRate")), "Guest 2 mirrored side walks should keep matching sample rates.");
        Assert.That(ReadAnimationScalar(leftClipText, "m_StopTime"), Is.EqualTo(ReadAnimationScalar(rightClipText, "m_StopTime")), "Guest 2 mirrored side walks should keep matching loop lengths.");
    }

    private static void AssertButlerGuestRightWalkFramesMirrorLeftWalkFrames()
    {
        for (int i = 8; i <= 15; i++)
        {
            AssertPngIsHorizontalMirror(GetButlerGuestExportedFramePath(i), GetButlerGuestExportedFramePath(i + 8), $"Guest 2 right walk frame {i + 8:00}");
        }

        AssertPngIsHorizontalMirror(ButlerGuestStandingSideLeftPath, ButlerGuestStandingSidePath, "Guest 2 right standing side frame");
    }

    private static void AssertButlerGuestSideWalkClipsUseFullScaleFrames(string leftClipText, string rightClipText)
    {
        Assert.That(leftClipText, Does.Contain("classID: 212"), "Guest 2 walk left should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(rightClipText, Does.Contain("classID: 212"), "Guest 2 walk right should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(leftClipText, Does.Contain("m_ScaleCurves: []"), "Guest 2 walk left should not fix size by scaling transforms.");
        Assert.That(rightClipText, Does.Contain("m_ScaleCurves: []"), "Guest 2 walk right should not fix size by scaling transforms.");
        AssertButlerGuestRightWalkUsesFullScaleStandaloneFrames();
    }

    private static void AssertButlerGuestRightWalkUsesFullScaleStandaloneFrames()
    {
        int[] expectedWidths = { 99, 91, 97, 91 };
        int[] expectedHeights = { 198, 199, 198, 199 };

        for (int i = 1; i <= 4; i++)
        {
            string framePath = $"{CharacterArtRoot}/guest2/Guest2Walkright/guest2right{i}.png";
            string metaPath = $"{framePath}.meta";
            string metaText = File.ReadAllText(metaPath);
            RectInt visibleBounds = ReadVisibleSpriteBounds(framePath, expectedWidths[i - 1], expectedHeights[i - 1]);
            float pixelsPerUnit = ReadSpritePixelsPerUnit(metaPath);
            float visibleWorldHeight = visibleBounds.height / pixelsPerUnit;

            Assert.That(metaText, Does.Contain("spritePixelsToUnits: 73.44827"), $"Guest 2 right walk frame {i} should use the same import scale as the rest of his butler art.");
            Assert.That(visibleWorldHeight, Is.GreaterThanOrEqualTo(2.6f), $"Guest 2 right walk frame {i} should not shrink compared with his other standing animations.");
        }
    }

    private static Dictionary<string, string> BuildButlerGuestMirroredSideWalkGuidMap()
    {
        var mirroredGuids = new Dictionary<string, string>();

        for (int i = 8; i <= 15; i++)
        {
            mirroredGuids[ReadGuidFromMeta($"{GetButlerGuestExportedFramePath(i)}.meta")] = ReadGuidFromMeta($"{GetButlerGuestExportedFramePath(i + 8)}.meta");
        }

        mirroredGuids[ReadGuidFromMeta($"{ButlerGuestStandingSideLeftPath}.meta")] = ReadGuidFromMeta($"{ButlerGuestStandingSidePath}.meta");
        return mirroredGuids;
    }

    private static string[] ReadSpriteGuidSequence(string clipText)
    {
        return Regex.Matches(clipText, @"\{fileID: 21300000, guid: (?<guid>[0-9a-f]{32}), type: 3\}")
            .Cast<Match>()
            .Select(match => match.Groups["guid"].Value)
            .ToArray();
    }

    private static string ReadAnimationScalar(string clipText, string key)
    {
        Match match = Regex.Match(clipText, $@"^\s+{Regex.Escape(key)}: (?<value>\S+)$", RegexOptions.Multiline);
        Assert.That(match.Success, Is.True, $"Could not find {key} in animation clip.");
        return match.Groups["value"].Value;
    }

    private static void AssertClipUsesButlerExportedFrames(string clipText, IEnumerable<int> spriteIndexes, string direction, int expectedSpriteKeyCount, int expectedPointerMappingCount, string expectedSampleRate, string expectedStopTime, bool requiresImageBinding)
    {
        foreach (int i in spriteIndexes)
        {
            string framePath = GetButlerGuestExportedFramePath(i);
            string frameGuid = ReadGuidFromMeta($"{framePath}.meta");
            string spriteReference = $"{{fileID: 21300000, guid: {frameGuid}, type: 3}}";
            Assert.That(File.Exists(framePath), Is.True, $"Guest 2 exported walk {direction} frame {i:00} should exist.");
            Assert.That(clipText, Does.Contain(spriteReference), $"Guest 2 walk {direction} should reference exported frame butlersprite_{i:00}.png.");
        }

        Assert.That(clipText, Does.Not.Contain(ReadGuidFromMeta(ButlerGuestSpriteMetaPath)), $"Guest 2 walk {direction} should not reference the sheet sub-sprites.");
        Assert.That(Regex.Matches(clipText, @"value: \{fileID: ").Count, Is.EqualTo(expectedSpriteKeyCount), $"Guest 2 walk {direction} should keep its authored sprite key count.");
        Assert.That(Regex.Matches(clipText, @"^\s+- \{fileID: ", RegexOptions.Multiline).Count, Is.EqualTo(expectedPointerMappingCount), $"Guest 2 walk {direction} should keep its authored pointer mapping count.");
        if (requiresImageBinding)
        {
            Assert.That(clipText, Does.Contain("classID: 114"), $"Guest 2 walk {direction} should animate UI Images for room-stage reuse.");
        }

        Assert.That(clipText, Does.Contain("classID: 212"), $"Guest 2 walk {direction} should animate SpriteRenderers for prefab-stage reuse.");
        Assert.That(clipText, Does.Contain($"m_SampleRate: {expectedSampleRate}"), $"Guest 2 walk {direction} should keep its authored walk timing.");
        Assert.That(clipText, Does.Contain($"m_StopTime: {expectedStopTime}"), $"Guest 2 walk {direction} should keep its authored row length.");
    }

    private static void AssertClipUsesButlerExportedSideWalkWithStanding(string clipText, IEnumerable<int> spriteIndexes, int replacedSprite, string standingFramePath, string direction, int expectedSpriteKeyCount, int expectedPointerMappingCount, int expectedStandingReferences, string expectedSampleRate, string expectedStopTime, bool requiresImageBinding)
    {
        AssertClipUsesButlerExportedFrames(clipText, spriteIndexes, direction, expectedSpriteKeyCount, expectedPointerMappingCount, expectedSampleRate, expectedStopTime, requiresImageBinding);

        string replacedFrameGuid = ReadGuidFromMeta($"{GetButlerGuestExportedFramePath(replacedSprite)}.meta");
        string standingGuid = ReadGuidFromMeta($"{standingFramePath}.meta");
        string standingValue = $"{{fileID: 21300000, guid: {standingGuid}, type: 3}}";

        Assert.That(clipText, Does.Not.Contain($"{{fileID: 21300000, guid: {replacedFrameGuid}, type: 3}}"), $"Guest 2 walk {direction} should keep replacing exported frame {replacedSprite:00} with the hands-at-side standing frame.");
        Assert.That(Regex.Matches(clipText, Regex.Escape(standingValue)).Count, Is.EqualTo(expectedStandingReferences), $"Guest 2 walk {direction} should use the standing side frame for its authored keys and pointer mappings.");
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

    private static string GetButlerGuestExportedFramePath(int spriteIndex)
    {
        return $"Assets/Art/Characters/guest2/butlersprite_{spriteIndex:00}.png";
    }

    private static RectInt ReadButlerSheetSpriteRect(string metaText, int spriteIndex)
    {
        return ReadNamedSpriteRect(metaText, $"butlersprite_{spriteIndex}");
    }

    private static RectInt ReadNamedSpriteRect(string metaText, string spriteName)
    {
        Match match = Regex.Match(metaText, $@"(?s)name: {Regex.Escape(spriteName)}\s+rect:\s+serializedVersion: 2\s+x: (?<x>\d+)\s+y: (?<y>\d+)\s+width: (?<width>\d+)\s+height: (?<height>\d+)");
        Assert.That(match.Success, Is.True, $"Could not read sheet rect for {spriteName}.");
        return new RectInt(
            int.Parse(match.Groups["x"].Value),
            int.Parse(match.Groups["y"].Value),
            int.Parse(match.Groups["width"].Value),
            int.Parse(match.Groups["height"].Value));
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

    private static void AssertForwardIdleClip(string clipText, string framePathPrefix, string characterName, int expectedWidth, int expectedHeight, string expectedPixelsPerUnit, string expectedPivotX)
    {
        RectInt? firstBounds = null;
        Dictionary<string, int> expectedFrameUses = new Dictionary<string, int>();
        Assert.That(clipText, Does.Contain("classID: 114"), $"{characterName} idle should bind UI Images for room-stage reuse.");
        Assert.That(clipText, Does.Contain("classID: 212"), $"{characterName} idle should bind SpriteRenderers for prefab-stage reuse.");
        Assert.That(Regex.Matches(clipText, @"value: \{fileID: 21300000").Count, Is.EqualTo(8), $"{characterName} idle should have four forward sprite keys for Image and four for SpriteRenderer.");
        Assert.That(Regex.Matches(clipText, @"^\s+- \{fileID: 21300000, guid: [0-9a-f]{32}, type: 3\}$", RegexOptions.Multiline).Count, Is.EqualTo(8), $"{characterName} idle should keep four pointer mappings per binding.");
        Assert.That(clipText, Does.Contain("m_SampleRate: 4"), $"{characterName} idle should breathe slowly, not step like a walk.");
        Assert.That(clipText, Does.Contain("m_StopTime: 1"), $"{characterName} idle should loop over a full one-second breathing cycle.");
        Assert.That(clipText, Does.Contain("m_LoopTime: 1"), $"{characterName} idle should loop.");
        Assert.That(clipText, Does.Contain("m_PositionCurves: []"), $"{characterName} idle should not move transforms.");
        Assert.That(clipText, Does.Contain("m_ScaleCurves: []"), $"{characterName} idle should not scale transforms.");

        for (int i = 1; i <= 4; i++)
        {
            string framePath = $"{framePathPrefix}_{i:00}.png";
            string metaPath = $"{framePath}.meta";
            string frameGuid = ReadGuidFromMeta(metaPath);
            string metaText = File.ReadAllText(ResolveConsolidatedSpriteMetaPath(metaPath));
            RectInt bounds = ReadVisibleSpriteBounds(framePath, expectedWidth, expectedHeight);
            expectedFrameUses.TryGetValue(frameGuid, out int frameUseCount);
            expectedFrameUses[frameGuid] = frameUseCount + 1;

            Assert.That(metaText, Does.Contain("spriteMode: 1"), $"{characterName} idle frame {i} should import as a single sprite.");
            Assert.That(metaText, Does.Contain($"spritePixelsToUnits: {expectedPixelsPerUnit}"), $"{characterName} idle frame {i} should keep the expected pixels-per-unit.");
            Assert.That(metaText, Does.Contain($"spritePivot: {{x: {expectedPivotX}, y: 0"), $"{characterName} idle frame {i} should keep the expected foot pivot.");
            Assert.That(metaText, Does.Contain("alphaIsTransparency: 1"), $"{characterName} idle frame {i} should keep transparent sprite import behavior.");
            Assert.That(metaText, Does.Contain("filterMode: 1"), $"{characterName} idle frame {i} should keep point filtering.");

            if (firstBounds.HasValue)
            {
                Assert.That(bounds.yMin, Is.InRange(firstBounds.Value.yMin - 1, firstBounds.Value.yMin + 1), $"{characterName} idle frame {i} should keep the same foot baseline.");
                Assert.That(bounds.height, Is.InRange(firstBounds.Value.height - 4, firstBounds.Value.height + 4), $"{characterName} idle frame {i} should keep the same visible scale.");
            }
            else
            {
                firstBounds = bounds;
            }
        }

        foreach (KeyValuePair<string, int> expectedFrameUse in expectedFrameUses)
        {
            string spriteReference = $"{{fileID: 21300000, guid: {expectedFrameUse.Key}, type: 3}}";
            Assert.That(Regex.Matches(clipText, Regex.Escape(spriteReference)).Count, Is.EqualTo(expectedFrameUse.Value * 4), $"{characterName} idle should use each canonical forward frame for both sprite keys and pointer mappings.");
        }

        foreach (string direction in new[] { "left", "right", "up" })
        {
            string directionalPrefix = framePathPrefix.Replace("_idle_down", $"_idle_{direction}");
            for (int i = 1; i <= 4; i++)
            {
                string metaPath = $"{directionalPrefix}_{i:00}.png.meta";
                if (File.Exists(metaPath))
                {
                    Assert.That(clipText, Does.Not.Contain(ReadGuidFromMeta(metaPath)), $"{characterName} idle should stay forward-facing and not use {direction} idle frame {i}.");
                }
            }
        }
    }

    private static void AssertForwardIdleMatchesWalkScale(string walkFramePath, string idleFramePathPrefix, string characterName)
    {
        RectInt walkBounds = ReadVisibleSpriteBounds(walkFramePath);

        for (int i = 1; i <= 4; i++)
        {
            RectInt idleBounds = ReadVisibleSpriteBounds($"{idleFramePathPrefix}_{i:00}.png");

            Assert.That(idleBounds.width, Is.EqualTo(walkBounds.width), $"{characterName} idle frame {i} should keep the same visible width as the forward walk frame.");
            Assert.That(idleBounds.height, Is.InRange(walkBounds.height, walkBounds.height + 3), $"{characterName} idle frame {i} should not shrink below the forward walk frame.");
            Assert.That(idleBounds.yMin, Is.EqualTo(walkBounds.yMin), $"{characterName} idle frame {i} should keep the same foot baseline as the forward walk frame.");
        }
    }

    private static void AssertSceneGuestUsesNamedAnimation(string sceneText, int guestNumber, string displayName, string assetName, string filePrefix)
    {
        string walkFolder = GetCharacterWalkFolder(assetName);
        string animationFolder = $"Assets/Animation/{assetName}";
        string guestBlock = FindPrefabInstanceBlock(sceneText, $"value: Guest {guestNumber}");
        string firstFrameGuid = ReadGuidFromMeta(GetScenePreviewFrameMetaPath(assetName, walkFolder, filePrefix));
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

    private static void AssertNamedGuestAnimationAssets(string displayName, string assetName, string filePrefix, bool usesForwardIdle)
    {
        if (assetName == "LordAmbroseVeil")
        {
            AssertLordAmbroseUsesGuestPair02ManAnimations(displayName, assetName);
            return;
        }

        string walkFolder = GetCharacterWalkFolder(assetName);
        string idleFramePrefix = GetCharacterIdleFramePrefix(assetName, filePrefix);
        string animationFolder = $"Assets/Animation/{assetName}";
        string overrideText = File.ReadAllText($"{animationFolder}/{assetName}.overrideController");
        string firstFrameGuid = ReadGuidFromMeta($"{walkFolder}/{filePrefix}_walk_01_r01_c01.png.meta");
        string idleClipGuid = ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle.anim.meta");

        Assert.That(Directory.GetFiles(walkFolder, $"{filePrefix}_walk_*.png").Length, Is.EqualTo(GetExpectedWalkFrameCount(assetName)), $"{displayName} should keep the walk frames used by the shipped clips.");
        Assert.That(overrideText, Does.Contain(idleClipGuid), $"{displayName} override controller should wire generic idle states to the main idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Down.anim.meta")), $"{displayName} should not wire the separate directional down idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Left.anim.meta")), $"{displayName} should not wire the directional left idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Right.anim.meta")), $"{displayName} should not wire the directional right idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Up.anim.meta")), $"{displayName} should not wire the directional up idle clip.");

        if (usesForwardIdle)
        {
            AssertForwardIdleClip(File.ReadAllText($"{animationFolder}/{assetName}_Idle.anim"), idleFramePrefix, displayName, 166, 297, "100", "0.5");
        }
        else
        {
            AssertStillIdleClip(File.ReadAllText($"{animationFolder}/{assetName}_Idle.anim"), firstFrameGuid, displayName);
        }

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

    private static void AssertLordAmbroseUsesGuestPair02ManAnimations(string displayName, string assetName)
    {
        string animationFolder = $"Assets/Animation/{assetName}";
        string overrideText = File.ReadAllText($"{animationFolder}/{assetName}.overrideController");
        string idleClipGuid = ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle.anim.meta");

        Assert.That(Directory.GetFiles($"{CharacterArtRoot}/guest7", "lord_ambrose_veil_*.png").Length, Is.EqualTo(0), $"{displayName} should not keep the deleted generated Lord Ambrose sprites in the guest7 root.");
        Assert.That(overrideText, Does.Contain(idleClipGuid), $"{displayName} override controller should wire generic idle states to the main idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Down.anim.meta")), $"{displayName} should not wire the separate directional down idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Left.anim.meta")), $"{displayName} should not wire the directional left idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Right.anim.meta")), $"{displayName} should not wire the directional right idle clip.");
        Assert.That(overrideText, Does.Not.Contain(ReadGuidFromMeta($"{animationFolder}/{assetName}_Idle_Up.anim.meta")), $"{displayName} should not wire the directional up idle clip.");

        AssertGuestPair02ManClipSequence(File.ReadAllText($"{animationFolder}/{assetName}_Idle.anim"), "guest7standidle", "GuestPair02Man_standing_idle", new[] { 1, 2, 3, 4 }, displayName, "idle");
        AssertGuestPair02ManClipSequence(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Down.anim"), "guest7down", "GuestPair02Man_down", new[] { 1, 4, 2, 3 }, displayName, "walk down");
        AssertGuestPair02ManClipSequence(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Left.anim"), "guest7left", "GuestPair02Man_left", new[] { 2, 4, 3, 4 }, displayName, "walk left");
        AssertGuestPair02ManClipSequence(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Right.anim"), "guest7right", "GuestPair02Man_right", new[] { 1, 4, 2, 4 }, displayName, "walk right");
        AssertGuestPair02ManClipSequence(File.ReadAllText($"{animationFolder}/{assetName}_Walk_Up.anim"), "guest07up", "GuestPair02Man_up", new[] { 2, 2, 1, 4 }, displayName, "walk up");
    }

    private static void AssertGuestPair02ManClipSequence(string clipText, string subfolder, string filePrefix, int[] frameNumbers, string displayName, string clipLabel)
    {
        Assert.That(clipText, Does.Contain("classID: 212"), $"{displayName} {clipLabel} should animate SpriteRenderers.");
        Assert.That(clipText, Does.Contain("m_LoopTime: 1"), $"{displayName} {clipLabel} should loop.");

        for (int i = 0; i < frameNumbers.Length; i++)
        {
            string framePath = $"{CharacterArtRoot}/guest7/{subfolder}/{filePrefix}_{frameNumbers[i]:00}.png";
            string metaPath = $"{framePath}.meta";
            string spriteName = $"{Path.GetFileNameWithoutExtension(framePath)}_0";
            string guid = ReadGuidFromMeta(metaPath);
            string fileId = ReadSpriteFileIdFromMeta(metaPath, spriteName);
            string spriteValue = $"value: {{fileID: {fileId}, guid: {guid}, type: 3}}";
            string mappingValue = $"- {{fileID: {fileId}, guid: {guid}, type: 3}}";

            Assert.That(File.Exists(framePath), Is.True, $"{displayName} {clipLabel} should use {framePath}.");
            Assert.That(clipText, Does.Contain(spriteValue), $"{displayName} {clipLabel} should include {Path.GetFileName(framePath)} as an animation key.");
            Assert.That(clipText, Does.Contain(mappingValue), $"{displayName} {clipLabel} should include {Path.GetFileName(framePath)} in pointer mappings.");
        }
    }

    private static void AssertGuestPair02ManWalkSpritesExist(string walkFolder)
    {
        Assert.That(Directory.GetFiles($"{walkFolder}/guest7left", "GuestPair02Man_left_*.png").Length, Is.GreaterThanOrEqualTo(3), "Lord Ambrose Veil should keep the correct guest7 left walk sprites.");
        Assert.That(Directory.GetFiles($"{walkFolder}/guest7right", "GuestPair02Man_right_*.png").Length, Is.GreaterThanOrEqualTo(3), "Lord Ambrose Veil should keep the correct guest7 right walk sprites.");
    }

    private static void AssertGuestPair02ManStandingIdleFramesAreValid()
    {
        for (int i = 1; i <= 4; i++)
        {
            RectInt standingBounds = ReadVisibleSpriteBounds($"{CharacterArtRoot}/guest7/guest7standidle/GuestPair02Man_standing_idle_{i:00}.png", 248, 304);
            Assert.That(standingBounds.height, Is.GreaterThanOrEqualTo(240), $"Lord Ambrose Veil standing idle frame {i} should keep the full-height GuestPair02Man art.");
            Assert.That(standingBounds.width, Is.GreaterThanOrEqualTo(80), $"Lord Ambrose Veil standing idle frame {i} should keep visible character pixels.");
        }
    }

    private static void AssertStandingIdleSequenceWorldHeight(
        string displayName,
        string frameFolder,
        string frameName,
        int expectedWidth,
        int expectedHeight)
    {
        const float MinimumEntranceIdleWorldHeight = 2.8f;

        for (int i = 1; i <= 4; i++)
        {
            string framePath = $"{frameFolder}/{i:00}_{frameName}_{i:00}.png";

            if (!File.Exists(framePath))
            {
                framePath = $"{frameFolder}/{frameName}_{i:00}.png";
            }

            RectInt visibleBounds = ReadVisibleSpriteBounds(framePath, expectedWidth, expectedHeight);
            float pixelsPerUnit = ReadSpritePixelsPerUnit($"{framePath}.meta");
            float visibleWorldHeight = visibleBounds.height / pixelsPerUnit;

            Assert.That(visibleWorldHeight, Is.GreaterThanOrEqualTo(MinimumEntranceIdleWorldHeight), $"{displayName} entrance idle frame {i} should not render smaller than the other line-up guests.");
        }
    }

    private static string GetScenePreviewFrameMetaPath(string assetName, string walkFolder, string filePrefix)
    {
        return assetName == "LordAmbroseVeil"
            ? $"{CharacterArtRoot}/guest7/guest7standidle/GuestPair02Man_standing_idle_01.png.meta"
            : $"{walkFolder}/{filePrefix}_walk_01_r01_c01.png.meta";
    }

    private static bool UsesCustomSideWalk(string assetName)
    {
        return assetName == "BaronHectorGlass"
            || assetName == "LordAmbroseVeil"
            || assetName == "ProfessorLucienVale";
    }

    private static string GetCharacterWalkFolder(string assetName)
    {
        return assetName switch
        {
            "BaronHectorGlass" => $"{CharacterArtRoot}/guest5",
            "LadySabineMarrow" => $"{CharacterArtRoot}/guest6",
            "LordAmbroseVeil" => $"{CharacterArtRoot}/guest7",
            "MadameCoralieThread" => $"{CharacterArtRoot}/guest8",
            _ => $"Assets/Art/Library/LegacyCharacters/{assetName}/walk/aligned"
        };
    }

    private static int GetExpectedWalkFrameCount(string assetName)
    {
        return 32;
    }

    private static string GetCharacterIdleFramePrefix(string assetName, string filePrefix)
    {
        return assetName switch
        {
            "BaronHectorGlass" => $"{CharacterArtRoot}/guest5/{filePrefix}_idle_down",
            "LadySabineMarrow" => $"{CharacterArtRoot}/guest6/{filePrefix}_idle_down",
            "LordAmbroseVeil" => $"{CharacterArtRoot}/guest7/{filePrefix}_idle_down",
            "MadameCoralieThread" => $"{CharacterArtRoot}/guest8/{filePrefix}_idle_down",
            _ => $"Assets/Art/Library/LegacyCharacters/{assetName}/idle/aligned/{filePrefix}_idle_down"
        };
    }

    private static void AssertCharacterArtRootIsGuestAndButlerFoldersOnly()
    {
        string[] expectedFolders =
        {
            "butler",
            "guest1",
            "guest2",
            "guest3",
            "guest4",
            "guest5",
            "guest6",
            "guest7",
            "guest8"
        };
        string[] actualFolders = Directory.GetDirectories(CharacterArtRoot)
            .Select(Path.GetFileName)
            .OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.That(actualFolders, Is.EqualTo(expectedFolders.OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase).ToArray()), "Assets/Art/Characters should contain only the labeled guest and butler folders.");
        Assert.That(Directory.GetFiles(CharacterArtRoot, "*.png").Length, Is.EqualTo(0), "Character images should not sit loose in Assets/Art/Characters.");

        for (int i = 0; i < expectedFolders.Length; i++)
        {
            string folderPath = $"{CharacterArtRoot}/{expectedFolders[i]}";
            Assert.That(Directory.GetFiles(folderPath, "*.png").Length, Is.GreaterThan(0), $"{expectedFolders[i]} should contain the images used by its animations.");

            foreach (string filePath in Directory.GetFiles(folderPath))
            {
                string extension = Path.GetExtension(filePath);
                Assert.That(extension == ".png" || extension == ".meta", Is.True, $"{expectedFolders[i]} should contain only sprite images and Unity meta files.");
            }
        }
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
        if (Regex.IsMatch(metaText, @"^\s+spriteMode: 1$", RegexOptions.Multiline))
        {
            return "21300000";
        }

        Match match = Regex.Match(metaText, $@"^\s+{Regex.Escape(spriteName)}: (-?\d+)$", RegexOptions.Multiline);
        Assert.That(match.Success, Is.True, $"Could not find sprite fileID for {spriteName} in {metaPath}.");
        return match.Groups[1].Value;
    }

    private static float ReadSpritePixelsPerUnit(string metaPath)
    {
        string metaText = File.ReadAllText(metaPath);
        Match match = Regex.Match(metaText, @"^\s+spritePixelsToUnits: (?<value>[0-9.]+)$", RegexOptions.Multiline);

        Assert.That(match.Success, Is.True, $"Could not find spritePixelsToUnits in {metaPath}.");
        return float.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
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

    private static void AssertPngIsHorizontalMirror(string leftImagePath, string rightImagePath, string label)
    {
        leftImagePath = ResolveConsolidatedSpritePath(leftImagePath);
        rightImagePath = ResolveConsolidatedSpritePath(rightImagePath);
        Texture2D leftTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        Texture2D rightTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(ImageConversion.LoadImage(leftTexture, File.ReadAllBytes(leftImagePath)), Is.True, $"Could not load PNG sprite at {leftImagePath}.");
            Assert.That(ImageConversion.LoadImage(rightTexture, File.ReadAllBytes(rightImagePath)), Is.True, $"Could not load PNG sprite at {rightImagePath}.");
            Assert.That(rightTexture.width, Is.EqualTo(leftTexture.width), $"{label} should keep the same canvas width as its left-facing source.");
            Assert.That(rightTexture.height, Is.EqualTo(leftTexture.height), $"{label} should keep the same canvas height as its left-facing source.");

            Color32[] leftPixels = leftTexture.GetPixels32();
            Color32[] rightPixels = rightTexture.GetPixels32();
            for (int y = 0; y < leftTexture.height; y++)
            {
                for (int x = 0; x < leftTexture.width; x++)
                {
                    Color32 expected = leftPixels[(y * leftTexture.width) + (leftTexture.width - 1 - x)];
                    Color32 actual = rightPixels[(y * rightTexture.width) + x];
                    if (actual.r == expected.r && actual.g == expected.g && actual.b == expected.b && actual.a == expected.a)
                    {
                        continue;
                    }

                    Assert.Fail($"{label} should be a horizontal flip of {leftImagePath}; first mismatch at ({x}, {y}).");
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(leftTexture);
            UnityEngine.Object.DestroyImmediate(rightTexture);
        }
    }

    private static RectInt ReadVisibleSpriteBounds(string imagePath, int expectedWidth = 166, int expectedHeight = 297)
    {
        imagePath = ResolveConsolidatedSpritePath(imagePath);
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

    private static string ResolveConsolidatedSpriteMetaPath(string metaPath)
    {
        if (!metaPath.EndsWith(".png.meta", StringComparison.Ordinal))
        {
            return metaPath;
        }

        string imagePath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
        return $"{ResolveConsolidatedSpritePath(imagePath)}.meta";
    }

    private static string ResolveConsolidatedSpritePath(string imagePath)
    {
        return imagePath switch
        {
            "Assets/Art/Characters/butler/butler_classic_idle_down_01.png" => "Assets/Art/Characters/butler/butler_classic_walk_01_r01_c01.png",
            "Assets/Art/Characters/butler/butler_classic_idle_down_03.png" => "Assets/Art/Characters/butler/butler_classic_walk_01_r01_c01.png",
            "Assets/Art/Characters/butler/butler_classic_idle_left_01.png" => "Assets/Art/Characters/butler/butler_classic_walk_05_r02_c01.png",
            "Assets/Art/Characters/butler/butler_classic_idle_left_03.png" => "Assets/Art/Characters/butler/butler_classic_walk_05_r02_c01.png",
            "Assets/Art/Characters/butler/butler_classic_idle_right_01.png" => "Assets/Art/Characters/butler/butler_classic_walk_09_r03_c01.png",
            "Assets/Art/Characters/butler/butler_classic_idle_right_03.png" => "Assets/Art/Characters/butler/butler_classic_walk_09_r03_c01.png",
            "Assets/Art/Characters/butler/butler_classic_idle_up_01.png" => "Assets/Art/Characters/butler/butler_classic_walk_13_r04_c01.png",
            "Assets/Art/Characters/butler/butler_classic_idle_up_03.png" => "Assets/Art/Characters/butler/butler_classic_walk_13_r04_c01.png",
            "Assets/Art/Characters/guest2/butler_guest_idle_down_03.png" => "Assets/Art/Characters/guest2/butler_guest_idle_down_02.png",
            "Assets/Art/Characters/guest2/butler_guest_idle_down_04.png" => "Assets/Art/Characters/guest2/butler_guest_idle_down_02.png",
            _ => imagePath
        };
    }
}
