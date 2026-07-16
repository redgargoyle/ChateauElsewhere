using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CharacterScaleOwnershipRegressionTests
{
    private const string SnapshotPath = "docs/migrations/character-scale/legacy-character-scale-snapshot.json";
    private const string GameplayPath = "Assets/Scenes/Gameplay.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string DrawingRoomProfilePath = "Assets/ScriptableObjects/Rooms/DrawingRoomPerspectiveProfile.asset";
    private const string DiningRoomProfilePath = "Assets/ScriptableObjects/Rooms/DiningRoomPerspectiveProfile.asset";

    private static readonly object[] GuestSittingRoster =
    {
        new object[] { "Guest 1", "Assets/Animation/Lady/Lady.overrideController", "Assets/Animation/Lady/Lady_Sitting.anim" },
        new object[] { "Guest 2", "Assets/Animation/ButlerGuest/ButlerGuest.overrideController", "Assets/Animation/ButlerGuest/ButlerGuest_Sitting.anim" },
        new object[] { "Guest 3", "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell.overrideController", "Assets/Animation/MisterFlorianKnell/MisterFlorianKnell_Sitting.anim" },
        new object[] { "Guest 4", "Assets/Animation/CountessElowenDusk/CountessElowenDusk.overrideController", "Assets/Animation/CountessElowenDusk/CountessElowenDusk_Sitting.anim" },
        new object[] { "Guest 5", "Assets/Animation/BaronHectorGlass/BaronHectorGlass.overrideController", "Assets/Animation/BaronHectorGlass/BaronHectorGlass_Sitting.anim" },
        new object[] { "Guest 6", "Assets/Animation/LadySabineMarrow/LadySabineMarrow.overrideController", "Assets/Animation/LadySabineMarrow/LadySabineMarrow_Sitting.anim" },
        new object[] { "Guest 7", "Assets/Animation/LordAmbroseVeil/LordAmbroseVeil.overrideController", "Assets/Animation/LordAmbroseVeil/LordAmbroseVeil_Sitting.anim" },
        new object[] { "Guest 8", "Assets/Animation/MadameCoralieThread/MadameCoralieThread.overrideController", "Assets/Animation/MadameCoralieThread/MadameCoralieThread_Sitting.anim" }
    };

    [Test]
    public void LegacySnapshotPreservesCompletePhaseOneMigrationEvidence()
    {
        Assert.That(File.Exists(SnapshotPath), Is.True, "Phase 1 must freeze legacy values before deleting their owners.");
        LegacySnapshot snapshot = JsonUtility.FromJson<LegacySnapshot>(File.ReadAllText(SnapshotPath));

        Assert.That(snapshot.schemaVersion, Is.EqualTo(1));
        Assert.That(snapshot.source.gitCommit, Is.EqualTo("2a92396176c2baa6310e42f9ee906ee846d94e03"));
        Assert.That(snapshot.source.unityVersion, Is.EqualTo("6000.4.10f1"));
        Assert.That(snapshot.source.files, Has.Length.EqualTo(4));
        Assert.That(
            snapshot.source.files.Select(file => file.sha256),
            Is.EqualTo(new[]
            {
                "1099b1469437d46f5c45b7b8041e50977817112a7ec65027d10899222d2bd17d",
                "fcc64c863c1101340cf4cb96d91389af679e7a7fea8f6bdcb2d1c0e6101b3f71",
                "96a746b728e0048deec1f4df782ca3e79a67ab11137a887130d91f9fe53c2032",
                "aca70313aa7fc8a5568a54e9c0955517cfc84b477d00b765e2c48b148804db7a"
            }));
        Assert.That(snapshot.butler.roomOverrides, Has.Length.EqualTo(19));
        Assert.That(snapshot.guestRoomCalibration.rooms, Has.Length.EqualTo(19));
        Assert.That(snapshot.guests, Has.Length.EqualTo(8));
        Assert.That(snapshot.posePlacement.drawingRoom.assignments, Has.Length.EqualTo(8));
        Assert.That(snapshot.posePlacement.diningRoom.assignments, Has.Length.EqualTo(8));
        Assert.That(snapshot.posePlacement.diningRoom.occlusionBindings, Has.Length.EqualTo(8));
        Assert.That(snapshot.guests.All(guest => !string.IsNullOrWhiteSpace(guest.sittingMapping.replacementClipGuid)), Is.True);
        Assert.That(snapshot.roomPerspectiveProfiles, Has.Length.EqualTo(2));
        Assert.That(snapshot.integrity.expectedCounts.sourceFiles, Is.EqualTo(4));
        Assert.That(snapshot.integrity.expectedCounts.butlerRoomOverrides, Is.EqualTo(19));
        Assert.That(snapshot.integrity.expectedCounts.guestRoomCalibrationRows, Is.EqualTo(19));
        Assert.That(snapshot.integrity.expectedCounts.participantRecords, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.sittingMappings, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.drawingRoomAssignments, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.diningRoomAssignments, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.diningRoomOcclusionBindings, Is.EqualTo(8));
        Assert.That(snapshot.integrity.expectedCounts.roomPerspectiveProfiles, Is.EqualTo(2));
    }

    [TestCaseSource(nameof(GuestSittingRoster))]
    public void GuestOverrideControllerPreservesSittingMapping(
        string characterId,
        string controllerPath,
        string expectedSittingClipPath)
    {
        AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        AnimationClip expectedSittingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(expectedSittingClipPath);

        Assert.That(controller, Is.Not.Null, $"{characterId} override controller must remain at {controllerPath}.");
        Assert.That(expectedSittingClip, Is.Not.Null, $"{characterId} sitting clip must remain at {expectedSittingClipPath}.");

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(controller.overridesCount);
        controller.GetOverrides(overrides);
        KeyValuePair<AnimationClip, AnimationClip> sittingMapping = overrides.SingleOrDefault(
            mapping => mapping.Key != null && string.Equals(mapping.Key.name, "Player_Croutch", StringComparison.Ordinal));

        Assert.That(sittingMapping.Key, Is.Not.Null, $"{characterId} must retain the Player_Croutch override slot.");
        Assert.That(sittingMapping.Value, Is.SameAs(expectedSittingClip), $"{characterId} must retain its authored sitting clip.");
    }

    [Test]
    public void AnimationClipsDoNotWriteTransformScale()
    {
        string[] clipPaths = Directory.GetFiles("Assets", "*.anim", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.That(clipPaths, Is.Not.Empty);

        foreach (string clipPath in clipPaths)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.That(clip, Is.Not.Null, clipPath);
            Assert.That(
                AnimationUtility.GetCurveBindings(clip).Any(
                    binding => binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal)),
                Is.False,
                clipPath);
        }
    }

    [Serializable]
    private sealed class LegacySnapshot
    {
        public int schemaVersion;
        public SnapshotSource source;
        public ButlerSnapshot butler;
        public GuestRoomCalibrationSnapshot guestRoomCalibration;
        public GuestSnapshot[] guests;
        public RoomPerspectiveProfileSnapshot[] roomPerspectiveProfiles;
        public PosePlacementSnapshot posePlacement;
        public IntegritySnapshot integrity;
    }

    [Serializable]
    private sealed class SnapshotSource
    {
        public string gitCommit;
        public string unityVersion;
        public SnapshotSourceFile[] files;
    }

    [Serializable]
    private sealed class SnapshotSourceFile
    {
        public string path;
        public string guid;
        public string sha256;
    }

    [Serializable]
    private sealed class ButlerSnapshot
    {
        public SerializedRecord[] roomOverrides;
    }

    [Serializable]
    private sealed class GuestRoomCalibrationSnapshot
    {
        public SerializedRecord[] rooms;
    }

    [Serializable]
    private sealed class GuestSnapshot
    {
        public SittingMappingSnapshot sittingMapping;
    }

    [Serializable]
    private sealed class SittingMappingSnapshot
    {
        public string replacementClipGuid;
    }

    [Serializable]
    private sealed class RoomPerspectiveProfileSnapshot
    {
        public string roomId;
    }

    [Serializable]
    private sealed class PosePlacementSnapshot
    {
        public DrawingRoomPlacementSnapshot drawingRoom;
        public DiningRoomPlacementSnapshot diningRoom;
    }

    [Serializable]
    private sealed class DrawingRoomPlacementSnapshot
    {
        public SerializedRecord[] assignments;
    }

    [Serializable]
    private sealed class DiningRoomPlacementSnapshot
    {
        public SerializedRecord[] assignments;
        public SerializedRecord[] occlusionBindings;
    }

    [Serializable]
    private sealed class IntegritySnapshot
    {
        public ExpectedCounts expectedCounts;
    }

    [Serializable]
    private sealed class ExpectedCounts
    {
        public int sourceFiles;
        public int butlerRoomOverrides;
        public int guestRoomCalibrationRows;
        public int guests;
        public int participantRecords;
        public int sittingMappings;
        public int drawingRoomAssignments;
        public int diningRoomAssignments;
        public int diningRoomOcclusionBindings;
        public int roomPerspectiveProfiles;
    }

    [Serializable]
    private sealed class SerializedRecord
    {
        public string propertyPath;
        public string rawValue;
        public string provenance;
    }
}
