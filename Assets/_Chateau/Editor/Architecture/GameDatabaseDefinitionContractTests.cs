#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Chateau.Architecture;
using Chateau.World.Rooms.Passages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using CanonicalRoomDefinition = Chateau.World.Rooms.RoomDefinition;

public sealed class GameDatabaseDefinitionContractTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string RoomsFolder = "Assets/_Chateau/Data/World/Rooms";
    private const string PassagesFolder = "Assets/_Chateau/Data/World/Passages";
    private const string DatabasePath = "Assets/_Chateau/Data/GameDatabase.asset";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string RoomContentGroupGuid = "d0ea47fd950844bcacb0fd5556a9d880";
    private const string RoomViewGuid = "ccd2f3bd803e45aa8a1174cc881d6dc0";
    private const string PassageGuid = "518dad8adf634786a103bf4e76aa0881";
    private const string LegacyDoorTriggerGuid = "7e419b0f8f26d4f2d8d03e567fef4c52";
    private const string GameRootGuid = "bc887e2e5e4f5cc594cd3d8920eb9f90";

    private static readonly RoomExpectation[] ApprovedRooms =
    {
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_GrandEntranceHall.asset",
            "room.grand-entrance-hall",
            "Grand Entrance Hall",
            new[] { "Grand Entrance Hall" },
            "3e163816317a638f5adedc338ec34d98"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_DrawingRoom.asset",
            "room.drawing-room",
            "Drawing Room",
            new[] { "Drawing Room" },
            "28c74b6dea1ed8e2c9c7d612355f9734"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_MusicRoom.asset",
            "room.music-room",
            "Music Room",
            new[] { "Music Room" },
            "028084782cdcf3d4ab3b596624c8b7c5"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_Library.asset",
            "room.library",
            "Library",
            new[] { "Library" },
            "0a85e4fdd73e4714fabde63002a457e7"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_Ballroom.asset",
            "room.ballroom",
            "Ballroom",
            new[] { "Ballroom" },
            "7dabdfc97f536fe458e28ca413b0a0fa"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_DiningRoom.asset",
            "room.dining-room",
            "Dining Room",
            new[] { "Dining Room" },
            "004ab4cca930d0387892725fe69b4f72",
            "a63248cfbd6b4a72af45c62cff7e94d0"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_ButlersPantry.asset",
            "room.butlers-pantry",
            "Butlers Pantry",
            new[] { "Butlers Pantry", "Butler's Pantry" },
            "e73e44419d3782452bb6abd0e8edd452"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_BilliardRoom.asset",
            "room.billiard-room",
            "Billiard Room",
            new[] { "Billiard Room" },
            "5987c5a8b3a09fc1ca848ac0ece03658"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_ServiceCorridor.asset",
            "room.service-corridor",
            "Service Corridor",
            new[] { "Service Corridor" },
            "63139e8fe55e5e00f97b08fe5f2b145b"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_Kitchen.asset",
            "room.kitchen",
            "Kitchen",
            new[] { "Kitchen" },
            "788c4ce8a4f6e8b8580f808a95b41c05"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_Chapel.asset",
            "room.chapel",
            "Chapel",
            new[] { "Chapel" },
            "d40ce95937763bcddb24975fe9c6ec20"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_GrandEntranceHallRearView.asset",
            "room.grand-entrance-hall-rear-view",
            "Grand Entrance Hall Rear View",
            new[] { "Grand Entrance Hall Rear view" },
            "be7b38f2cec9bee98bad55097937c9c6"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_Conservatory.asset",
            "room.conservatory",
            "Conservatory",
            new[] { "Conservatory" },
            "b86ab0433400447849c3249e0a503052"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_SideStairMudroom.asset",
            "room.side-stair-mudroom",
            "Side Stair & Mudroom",
            new[] { "Side Stair & Mudroom", "Side Stair Mudroom" },
            "755ad9d5953d1f60cafbff7c71f5767f"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_UpperSittingHall.asset",
            "room.upper-sitting-hall",
            "Upper Sitting Hall",
            new[] { "Upper Sitting Hall" },
            "45a669872351e5ba0b2a6749ecc2065f"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_UpperGallery.asset",
            "room.upper-gallery",
            "Upper Gallery",
            new[] { "Upper Gallery" },
            "1bbe0674f13938fcc9559b5fb933be83"),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_MasterBedroomSuite.asset",
            "room.master-bedroom-suite",
            "Master Bedroom Suite",
            new[] { "Master Bedroom Suite" },
            "6169353da08ead53db8fa10441e9ae49",
            isDataOnly: true),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_Nursery.asset",
            "room.nursery",
            "Nursery",
            new[] { "Nursery" },
            "5e685bb9c8917582f977493e3feed036",
            isDataOnly: true),
        new RoomExpectation(
            "Assets/_Chateau/Data/World/Rooms/Room_BlueBedroom.asset",
            "room.blue-bedroom",
            "Blue Bedroom",
            new[] { "Blue Bedroom" },
            "726529cec513ee83585fb467c7093cbe",
            isDataOnly: true)
    };

    private static readonly SceneRoomExpectation[] SceneRooms =
    {
        new SceneRoomExpectation("2102000000", "43637644", "Ballroom", "7dabdfc97f536fe458e28ca413b0a0fa"),
        new SceneRoomExpectation("2300000012", "2300000010", "Billiard Room", "5987c5a8b3a09fc1ca848ac0ece03658"),
        new SceneRoomExpectation("2300000062", "2300000060", "Blue Bedroom", "726529cec513ee83585fb467c7093cbe"),
        new SceneRoomExpectation("2300000022", "2300000020", "Butlers Pantry", "e73e44419d3782452bb6abd0e8edd452"),
        new SceneRoomExpectation("2300000032", "2300000030", "Chapel", "d40ce95937763bcddb24975fe9c6ec20"),
        new SceneRoomExpectation("2300000002", "2300000000", "Conservatory", "b86ab0433400447849c3249e0a503052"),
        new SceneRoomExpectation(
            "2300000017",
            "2300000015",
            "Dining Room",
            "004ab4cca930d0387892725fe69b4f72",
            "a63248cfbd6b4a72af45c62cff7e94d0"),
        new SceneRoomExpectation("2300000007", "2300000005", "Drawing Room", "28c74b6dea1ed8e2c9c7d612355f9734"),
        new SceneRoomExpectation("969603170", "969603168", "Grand Entrance Hall Rear view", "be7b38f2cec9bee98bad55097937c9c6"),
        new SceneRoomExpectation("2102000002", "567115833", "Grand Entrance Hall", "3e163816317a638f5adedc338ec34d98"),
        new SceneRoomExpectation("2102000004", "1541978210", "Kitchen", "788c4ce8a4f6e8b8580f808a95b41c05"),
        new SceneRoomExpectation("2102000003", "1367921344", "Library", "0a85e4fdd73e4714fabde63002a457e7"),
        new SceneRoomExpectation("2300000047", "2300000045", "Master Bedroom Suite", "6169353da08ead53db8fa10441e9ae49"),
        new SceneRoomExpectation("2102000001", "354156755", "Music Room", "028084782cdcf3d4ab3b596624c8b7c5"),
        new SceneRoomExpectation("2300000057", "2300000055", "Nursery", "5e685bb9c8917582f977493e3feed036"),
        new SceneRoomExpectation("2300000027", "2300000025", "Service Corridor", "63139e8fe55e5e00f97b08fe5f2b145b"),
        new SceneRoomExpectation("2300000037", "2300000035", "Side Stair Mudroom", "755ad9d5953d1f60cafbff7c71f5767f"),
        new SceneRoomExpectation("2300000042", "2300000040", "Upper Gallery", "1bbe0674f13938fcc9559b5fb933be83"),
        new SceneRoomExpectation("2300000052", "2300000050", "Upper Sitting Hall", "45a669872351e5ba0b2a6749ecc2065f")
    };

    private static readonly string[] ApprovedPassageAssetPathsInDatabaseOrder =
    {
        "Assets/_Chateau/Data/World/Passages/Passage_GEH_DrawingRoom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_DrawingRoom_GEH.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_DrawingRoom_MusicRoom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_MusicRoom_DrawingRoom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_MusicRoom_Library.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_Library_MusicRoom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_Library_Ballroom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_Ballroom_Library.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_GEH_DiningRoom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_DiningRoom_GEH.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_DiningRoom_ButlersPantry.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_ButlersPantry_DiningRoom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_ButlersPantry_BilliardRoom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_BilliardRoom_ButlersPantry.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_ButlersPantry_ServiceCorridor.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_ServiceCorridor_ButlersPantry.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_ServiceCorridor_Kitchen.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_Kitchen_ServiceCorridor.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_ServiceCorridor_Chapel.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_Chapel_ServiceCorridor.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_GrandEntranceHall_GrandEntranceHallRearView.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_GrandEntranceHallRearView_GrandEntranceHall.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_GrandEntranceHallRearView_BilliardRoom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_BilliardRoom_GrandEntranceHallRearView.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_GrandEntranceHallRearView_Conservatory.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_Conservatory_GrandEntranceHallRearView.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_ServiceCorridor_SideStairMudroom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_SideStairMudroom_ServiceCorridor.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_UpperSittingHall_SideStairMudroom.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_SideStairMudroom_UpperSittingHall.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_UpperGallery_UpperSittingHall.asset",
        "Assets/_Chateau/Data/World/Passages/Passage_UpperSittingHall_UpperGallery.asset"
    };

    private readonly List<UnityEngine.Object> transientObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = transientObjects.Count - 1; i >= 0; i--)
        {
            if (transientObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(transientObjects[i]);
            }
        }

        transientObjects.Clear();
    }

    [Test]
    public void AuthoredRoomCatalogContainsExactlyTheNineteenApprovedDefinitions()
    {
        CanonicalRoomDefinition[] rooms = LoadDefinitions<CanonicalRoomDefinition>(RoomsFolder);
        GameDatabase database = LoadDatabase();

        Assert.That(rooms, Has.Length.EqualTo(19));
        Assert.That(database.Definitions, Has.Count.EqualTo(51));
        Assert.That(database.RoomDefinitions, Has.Count.EqualTo(19));
        Assert.That(database.PassageDefinitions, Has.Count.EqualTo(32));
        Assert.That(database.PassageDefinitions.Select(definition => AssetDatabase.GetAssetPath(definition)),
            Is.EqualTo(ApprovedPassageAssetPathsInDatabaseOrder),
            "The canonical directed-passage catalog must remain in migration order through Group15.");
        Assert.That(rooms.Select(room => room.StableId),
            Is.EquivalentTo(ApprovedRooms.Select(room => room.StableId)));

        Dictionary<string, CanonicalRoomDefinition> roomsById =
            rooms.ToDictionary(room => room.StableId, StringComparer.Ordinal);

        foreach (RoomExpectation expected in ApprovedRooms)
        {
            Assert.That(roomsById.TryGetValue(expected.StableId, out CanonicalRoomDefinition room),
                Is.True,
                expected.StableId);
            Assert.That(AssetDatabase.GetAssetPath(room), Is.EqualTo(expected.AssetPath));
            Assert.That(room.SchemaVersion, Is.EqualTo(1), expected.StableId);
            Assert.That(room.DisplayName, Is.EqualTo(expected.DisplayName), expected.StableId);
            Assert.That(room.LegacyNames, Is.EqualTo(expected.LegacyNames), expected.StableId);
            Assert.That(AssetGuid(room.BackgroundTexture), Is.EqualTo(expected.BackgroundGuid), expected.StableId);
            Assert.That(AssetGuid(room.PerspectiveProfile), Is.EqualTo(expected.PerspectiveProfileGuid),
                expected.StableId);
            Assert.That(database.RoomDefinitions, Does.Contain(room), expected.StableId);
        }

        ValidationReport report = new ValidationReport();
        database.ValidateConfiguration(report);
        Assert.That(report.HasErrors, Is.False,
            string.Join("\n", report.Messages.Select(message => message.ToString())));
    }

    [Test]
    public void TypedRoomIndexResolvesAllNineteenRoomsAndRejectsUnknownIds()
    {
        GameDatabase database = LoadDatabase();

        foreach (CanonicalRoomDefinition room in LoadDefinitions<CanonicalRoomDefinition>(RoomsFolder))
        {
            Assert.That(room.TryGetId(out RoomId id), Is.True, room.name);
            Assert.That(database.TryGetRoomDefinition(id, out CanonicalRoomDefinition resolved),
                Is.True,
                room.name);
            Assert.That(resolved, Is.SameAs(room), room.name);
        }

        Assert.That(database.TryGetRoomDefinition(RoomId.Parse("room.not-authored"), out _), Is.False);
        Assert.That(database.TryGetRoomDefinition(default, out _), Is.False);
    }

    [Test]
    public void TypedPassageIndexResolvesAllThirtyTwoPassagesAndRejectsUnknownIds()
    {
        GameDatabase database = LoadDatabase();
        PassageDefinition[] passages = LoadDefinitions<PassageDefinition>(PassagesFolder);

        Assert.That(passages, Has.Length.EqualTo(32));

        foreach (PassageDefinition passage in passages)
        {
            Assert.That(passage.TryGetId(out PassageId id), Is.True, passage.name);
            Assert.That(database.TryGetPassageDefinition(id, out PassageDefinition resolved),
                Is.True,
                passage.name);
            Assert.That(resolved, Is.SameAs(passage), passage.name);
        }

        Assert.That(database.TryGetPassageDefinition(
            PassageId.Parse("passage.not-authored.nowhere"), out _), Is.False);
        Assert.That(database.TryGetPassageDefinition(default, out _), Is.False);
    }

    [Test]
    public void ApprovedDisplayAndLegacyAliasesResolveUniquelyAcrossRoomDefinitions()
    {
        IReadOnlyList<CanonicalRoomDefinition> rooms = LoadDatabase().RoomDefinitions;

        foreach (RoomExpectation expected in ApprovedRooms)
        {
            foreach (string alias in expected.LegacyNames.Prepend(expected.DisplayName))
            {
                CanonicalRoomDefinition[] matches = FindAliasMatches(rooms, alias);
                Assert.That(matches, Has.Length.EqualTo(1), alias);
                Assert.That(matches[0].StableId, Is.EqualTo(expected.StableId), alias);
            }
        }

        Assert.That(FindAliasMatches(rooms, "Side Stair & Mudroom").Single().StableId,
            Is.EqualTo("room.side-stair-mudroom"));
        Assert.That(FindAliasMatches(rooms, "Side Stair Mudroom").Single().StableId,
            Is.EqualTo("room.side-stair-mudroom"));
        Assert.That(FindAliasMatches(rooms, "Grand Entrance Hall Rear view").Single().StableId,
            Is.EqualTo("room.grand-entrance-hall-rear-view"));
        Assert.That(FindAliasMatches(rooms, "Butler's Pantry").Single().StableId,
            Is.EqualTo("room.butlers-pantry"));
        Assert.That(FindAliasMatches(rooms, "Not An Authored Room"), Is.Empty);
    }

    [Test]
    public void DuplicateStableIdsAreRejectedAndTypedLookupFailsClosed()
    {
        CanonicalRoomDefinition first = CreateRoom("FirstRoom", "room.duplicate", "First Room");
        CanonicalRoomDefinition second = CreateRoom("SecondRoom", "room.duplicate", "Second Room");
        GameDatabase database = CreateDatabase(first, second);
        ValidationReport report = new ValidationReport();

        database.ValidateConfiguration(report);

        Assert.That(report.Messages.Any(message =>
            message.Message.Contains("Duplicate definition ID 'room.duplicate'")), Is.True);
        Assert.That(database.RoomDefinitions, Has.Count.EqualTo(2));
        Assert.That(database.TryGetRoomDefinition(RoomId.Parse("room.duplicate"), out _), Is.False);
    }

    [Test]
    public void NullMissingAndMalformedDefinitionIdsAreRejectedWithoutThrowing()
    {
        CanonicalRoomDefinition missing = CreateRoom("MissingId", string.Empty, "Missing ID");
        CanonicalRoomDefinition malformed = CreateRoom("MalformedId", "Room.malformed", "Malformed ID");
        GameDatabase database = CreateDatabase(null, missing, malformed);
        ValidationReport report = new ValidationReport();

        Assert.DoesNotThrow(() => database.ValidateConfiguration(report));
        Assert.That(report.Messages.Any(message =>
            message.Message.Contains("definition slot 0 is null")), Is.True);
        Assert.That(report.Messages.Any(message =>
            message.Message.Contains("asset 'MissingId' has no stable ID")), Is.True);
        Assert.That(report.Messages.Any(message =>
            message.Message.Contains("stable ID must start with 'room.'")), Is.True);
        Assert.That(database.RoomDefinitions, Has.Count.EqualTo(2));
        Assert.That(database.TryGetRoomDefinition(default, out _), Is.False);
    }

    [Test]
    public void SameRoomAliasRepeatsAreAllowedButCrossRoomAliasAmbiguityIsRejected()
    {
        CanonicalRoomDefinition first = CreateRoom(
            "FirstRoom",
            "room.first",
            "First Room",
            "First Room",
            "Shared Hall");
        GameDatabase database = CreateDatabase(first);
        ValidationReport validReport = new ValidationReport();
        database.ValidateConfiguration(validReport);
        Assert.That(validReport.HasErrors, Is.False,
            string.Join("\n", validReport.Messages.Select(message => message.ToString())));

        CanonicalRoomDefinition second = CreateRoom(
            "SecondRoom",
            "room.second",
            "Second Room",
            "Second Room",
            "shared hall");
        SetDefinitions(database, first, second);
        ValidationReport conflictReport = new ValidationReport();
        database.ValidateConfiguration(conflictReport);

        Assert.That(conflictReport.Messages.Any(message =>
            message.Message.Contains("Room alias 'shared hall' is shared")), Is.True);
        Assert.That(FindAliasMatches(database.RoomDefinitions, "Shared Hall"), Has.Length.EqualTo(2));
    }

    [Test]
    public void PassageEndpointsAndReverseMustBeExactRegisteredDefinitions()
    {
        CanonicalRoomDefinition source = CreateRoom("Source", "room.source", "Source");
        CanonicalRoomDefinition destination = CreateRoom("Destination", "room.destination", "Destination");
        PassageDefinition forward = CreatePassage(
            "Forward",
            "passage.source.destination",
            source,
            destination);
        PassageDefinition reverse = CreatePassage(
            "Reverse",
            "passage.destination.source",
            destination,
            source);
        LinkReverse(forward, reverse);
        GameDatabase database = CreateDatabase(forward);
        ValidationReport missingReport = new ValidationReport();

        database.ValidateConfiguration(missingReport);

        Assert.That(missingReport.Messages.Count(message =>
            message.Message.Contains("is not registered in GameDatabase")), Is.EqualTo(3));
        Assert.That(missingReport.Messages.Any(message => message.Message.Contains("source room 'Source'")), Is.True);
        Assert.That(missingReport.Messages.Any(message =>
            message.Message.Contains("destination room 'Destination'")), Is.True);
        Assert.That(missingReport.Messages.Any(message => message.Message.Contains("reverse passage 'Reverse'")), Is.True);

        CanonicalRoomDefinition registeredSource =
            CreateRoom("RegisteredSource", "room.source", "Registered Source");
        SetDefinitions(database, registeredSource, destination, forward, reverse);
        ValidationReport identityReport = new ValidationReport();
        database.ValidateConfiguration(identityReport);

        Assert.That(identityReport.Messages.Count(message =>
            message.Message.Contains("source room 'Source'") ||
            message.Message.Contains("destination room 'Source'")), Is.EqualTo(2),
            "A different registered object with the same RoomId must not resolve a passage endpoint.");
    }

    [Test]
    public void RemainingDataOnlyDefinitionsLeaveSceneRoomAndRouteOwnershipExact()
    {
        string sceneText = File.ReadAllText(GameplayScenePath);

        Assert.That(CountOccurrences(sceneText, "\n--- !u!"), Is.EqualTo(6055));
        Assert.That(CountOccurrences(sceneText, $"guid: {RoomContentGroupGuid}"), Is.EqualTo(19));
        Assert.That(CountOccurrences(sceneText, $"guid: {RoomViewGuid}"), Is.EqualTo(16));
        Assert.That(CountOccurrences(sceneText, $"guid: {PassageGuid}"), Is.EqualTo(32));
        Assert.That(CountOccurrences(sceneText, $"guid: {LegacyDoorTriggerGuid}"), Is.EqualTo(45));
        Assert.That(CountOccurrences(sceneText, $"guid: {GameRootGuid}"), Is.EqualTo(1));

        foreach (SceneRoomExpectation expected in SceneRooms)
        {
            string document = ExtractDocument(sceneText, $"--- !u!114 &{expected.ComponentFileId}");
            Assert.That(document, Does.Contain($"m_GameObject: {{fileID: {expected.GameObjectFileId}}}"),
                expected.RoomName);
            Assert.That(document, Does.Contain($"roomName: {expected.RoomName}"), expected.RoomName);
            Assert.That(document, Does.Contain(
                $"roomBackgroundTexture: {{fileID: 2800000, guid: {expected.BackgroundGuid}, type: 3}}"),
                expected.RoomName);
            Assert.That(document, Does.Contain(expected.PerspectiveProfileGuid.Length == 0
                ? "perspectiveProfile: {fileID: 0}"
                : $"perspectiveProfile: {{fileID: 11400000, guid: {expected.PerspectiveProfileGuid}, type: 2}}"),
                expected.RoomName);
        }

        foreach (RoomExpectation expected in ApprovedRooms.Where(room => room.IsDataOnly))
        {
            string definitionGuid = AssetDatabase.AssetPathToGUID(expected.AssetPath);
            Assert.That(definitionGuid, Is.Not.Empty, expected.AssetPath);
            Assert.That(CountOccurrences(sceneText, $"guid: {definitionGuid}"), Is.Zero,
                $"{expected.StableId} must remain data-only until its dedicated room/navigation slice.");
        }
    }

    private GameDatabase CreateDatabase(params DefinitionAssetBase[] definitions)
    {
        GameDatabase database = Track(ScriptableObject.CreateInstance<GameDatabase>());
        database.name = "TransientGameDatabase";
        SetDefinitions(database, definitions);
        return database;
    }

    private CanonicalRoomDefinition CreateRoom(
        string name,
        string stableId,
        string displayName,
        params string[] legacyNames)
    {
        CanonicalRoomDefinition room = Track(ScriptableObject.CreateInstance<CanonicalRoomDefinition>());
        room.name = name;
        Texture2D background = Track(new Texture2D(2, 2));
        background.name = $"{name}Background";
        SetStableId(room, stableId);
        SetPrivateField(room, "displayName", displayName);
        SetPrivateField(room, "legacyNames",
            legacyNames == null || legacyNames.Length == 0 ? new[] { displayName } : legacyNames);
        SetPrivateField<Texture>(room, "backgroundTexture", background);
        return room;
    }

    private PassageDefinition CreatePassage(
        string name,
        string stableId,
        CanonicalRoomDefinition source,
        CanonicalRoomDefinition destination)
    {
        PassageDefinition passage = Track(ScriptableObject.CreateInstance<PassageDefinition>());
        passage.name = name;
        SetStableId(passage, stableId);
        SetPrivateField(passage, "sourceRoom", source);
        SetPrivateField(passage, "destinationRoom", destination);
        SetPrivateField(passage, "promptText", "Open Door");
        return passage;
    }

    private static void LinkReverse(PassageDefinition forward, PassageDefinition reverse)
    {
        SetPrivateField(forward, "reverse", reverse);
        SetPrivateField(reverse, "reverse", forward);
    }

    private static void SetDefinitions(GameDatabase database, params DefinitionAssetBase[] definitions)
    {
        SetPrivateField(database, "definitions", new List<DefinitionAssetBase>(definitions));
    }

    private static GameDatabase LoadDatabase()
    {
        GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
        Assert.That(database, Is.Not.Null);
        return database;
    }

    private static T[] LoadDefinitions<T>(string folder) where T : DefinitionAssetBase
    {
        return AssetDatabase.FindAssets(string.Empty, new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(definition => definition != null)
            .ToArray();
    }

    private static CanonicalRoomDefinition[] FindAliasMatches(
        IReadOnlyList<CanonicalRoomDefinition> rooms,
        string alias)
    {
        return rooms.Where(room => room != null && room.MatchesLegacyName(alias)).ToArray();
    }

    private static string AssetGuid(UnityEngine.Object asset)
    {
        return asset == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    }

    private static void SetStableId(DefinitionAssetBase definition, string stableId)
    {
        FieldInfo field = typeof(DefinitionAssetBase).GetField("stableId", PrivateInstance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(definition, stableId);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Missing field {target.GetType().Name}.{fieldName}");
        field.SetValue(target, value);
    }

    private T Track<T>(T item) where T : UnityEngine.Object
    {
        transientObjects.Add(item);
        return item;
    }

    private static string ExtractDocument(string text, string header)
    {
        int start = text.IndexOf(header, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing document {header}");
        int end = text.IndexOf("\n--- !u!", start + header.Length, StringComparison.Ordinal);
        return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;

        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class RoomExpectation
    {
        public RoomExpectation(
            string assetPath,
            string stableId,
            string displayName,
            string[] legacyNames,
            string backgroundGuid,
            string perspectiveProfileGuid = "",
            bool isDataOnly = false)
        {
            AssetPath = assetPath;
            StableId = stableId;
            DisplayName = displayName;
            LegacyNames = legacyNames;
            BackgroundGuid = backgroundGuid;
            PerspectiveProfileGuid = perspectiveProfileGuid;
            IsDataOnly = isDataOnly;
        }

        public string AssetPath { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public string[] LegacyNames { get; }
        public string BackgroundGuid { get; }
        public string PerspectiveProfileGuid { get; }
        public bool IsDataOnly { get; }
    }

    private sealed class SceneRoomExpectation
    {
        public SceneRoomExpectation(
            string componentFileId,
            string gameObjectFileId,
            string roomName,
            string backgroundGuid,
            string perspectiveProfileGuid = "")
        {
            ComponentFileId = componentFileId;
            GameObjectFileId = gameObjectFileId;
            RoomName = roomName;
            BackgroundGuid = backgroundGuid;
            PerspectiveProfileGuid = perspectiveProfileGuid;
        }

        public string ComponentFileId { get; }
        public string GameObjectFileId { get; }
        public string RoomName { get; }
        public string BackgroundGuid { get; }
        public string PerspectiveProfileGuid { get; }
    }
}
#endif
