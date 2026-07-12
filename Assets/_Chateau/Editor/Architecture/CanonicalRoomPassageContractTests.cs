#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Chateau.Architecture;
using Chateau.World.Navigation;
using Chateau.World.Rooms;
using Chateau.World.Rooms.Passages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using CanonicalRoomDefinition = Chateau.World.Rooms.RoomDefinition;

public sealed class CanonicalRoomPassageContractTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string EntranceRoomPath = "Assets/_Chateau/Data/World/Rooms/Room_GrandEntranceHall.asset";
    private const string DrawingRoomPath = "Assets/_Chateau/Data/World/Rooms/Room_DrawingRoom.asset";
    private const string MusicRoomPath = "Assets/_Chateau/Data/World/Rooms/Room_MusicRoom.asset";
    private const string LibraryRoomPath = "Assets/_Chateau/Data/World/Rooms/Room_Library.asset";
    private const string ForwardPassagePath = "Assets/_Chateau/Data/World/Passages/Passage_GEH_DrawingRoom.asset";
    private const string ReversePassagePath = "Assets/_Chateau/Data/World/Passages/Passage_DrawingRoom_GEH.asset";
    private const string DrawingMusicPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_DrawingRoom_MusicRoom.asset";
    private const string MusicDrawingPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_MusicRoom_DrawingRoom.asset";
    private const string MusicLibraryPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_MusicRoom_Library.asset";
    private const string LibraryMusicPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_Library_MusicRoom.asset";
    private const string GameDatabasePath = "Assets/_Chateau/Data/GameDatabase.asset";
    private const string LibraryRoomGuid = "8da3a3e936712e7b9f534786110323e4";
    private const string MusicLibraryPassageGuid = "aefe77f20874eb81b83fccb6ff5b8046";
    private const string LibraryMusicPassageGuid = "3a641d5febbfd7aec481ada678ba9fe4";

    [Test]
    public void CanonicalRouteDataViewsPassagesAndGroup02PassageBoundCertificationAreExact()
    {
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(EntranceRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(DrawingRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(MusicRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(LibraryRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ForwardPassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ReversePassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(DrawingMusicPassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(MusicDrawingPassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(MusicLibraryPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(LibraryMusicPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(GameDatabasePath), Is.EqualTo(typeof(GameDatabase)));

        CanonicalRoomDefinition entrance = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(EntranceRoomPath);
        CanonicalRoomDefinition drawing = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(DrawingRoomPath);
        CanonicalRoomDefinition music = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(MusicRoomPath);
        CanonicalRoomDefinition library = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(LibraryRoomPath);
        PassageDefinition forward = AssetDatabase.LoadAssetAtPath<PassageDefinition>(ForwardPassagePath);
        PassageDefinition reverse = AssetDatabase.LoadAssetAtPath<PassageDefinition>(ReversePassagePath);
        PassageDefinition drawingMusic = AssetDatabase.LoadAssetAtPath<PassageDefinition>(DrawingMusicPassagePath);
        PassageDefinition musicDrawing = AssetDatabase.LoadAssetAtPath<PassageDefinition>(MusicDrawingPassagePath);
        PassageDefinition musicLibrary = AssetDatabase.LoadAssetAtPath<PassageDefinition>(MusicLibraryPassagePath);
        PassageDefinition libraryMusic = AssetDatabase.LoadAssetAtPath<PassageDefinition>(LibraryMusicPassagePath);
        GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabasePath);

        Assert.That(entrance, Is.Not.Null);
        Assert.That(drawing, Is.Not.Null);
        Assert.That(music, Is.Not.Null);
        Assert.That(library, Is.Not.Null);
        Assert.That(forward, Is.Not.Null);
        Assert.That(reverse, Is.Not.Null);
        Assert.That(drawingMusic, Is.Not.Null);
        Assert.That(musicDrawing, Is.Not.Null);
        Assert.That(musicLibrary, Is.Not.Null);
        Assert.That(libraryMusic, Is.Not.Null);
        Assert.That(database, Is.Not.Null);

        Assert.That(AssetDatabase.AssetPathToGUID(EntranceRoomPath), Is.EqualTo("5e4e6adcd42c4058867aaa6c47b84de1"));
        Assert.That(AssetDatabase.AssetPathToGUID(DrawingRoomPath), Is.EqualTo("057575e9763145759aa12184580d27d8"));
        Assert.That(AssetDatabase.AssetPathToGUID(MusicRoomPath), Is.EqualTo("c0f34d74a30db58bb2b87b6ec316120b"));
        Assert.That(AssetDatabase.AssetPathToGUID(ForwardPassagePath), Is.EqualTo("0344228bb90d4997818e13c84f0bcf63"));
        Assert.That(AssetDatabase.AssetPathToGUID(ReversePassagePath), Is.EqualTo("50ae5112eed74cfda8588ff835b92516"));
        Assert.That(AssetDatabase.AssetPathToGUID(DrawingMusicPassagePath),
            Is.EqualTo("3167361ca4c671298c0e84f43320619b"));
        Assert.That(AssetDatabase.AssetPathToGUID(MusicDrawingPassagePath),
            Is.EqualTo("01544de8f55723585d60e5c0915345fd"));
        Assert.That(AssetDatabase.AssetPathToGUID(LibraryRoomPath), Is.EqualTo(LibraryRoomGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(MusicLibraryPassagePath),
            Is.EqualTo(MusicLibraryPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(LibraryMusicPassagePath),
            Is.EqualTo(LibraryMusicPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(GameDatabasePath), Is.EqualTo("6b7925c3057e11ad688e890ddb547110"));

        string[] definitionGuids = new[]
        {
            AssetDatabase.AssetPathToGUID(EntranceRoomPath),
            AssetDatabase.AssetPathToGUID(DrawingRoomPath),
            AssetDatabase.AssetPathToGUID(ForwardPassagePath),
            AssetDatabase.AssetPathToGUID(ReversePassagePath),
            AssetDatabase.AssetPathToGUID(MusicRoomPath),
            AssetDatabase.AssetPathToGUID(DrawingMusicPassagePath),
            AssetDatabase.AssetPathToGUID(MusicDrawingPassagePath),
            AssetDatabase.AssetPathToGUID(LibraryRoomPath),
            AssetDatabase.AssetPathToGUID(MusicLibraryPassagePath),
            AssetDatabase.AssetPathToGUID(LibraryMusicPassagePath)
        };
        Assert.That(definitionGuids.All(guid => !string.IsNullOrEmpty(guid)), Is.True);
        Assert.That(definitionGuids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(10));

        Assert.That(entrance.StableId, Is.EqualTo("room.grand-entrance-hall"));
        Assert.That(entrance.SchemaVersion, Is.EqualTo(1));
        Assert.That(entrance.DisplayName, Is.EqualTo("Grand Entrance Hall"));
        Assert.That(entrance.LegacyNames, Is.EqualTo(new[] { "Grand Entrance Hall" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(entrance.BackgroundTexture)),
            Is.EqualTo("3e163816317a638f5adedc338ec34d98"));
        Assert.That(entrance.PerspectiveProfile, Is.Null);

        Assert.That(drawing.StableId, Is.EqualTo("room.drawing-room"));
        Assert.That(drawing.SchemaVersion, Is.EqualTo(1));
        Assert.That(drawing.DisplayName, Is.EqualTo("Drawing Room"));
        Assert.That(drawing.LegacyNames, Is.EqualTo(new[] { "Drawing Room" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(drawing.BackgroundTexture)),
            Is.EqualTo("28c74b6dea1ed8e2c9c7d612355f9734"));
        Assert.That(drawing.PerspectiveProfile, Is.Null);

        Assert.That(music.StableId, Is.EqualTo("room.music-room"));
        Assert.That(music.SchemaVersion, Is.EqualTo(1));
        Assert.That(music.DisplayName, Is.EqualTo("Music Room"));
        Assert.That(music.LegacyNames, Is.EqualTo(new[] { "Music Room" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(music.BackgroundTexture)),
            Is.EqualTo("028084782cdcf3d4ab3b596624c8b7c5"));
        Assert.That(music.PerspectiveProfile, Is.Null);

        Assert.That(library.StableId, Is.EqualTo("room.library"));
        Assert.That(library.SchemaVersion, Is.EqualTo(1));
        Assert.That(library.DisplayName, Is.EqualTo("Library"));
        Assert.That(library.LegacyNames, Is.EqualTo(new[] { "Library" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(library.BackgroundTexture)),
            Is.EqualTo("0a85e4fdd73e4714fabde63002a457e7"));
        Assert.That(library.PerspectiveProfile, Is.Null);

        Assert.That(forward.StableId, Is.EqualTo("passage.grand-entrance-hall.drawing-room"));
        Assert.That(forward.SchemaVersion, Is.EqualTo(1));
        Assert.That(forward.SourceRoom, Is.SameAs(entrance));
        Assert.That(forward.DestinationRoom, Is.SameAs(drawing));
        Assert.That(forward.Reverse, Is.SameAs(reverse));
        Assert.That(forward.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(forward.PromptText, Is.EqualTo("Open Door"));
        Assert.That(forward.LegacyDoorId, Is.EqualTo("GEH_Drawing_Room"));

        Assert.That(reverse.StableId, Is.EqualTo("passage.drawing-room.grand-entrance-hall"));
        Assert.That(reverse.SchemaVersion, Is.EqualTo(1));
        Assert.That(reverse.SourceRoom, Is.SameAs(drawing));
        Assert.That(reverse.DestinationRoom, Is.SameAs(entrance));
        Assert.That(reverse.Reverse, Is.SameAs(forward));
        Assert.That(reverse.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(reverse.PromptText, Is.EqualTo("Open Door"));
        Assert.That(reverse.LegacyDoorId, Is.EqualTo("DrawingRoom_GEH"));

        Assert.That(drawingMusic.StableId, Is.EqualTo("passage.drawing-room.music-room"));
        Assert.That(drawingMusic.SchemaVersion, Is.EqualTo(1));
        Assert.That(drawingMusic.SourceRoom, Is.SameAs(drawing));
        Assert.That(drawingMusic.DestinationRoom, Is.SameAs(music));
        Assert.That(drawingMusic.Reverse, Is.SameAs(musicDrawing));
        Assert.That(drawingMusic.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(drawingMusic.PromptText, Is.EqualTo("Open Door"));
        Assert.That(drawingMusic.LegacyDoorId, Is.EqualTo("DrawingRoom_MusicRoom"));

        Assert.That(musicDrawing.StableId, Is.EqualTo("passage.music-room.drawing-room"));
        Assert.That(musicDrawing.SchemaVersion, Is.EqualTo(1));
        Assert.That(musicDrawing.SourceRoom, Is.SameAs(music));
        Assert.That(musicDrawing.DestinationRoom, Is.SameAs(drawing));
        Assert.That(musicDrawing.Reverse, Is.SameAs(drawingMusic));
        Assert.That(musicDrawing.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(musicDrawing.PromptText, Is.EqualTo("Open Door"));
        Assert.That(musicDrawing.LegacyDoorId, Is.EqualTo("MusicRoom_DrawingRoom"));

        Assert.That(musicLibrary.StableId, Is.EqualTo("passage.music-room.library"));
        Assert.That(musicLibrary.SchemaVersion, Is.EqualTo(1));
        Assert.That(musicLibrary.SourceRoom, Is.SameAs(music));
        Assert.That(musicLibrary.DestinationRoom, Is.SameAs(library));
        Assert.That(musicLibrary.Reverse, Is.SameAs(libraryMusic));
        Assert.That(musicLibrary.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(musicLibrary.PromptText, Is.EqualTo("Open Door"));
        Assert.That(musicLibrary.LegacyDoorId, Is.EqualTo("MusicRoom_Library"));

        Assert.That(libraryMusic.StableId, Is.EqualTo("passage.library.music-room"));
        Assert.That(libraryMusic.SchemaVersion, Is.EqualTo(1));
        Assert.That(libraryMusic.SourceRoom, Is.SameAs(library));
        Assert.That(libraryMusic.DestinationRoom, Is.SameAs(music));
        Assert.That(libraryMusic.Reverse, Is.SameAs(musicLibrary));
        Assert.That(libraryMusic.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(libraryMusic.PromptText, Is.EqualTo("Open Door"));
        Assert.That(libraryMusic.LegacyDoorId, Is.EqualTo("Library_MusicRoom"));

        Assert.That(database.Definitions, Has.Count.EqualTo(10));
        Assert.That(database.Definitions[0], Is.SameAs(entrance));
        Assert.That(database.Definitions[1], Is.SameAs(drawing));
        Assert.That(database.Definitions[2], Is.SameAs(forward));
        Assert.That(database.Definitions[3], Is.SameAs(reverse));
        Assert.That(database.Definitions[4], Is.SameAs(music));
        Assert.That(database.Definitions[5], Is.SameAs(drawingMusic));
        Assert.That(database.Definitions[6], Is.SameAs(musicDrawing));
        Assert.That(database.Definitions[7], Is.SameAs(library));
        Assert.That(database.Definitions[8], Is.SameAs(musicLibrary));
        Assert.That(database.Definitions[9], Is.SameAs(libraryMusic));

        string[] stableIds = database.Definitions.Select(definition => definition.StableId).ToArray();
        Assert.That(stableIds, Is.EqualTo(new[]
        {
            "room.grand-entrance-hall",
            "room.drawing-room",
            "passage.grand-entrance-hall.drawing-room",
            "passage.drawing-room.grand-entrance-hall",
            "room.music-room",
            "passage.drawing-room.music-room",
            "passage.music-room.drawing-room",
            "room.library",
            "passage.music-room.library",
            "passage.library.music-room"
        }));
        Assert.That(stableIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(10));

        string databaseText = File.ReadAllText(GameDatabasePath);
        foreach (string definitionGuid in definitionGuids)
        {
            Assert.That(CountOccurrences(databaseText, $"guid: {definitionGuid}"), Is.EqualTo(1),
                $"GameDatabase must register definition {definitionGuid} exactly once.");
        }

        ValidationReport report = new ValidationReport();
        database.ValidateConfiguration(report);
        Assert.That(report.HasErrors, Is.False, string.Join("\n", report.Messages.Select(message => message.ToString())));

        string gameplayText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        Assert.That(CountOccurrences(gameplayText, "\n--- !u!"), Is.EqualTo(6017));
        Assert.That(CountOccurrences(gameplayText, $"guid: {LibraryRoomGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {MusicLibraryPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {LibraryMusicPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, "guid: 3167361ca4c671298c0e84f43320619b"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, "guid: 01544de8f55723585d60e5c0915345fd"), Is.EqualTo(1));
        string entranceRoomObject = ExtractDocument(gameplayText, "--- !u!1 &567115833");
        string drawingRoomObject = ExtractDocument(gameplayText, "--- !u!1 &2300000005");
        string musicRoomObject = ExtractDocument(gameplayText, "--- !u!1 &354156755");
        string libraryRoomObject = ExtractDocument(gameplayText, "--- !u!1 &1367921344");
        string entranceView = ExtractDocument(gameplayText, "--- !u!114 &4100000001");
        string drawingView = ExtractDocument(gameplayText, "--- !u!114 &4100000002");
        string musicView = ExtractDocument(gameplayText, "--- !u!114 &4100000003");
        string libraryView = ExtractDocument(gameplayText, "--- !u!114 &4100000004");
        string gameRoot = ExtractDocument(gameplayText, "--- !u!114 &1878886998");
        string outboundObject = ExtractDocument(gameplayText, "--- !u!1 &109889176");
        string outboundTrigger = ExtractDocument(gameplayText, "--- !u!114 &109889178");
        string forwardPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000011");
        string reverseObject = ExtractDocument(gameplayText, "--- !u!1 &2300000100");
        string reverseTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000104");
        string reversePassage = ExtractDocument(gameplayText, "--- !u!114 &4100000012");
        string drawingMusicObject = ExtractDocument(gameplayText, "--- !u!1 &2300000095");
        string drawingMusicTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000099");
        string drawingMusicPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000013");
        string musicDrawingObject = ExtractDocument(gameplayText, "--- !u!1 &2300000085");
        string musicDrawingTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000089");
        string musicDrawingPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000014");
        string musicLibraryObject = ExtractDocument(gameplayText, "--- !u!1 &552135202");
        string musicLibraryTrigger = ExtractDocument(gameplayText, "--- !u!114 &552135204");
        string musicLibraryPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000015");
        string libraryMusicObject = ExtractDocument(gameplayText, "--- !u!1 &2300000075");
        string libraryMusicTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000079");
        string libraryMusicPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000016");
        string playerTransform = ExtractDocument(gameplayText, "--- !u!4 &81962843 stripped");

        Assert.That(CountOccurrences(gameplayText, "guid: ccd2f3bd803e45aa8a1174cc881d6dc0"), Is.EqualTo(4),
            "Entrance, Drawing, Music, and Library must be the only passive RoomView scene owners at this gate.");
        Assert.That(CountOccurrences(gameplayText, "guid: 518dad8adf634786a103bf4e76aa0881"), Is.EqualTo(6),
            "The two completed pairs and one passive Group 02 pair must be the only Passages at this gate.");
        Assert.That(CountOccurrences(gameplayText, "anchorMigrationStage:"), Is.EqualTo(6),
            "Every staged Passage must serialize exactly one explicit anchor-ownership mode.");
        Assert.That(CountOccurrences(gameplayText, "anchorMigrationStage: 0"), Is.EqualTo(2),
            "The passive Group 02 reciprocal pair must retain legacy sampling until its later ownership gates.");
        Assert.That(CountOccurrences(gameplayText, "anchorMigrationStage: 1"), Is.Zero,
            "No staged reciprocal pair may retain legacy approach sampling at this gate.");
        Assert.That(CountOccurrences(gameplayText, "anchorMigrationStage: 2"), Is.EqualTo(4),
            "Both completed reciprocal pairs must own their authored approach and arrival anchors.");

        Assert.That(entranceRoomObject, Does.Contain("- component: {fileID: 4100000001}"));
        Assert.That(drawingRoomObject, Does.Contain("- component: {fileID: 4100000002}"));
        Assert.That(musicRoomObject, Does.Contain("- component: {fileID: 4100000003}"));
        Assert.That(libraryRoomObject, Does.Contain("- component: {fileID: 4100000004}"));
        Assert.That(CountOccurrences(libraryRoomObject, "- component:"), Is.EqualTo(3));
        Assert.That(entranceView, Does.Contain("m_GameObject: {fileID: 567115833}"));
        Assert.That(entranceView, Does.Contain(
            "m_Script: {fileID: 11500000, guid: ccd2f3bd803e45aa8a1174cc881d6dc0, type: 3}"));
        Assert.That(entranceView, Does.Contain(
            "definition: {fileID: 11400000, guid: 5e4e6adcd42c4058867aaa6c47b84de1, type: 2}"));
        Assert.That(entranceView, Does.Contain("legacyContentGroup: {fileID: 2102000002}"));
        Assert.That(drawingView, Does.Contain("m_GameObject: {fileID: 2300000005}"));
        Assert.That(drawingView, Does.Contain(
            "m_Script: {fileID: 11500000, guid: ccd2f3bd803e45aa8a1174cc881d6dc0, type: 3}"));
        Assert.That(drawingView, Does.Contain(
            "definition: {fileID: 11400000, guid: 057575e9763145759aa12184580d27d8, type: 2}"));
        Assert.That(drawingView, Does.Contain("legacyContentGroup: {fileID: 2300000007}"));
        Assert.That(musicView, Does.Contain("m_GameObject: {fileID: 354156755}"));
        Assert.That(musicView, Does.Contain(
            "m_Script: {fileID: 11500000, guid: ccd2f3bd803e45aa8a1174cc881d6dc0, type: 3}"));
        Assert.That(musicView, Does.Contain(
            "definition: {fileID: 11400000, guid: c0f34d74a30db58bb2b87b6ec316120b, type: 2}"));
        Assert.That(musicView, Does.Contain("legacyContentGroup: {fileID: 2102000001}"));
        Assert.That(libraryView, Does.Contain("m_GameObject: {fileID: 1367921344}"));
        Assert.That(libraryView, Does.Contain(
            "m_Script: {fileID: 11500000, guid: ccd2f3bd803e45aa8a1174cc881d6dc0, type: 3}"));
        Assert.That(libraryView, Does.Contain(
            $"definition: {{fileID: 11400000, guid: {LibraryRoomGuid}, type: 2}}"));
        Assert.That(libraryView, Does.Contain("legacyContentGroup: {fileID: 2102000003}"));

        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000001}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000002}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000003}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000004}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000011}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000012}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000013}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000014}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000015}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000016}"), Is.EqualTo(1));
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000013}\n" +
            "  - {fileID: 4100000014}\n" +
            "  - {fileID: 4100000015}\n" +
            "  - {fileID: 4100000016}"),
            "The Group 02 passive pair must append after all certified Passages without reordering them.");
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000003}\n  - {fileID: 4100000004}\n  - {fileID: 4100000011}"),
            "The Library RoomView must follow the existing views without reordering certified Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000001"), Is.EqualTo(4),
            "The entrance RoomView should occur only on its owner, document header, GameRoot registration, and source Passage.");
        Assert.That(CountOccurrences(gameplayText, "4100000002"), Is.EqualTo(5),
            "The drawing-room RoomView should occur only on its owner, document header, GameRoot registration, and two source Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000003"), Is.EqualTo(5),
            "The Music RoomView should occur only on its owner, document header, GameRoot registration, and two source Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000004"), Is.EqualTo(4),
            "The Library RoomView should occur only on its owner, document header, GameRoot registration, and source Passage.");
        Assert.That(outboundObject, Does.Contain("- component: {fileID: 4100000011}"));
        Assert.That(reverseObject, Does.Contain("- component: {fileID: 4100000012}"));
        Assert.That(drawingMusicObject, Does.Contain("- component: {fileID: 4100000013}"));
        Assert.That(musicDrawingObject, Does.Contain("- component: {fileID: 4100000014}"));
        Assert.That(musicLibraryObject, Does.Contain("- component: {fileID: 4100000015}"));
        Assert.That(libraryMusicObject, Does.Contain("- component: {fileID: 4100000016}"));
        Assert.That(CountOccurrences(drawingMusicObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(musicDrawingObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(musicLibraryObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(libraryMusicObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(gameplayText, "4100000011"), Is.EqualTo(5),
            "The forward Passage should occur only on its owner, header, GameRoot registration, reciprocal link, and trigger caller binding.");
        Assert.That(CountOccurrences(gameplayText, "4100000012"), Is.EqualTo(5),
            "The reverse Passage should occur only on its owner, header, GameRoot registration, reciprocal link, and trigger caller binding.");
        Assert.That(CountOccurrences(gameplayText, "4100000013"), Is.EqualTo(5),
            "The Drawing-to-Music Passage should occur only on its owner, header, GameRoot registration, reciprocal link, and trigger caller binding.");
        Assert.That(CountOccurrences(gameplayText, "4100000014"), Is.EqualTo(5),
            "The Music-to-Drawing Passage should occur only on its owner, header, GameRoot registration, reciprocal link, and trigger caller binding.");
        Assert.That(CountOccurrences(gameplayText, "4100000015"), Is.EqualTo(4),
            "The Music-to-Library Passage should occur only on its owner, header, GameRoot registration, and reciprocal link.");
        Assert.That(CountOccurrences(gameplayText, "4100000016"), Is.EqualTo(4),
            "The Library-to-Music Passage should occur only on its owner, header, GameRoot registration, and reciprocal link.");
        Assert.That(CountOccurrences(gameplayText, "canonicalPassage: {fileID:"), Is.EqualTo(4),
            "Only the first two reciprocal routes may cut over to canonical traversal at this gate.");
        Assert.That(CountOccurrences(gameplayText, "player: {fileID: 81962843}"), Is.EqualTo(4),
            "Only the two dependency-bound reciprocal pairs may bind the exact Player transform at this gate.");
        Assert.That(CountOccurrences(gameplayText, "81962843"), Is.EqualTo(5),
            "The Player Transform proxy should occur only in its header and the four trigger bindings.");
        string[] legacyTriggerDocuments = gameplayText
            .Split(new[] { "\n--- !u!" }, StringSplitOptions.None)
            .Where(document => document.Contains("guid: 7e419b0f8f26d4f2d8d03e567fef4c52"))
            .ToArray();
        Assert.That(legacyTriggerDocuments, Has.Length.EqualTo(45));
        Assert.That(
            legacyTriggerDocuments.Count(document =>
                document.Contains("navigationManager: {fileID: 1878886997}") &&
                document.Contains("doorOpenAudioSource: {fileID: 2201000013}") &&
                document.Contains("player: {fileID: 81962843}") &&
                document.Contains("doorOpenSoundCatalog: {fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}")),
            Is.EqualTo(4),
            "Only the first two reciprocal routes may receive direct compatibility bindings at this gate.");
        Assert.That(
            legacyTriggerDocuments.Count(document =>
                document.Contains("navigationManager: {fileID: 0}") &&
                document.Contains("doorOpenAudioSource: {fileID: 0}") &&
                document.Contains("player: {fileID: 0}") &&
                document.Contains("doorOpenSoundCatalog: {fileID: 0}")),
            Is.EqualTo(41),
            "Every unmigrated legacy trigger must remain byte-semantically unbound until its own route slice.");
        Assert.That(
            legacyTriggerDocuments.All(document => document.Contains("stairwaySoundCatalog: {fileID: 0}")),
            Is.True,
            "The door-only binding slice must not mutate stairway audio ownership.");
        Assert.That(
            legacyTriggerDocuments.Count(document => document.Contains("canonicalPassage: {fileID:")),
            Is.EqualTo(4));
        Assert.That(
            legacyTriggerDocuments.Count(document => !document.Contains("canonicalPassage:")),
            Is.EqualTo(41),
            "Every unmigrated trigger must deserialize a null canonical edge and retain the legacy fallback.");
        Assert.That(playerTransform, Does.Contain(
            "m_CorrespondingSourceObject: {fileID: 7967904164350347880, guid: 3c2a23f8d68b2d05cace0338fba9a1d1, type: 3}"));
        Assert.That(playerTransform, Does.Contain("m_PrefabInstance: {fileID: 81962841}"));
        Assert.That(playerTransform, Does.Contain("m_PrefabAsset: {fileID: 0}"));

        AssertPassivePassageDocument(
            forwardPassage,
            "109889176",
            "0344228bb90d4997818e13c84f0bcf63",
            "4100000001",
            "4100000012",
            "{x: -7.75, y: -2.22}",
            "{x: 5.267176, y: -2.104616}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            reversePassage,
            "2300000100",
            "50ae5112eed74cfda8588ff835b92516",
            "4100000002",
            "4100000011",
            "{x: 5.267176, y: -2.104616}",
            "{x: -7.75, y: -2.22}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            drawingMusicPassage,
            "2300000095",
            "3167361ca4c671298c0e84f43320619b",
            "4100000002",
            "4100000014",
            "{x: -7.16, y: -1.78}",
            "{x: -7.94, y: -3.27}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            musicDrawingPassage,
            "2300000085",
            "01544de8f55723585d60e5c0915345fd",
            "4100000003",
            "4100000013",
            "{x: -7.94, y: -3.27}",
            "{x: -7.16, y: -1.78}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            musicLibraryPassage,
            "552135202",
            MusicLibraryPassageGuid,
            "4100000003",
            "4100000016",
            "{x: 7.439471, y: -2.846709}",
            "{x: -7.244175, y: -2.799095}",
            PassageAnchorMigrationStage.LegacySampling);
        AssertPassivePassageDocument(
            libraryMusicPassage,
            "2300000075",
            LibraryMusicPassageGuid,
            "4100000004",
            "4100000015",
            "{x: -7.244175, y: -2.799095}",
            "{x: 7.439471, y: -2.846709}",
            PassageAnchorMigrationStage.LegacySampling);

        Assert.That(drawingMusicTrigger, Does.Contain("canonicalPassage: {fileID: 4100000013}"));
        Assert.That(musicDrawingTrigger, Does.Contain("canonicalPassage: {fileID: 4100000014}"));
        foreach (string callerBoundTrigger in new[] { drawingMusicTrigger, musicDrawingTrigger })
        {
            Assert.That(callerBoundTrigger, Does.Contain("navigationManager: {fileID: 1878886997}"));
            Assert.That(callerBoundTrigger, Does.Contain("doorOpenAudioSource: {fileID: 2201000013}"));
            Assert.That(callerBoundTrigger, Does.Contain("player: {fileID: 81962843}"));
            Assert.That(callerBoundTrigger, Does.Contain(
                "doorOpenSoundCatalog: {fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}"));
            Assert.That(callerBoundTrigger, Does.Contain("stairwaySoundCatalog: {fileID: 0}"));
        }
        foreach (string passiveTrigger in new[] { musicLibraryTrigger, libraryMusicTrigger })
        {
            Assert.That(passiveTrigger, Does.Not.Contain("canonicalPassage:"));
            Assert.That(passiveTrigger, Does.Contain("navigationManager: {fileID: 0}"));
            Assert.That(passiveTrigger, Does.Contain("doorOpenAudioSource: {fileID: 0}"));
            Assert.That(passiveTrigger, Does.Contain("player: {fileID: 0}"));
            Assert.That(passiveTrigger, Does.Contain("doorOpenSoundCatalog: {fileID: 0}"));
            Assert.That(passiveTrigger, Does.Contain("stairwaySoundCatalog: {fileID: 0}"));
        }

        AssertLegacyDoorTriggerCompatibilityBound(
            outboundTrigger,
            "109889176",
            "Grand Entrance Hall",
            "GEH_Drawing_Room",
            "Drawing Room",
            "109889179",
            "4100000011");
        AssertLegacyDoorTriggerCompatibilityBound(
            reverseTrigger,
            "2300000100",
            "Drawing Room",
            "DrawingRoom_GEH",
            "Grand Entrance Hall",
            "2300000103",
            "4100000012");
    }

    [Test]
    public void RoomDefinitionSeparatesStableIdentityFromPresentationAndLegacyNames()
    {
        Texture2D background = new Texture2D(2, 2);
        CanonicalRoomDefinition room = CreateRoomDefinition(
            "EntranceDefinition",
            "  room.grand-entrance-hall  ",
            "  Grand Entrance Hall  ",
            background,
            "  GEH  ",
            "Grand Entrance Hall");

        try
        {
            Assert.That(room.StableId, Is.EqualTo("room.grand-entrance-hall"));
            Assert.That(room.DisplayName, Is.EqualTo("Grand Entrance Hall"));
            Assert.That(room.PrimaryLegacyName, Is.EqualTo("GEH"));
            Assert.That(room.BackgroundTexture, Is.SameAs(background));
            Assert.That(room.PerspectiveProfile, Is.Null);
            Assert.That(room.MatchesLegacyName("geh"), Is.True);
            Assert.That(room.MatchesLegacyName(" GRAND ENTRANCE HALL "), Is.True);
            Assert.That(room.MatchesLegacyName("Drawing Room"), Is.False);

            ValidationReport report = new ValidationReport();
            room.ValidateConfiguration(report);
            Assert.That(report.HasErrors, Is.False);

            SetStableId(room, "not-a-room-id");
            SetPrivateField(room, "displayName", " ");
            SetPrivateField<Texture>(room, "backgroundTexture", null);
            SetPrivateField(room, "legacyNames", new[] { "GEH", "geh", " " });
            report = new ValidationReport();
            room.ValidateConfiguration(report);

            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Messages.Any(message => message.Message.Contains("must start with 'room.'")), Is.True);
            Assert.That(report.Messages.Any(message => message.Message.Contains("no display name")), Is.True);
            Assert.That(report.Messages.Any(message => message.Message.Contains("requires a background texture")), Is.True);
            Assert.That(report.Messages.Any(message => message.Message.Contains("repeats legacy name")), Is.True);
            Assert.That(report.Messages.Any(message => message.Message.Contains("legacy-name slot 2 is empty")), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(background);
        }
    }

    [Test]
    public void PassageDefinitionsRequireDirectedReciprocalEndpointsWithoutRecursiveValidation()
    {
        Texture2D entranceBackground = new Texture2D(2, 2);
        Texture2D drawingBackground = new Texture2D(2, 2);
        CanonicalRoomDefinition entrance = CreateRoomDefinition(
            "EntranceDefinition",
            "room.grand-entrance-hall",
            "Grand Entrance Hall",
            entranceBackground,
            "Grand Entrance Hall");
        CanonicalRoomDefinition drawing = CreateRoomDefinition(
            "DrawingDefinition",
            "room.drawing-room",
            "Drawing Room",
            drawingBackground,
            "Drawing Room");
        PassageDefinition forward = CreatePassageDefinition(
            "ForwardPassage",
            "passage.grand-entrance-hall.drawing-room",
            entrance,
            drawing,
            "  Open Door  ",
            "  GEH_Drawing_Room  ");
        PassageDefinition reverse = CreatePassageDefinition(
            "ReversePassage",
            "passage.drawing-room.grand-entrance-hall",
            drawing,
            entrance,
            "Open Door",
            "DrawingRoom_GEH");

        try
        {
            SetPrivateField(forward, "reverse", reverse);
            SetPrivateField(reverse, "reverse", forward);

            ValidationReport forwardReport = new ValidationReport();
            ValidationReport reverseReport = new ValidationReport();
            forward.ValidateConfiguration(forwardReport);
            reverse.ValidateConfiguration(reverseReport);

            Assert.That(forwardReport.HasErrors, Is.False);
            Assert.That(reverseReport.HasErrors, Is.False);
            Assert.That(forward.SourceRoom, Is.SameAs(entrance));
            Assert.That(forward.DestinationRoom, Is.SameAs(drawing));
            Assert.That(forward.Reverse, Is.SameAs(reverse));
            Assert.That(forward.Kind, Is.EqualTo(PassageKind.Door));
            Assert.That(forward.PromptText, Is.EqualTo("Open Door"));
            Assert.That(forward.LegacyDoorId, Is.EqualTo("GEH_Drawing_Room"));

            SetPrivateField(reverse, "reverse", reverse);
            forwardReport = new ValidationReport();
            forward.ValidateConfiguration(forwardReport);
            Assert.That(forwardReport.Messages.Any(message => message.Message.Contains("link back")), Is.True);

            SetPrivateField(reverse, "reverse", forward);
            SetPrivateField(reverse, "sourceRoom", entrance);
            forwardReport = new ValidationReport();
            forward.ValidateConfiguration(forwardReport);
            Assert.That(forwardReport.Messages.Any(message => message.Message.Contains("swap its room endpoints")), Is.True);

            SetPrivateField(reverse, "sourceRoom", drawing);
            SetPrivateField(forward, "reverse", forward);
            forwardReport = new ValidationReport();
            forward.ValidateConfiguration(forwardReport);
            Assert.That(forwardReport.Messages.Any(message => message.Message.Contains("cannot reverse to itself")), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(forward);
            UnityEngine.Object.DestroyImmediate(reverse);
            UnityEngine.Object.DestroyImmediate(entrance);
            UnityEngine.Object.DestroyImmediate(drawing);
            UnityEngine.Object.DestroyImmediate(entranceBackground);
            UnityEngine.Object.DestroyImmediate(drawingBackground);
        }
    }

    [Test]
    public void RoomViewsAndPassagesArePassiveValidatedSceneBindings()
    {
        Texture2D entranceBackground = new Texture2D(2, 2);
        Texture2D drawingBackground = new Texture2D(2, 2);
        CanonicalRoomDefinition entranceDefinition = CreateRoomDefinition(
            "EntranceDefinition",
            "room.grand-entrance-hall",
            "Grand Entrance Hall",
            entranceBackground,
            "Grand Entrance Hall");
        CanonicalRoomDefinition drawingDefinition = CreateRoomDefinition(
            "DrawingDefinition",
            "room.drawing-room",
            "Drawing Room",
            drawingBackground,
            "Drawing Room");
        PassageDefinition forwardDefinition = CreatePassageDefinition(
            "ForwardDefinition",
            "passage.grand-entrance-hall.drawing-room",
            entranceDefinition,
            drawingDefinition,
            "Open Door",
            "GEH_Drawing_Room");
        PassageDefinition reverseDefinition = CreatePassageDefinition(
            "ReverseDefinition",
            "passage.drawing-room.grand-entrance-hall",
            drawingDefinition,
            entranceDefinition,
            "Open Door",
            "DrawingRoom_GEH");
        GameObject house = new GameObject("House");
        GameObject entranceObject = new GameObject("Room_Grand_Entrance_Hall");
        GameObject drawingObject = new GameObject("Room_Drawing_Room");
        GameObject forwardObject = new GameObject("Passage_GEH_DrawingRoom");
        GameObject reverseObject = new GameObject("Passage_DrawingRoom_GEH");

        try
        {
            SetPrivateField(forwardDefinition, "reverse", reverseDefinition);
            SetPrivateField(reverseDefinition, "reverse", forwardDefinition);
            entranceObject.transform.SetParent(house.transform, false);
            drawingObject.transform.SetParent(house.transform, false);
            forwardObject.transform.SetParent(entranceObject.transform, false);
            reverseObject.transform.SetParent(drawingObject.transform, false);

            RoomContentGroup entranceContent = entranceObject.AddComponent<RoomContentGroup>();
            RoomContentGroup drawingContent = drawingObject.AddComponent<RoomContentGroup>();
            RoomView entranceView = entranceObject.AddComponent<RoomView>();
            RoomView drawingView = drawingObject.AddComponent<RoomView>();
            Passage forward = forwardObject.AddComponent<Passage>();
            Passage reverse = reverseObject.AddComponent<Passage>();
            SetPrivateField(entranceView, "definition", entranceDefinition);
            SetPrivateField(entranceView, "legacyContentGroup", entranceContent);
            SetPrivateField(drawingView, "definition", drawingDefinition);
            SetPrivateField(drawingView, "legacyContentGroup", drawingContent);
            ConfigurePassage(
                forward,
                forwardDefinition,
                entranceView,
                reverse,
                new Vector2(-7.576081f, -1.986423f),
                new Vector2(5.267176f, -2.104616f));
            ConfigurePassage(
                reverse,
                reverseDefinition,
                drawingView,
                forward,
                new Vector2(5.280546f, -2.015396f),
                Vector2.zero);

            ValidationReport entranceReport = new ValidationReport();
            ValidationReport drawingReport = new ValidationReport();
            ValidationReport forwardReport = new ValidationReport();
            ValidationReport reverseReport = new ValidationReport();
            entranceView.ValidateConfiguration(entranceReport);
            drawingView.ValidateConfiguration(drawingReport);
            forward.ValidateConfiguration(forwardReport);
            reverse.ValidateConfiguration(reverseReport);

            Assert.That(entranceReport.HasErrors, Is.False);
            Assert.That(drawingReport.HasErrors, Is.False);
            Assert.That(forwardReport.HasErrors, Is.False);
            Assert.That(reverseReport.HasErrors, Is.False);
            Assert.That(entranceView.Root, Is.SameAs(entranceObject.transform));
            Assert.That(entranceView.LegacyContentGroup, Is.SameAs(entranceContent));
            Assert.That(forward.SourceRoomView, Is.SameAs(entranceView));
            Assert.That(forward.ReversePassage, Is.SameAs(reverse));
            Assert.That(forward.ArrivalAnchor.LogicalPosition, Is.EqualTo(new Vector2(5.267176f, -2.104616f)));
            Assert.That(reverse.ArrivalAnchor.LogicalPosition, Is.EqualTo(Vector2.zero),
                "Logical zero is valid authored anchor data when the anchor object is present.");
            Assert.That(forward.AnchorMigrationStage, Is.EqualTo(PassageAnchorMigrationStage.LegacySampling));
            Assert.That(reverse.AnchorMigrationStage, Is.EqualTo(forward.AnchorMigrationStage));
            Assert.That(forward.HasValidAnchorMigrationStage, Is.True);
            Assert.That(forward.UsesAuthoredArrival, Is.False);
            Assert.That(forward.UsesAuthoredApproach, Is.False);

            SetPrivateField(forward, "anchorMigrationStage", PassageAnchorMigrationStage.AuthoredArrival);
            SetPrivateField(reverse, "anchorMigrationStage", PassageAnchorMigrationStage.AuthoredArrival);
            Assert.That(forward.AnchorMigrationStage, Is.EqualTo(reverse.AnchorMigrationStage));
            Assert.That(forward.UsesAuthoredArrival, Is.True);
            Assert.That(forward.UsesAuthoredApproach, Is.False,
                "The arrival-owned gate must retain legacy approach sampling.");

            SetPrivateField(forward, "anchorMigrationStage", PassageAnchorMigrationStage.AuthoredAnchors);
            SetPrivateField(reverse, "anchorMigrationStage", PassageAnchorMigrationStage.AuthoredAnchors);
            Assert.That(forward.AnchorMigrationStage, Is.EqualTo(reverse.AnchorMigrationStage));
            Assert.That(forward.UsesAuthoredArrival, Is.True);
            Assert.That(forward.UsesAuthoredApproach, Is.True);

            SetPrivateField(reverse, "anchorMigrationStage", PassageAnchorMigrationStage.AuthoredArrival);
            forwardReport = new ValidationReport();
            forward.ValidateConfiguration(forwardReport);
            Assert.That(forwardReport.Messages.Any(message =>
                message.Message.Contains("reciprocal pair must share one anchor migration stage")), Is.True);
            SetPrivateField(reverse, "anchorMigrationStage", PassageAnchorMigrationStage.AuthoredAnchors);

            SetPrivateField(forward, "anchorMigrationStage", (PassageAnchorMigrationStage)99);
            forwardReport = new ValidationReport();
            forward.ValidateConfiguration(forwardReport);
            Assert.That(forward.HasValidAnchorMigrationStage, Is.False);
            Assert.That(forward.UsesAuthoredArrival, Is.False);
            Assert.That(forward.UsesAuthoredApproach, Is.False);
            Assert.That(forwardReport.Messages.Any(message =>
                message.Message.Contains("unknown anchor migration stage")), Is.True);
            SetPrivateField(forward, "anchorMigrationStage", PassageAnchorMigrationStage.LegacySampling);
            SetPrivateField(reverse, "anchorMigrationStage", PassageAnchorMigrationStage.LegacySampling);

            house.SetActive(false);
            Assert.That(entranceObject.activeSelf, Is.True);
            Assert.That(entranceObject.activeInHierarchy, Is.False);
            Assert.That(entranceView.IsVisible, Is.True,
                "RoomView reports the room root's owned activeSelf value, not an ancestor's state.");

            SetPrivateField(forward, "sourceRoomView", drawingView);
            forwardReport = new ValidationReport();
            forward.ValidateConfiguration(forwardReport);
            Assert.That(forwardReport.Messages.Any(message => message.Message.Contains("descendant")), Is.True);
            Assert.That(forwardReport.Messages.Any(message => message.Message.Contains("definition source room")), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(house);
            UnityEngine.Object.DestroyImmediate(forwardDefinition);
            UnityEngine.Object.DestroyImmediate(reverseDefinition);
            UnityEngine.Object.DestroyImmediate(entranceDefinition);
            UnityEngine.Object.DestroyImmediate(drawingDefinition);
            UnityEngine.Object.DestroyImmediate(entranceBackground);
            UnityEngine.Object.DestroyImmediate(drawingBackground);
        }
    }

    [Test]
    public void CanonicalContractsIntroduceNoSecondStateOwnerDiscoveryOrRuntimeMutation()
    {
        string roomDefinitionText = File.ReadAllText("Assets/_Chateau/Runtime/World/Rooms/RoomDefinition.cs");
        string roomViewText = File.ReadAllText("Assets/_Chateau/Runtime/World/Rooms/RoomView.cs");
        string passageDefinitionText = File.ReadAllText("Assets/_Chateau/Runtime/World/Rooms/Passages/PassageDefinition.cs");
        string anchorText = File.ReadAllText("Assets/_Chateau/Runtime/World/Rooms/Passages/PassageAnchorData.cs");
        string passageText = File.ReadAllText("Assets/_Chateau/Runtime/World/Rooms/Passages/Passage.cs");
        string interfaceText = File.ReadAllText("Assets/_Chateau/Runtime/World/Navigation/INavigationService.cs");
        string navigationManagerText = File.ReadAllText("Assets/Scripts/Navigation/RoomNavigationManager.cs");
        string doorTriggerText = File.ReadAllText("Assets/Scripts/Navigation/DoorTriggerNavigation.cs");
        string combinedText = string.Join(
            "\n",
            roomDefinitionText,
            roomViewText,
            passageDefinitionText,
            anchorText,
            passageText,
            interfaceText);

        string[] forbiddenRuntimePatterns =
        {
            "FindAnyObjectByType",
            "FindFirstObjectByType",
            "FindObjectsByType",
            "GameObject.Find",
            "Resources.Load",
            "new GameObject",
            "AddComponent<",
            "RuntimeInitializeOnLoadMethod",
            "static Instance"
        };

        for (int i = 0; i < forbiddenRuntimePatterns.Length; i++)
        {
            Assert.That(combinedText, Does.Not.Contain(forbiddenRuntimePatterns[i]));
        }

        Assert.That(roomViewText, Does.Not.Contain("SetVisible"));
        Assert.That(roomViewText, Does.Not.Match(@"\b(?:Awake|Start|OnEnable|OnDisable|Update|LateUpdate|FixedUpdate)\s*\("));
        Assert.That(passageText, Does.Not.Match(@"\b(?:Awake|Start|OnEnable|OnDisable|Update|LateUpdate|FixedUpdate)\s*\("));
        Assert.That(typeof(RoomView).IsSubclassOf(typeof(RoomElementBase)), Is.True);
        Assert.That(typeof(Passage).IsSubclassOf(typeof(RoomElementBase)), Is.True);
        Assert.That(Enum.GetValues(typeof(PassageAnchorMigrationStage))
                .Cast<PassageAnchorMigrationStage>()
                .Select(value => (int)value),
            Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(Enum.GetNames(typeof(PassageAnchorMigrationStage)), Is.EqualTo(new[]
        {
            nameof(PassageAnchorMigrationStage.LegacySampling),
            nameof(PassageAnchorMigrationStage.AuthoredArrival),
            nameof(PassageAnchorMigrationStage.AuthoredAnchors)
        }));
        Assert.That(
            typeof(Passage).GetFields(PrivateInstance)
                .Single(field => field.Name == "anchorMigrationStage").FieldType,
            Is.EqualTo(typeof(PassageAnchorMigrationStage)));
        Assert.That(typeof(Passage).GetProperty("AnchorMigrationStage")?.PropertyType,
            Is.EqualTo(typeof(PassageAnchorMigrationStage)));
        Assert.That(typeof(Passage).GetProperty("HasValidAnchorMigrationStage")?.PropertyType,
            Is.EqualTo(typeof(bool)));
        Assert.That(typeof(Passage).GetProperty("UsesAuthoredArrival")?.PropertyType, Is.EqualTo(typeof(bool)));
        Assert.That(typeof(Passage).GetProperty("UsesAuthoredApproach")?.PropertyType, Is.EqualTo(typeof(bool)));
        Assert.That(typeof(INavigationService).IsInterface, Is.True);
        Assert.That(typeof(INavigationService).GetProperty("CurrentRoomDefinition")?.PropertyType, Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(typeof(INavigationService).GetMethod("CanTraverse")?.ReturnType, Is.EqualTo(typeof(bool)));
        Assert.That(typeof(INavigationService).GetMethod("TryTraverse")?.ReturnType, Is.EqualTo(typeof(bool)));
        Assert.That(typeof(INavigationService).IsAssignableFrom(typeof(RoomNavigationManager)), Is.True,
            "The existing sole navigation owner should expose the canonical boundary without creating another service.");
        Assert.That(
            typeof(RoomNavigationManager).GetFields(PrivateInstance)
                .Count(field =>
                    field.FieldType == typeof(CanonicalRoomDefinition) ||
                    field.FieldType == typeof(Passage) ||
                    field.FieldType == typeof(INavigationService)),
            Is.Zero,
            "The compatibility facade must derive canonical state instead of introducing a second serialized or cached owner.");
        Assert.That(
            typeof(RoomNavigationManager).GetFields(PrivateInstance).Count(field => field.Name == "currentRoom"),
            Is.EqualTo(1));
        Assert.That(
            typeof(RoomNavigationManager).GetFields(PrivateInstance).Count(field => field.Name == "onCurrentRoomChanged"),
            Is.EqualTo(1));
        Assert.That(navigationManagerText, Does.Contain(
            "public class RoomNavigationManager : Chateau.Architecture.GameServiceBase, INavigationService"));
        Assert.That(navigationManagerText, Does.Contain(
            "public CanonicalRoomDefinition CurrentRoomDefinition => FindRegisteredRoomDefinition(currentRoom);"));
        Assert.That(navigationManagerText, Does.Contain("public bool CanTraverse(Passage passage)"));
        Assert.That(navigationManagerText, Does.Contain("public bool TryTraverse(Passage passage)"));
        Assert.That(navigationManagerText, Does.Contain("return MoveThroughCanonicalPassage(passage);"));
        Assert.That(navigationManagerText, Does.Contain("passage.HasValidAnchorMigrationStage"));
        Assert.That(navigationManagerText, Does.Contain("reverse.HasValidAnchorMigrationStage"));
        Assert.That(navigationManagerText, Does.Contain(
            "reverse.AnchorMigrationStage == passage.AnchorMigrationStage"));
        Assert.That(navigationManagerText, Does.Contain("Vector2 arrivalPosition = passage.ArrivalAnchor.LogicalPosition;"));
        Assert.That(navigationManagerText, Does.Contain("playerMovement.TryWarpToExact(arrivalPosition)"));
        Assert.That(navigationManagerText, Does.Contain(
            "(!passage.UsesAuthoredArrival || IsFinite(arrivalAnchor.LogicalPosition))"));
        Assert.That(navigationManagerText, Does.Contain(
            "(!passage.UsesAuthoredApproach || IsFinite(approachAnchor.LogicalPosition))"));
        Assert.That(navigationManagerText, Does.Contain("if (passage.UsesAuthoredArrival)"));
        Assert.That(navigationManagerText, Does.Contain("PlacePlayerAtCanonicalArrival(passage);"));
        Assert.That(navigationManagerText, Does.Contain("PlacePlayerAtDestinationDoor("));
        Assert.That(navigationManagerText, Does.Not.Contain("[SerializeField] private CanonicalRoomDefinition"));
        Assert.That(navigationManagerText, Does.Not.Contain("[SerializeField] private Passage"));
        Assert.That(doorTriggerText, Does.Contain("using Chateau.World.Navigation;"));
        Assert.That(doorTriggerText, Does.Contain("[SerializeField] private CanonicalPassage canonicalPassage;"));
        Assert.That(doorTriggerText, Does.Contain("INavigationService navigationService = navigationManager;"));
        Assert.That(doorTriggerText, Does.Contain("navigationService.TryTraverse(canonicalPassage)"));
        Assert.That(doorTriggerText, Does.Contain("TryFindTraversalApproachDestination"));
        Assert.That(doorTriggerText, Does.Contain("TryFindCanonicalApproachDestination"));
        Assert.That(doorTriggerText, Does.Contain(
            "if (canonicalPassage == null || !canonicalPassage.UsesAuthoredApproach)"));
        Assert.That(passageText, Does.Contain(
            "Passage reciprocal pair must share one anchor migration stage."));
        Assert.That(doorTriggerText, Does.Contain("canonicalPassage.ApproachAnchor.LogicalPosition"));
        Assert.That(doorTriggerText, Does.Contain(
            "navigationManager.MoveThroughInspectorDoor(SourceRoom, DoorName, DestinationRoom, requirePlayerInSourceRoom)"));
        Assert.That(
            typeof(DoorTriggerNavigation).GetFields(PrivateInstance).Count(field => field.FieldType == typeof(Passage)),
            Is.EqualTo(1));
        Assert.That(
            typeof(DoorTriggerNavigation).GetFields(PrivateInstance).Count(field => field.FieldType == typeof(INavigationService)),
            Is.Zero);

        GameObject unboundOwner = new GameObject("UnboundNavigationFacadeContract");

        try
        {
            RoomNavigationManager unboundFacade = unboundOwner.AddComponent<RoomNavigationManager>();
            Assert.That(unboundFacade.CurrentRoomDefinition, Is.Null);
            Assert.That(unboundFacade.CanTraverse(null), Is.False);
            Assert.That(unboundFacade.TryTraverse(null), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(unboundOwner);
        }

        string gameplayText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        Assert.That(CountOccurrences(gameplayText, "guid: ccd2f3bd803e45aa8a1174cc881d6dc0"), Is.EqualTo(4));
        Assert.That(CountOccurrences(gameplayText, "guid: 518dad8adf634786a103bf4e76aa0881"), Is.EqualTo(6));
    }

    private static void AssertPassivePassageDocument(
        string document,
        string gameObjectFileId,
        string definitionGuid,
        string sourceRoomViewFileId,
        string reversePassageFileId,
        string approachPosition,
        string arrivalPosition,
        PassageAnchorMigrationStage expectedAnchorMigrationStage)
    {
        Assert.That(document.TrimEnd('\r', '\n').Split('\n'), Has.Length.EqualTo(20),
            "Every serialized Passage document must retain the exact 20-line schema including its stage scalar.");
        Assert.That(document, Does.Contain($"m_GameObject: {{fileID: {gameObjectFileId}}}"));
        Assert.That(document, Does.Contain(
            "m_Script: {fileID: 11500000, guid: 518dad8adf634786a103bf4e76aa0881, type: 3}"));
        Assert.That(document, Does.Contain(
            $"definition: {{fileID: 11400000, guid: {definitionGuid}, type: 2}}"));
        Assert.That(document, Does.Contain($"sourceRoomView: {{fileID: {sourceRoomViewFileId}}}"));
        Assert.That(document, Does.Contain($"reversePassage: {{fileID: {reversePassageFileId}}}"));
        Assert.That(document, Does.Contain($"approachAnchor:\n    logicalPosition: {approachPosition}"));
        Assert.That(document, Does.Contain($"arrivalAnchor:\n    logicalPosition: {arrivalPosition}"));
        Assert.That(CountOccurrences(document, "anchorMigrationStage:"), Is.EqualTo(1));
        Assert.That(document, Does.Contain(
            $"anchorMigrationStage: {(int)expectedAnchorMigrationStage}"));
    }

    private static void AssertLegacyDoorTriggerCompatibilityBound(
        string document,
        string gameObjectFileId,
        string sourceRoom,
        string doorName,
        string destinationRoom,
        string imageFileId,
        string canonicalPassageFileId)
    {
        Assert.That(document, Does.Contain($"m_GameObject: {{fileID: {gameObjectFileId}}}"));
        Assert.That(document, Does.Contain(
            "m_Script: {fileID: 11500000, guid: 7e419b0f8f26d4f2d8d03e567fef4c52, type: 3}"));
        Assert.That(document, Does.Contain($"sourceRoom: {sourceRoom}"));
        Assert.That(document, Does.Contain($"doorName: {doorName}"));
        Assert.That(document, Does.Contain($"destinationRoom: {destinationRoom}"));
        Assert.That(document, Does.Contain("requirePlayerInSourceRoom: 1"));
        Assert.That(document, Does.Contain("useCameraSequence: 0"));
        Assert.That(document, Does.Contain("triggerKind: 0"));
        Assert.That(document, Does.Contain("stairwayDirection: 0"));
        Assert.That(document, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(document, Does.Contain($"canonicalPassage: {{fileID: {canonicalPassageFileId}}}"));
        Assert.That(document, Does.Contain($"image: {{fileID: {imageFileId}}}"));
        Assert.That(document, Does.Contain("doorOpenAudioSource: {fileID: 2201000013}"));
        Assert.That(document, Does.Contain("player: {fileID: 81962843}"));
        Assert.That(document, Does.Contain("requirePlayerProximity: 1"));
        Assert.That(document, Does.Contain("walkPlayerToTriggerWhenFar: 1"));
        Assert.That(document, Does.Contain("autoActivateAfterApproach: 1"));
        Assert.That(document, Does.Contain("maxPlayerScreenDistance: 145"));
        Assert.That(document, Does.Contain(
            "doorOpenSoundCatalog: {fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}"));
        Assert.That(document, Does.Contain("stairwaySoundCatalog: {fileID: 0}"));
    }

    private static int CountOccurrences(string text, string value)
    {
        return text.Split(new[] { value }, StringSplitOptions.None).Length - 1;
    }

    private static string ExtractDocument(string assetText, string header)
    {
        int start = assetText.IndexOf(header, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing document '{header}'.");
        int end = assetText.IndexOf("\n--- !u!", start + header.Length, StringComparison.Ordinal);
        return end >= 0 ? assetText.Substring(start, end - start) : assetText.Substring(start);
    }

    private static CanonicalRoomDefinition CreateRoomDefinition(
        string assetName,
        string stableId,
        string displayName,
        Texture background,
        params string[] legacyNames)
    {
        CanonicalRoomDefinition definition = ScriptableObject.CreateInstance<CanonicalRoomDefinition>();
        definition.name = assetName;
        SetStableId(definition, stableId);
        SetPrivateField(definition, "displayName", displayName);
        SetPrivateField(definition, "backgroundTexture", background);
        SetPrivateField(definition, "legacyNames", legacyNames ?? Array.Empty<string>());
        return definition;
    }

    private static PassageDefinition CreatePassageDefinition(
        string assetName,
        string stableId,
        CanonicalRoomDefinition source,
        CanonicalRoomDefinition destination,
        string promptText,
        string legacyDoorId)
    {
        PassageDefinition definition = ScriptableObject.CreateInstance<PassageDefinition>();
        definition.name = assetName;
        SetStableId(definition, stableId);
        SetPrivateField(definition, "sourceRoom", source);
        SetPrivateField(definition, "destinationRoom", destination);
        SetPrivateField(definition, "kind", PassageKind.Door);
        SetPrivateField(definition, "promptText", promptText);
        SetPrivateField(definition, "legacyDoorId", legacyDoorId);
        return definition;
    }

    private static void ConfigurePassage(
        Passage passage,
        PassageDefinition definition,
        RoomView sourceRoomView,
        Passage reverse,
        Vector2 approachPosition,
        Vector2 arrivalPosition)
    {
        PassageAnchorData approach = new PassageAnchorData();
        PassageAnchorData arrival = new PassageAnchorData();
        SetPrivateField(approach, "logicalPosition", approachPosition);
        SetPrivateField(arrival, "logicalPosition", arrivalPosition);
        SetPrivateField(passage, "definition", definition);
        SetPrivateField(passage, "sourceRoomView", sourceRoomView);
        SetPrivateField(passage, "reversePassage", reverse);
        SetPrivateField(passage, "approachAnchor", approach);
        SetPrivateField(passage, "arrivalAnchor", arrival);
    }

    private static void SetStableId(DefinitionAssetBase definition, string stableId)
    {
        SerializedObject serializedDefinition = new SerializedObject(definition);
        SerializedProperty stableIdProperty = serializedDefinition.FindProperty("stableId");
        Assert.That(stableIdProperty, Is.Not.Null);
        stableIdProperty.stringValue = stableId;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetPrivateField<T>(object owner, string fieldName, T value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on {owner.GetType().Name}.");
        field.SetValue(owner, value);
    }
}
#endif
