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
    private const string BallroomRoomPath = "Assets/_Chateau/Data/World/Rooms/Room_Ballroom.asset";
    private const string DiningRoomPath = "Assets/_Chateau/Data/World/Rooms/Room_DiningRoom.asset";
    private const string ButlersPantryRoomPath =
        "Assets/_Chateau/Data/World/Rooms/Room_ButlersPantry.asset";
    private const string BilliardRoomPath =
        "Assets/_Chateau/Data/World/Rooms/Room_BilliardRoom.asset";
    private const string ServiceCorridorRoomPath =
        "Assets/_Chateau/Data/World/Rooms/Room_ServiceCorridor.asset";
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
    private const string LibraryBallroomPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_Library_Ballroom.asset";
    private const string BallroomLibraryPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_Ballroom_Library.asset";
    private const string EntranceDiningPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_GEH_DiningRoom.asset";
    private const string DiningEntrancePassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_DiningRoom_GEH.asset";
    private const string DiningButlersPantryPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_DiningRoom_ButlersPantry.asset";
    private const string ButlersPantryDiningPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_ButlersPantry_DiningRoom.asset";
    private const string ButlersPantryBilliardPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_ButlersPantry_BilliardRoom.asset";
    private const string BilliardButlersPantryPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_BilliardRoom_ButlersPantry.asset";
    private const string ButlersPantryServiceCorridorPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_ButlersPantry_ServiceCorridor.asset";
    private const string ServiceCorridorButlersPantryPassagePath =
        "Assets/_Chateau/Data/World/Passages/Passage_ServiceCorridor_ButlersPantry.asset";
    private const string GameDatabasePath = "Assets/_Chateau/Data/GameDatabase.asset";
    private const string LibraryRoomGuid = "8da3a3e936712e7b9f534786110323e4";
    private const string MusicLibraryPassageGuid = "aefe77f20874eb81b83fccb6ff5b8046";
    private const string LibraryMusicPassageGuid = "3a641d5febbfd7aec481ada678ba9fe4";
    private const string BallroomRoomGuid = "d3b02ee2732843d484037af98d0e53e7";
    private const string LibraryBallroomPassageGuid = "1de38005c66d42e2b2f1a65c59ce8ad8";
    private const string BallroomLibraryPassageGuid = "0c60f4c2fe6f4e45947fc2a200cc6053";
    private const string DiningRoomGuid = "0eb3282aded74fc4889f4321df8c5258";
    private const string EntranceDiningPassageGuid = "30b5c4cfef2b45e2970b4cdac4b7a3ef";
    private const string DiningEntrancePassageGuid = "94e16c6eca714188bced397612d48fff";
    private const string ButlersPantryRoomGuid = "f2e9016bf08c45ebba8600eabc9e0b4d";
    private const string DiningButlersPantryPassageGuid = "1dedaedb6c544e9e8ca4fd2a5be912cf";
    private const string ButlersPantryDiningPassageGuid = "d42e018868914021a713f19df8fe60e8";
    private const string BilliardRoomGuid = "bed158a9affd015fcc961340d9be5dd8";
    private const string ButlersPantryBilliardPassageGuid = "71ea8ce4d4eb8fa7f107abe24d7c903e";
    private const string BilliardButlersPantryPassageGuid = "be2f1b94b724dcfa061876e33bce02ca";
    private const string ServiceCorridorRoomGuid = "85d51b6fcb4840458d45f66bbf6c233b";
    private const string ButlersPantryServiceCorridorPassageGuid = "1b2d5f64523942a08e10402e24e88738";
    private const string ServiceCorridorButlersPantryPassageGuid = "b485e8a6f574414a84f77437e02147f1";

    [Test]
    public void CanonicalRouteDataViewsPassagesAndGroup07CompleteCertificationAreExact()
    {
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(EntranceRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(DrawingRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(MusicRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(LibraryRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(BallroomRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(DiningRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ButlersPantryRoomPath),
            Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(BilliardRoomPath),
            Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ServiceCorridorRoomPath),
            Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ForwardPassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ReversePassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(DrawingMusicPassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(MusicDrawingPassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(MusicLibraryPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(LibraryMusicPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(LibraryBallroomPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(BallroomLibraryPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(EntranceDiningPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(DiningEntrancePassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(DiningButlersPantryPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ButlersPantryDiningPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ButlersPantryBilliardPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(BilliardButlersPantryPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ButlersPantryServiceCorridorPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ServiceCorridorButlersPantryPassagePath),
            Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(GameDatabasePath), Is.EqualTo(typeof(GameDatabase)));

        CanonicalRoomDefinition entrance = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(EntranceRoomPath);
        CanonicalRoomDefinition drawing = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(DrawingRoomPath);
        CanonicalRoomDefinition music = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(MusicRoomPath);
        CanonicalRoomDefinition library = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(LibraryRoomPath);
        CanonicalRoomDefinition ballroom = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(BallroomRoomPath);
        CanonicalRoomDefinition dining = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(DiningRoomPath);
        CanonicalRoomDefinition butlersPantry =
            AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(ButlersPantryRoomPath);
        CanonicalRoomDefinition billiard =
            AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(BilliardRoomPath);
        CanonicalRoomDefinition serviceCorridor =
            AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(ServiceCorridorRoomPath);
        PassageDefinition forward = AssetDatabase.LoadAssetAtPath<PassageDefinition>(ForwardPassagePath);
        PassageDefinition reverse = AssetDatabase.LoadAssetAtPath<PassageDefinition>(ReversePassagePath);
        PassageDefinition drawingMusic = AssetDatabase.LoadAssetAtPath<PassageDefinition>(DrawingMusicPassagePath);
        PassageDefinition musicDrawing = AssetDatabase.LoadAssetAtPath<PassageDefinition>(MusicDrawingPassagePath);
        PassageDefinition musicLibrary = AssetDatabase.LoadAssetAtPath<PassageDefinition>(MusicLibraryPassagePath);
        PassageDefinition libraryMusic = AssetDatabase.LoadAssetAtPath<PassageDefinition>(LibraryMusicPassagePath);
        PassageDefinition libraryBallroom =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(LibraryBallroomPassagePath);
        PassageDefinition ballroomLibrary =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(BallroomLibraryPassagePath);
        PassageDefinition entranceDining =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(EntranceDiningPassagePath);
        PassageDefinition diningEntrance =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(DiningEntrancePassagePath);
        PassageDefinition diningButlersPantry =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(DiningButlersPantryPassagePath);
        PassageDefinition butlersPantryDining =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(ButlersPantryDiningPassagePath);
        PassageDefinition butlersPantryBilliard =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(ButlersPantryBilliardPassagePath);
        PassageDefinition billiardButlersPantry =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(BilliardButlersPantryPassagePath);
        PassageDefinition butlersPantryServiceCorridor =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(ButlersPantryServiceCorridorPassagePath);
        PassageDefinition serviceCorridorButlersPantry =
            AssetDatabase.LoadAssetAtPath<PassageDefinition>(ServiceCorridorButlersPantryPassagePath);
        GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabasePath);

        Assert.That(entrance, Is.Not.Null);
        Assert.That(drawing, Is.Not.Null);
        Assert.That(music, Is.Not.Null);
        Assert.That(library, Is.Not.Null);
        Assert.That(ballroom, Is.Not.Null);
        Assert.That(dining, Is.Not.Null);
        Assert.That(butlersPantry, Is.Not.Null);
        Assert.That(billiard, Is.Not.Null);
        Assert.That(serviceCorridor, Is.Not.Null);
        Assert.That(forward, Is.Not.Null);
        Assert.That(reverse, Is.Not.Null);
        Assert.That(drawingMusic, Is.Not.Null);
        Assert.That(musicDrawing, Is.Not.Null);
        Assert.That(musicLibrary, Is.Not.Null);
        Assert.That(libraryMusic, Is.Not.Null);
        Assert.That(libraryBallroom, Is.Not.Null);
        Assert.That(ballroomLibrary, Is.Not.Null);
        Assert.That(entranceDining, Is.Not.Null);
        Assert.That(diningEntrance, Is.Not.Null);
        Assert.That(diningButlersPantry, Is.Not.Null);
        Assert.That(butlersPantryDining, Is.Not.Null);
        Assert.That(butlersPantryBilliard, Is.Not.Null);
        Assert.That(billiardButlersPantry, Is.Not.Null);
        Assert.That(butlersPantryServiceCorridor, Is.Not.Null);
        Assert.That(serviceCorridorButlersPantry, Is.Not.Null);
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
        Assert.That(AssetDatabase.AssetPathToGUID(BallroomRoomPath), Is.EqualTo(BallroomRoomGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(LibraryBallroomPassagePath),
            Is.EqualTo(LibraryBallroomPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(BallroomLibraryPassagePath),
            Is.EqualTo(BallroomLibraryPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(DiningRoomPath), Is.EqualTo(DiningRoomGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(EntranceDiningPassagePath),
            Is.EqualTo(EntranceDiningPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(DiningEntrancePassagePath),
            Is.EqualTo(DiningEntrancePassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(ButlersPantryRoomPath), Is.EqualTo(ButlersPantryRoomGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(DiningButlersPantryPassagePath),
            Is.EqualTo(DiningButlersPantryPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(ButlersPantryDiningPassagePath),
            Is.EqualTo(ButlersPantryDiningPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(BilliardRoomPath), Is.EqualTo(BilliardRoomGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(ButlersPantryBilliardPassagePath),
            Is.EqualTo(ButlersPantryBilliardPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(BilliardButlersPantryPassagePath),
            Is.EqualTo(BilliardButlersPantryPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(ServiceCorridorRoomPath), Is.EqualTo(ServiceCorridorRoomGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(ButlersPantryServiceCorridorPassagePath),
            Is.EqualTo(ButlersPantryServiceCorridorPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(ServiceCorridorButlersPantryPassagePath),
            Is.EqualTo(ServiceCorridorButlersPantryPassageGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(GameDatabasePath), Is.EqualTo("6b7925c3057e11ad688e890ddb547110"));

        string[] completedRoomPaths =
        {
            "Assets/_Chateau/Data/World/Rooms/Room_ServiceCorridor.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_Kitchen.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_Chapel.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_GrandEntranceHallRearView.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_Conservatory.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_SideStairMudroom.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_UpperSittingHall.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_UpperGallery.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_MasterBedroomSuite.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_Nursery.asset",
            "Assets/_Chateau/Data/World/Rooms/Room_BlueBedroom.asset"
        };
        CanonicalRoomDefinition[] completedRooms = completedRoomPaths
            .Select(AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>)
            .ToArray();
        Assert.That(completedRooms.All(room => room != null), Is.True);

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
            AssetDatabase.AssetPathToGUID(LibraryMusicPassagePath),
            AssetDatabase.AssetPathToGUID(BallroomRoomPath),
            AssetDatabase.AssetPathToGUID(LibraryBallroomPassagePath),
            AssetDatabase.AssetPathToGUID(BallroomLibraryPassagePath),
            AssetDatabase.AssetPathToGUID(DiningRoomPath),
            AssetDatabase.AssetPathToGUID(EntranceDiningPassagePath),
            AssetDatabase.AssetPathToGUID(DiningEntrancePassagePath),
            AssetDatabase.AssetPathToGUID(ButlersPantryRoomPath),
            AssetDatabase.AssetPathToGUID(DiningButlersPantryPassagePath),
            AssetDatabase.AssetPathToGUID(ButlersPantryDiningPassagePath),
            AssetDatabase.AssetPathToGUID(BilliardRoomPath),
            AssetDatabase.AssetPathToGUID(ButlersPantryBilliardPassagePath),
            AssetDatabase.AssetPathToGUID(BilliardButlersPantryPassagePath),
            AssetDatabase.AssetPathToGUID(ButlersPantryServiceCorridorPassagePath),
            AssetDatabase.AssetPathToGUID(ServiceCorridorButlersPantryPassagePath)
        }.Concat(completedRoomPaths.Select(AssetDatabase.AssetPathToGUID)).ToArray();
        Assert.That(definitionGuids.All(guid => !string.IsNullOrEmpty(guid)), Is.True);
        Assert.That(definitionGuids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(35));

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

        Assert.That(ballroom.StableId, Is.EqualTo("room.ballroom"));
        Assert.That(ballroom.SchemaVersion, Is.EqualTo(1));
        Assert.That(ballroom.DisplayName, Is.EqualTo("Ballroom"));
        Assert.That(ballroom.LegacyNames, Is.EqualTo(new[] { "Ballroom" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(ballroom.BackgroundTexture)),
            Is.EqualTo("7dabdfc97f536fe458e28ca413b0a0fa"));
        Assert.That(ballroom.PerspectiveProfile, Is.Null);

        Assert.That(dining.StableId, Is.EqualTo("room.dining-room"));
        Assert.That(dining.SchemaVersion, Is.EqualTo(1));
        Assert.That(dining.DisplayName, Is.EqualTo("Dining Room"));
        Assert.That(dining.LegacyNames, Is.EqualTo(new[] { "Dining Room" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(dining.BackgroundTexture)),
            Is.EqualTo("004ab4cca930d0387892725fe69b4f72"));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(dining.PerspectiveProfile)),
            Is.EqualTo("a63248cfbd6b4a72af45c62cff7e94d0"));

        Assert.That(butlersPantry.StableId, Is.EqualTo("room.butlers-pantry"));
        Assert.That(butlersPantry.SchemaVersion, Is.EqualTo(1));
        Assert.That(butlersPantry.DisplayName, Is.EqualTo("Butlers Pantry"));
        Assert.That(butlersPantry.LegacyNames, Is.EqualTo(new[] { "Butlers Pantry", "Butler's Pantry" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(butlersPantry.BackgroundTexture)),
            Is.EqualTo("e73e44419d3782452bb6abd0e8edd452"));
        Assert.That(butlersPantry.PerspectiveProfile, Is.Null);

        Assert.That(billiard.StableId, Is.EqualTo("room.billiard-room"));
        Assert.That(billiard.SchemaVersion, Is.EqualTo(1));
        Assert.That(billiard.DisplayName, Is.EqualTo("Billiard Room"));
        Assert.That(billiard.LegacyNames, Is.EqualTo(new[] { "Billiard Room" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(billiard.BackgroundTexture)),
            Is.EqualTo("5987c5a8b3a09fc1ca848ac0ece03658"));
        Assert.That(billiard.PerspectiveProfile, Is.Null);

        Assert.That(serviceCorridor.StableId, Is.EqualTo("room.service-corridor"));
        Assert.That(serviceCorridor.SchemaVersion, Is.EqualTo(1));
        Assert.That(serviceCorridor.DisplayName, Is.EqualTo("Service Corridor"));
        Assert.That(serviceCorridor.LegacyNames, Is.EqualTo(new[] { "Service Corridor" }));
        Assert.That(
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(serviceCorridor.BackgroundTexture)),
            Is.EqualTo("63139e8fe55e5e00f97b08fe5f2b145b"));
        Assert.That(serviceCorridor.PerspectiveProfile, Is.Null);

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

        Assert.That(libraryBallroom.StableId, Is.EqualTo("passage.library.ballroom"));
        Assert.That(libraryBallroom.SchemaVersion, Is.EqualTo(1));
        Assert.That(libraryBallroom.SourceRoom, Is.SameAs(library));
        Assert.That(libraryBallroom.DestinationRoom, Is.SameAs(ballroom));
        Assert.That(libraryBallroom.Reverse, Is.SameAs(ballroomLibrary));
        Assert.That(libraryBallroom.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(libraryBallroom.PromptText, Is.EqualTo("Open Door"));
        Assert.That(libraryBallroom.LegacyDoorId, Is.EqualTo("Library_Ballroom"));

        Assert.That(ballroomLibrary.StableId, Is.EqualTo("passage.ballroom.library"));
        Assert.That(ballroomLibrary.SchemaVersion, Is.EqualTo(1));
        Assert.That(ballroomLibrary.SourceRoom, Is.SameAs(ballroom));
        Assert.That(ballroomLibrary.DestinationRoom, Is.SameAs(library));
        Assert.That(ballroomLibrary.Reverse, Is.SameAs(libraryBallroom));
        Assert.That(ballroomLibrary.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(ballroomLibrary.PromptText, Is.EqualTo("Open Door"));
        Assert.That(ballroomLibrary.LegacyDoorId, Is.EqualTo("Ballroom_Library"));

        Assert.That(entranceDining.StableId, Is.EqualTo("passage.grand-entrance-hall.dining-room"));
        Assert.That(entranceDining.SchemaVersion, Is.EqualTo(1));
        Assert.That(entranceDining.SourceRoom, Is.SameAs(entrance));
        Assert.That(entranceDining.DestinationRoom, Is.SameAs(dining));
        Assert.That(entranceDining.Reverse, Is.SameAs(diningEntrance));
        Assert.That(entranceDining.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(entranceDining.PromptText, Is.EqualTo("Open Door"));
        Assert.That(entranceDining.LegacyDoorId, Is.EqualTo("GEH_DiningRoom"));

        Assert.That(diningEntrance.StableId, Is.EqualTo("passage.dining-room.grand-entrance-hall"));
        Assert.That(diningEntrance.SchemaVersion, Is.EqualTo(1));
        Assert.That(diningEntrance.SourceRoom, Is.SameAs(dining));
        Assert.That(diningEntrance.DestinationRoom, Is.SameAs(entrance));
        Assert.That(diningEntrance.Reverse, Is.SameAs(entranceDining));
        Assert.That(diningEntrance.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(diningEntrance.PromptText, Is.EqualTo("Open Door"));
        Assert.That(diningEntrance.LegacyDoorId, Is.EqualTo("DiningRoom_GEH"));

        Assert.That(diningButlersPantry.StableId, Is.EqualTo("passage.dining-room.butlers-pantry"));
        Assert.That(diningButlersPantry.SchemaVersion, Is.EqualTo(1));
        Assert.That(diningButlersPantry.SourceRoom, Is.SameAs(dining));
        Assert.That(diningButlersPantry.DestinationRoom, Is.SameAs(butlersPantry));
        Assert.That(diningButlersPantry.Reverse, Is.SameAs(butlersPantryDining));
        Assert.That(diningButlersPantry.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(diningButlersPantry.PromptText, Is.EqualTo("Open Door"));
        Assert.That(diningButlersPantry.LegacyDoorId, Is.EqualTo("DiningRoom_ButlersPantry"));

        Assert.That(butlersPantryDining.StableId, Is.EqualTo("passage.butlers-pantry.dining-room"));
        Assert.That(butlersPantryDining.SchemaVersion, Is.EqualTo(1));
        Assert.That(butlersPantryDining.SourceRoom, Is.SameAs(butlersPantry));
        Assert.That(butlersPantryDining.DestinationRoom, Is.SameAs(dining));
        Assert.That(butlersPantryDining.Reverse, Is.SameAs(diningButlersPantry));
        Assert.That(butlersPantryDining.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(butlersPantryDining.PromptText, Is.EqualTo("Open Door"));
        Assert.That(butlersPantryDining.LegacyDoorId, Is.EqualTo("ButlersPantry_DiningRoom"));

        Assert.That(butlersPantryBilliard.StableId,
            Is.EqualTo("passage.butlers-pantry.billiard-room"));
        Assert.That(butlersPantryBilliard.SchemaVersion, Is.EqualTo(1));
        Assert.That(butlersPantryBilliard.SourceRoom, Is.SameAs(butlersPantry));
        Assert.That(butlersPantryBilliard.DestinationRoom, Is.SameAs(billiard));
        Assert.That(butlersPantryBilliard.Reverse, Is.SameAs(billiardButlersPantry));
        Assert.That(butlersPantryBilliard.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(butlersPantryBilliard.PromptText, Is.EqualTo("Open Door"));
        Assert.That(butlersPantryBilliard.LegacyDoorId, Is.EqualTo("Butlers_Pantry_BilliardRoom"));

        Assert.That(billiardButlersPantry.StableId,
            Is.EqualTo("passage.billiard-room.butlers-pantry"));
        Assert.That(billiardButlersPantry.SchemaVersion, Is.EqualTo(1));
        Assert.That(billiardButlersPantry.SourceRoom, Is.SameAs(billiard));
        Assert.That(billiardButlersPantry.DestinationRoom, Is.SameAs(butlersPantry));
        Assert.That(billiardButlersPantry.Reverse, Is.SameAs(butlersPantryBilliard));
        Assert.That(billiardButlersPantry.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(billiardButlersPantry.PromptText, Is.EqualTo("Open Door"));
        Assert.That(billiardButlersPantry.LegacyDoorId, Is.EqualTo("BilliardRoom_ButlersPantry"));

        Assert.That(butlersPantryServiceCorridor.StableId,
            Is.EqualTo("passage.butlers-pantry.service-corridor"));
        Assert.That(butlersPantryServiceCorridor.SchemaVersion, Is.EqualTo(1));
        Assert.That(butlersPantryServiceCorridor.SourceRoom, Is.SameAs(butlersPantry));
        Assert.That(butlersPantryServiceCorridor.DestinationRoom, Is.SameAs(serviceCorridor));
        Assert.That(butlersPantryServiceCorridor.Reverse, Is.SameAs(serviceCorridorButlersPantry));
        Assert.That(butlersPantryServiceCorridor.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(butlersPantryServiceCorridor.PromptText, Is.EqualTo("Open Door"));
        Assert.That(butlersPantryServiceCorridor.LegacyDoorId,
            Is.EqualTo("ButlersPantry_ServiceCorridor"));

        Assert.That(serviceCorridorButlersPantry.StableId,
            Is.EqualTo("passage.service-corridor.butlers-pantry"));
        Assert.That(serviceCorridorButlersPantry.SchemaVersion, Is.EqualTo(1));
        Assert.That(serviceCorridorButlersPantry.SourceRoom, Is.SameAs(serviceCorridor));
        Assert.That(serviceCorridorButlersPantry.DestinationRoom, Is.SameAs(butlersPantry));
        Assert.That(serviceCorridorButlersPantry.Reverse, Is.SameAs(butlersPantryServiceCorridor));
        Assert.That(serviceCorridorButlersPantry.Kind, Is.EqualTo(PassageKind.Door));
        Assert.That(serviceCorridorButlersPantry.PromptText, Is.EqualTo("Open Door"));
        Assert.That(serviceCorridorButlersPantry.LegacyDoorId,
            Is.EqualTo("ServiceCorridor_ButlersPantry"));

        Assert.That(database.Definitions, Has.Count.EqualTo(35));
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
        Assert.That(database.Definitions[10], Is.SameAs(ballroom));
        Assert.That(database.Definitions[11], Is.SameAs(libraryBallroom));
        Assert.That(database.Definitions[12], Is.SameAs(ballroomLibrary));
        Assert.That(database.Definitions[13], Is.SameAs(dining));
        Assert.That(database.Definitions[14], Is.SameAs(entranceDining));
        Assert.That(database.Definitions[15], Is.SameAs(diningEntrance));
        Assert.That(database.Definitions[16], Is.SameAs(butlersPantry));
        Assert.That(database.Definitions[17], Is.SameAs(diningButlersPantry));
        Assert.That(database.Definitions[18], Is.SameAs(butlersPantryDining));
        Assert.That(database.Definitions[19], Is.SameAs(billiard));
        Assert.That(database.Definitions[20], Is.SameAs(butlersPantryBilliard));
        Assert.That(database.Definitions[21], Is.SameAs(billiardButlersPantry));
        Assert.That(database.Definitions[22], Is.SameAs(serviceCorridor));
        Assert.That(database.Definitions[23], Is.SameAs(butlersPantryServiceCorridor));
        Assert.That(database.Definitions[24], Is.SameAs(serviceCorridorButlersPantry));
        Assert.That(database.Definitions.Skip(25), Is.EqualTo(completedRooms.Skip(1)));

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
            "passage.library.music-room",
            "room.ballroom",
            "passage.library.ballroom",
            "passage.ballroom.library",
            "room.dining-room",
            "passage.grand-entrance-hall.dining-room",
            "passage.dining-room.grand-entrance-hall",
            "room.butlers-pantry",
            "passage.dining-room.butlers-pantry",
            "passage.butlers-pantry.dining-room",
            "room.billiard-room",
            "passage.butlers-pantry.billiard-room",
            "passage.billiard-room.butlers-pantry",
            "room.service-corridor",
            "passage.butlers-pantry.service-corridor",
            "passage.service-corridor.butlers-pantry",
            "room.kitchen",
            "room.chapel",
            "room.grand-entrance-hall-rear-view",
            "room.conservatory",
            "room.side-stair-mudroom",
            "room.upper-sitting-hall",
            "room.upper-gallery",
            "room.master-bedroom-suite",
            "room.nursery",
            "room.blue-bedroom"
        }));
        Assert.That(stableIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(35));

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
        Assert.That(CountOccurrences(gameplayText, "\n--- !u!"), Is.EqualTo(6032));
        Assert.That(CountOccurrences(gameplayText, $"guid: {LibraryRoomGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {MusicLibraryPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {LibraryMusicPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, "guid: 3167361ca4c671298c0e84f43320619b"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, "guid: 01544de8f55723585d60e5c0915345fd"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {BallroomRoomGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {LibraryBallroomPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {BallroomLibraryPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {DiningRoomGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {EntranceDiningPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {DiningEntrancePassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {ButlersPantryRoomGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {DiningButlersPantryPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {ButlersPantryDiningPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {BilliardRoomGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {ButlersPantryBilliardPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {BilliardButlersPantryPassageGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {ServiceCorridorRoomGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {ButlersPantryServiceCorridorPassageGuid}"),
            Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, $"guid: {ServiceCorridorButlersPantryPassageGuid}"),
            Is.EqualTo(1));
        string entranceRoomObject = ExtractDocument(gameplayText, "--- !u!1 &567115833");
        string drawingRoomObject = ExtractDocument(gameplayText, "--- !u!1 &2300000005");
        string musicRoomObject = ExtractDocument(gameplayText, "--- !u!1 &354156755");
        string libraryRoomObject = ExtractDocument(gameplayText, "--- !u!1 &1367921344");
        string ballroomRoomObject = ExtractDocument(gameplayText, "--- !u!1 &43637644");
        string entranceView = ExtractDocument(gameplayText, "--- !u!114 &4100000001");
        string drawingView = ExtractDocument(gameplayText, "--- !u!114 &4100000002");
        string musicView = ExtractDocument(gameplayText, "--- !u!114 &4100000003");
        string libraryView = ExtractDocument(gameplayText, "--- !u!114 &4100000004");
        string ballroomView = ExtractDocument(gameplayText, "--- !u!114 &4100000005");
        string diningRoomObject = ExtractDocument(gameplayText, "--- !u!1 &2300000015");
        string diningView = ExtractDocument(gameplayText, "--- !u!114 &4100000006");
        string butlersPantryRoomObject = ExtractDocument(gameplayText, "--- !u!1 &2300000020");
        string butlersPantryView = ExtractDocument(gameplayText, "--- !u!114 &4100000007");
        string billiardRoomObject = ExtractDocument(gameplayText, "--- !u!1 &2300000010");
        string billiardView = ExtractDocument(gameplayText, "--- !u!114 &4100000008");
        string serviceCorridorRoomObject = ExtractDocument(gameplayText, "--- !u!1 &2300000025");
        string serviceCorridorRoomTransform = ExtractDocument(gameplayText, "--- !u!224 &2300000026");
        string serviceCorridorRoomContent = ExtractDocument(gameplayText, "--- !u!114 &2300000027");
        string serviceCorridorView = ExtractDocument(gameplayText, "--- !u!114 &4100000009");
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
        string libraryBallroomObject = ExtractDocument(gameplayText, "--- !u!1 &2300000080");
        string libraryBallroomTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000084");
        string libraryBallroomPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000017");
        string ballroomLibraryObject = ExtractDocument(gameplayText, "--- !u!1 &2101000021");
        string ballroomLibraryTrigger = ExtractDocument(gameplayText, "--- !u!114 &2101000025");
        string ballroomLibraryPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000018");
        string entranceDiningObject = ExtractDocument(gameplayText, "--- !u!1 &340611598");
        string entranceDiningTrigger = ExtractDocument(gameplayText, "--- !u!114 &340611600");
        string entranceDiningPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000019");
        string diningEntranceObject = ExtractDocument(gameplayText, "--- !u!1 &2300000105");
        string diningEntranceTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000109");
        string diningEntrancePassage = ExtractDocument(gameplayText, "--- !u!114 &4100000020");
        string diningButlersObject = ExtractDocument(gameplayText, "--- !u!1 &2300000115");
        string diningButlersTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000119");
        string diningButlersPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000021");
        string butlersDiningObject = ExtractDocument(gameplayText, "--- !u!1 &2300000135");
        string butlersDiningTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000139");
        string butlersDiningPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000022");
        string pantryBilliardObject = ExtractDocument(gameplayText, "--- !u!1 &1505671644");
        string pantryBilliardTrigger = ExtractDocument(gameplayText, "--- !u!114 &1505671646");
        string pantryBilliardPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000023");
        string billiardPantryObject = ExtractDocument(gameplayText, "--- !u!1 &2300000130");
        string billiardPantryTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000134");
        string billiardPantryPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000024");
        string pantryServiceCorridorObject = ExtractDocument(gameplayText, "--- !u!1 &2300000145");
        string pantryServiceCorridorTransform = ExtractDocument(gameplayText, "--- !u!224 &2300000146");
        string pantryServiceCorridorTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000149");
        string pantryServiceCorridorPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000025");
        string serviceCorridorPantryObject = ExtractDocument(gameplayText, "--- !u!1 &2300000150");
        string serviceCorridorPantryTransform = ExtractDocument(gameplayText, "--- !u!224 &2300000151");
        string serviceCorridorPantryTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000154");
        string serviceCorridorPantryPassage = ExtractDocument(gameplayText, "--- !u!114 &4100000026");
        string playerTransform = ExtractDocument(gameplayText, "--- !u!4 &81962843 stripped");

        Assert.That(CountOccurrences(gameplayText, "guid: ccd2f3bd803e45aa8a1174cc881d6dc0"), Is.EqualTo(9),
            "Exactly the nine staged rooms through Service Corridor may own passive RoomViews.");
        Assert.That(CountOccurrences(gameplayText, "guid: 518dad8adf634786a103bf4e76aa0881"), Is.EqualTo(16),
            "The eight introduced reciprocal pairs must be the only Passages at this gate.");
        Assert.That(CountOccurrences(gameplayText, "anchorMigrationStage:"), Is.EqualTo(16),
            "Every staged Passage must serialize exactly one explicit anchor-ownership mode.");
        Assert.That(CountOccurrences(gameplayText, "anchorMigrationStage: 0"), Is.Zero,
            "No completed reciprocal pair may retain legacy sampling.");
        Assert.That(CountOccurrences(gameplayText, "anchorMigrationStage: 1"), Is.Zero,
            "No completed reciprocal pair may retain legacy approach sampling.");
        Assert.That(CountOccurrences(gameplayText, "anchorMigrationStage: 2"), Is.EqualTo(16),
            "All eight completed reciprocal pairs must own their authored approach and arrival anchors.");
        Assert.That(CountOccurrences(gameplayText, "maxPlayerScreenDistance: 145"), Is.EqualTo(44),
            "Every trigger except the calibrated Library-to-Music endpoint must retain the legacy threshold.");
        Assert.That(CountOccurrences(gameplayText, "maxPlayerScreenDistance: 149"), Is.EqualTo(1),
            "Only the calibrated Library-to-Music endpoint may use the 149-pixel threshold.");

        Assert.That(entranceRoomObject, Does.Contain("- component: {fileID: 4100000001}"));
        Assert.That(drawingRoomObject, Does.Contain("- component: {fileID: 4100000002}"));
        Assert.That(musicRoomObject, Does.Contain("- component: {fileID: 4100000003}"));
        Assert.That(libraryRoomObject, Does.Contain("- component: {fileID: 4100000004}"));
        Assert.That(ballroomRoomObject, Does.Contain("- component: {fileID: 4100000005}"));
        Assert.That(diningRoomObject, Does.Contain("- component: {fileID: 4100000006}"));
        Assert.That(butlersPantryRoomObject, Does.Contain("- component: {fileID: 4100000007}"));
        Assert.That(billiardRoomObject, Does.Contain("- component: {fileID: 4100000008}"));
        Assert.That(CountOccurrences(libraryRoomObject, "- component:"), Is.EqualTo(3));
        Assert.That(CountOccurrences(ballroomRoomObject, "- component:"), Is.EqualTo(3));
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
        Assert.That(ballroomView, Does.Contain("m_GameObject: {fileID: 43637644}"));
        Assert.That(ballroomView, Does.Contain(
            "m_Script: {fileID: 11500000, guid: ccd2f3bd803e45aa8a1174cc881d6dc0, type: 3}"));
        Assert.That(ballroomView, Does.Contain(
            $"definition: {{fileID: 11400000, guid: {BallroomRoomGuid}, type: 2}}"));
        Assert.That(ballroomView, Does.Contain("legacyContentGroup: {fileID: 2102000000}"));
        Assert.That(diningView, Does.Contain("m_GameObject: {fileID: 2300000015}"));
        Assert.That(diningView, Does.Contain(
            $"definition: {{fileID: 11400000, guid: {DiningRoomGuid}, type: 2}}"));
        Assert.That(diningView, Does.Contain("legacyContentGroup: {fileID: 2300000017}"));
        Assert.That(butlersPantryView, Does.Contain("m_GameObject: {fileID: 2300000020}"));
        Assert.That(butlersPantryView, Does.Contain(
            $"definition: {{fileID: 11400000, guid: {ButlersPantryRoomGuid}, type: 2}}"));
        Assert.That(butlersPantryView, Does.Contain("legacyContentGroup: {fileID: 2300000022}"));
        Assert.That(billiardView, Does.Contain("m_GameObject: {fileID: 2300000010}"));
        Assert.That(billiardView, Does.Contain(
            $"definition: {{fileID: 11400000, guid: {BilliardRoomGuid}, type: 2}}"));
        Assert.That(billiardView, Does.Contain("legacyContentGroup: {fileID: 2300000012}"));
        Assert.That(serviceCorridorRoomObject, Does.Contain("m_Name: Room_Service_Corridor"));
        Assert.That(serviceCorridorRoomObject, Does.Contain("m_IsActive: 0"));
        Assert.That(serviceCorridorRoomObject, Does.Contain("- component: {fileID: 2300000026}"));
        Assert.That(serviceCorridorRoomObject, Does.Contain("- component: {fileID: 2300000027}"));
        Assert.That(serviceCorridorRoomObject, Does.Contain("- component: {fileID: 4100000009}"));
        Assert.That(CountOccurrences(serviceCorridorRoomObject, "- component:"), Is.EqualTo(3));
        Assert.That(serviceCorridorRoomTransform, Does.Contain("m_Father: {fileID: 668915133}"));
        Assert.That(CountOccurrences(serviceCorridorRoomTransform, "  - {fileID:"), Is.EqualTo(40),
            "The Service Corridor room migration must preserve every authored presentation child.");
        foreach (string preservedChildFileId in new[]
                 {
                     "21631085", "461008708", "297820109", "334646579", "839535681", "2300000029"
                 })
        {
            Assert.That(serviceCorridorRoomTransform, Does.Contain($"- {{fileID: {preservedChildFileId}}}"),
                $"Service Corridor child {preservedChildFileId} must remain attached to its room root.");
        }
        Assert.That(serviceCorridorRoomContent, Does.Contain("roomName: Service Corridor"));
        Assert.That(serviceCorridorRoomContent, Does.Contain(
            "roomBackgroundTexture: {fileID: 2800000, guid: 63139e8fe55e5e00f97b08fe5f2b145b, type: 3}"));
        Assert.That(serviceCorridorRoomContent, Does.Contain("perspectiveProfile: {fileID: 0}"));
        Assert.That(serviceCorridorView, Does.Contain("m_GameObject: {fileID: 2300000025}"));
        Assert.That(serviceCorridorView, Does.Contain(
            $"definition: {{fileID: 11400000, guid: {ServiceCorridorRoomGuid}, type: 2}}"));
        Assert.That(serviceCorridorView, Does.Contain("legacyContentGroup: {fileID: 2300000027}"));

        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000001}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000002}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000003}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000004}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000005}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000006}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000007}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000008}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000009}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000011}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000012}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000013}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000014}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000015}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000016}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000017}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000018}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000019}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000020}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000021}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000022}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000023}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000024}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000025}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000026}"), Is.EqualTo(1));
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000015}\n" +
            "  - {fileID: 4100000016}\n" +
            "  - {fileID: 4100000017}\n" +
            "  - {fileID: 4100000018}\n" +
            "  - {fileID: 4100000019}\n" +
            "  - {fileID: 4100000020}\n" +
            "  - {fileID: 4100000021}\n" +
            "  - {fileID: 4100000022}\n" +
            "  - {fileID: 4100000023}\n" +
            "  - {fileID: 4100000024}\n" +
            "  - {fileID: 4100000025}\n" +
            "  - {fileID: 4100000026}"),
            "The completed Group 07 pair must follow the previously certified Passages.");
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000003}\n" +
            "  - {fileID: 4100000004}\n" +
            "  - {fileID: 4100000005}\n" +
            "  - {fileID: 4100000006}\n" +
            "  - {fileID: 4100000007}\n" +
            "  - {fileID: 4100000008}\n" +
            "  - {fileID: 4100000009}\n" +
            "  - {fileID: 4100000011}"),
            "The Service Corridor RoomView must follow existing views without reordering certified Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000001"), Is.EqualTo(5),
            "The entrance RoomView should occur only on its owner, header, GameRoot, and two source Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000002"), Is.EqualTo(5),
            "The drawing-room RoomView should occur only on its owner, document header, GameRoot registration, and two source Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000003"), Is.EqualTo(5),
            "The Music RoomView should occur only on its owner, document header, GameRoot registration, and two source Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000004"), Is.EqualTo(5),
            "The Library RoomView should occur only on its owner, document header, GameRoot registration, and two source Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000005"), Is.EqualTo(4),
            "The Ballroom RoomView should occur only on its owner, document header, GameRoot registration, and source Passage.");
        Assert.That(CountOccurrences(gameplayText, "4100000006"), Is.EqualTo(5),
            "The Dining RoomView should occur only on its owner, header, GameRoot, and two source Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000007"), Is.EqualTo(6),
            "The Butlers Pantry RoomView should occur only on its owner, header, GameRoot, and three source Passages.");
        Assert.That(CountOccurrences(gameplayText, "4100000008"), Is.EqualTo(4),
            "The Billiard RoomView should occur only on its owner, header, GameRoot, and source Passage.");
        Assert.That(CountOccurrences(gameplayText, "4100000009"), Is.EqualTo(4),
            "The Service Corridor RoomView should occur only on its owner, header, GameRoot, and source Passage.");
        Assert.That(outboundObject, Does.Contain("- component: {fileID: 4100000011}"));
        Assert.That(reverseObject, Does.Contain("- component: {fileID: 4100000012}"));
        Assert.That(drawingMusicObject, Does.Contain("- component: {fileID: 4100000013}"));
        Assert.That(musicDrawingObject, Does.Contain("- component: {fileID: 4100000014}"));
        Assert.That(musicLibraryObject, Does.Contain("- component: {fileID: 4100000015}"));
        Assert.That(libraryMusicObject, Does.Contain("- component: {fileID: 4100000016}"));
        Assert.That(libraryBallroomObject, Does.Contain("- component: {fileID: 4100000017}"));
        Assert.That(ballroomLibraryObject, Does.Contain("- component: {fileID: 4100000018}"));
        Assert.That(entranceDiningObject, Does.Contain("- component: {fileID: 4100000019}"));
        Assert.That(diningEntranceObject, Does.Contain("- component: {fileID: 4100000020}"));
        Assert.That(diningButlersObject, Does.Contain("- component: {fileID: 4100000021}"));
        Assert.That(butlersDiningObject, Does.Contain("- component: {fileID: 4100000022}"));
        Assert.That(pantryBilliardObject, Does.Contain("- component: {fileID: 4100000023}"));
        Assert.That(billiardPantryObject, Does.Contain("- component: {fileID: 4100000024}"));
        Assert.That(pantryServiceCorridorObject, Does.Contain("- component: {fileID: 4100000025}"));
        Assert.That(serviceCorridorPantryObject, Does.Contain("- component: {fileID: 4100000026}"));
        Assert.That(CountOccurrences(drawingMusicObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(musicDrawingObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(musicLibraryObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(libraryMusicObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(libraryBallroomObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(ballroomLibraryObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(entranceDiningObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(diningEntranceObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(diningButlersObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(butlersDiningObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(pantryBilliardObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(billiardPantryObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(pantryServiceCorridorObject, "- component:"), Is.EqualTo(5));
        Assert.That(CountOccurrences(serviceCorridorPantryObject, "- component:"), Is.EqualTo(5));
        Assert.That(pantryServiceCorridorTransform, Does.Contain("m_Father: {fileID: 2300000024}"));
        Assert.That(pantryServiceCorridorTransform, Does.Contain(
            "m_AnchoredPosition: {x: 591.2165, y: 33.108276}"));
        Assert.That(pantryServiceCorridorTransform, Does.Contain(
            "m_SizeDelta: {x: 188.3424, y: 453.9467}"));
        Assert.That(serviceCorridorPantryTransform, Does.Contain("m_Father: {fileID: 2300000029}"));
        Assert.That(serviceCorridorPantryTransform, Does.Contain("m_AnchoredPosition: {x: 352, y: 28}"));
        Assert.That(serviceCorridorPantryTransform, Does.Contain(
            "m_SizeDelta: {x: 124.2894, y: 524.2852}"));
        Assert.That(CountOccurrences(gameplayText, "4100000011"), Is.EqualTo(5),
            "The forward Passage should occur only on its owner, header, GameRoot registration, reciprocal link, and trigger caller binding.");
        Assert.That(CountOccurrences(gameplayText, "4100000012"), Is.EqualTo(5),
            "The reverse Passage should occur only on its owner, header, GameRoot registration, reciprocal link, and trigger caller binding.");
        Assert.That(CountOccurrences(gameplayText, "4100000013"), Is.EqualTo(5),
            "The Drawing-to-Music Passage should occur only on its owner, header, GameRoot registration, reciprocal link, and trigger caller binding.");
        Assert.That(CountOccurrences(gameplayText, "4100000014"), Is.EqualTo(5),
            "The Music-to-Drawing Passage should occur only on its owner, header, GameRoot registration, reciprocal link, and trigger caller binding.");
        Assert.That(CountOccurrences(gameplayText, "4100000015"), Is.EqualTo(5),
            "The Music-to-Library Passage should occur only on its owner, header, GameRoot, reverse link, and trigger caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000016"), Is.EqualTo(5),
            "The Library-to-Music Passage should occur only on its owner, header, GameRoot, reverse link, and trigger caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000017"), Is.EqualTo(5),
            "The Library-to-Ballroom Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000018"), Is.EqualTo(5),
            "The Ballroom-to-Library Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000019"), Is.EqualTo(5),
            "The Entrance-to-Dining Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000020"), Is.EqualTo(5),
            "The Dining-to-Entrance Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000021"), Is.EqualTo(5),
            "The Dining-to-Butlers Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000022"), Is.EqualTo(5),
            "The Butlers-to-Dining Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000023"), Is.EqualTo(5),
            "The Pantry-to-Billiard Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000024"), Is.EqualTo(5),
            "The Billiard-to-Pantry Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000025"), Is.EqualTo(5),
            "The Pantry-to-Service Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "4100000026"), Is.EqualTo(5),
            "The Service-to-Pantry Passage should occur only on its owner, header, GameRoot, reverse link, and caller.");
        Assert.That(CountOccurrences(gameplayText, "canonicalPassage: {fileID:"), Is.EqualTo(16),
            "All eight certified reciprocal routes must use canonical identity at this gate.");
        Assert.That(CountOccurrences(gameplayText, "player: {fileID: 81962843}"), Is.EqualTo(16),
            "Exactly the eight dependency-bound reciprocal pairs may bind the Player transform at this gate.");
        Assert.That(CountOccurrences(gameplayText, "81962843"), Is.EqualTo(17),
            "The Player Transform proxy should occur only in its header and sixteen trigger bindings.");
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
            Is.EqualTo(16),
            "Exactly the eight dependency-bound reciprocal routes may receive direct compatibility bindings at this gate.");
        Assert.That(
            legacyTriggerDocuments.Count(document =>
                document.Contains("navigationManager: {fileID: 0}") &&
                document.Contains("doorOpenAudioSource: {fileID: 0}") &&
                document.Contains("player: {fileID: 0}") &&
                document.Contains("doorOpenSoundCatalog: {fileID: 0}")),
            Is.EqualTo(29),
            "Every trigger before its dependency slice must remain byte-semantically unbound.");
        Assert.That(
            legacyTriggerDocuments.All(document => document.Contains("stairwaySoundCatalog: {fileID: 0}")),
            Is.True,
            "The door-only binding slice must not mutate stairway audio ownership.");
        Assert.That(
            legacyTriggerDocuments.Count(document => document.Contains("canonicalPassage: {fileID:")),
            Is.EqualTo(16));
        Assert.That(
            legacyTriggerDocuments.Count(document => !document.Contains("canonicalPassage:")),
            Is.EqualTo(29),
            "Every trigger before its caller slice must deserialize a null canonical edge and retain the fallback.");
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
            "{x: 7.714471, y: -3.121709}",
            "{x: -7.744175, y: -3.059095}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            libraryMusicPassage,
            "2300000075",
            LibraryMusicPassageGuid,
            "4100000004",
            "4100000015",
            "{x: -7.744175, y: -3.059095}",
            "{x: 7.714471, y: -3.121709}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            libraryBallroomPassage,
            "2300000080",
            LibraryBallroomPassageGuid,
            "4100000004",
            "4100000018",
            "{x: 7.95, y: -3}",
            "{x: -8.607888, y: -2.439877}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            ballroomLibraryPassage,
            "2101000021",
            BallroomLibraryPassageGuid,
            "4100000005",
            "4100000017",
            "{x: -8.607888, y: -2.439877}",
            "{x: 7.95, y: -3}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            entranceDiningPassage,
            "340611598",
            EntranceDiningPassageGuid,
            "4100000001",
            "4100000020",
            "{x: 8.705841, y: -2.346406}",
            "{x: -7.192237, y: -1.740209}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            diningEntrancePassage,
            "2300000105",
            DiningEntrancePassageGuid,
            "4100000006",
            "4100000019",
            "{x: -7.192237, y: -1.740209}",
            "{x: 8.705841, y: -2.346406}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            diningButlersPassage,
            "2300000115",
            DiningButlersPantryPassageGuid,
            "4100000006",
            "4100000022",
            "{x: 3.391918, y: -0.36}",
            "{x: -5.163103, y: -3.463186}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            butlersDiningPassage,
            "2300000135",
            ButlersPantryDiningPassageGuid,
            "4100000007",
            "4100000021",
            "{x: -5.163103, y: -3.463186}",
            "{x: 3.391918, y: -0.36}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            pantryBilliardPassage,
            "1505671644",
            ButlersPantryBilliardPassageGuid,
            "4100000007",
            "4100000024",
            "{x: 3.244461, y: -3.108338}",
            "{x: 6.9, y: -1.6}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            billiardPantryPassage,
            "2300000130",
            BilliardButlersPantryPassageGuid,
            "4100000008",
            "4100000023",
            "{x: 6.9, y: -1.6}",
            "{x: 3.244461, y: -3.108338}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            pantryServiceCorridorPassage,
            "2300000145",
            ButlersPantryServiceCorridorPassageGuid,
            "4100000007",
            "4100000026",
            "{x: 7, y: -2.8}",
            "{x: 4.2, y: -3.3}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageDocument(
            serviceCorridorPantryPassage,
            "2300000150",
            ServiceCorridorButlersPantryPassageGuid,
            "4100000009",
            "4100000025",
            "{x: 4.2, y: -3.3}",
            "{x: 7, y: -2.8}",
            PassageAnchorMigrationStage.AuthoredAnchors);

        AssertLegacyDoorTriggerCallerBound(
            pantryServiceCorridorTrigger, "2300000145", "Butlers Pantry",
            "ButlersPantry_ServiceCorridor", "Service Corridor", "2300000148", "4100000025");
        AssertLegacyDoorTriggerCallerBound(
            serviceCorridorPantryTrigger, "2300000150", "Service Corridor",
            "ServiceCorridor_ButlersPantry", "Butlers Pantry", "2300000153", "4100000026");

        AssertLegacyDoorTriggerCallerBound(
            pantryBilliardTrigger, "1505671644", "Butlers Pantry", "Butlers_Pantry_BilliardRoom",
            "Billiard Room", "1505671647", "4100000023");
        AssertLegacyDoorTriggerCallerBound(
            billiardPantryTrigger, "2300000130", "Billiard Room", "BilliardRoom_ButlersPantry",
            "Butlers Pantry", "2300000133", "4100000024");

        AssertLegacyDoorTriggerCallerBound(
            diningButlersTrigger, "2300000115", "Dining Room", "DiningRoom_ButlersPantry",
            "Butlers Pantry", "2300000118", "4100000021");
        AssertLegacyDoorTriggerCallerBound(
            butlersDiningTrigger, "2300000135", "Butlers Pantry", "ButlersPantry_DiningRoom",
            "Dining Room", "2300000138", "4100000022");

        AssertLegacyDoorTriggerCallerBound(
            entranceDiningTrigger, "340611598", "Grand Entrance Hall", "GEH_DiningRoom",
            "Dining Room", "340611601", "4100000019");
        AssertLegacyDoorTriggerCallerBound(
            diningEntranceTrigger, "2300000105", "Dining Room", "DiningRoom_GEH",
            "Grand Entrance Hall", "2300000108", "4100000020");

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
        Assert.That(musicLibraryTrigger, Does.Contain("canonicalPassage: {fileID: 4100000015}"));
        Assert.That(libraryMusicTrigger, Does.Contain("canonicalPassage: {fileID: 4100000016}"));
        foreach (string callerBoundTrigger in new[] { musicLibraryTrigger, libraryMusicTrigger })
        {
            Assert.That(callerBoundTrigger, Does.Contain("navigationManager: {fileID: 1878886997}"));
            Assert.That(callerBoundTrigger, Does.Contain("doorOpenAudioSource: {fileID: 2201000013}"));
            Assert.That(callerBoundTrigger, Does.Contain("player: {fileID: 81962843}"));
            Assert.That(callerBoundTrigger, Does.Contain(
                "doorOpenSoundCatalog: {fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}"));
            Assert.That(callerBoundTrigger, Does.Contain("stairwaySoundCatalog: {fileID: 0}"));
        }
        Assert.That(musicLibraryTrigger, Does.Contain("maxPlayerScreenDistance: 145"));
        Assert.That(libraryMusicTrigger, Does.Contain("maxPlayerScreenDistance: 149"));

        AssertLegacyDoorTriggerCallerBound(
            libraryBallroomTrigger,
            "2300000080",
            "Library",
            "Library_Ballroom",
            "Ballroom",
            "2300000083",
            "4100000017");
        AssertLegacyDoorTriggerCallerBound(
            ballroomLibraryTrigger,
            "2101000021",
            "Ballroom",
            "Ballroom_Library",
            "Library",
            "2101000024",
            "4100000018");

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
        Assert.That(CountOccurrences(gameplayText, "guid: ccd2f3bd803e45aa8a1174cc881d6dc0"), Is.EqualTo(9));
        Assert.That(CountOccurrences(gameplayText, "guid: 518dad8adf634786a103bf4e76aa0881"), Is.EqualTo(16));
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

    private static void AssertLegacyDoorTriggerCallerBound(
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
        Assert.That(document, Does.Contain(
            $"canonicalPassage: {{fileID: {canonicalPassageFileId}}}"));
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

    private static void AssertLegacyDoorTriggerDependenciesBound(
        string document,
        string gameObjectFileId,
        string sourceRoom,
        string doorName,
        string destinationRoom,
        string imageFileId)
    {
        Assert.That(document, Does.Contain($"m_GameObject: {{fileID: {gameObjectFileId}}}"));
        Assert.That(document, Does.Contain($"sourceRoom: {sourceRoom}"));
        Assert.That(document, Does.Contain($"doorName: {doorName}"));
        Assert.That(document, Does.Contain($"destinationRoom: {destinationRoom}"));
        Assert.That(document, Does.Contain("navigationManager: {fileID: 1878886997}"));
        Assert.That(document, Does.Contain($"image: {{fileID: {imageFileId}}}"));
        Assert.That(document, Does.Contain("doorOpenAudioSource: {fileID: 2201000013}"));
        Assert.That(document, Does.Contain("player: {fileID: 81962843}"));
        Assert.That(document, Does.Contain(
            "doorOpenSoundCatalog: {fileID: 11400000, guid: 9a77542e25184fbc945d6a79f77007e7, type: 2}"));
        Assert.That(document, Does.Contain("stairwaySoundCatalog: {fileID: 0}"));
        Assert.That(document, Does.Not.Contain("canonicalPassage:"));
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
