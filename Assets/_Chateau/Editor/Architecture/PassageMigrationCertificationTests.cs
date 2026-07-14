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
    private const string BallroomRoomDefinitionGuid = "d3b02ee2732843d484037af98d0e53e7";
    private const string LibraryBallroomPassageDefinitionGuid = "1de38005c66d42e2b2f1a65c59ce8ad8";
    private const string BallroomLibraryPassageDefinitionGuid = "0c60f4c2fe6f4e45947fc2a200cc6053";
    private const string DiningRoomDefinitionGuid = "0eb3282aded74fc4889f4321df8c5258";
    private const string EntranceDiningPassageDefinitionGuid = "30b5c4cfef2b45e2970b4cdac4b7a3ef";
    private const string DiningEntrancePassageDefinitionGuid = "94e16c6eca714188bced397612d48fff";
    private const string ButlersPantryRoomDefinitionGuid = "f2e9016bf08c45ebba8600eabc9e0b4d";
    private const string DiningButlersPantryPassageDefinitionGuid = "1dedaedb6c544e9e8ca4fd2a5be912cf";
    private const string ButlersPantryDiningPassageDefinitionGuid = "d42e018868914021a713f19df8fe60e8";
    private const string BilliardRoomDefinitionGuid = "bed158a9affd015fcc961340d9be5dd8";
    private const string ButlersPantryBilliardPassageDefinitionGuid = "71ea8ce4d4eb8fa7f107abe24d7c903e";
    private const string BilliardButlersPantryPassageDefinitionGuid = "be2f1b94b724dcfa061876e33bce02ca";
    private const string ServiceCorridorRoomDefinitionGuid = "85d51b6fcb4840458d45f66bbf6c233b";
    private const string ButlersPantryServiceCorridorPassageDefinitionGuid =
        "1b2d5f64523942a08e10402e24e88738";
    private const string ServiceCorridorButlersPantryPassageDefinitionGuid =
        "b485e8a6f574414a84f77437e02147f1";
    private const string KitchenRoomDefinitionGuid = "70531cbf9a67476f81f54b528029132e";
    private const string ServiceCorridorKitchenPassageDefinitionGuid =
        "2985cbdd527b4faaec13ff03091dbcd1";
    private const string KitchenServiceCorridorPassageDefinitionGuid =
        "453ad73cf2df1107f56be7a00daa3145";
    private const string ChapelRoomDefinitionGuid = "e3102dbfecc44551b6443ca88625a924";
    private const string ServiceCorridorChapelPassageDefinitionGuid =
        "fc2a0af2de3f4ade831c53f64fe0271b";
    private const string ChapelServiceCorridorPassageDefinitionGuid =
        "47e06869bf2b47a2980b0d02a53ee1df";
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

        Assert.That(certified, Has.Count.EqualTo(20),
            "Exactly the first ten reciprocal pairs through Service Corridor/Chapel are complete.");
        Assert.That(certified.Select(row => row.Order).Distinct().OrderBy(order => order),
            Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }),
            "Completed-route certification must cover groups 00 through 09 exactly once per reciprocal pair.");

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
        Assert.That(rows.Count(row => row.Status == "complete"), Is.EqualTo(20));
        Assert.That(rows.Count(row => row.Status == "characterized"), Is.Zero);
        Assert.That(rows.Count(row => row.Status == "dependencies-bound"), Is.Zero);
        Assert.That(rows.Count(row => row.Status == "caller-bound"), Is.Zero);
        Assert.That(rows.Count(row => row.Status == "queued"), Is.EqualTo(20));
        Assert.That(rows.Count(row => row.Status == "blocked-one-way"), Is.EqualTo(2));
        Assert.That(rows.Count(row => row.Status == "blocked-parallel"), Is.EqualTo(3));
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
    public void ButlersBilliardCompleteCertificationPreservesAnchorsCallersTopologyPropsAndCollision()
    {
        List<RouteInventoryRow> rows = ReadInventory();
        List<RouteInventoryRow> group = rows.Where(row => row.Order == 6)
            .OrderBy(row => row.ComponentFileId)
            .ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string database = File.ReadAllText(GameDatabasePath);
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string lifecycle = File.ReadAllText(LifecycleCharacterizationPath);
        string legacyDoorData = File.ReadAllText("Assets/Resources/Navigation/doors.txt");
        string canonicalData = string.Join("\n", Directory
            .GetFiles("Assets/_Chateau/Data", "*.asset", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        Assert.That(documents, Has.Count.EqualTo(6038));
        Assert.That(group, Has.Count.EqualTo(2));
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.All(row => row.Group == "Butlers-Billiard"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "template-certified"), Is.True);
        Assert.That(rows.Count(row => row.Status == "complete"), Is.EqualTo(20));
        Assert.That(rows.Count(row => row.Status == "dependencies-bound"), Is.Zero);
        Assert.That(rows.Count(row => row.Status == "queued"), Is.EqualTo(20));
        Assert.That(rows.Count(row => row.Status == "blocked-one-way"), Is.EqualTo(2));
        Assert.That(rows.Count(row => row.Status == "blocked-parallel"), Is.EqualTo(3));

        RouteInventoryRow pantryRow = group.Single(row => row.ComponentFileId == "1505671646");
        RouteInventoryRow billiardRow = group.Single(row => row.ComponentFileId == "2300000134");
        AssertReciprocal(pantryRow, billiardRow);
        AssertReciprocal(billiardRow, pantryRow);
        Assert.That(pantryRow.Owner, Is.EqualTo("DoorTrigger_Butlers_Pantry_BilliardRoom"));
        Assert.That(pantryRow.SourceRoom, Is.EqualTo("Butlers Pantry"));
        Assert.That(pantryRow.LegacyDoorId, Is.EqualTo("Butlers_Pantry_BilliardRoom"));
        Assert.That(pantryRow.DestinationRoom, Is.EqualTo("Billiard Room"));
        Assert.That(billiardRow.Owner, Is.EqualTo("DoorTrigger_BilliardRoom_ButlersPantry"));
        Assert.That(billiardRow.SourceRoom, Is.EqualTo("Billiard Room"));
        Assert.That(billiardRow.LegacyDoorId, Is.EqualTo("BilliardRoom_ButlersPantry"));
        Assert.That(billiardRow.DestinationRoom, Is.EqualTo("Butlers Pantry"));
        Assert.That(pantryRow.PassageFileId, Is.EqualTo("4100000023"));
        Assert.That(pantryRow.PassageDefinitionGuid,
            Is.EqualTo(ButlersPantryBilliardPassageDefinitionGuid));
        Assert.That(pantryRow.PassageStableId,
            Is.EqualTo("passage.butlers-pantry.billiard-room"));
        Assert.That(pantryRow.SourceRoomViewFileId, Is.EqualTo("4100000007"));
        Assert.That(pantryRow.ApproachX, Is.EqualTo("3.244461"));
        Assert.That(pantryRow.ApproachY, Is.EqualTo("-3.108338"));
        Assert.That(pantryRow.ArrivalX, Is.EqualTo("6.9"));
        Assert.That(pantryRow.ArrivalY, Is.EqualTo("-1.6"));
        Assert.That(billiardRow.PassageFileId, Is.EqualTo("4100000024"));
        Assert.That(billiardRow.PassageDefinitionGuid,
            Is.EqualTo(BilliardButlersPantryPassageDefinitionGuid));
        Assert.That(billiardRow.PassageStableId,
            Is.EqualTo("passage.billiard-room.butlers-pantry"));
        Assert.That(billiardRow.SourceRoomViewFileId, Is.EqualTo("4100000008"));
        Assert.That(billiardRow.ApproachX, Is.EqualTo("6.9"));
        Assert.That(billiardRow.ApproachY, Is.EqualTo("-1.6"));
        Assert.That(billiardRow.ArrivalX, Is.EqualTo("3.244461"));
        Assert.That(billiardRow.ArrivalY, Is.EqualTo("-3.108338"));
        Assert.That(pantryRow.ApproachX, Is.EqualTo(billiardRow.ArrivalX));
        Assert.That(pantryRow.ApproachY, Is.EqualTo(billiardRow.ArrivalY));
        Assert.That(pantryRow.ArrivalX, Is.EqualTo(billiardRow.ApproachX));
        Assert.That(pantryRow.ArrivalY, Is.EqualTo(billiardRow.ApproachY));

        string pantryOwner = RequireDocument(documents, "1505671644");
        string pantryTransform = RequireDocument(documents, "1505671645");
        string pantryTrigger = RequireDocument(documents, "1505671646");
        string billiardOwner = RequireDocument(documents, "2300000130");
        string billiardTransform = RequireDocument(documents, "2300000131");
        string billiardTrigger = RequireDocument(documents, "2300000134");
        AssertExactComponentIds(
            pantryOwner,
            "1505671645", "1505671648", "1505671647", "1505671646", "4100000023");
        Assert.That(pantryOwner, Does.Contain("m_Name: DoorTrigger_Butlers_Pantry_BilliardRoom"));
        Assert.That(ReadField(pantryOwner, "m_Layer"), Is.EqualTo("5"));
        Assert.That(ReadField(pantryOwner, "m_IsActive"), Is.EqualTo("1"));
        Assert.That(ReadReferenceFileId(pantryTransform, "m_GameObject"), Is.EqualTo("1505671644"));
        Assert.That(ReadReferenceFileId(pantryTransform, "m_Father"), Is.EqualTo("2300000024"));
        Assert.That(ReadField(pantryTransform, "m_LocalScale"), Is.EqualTo("{x: 1, y: 1, z: 1}"));
        Assert.That(ReadField(pantryTransform, "m_AnchoredPosition"), Is.EqualTo("{x: 304.7408, y: 0.153}"));
        Assert.That(ReadField(pantryTransform, "m_SizeDelta"), Is.EqualTo("{x: 187.9324, y: 422.4507}"));
        AssertCertifiedLegacyTriggerSnapshot(
            pantryTrigger,
            "1505671644",
            "1505671647",
            "Butlers Pantry",
            "Butlers_Pantry_BilliardRoom",
            "Billiard Room",
            "4100000023",
            "8fec46a589e1403dd32f2ff4ebb8d45b3abf5138ac4b8341e3a9ff259edf95d5",
            "a0d137d952dcabf1a0760cb78ac0e7c449ffbfd5006a2ec9ea3ea24bd30799d6",
            "68f081c9b30a039aa2e6c9f4a2ad53e875d9f61901d348f7603219ea61ecad63");

        AssertExactComponentIds(
            billiardOwner,
            "2300000131", "2300000132", "2300000133", "2300000134", "4100000024");
        Assert.That(billiardOwner, Does.Contain("m_Name: DoorTrigger_BilliardRoom_ButlersPantry"));
        Assert.That(ReadField(billiardOwner, "m_Layer"), Is.EqualTo("5"));
        Assert.That(ReadField(billiardOwner, "m_IsActive"), Is.EqualTo("1"));
        Assert.That(ReadReferenceFileId(billiardTransform, "m_GameObject"), Is.EqualTo("2300000130"));
        Assert.That(ReadReferenceFileId(billiardTransform, "m_Father"), Is.EqualTo("2300000014"));
        Assert.That(ReadField(billiardTransform, "m_LocalScale"), Is.EqualTo("{x: 1, y: 1, z: 1}"));
        Assert.That(ReadField(billiardTransform, "m_AnchoredPosition"), Is.EqualTo("{x: 565, y: 52.91918}"));
        Assert.That(ReadField(billiardTransform, "m_SizeDelta"), Is.EqualTo("{x: 120, y: 333.8383}"));
        AssertCertifiedLegacyTriggerSnapshot(
            billiardTrigger,
            "2300000130",
            "2300000133",
            "Billiard Room",
            "BilliardRoom_ButlersPantry",
            "Butlers Pantry",
            "4100000024",
            "2f88d6260b3328e1d36ae469d97d89ad4463279236f2623145c82d88ef40ef5a",
            "4a6d7375ebb09d1dbe3ddc6f9d884d1def330a730509308b96dc1b716f2331a6",
            "d4aa05562a67946235514d1f53fc1b71ca299308c205240fbf5686e5861afd26");

        string billiardRoom = RequireDocument(documents, "2300000010");
        string billiardRoomTransform = RequireDocument(documents, "2300000011");
        string billiardContent = RequireDocument(documents, "2300000012");
        string billiardDoors = RequireDocument(documents, "2300000013");
        string billiardDoorsTransform = RequireDocument(documents, "2300000014");
        string billiardView = RequireDocument(documents, "4100000008");
        string pantryPassage = RequireDocument(documents, "4100000023");
        string billiardPassage = RequireDocument(documents, "4100000024");
        AssertExactComponentIds(billiardRoom, "2300000011", "2300000012", "4100000008");
        Assert.That(billiardRoom, Does.Contain("m_Name: Room_Billiard_Room"));
        Assert.That(ReadField(billiardRoom, "m_IsActive"), Is.EqualTo("0"));
        Assert.That(billiardRoomTransform, Does.Contain(
            "  m_Children:\n" +
            "  - {fileID: 638366274}\n" +
            "  - {fileID: 745575663}\n" +
            "  - {fileID: 1595128726}\n" +
            "  - {fileID: 2300000014}\n" +
            "  - {fileID: 1690104634}\n" +
            "  - {fileID: 21640001}\n" +
            "  - {fileID: 21640005}\n" +
            "  - {fileID: 3601000021}\n" +
            "  - {fileID: 1298202151}\n" +
            "  - {fileID: 1283109191}\n" +
            "  - {fileID: 754090197}\n" +
            "  m_Father: {fileID: 668915133}"));
        Assert.That(ReadField(billiardContent, "roomName"), Is.EqualTo("Billiard Room"));
        Assert.That(billiardContent, Does.Contain(
            "roomBackgroundTexture: {fileID: 2800000, guid: 5987c5a8b3a09fc1ca848ac0ece03658, type: 3}"));
        Assert.That(ReadReferenceFileId(billiardContent, "perspectiveProfile"), Is.EqualTo("0"));
        Assert.That(ReadReferenceFileId(billiardView, "m_GameObject"), Is.EqualTo("2300000010"));
        Assert.That(ReadReferenceGuid(billiardView, "definition"), Is.EqualTo(BilliardRoomDefinitionGuid));
        Assert.That(ReadReferenceFileId(billiardView, "legacyContentGroup"), Is.EqualTo("2300000012"));
        AssertExactComponentIds(billiardDoors, "2300000014");
        Assert.That(billiardDoors, Does.Contain("m_Name: Doors"));
        Assert.That(ReadReferenceFileId(billiardDoorsTransform, "m_GameObject"), Is.EqualTo("2300000013"));
        Assert.That(billiardDoorsTransform, Does.Contain(
            "  m_Children:\n" +
            "  - {fileID: 2300000121}\n" +
            "  - {fileID: 2300000131}\n" +
            "  m_Father: {fileID: 2300000011}"));

        foreach (RouteInventoryRow row in group)
        {
            string trigger = RequireDocument(documents, row.ComponentFileId);
            AssertStagedRouteSerialization(rows, row, documents, database, gameRoot, trigger,
                GetMigrationStage("complete"));
            Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(row.PassageFileId));
        }
        AssertPassivePassageSnapshot(
            pantryPassage,
            "1505671644",
            ButlersPantryBilliardPassageDefinitionGuid,
            "4100000007",
            "4100000024",
            "{x: 3.244461, y: -3.108338}",
            "{x: 6.9, y: -1.6}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageSnapshot(
            billiardPassage,
            "2300000130",
            BilliardButlersPantryPassageDefinitionGuid,
            "4100000008",
            "4100000023",
            "{x: 6.9, y: -1.6}",
            "{x: 3.244461, y: -3.108338}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        Assert.That(ComputeSha256(pantryPassage),
            Is.EqualTo("61ae1a519da8a47275e2713b3182b7297c049b0eb9b2d590f5cb5037607abcc2"));
        Assert.That(ComputeSha256(billiardPassage),
            Is.EqualTo("b46ec8f0b48730348e80287e50393c153ce05621c35db00741dfd7af50e6b1ce"));

        AssertMovementBlockerSnapshot(
            documents,
            "1298202150", "1298202151", "1298202153", "1298202152",
            "PlayerBlocker_billiard_table", "2300000011", "1690104633",
            "billiard_table", "Billiard Room", "Table", "0.3",
            "    - - {x: -327.9786, y: -389.89835}\n" +
            "      - {x: 222.57861, y: -389.89835}\n" +
            "      - {x: 222.57861, y: -283.81934}\n" +
            "      - {x: -327.9786, y: -283.81934}");
        AssertMovementBlockerSnapshot(
            documents,
            "1283109190", "1283109191", "1283109193", "1283109192",
            "PlayerBlocker_billiard_left_armchair", "2300000011", "21640000",
            "billiard_left_armchair", "Billiard Room", "Chair", "0.3",
            "    - - {x: -796.5127, y: -561.52}\n" +
            "      - {x: -546.3674, y: -561.52}\n" +
            "      - {x: -546.3674, y: -429.46884}\n" +
            "      - {x: -796.5127, y: -429.46884}");
        AssertMovementBlockerSnapshot(
            documents,
            "754090196", "754090197", "754090199", "754090198",
            "PlayerBlocker_billiard_left_lamp_table", "2300000011", "21640004",
            "billiard_left_lamp_table", "Billiard Room", "Table", "0.3",
            "    - - {x: -750.90137, y: -368.68}\n" +
            "      - {x: -596.5385, y: -368.68}\n" +
            "      - {x: -596.5385, y: -274.2635}\n" +
            "      - {x: -750.90137, y: -274.2635}");

        AssertForegroundCutoutSnapshot(
            documents,
            "1690104633", "1690104634", "1690104636",
            "billiard_table", "{x: -52.7, y: -213.1, z: -6570.105}",
            "{x: 99.608696, y: 97.40954, z: 73.00117}",
            "77397d773be7a0186bf0ec435c6eb2ef",
            "1690104635", "{x: 0, y: 3.685}", "{x: 13.82, y: 7.37}");
        AssertForegroundCutoutSnapshot(
            documents,
            "21640000", "21640001", "21640002",
            "billiard_left_armchair", "{x: -671.44, y: -561.52, z: -6570.105}",
            "{x: 37.800003, y: 36.05, z: 73.00117}",
            "8017e9546fbe40beaaa61662f1e8191a");
        AssertForegroundCutoutSnapshot(
            documents,
            "21640004", "21640005", "21640006",
            "billiard_left_lamp_table", "{x: -673.72, y: -368.68, z: -6570.105}",
            "{x: 32.524826, y: 29.167902, z: 73.00117}",
            "dfcaa33b0d194eb0a42a334d83b90fb8");
        Assert.That(File.Exists("Assets/Art/Objects/green_armchair.png"), Is.True);
        Assert.That(File.Exists("Assets/Art/Objects/round_lamp_table.png"), Is.True);

        List<string> passageDocuments = documents.Values
            .Where(document => document.Contains($"guid: {PassageGuid}"))
            .ToList();
        List<string> roomViewDocuments = documents.Values
            .Where(document => document.Contains($"guid: {RoomViewGuid}"))
            .ToList();
        List<string> triggerDocuments = documents.Values
            .Where(document => document.Contains($"guid: {DoorTriggerGuid}"))
            .ToList();
        Assert.That(passageDocuments, Has.Count.EqualTo(20));
        Assert.That(roomViewDocuments, Has.Count.EqualTo(11));
        Assert.That(passageDocuments.Count(document => document.Contains("anchorMigrationStage: 0")), Is.Zero);
        Assert.That(passageDocuments.Count(document => document.Contains("anchorMigrationStage: 1")), Is.Zero);
        Assert.That(passageDocuments.Count(document => document.Contains("anchorMigrationStage: 2")), Is.EqualTo(20));
        Assert.That(triggerDocuments, Has.Count.EqualTo(45));
        Assert.That(triggerDocuments.Count(document =>
            document.Contains($"navigationManager: {{fileID: {NavigationManagerFileId}}}")), Is.EqualTo(20));
        Assert.That(triggerDocuments.Count(document => document.Contains("navigationManager: {fileID: 0}")),
            Is.EqualTo(25));
        Assert.That(triggerDocuments.Count(document => document.Contains("canonicalPassage:")), Is.EqualTo(20));
        Assert.That(triggerDocuments.Count(document => !document.Contains("canonicalPassage:")), Is.EqualTo(25));
        Assert.That(passageDocuments.Count(document =>
            document.Contains("m_GameObject: {fileID: 1505671644}") ||
            document.Contains("m_GameObject: {fileID: 2300000130}")), Is.EqualTo(2));
        Assert.That(roomViewDocuments.Count(document =>
            document.Contains("m_GameObject: {fileID: 2300000010}")), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000008}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000023}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000024}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, "  - {fileID: 11400000, guid:"), Is.EqualTo(39));
        Assert.That(CountOccurrences(database, $"guid: {BilliardRoomDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {ButlersPantryBilliardPassageDefinitionGuid}"),
            Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {BilliardButlersPantryPassageDefinitionGuid}"),
            Is.EqualTo(1));
        Assert.That(canonicalData, Does.Contain("stableId: room.billiard-room"));
        Assert.That(canonicalData, Does.Contain("stableId: passage.butlers-pantry.billiard-room"));
        Assert.That(canonicalData, Does.Contain("stableId: passage.billiard-room.butlers-pantry"));
        Assert.That(legacyDoorData, Does.Not.Contain("Butlers_Pantry_BilliardRoom"));
        Assert.That(legacyDoorData, Does.Not.Contain("BilliardRoom_ButlersPantry"));

        Assert.That(lifecycle, Does.Contain(
            "ButlersPantryBilliardRoomAuthoredAnchorPassagesUseInvariantApproachesAndArrivals"));
        Assert.That(lifecycle, Does.Contain("[ButlersBilliardStageOneArrivalProof]"),
            "The final lifecycle must retain the tests-only arrival-ownership preflight.");
        Assert.That(lifecycle, Does.Contain("[ButlersBilliardAuthoredPrimary]"));
        Assert.That(lifecycle, Does.Contain("[ButlersBilliardAuthoredAspect]"));
        Assert.That(lifecycle, Does.Contain("[ButlersBilliardAuthoredMaximumZoom]"));
        Assert.That(lifecycle, Does.Contain("[ButlersBilliardAuthoredProfile]"));
        Assert.That(lifecycle, Does.Contain(
            "cd248f01301448b5cd807cc9331e58d99bd59a139bb772dab352869527b9a6eb"),
            "The no-trailing-newline seven-line authored observation fingerprint must remain explicit.");
        Assert.That(lifecycle, Does.Contain("profiles=invariant callers=bound serializedDependencies=bound"));
        Assert.That(lifecycle, Does.Contain(
            "float[] expectedForwardDistances = { 189.196f, 259.674f, 265.915f, 364.763f };"));
        Assert.That(lifecycle, Does.Contain(
            "float[] expectedReverseDistances = { 438.675f, 619.196f, 616.589f, 820.181f };"));
        Assert.That(lifecycle, Does.Contain("new Vector2(2.744461f, -2.748338f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(6.575521f, -1.484375f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(5.191498f, -2.748338f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(3.244461f, -3.108338f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(6.9f, -1.6f)"));
        Assert.That(lifecycle, Does.Contain("maximum.ForwardScreenDistance, Is.EqualTo(419.821f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(3.168258f, -3.172733f)"));
        Assert.That(lifecycle, Does.Contain("maximum.ReverseScreenDistance, Is.EqualTo(943.982f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(7.590905f, -1.71359f)"));
        Assert.That(lifecycle, Does.Contain("observationProfileLines, Has.Count.EqualTo(7)"));
        Assert.That(lifecycle, Does.Contain("observationProfile.EndsWith(\"\\n\""));
    }

    [Test]
    public void ButlersServiceCompleteCertificationPreservesAnchorsCallersTopologyPropsAndCollision()
    {
        List<RouteInventoryRow> rows = ReadInventory();
        List<RouteInventoryRow> group = rows.Where(row => row.Order == 7)
            .OrderBy(row => row.ComponentFileId)
            .ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string database = File.ReadAllText(GameDatabasePath);
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string lifecycle = File.ReadAllText(LifecycleCharacterizationPath);
        string legacyDoorData = File.ReadAllText("Assets/Resources/Navigation/doors.txt");

        Assert.That(documents, Has.Count.EqualTo(6038));
        Assert.That(group, Has.Count.EqualTo(2));
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.All(row => row.Group == "Butlers-Service"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "template-certified"), Is.True);

        RouteInventoryRow pantryRow = group.Single(row => row.ComponentFileId == "2300000149");
        RouteInventoryRow serviceRow = group.Single(row => row.ComponentFileId == "2300000154");
        AssertReciprocal(pantryRow, serviceRow);
        AssertReciprocal(serviceRow, pantryRow);
        Assert.That(pantryRow.PassageFileId, Is.EqualTo("4100000025"));
        Assert.That(serviceRow.PassageFileId, Is.EqualTo("4100000026"));
        Assert.That(pantryRow.PassageDefinitionGuid,
            Is.EqualTo(ButlersPantryServiceCorridorPassageDefinitionGuid));
        Assert.That(serviceRow.PassageDefinitionGuid,
            Is.EqualTo(ServiceCorridorButlersPantryPassageDefinitionGuid));
        Assert.That(pantryRow.PassageStableId,
            Is.EqualTo("passage.butlers-pantry.service-corridor"));
        Assert.That(serviceRow.PassageStableId,
            Is.EqualTo("passage.service-corridor.butlers-pantry"));
        Assert.That(pantryRow.SourceRoomViewFileId, Is.EqualTo("4100000007"));
        Assert.That(serviceRow.SourceRoomViewFileId, Is.EqualTo("4100000009"));
        Assert.That(new[] { pantryRow.ApproachX, pantryRow.ApproachY, pantryRow.ArrivalX, pantryRow.ArrivalY },
            Is.EqualTo(new[] { "7", "-2.8", "4.2", "-3.3" }));
        Assert.That(new[] { serviceRow.ApproachX, serviceRow.ApproachY, serviceRow.ArrivalX, serviceRow.ArrivalY },
            Is.EqualTo(new[] { "4.2", "-3.3", "7", "-2.8" }));

        foreach (RouteInventoryRow row in group)
        {
            string trigger = RequireDocument(documents, row.ComponentFileId);
            AssertStagedRouteSerialization(
                rows,
                row,
                documents,
                database,
                gameRoot,
                trigger,
                GetMigrationStage("complete"));
            Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(row.PassageFileId));
            Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo(NavigationManagerFileId));
            Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo(PlayerTransformFileId));
            Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo(DoorAudioSourceFileId));
            AssertCertifiedAudioCatalogs(trigger, row);
            Assert.That(ReadField(trigger, "maxPlayerScreenDistance"), Is.EqualTo("145"));
        }

        string serviceRoom = RequireDocument(documents, "2300000025");
        string serviceTransform = RequireDocument(documents, "2300000026");
        string serviceContent = RequireDocument(documents, "2300000027");
        string serviceView = RequireDocument(documents, "4100000009");
        Assert.That(serviceRoom, Does.Contain("m_Name: Room_Service_Corridor"));
        Assert.That(serviceRoom, Does.Contain("- component: {fileID: 4100000009}"));
        Assert.That(CountOccurrences(serviceTransform, "  - {fileID:"), Is.EqualTo(40));
        Assert.That(ReadField(serviceContent, "roomName"), Is.EqualTo("Service Corridor"));
        Assert.That(ReadReferenceGuid(serviceView, "definition"), Is.EqualTo(ServiceCorridorRoomDefinitionGuid));
        Assert.That(ReadReferenceFileId(serviceView, "legacyContentGroup"), Is.EqualTo("2300000027"));

        string pantryOwner = RequireDocument(documents, "2300000145");
        string pantryTransform = RequireDocument(documents, "2300000146");
        string serviceOwner = RequireDocument(documents, "2300000150");
        string serviceDoorTransform = RequireDocument(documents, "2300000151");
        Assert.That(pantryOwner, Does.Contain("- component: {fileID: 4100000025}"));
        Assert.That(serviceOwner, Does.Contain("- component: {fileID: 4100000026}"));
        Assert.That(pantryTransform, Does.Contain("m_AnchoredPosition: {x: 591.2165, y: 33.108276}"));
        Assert.That(pantryTransform, Does.Contain("m_SizeDelta: {x: 188.3424, y: 453.9467}"));
        Assert.That(serviceDoorTransform, Does.Contain("m_AnchoredPosition: {x: 352, y: 28}"));
        Assert.That(serviceDoorTransform, Does.Contain("m_SizeDelta: {x: 124.2894, y: 524.2852}"));

        string leftProp = RequireDocument(documents, "21631084");
        string leftBlocker = RequireDocument(documents, "334646578");
        string rightProp = RequireDocument(documents, "461008707");
        string rightBlocker = RequireDocument(documents, "839535680");
        Assert.That(leftProp, Does.Contain("m_Name: service_corridor_left_table_0"));
        Assert.That(leftBlocker, Does.Contain("m_Name: PlayerBlocker_service_corridor_left_table_0"));
        Assert.That(rightProp, Does.Contain("m_Name: service_corridor_right_desk_0"));
        Assert.That(rightBlocker, Does.Contain("m_Name: PlayerBlocker_service_corridor_right_desk_0"));

        string forwardDefinition = File.ReadAllText(
            "Assets/_Chateau/Data/World/Passages/Passage_ButlersPantry_ServiceCorridor.asset");
        string reverseDefinition = File.ReadAllText(
            "Assets/_Chateau/Data/World/Passages/Passage_ServiceCorridor_ButlersPantry.asset");
        Assert.That(forwardDefinition, Does.Contain("stableId: passage.butlers-pantry.service-corridor"));
        Assert.That(forwardDefinition, Does.Contain(
            $"reverse: {{fileID: 11400000, guid: {ServiceCorridorButlersPantryPassageDefinitionGuid}, type: 2}}"));
        Assert.That(reverseDefinition, Does.Contain("stableId: passage.service-corridor.butlers-pantry"));
        Assert.That(reverseDefinition, Does.Contain(
            $"reverse: {{fileID: 11400000, guid: {ButlersPantryServiceCorridorPassageDefinitionGuid}, type: 2}}"));
        Assert.That(CountOccurrences(database, $"guid: {ServiceCorridorRoomDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database,
            $"guid: {ButlersPantryServiceCorridorPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database,
            $"guid: {ServiceCorridorButlersPantryPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(legacyDoorData, Does.Contain("ButlersPantry_ServiceCorridor: Service Corridor"));
        Assert.That(legacyDoorData, Does.Contain("ServiceCorridor_ButlersPantry: Butler's Pantry"));

        Assert.That(lifecycle, Does.Contain(
            "ButlersPantryServiceCorridorAuthoredAnchorPassagesUseInvariantApproachesAndArrivals"));
        Assert.That(lifecycle, Does.Contain("[ButlersServiceAuthoredAspect]"));
        Assert.That(lifecycle, Does.Contain("[ButlersServiceAuthoredMaximumZoom]"));
        Assert.That(lifecycle, Does.Contain("[ButlersServiceAuthoredProfile]"));
        Assert.That(lifecycle, Does.Contain(
            "7cc7c8706a02443b30c91c7a3cdcce40b7c04fec2f54e3f2eb7d5acad7058ea9"));
        Assert.That(lifecycle, Does.Contain(
            "pantryScreenDistance={pantryDistance:0.###}"));
        Assert.That(lifecycle, Does.Contain(
            "serviceScreenDistance={serviceDistance:0.###}"));
        Assert.That(lifecycle, Does.Contain("blockers=0/2"));
        Assert.That(lifecycle, Does.Contain("observationProfile.EndsWith(\"\\n\""));
    }

    [Test]
    public void ServiceKitchenCompleteCertificationUsesRoomViewLocalAnchorsAndPreservesRouteOwnership()
    {
        List<RouteInventoryRow> rows = ReadInventory();
        List<RouteInventoryRow> group = rows.Where(row => row.Order == 8)
            .OrderBy(row => row.ComponentFileId)
            .ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string database = File.ReadAllText(GameDatabasePath);
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string lifecycle = File.ReadAllText(LifecycleCharacterizationPath);
        string legacyDoorData = File.ReadAllText("Assets/Resources/Navigation/doors.txt");

        Assert.That(documents, Has.Count.EqualTo(6038));
        Assert.That(group, Has.Count.EqualTo(2));
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.All(row => row.Group == "Service-Kitchen"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "roomview-local-certified"), Is.True);

        RouteInventoryRow serviceRow = group.Single(row => row.ComponentFileId == "2300000164");
        RouteInventoryRow kitchenRow = group.Single(row => row.ComponentFileId == "802263367");
        AssertReciprocal(serviceRow, kitchenRow);
        AssertReciprocal(kitchenRow, serviceRow);
        Assert.That(serviceRow.PassageFileId, Is.EqualTo("4100000027"));
        Assert.That(kitchenRow.PassageFileId, Is.EqualTo("4100000028"));
        Assert.That(serviceRow.PassageDefinitionGuid,
            Is.EqualTo(ServiceCorridorKitchenPassageDefinitionGuid));
        Assert.That(kitchenRow.PassageDefinitionGuid,
            Is.EqualTo(KitchenServiceCorridorPassageDefinitionGuid));
        Assert.That(serviceRow.PassageStableId, Is.EqualTo("passage.service-corridor.kitchen"));
        Assert.That(kitchenRow.PassageStableId, Is.EqualTo("passage.kitchen.service-corridor"));
        Assert.That(serviceRow.SourceRoomViewFileId, Is.EqualTo("4100000009"));
        Assert.That(kitchenRow.SourceRoomViewFileId, Is.EqualTo("4100000010"));
        Assert.That(new[]
            {
                serviceRow.ApproachX,
                serviceRow.ApproachY,
                serviceRow.ArrivalX,
                serviceRow.ArrivalY
            },
            Is.EqualTo(new[] { "589.9897", "-419.25894", "-478.36285", "-156.76599" }));
        Assert.That(new[]
            {
                kitchenRow.ApproachX,
                kitchenRow.ApproachY,
                kitchenRow.ArrivalX,
                kitchenRow.ArrivalY
            },
            Is.EqualTo(new[] { "-478.36285", "-156.76599", "589.9897", "-419.25894" }));

        foreach (RouteInventoryRow row in group)
        {
            string trigger = RequireDocument(documents, row.ComponentFileId);
            AssertStagedRouteSerialization(
                rows,
                row,
                documents,
                database,
                gameRoot,
                trigger,
                GetMigrationStage("complete"));
            Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(row.PassageFileId));
            Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo(NavigationManagerFileId));
            Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo(PlayerTransformFileId));
            Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo(DoorAudioSourceFileId));
            AssertCertifiedAudioCatalogs(trigger, row);
            Assert.That(ReadField(trigger, "maxPlayerScreenDistance"), Is.EqualTo("145"));
        }

        string serviceTrigger = RequireDocument(documents, serviceRow.ComponentFileId);
        string kitchenTrigger = RequireDocument(documents, kitchenRow.ComponentFileId);
        AssertCallerBoundLegacyTriggerSnapshot(
            serviceTrigger,
            "2300000160",
            "2300000163",
            "Service Corridor",
            "ServiceCorridor_Kitchen",
            "Kitchen",
            serviceRow.PassageFileId);
        AssertCallerBoundLegacyTriggerSnapshot(
            kitchenTrigger,
            "802263365",
            "802263368",
            "Kitchen",
            "Kitchen_ServiceCorridor",
            "Service Corridor",
            kitchenRow.PassageFileId);

        string servicePassage = RequireDocument(documents, serviceRow.PassageFileId);
        string kitchenPassage = RequireDocument(documents, kitchenRow.PassageFileId);
        foreach (string passage in new[] { servicePassage, kitchenPassage })
        {
            Assert.That(CountOccurrences(passage, "coordinateSpace: 1"), Is.EqualTo(2));
            Assert.That(CountOccurrences(passage, "logicalPosition: {x: 0, y: 0}"), Is.EqualTo(2));
            Assert.That(CountOccurrences(passage, "roomViewLocalPosition:"), Is.EqualTo(2));
            Assert.That(ReadField(passage, "anchorMigrationStage"), Is.EqualTo("2"));
        }
        Assert.That(documents.Values.Count(document => document.Contains("coordinateSpace: 1")), Is.EqualTo(4),
            "Exactly the two certified RoomView-local reciprocal pairs may serialize the new coordinate mode.");

        string kitchenRoom = RequireDocument(documents, "1541978210");
        string kitchenContent = RequireDocument(documents, "2102000004");
        string kitchenView = RequireDocument(documents, "4100000010");
        Assert.That(kitchenRoom, Does.Contain("m_Name: Room_Kitchen"));
        Assert.That(kitchenRoom, Does.Contain("- component: {fileID: 4100000010}"));
        Assert.That(ReadField(kitchenContent, "roomName"), Is.EqualTo("Kitchen"));
        Assert.That(ReadReferenceFileId(kitchenView, "m_GameObject"), Is.EqualTo("1541978210"));
        Assert.That(ReadReferenceGuid(kitchenView, "definition"), Is.EqualTo(KitchenRoomDefinitionGuid));
        Assert.That(ReadReferenceFileId(kitchenView, "legacyContentGroup"), Is.EqualTo("2102000004"));
        Assert.That(RequireDocument(documents, "618835546"), Does.Contain("m_Name: kitchen_work_table"));
        Assert.That(RequireDocument(documents, "1775169626"),
            Does.Contain("m_Name: PlayerBlocker_kitchen_work_table"));

        string serviceOwner = RequireDocument(documents, "2300000160");
        string kitchenOwner = RequireDocument(documents, "802263365");
        Assert.That(serviceOwner, Does.Contain("- component: {fileID: 4100000027}"));
        Assert.That(kitchenOwner, Does.Contain("- component: {fileID: 4100000028}"));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {RoomViewGuid}")),
            Is.EqualTo(11));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")),
            Is.EqualTo(20));
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000009}\n" +
            "  - {fileID: 4100000010}\n" +
            "  - {fileID: 4100000011}"));
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000026}\n" +
            "  - {fileID: 4100000027}\n" +
            "  - {fileID: 4100000028}"));

        string forwardDefinition = File.ReadAllText(
            "Assets/_Chateau/Data/World/Passages/Passage_ServiceCorridor_Kitchen.asset");
        string reverseDefinition = File.ReadAllText(
            "Assets/_Chateau/Data/World/Passages/Passage_Kitchen_ServiceCorridor.asset");
        Assert.That(ReadField(forwardDefinition, "stableId"),
            Is.EqualTo("passage.service-corridor.kitchen"));
        Assert.That(ReadReferenceGuid(forwardDefinition, "sourceRoom"),
            Is.EqualTo(ServiceCorridorRoomDefinitionGuid));
        Assert.That(ReadReferenceGuid(forwardDefinition, "destinationRoom"),
            Is.EqualTo(KitchenRoomDefinitionGuid));
        Assert.That(ReadReferenceGuid(forwardDefinition, "reverse"),
            Is.EqualTo(KitchenServiceCorridorPassageDefinitionGuid));
        Assert.That(ReadField(reverseDefinition, "stableId"),
            Is.EqualTo("passage.kitchen.service-corridor"));
        Assert.That(ReadReferenceGuid(reverseDefinition, "sourceRoom"),
            Is.EqualTo(KitchenRoomDefinitionGuid));
        Assert.That(ReadReferenceGuid(reverseDefinition, "destinationRoom"),
            Is.EqualTo(ServiceCorridorRoomDefinitionGuid));
        Assert.That(ReadReferenceGuid(reverseDefinition, "reverse"),
            Is.EqualTo(ServiceCorridorKitchenPassageDefinitionGuid));
        Assert.That(CountOccurrences(database, "  - {fileID: 11400000, guid:"), Is.EqualTo(39));
        Assert.That(CountOccurrences(database, $"guid: {KitchenRoomDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database,
            $"guid: {ServiceCorridorKitchenPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database,
            $"guid: {KitchenServiceCorridorPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(legacyDoorData, Does.Contain("ServiceCorridor_Kitchen: Kitchen"));
        Assert.That(legacyDoorData, Does.Contain("Kitchen_ServiceCorridor: Service Corridor"));

        Assert.That(lifecycle, Does.Contain(
            "ServiceCorridorKitchenPassagesUseRoomViewLocalAnchorsAcrossRenderedAspects"));
        Assert.That(lifecycle, Does.Contain("new Vector2(589.9897f, -419.25894f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(-478.36285f, -156.76599f)"));
        Assert.That(lifecycle, Does.Contain("[ServiceKitchenRoomLocal]"));
        Assert.That(lifecycle, Does.Contain("maximumZoom: true"));
    }

    [Test]
    public void ServiceChapelCompleteCertificationUsesRoomViewLocalAnchorsAndPreservesWorldPresentation()
    {
        List<RouteInventoryRow> rows = ReadInventory();
        List<RouteInventoryRow> group = rows.Where(row => row.Order == 9)
            .OrderBy(row => row.ComponentFileId)
            .ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string database = File.ReadAllText(GameDatabasePath);
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string lifecycle = File.ReadAllText(LifecycleCharacterizationPath);
        string legacyDoorData = File.ReadAllText("Assets/Resources/Navigation/doors.txt");

        Assert.That(documents, Has.Count.EqualTo(6038));
        Assert.That(group, Has.Count.EqualTo(2));
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.All(row => row.Group == "Service-Chapel"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "roomview-local-certified"), Is.True);

        RouteInventoryRow serviceRow = group.Single(row => row.ComponentFileId == "2300000169");
        RouteInventoryRow chapelRow = group.Single(row => row.ComponentFileId == "2300000179");
        AssertReciprocal(serviceRow, chapelRow);
        AssertReciprocal(chapelRow, serviceRow);
        Assert.That(serviceRow.PassageFileId, Is.EqualTo("4100000030"));
        Assert.That(chapelRow.PassageFileId, Is.EqualTo("4100000031"));
        Assert.That(serviceRow.PassageDefinitionGuid,
            Is.EqualTo(ServiceCorridorChapelPassageDefinitionGuid));
        Assert.That(chapelRow.PassageDefinitionGuid,
            Is.EqualTo(ChapelServiceCorridorPassageDefinitionGuid));
        Assert.That(serviceRow.PassageStableId, Is.EqualTo("passage.service-corridor.chapel"));
        Assert.That(chapelRow.PassageStableId, Is.EqualTo("passage.chapel.service-corridor"));
        Assert.That(serviceRow.SourceRoomViewFileId, Is.EqualTo("4100000009"));
        Assert.That(chapelRow.SourceRoomViewFileId, Is.EqualTo("4100000029"));
        Assert.That(new[]
            {
                serviceRow.ApproachX,
                serviceRow.ApproachY,
                serviceRow.ArrivalX,
                serviceRow.ArrivalY
            },
            Is.EqualTo(new[] { "-133.2642", "-171.8258", "461.4019", "-190.7613" }));
        Assert.That(new[]
            {
                chapelRow.ApproachX,
                chapelRow.ApproachY,
                chapelRow.ArrivalX,
                chapelRow.ArrivalY
            },
            Is.EqualTo(new[] { "461.4019", "-190.7613", "-133.2642", "-171.8258" }));

        foreach (RouteInventoryRow row in group)
        {
            string trigger = RequireDocument(documents, row.ComponentFileId);
            AssertStagedRouteSerialization(
                rows,
                row,
                documents,
                database,
                gameRoot,
                trigger,
                GetMigrationStage("complete"));
            Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(row.PassageFileId));
            Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo(NavigationManagerFileId));
            Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo(PlayerTransformFileId));
            Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo(DoorAudioSourceFileId));
            AssertCertifiedAudioCatalogs(trigger, row);
            Assert.That(ReadField(trigger, "maxPlayerScreenDistance"), Is.EqualTo("145"));
        }

        string serviceTrigger = RequireDocument(documents, serviceRow.ComponentFileId);
        string chapelTrigger = RequireDocument(documents, chapelRow.ComponentFileId);
        AssertCallerBoundLegacyTriggerSnapshot(
            serviceTrigger,
            "2300000165",
            "2300000168",
            "Service Corridor",
            "ServiceCorridor_Chapel",
            "Chapel",
            serviceRow.PassageFileId);
        AssertCallerBoundLegacyTriggerSnapshot(
            chapelTrigger,
            "2300000175",
            "2300000178",
            "Chapel",
            "Chapel_ServiceCorridor",
            "Service Corridor",
            chapelRow.PassageFileId);

        string servicePassage = RequireDocument(documents, serviceRow.PassageFileId);
        string chapelPassage = RequireDocument(documents, chapelRow.PassageFileId);
        foreach (string passage in new[] { servicePassage, chapelPassage })
        {
            Assert.That(CountOccurrences(passage, "coordinateSpace: 1"), Is.EqualTo(2));
            Assert.That(CountOccurrences(passage, "logicalPosition: {x: 0, y: 0}"), Is.EqualTo(2));
            Assert.That(CountOccurrences(passage, "roomViewLocalPosition:"), Is.EqualTo(2));
            Assert.That(ReadField(passage, "anchorMigrationStage"), Is.EqualTo("2"));
        }
        Assert.That(documents.Values.Count(document => document.Contains("coordinateSpace: 1")), Is.EqualTo(4));

        string chapelRoot = RequireDocument(documents, "2300000030");
        string chapelContent = RequireDocument(documents, "2300000032");
        string chapelDoors = RequireDocument(documents, "2300000033");
        string chapelDoorsTransform = RequireDocument(documents, "2300000034");
        string chapelView = RequireDocument(documents, "4100000029");
        Assert.That(chapelRoot, Does.Contain("m_Name: Room_Chapel"));
        Assert.That(chapelRoot, Does.Contain("- component: {fileID: 4100000029}"));
        Assert.That(ReadField(chapelContent, "roomName"), Is.EqualTo("Chapel"));
        Assert.That(ReadReferenceFileId(chapelView, "m_GameObject"), Is.EqualTo("2300000030"));
        Assert.That(ReadReferenceGuid(chapelView, "definition"), Is.EqualTo(ChapelRoomDefinitionGuid));
        Assert.That(ReadReferenceFileId(chapelView, "legacyContentGroup"), Is.EqualTo("2300000032"));
        Assert.That(chapelDoors, Does.Contain("m_Name: Doors"));
        Assert.That(chapelDoorsTransform, Does.Contain("m_Father: {fileID: 2300000031}"));
        Assert.That(chapelDoorsTransform, Does.Contain("- {fileID: 2300000176}"));
        foreach (string worldOwnerName in new[]
        {
            "chapel_bench_0",
            "chapel_bench_right1_0",
            "chapel_bench_right_2_0",
            "PlayerBlocker_chapel_bench_0",
            "PlayerBlocker_chapel_bench_right1_0",
            "PlayerBlocker_chapel_bench_right_2_0",
            "Ch2_Hide_Guest06"
        })
        {
            Assert.That(documents.Values.Count(document => document.Contains($"m_Name: {worldOwnerName}")),
                Is.EqualTo(1), worldOwnerName);
        }
        Assert.That(RequireDocument(documents, "2107245465"), Does.Contain("m_IsTrigger: 0"));
        Assert.That(RequireDocument(documents, "2107245465"),
            Does.Contain("m_Points:\n    m_Paths:\n    - - {x: 2.045788, y: -0.6854759}"));

        string serviceOwner = RequireDocument(documents, "2300000165");
        string chapelOwner = RequireDocument(documents, "2300000175");
        Assert.That(serviceOwner, Does.Contain("- component: {fileID: 4100000030}"));
        Assert.That(chapelOwner, Does.Contain("- component: {fileID: 4100000031}"));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {RoomViewGuid}")),
            Is.EqualTo(11));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")),
            Is.EqualTo(20));
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000028}\n" +
            "  - {fileID: 4100000029}\n" +
            "  - {fileID: 4100000030}\n" +
            "  - {fileID: 4100000031}"));

        string forwardDefinition = File.ReadAllText(
            "Assets/_Chateau/Data/World/Passages/Passage_ServiceCorridor_Chapel.asset");
        string reverseDefinition = File.ReadAllText(
            "Assets/_Chateau/Data/World/Passages/Passage_Chapel_ServiceCorridor.asset");
        Assert.That(ReadField(forwardDefinition, "stableId"),
            Is.EqualTo("passage.service-corridor.chapel"));
        Assert.That(ReadReferenceGuid(forwardDefinition, "sourceRoom"),
            Is.EqualTo(ServiceCorridorRoomDefinitionGuid));
        Assert.That(ReadReferenceGuid(forwardDefinition, "destinationRoom"),
            Is.EqualTo(ChapelRoomDefinitionGuid));
        Assert.That(ReadReferenceGuid(forwardDefinition, "reverse"),
            Is.EqualTo(ChapelServiceCorridorPassageDefinitionGuid));
        Assert.That(ReadField(reverseDefinition, "stableId"),
            Is.EqualTo("passage.chapel.service-corridor"));
        Assert.That(ReadReferenceGuid(reverseDefinition, "sourceRoom"), Is.EqualTo(ChapelRoomDefinitionGuid));
        Assert.That(ReadReferenceGuid(reverseDefinition, "destinationRoom"),
            Is.EqualTo(ServiceCorridorRoomDefinitionGuid));
        Assert.That(ReadReferenceGuid(reverseDefinition, "reverse"),
            Is.EqualTo(ServiceCorridorChapelPassageDefinitionGuid));
        Assert.That(CountOccurrences(database, "  - {fileID: 11400000, guid:"), Is.EqualTo(39));
        Assert.That(CountOccurrences(database, $"guid: {ChapelRoomDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database,
            $"guid: {ServiceCorridorChapelPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database,
            $"guid: {ChapelServiceCorridorPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(legacyDoorData, Does.Contain("ServiceCorridor_Chapel: Chapel"));
        Assert.That(legacyDoorData, Does.Contain("Chapel_ServiceCorridor: Service Corridor"));

        Assert.That(lifecycle, Does.Contain(
            "ServiceCorridorChapelPassagesUseRoomViewLocalAnchorsAcrossRenderedAspects"));
        Assert.That(lifecycle, Does.Contain("new Vector2(-133.2642f, -171.8258f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(461.4019f, -190.7613f)"));
        Assert.That(lifecycle, Does.Contain("[ServiceChapelAuthoredAspect]"));
        Assert.That(lifecycle, Does.Contain("[ServiceChapelAuthoredMaximumZoom]"));
        Assert.That(lifecycle, Does.Contain("[ServiceChapelRoomLocalAuthored]"));
        Assert.That(lifecycle, Does.Contain(
            "9574b82e5099f96bdaef11dd20bdbab386f5b54baf9ad06278b40af7a58449b2"));
    }

    [Test]
    public void LibraryBallroomCompleteCertificationPreservesAuthoredAnchorsCallersDependenciesAndTopology()
    {
        List<RouteInventoryRow> group = ReadInventory()
            .Where(row => row.Order == 3)
            .OrderBy(row => row.ComponentFileId)
            .ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string database = File.ReadAllText(GameDatabasePath);
        string ballroomOwner = RequireDocument(documents, "2101000021");
        string ballroomTransform = RequireDocument(documents, "2101000022");
        string ballroomTrigger = RequireDocument(documents, "2101000025");
        string libraryOwner = RequireDocument(documents, "2300000080");
        string libraryTransform = RequireDocument(documents, "2300000081");
        string libraryTrigger = RequireDocument(documents, "2300000084");
        string ballroomRoom = RequireDocument(documents, "43637644");
        string ballroomRoomTransform = RequireDocument(documents, "43637645");
        string ballroomContent = RequireDocument(documents, "2102000000");
        string ballroomDoors = RequireDocument(documents, "2103000000");
        string ballroomDoorsTransform = RequireDocument(documents, "2103000001");
        string ballroomView = RequireDocument(documents, "4100000005");
        string libraryRoom = RequireDocument(documents, "1367921344");
        string libraryView = RequireDocument(documents, "4100000004");
        string libraryPassage = RequireDocument(documents, "4100000017");
        string ballroomPassage = RequireDocument(documents, "4100000018");
        string gameRoot = RequireDocument(documents, GameRootFileId);

        Assert.That(documents, Has.Count.EqualTo(6038));
        Assert.That(group, Has.Count.EqualTo(2));
        RouteInventoryRow ballroomRow = group.Single(row => row.ComponentFileId == "2101000025");
        RouteInventoryRow libraryRow = group.Single(row => row.ComponentFileId == "2300000084");
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.All(row => row.Group == "Library-Ballroom"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "template-certified"), Is.True);
        AssertReciprocal(ballroomRow, libraryRow);
        AssertReciprocal(libraryRow, ballroomRow);
        Assert.That(ballroomRow.PassageFileId, Is.EqualTo("4100000018"));
        Assert.That(libraryRow.PassageFileId, Is.EqualTo("4100000017"));
        Assert.That(ballroomRow.PassageDefinitionGuid, Is.EqualTo(BallroomLibraryPassageDefinitionGuid));
        Assert.That(libraryRow.PassageDefinitionGuid, Is.EqualTo(LibraryBallroomPassageDefinitionGuid));
        Assert.That(ballroomRow.PassageStableId, Is.EqualTo("passage.ballroom.library"));
        Assert.That(libraryRow.PassageStableId, Is.EqualTo("passage.library.ballroom"));
        Assert.That(ballroomRow.SourceRoomViewFileId, Is.EqualTo("4100000005"));
        Assert.That(libraryRow.SourceRoomViewFileId, Is.EqualTo("4100000004"));
        Assert.That(ballroomRow.ApproachX, Is.EqualTo("-8.607888"));
        Assert.That(ballroomRow.ApproachY, Is.EqualTo("-2.439877"));
        Assert.That(ballroomRow.ArrivalX, Is.EqualTo("7.95"));
        Assert.That(ballroomRow.ArrivalY, Is.EqualTo("-3"));
        Assert.That(libraryRow.ApproachX, Is.EqualTo("7.95"));
        Assert.That(libraryRow.ApproachY, Is.EqualTo("-3"));
        Assert.That(libraryRow.ArrivalX, Is.EqualTo("-8.607888"));
        Assert.That(libraryRow.ArrivalY, Is.EqualTo("-2.439877"));
        Assert.That(ballroomRow.ApproachX, Is.EqualTo(libraryRow.ArrivalX));
        Assert.That(ballroomRow.ApproachY, Is.EqualTo(libraryRow.ArrivalY));
        Assert.That(ballroomRow.ArrivalX, Is.EqualTo(libraryRow.ApproachX));
        Assert.That(ballroomRow.ArrivalY, Is.EqualTo(libraryRow.ApproachY));
        foreach (RouteInventoryRow row in group)
        {
            AssertFinite(row.ApproachX, $"{row.LegacyDoorId} approach x");
            AssertFinite(row.ApproachY, $"{row.LegacyDoorId} approach y");
            AssertFinite(row.ArrivalX, $"{row.LegacyDoorId} arrival x");
            AssertFinite(row.ArrivalY, $"{row.LegacyDoorId} arrival y");
        }

        string lifecycleCharacterization = File.ReadAllText(LifecycleCharacterizationPath);
        Assert.That(lifecycleCharacterization, Does.Contain(
            "LibraryBallroomAuthoredAnchorPassagesUseInvariantApproachesAndArrivals"));
        Assert.That(lifecycleCharacterization, Does.Contain("new Vector2(7.95f, -3f)"));
        Assert.That(lifecycleCharacterization, Does.Contain("new Vector2(-8.607888f, -2.439877f)"));

        AssertExactComponentIds(
            ballroomOwner, "2101000022", "2101000023", "2101000024", "2101000025", "4100000018");
        Assert.That(ballroomOwner, Does.Contain("m_Name: DoorTrigger_Ballroom_Library"));
        Assert.That(ReadField(ballroomOwner, "m_Layer"), Is.EqualTo("5"));
        Assert.That(ReadField(ballroomOwner, "m_IsActive"), Is.EqualTo("1"));
        Assert.That(ReadReferenceFileId(ballroomTransform, "m_Father"), Is.EqualTo("2103000001"));
        Assert.That(ReadField(ballroomTransform, "m_LocalScale"), Is.EqualTo("{x: 1, y: 1, z: 1}"));
        Assert.That(ReadField(ballroomTransform, "m_AnchoredPosition"), Is.EqualTo("{x: -724.69, y: 34.2}"));
        Assert.That(ReadField(ballroomTransform, "m_SizeDelta"), Is.EqualTo("{x: 195.35, y: 359.59}"));
        AssertCertifiedLegacyTriggerSnapshot(
            ballroomTrigger,
            "2101000021",
            "2101000024",
            "Ballroom",
            "Ballroom_Library",
            "Library",
            "4100000018",
            "44d6784f30c28c9dee4b209eefe8ac97340d9de28d9e3ad41d7fdd72191be856",
            "33f73a31f657f0ddef49736456e7e248c5364ad2b322313d85a4ed617e15c0f3",
            "fde492fc20476d3821b7cd81bea9880e1ce7480242f18845f8c2cc273612d1fe");

        AssertExactComponentIds(
            libraryOwner, "2300000081", "2300000082", "2300000083", "2300000084", "4100000017");
        Assert.That(libraryOwner, Does.Contain("m_Name: DoorTrigger_Library_Ballroom"));
        Assert.That(ReadField(libraryOwner, "m_Layer"), Is.EqualTo("5"));
        Assert.That(ReadField(libraryOwner, "m_IsActive"), Is.EqualTo("1"));
        Assert.That(ReadReferenceFileId(libraryTransform, "m_Father"), Is.EqualTo("2103000031"));
        Assert.That(ReadField(libraryTransform, "m_LocalScale"), Is.EqualTo("{x: 3.299394, y: 2.6126, z: 1}"));
        Assert.That(ReadField(libraryTransform, "m_AnchoredPosition"), Is.EqualTo("{x: 669, y: 21.3902}"));
        Assert.That(ReadField(libraryTransform, "m_SizeDelta"), Is.EqualTo("{x: 51.8593, y: 142.1376}"));
        AssertCertifiedLegacyTriggerSnapshot(
            libraryTrigger,
            "2300000080",
            "2300000083",
            "Library",
            "Library_Ballroom",
            "Ballroom",
            "4100000017",
            "77171a4b3e59c988977011f3ab01f2e74fe2cae3dd400046cd0b9803f7cad3f6",
            "b81c9d24579a255d338339c61e120485319c0f72a696c2f9090e22e5a53eb939",
            "3088f9b3e29eec2df79c21670c6d19301b2f5dca4de990bd1b180c3d47f51f41");

        AssertExactComponentIds(ballroomRoom, "43637645", "2102000000", "4100000005");
        Assert.That(ballroomRoom, Does.Contain("m_Name: Room_Ballroom"));
        Assert.That(ReadField(ballroomRoom, "m_IsActive"), Is.EqualTo("0"));
        Assert.That(ReadReferenceFileId(ballroomRoomTransform, "m_Father"), Is.EqualTo("668915133"));
        Assert.That(ReadField(ballroomContent, "roomName"), Is.EqualTo("Ballroom"));
        Assert.That(ballroomContent, Does.Contain(
            "roomBackgroundTexture: {fileID: 2800000, guid: 7dabdfc97f536fe458e28ca413b0a0fa, type: 3}"));
        Assert.That(ReadReferenceFileId(ballroomContent, "perspectiveProfile"), Is.EqualTo("0"));
        Assert.That(ballroomDoors, Does.Contain("m_Name: Doors"));
        AssertExactComponentIds(ballroomDoors, "2103000001");
        Assert.That(ReadReferenceFileId(ballroomDoorsTransform, "m_Father"), Is.EqualTo("43637645"));
        Assert.That(ReadReferenceFileId(ballroomView, "m_GameObject"), Is.EqualTo("43637644"));
        Assert.That(ReadReferenceGuid(ballroomView, "definition"), Is.EqualTo(BallroomRoomDefinitionGuid));
        Assert.That(ReadReferenceFileId(ballroomView, "legacyContentGroup"), Is.EqualTo("2102000000"));
        AssertExactComponentIds(libraryRoom, "1367921345", "2102000003", "4100000004");
        Assert.That(ReadReferenceGuid(libraryView, "definition"), Is.EqualTo(LibraryRoomDefinitionGuid));

        AssertPassivePassageSnapshot(
            libraryPassage,
            "2300000080",
            LibraryBallroomPassageDefinitionGuid,
            "4100000004",
            "4100000018",
            "{x: 7.95, y: -3}",
            "{x: -8.607888, y: -2.439877}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageSnapshot(
            ballroomPassage,
            "2101000021",
            BallroomLibraryPassageDefinitionGuid,
            "4100000005",
            "4100000017",
            "{x: -8.607888, y: -2.439877}",
            "{x: 7.95, y: -3}",
            PassageAnchorMigrationStage.AuthoredAnchors);

        Assert.That(documents.Values.Count(document => document.Contains($"guid: {RoomViewGuid}")),
            Is.EqualTo(11));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")),
            Is.EqualTo(20));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000005}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000017}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000018}"), Is.EqualTo(1));
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000003}\n" +
            "  - {fileID: 4100000004}\n" +
            "  - {fileID: 4100000005}\n" +
            "  - {fileID: 4100000006}\n" +
            "  - {fileID: 4100000007}\n" +
            "  - {fileID: 4100000008}\n" +
            "  - {fileID: 4100000009}\n" +
            "  - {fileID: 4100000010}\n" +
            "  - {fileID: 4100000011}"));
        Assert.That(gameRoot, Does.Contain(
            "  - {fileID: 4100000015}\n" +
            "  - {fileID: 4100000016}\n" +
            "  - {fileID: 4100000017}\n" +
            "  - {fileID: 4100000018}"));
        Assert.That(CountOccurrences(database, "  - {fileID: 11400000, guid:"), Is.EqualTo(39));
        Assert.That(CountOccurrences(database, $"guid: {BallroomRoomDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {LibraryBallroomPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {BallroomLibraryPassageDefinitionGuid}"), Is.EqualTo(1));
    }

    [Test]
    public void GrandEntranceDiningCompleteCertificationPreservesAuthoredAnchorsCallersDependenciesAndTopology()
    {
        List<RouteInventoryRow> rows = ReadInventory();
        List<RouteInventoryRow> group = rows.Where(row => row.Order == 4).OrderBy(row => row.ComponentFileId).ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string database = File.ReadAllText(GameDatabasePath);
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string lifecycle = File.ReadAllText(LifecycleCharacterizationPath);

        Assert.That(documents, Has.Count.EqualTo(6038));
        Assert.That(group, Has.Count.EqualTo(2));
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.All(row => row.Group == "GEH-Dining"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "template-certified"), Is.True);

        RouteInventoryRow diningRow = group.Single(row => row.ComponentFileId == "2300000109");
        RouteInventoryRow entranceRow = group.Single(row => row.ComponentFileId == "340611600");
        AssertReciprocal(diningRow, entranceRow);
        AssertReciprocal(entranceRow, diningRow);
        Assert.That(diningRow.PassageFileId, Is.EqualTo("4100000020"));
        Assert.That(entranceRow.PassageFileId, Is.EqualTo("4100000019"));
        Assert.That(diningRow.PassageDefinitionGuid, Is.EqualTo(DiningEntrancePassageDefinitionGuid));
        Assert.That(entranceRow.PassageDefinitionGuid, Is.EqualTo(EntranceDiningPassageDefinitionGuid));
        Assert.That(diningRow.PassageStableId, Is.EqualTo("passage.dining-room.grand-entrance-hall"));
        Assert.That(entranceRow.PassageStableId, Is.EqualTo("passage.grand-entrance-hall.dining-room"));
        Assert.That(diningRow.SourceRoomViewFileId, Is.EqualTo("4100000006"));
        Assert.That(entranceRow.SourceRoomViewFileId, Is.EqualTo("4100000001"));
        Assert.That(diningRow.ApproachX, Is.EqualTo("-7.192237"));
        Assert.That(diningRow.ApproachY, Is.EqualTo("-1.740209"));
        Assert.That(diningRow.ArrivalX, Is.EqualTo("8.705841"));
        Assert.That(diningRow.ArrivalY, Is.EqualTo("-2.346406"));
        Assert.That(entranceRow.ApproachX, Is.EqualTo("8.705841"));
        Assert.That(entranceRow.ApproachY, Is.EqualTo("-2.346406"));
        Assert.That(entranceRow.ArrivalX, Is.EqualTo("-7.192237"));
        Assert.That(entranceRow.ArrivalY, Is.EqualTo("-1.740209"));
        Assert.That(diningRow.ApproachX, Is.EqualTo(entranceRow.ArrivalX));
        Assert.That(diningRow.ApproachY, Is.EqualTo(entranceRow.ArrivalY));
        Assert.That(diningRow.ArrivalX, Is.EqualTo(entranceRow.ApproachX));
        Assert.That(diningRow.ArrivalY, Is.EqualTo(entranceRow.ApproachY));

        foreach (RouteInventoryRow row in group)
        {
            string trigger = RequireDocument(documents, row.ComponentFileId);
            AssertStagedRouteSerialization(rows, row, documents, database, gameRoot, trigger,
                GetMigrationStage("complete"));
            Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(row.PassageFileId));
            AssertFinite(row.ApproachX, $"{row.LegacyDoorId} approach x");
            AssertFinite(row.ApproachY, $"{row.LegacyDoorId} approach y");
            AssertFinite(row.ArrivalX, $"{row.LegacyDoorId} arrival x");
            AssertFinite(row.ArrivalY, $"{row.LegacyDoorId} arrival y");
        }

        string entranceTrigger = RequireDocument(documents, entranceRow.ComponentFileId);
        string diningTrigger = RequireDocument(documents, diningRow.ComponentFileId);
        AssertCertifiedLegacyTriggerSnapshot(
            entranceTrigger,
            "340611598",
            "340611601",
            "Grand Entrance Hall",
            "GEH_DiningRoom",
            "Dining Room",
            "4100000019",
            "a7bf770a0a094efe465804ed093275018e41c75c47de069b41b68538488ec278",
            "413c5747ac82caae76aca194f6050c38d5649f88eb3696ec2efa6ee58f303c2b",
            "e187dbdf74e03d61734c05a8c8d40ff407df6e88f42b93ecc587c10ad620688d");
        AssertCertifiedLegacyTriggerSnapshot(
            diningTrigger,
            "2300000105",
            "2300000108",
            "Dining Room",
            "DiningRoom_GEH",
            "Grand Entrance Hall",
            "4100000020",
            "7f956609f887f6c68bbf9cfb7a09909a8449e62ba7b3ef6ef7a38b57dba04ea8",
            "d175220b4a626d8f62da1738509bd3659f189f1a9fda45d56d11d6044b0a2903",
            "3ed6867c72e1db947a85a7176c20812fd5849633367beeacc219c984db85497a");

        string entrancePassage = RequireDocument(documents, "4100000019");
        string diningPassage = RequireDocument(documents, "4100000020");
        AssertPassivePassageSnapshot(
            entrancePassage,
            "340611598",
            EntranceDiningPassageDefinitionGuid,
            "4100000001",
            "4100000020",
            "{x: 8.705841, y: -2.346406}",
            "{x: -7.192237, y: -1.740209}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageSnapshot(
            diningPassage,
            "2300000105",
            DiningEntrancePassageDefinitionGuid,
            "4100000006",
            "4100000019",
            "{x: -7.192237, y: -1.740209}",
            "{x: 8.705841, y: -2.346406}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        Assert.That(ReadAnchorMigrationStage(entrancePassage, "4100000019"),
            Is.EqualTo(ReadAnchorMigrationStage(diningPassage, "4100000020")),
            "The reciprocal Entrance/Dining route pair must cut anchor ownership over together.");
        Assert.That(ReadReferenceGuid(RequireDocument(documents, "4100000006"), "definition"),
            Is.EqualTo(DiningRoomDefinitionGuid));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {RoomViewGuid}")),
            Is.EqualTo(11));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")),
            Is.EqualTo(20));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000006}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000019}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000020}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {DiningRoomDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {EntranceDiningPassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {DiningEntrancePassageDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(lifecycle, Does.Contain(
            "GrandEntranceDiningAuthoredAnchorPassagesUseInvariantApproachesAndArrivals"));
        Assert.That(lifecycle, Does.Contain("new Vector2(8.705841f, -2.346406f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(-7.192237f, -1.740209f)"));
    }

    [Test]
    public void DiningButlersPantryCompleteCertificationPreservesAuthoredAnchorsCallersDependenciesAndTopology()
    {
        List<RouteInventoryRow> rows = ReadInventory();
        List<RouteInventoryRow> group = rows.Where(row => row.Order == 5)
            .OrderBy(row => row.ComponentFileId)
            .ToList();
        Dictionary<string, string> documents = ReadUnityDocuments(File.ReadAllText(GameplayScenePath));
        string database = File.ReadAllText(GameDatabasePath);
        string gameRoot = RequireDocument(documents, GameRootFileId);
        string lifecycle = File.ReadAllText(LifecycleCharacterizationPath);

        Assert.That(documents, Has.Count.EqualTo(6038));
        Assert.That(group, Has.Count.EqualTo(2));
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.All(row => row.Group == "Dining-Butlers"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "template-certified"), Is.True);

        RouteInventoryRow diningRow = group.Single(row => row.ComponentFileId == "2300000119");
        RouteInventoryRow pantryRow = group.Single(row => row.ComponentFileId == "2300000139");
        AssertReciprocal(diningRow, pantryRow);
        AssertReciprocal(pantryRow, diningRow);
        Assert.That(diningRow.PassageFileId, Is.EqualTo("4100000021"));
        Assert.That(pantryRow.PassageFileId, Is.EqualTo("4100000022"));
        Assert.That(diningRow.PassageDefinitionGuid,
            Is.EqualTo(DiningButlersPantryPassageDefinitionGuid));
        Assert.That(pantryRow.PassageDefinitionGuid,
            Is.EqualTo(ButlersPantryDiningPassageDefinitionGuid));
        Assert.That(diningRow.PassageStableId, Is.EqualTo("passage.dining-room.butlers-pantry"));
        Assert.That(pantryRow.PassageStableId, Is.EqualTo("passage.butlers-pantry.dining-room"));
        Assert.That(diningRow.SourceRoomViewFileId, Is.EqualTo("4100000006"));
        Assert.That(pantryRow.SourceRoomViewFileId, Is.EqualTo("4100000007"));
        Assert.That(diningRow.ApproachX, Is.EqualTo("3.391918"));
        Assert.That(diningRow.ApproachY, Is.EqualTo("-0.36"));
        Assert.That(diningRow.ArrivalX, Is.EqualTo("-5.163103"));
        Assert.That(diningRow.ArrivalY, Is.EqualTo("-3.463186"));
        Assert.That(pantryRow.ApproachX, Is.EqualTo("-5.163103"));
        Assert.That(pantryRow.ApproachY, Is.EqualTo("-3.463186"));
        Assert.That(pantryRow.ArrivalX, Is.EqualTo("3.391918"));
        Assert.That(pantryRow.ArrivalY, Is.EqualTo("-0.36"));
        Assert.That(diningRow.ApproachX, Is.EqualTo(pantryRow.ArrivalX));
        Assert.That(diningRow.ApproachY, Is.EqualTo(pantryRow.ArrivalY));
        Assert.That(diningRow.ArrivalX, Is.EqualTo(pantryRow.ApproachX));
        Assert.That(diningRow.ArrivalY, Is.EqualTo(pantryRow.ApproachY));

        foreach (RouteInventoryRow row in group)
        {
            string trigger = RequireDocument(documents, row.ComponentFileId);
            AssertStagedRouteSerialization(rows, row, documents, database, gameRoot, trigger,
                GetMigrationStage("complete"));
            Assert.That(ReadReferenceFileId(trigger, "canonicalPassage"), Is.EqualTo(row.PassageFileId));
            AssertFinite(row.ApproachX, $"{row.LegacyDoorId} approach x");
            AssertFinite(row.ApproachY, $"{row.LegacyDoorId} approach y");
            AssertFinite(row.ArrivalX, $"{row.LegacyDoorId} arrival x");
            AssertFinite(row.ArrivalY, $"{row.LegacyDoorId} arrival y");
        }

        string diningTrigger = RequireDocument(documents, "2300000119");
        string pantryTrigger = RequireDocument(documents, "2300000139");
        AssertCertifiedLegacyTriggerSnapshot(
            diningTrigger,
            "2300000115",
            "2300000118",
            "Dining Room",
            "DiningRoom_ButlersPantry",
            "Butlers Pantry",
            "4100000021",
            "be4e1c81a14b14b7afe4aeae52695043c76aa89ad2be7096b8366288fe19ce6f",
            "55b31370949511e5a0e0a97a8344fb4331d06f384f7b1c7fbbb73a12070e60be",
            "c4c98e2c8ca7a1a126ba100ec358d50c3328460a94cf06fee6a173d43683c481");
        AssertCertifiedLegacyTriggerSnapshot(
            pantryTrigger,
            "2300000135",
            "2300000138",
            "Butlers Pantry",
            "ButlersPantry_DiningRoom",
            "Dining Room",
            "4100000022",
            "bd5173707fae47832bdd2f8fbead4bf767d9ca17e3ead391f8910389d9f3440e",
            "ac4123fe0c998c0b14d1dee84c23e9e94f4d5a8040cf9ba8b67eab1848eba2b3",
            "d916f02cdf6f16b370c0a76319331965b9daeaaec92a5ab139d44858bcba3555");

        string diningPassage = RequireDocument(documents, "4100000021");
        string pantryPassage = RequireDocument(documents, "4100000022");
        AssertPassivePassageSnapshot(
            diningPassage,
            "2300000115",
            DiningButlersPantryPassageDefinitionGuid,
            "4100000006",
            "4100000022",
            "{x: 3.391918, y: -0.36}",
            "{x: -5.163103, y: -3.463186}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        AssertPassivePassageSnapshot(
            pantryPassage,
            "2300000135",
            ButlersPantryDiningPassageDefinitionGuid,
            "4100000007",
            "4100000021",
            "{x: -5.163103, y: -3.463186}",
            "{x: 3.391918, y: -0.36}",
            PassageAnchorMigrationStage.AuthoredAnchors);
        Assert.That(ReadAnchorMigrationStage(diningPassage, "4100000021"),
            Is.EqualTo(ReadAnchorMigrationStage(pantryPassage, "4100000022")),
            "The reciprocal Dining/Butlers Pantry route pair must cut anchor ownership over together.");
        Assert.That(ReadReferenceGuid(RequireDocument(documents, "4100000007"), "definition"),
            Is.EqualTo(ButlersPantryRoomDefinitionGuid));
        Assert.That(CountOccurrences(database, $"guid: {ButlersPantryRoomDefinitionGuid}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {DiningButlersPantryPassageDefinitionGuid}"),
            Is.EqualTo(1));
        Assert.That(CountOccurrences(database, $"guid: {ButlersPantryDiningPassageDefinitionGuid}"),
            Is.EqualTo(1));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {RoomViewGuid}")),
            Is.EqualTo(11));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")),
            Is.EqualTo(20));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000007}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000021}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000022}"), Is.EqualTo(1));
        Assert.That(lifecycle, Does.Contain(
            "DiningButlersPantryAuthoredAnchorPassagesUseInvariantApproachesAndArrivals"));
        Assert.That(lifecycle, Does.Contain("[DiningButlersStageOneArrivalProof]"),
            "The final lifecycle must retain the exact tests-only arrival-ownership preflight.");
        Assert.That(lifecycle, Does.Contain("[DiningButlersAuthoredPrimary]"));
        Assert.That(lifecycle, Does.Contain("[DiningButlersAuthoredAspect]"));
        Assert.That(lifecycle, Does.Contain("[DiningButlersAuthoredMaximumZoom]"));
        Assert.That(lifecycle, Does.Contain("[DiningButlersAuthoredProfile]"));
        Assert.That(lifecycle, Does.Contain(
            "float[] expectedDiningDistances = { 23.943f, 38.849f, 33.662f, 38.87f, 44.737f };"),
            "The approach-ownership preflight must lock all rendered Dining distances, including maximum zoom.");
        Assert.That(lifecycle, Does.Contain(
            "float[] expectedPantryDistances = { 23.943f, 83.785f, 33.733f, 27.022f, 31.101f };"),
            "The approach-ownership preflight must lock all rendered Pantry distances, including maximum zoom.");
        Assert.That(lifecycle, Does.Contain(
            "78267865829752279aaa796771f8d51c92b0fac44dc2fe93a4dd12885ccf2d7e"),
            "The final no-trailing-newline seven-line authored observation hash must remain explicit.");
        Assert.That(lifecycle, Does.Contain("new Vector2(3.391918f, -0.36f)"));
        Assert.That(lifecycle, Does.Contain("new Vector2(-5.163103f, -3.463186f)"));
    }

    [Test]
    public void MusicLibraryCompleteCertificationPreservesAuthoredAnchorsCallersDependenciesAndTopology()
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

        Assert.That(documents, Has.Count.EqualTo(6038));
        Assert.That(group, Has.Count.EqualTo(2));
        RouteInventoryRow libraryRow = group.Single(row => row.ComponentFileId == "2300000079");
        RouteInventoryRow musicRow = group.Single(row => row.ComponentFileId == "552135204");
        Assert.That(group.All(row => row.Status == "complete"), Is.True);
        Assert.That(group.All(row => row.Group == "Music-Library"), Is.True);
        Assert.That(group.All(row => row.Profile == "standard-door"), Is.True);
        Assert.That(group.All(row => row.Notes == "template-certified"), Is.True);
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
        Assert.That(libraryRow.ApproachX, Is.EqualTo("-7.744175"));
        Assert.That(libraryRow.ApproachY, Is.EqualTo("-3.059095"));
        Assert.That(libraryRow.ArrivalX, Is.EqualTo("7.714471"));
        Assert.That(libraryRow.ArrivalY, Is.EqualTo("-3.121709"));
        Assert.That(musicRow.ApproachX, Is.EqualTo("7.714471"));
        Assert.That(musicRow.ApproachY, Is.EqualTo("-3.121709"));
        Assert.That(musicRow.ArrivalX, Is.EqualTo("-7.744175"));
        Assert.That(musicRow.ArrivalY, Is.EqualTo("-3.059095"));
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
        string lifecycleCharacterization = File.ReadAllText(LifecycleCharacterizationPath);
        Assert.That(lifecycleCharacterization, Does.Contain(
            "new Vector2(7.714471f, -3.121709f)"),
            "The lifecycle gate must consume the calibrated Music Room anchor.");
        Assert.That(lifecycleCharacterization, Does.Contain(
            "new Vector2(-7.744175f, -3.059095f)"),
            "The lifecycle gate must consume the calibrated Library anchor.");
        Assert.That(lifecycleCharacterization, Does.Contain(
            "new Vector2(-7.287828f, -2.936489f)"),
            "The source-sensitive legacy far Library result must remain fallback evidence.");
        Assert.That(lifecycleCharacterization, Does.Contain(
            "new Vector2(-7.244175f, -2.799095f)"),
            "The legacy near Library result must remain fallback evidence.");
        Assert.That(lifecycleCharacterization, Does.Contain(
            "PassageAnchorMigrationStage.AuthoredAnchors"),
            "The lifecycle gate must exercise final stage-2 ownership.");

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
        AssertMusicLibraryCertifiedLegacyTriggerSnapshot(
            musicTrigger,
            "552135202",
            "552135205",
            "Music Room",
            "MusicRoom_Library",
            "Library",
            "4100000015",
            "145");

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
        AssertMusicLibraryCertifiedLegacyTriggerSnapshot(
            libraryTrigger,
            "2300000075",
            "2300000078",
            "Library",
            "Library_MusicRoom",
            "Music Room",
            "4100000016",
            "149");
        string musicDependencyBoundTrigger = AssertRevertsToDependenciesBoundTriggerHash(
            musicTrigger,
            "4100000015",
            "f8cb20d42f85dd56e7c21a60b8853dc51d9e22c89d6ee3c253dadcf3f69444b0");
        string libraryDependencyBoundTrigger = AssertRevertsToDependenciesBoundTriggerHash(
            libraryTrigger,
            "4100000016",
            "eb76e665c11392dc7e506cc869680822cf4a70c014b338fb00d47fdb02d7ad92");
        AssertRevertsToPassageBoundTriggerHash(
            musicDependencyBoundTrigger,
            "e1706c94957de2d852784a32de5f8a4a20c0d6fecc5b8dbb4232149de3a6d86b");
        AssertRevertsToPassageBoundTriggerHash(
            libraryDependencyBoundTrigger,
            "62855ffb92390f74e49fc64f2ec6367451ecaf91ccc4b3a7a98cd5cc88465ede");

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
            "approachAnchor:\n    logicalPosition: {x: 7.714471, y: -3.121709}"));
        Assert.That(musicPassage, Does.Contain(
            "arrivalAnchor:\n    logicalPosition: {x: -7.744175, y: -3.059095}"));
        Assert.That(ReadAnchorMigrationStage(musicPassage, musicRow.PassageFileId),
            Is.EqualTo(PassageAnchorMigrationStage.AuthoredAnchors));

        Assert.That(libraryPassage.TrimEnd('\r', '\n').Split('\n'), Has.Length.EqualTo(20));
        Assert.That(libraryPassage, Does.Contain($"guid: {PassageGuid}"));
        Assert.That(ReadReferenceFileId(libraryPassage, "m_GameObject"), Is.EqualTo("2300000075"));
        Assert.That(ReadReferenceGuid(libraryPassage, "definition"),
            Is.EqualTo(LibraryMusicPassageDefinitionGuid));
        Assert.That(ReadReferenceFileId(libraryPassage, "sourceRoomView"), Is.EqualTo("4100000004"));
        Assert.That(ReadReferenceFileId(libraryPassage, "reversePassage"), Is.EqualTo("4100000015"));
        Assert.That(libraryPassage, Does.Contain(
            "approachAnchor:\n    logicalPosition: {x: -7.744175, y: -3.059095}"));
        Assert.That(libraryPassage, Does.Contain(
            "arrivalAnchor:\n    logicalPosition: {x: 7.714471, y: -3.121709}"));
        Assert.That(ReadAnchorMigrationStage(libraryPassage, libraryRow.PassageFileId),
            Is.EqualTo(PassageAnchorMigrationStage.AuthoredAnchors));
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
            Is.EqualTo(11));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")),
            Is.EqualTo(20));
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

        Assert.That(CountOccurrences(database, "  - {fileID: 11400000, guid:"), Is.EqualTo(39));
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
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {RoomViewGuid}")), Is.EqualTo(11));
        Assert.That(documents.Values.Count(document => document.Contains($"guid: {PassageGuid}")), Is.EqualTo(20));
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

    private static void AssertDependenciesBoundTriggerSnapshot(
        string trigger,
        string gameObjectFileId,
        string imageFileId,
        string sourceRoom,
        string legacyDoorId,
        string destinationRoom,
        string expectedDependenciesBoundSha256,
        string expectedNullRestoredSha256)
    {
        Assert.That(trigger.TrimEnd('\r', '\n').Split('\n'), Has.Length.EqualTo(40));
        Assert.That(ComputeSha256(trigger), Is.EqualTo(expectedDependenciesBoundSha256));
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
        Assert.That(trigger, Does.Not.Contain("canonicalPassage:"));
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
        AssertRevertsToPassageBoundTriggerHash(trigger, expectedNullRestoredSha256);
    }

    private static void AssertForegroundCutoutSnapshot(
        Dictionary<string, string> documents,
        string gameObjectFileId,
        string transformFileId,
        string rendererFileId,
        string objectName,
        string localPosition,
        string localScale,
        string spriteGuid,
        string disabledBoxColliderFileId = null,
        string colliderOffset = null,
        string colliderSize = null)
    {
        string gameObject = RequireDocument(documents, gameObjectFileId);
        string transform = RequireDocument(documents, transformFileId);
        string renderer = RequireDocument(documents, rendererFileId);

        if (string.IsNullOrEmpty(disabledBoxColliderFileId))
        {
            AssertExactComponentIds(gameObject, transformFileId, rendererFileId);
        }
        else
        {
            AssertExactComponentIds(gameObject, transformFileId, rendererFileId, disabledBoxColliderFileId);
        }
        Assert.That(gameObject, Does.Contain($"m_Name: {objectName}"));
        Assert.That(ReadReferenceFileId(transform, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadReferenceFileId(transform, "m_Father"), Is.EqualTo("2300000011"));
        Assert.That(ReadField(transform, "m_LocalPosition"), Is.EqualTo(localPosition));
        Assert.That(ReadField(transform, "m_LocalScale"), Is.EqualTo(localScale));
        Assert.That(ReadReferenceFileId(renderer, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadField(renderer, "m_Enabled"), Is.EqualTo("1"));
        Assert.That(ReadField(renderer, "m_SortingLayer"), Is.EqualTo("2"));
        Assert.That(ReadField(renderer, "m_SortingOrder"), Is.EqualTo("1000"));
        Assert.That(ReadReferenceGuid(renderer, "m_Sprite"), Is.EqualTo(spriteGuid));
        Assert.That(ReadField(renderer, "m_SpriteSortPoint"), Is.EqualTo("1"));

        if (!string.IsNullOrEmpty(disabledBoxColliderFileId))
        {
            string collider = RequireDocument(documents, disabledBoxColliderFileId);
            Assert.That(collider, Does.StartWith($"--- !u!61 &{disabledBoxColliderFileId}\nBoxCollider2D:"));
            Assert.That(ReadReferenceFileId(collider, "m_GameObject"), Is.EqualTo(gameObjectFileId));
            Assert.That(ReadField(collider, "m_Enabled"), Is.EqualTo("0"),
                "The source sprite's broad authored BoxCollider2D must stay disabled; the exact blocker owns collision.");
            Assert.That(ReadField(collider, "m_IsTrigger"), Is.EqualTo("0"));
            Assert.That(ReadField(collider, "m_Offset"), Is.EqualTo(colliderOffset));
            Assert.That(ReadField(collider, "m_Size"), Is.EqualTo(colliderSize));
        }
    }

    private static void AssertCertifiedLegacyTriggerSnapshot(
        string trigger,
        string gameObjectFileId,
        string imageFileId,
        string sourceRoom,
        string legacyDoorId,
        string destinationRoom,
        string canonicalPassageFileId,
        string expectedCallerBoundSha256,
        string expectedDependencyBoundSha256,
        string expectedPassageBoundSha256)
    {
        Assert.That(ComputeSha256(trigger), Is.EqualTo(expectedCallerBoundSha256));
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
        string dependencyBoundTrigger = AssertRevertsToDependenciesBoundTriggerHash(
            trigger,
            canonicalPassageFileId,
            expectedDependencyBoundSha256);
        AssertRevertsToPassageBoundTriggerHash(dependencyBoundTrigger, expectedPassageBoundSha256);
    }

    private static void AssertPassivePassageSnapshot(
        string passage,
        string gameObjectFileId,
        string definitionGuid,
        string sourceRoomViewFileId,
        string reversePassageFileId,
        string approachPosition,
        string arrivalPosition,
        PassageAnchorMigrationStage expectedStage)
    {
        Assert.That(passage.TrimEnd('\r', '\n').Split('\n'), Has.Length.EqualTo(20));
        Assert.That(passage, Does.Contain($"guid: {PassageGuid}"));
        Assert.That(ReadReferenceFileId(passage, "m_GameObject"), Is.EqualTo(gameObjectFileId));
        Assert.That(ReadReferenceGuid(passage, "definition"), Is.EqualTo(definitionGuid));
        Assert.That(ReadReferenceFileId(passage, "sourceRoomView"), Is.EqualTo(sourceRoomViewFileId));
        Assert.That(ReadReferenceFileId(passage, "reversePassage"), Is.EqualTo(reversePassageFileId));
        Assert.That(passage, Does.Contain($"approachAnchor:\n    logicalPosition: {approachPosition}"));
        Assert.That(passage, Does.Contain($"arrivalAnchor:\n    logicalPosition: {arrivalPosition}"));
        Assert.That(ReadAnchorMigrationStage(passage, reversePassageFileId), Is.EqualTo(expectedStage));
    }

    private static void AssertMusicLibraryCertifiedLegacyTriggerSnapshot(
        string trigger,
        string gameObjectFileId,
        string imageFileId,
        string sourceRoom,
        string legacyDoorId,
        string destinationRoom,
        string canonicalPassageFileId,
        string expectedMaxPlayerScreenDistance)
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
        Assert.That(ReadField(trigger, "maxPlayerScreenDistance"),
            Is.EqualTo(expectedMaxPlayerScreenDistance));
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
        if (row.Notes == "roomview-local-certified")
        {
            Assert.That(passage, Does.Contain(
                "approachAnchor:\n" +
                "    coordinateSpace: 1\n" +
                "    logicalPosition: {x: 0, y: 0}\n" +
                $"    roomViewLocalPosition: {{x: {row.ApproachX}, y: {row.ApproachY}}}"));
            Assert.That(passage, Does.Contain(
                "arrivalAnchor:\n" +
                "    coordinateSpace: 1\n" +
                "    logicalPosition: {x: 0, y: 0}\n" +
                $"    roomViewLocalPosition: {{x: {row.ArrivalX}, y: {row.ArrivalY}}}"));
        }
        else
        {
            Assert.That(row.Notes, Is.EqualTo("template-certified"),
                $"Passage-bound inventory row {row.LegacyDoorId} has no recognized anchor schema.");
            Assert.That(passage, Does.Not.Contain("coordinateSpace:"),
                $"Legacy logical Passage {row.PassageFileId} must retain its compact compatibility serialization.");
            Assert.That(passage, Does.Not.Contain("roomViewLocalPosition:"),
                $"Legacy logical Passage {row.PassageFileId} must not silently change coordinate ownership.");
            Assert.That(passage, Does.Contain(
                $"approachAnchor:\n    logicalPosition: {{x: {row.ApproachX}, y: {row.ApproachY}}}"));
            Assert.That(passage, Does.Contain(
                $"arrivalAnchor:\n    logicalPosition: {{x: {row.ArrivalX}, y: {row.ArrivalY}}}"));
        }
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
