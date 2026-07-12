using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Chateau.Architecture;
using Chateau.World.Rooms.Passages;
using NUnit.Framework;
using UnityEditor;
using CanonicalRoomDefinition = Chateau.World.Rooms.RoomDefinition;

public sealed class PassageMigrationCertificationTests
{
    private const string InventoryPath = "Docs/Architecture/RemainingRouteInventory.csv";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";
    private const string LifecycleCharacterizationPath = "Assets/Editor/GameplayLifecycleCharacterizationTests.cs";
    private const string GameDatabasePath = "Assets/_Chateau/Data/GameDatabase.asset";
    private const string DoorTriggerGuid = "7e419b0f8f26d4f2d8d03e567fef4c52";
    private const string PassageGuid = "518dad8adf634786a103bf4e76aa0881";
    private const string RoomViewGuid = "ccd2f3bd803e45aa8a1174cc881d6dc0";
    private const string DoorCatalogGuid = "9a77542e25184fbc945d6a79f77007e7";
    private const string StairwayCatalogGuid = "ca795d9a1ee74c14aa22f9f38a14f8ea";
    private const string NavigationManagerFileId = "1878886997";
    private const string PlayerTransformFileId = "81962843";
    private const string DoorAudioSourceFileId = "2201000013";
    private const string GameRootFileId = "1878886998";
    private const string LibraryRoomDefinitionGuid = "8da3a3e936712e7b9f534786110323e4";
    private const string MusicLibraryPassageDefinitionGuid = "aefe77f20874eb81b83fccb6ff5b8046";
    private const string LibraryMusicPassageDefinitionGuid = "3a641d5febbfd7aec481ada678ba9fe4";
    private static readonly IReadOnlyDictionary<string, int> MigrationStages =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { "queued", 0 },
            { "characterized", 1 },
            { "data-authored", 2 },
            { "view-bound", 3 },
            { "passage-bound", 4 },
            { "dependencies-bound", 5 },
            { "caller-bound", 6 },
            { "arrival-owned", 7 },
            { "approach-owned", 8 },
            { "complete", 9 },
            { "blocked-one-way", -1 },
            { "blocked-parallel", -1 }
        };

    [Test]
    public void CertifiedRoutePairsSatisfyTemplateContract()
    {
        List<RouteInventoryRow> rows = ReadInventory();
        List<RouteInventoryRow> certified = rows.Where(row => row.Status == "complete").ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string database = File.ReadAllText(GameDatabasePath);

        Assert.That(certified, Has.Count.EqualTo(4),
            "Exactly the Entrance/Drawing and Drawing/Music reciprocal pairs are complete at this gate.");
        Assert.That(certified.Select(row => row.Order).Distinct().OrderBy(order => order),
            Is.EqualTo(new[] { 0, 1 }),
            "Completed-route certification must cover groups 00 and 01 exactly once per reciprocal pair.");

        foreach (IGrouping<int, RouteInventoryRow> certifiedGroup in certified.GroupBy(row => row.Order))
        {
            Assert.That(certifiedGroup.ToList(), Has.Count.EqualTo(2),
                $"Completed group {certifiedGroup.Key:00} must remain one reciprocal pair.");
        }

        for (int i = 0; i < certified.Count; i++)
        {
            RouteInventoryRow row = certified[i];
            RouteInventoryRow partner = certified.Single(candidate =>
                candidate.ComponentFileId == row.PartnerComponentIds.Single());
            string trigger = RequireDocument(documents, row.ComponentFileId);
            AssertStagedRouteSerialization(
                rows,
                row,
                documents,
                database,
                gameRoot,
                trigger,
                GetMigrationStage(row.Status));
            Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(row.PassageFileId));
            Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo(NavigationManagerFileId));
            Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo(PlayerTransformFileId));
            Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo(DoorAudioSourceFileId));
            AssertCertifiedAudioCatalogs(trigger, row);
            Assert.That(ParseFloat(row.ApproachX), Is.EqualTo(ParseFloat(partner.ArrivalX)).Within(0.000001f));
            Assert.That(ParseFloat(row.ApproachY), Is.EqualTo(ParseFloat(partner.ArrivalY)).Within(0.000001f));
        }
    }

    [Test]
    public void CanonicalRouteInventoryMatchesManifestAndLeavesFallbacksIsolated()
    {
        Assert.That(GetExpectedAnchorMigrationStage("passage-bound"),
            Is.EqualTo(PassageAnchorMigrationStage.LegacySampling));
        Assert.That(GetExpectedAnchorMigrationStage("dependencies-bound"),
            Is.EqualTo(PassageAnchorMigrationStage.LegacySampling));
        Assert.That(GetExpectedAnchorMigrationStage("caller-bound"),
            Is.EqualTo(PassageAnchorMigrationStage.LegacySampling));
        Assert.That(GetExpectedAnchorMigrationStage("arrival-owned"),
            Is.EqualTo(PassageAnchorMigrationStage.AuthoredArrival));
        Assert.That(GetExpectedAnchorMigrationStage("approach-owned"),
            Is.EqualTo(PassageAnchorMigrationStage.AuthoredAnchors));
        Assert.That(GetExpectedAnchorMigrationStage("complete"),
            Is.EqualTo(PassageAnchorMigrationStage.AuthoredAnchors));

        List<RouteInventoryRow> rows = ReadInventory();
        string sceneText = File.ReadAllText(GameplayScenePath);
        Dictionary<string, string> documents = ReadUnityDocuments(sceneText);
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string database = File.ReadAllText(GameDatabasePath);
        Dictionary<string, string> gameObjectNames = ReadGameObjectNames(documents);
        Dictionary<string, string> triggerDocuments = documents
            .Where(pair => pair.Value.Contains($"guid: {DoorTriggerGuid}"))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Dictionary<string, string> passageDocuments = documents
            .Where(pair => pair.Value.Contains($"guid: {PassageGuid}"))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        HashSet<string> manifestedPassageIds = rows
            .Where(row => !string.IsNullOrEmpty(row.PassageFileId))
            .Select(row => row.PassageFileId)
            .ToHashSet(StringComparer.Ordinal);

        Assert.That(rows, Has.Count.EqualTo(45));
        Assert.That(triggerDocuments, Has.Count.EqualTo(rows.Count));
        Assert.That(passageDocuments.Keys.OrderBy(value => value),
            Is.EqualTo(manifestedPassageIds.OrderBy(value => value)));
        Assert.That(passageDocuments.Values.All(document =>
            CountOccurrences(document, "anchorMigrationStage:") == 1), Is.True,
            "Every staged Passage must serialize exactly one explicit anchor migration stage.");
        Assert.That(rows.Select(row => row.ComponentFileId).Distinct().ToList(), Has.Count.EqualTo(rows.Count));
        Assert.That(rows.Select(row => row.Owner).Distinct().ToList(), Has.Count.EqualTo(rows.Count));
        Assert.That(rows.Select(row => row.LegacyDoorId).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Has.Count.EqualTo(rows.Count));

        for (int i = 0; i < rows.Count; i++)
        {
            RouteInventoryRow row = rows[i];
            int stage = GetMigrationStage(row.Status);
            Assert.That(triggerDocuments.TryGetValue(row.ComponentFileId, out string trigger), Is.True,
                $"Inventory component {row.ComponentFileId} is not a Gameplay DoorTriggerNavigation.");
            string gameObjectFileId = ReadReferenceFileId(trigger, "m_GameObject");
            Assert.That(gameObjectNames[gameObjectFileId], Is.EqualTo(row.Owner));
            Assert.That(ReadField(trigger, "sourceRoom"), Is.EqualTo(row.SourceRoom));
            Assert.That(ReadField(trigger, "doorName"), Is.EqualTo(row.LegacyDoorId));
            Assert.That(ReadField(trigger, "destinationRoom"), Is.EqualTo(row.DestinationRoom));
            Assert.That(ReadField(trigger, "requirePlayerInSourceRoom"), Is.EqualTo("1"));
            Assert.That(ReadField(trigger, "useCameraSequence"), Is.EqualTo("0"));
            Assert.That(ReadField(trigger, "autoActivateAfterApproach"), Is.EqualTo("1"));
            Assert.That(ReadField(trigger, "playDoorOpenSound"), Is.EqualTo("1"));

            if (row.Profile == "bottom-edge-door")
            {
                Assert.That(ReadField(trigger, "useBottomScreenEdgeInteraction"), Is.EqualTo("1"));
                Assert.That(ReadField(trigger, "requirePlayerProximity"), Is.EqualTo("0"));
                Assert.That(ReadField(trigger, "walkPlayerToTriggerWhenFar"), Is.EqualTo("0"));
            }
            else
            {
                Assert.That(ReadField(trigger, "useBottomScreenEdgeInteraction"), Is.EqualTo("0"));
                Assert.That(ReadField(trigger, "requirePlayerProximity"), Is.EqualTo("1"));
                Assert.That(ReadField(trigger, "walkPlayerToTriggerWhenFar"), Is.EqualTo("1"));
            }

            if (row.Profile == "inferred-stairway")
            {
                Assert.That(ReadField(trigger, "triggerKind"), Is.EqualTo("0"));
                Assert.That(ReadField(trigger, "stairwayDirection"), Is.EqualTo("0"));
                Assert.That(row.Owner.StartsWith("StairwayTrigger_", StringComparison.Ordinal) ||
                    row.LegacyDoorId.IndexOf("Stairway", StringComparison.Ordinal) >= 0,
                    Is.True);
            }

            bool hasData = stage >= GetMigrationStage("data-authored");
            bool hasView = stage >= GetMigrationStage("view-bound");
            bool hasPassage = stage >= GetMigrationStage("passage-bound");
            Assert.That(string.IsNullOrEmpty(row.PassageFileId), Is.EqualTo(!hasPassage));
            Assert.That(string.IsNullOrEmpty(row.PassageDefinitionGuid), Is.EqualTo(!hasData));
            Assert.That(string.IsNullOrEmpty(row.PassageStableId), Is.EqualTo(!hasData));
            Assert.That(string.IsNullOrEmpty(row.SourceRoomViewFileId), Is.EqualTo(!hasView));
            Assert.That(string.IsNullOrEmpty(row.ApproachX), Is.EqualTo(!hasPassage));
            Assert.That(string.IsNullOrEmpty(row.ApproachY), Is.EqualTo(!hasPassage));
            Assert.That(string.IsNullOrEmpty(row.ArrivalX), Is.EqualTo(!hasPassage));
            Assert.That(string.IsNullOrEmpty(row.ArrivalY), Is.EqualTo(!hasPassage));
            AssertStagedRouteSerialization(rows, row, documents, database, gameRoot, trigger, stage);

            bool hasDirectDependencies = stage >= GetMigrationStage("dependencies-bound");
            bool hasCanonicalCaller = stage >= GetMigrationStage("caller-bound");

            if (hasCanonicalCaller)
            {
                Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(row.PassageFileId));
            }
            else
            {
                Assert.That(trigger, Does.Not.Contain("canonicalPassage:"));
            }

            if (hasDirectDependencies)
            {
                Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo(NavigationManagerFileId));
                Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo(PlayerTransformFileId));
                Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo(DoorAudioSourceFileId));
                AssertCertifiedAudioCatalogs(trigger, row);
            }
            else
            {
                Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo("0"));
                Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo("0"));
                Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo("0"));
                Assert.That(ReadReferenceFileId(trigger, "doorOpenSoundCatalog"), Is.EqualTo("0"));
                Assert.That(ReadReferenceFileId(trigger, "stairwaySoundCatalog"), Is.EqualTo("0"));
            }
        }
    }

    [Test]
    public void RemainingLegacyRouteTopologyIsExplicitlyClassified()
    {
        List<RouteInventoryRow> rows = ReadInventory();
        List<IGrouping<int, RouteInventoryRow>> groups = rows
            .GroupBy(row => row.Order)
            .OrderBy(group => group.Key)
            .ToList();

        Assert.That(groups.Select(group => group.Key), Is.EqualTo(Enumerable.Range(0, 23)));
        Assert.That(groups.Select(group => group.First().Group).Distinct().ToList(), Has.Count.EqualTo(23));
        Assert.That(rows.All(row => MigrationStages.ContainsKey(row.Status)), Is.True);
        Assert.That(rows.All(row =>
            row.Profile == "standard-door" ||
            row.Profile == "bottom-edge-door" ||
            row.Profile == "inferred-stairway"), Is.True);
        Assert.That(rows.Count(row => row.Profile == "standard-door"), Is.EqualTo(38));
        Assert.That(rows.Count(row => row.Profile == "bottom-edge-door"), Is.EqualTo(3));
        Assert.That(rows.Count(row => row.Profile == "inferred-stairway"), Is.EqualTo(4));
        Assert.That(rows.SelectMany(row => new[] { row.SourceRoom, row.DestinationRoom })
            .Select(NormalizeRoomName).Distinct().ToList(), Has.Count.EqualTo(19));

        foreach (IGrouping<string, RouteInventoryRow> connectivityGroup in rows.GroupBy(GetConnectivityKey))
        {
            Assert.That(connectivityGroup.Select(row => row.Order).Distinct().ToList(), Has.Count.EqualTo(1),
                $"Endpoint group {connectivityGroup.Key} must not be split across migration orders.");
            Assert.That(connectivityGroup.Select(row => row.Group).Distinct().ToList(), Has.Count.EqualTo(1),
                $"Endpoint group {connectivityGroup.Key} must have one manifest identity.");
        }

        for (int i = 0; i < groups.Count; i++)
        {
            List<RouteInventoryRow> groupRows = groups[i].ToList();
            Assert.That(groupRows.Select(row => row.Status).Distinct().ToList(), Has.Count.EqualTo(1));
            Assert.That(groupRows.Select(row => row.Notes).Distinct().ToList(), Has.Count.EqualTo(1));

            for (int rowIndex = 0; rowIndex < groupRows.Count; rowIndex++)
            {
                RouteInventoryRow row = groupRows[rowIndex];
                List<string> reverseCandidateIds = rows
                    .Where(candidate => candidate != row && AreReverseEndpoints(row, candidate))
                    .Select(candidate => candidate.ComponentFileId)
                    .OrderBy(value => value)
                    .ToList();
                Assert.That(row.PartnerComponentIds.OrderBy(value => value),
                    Is.EqualTo(reverseCandidateIds),
                    $"{row.ComponentFileId} partners must equal the globally derived reverse candidates.");
            }

            if (GetMigrationStage(groupRows[0].Status) >= 0)
            {
                Assert.That(groupRows, Has.Count.EqualTo(2));
                AssertReciprocal(groupRows[0], groupRows[1]);
                AssertReciprocal(groupRows[1], groupRows[0]);
            }
            else if (groupRows[0].Status == "blocked-one-way")
            {
                Assert.That(groupRows, Has.Count.EqualTo(1));
                Assert.That(groupRows[0].PartnerComponentIds, Is.Empty);
            }
            else
            {
                Assert.That(groupRows[0].Status, Is.EqualTo("blocked-parallel"));
                Assert.That(groupRows, Has.Count.EqualTo(3));

                Assert.That(groupRows.Count(row => NormalizeRoomName(row.SourceRoom) == "GRANDENTRANCEHALL"),
                    Is.EqualTo(2));
                Assert.That(groupRows.Count(row => NormalizeRoomName(row.SourceRoom) == "UPPERGALLERY"),
                    Is.EqualTo(1));
            }
        }

        Assert.That(rows.Where(row => row.Status == "blocked-one-way")
            .Select(row => row.ComponentFileId).OrderBy(value => value),
            Is.EqualTo(new[] { "1615236111", "2300000159" }));
        Assert.That(rows.Where(row => row.Profile == "bottom-edge-door")
            .Select(row => row.ComponentFileId).OrderBy(value => value),
            Is.EqualTo(new[] { "1858342503", "2300000074", "70736571" }));
        Assert.That(rows.Where(row => row.Profile == "inferred-stairway")
            .Select(row => row.ComponentFileId).OrderBy(value => value),
            Is.EqualTo(new[] { "106972347", "2300000069", "2300000189", "2300000194" }));
    }

    [Test]
    public void MusicLibraryCallersAreBoundWithoutAnchorOrDependencyChanges()
    {
        List<RouteInventoryRow> group = ReadInventory()
            .Where(row => row.Order == 2)
            .OrderBy(row => row.ComponentFileId)
            .ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string database = File.ReadAllText(GameDatabasePath);
        string musicOwner = RequireDocument(documents, "552135202");
        string musicTransform = RequireDocument(documents, "552135203");
        string musicTrigger = RequireDocument(documents, "552135204");
        string libraryOwner = RequireDocument(documents, "2300000075");
        string libraryTransform = RequireDocument(documents, "2300000076");
        string libraryTrigger = RequireDocument(documents, "2300000079");
        string musicRoom = RequireDocument(documents, "354156755");
        string musicRoomTransform = RequireDocument(documents, "354156756");
        string musicContent = RequireDocument(documents, "2102000001");
        string musicDoors = RequireDocument(documents, "2103000010");
        string musicDoorsTransform = RequireDocument(documents, "2103000011");
        string musicView = RequireDocument(documents, "4100000003");
        string libraryRoom = RequireDocument(documents, "1367921344");
        string libraryRoomTransform = RequireDocument(documents, "1367921345");
        string libraryContent = RequireDocument(documents, "2102000003");
        string libraryDoors = RequireDocument(documents, "2103000030");
        string libraryDoorsTransform = RequireDocument(documents, "2103000031");
        string libraryView = RequireDocument(documents, "4100000004");
        string musicPassage = RequireDocument(documents, "4100000015");
        string libraryPassage = RequireDocument(documents, "4100000016");
        string gameRoot = RequireDocument(documents, GameRootFileId);

        Assert.That(documents, Has.Count.EqualTo(6017));
        Assert.That(group, Has.Count.EqualTo(2));
        RouteInventoryRow libraryRow = group.Single(row => row.ComponentFileId == "2300000079");
        RouteInventoryRow musicRow = group.Single(row => row.ComponentFileId == "552135204");
        Assert.That(group.All(row => row.Status == "caller-bound"), Is.True);
        Assert.That(group.All(row => row.Group == "Music-Library"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "canonical-caller-bound-arrival-next"), Is.True);
        AssertReciprocal(libraryRow, musicRow);
        AssertReciprocal(musicRow, libraryRow);
        Assert.That(libraryRow.PassageFileId, Is.EqualTo("4100000016"));
        Assert.That(musicRow.PassageFileId, Is.EqualTo("4100000015"));
        Assert.That(libraryRow.PassageDefinitionGuid, Is.EqualTo(LibraryMusicPassageDefinitionGuid));
        Assert.That(musicRow.PassageDefinitionGuid, Is.EqualTo(MusicLibraryPassageDefinitionGuid));
        Assert.That(libraryRow.PassageStableId, Is.EqualTo("passage.library.music-room"));
        Assert.That(musicRow.PassageStableId, Is.EqualTo("passage.music-room.library"));
        Assert.That(libraryRow.SourceRoomViewFileId, Is.EqualTo("4100000004"));
        Assert.That(musicRow.SourceRoomViewFileId, Is.EqualTo("4100000003"));
        Assert.That(libraryRow.ApproachX, Is.EqualTo("-7.244175"));
        Assert.That(libraryRow.ApproachY, Is.EqualTo("-2.799095"));
        Assert.That(libraryRow.ArrivalX, Is.EqualTo("7.439471"));
        Assert.That(libraryRow.ArrivalY, Is.EqualTo("-2.846709"));
        Assert.That(musicRow.ApproachX, Is.EqualTo("7.439471"));
        Assert.That(musicRow.ApproachY, Is.EqualTo("-2.846709"));
        Assert.That(musicRow.ArrivalX, Is.EqualTo("-7.244175"));
        Assert.That(musicRow.ArrivalY, Is.EqualTo("-2.799095"));
        Assert.That(musicRow.ApproachX, Is.EqualTo(libraryRow.ArrivalX));
        Assert.That(musicRow.ApproachY, Is.EqualTo(libraryRow.ArrivalY));
        Assert.That(musicRow.ArrivalX, Is.EqualTo(libraryRow.ApproachX));
        Assert.That(musicRow.ArrivalY, Is.EqualTo(libraryRow.ApproachY));
        foreach (RouteInventoryRow row in group)
        {
            AssertFinite(row.ApproachX, $"{row.LegacyDoorId} approach x");
            AssertFinite(row.ApproachY, $"{row.LegacyDoorId} approach y");
            AssertFinite(row.ArrivalX, $"{row.LegacyDoorId} arrival x");
            AssertFinite(row.ArrivalY, $"{row.LegacyDoorId} arrival y");
        }
        Assert.That(musicRow.ArrivalX, Is.Not.EqualTo("-7.287828"),
            "The passive reciprocal reference point must not erase the characterized source-sensitive far arrival.");
        Assert.That(musicRow.ArrivalY, Is.Not.EqualTo("-2.936489"));
        string lifecycleCharacterization = File.ReadAllText(LifecycleCharacterizationPath);
        Assert.That(lifecycleCharacterization, Does.Match(
            @"AssertVector2Within\(primaryForwardArrival,\s*new Vector2\(-7\.287828f, -2\.936489f\), 0\.01f,"));
        Assert.That(lifecycleCharacterization, Does.Match(
            @"AssertVector2Within\(primaryNearForwardArrival,\s*new Vector2\(-7\.244175f, -2\.799095f\), 0\.01f,"));
        Assert.That(lifecycleCharacterization, Does.Match(
            @"Vector2\.Distance\(primaryForwardArrival,\s*musicLibraryPassage\.ArrivalAnchor\.LogicalPosition\),\s*Is\.GreaterThan\(0\.1f\)"),
            "The live far arrival must remain explicitly distinct from the new passive Passage reference point.");
        Assert.That(lifecycleCharacterization, Does.Match(
            @"Vector2\.Distance\(primaryForwardArrival, primaryNearForwardArrival\),\s*Is\.GreaterThan\(0\.1f\)"),
            "The locked lifecycle proof must retain the source-sensitive far/near Library arrival distinction.");

        AssertExactComponentIds(
            musicOwner, "552135203", "552135206", "552135205", "552135204", "4100000015");
        Assert.That(musicOwner, Does.Contain("m_Name: DoorTrigger_MusicRoom_Library"));
        Assert.That(ReadField(musicOwner, "m_Layer"), Is.EqualTo("5"));
        Assert.That(ReadField(musicOwner, "m_IsActive"), Is.EqualTo("1"));
        Assert.That(ReadReferenceFileId(musicTransform, "m_GameObject"), Is.EqualTo("552135202"));
        Assert.That(ReadReferenceFileId(musicTransform, "m_Father"), Is.EqualTo("2103000011"));
        Assert.That(ReadField(musicTransform, "m_LocalScale"), Is.EqualTo("{x: 0.95, y: 1, z: 1}"));
        Assert.That(ReadField(musicTransform, "m_AnchoredPosition"), Is.EqualTo("{x: 682, y: 4}"));
        Assert.That(ReadField(musicTransform, "m_SizeDelta"),
            Is.EqualTo("{x: 197.70117, y: 390.22205}"));
        Assert.That(ReadField(musicTransform, "m_AnchorMin"), Is.EqualTo("{x: 0.5, y: 0.5}"));
        Assert.That(ReadField(musicTransform, "m_AnchorMax"), Is.EqualTo("{x: 0.5, y: 0.5}"));
        Assert.That(ReadField(musicTransform, "m_Pivot"), Is.EqualTo("{x: 0.5, y: 0.5}"));
        AssertMusicLibraryCallerBoundLegacyTriggerSnapshot(
            musicTrigger,
            "552135202",
            "552135205",
            "Music Room",
            "MusicRoom_Library",
            "Library",
            "4100000015");

        AssertExactComponentIds(
            libraryOwner, "2300000076", "2300000077", "2300000078", "2300000079", "4100000016");
        Assert.That(libraryOwner, Does.Contain("m_Name: DoorTrigger_Library_MusicRoom"));
        Assert.That(ReadField(libraryOwner, "m_Layer"), Is.EqualTo("5"));
        Assert.That(ReadField(libraryOwner, "m_IsActive"), Is.EqualTo("1"));
        Assert.That(ReadReferenceFileId(libraryTransform, "m_GameObject"), Is.EqualTo("2300000075"));
        Assert.That(ReadReferenceFileId(libraryTransform, "m_Father"), Is.EqualTo("2103000031"));
        Assert.That(ReadField(libraryTransform, "m_LocalScale"), Is.EqualTo("{x: 1, y: 1, z: 1}"));
        Assert.That(ReadField(libraryTransform, "m_AnchoredPosition"),
            Is.EqualTo("{x: -651.72284, y: 30.3167}"));
        Assert.That(ReadField(libraryTransform, "m_SizeDelta"),
            Is.EqualTo("{x: 157.0319, y: 359.0855}"));
        Assert.That(ReadField(libraryTransform, "m_AnchorMin"), Is.EqualTo("{x: 0.5, y: 0.5}"));
        Assert.That(ReadField(libraryTransform, "m_AnchorMax"), Is.EqualTo("{x: 0.5, y: 0.5}"));
        Assert.That(ReadField(libraryTransform, "m_Pivot"), Is.EqualTo("{x: 0.5, y: 0.5}"));
        AssertMusicLibraryCallerBoundLegacyTriggerSnapshot(
            libraryTrigger,
            "2300000075",
            "2300000078",
            "Library",
            "Library_MusicRoom",
            "Music Room",
            "4100000016");
        string musicDependencyBoundTrigger = AssertRevertsToDependenciesBoundTriggerHash(
            musicTrigger,
            "4100000015",
            "f8cb20d42f85dd56e7c21a60b8853dc51d9e22c89d6ee3c253dadcf3f69444b0");
        string libraryDependencyBoundTrigger = AssertRevertsToDependenciesBoundTriggerHash(
            libraryTrigger,
            "4100000016",
            "35d9f37795a58c235ee7c62a084d7c0313ce5bba8b8a29d787f52c9582834e70");
        AssertRevertsToPassageBoundTriggerHash(
            musicDependencyBoundTrigger,
            "e1706c94957de2d852784a32de5f8a4a20c0d6fecc5b8dbb4232149de3a6d86b");
        AssertRevertsToPassageBoundTriggerHash(
            libraryDependencyBoundTrigger,
            "6c47baaf887547bd673a73fefc07de810b7ecd2e2b572a60fb0f8beb2c53399a");

        Assert.That(musicDoors, Does.Contain("m_Name: Doors"));
        AssertExactComponentIds(musicDoors, "2103000011");
        Assert.That(ReadReferenceFileId(musicDoorsTransform, "m_GameObject"), Is.EqualTo("2103000010"));
        Assert.That(musicDoorsTransform, Does.Contain(
            "  m_Children:\n" +
            "  - {fileID: 552135203}\n" +
            "  - {fileID: 2300000086}\n" +
            "  m_Father: {fileID: 354156756}"));
        Assert.That(libraryDoors, Does.Contain("m_Name: Doors"));
        AssertExactComponentIds(libraryDoors, "2103000031");
        Assert.That(ReadReferenceFileId(libraryDoorsTransform, "m_GameObject"), Is.EqualTo("2103000030"));
        Assert.That(libraryDoorsTransform, Does.Contain(
            "  m_Children:\n" +
            "  - {fileID: 2300000076}\n" +
            "  - {fileID: 2300000081}\n" +
            "  m_Father: {fileID: 1367921345}"));

        AssertExactComponentIds(musicRoom, "354156756", "2102000001", "4100000003");
        Assert.That(musicRoom, Does.Contain("m_Name: Room_Music_Room"));
        Assert.That(ReadField(musicRoom, "m_IsActive"), Is.EqualTo("0"));
        Assert.That(musicRoomTransform, Does.Contain(
            "  m_Children:\n" +
            "  - {fileID: 1211449108}\n" +
            "  - {fileID: 1305291220}\n" +
            "  - {fileID: 2501000078}\n" +
            "  - {fileID: 2103000011}\n" +
            "  - {fileID: 598810695}\n" +
            "  - {fileID: 1055422819}\n" +
            "  - {fileID: 3601000011}\n" +
            "  - {fileID: 636459574}\n" +
            "  - {fileID: 1931113030}\n" +
            "  m_Father: {fileID: 668915133}"));
        Assert.That(ReadField(musicContent, "roomName"), Is.EqualTo("Music Room"));
        Assert.That(musicContent, Does.Contain(
            "roomBackgroundTexture: {fileID: 2800000, guid: 028084782cdcf3d4ab3b596624c8b7c5, type: 3}"));
        Assert.That(ReadReferenceFileId(musicContent, "perspectiveProfile"), Is.EqualTo("0"));
        Assert.That(musicView, Does.Contain($"guid: {RoomViewGuid}"));
        Assert.That(ReadReferenceFileId(musicView, "m_GameObject"), Is.EqualTo("354156755"));
        Assert.That(ReadReferenceGuid(musicView, "definition"),
            Is.EqualTo("c0f34d74a30db58bb2b87b6ec316120b"));
        Assert.That(ReadReferenceFileId(musicView, "legacyContentGroup"), Is.EqualTo("2102000001"));

        AssertExactComponentIds(libraryRoom, "1367921345", "2102000003", "4100000004");
        Assert.That(libraryRoom, Does.Contain("m_Name: Room_Library"));
        Assert.That(ReadField(libraryRoom, "m_IsActive"), Is.EqualTo("0"));
        Assert.That(libraryRoomTransform, Does.Contain(
            "  m_Children:\n" +
            "  - {fileID: 2108382636}\n" +
            "  - {fileID: 2501000029}\n" +
            "  - {fileID: 2103000031}\n" +
            "  - {fileID: 764430749}\n" +
            "  - {fileID: 834313925}\n" +
            "  - {fileID: 1762572057}\n" +
            "  - {fileID: 31657297}\n" +
            "  - {fileID: 1199634577}\n" +
            "  - {fileID: 3601000001}\n" +
            "  - {fileID: 1154736409}\n" +
            "  - {fileID: 1694187417}\n" +
            "  - {fileID: 861739721}\n" +
            "  m_Father: {fileID: 668915133}"));
        Assert.That(ReadField(libraryContent, "roomName"), Is.EqualTo("Library"));
        Assert.That(libraryContent, Does.Contain(
            "roomBackgroundTexture: {fileID: 2800000, guid: 0a85e4fdd73e4714fabde63002a457e7, type: 3}"));
        Assert.That(ReadReferenceFileId(libraryContent, "perspectiveProfile"), Is.EqualTo("0"));
        Assert.That(libraryView, Does.Contain($"guid: {RoomViewGuid}"));
        Assert.That(ReadReferenceFileId(libraryView, "m_GameObject"), Is.EqualTo("1367921344"));
        Assert.That(ReadReferenceGuid(libraryView, "definition"), Is.EqualTo(LibraryRoomDefinitionGuid));
        Assert.That(ReadReferenceFileId(libraryView, "legacyContentGroup"), Is.EqualTo("2102000003"));
        Assert.That(documents.Values.Count(document =>
            document.Contains($"guid: {RoomViewGuid}") &&
            document.Contains("m_GameObject: {fileID: 354156755}")), Is.EqualTo(1));
        Assert.That(documents.Values.Count(document =>
            document.Contains($"guid: {RoomViewGuid}") &&
            document.Contains("m_GameObject: {fileID: 1367921344}")), Is.EqualTo(1));
        Assert.That(musicPassage.TrimEnd('\r', '\n').Split('\n'), Has.Length.EqualTo(20));
        Assert.That(musicPassage, Does.Contain($"guid: {PassageGuid}"));
        Assert.That(ReadReferenceFileId(musicPassage, "m_GameObject"), Is.EqualTo("552135202"));
        Assert.That(ReadReferenceGuid(musicPassage, "definition"),
            Is.EqualTo(MusicLibraryPassageDefinitionGuid));
        Assert.That(ReadReferenceFileId(musicPassage, "sourceRoomView"), Is.EqualTo("4100000003"));
        Assert.That(ReadReferenceFileId(musicPassage, "reversePassage"), Is.EqualTo("4100000016"));
        Assert.That(musicPassage, Does.Contain(
            "approachAnchor:\n    logicalPosition: {x: 7.439471, y: -2.846709}"));
        Assert.That(musicPassage, Does.Contain(
            "arrivalAnchor:\n    logicalPosition: {x: -7.244175, y: -2.799095}"));
        Assert.That(ReadAnchorMigrationStage(musicPassage, musicRow.PassageFileId),
            Is.EqualTo(PassageAnchorMigrationStage.LegacySampling));

        Assert.That(libraryPassage.TrimEnd('\r', '\n').Split('\n'), Has.Length.EqualTo(20));
        Assert.That(libraryPassage, Does.Contain($"guid: {PassageGuid}"));
        Assert.That(ReadReferenceFileId(libraryPassage, "m_GameObject"), Is.EqualTo("2300000075"));
        Assert.That(ReadReferenceGuid(libraryPassage, "definition"),
            Is.EqualTo(LibraryMusicPassageDefinitionGuid));
        Assert.That(ReadReferenceFileId(libraryPassage, "sourceRoomView"), Is.EqualTo("4100000004"));
        Assert.That(ReadReferenceFileId(libraryPassage, "reversePassage"), Is.EqualTo("4100000015"));
        Assert.That(libraryPassage, Does.Contain(
            "approachAnchor:\n    logicalPosition: {x: -7.244175, y: -2.799095}"));
        Assert.That(libraryPassage, Does.Contain(
            "arrivalAnchor:\n    logicalPosition: {x: 7.439471, y: -2.846709}"));
        Assert.That(ReadAnchorMigrationStage(libraryPassage, libraryRow.PassageFileId),
            Is.EqualTo(PassageAnchorMigrationStage.LegacySampling));
        Assert.That(ReadAnchorMigrationStage(musicPassage, musicRow.PassageFileId),
            Is.EqualTo(ReadAnchorMigrationStage(libraryPassage, libraryRow.PassageFileId)));

        AssertBoundarySnapshot(
            documents,
            "1305291219",
            "1305291220",
            "1305291221",
            "354156756",
            "{x: 29.876, y: 33.383, z: 0}",
            "{x: 437.96204, y: 649.6864, z: 4.888888}",
            "    - - {x: 1.8357543, y: -0.5898214}\n" +
            "      - {x: 0.7589498, y: -0.22648144}\n" +
            "      - {x: -0.7301293, y: -0.21486577}\n" +
            "      - {x: -1.9802924, y: -0.5537329}\n" +
            "      - {x: -1.9835253, y: -0.7687184}\n" +
            "      - {x: 0.58778536, y: -0.80901694}\n" +
            "      - {x: 1.8477609, y: -0.7667822}");
        AssertMovementBlockerSnapshot(
            documents,
            "636459573", "636459574", "636459576", "636459575",
            "PlayerBlocker_grand_piano", "354156756", "598810694",
            "grand_piano", "Music Room", "Piano", "0.35",
            "    - - {x: -125.167755, y: -389}\n" +
            "      - {x: 295.1678, y: -389}\n" +
            "      - {x: 295.1678, y: -190.32002}\n" +
            "      - {x: -125.167755, y: -190.32002}");
        AssertMovementBlockerSnapshot(
            documents,
            "1931113029", "1931113030", "1931113032", "1931113031",
            "PlayerBlocker_piano_bench", "354156756", "1055422818",
            "piano_bench", "Music Room", "Seating", "0.32",
            "    - - {x: -129.28714, y: -403}\n" +
            "      - {x: 39.287136, y: -403}\n" +
            "      - {x: 39.287136, y: -327.54892}\n" +
            "      - {x: -129.28714, y: -327.54892}");
        AssertRoomAnchorSnapshot(
            documents,
            "3601000010", "3601000011", "3601000012", "354156756",
            "Ch2_Hide_Guest02", "Music Room", "{x: 306, y: -162}");

        AssertBoundarySnapshot(
            documents,
            "1199634576",
            "1199634577",
            "1199634578",
            "1367921345",
            "{x: 30.163, y: -25.584, z: 0}",
            "{x: 433.20193, y: 527.87036, z: 4.888888}",
            "    - - {x: 0.46773112, y: -0.19898641}\n" +
            "      - {x: -0.42814368, y: -0.1869666}\n" +
            "      - {x: -1.1921569, y: -0.32165936}\n" +
            "      - {x: -1.9963604, y: -0.5046056}\n" +
            "      - {x: -1.9896692, y: -0.8412314}\n" +
            "      - {x: 0.58778536, y: -0.80901694}\n" +
            "      - {x: 1.600851, y: -0.8410295}\n" +
            "      - {x: 1.5283847, y: -0.6539346}\n" +
            "      - {x: 1.5442408, y: -0.5275091}\n" +
            "      - {x: 1.61061, y: -0.40261146}");
        AssertMovementBlockerSnapshot(
            documents,
            "861739720", "861739721", "861739723", "861739722",
            "PlayerBlocker_librarychairandlamp_0", "1367921345", "31657296",
            "librarychairandlamp_0", "Library", "Chair", "0.3",
            "    - - {x: -744.629, y: -474.01364}\n" +
            "      - {x: -437.63107, y: -474.01364}\n" +
            "      - {x: -437.63107, y: -393.57547}\n" +
            "      - {x: -744.629, y: -393.57547}");
        AssertMovementBlockerSnapshot(
            documents,
            "1154736408", "1154736409", "1154736411", "1154736410",
            "PlayerBlocker_writing_desk", "1367921345", "764430748",
            "writing_desk", "Library", "Table", "0.3",
            "    - - {x: -223.30817, y: -460.6}\n" +
            "      - {x: 203.74817, y: -460.6}\n" +
            "      - {x: 203.74817, y: -324.4257}\n" +
            "      - {x: -223.30817, y: -324.4257}");
        AssertMovementBlockerSnapshot(
            documents,
            "1694187416", "1694187417", "1694187419", "1694187418",
            "PlayerBlocker_potted_plant", "1367921345", "1762572056",
            "potted_plant", "Library", "Plant", "0.26",
            "    - - {x: 679.4027, y: -512.55}\n" +
            "      - {x: 830.5972, y: -512.55}\n" +
            "      - {x: 830.5972, y: -340.43423}\n" +
            "      - {x: 679.4027, y: -340.43423}");
        AssertRoomAnchorSnapshot(
            documents,
            "3601000000", "3601000001", "3601000002", "1367921345",
            "Ch2_Hide_Guest01", "Library", "{x: -255, y: -181}");

        Assert.That(documents.Values.Count(document => document.Contains($"guid: {RoomViewGuid}")),
            Is.EqualTo(4));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")),
            Is.EqualTo(6));
        Assert.That(documents.Values.Count(document =>
            document.Contains($"guid: {PassageGuid}") &&
            (document.Contains("m_GameObject: {fileID: 552135202}") ||
             document.Contains("m_GameObject: {fileID: 2300000075}"))), Is.EqualTo(2));
        foreach (string groupFileId in new[]
                 {
                     "552135202", "552135203", "552135204",
                     "2300000075", "2300000076", "2300000079"
                 })
        {
            Assert.That(CountOccurrences(gameRoot, $"{{fileID: {groupFileId}}}"), Is.Zero,
                $"Group 02 legacy object {groupFileId} must not be registered with GameRoot yet.");
        }
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000004}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000015}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000016}"), Is.EqualTo(1));
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000013}\n" +
            "  - {fileID: 4100000014}\n" +
            "  - {fileID: 4100000015}\n" +
            "  - {fileID: 4100000016}"));

        Assert.That(CountOccurrences(database, "  - {fileID: 11400000, guid:"), Is.EqualTo(10));
        Assert.That(CountOccurrences(database, $"guid: {LibraryMusicPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {MusicLibraryPassageDefinitionGuid}"), Is.EqualTo(1));
        string canonicalData = string.Join("\n", Directory
            .GetFiles("Assets/_Chateau/Data", "*.asset", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        Assert.That(CountOccurrences(canonicalData, "stableId: room.library"), Is.EqualTo(1));
        Assert.That(CountOccurrences(canonicalData, "stableId: passage.music-room.library"), Is.EqualTo(1));
        Assert.That(CountOccurrences(canonicalData, "stableId: passage.library.music-room"), Is.EqualTo(1));
    }

    [Test]
    public void DrawingMusicCompleteCertificationPreservesAuthoredAnchorsCallersDependenciesAndTopology()
    {
        List<RouteInventoryRow> group = ReadInventory()
            .Where(row => row.Order == 1)
            .OrderBy(row => row.ComponentFileId)
            .ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string drawingOwner = RequireDocument(documents, "2300000095");
        string drawingTransform = RequireDocument(documents, "2300000096");
        string drawingTrigger = RequireDocument(documents, "2300000099");
        string musicOwner = RequireDocument(documents, "2300000085");
        string musicTransform = RequireDocument(documents, "2300000086");
        string musicTrigger = RequireDocument(documents, "2300000089");
        string drawingRoom = RequireDocument(documents, "2300000005");
        string drawingRoomTransform = RequireDocument(documents, "2300000006");
        string drawingContent = RequireDocument(documents, "2300000007");
        string drawingDoors = RequireDocument(documents, "2300000008");
        string drawingDoorsTransform = RequireDocument(documents, "2300000009");
        string musicRoom = RequireDocument(documents, "354156755");
        string musicRoomTransform = RequireDocument(documents, "354156756");
        string musicContent = RequireDocument(documents, "2102000001");
        string musicDoors = RequireDocument(documents, "2103000010");
        string musicDoorsTransform = RequireDocument(documents, "2103000011");
        string drawingView = RequireDocument(documents, "4100000002");
        string musicView = RequireDocument(documents, "4100000003");
        string drawingPassage = RequireDocument(documents, "4100000013");
        string musicPassage = RequireDocument(documents, "4100000014");
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string guestPanic = RequireDocument(documents, "3301000008");

        Assert.That(group, Has.Count.EqualTo(2));
        RouteInventoryRow musicRow = group.Single(row => row.ComponentFileId == "2300000089");
        RouteInventoryRow drawingRow = group.Single(row => row.ComponentFileId == "2300000099");
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.Select(row => row.PassageDefinitionGuid).OrderBy(value => value),
            Is.EqualTo(new[]
            {
                "01544de8f55723585d60e5c0915345fd",
                "3167361ca4c671298c0e84f43320619b"
            }));
        Assert.That(group.Select(row => row.PassageStableId).OrderBy(value => value),
            Is.EqualTo(new[]
            {
                "passage.drawing-room.music-room",
                "passage.music-room.drawing-room"
            }));
        Assert.That(musicRow.SourceRoomViewFileId, Is.EqualTo("4100000003"));
        Assert.That(drawingRow.SourceRoomViewFileId, Is.EqualTo("4100000002"));
        Assert.That(musicRow.PassageFileId, Is.EqualTo("4100000014"));
        Assert.That(drawingRow.PassageFileId, Is.EqualTo("4100000013"));
        Assert.That(musicRow.ApproachX, Is.EqualTo("-7.94"));
        Assert.That(musicRow.ApproachY, Is.EqualTo("-3.27"));
        Assert.That(musicRow.ArrivalX, Is.EqualTo("-7.16"));
        Assert.That(musicRow.ArrivalY, Is.EqualTo("-1.78"));
        Assert.That(drawingRow.ApproachX, Is.EqualTo("-7.16"));
        Assert.That(drawingRow.ApproachY, Is.EqualTo("-1.78"));
        Assert.That(drawingRow.ArrivalX, Is.EqualTo("-7.94"));
        Assert.That(drawingRow.ArrivalY, Is.EqualTo("-3.27"));
        foreach (RouteInventoryRow row in group)
        {
            AssertFinite(row.ApproachX, $"{row.LegacyDoorId} approach x");
            AssertFinite(row.ApproachY, $"{row.LegacyDoorId} approach y");
            AssertFinite(row.ArrivalX, $"{row.LegacyDoorId} arrival x");
            AssertFinite(row.ArrivalY, $"{row.LegacyDoorId} arrival y");
        }

        Assert.That(drawingOwner, Does.Contain("m_Name: DoorTrigger_DrawingRoom_MusicRoom"));
        Assert.That(drawingOwner, Does.Contain("- component: {fileID: 2300000096}"));
        Assert.That(drawingOwner, Does.Contain("- component: {fileID: 2300000099}"));
        Assert.That(drawingOwner, Does.Contain("- component: {fileID: 4100000013}"));
        Assert.That(Regex.Matches(drawingOwner, @"(?m)^  - component:").Count, Is.EqualTo(5));
        Assert.That(ReadReferenceFileId(drawingTransform, "m_Father"), Is.EqualTo("2300000009"));
        Assert.That(ReadField(drawingTransform, "m_LocalScale"), Is.EqualTo("{x: 1, y: 1, z: 1}"));
        Assert.That(ReadField(drawingTransform, "m_AnchoredPosition"), Is.EqualTo("{x: -628, y: 55}"));
        Assert.That(ReadField(drawingTransform, "m_SizeDelta"), Is.EqualTo("{x: 163.2982, y: 299.816}"));
        Assert.That(drawingDoors, Does.Contain("m_Name: Doors"));
        Assert.That(ReadReferenceFileId(drawingDoorsTransform, "m_GameObject"), Is.EqualTo("2300000008"));
        Assert.That(ReadReferenceFileId(drawingDoorsTransform, "m_Father"), Is.EqualTo("2300000006"));
        Assert.That(ReadReferenceFileId(drawingRoomTransform, "m_GameObject"), Is.EqualTo("2300000005"));
        Assert.That(ReadReferenceFileId(drawingRoomTransform, "m_Father"), Is.EqualTo("668915133"));
        AssertCallerBoundLegacyTriggerSnapshot(
            drawingTrigger,
            "2300000095",
            "2300000098",
            "Drawing Room",
            "DrawingRoom_MusicRoom",
            "Music Room",
            "4100000013");

        Assert.That(musicOwner, Does.Contain("m_Name: DoorTrigger_MusicRoom_DrawingRoom"));
        Assert.That(musicOwner, Does.Contain("- component: {fileID: 2300000086}"));
        Assert.That(musicOwner, Does.Contain("- component: {fileID: 2300000089}"));
        Assert.That(musicOwner, Does.Contain("- component: {fileID: 4100000014}"));
        Assert.That(Regex.Matches(musicOwner, @"(?m)^  - component:").Count, Is.EqualTo(5));
        Assert.That(ReadReferenceFileId(musicTransform, "m_Father"), Is.EqualTo("2103000011"));
        Assert.That(ReadField(musicTransform, "m_LocalScale"), Is.EqualTo("{x: 0.8625, y: 1, z: 1}"));
        Assert.That(ReadField(musicTransform, "m_AnchoredPosition"), Is.EqualTo("{x: -685, y: -36}"));
        Assert.That(ReadField(musicTransform, "m_SizeDelta"), Is.EqualTo("{x: 210.47021, y: 416.47205}"));
        Assert.That(musicDoors, Does.Contain("m_Name: Doors"));
        Assert.That(ReadReferenceFileId(musicDoorsTransform, "m_GameObject"), Is.EqualTo("2103000010"));
        Assert.That(ReadReferenceFileId(musicDoorsTransform, "m_Father"), Is.EqualTo("354156756"));
        Assert.That(ReadReferenceFileId(musicRoomTransform, "m_GameObject"), Is.EqualTo("354156755"));
        Assert.That(ReadReferenceFileId(musicRoomTransform, "m_Father"), Is.EqualTo("668915133"));
        AssertCallerBoundLegacyTriggerSnapshot(
            musicTrigger,
            "2300000085",
            "2300000088",
            "Music Room",
            "MusicRoom_DrawingRoom",
            "Drawing Room",
            "4100000014");

        Assert.That(drawingRoom, Does.Contain("m_Name: Room_Drawing_Room"));
        Assert.That(drawingRoom, Does.Contain("m_IsActive: 0"));
        Assert.That(ReadField(drawingContent, "roomName"), Is.EqualTo("Drawing Room"));
        Assert.That(drawingContent, Does.Contain(
            "roomBackgroundTexture: {fileID: 2800000, guid: 28c74b6dea1ed8e2c9c7d612355f9734, type: 3}"));
        Assert.That(ReadReferenceFileId(drawingContent, "perspectiveProfile"), Is.EqualTo("0"));
        Assert.That(musicRoom, Does.Contain("m_Name: Room_Music_Room"));
        Assert.That(musicRoom, Does.Contain("m_IsActive: 0"));
        Assert.That(ReadField(musicContent, "roomName"), Is.EqualTo("Music Room"));
        Assert.That(musicContent, Does.Contain(
            "roomBackgroundTexture: {fileID: 2800000, guid: 028084782cdcf3d4ab3b596624c8b7c5, type: 3}"));
        Assert.That(ReadReferenceFileId(musicContent, "perspectiveProfile"), Is.EqualTo("0"));
        Assert.That(musicRoom, Does.Contain("- component: {fileID: 4100000003}"));
        Assert.That(drawingView, Does.Contain($"guid: {RoomViewGuid}"));
        Assert.That(ReadReferenceFileId(drawingView, "m_GameObject"), Is.EqualTo("2300000005"));
        Assert.That(ReadReferenceGuid(drawingView, "definition"),
            Is.EqualTo("057575e9763145759aa12184580d27d8"));
        Assert.That(musicView, Does.Contain($"guid: {RoomViewGuid}"));
        Assert.That(ReadReferenceFileId(musicView, "m_GameObject"), Is.EqualTo("354156755"));
        Assert.That(ReadReferenceGuid(musicView, "definition"),
            Is.EqualTo("c0f34d74a30db58bb2b87b6ec316120b"));
        Assert.That(ReadReferenceFileId(musicView, "legacyContentGroup"), Is.EqualTo("2102000001"));
        Assert.That(drawingPassage, Does.Contain($"guid: {PassageGuid}"));
        Assert.That(ReadReferenceFileId(drawingPassage, "m_GameObject"), Is.EqualTo("2300000095"));
        Assert.That(ReadReferenceGuid(drawingPassage, "definition"),
            Is.EqualTo("3167361ca4c671298c0e84f43320619b"));
        Assert.That(ReadReferenceFileId(drawingPassage, "sourceRoomView"), Is.EqualTo("4100000002"));
        Assert.That(ReadReferenceFileId(drawingPassage, "reversePassage"), Is.EqualTo("4100000014"));
        Assert.That(drawingPassage, Does.Contain(
            "approachAnchor:\n    logicalPosition: {x: -7.16, y: -1.78}"));
        Assert.That(drawingPassage, Does.Contain(
            "arrivalAnchor:\n    logicalPosition: {x: -7.94, y: -3.27}"));
        Assert.That(ReadAnchorMigrationStage(drawingPassage, drawingRow.PassageFileId),
            Is.EqualTo(PassageAnchorMigrationStage.AuthoredAnchors),
            "Drawing-to-Music must own its exact authored Drawing-room approach and Music-room arrival.");
        Assert.That(musicPassage, Does.Contain($"guid: {PassageGuid}"));
        Assert.That(ReadReferenceFileId(musicPassage, "m_GameObject"), Is.EqualTo("2300000085"));
        Assert.That(ReadReferenceGuid(musicPassage, "definition"),
            Is.EqualTo("01544de8f55723585d60e5c0915345fd"));
        Assert.That(ReadReferenceFileId(musicPassage, "sourceRoomView"), Is.EqualTo("4100000003"));
        Assert.That(ReadReferenceFileId(musicPassage, "reversePassage"), Is.EqualTo("4100000013"));
        Assert.That(musicPassage, Does.Contain(
            "approachAnchor:\n    logicalPosition: {x: -7.94, y: -3.27}"));
        Assert.That(musicPassage, Does.Contain(
            "arrivalAnchor:\n    logicalPosition: {x: -7.16, y: -1.78}"));
        Assert.That(ReadAnchorMigrationStage(musicPassage, musicRow.PassageFileId),
            Is.EqualTo(PassageAnchorMigrationStage.AuthoredAnchors),
            "Music-to-Drawing must own its exact authored Music-room approach and Drawing-room arrival.");
        Assert.That(ReadAnchorMigrationStage(musicPassage, musicRow.PassageFileId),
            Is.EqualTo(ReadAnchorMigrationStage(drawingPassage, drawingRow.PassageFileId)),
            "A reciprocal route pair must cut anchor ownership over together.");
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000003}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000013}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000014}"), Is.EqualTo(1));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {RoomViewGuid}")), Is.EqualTo(4));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")), Is.EqualTo(6));
        Assert.That(ReadReferenceFileId(guestPanic, "leftExitTarget"), Is.EqualTo("2300000096"));
        Assert.That(ReadField(guestPanic, "leftExitTargetName"),
            Is.EqualTo("DoorTrigger_DrawingRoom_MusicRoom"));
    }

    private static List<RouteInventoryRow> ReadInventory()
    {
        Assert.That(File.Exists(InventoryPath), Is.True, $"Missing route inventory at {InventoryPath}.");
        string[] lines = File.ReadAllLines(InventoryPath);
        const string expectedHeader =
            "order,status,group,profile,component_file_id,owner,source_room,legacy_door_id,destination_room," +
            "partner_component_ids,passage_file_id,passage_definition_guid,passage_stable_id," +
            "source_room_view_file_id,approach_x,approach_y,arrival_x,arrival_y,notes";
        Assert.That(lines, Is.Not.Empty);
        Assert.That(lines[0], Is.EqualTo(expectedHeader));

        List<RouteInventoryRow> rows = new List<RouteInventoryRow>(lines.Length - 1);

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            string[] fields = lines[i].Split(',');
            Assert.That(fields, Has.Length.EqualTo(19), $"Malformed inventory row {i + 1}.");
            rows.Add(new RouteInventoryRow(fields));
        }

        return rows;
    }

    private static Dictionary<string, string> ReadUnityDocuments(string text)
    {
        MatchCollection headers = Regex.Matches(text, @"(?m)^--- !u!\d+ &(?<id>-?\d+)(?: stripped)?\r?\n");
        Dictionary<string, string> documents = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < headers.Count; i++)
        {
            int start = headers[i].Index;
            int end = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;
            documents.Add(headers[i].Groups["id"].Value, text.Substring(start, end - start));
        }

        return documents;
    }

    private static Dictionary<string, string> ReadGameObjectNames(Dictionary<string, string> documents)
    {
        Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> document in documents)
        {
            if (!Regex.IsMatch(document.Value, @"^--- !u!1 &-?\d+"))
            {
                continue;
            }

            Match name = Regex.Match(document.Value, @"(?m)^  m_Name: (?<value>.*)$");

            if (name.Success)
            {
                names.Add(document.Key, name.Groups["value"].Value.Trim());
            }
        }

        return names;
    }

    private static string RequireDocument(Dictionary<string, string> documents, string fileId)
    {
        Assert.That(documents.TryGetValue(fileId, out string document), Is.True,
            $"Missing Unity YAML document {fileId}.");
        return document;
    }

    private static string ReadField(string text, string fieldName)
    {
        Match match = Regex.Match(text, $@"(?m)^  {Regex.Escape(fieldName)}: (?<value>.*)$");
        Assert.That(match.Success, Is.True, $"Missing serialized field '{fieldName}'.");
        return match.Groups["value"].Value.Trim();
    }

    private static string ReadReferenceFileId(string text, string fieldName)
    {
        Match match = Regex.Match(text,
            $@"(?m)^  {Regex.Escape(fieldName)}: \{{fileID: (?<id>-?\d+)(?:,.*?)?\}}$");
        Assert.That(match.Success, Is.True, $"Missing object reference field '{fieldName}'.");
        return match.Groups["id"].Value;
    }

    private static string ReadReferenceGuid(string text, string fieldName)
    {
        Match match = Regex.Match(text,
            $@"(?m)^  {Regex.Escape(fieldName)}: \{{fileID: -?\d+, guid: (?<guid>[0-9a-f]{{32}}), type: \d+\}}$");
        Assert.That(match.Success, Is.True, $"Missing GUID reference field '{fieldName}'.");
        return match.Groups["guid"].Value;
    }

    private static void AssertExactComponentIds(string gameObject, params string[] expectedIds)
    {
        string[] componentIds = Regex.Matches(gameObject,
                @"(?m)^  - component: \{fileID: (?<id>-?\d+)\}$")
            .Cast<Match>()
            .Select(match => match.Groups["id"].Value)
            .ToArray();
        Assert.That(componentIds, Is.EqualTo(expectedIds));
    }

    private static void AssertMusicLibraryCallerBoundLegacyTriggerSnapshot(
        string trigger,
        string gameObjectFileId,
        string imageFileId,
        string sourceRoom,
        string legacyDoorId,
        string destinationRoom,
        string canonicalPassageFileId)
    {
        Assert.That(trigger, Does.Contain($"guid: {DoorTriggerGuid}"));
        Assert.That(ReadReferenceFileId(trigger, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadField(trigger, "sourceRoom"), Is.EqualTo(sourceRoom));
        Assert.That(ReadField(trigger, "doorName"), Is.EqualTo(legacyDoorId));
        Assert.That(ReadField(trigger, "destinationRoom"), Is.EqualTo(destinationRoom));
        Assert.That(ReadField(trigger, "requirePlayerInSourceRoom"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "useCameraSequence"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "triggerKind"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "stairwayDirection"), Is.EqualTo("0"));
        Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo(NavigationManagerFileId));
        Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(canonicalPassageFileId));
        Assert.That(ReadReferenceFileId(trigger, "image"), Is.EqualTo(imageFileId));
        Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo(DoorAudioSourceFileId));
        Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo(PlayerTransformFileId));
        Assert.That(ReadField(trigger, "makeInvisibleAtRuntime"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "runtimeColor"), Is.EqualTo("{r: 1, g: 1, b: 1, a: 0}"));
        Assert.That(ReadField(trigger, "bringToFront"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "useBottomScreenEdgeInteraction"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "bottomScreenEdgeActivationPixels"), Is.EqualTo("28"));
        Assert.That(ReadField(trigger, "disableGraphicRaycastForScreenEdgeInteraction"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "requirePlayerProximity"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "walkPlayerToTriggerWhenFar"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "autoActivateAfterApproach"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "playerObjectName"), Is.EqualTo("Player"));
        Assert.That(ReadField(trigger, "maxPlayerScreenDistance"), Is.EqualTo("145"));
        Assert.That(ReadField(trigger, "playDoorOpenSound"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "doorOpenAudioObjectName"), Is.EqualTo("Audio_DoorOpen"));
        Assert.That(trigger, Does.Contain(
            $"doorOpenSoundCatalog: {{fileID: 11400000, guid: {DoorCatalogGuid}, type: 2}}"));
        Assert.That(ReadField(trigger, "doorOpenSoundCatalogResourcePath"),
            Is.EqualTo("Audio/DoorOpenSoundCatalog"));
        Assert.That(ReadReferenceFileId(trigger, "stairwaySoundCatalog"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "stairwaySoundCatalogResourcePath"),
            Is.EqualTo("Audio/StairwaySoundCatalog"));
    }

    private static string AssertRevertsToDependenciesBoundTriggerHash(
        string trigger,
        string canonicalPassageFileId,
        string expectedSha256)
    {
        string callerLine = $"  canonicalPassage: {{fileID: {canonicalPassageFileId}}}\n";
        Assert.That(CountOccurrences(trigger, callerLine), Is.EqualTo(1),
            "Caller-bound trigger must contain exactly one canonical Passage line in the accepted location.");
        string reverted = trigger.Replace(callerLine, string.Empty);
        Assert.That(ComputeSha256(reverted), Is.EqualTo(expectedSha256),
            "Removing only the canonical caller must reproduce the accepted dependency-bound trigger bytes.");
        return reverted;
    }

    private static void AssertRevertsToPassageBoundTriggerHash(string trigger, string expectedSha256)
    {
        KeyValuePair<string, string>[] dependencyReplacements =
        {
            new KeyValuePair<string, string>(
                $"navigationManager: {{fileID: {NavigationManagerFileId}}}",
                "navigationManager: {fileID: 0}"),
            new KeyValuePair<string, string>(
                $"doorOpenAudioSource: {{fileID: {DoorAudioSourceFileId}}}",
                "doorOpenAudioSource: {fileID: 0}"),
            new KeyValuePair<string, string>(
                $"player: {{fileID: {PlayerTransformFileId}}}",
                "player: {fileID: 0}"),
            new KeyValuePair<string, string>(
                $"doorOpenSoundCatalog: {{fileID: 11400000, guid: {DoorCatalogGuid}, type: 2}}",
                "doorOpenSoundCatalog: {fileID: 0}")
        };

        string reverted = trigger;

        for (int i = 0; i < dependencyReplacements.Length; i++)
        {
            KeyValuePair<string, string> replacement = dependencyReplacements[i];
            Assert.That(CountOccurrences(reverted, replacement.Key), Is.EqualTo(1),
                $"Dependency-bound trigger must contain exactly one '{replacement.Key}'.");
            reverted = reverted.Replace(replacement.Key, replacement.Value);
        }

        Assert.That(ComputeSha256(reverted), Is.EqualTo(expectedSha256),
            "Nulling only the four direct dependencies must reproduce the accepted passage-bound trigger bytes.");
    }

    private static string ComputeSha256(string value)
    {
        using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
        {
            byte[] digest = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    private static void AssertBoundarySnapshot(
        Dictionary<string, string> documents,
        string gameObjectFileId,
        string transformFileId,
        string colliderFileId,
        string parentTransformFileId,
        string localPosition,
        string localScale,
        string pointBlock)
    {
        string gameObject = RequireDocument(documents, gameObjectFileId);
        string transform = RequireDocument(documents, transformFileId);
        string collider = RequireDocument(documents, colliderFileId);

        AssertExactComponentIds(gameObject, transformFileId, colliderFileId);
        Assert.That(gameObject, Does.Contain("m_Name: PlayerBoundary"));
        Assert.That(ReadReferenceFileId(transform, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadReferenceFileId(transform, "m_Father"), Is.EqualTo(parentTransformFileId));
        Assert.That(ReadField(transform, "m_LocalPosition"), Is.EqualTo(localPosition));
        Assert.That(ReadField(transform, "m_LocalScale"), Is.EqualTo(localScale));
        Assert.That(ReadReferenceFileId(collider, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadField(collider, "m_Enabled"), Is.EqualTo("1"));
        Assert.That(ReadField(collider, "m_IsTrigger"), Is.EqualTo("0"));
        Assert.That(collider, Does.Contain(
            "  m_Points:\n" +
            "    m_Paths:\n" +
            pointBlock + "\n" +
            "  m_UseDelaunayMesh: 1"));
    }

    private static void AssertMovementBlockerSnapshot(
        Dictionary<string, string> documents,
        string gameObjectFileId,
        string transformFileId,
        string colliderFileId,
        string markerFileId,
        string gameObjectName,
        string parentTransformFileId,
        string sourceObjectFileId,
        string sourceObjectName,
        string sourceRoomName,
        string category,
        string footprintHeightFraction,
        string pointBlock)
    {
        string gameObject = RequireDocument(documents, gameObjectFileId);
        string transform = RequireDocument(documents, transformFileId);
        string collider = RequireDocument(documents, colliderFileId);
        string marker = RequireDocument(documents, markerFileId);

        AssertExactComponentIds(gameObject, transformFileId, colliderFileId, markerFileId);
        Assert.That(gameObject, Does.Contain($"m_Name: {gameObjectName}"));
        Assert.That(ReadReferenceFileId(transform, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadReferenceFileId(transform, "m_Father"), Is.EqualTo(parentTransformFileId));
        Assert.That(ReadReferenceFileId(marker, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadReferenceFileId(marker, "sourceObject"), Is.EqualTo(sourceObjectFileId));
        Assert.That(ReadField(marker, "sourceObjectName"), Is.EqualTo(sourceObjectName));
        Assert.That(ReadField(marker, "sourceRoomName"), Is.EqualTo(sourceRoomName));
        Assert.That(ReadField(marker, "category"), Is.EqualTo(category));
        Assert.That(ReadField(marker, "footprintHeightFraction"), Is.EqualTo(footprintHeightFraction));
        Assert.That(ReadField(marker, "generatedByCollisionBoxTool"), Is.EqualTo("1"));
        Assert.That(ReadField(marker, "sortSourceRenderers"), Is.EqualTo("1"));
        Assert.That(ReadField(marker, "sourceSortingLayerName"), Is.EqualTo("People"));
        Assert.That(ReadField(marker, "sourceSortingOrderBase"), Is.EqualTo("1000"));
        Assert.That(ReadField(marker, "sourceSortingOrderPerYUnit"), Is.EqualTo("100"));
        Assert.That(ReadField(marker, "sourceSortingOrderOffset"), Is.EqualTo("0"));
        Assert.That(ReadField(marker, "forceSourcePivotSortPoint"), Is.EqualTo("1"));
        Assert.That(ReadReferenceFileId(collider, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadField(collider, "m_Enabled"), Is.EqualTo("1"));
        Assert.That(ReadField(collider, "m_IsTrigger"), Is.EqualTo("1"));
        Assert.That(collider, Does.Contain(
            "  m_Points:\n" +
            "    m_Paths:\n" +
            pointBlock + "\n" +
            "  m_UseDelaunayMesh: 1"));
    }

    private static void AssertRoomAnchorSnapshot(
        Dictionary<string, string> documents,
        string gameObjectFileId,
        string transformFileId,
        string anchorFileId,
        string parentTransformFileId,
        string anchorId,
        string roomId,
        string anchoredPosition)
    {
        string gameObject = RequireDocument(documents, gameObjectFileId);
        string transform = RequireDocument(documents, transformFileId);
        string anchor = RequireDocument(documents, anchorFileId);

        AssertExactComponentIds(gameObject, transformFileId, anchorFileId);
        Assert.That(gameObject, Does.Contain($"m_Name: {anchorId}"));
        Assert.That(ReadReferenceFileId(transform, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadReferenceFileId(transform, "m_Father"), Is.EqualTo(parentTransformFileId));
        Assert.That(ReadField(transform, "m_AnchoredPosition"), Is.EqualTo(anchoredPosition));
        Assert.That(ReadField(transform, "m_SizeDelta"), Is.EqualTo("{x: 100, y: 100}"));
        Assert.That(ReadReferenceFileId(anchor, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadField(anchor, "anchorId"), Is.EqualTo(anchorId));
        Assert.That(ReadField(anchor, "roomId"), Is.EqualTo(roomId));
    }

    private static void AssertCallerBoundLegacyTriggerSnapshot(
        string trigger,
        string gameObjectFileId,
        string imageFileId,
        string sourceRoom,
        string legacyDoorId,
        string destinationRoom,
        string canonicalPassageFileId)
    {
        Assert.That(trigger, Does.Contain($"guid: {DoorTriggerGuid}"));
        Assert.That(ReadReferenceFileId(trigger, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadField(trigger, "sourceRoom"), Is.EqualTo(sourceRoom));
        Assert.That(ReadField(trigger, "doorName"), Is.EqualTo(legacyDoorId));
        Assert.That(ReadField(trigger, "destinationRoom"), Is.EqualTo(destinationRoom));
        Assert.That(ReadField(trigger, "requirePlayerInSourceRoom"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "useCameraSequence"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "triggerKind"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "stairwayDirection"), Is.EqualTo("0"));
        Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo(NavigationManagerFileId));
        Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(canonicalPassageFileId));
        Assert.That(ReadReferenceFileId(trigger, "image"), Is.EqualTo(imageFileId));
        Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo(DoorAudioSourceFileId));
        Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo(PlayerTransformFileId));
        Assert.That(ReadField(trigger, "useBottomScreenEdgeInteraction"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "requirePlayerProximity"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "walkPlayerToTriggerWhenFar"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "autoActivateAfterApproach"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "maxPlayerScreenDistance"), Is.EqualTo("145"));
        Assert.That(ReadField(trigger, "playDoorOpenSound"), Is.EqualTo("1"));
        Assert.That(trigger, Does.Contain(
            $"doorOpenSoundCatalog: {{fileID: 11400000, guid: {DoorCatalogGuid}, type: 2}}"));
        Assert.That(ReadReferenceFileId(trigger, "stairwaySoundCatalog"), Is.EqualTo("0"));
    }

    private static void AssertStagedRouteSerialization(
        List<RouteInventoryRow> rows,
        RouteInventoryRow row,
        Dictionary<string, string> documents,
        string database,
        string gameRoot,
        string trigger,
        int stage)
    {
        if (stage < GetMigrationStage("data-authored"))
        {
            return;
        }

        Assert.That(row.PartnerComponentIds, Has.Length.EqualTo(1));
        RouteInventoryRow partner = rows.Single(candidate =>
            candidate.ComponentFileId == row.PartnerComponentIds[0]);
        Assert.That(partner.PassageDefinitionGuid, Is.Not.Empty);

        string definitionPath = AssetDatabase.GUIDToAssetPath(row.PassageDefinitionGuid);
        Assert.That(definitionPath, Is.Not.Empty, $"Missing definition asset for {row.PassageDefinitionGuid}.");
        string definitionText = File.ReadAllText(definitionPath);
        Assert.That(ReadField(definitionText, "stableId"), Is.EqualTo(row.PassageStableId));
        Assert.That(ReadField(definitionText, "legacyDoorId"), Is.EqualTo(row.LegacyDoorId));
        Assert.That(ReadField(definitionText, "kind"), Is.EqualTo(row.Profile == "inferred-stairway" ? "1" : "0"));
        Assert.That(ReadReferenceGuid(definitionText, "reverse"), Is.EqualTo(partner.PassageDefinitionGuid));

        string sourceDefinitionGuid = ReadReferenceGuid(definitionText, "sourceRoom");
        string destinationDefinitionGuid = ReadReferenceGuid(definitionText, "destinationRoom");
        Assert.That(CountOccurrences(database, $"guid: {row.PassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {sourceDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {destinationDefinitionGuid}"), Is.EqualTo(1));

        PassageDefinition definition = AssetDatabase.LoadAssetAtPath<PassageDefinition>(definitionPath);
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition.SourceRoom, Is.Not.Null);
        Assert.That(definition.DestinationRoom, Is.Not.Null);
        Assert.That(definition.SourceRoom.MatchesLegacyName(row.SourceRoom), Is.True);
        Assert.That(definition.DestinationRoom.MatchesLegacyName(row.DestinationRoom), Is.True);
        AssertRoomDefinitionIsValid(definition.SourceRoom);
        AssertRoomDefinitionIsValid(definition.DestinationRoom);
        ValidationReport definitionValidation = new ValidationReport();
        definition.ValidateConfiguration(definitionValidation);
        Assert.That(definitionValidation.HasErrors, Is.False,
            string.Join(" | ", definitionValidation.Messages.Select(message => message.ToString())));

        if (stage < GetMigrationStage("view-bound"))
        {
            return;
        }

        string sourceRoomView = RequireDocument(documents, row.SourceRoomViewFileId);
        Assert.That(sourceRoomView, Does.Contain($"guid: {RoomViewGuid}"));
        Assert.That(ReadReferenceGuid(sourceRoomView, "definition"), Is.EqualTo(sourceDefinitionGuid));
        string legacyContentGroup = RequireDocument(
            documents,
            ReadReferenceFileId(sourceRoomView, "legacyContentGroup"));
        Assert.That(definition.SourceRoom.MatchesLegacyName(ReadField(legacyContentGroup, "roomName")), Is.True);
        Assert.That(ReadReferenceFileId(sourceRoomView, "m_GameObject"),
            Is.EqualTo(ReadReferenceFileId(legacyContentGroup, "m_GameObject")));
        Assert.That(CountOccurrences(gameRoot, $"- {{fileID: {row.SourceRoomViewFileId}}}"), Is.EqualTo(1));

        if (stage < GetMigrationStage("passage-bound"))
        {
            return;
        }

        Assert.That(partner.PassageFileId, Is.Not.Empty);
        string passage = RequireDocument(documents, row.PassageFileId);
        string partnerPassage = RequireDocument(documents, partner.PassageFileId);
        PassageAnchorMigrationStage anchorMigrationStage =
            ReadAnchorMigrationStage(passage, row.PassageFileId);
        PassageAnchorMigrationStage partnerAnchorMigrationStage =
            ReadAnchorMigrationStage(partnerPassage, partner.PassageFileId);
        Assert.That(anchorMigrationStage, Is.EqualTo(GetExpectedAnchorMigrationStage(row.Status)),
            $"{row.LegacyDoorId} anchor ownership must match inventory status '{row.Status}'.");
        Assert.That(partnerAnchorMigrationStage, Is.EqualTo(anchorMigrationStage),
            $"{row.LegacyDoorId} and its reciprocal passage must share one anchor migration stage.");
        Assert.That(passage, Does.Contain($"guid: {PassageGuid}"));
        Assert.That(ReadReferenceFileId(passage, "m_GameObject"),
            Is.EqualTo(ReadReferenceFileId(trigger, "m_GameObject")));
        Assert.That(ReadReferenceGuid(passage, "definition"), Is.EqualTo(row.PassageDefinitionGuid));
        Assert.That(ReadReferenceFileId(passage, "sourceRoomView"), Is.EqualTo(row.SourceRoomViewFileId));
        Assert.That(ReadReferenceFileId(passage, "reversePassage"), Is.EqualTo(partner.PassageFileId));
        Assert.That(ReadReferenceFileId(partnerPassage, "reversePassage"), Is.EqualTo(row.PassageFileId));
        Assert.That(passage, Does.Contain(
            $"approachAnchor:\n    logicalPosition: {{x: {row.ApproachX}, y: {row.ApproachY}}}"));
        Assert.That(passage, Does.Contain(
            $"arrivalAnchor:\n    logicalPosition: {{x: {row.ArrivalX}, y: {row.ArrivalY}}}"));
        AssertFinite(row.ApproachX, $"{row.LegacyDoorId} approach x");
        AssertFinite(row.ApproachY, $"{row.LegacyDoorId} approach y");
        AssertFinite(row.ArrivalX, $"{row.LegacyDoorId} arrival x");
        AssertFinite(row.ArrivalY, $"{row.LegacyDoorId} arrival y");
        Assert.That(IsGameObjectDescendantOf(
            documents,
            ReadReferenceFileId(trigger, "m_GameObject"),
            ReadReferenceFileId(sourceRoomView, "m_GameObject")), Is.True);
        Assert.That(CountOccurrences(gameRoot, $"- {{fileID: {row.PassageFileId}}}"), Is.EqualTo(1));
    }

    private static PassageAnchorMigrationStage ReadAnchorMigrationStage(string passage, string passageFileId)
    {
        string serializedValue = ReadField(passage, "anchorMigrationStage");
        Assert.That(int.TryParse(
            serializedValue,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int scalar), Is.True,
            $"Passage {passageFileId} anchor migration stage must be an enum scalar.");
        Assert.That(Enum.IsDefined(typeof(PassageAnchorMigrationStage), scalar), Is.True,
            $"Passage {passageFileId} has unknown anchor migration stage {serializedValue}.");
        return (PassageAnchorMigrationStage)scalar;
    }

    private static PassageAnchorMigrationStage GetExpectedAnchorMigrationStage(string status)
    {
        switch (status)
        {
            case "passage-bound":
            case "dependencies-bound":
            case "caller-bound":
                return PassageAnchorMigrationStage.LegacySampling;
            case "arrival-owned":
                return PassageAnchorMigrationStage.AuthoredArrival;
            case "approach-owned":
            case "complete":
                return PassageAnchorMigrationStage.AuthoredAnchors;
            default:
                Assert.Fail($"Status '{status}' does not own a staged Passage anchor mode.");
                return PassageAnchorMigrationStage.LegacySampling;
        }
    }

    private static void AssertCertifiedAudioCatalogs(string trigger, RouteInventoryRow row)
    {
        if (row.Profile == "inferred-stairway")
        {
            Assert.That(trigger, Does.Contain(
                $"stairwaySoundCatalog: {{fileID: 11400000, guid: {StairwayCatalogGuid}, type: 2}}"));
            Assert.That(ReadReferenceFileId(trigger, "doorOpenSoundCatalog"), Is.EqualTo("0"));
            return;
        }

        Assert.That(trigger, Does.Contain(
            $"doorOpenSoundCatalog: {{fileID: 11400000, guid: {DoorCatalogGuid}, type: 2}}"));
        Assert.That(ReadReferenceFileId(trigger, "stairwaySoundCatalog"), Is.EqualTo("0"));
    }

    private static void AssertRoomDefinitionIsValid(CanonicalRoomDefinition definition)
    {
        ValidationReport validation = new ValidationReport();
        definition.ValidateConfiguration(validation);
        Assert.That(validation.HasErrors, Is.False,
            string.Join(" | ", validation.Messages.Select(message => message.ToString())));
    }

    private static bool IsGameObjectDescendantOf(
        Dictionary<string, string> documents,
        string childGameObjectFileId,
        string ancestorGameObjectFileId)
    {
        string transformFileId = FindTransformFileId(documents, childGameObjectFileId);
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(transformFileId) && transformFileId != "0" && visited.Add(transformFileId))
        {
            string transform = RequireDocument(documents, transformFileId);

            if (ReadReferenceFileId(transform, "m_GameObject") == ancestorGameObjectFileId)
            {
                return true;
            }

            transformFileId = ReadReferenceFileId(transform, "m_Father");
        }

        return false;
    }

    private static string FindTransformFileId(
        Dictionary<string, string> documents,
        string gameObjectFileId)
    {
        string gameObject = RequireDocument(documents, gameObjectFileId);
        MatchCollection components = Regex.Matches(gameObject,
            @"(?m)^  - component: \{fileID: (?<id>-?\d+)\}$");

        for (int i = 0; i < components.Count; i++)
        {
            string componentFileId = components[i].Groups["id"].Value;

            if (documents.TryGetValue(componentFileId, out string component) &&
                Regex.IsMatch(component, @"^--- !u!(?:4|224) &-?\d+"))
            {
                return componentFileId;
            }
        }

        Assert.Fail($"GameObject {gameObjectFileId} has no Transform/RectTransform document.");
        return string.Empty;
    }

    private static int GetMigrationStage(string status)
    {
        Assert.That(MigrationStages.TryGetValue(status, out int stage), Is.True,
            $"Unknown passage migration status '{status}'.");
        return stage;
    }

    private static bool AreReverseEndpoints(RouteInventoryRow left, RouteInventoryRow right)
    {
        return NormalizeRoomName(left.SourceRoom) == NormalizeRoomName(right.DestinationRoom) &&
            NormalizeRoomName(left.DestinationRoom) == NormalizeRoomName(right.SourceRoom);
    }

    private static string GetConnectivityKey(RouteInventoryRow row)
    {
        string[] endpoints =
        {
            NormalizeRoomName(row.SourceRoom),
            NormalizeRoomName(row.DestinationRoom)
        };
        Array.Sort(endpoints, StringComparer.Ordinal);
        return string.Join("|", endpoints);
    }

    private static void AssertFinite(string value, string label)
    {
        float parsed = ParseFloat(value);
        Assert.That(float.IsNaN(parsed) || float.IsInfinity(parsed), Is.False, $"{label} must be finite.");
    }

    private static float ParseFloat(string value)
    {
        Assert.That(float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed), Is.True,
            $"'{value}' is not an invariant float.");
        return parsed;
    }

    private static void AssertReciprocal(RouteInventoryRow row, RouteInventoryRow partner)
    {
        Assert.That(row.PartnerComponentIds, Is.EqualTo(new[] { partner.ComponentFileId }));
        Assert.That(NormalizeRoomName(row.SourceRoom), Is.EqualTo(NormalizeRoomName(partner.DestinationRoom)));
        Assert.That(NormalizeRoomName(row.DestinationRoom), Is.EqualTo(NormalizeRoomName(partner.SourceRoom)));
    }

    private static string NormalizeRoomName(string value)
    {
        string normalized = new string(value.Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant).ToArray());
        return normalized == "SIDESTAIRANDMUDROOM" ? "SIDESTAIRMUDROOM" : normalized;
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

    private sealed class RouteInventoryRow
    {
        public RouteInventoryRow(string[] fields)
        {
            Order = int.Parse(fields[0], CultureInfo.InvariantCulture);
            Status = fields[1];
            Group = fields[2];
            Profile = fields[3];
            ComponentFileId = fields[4];
            Owner = fields[5];
            SourceRoom = fields[6];
            LegacyDoorId = fields[7];
            DestinationRoom = fields[8];
            PartnerComponentIds = string.IsNullOrEmpty(fields[9])
                ? Array.Empty<string>()
                : fields[9].Split('|');
            PassageFileId = fields[10];
            PassageDefinitionGuid = fields[11];
            PassageStableId = fields[12];
            SourceRoomViewFileId = fields[13];
            ApproachX = fields[14];
            ApproachY = fields[15];
            ArrivalX = fields[16];
            ArrivalY = fields[17];
            Notes = fields[18];
        }

        public int Order { get; }
        public string Status { get; }
        public string Group { get; }
        public string Profile { get; }
        public string ComponentFileId { get; }
        public string Owner { get; }
        public string SourceRoom { get; }
        public string LegacyDoorId { get; }
        public string DestinationRoom { get; }
        public string[] PartnerComponentIds { get; }
        public string PassageFileId { get; }
        public string PassageDefinitionGuid { get; }
        public string PassageStableId { get; }
        public string SourceRoomViewFileId { get; }
        public string ApproachX { get; }
        public string ApproachY { get; }
        public string ArrivalX { get; }
        public string ArrivalY { get; }
        public string Notes { get; }
    }
}
