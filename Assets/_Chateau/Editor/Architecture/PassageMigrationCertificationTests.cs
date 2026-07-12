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

        Assert.That(certified, Is.Not.Empty);

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
    public void DrawingMusicDataIsAuthoredWithoutSceneBindingsOrCallerChanges()
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
        string guestPanic = RequireDocument(documents, "3301000008");

        Assert.That(group, Has.Count.EqualTo(2));
        Assert.That(group.All(row => row.Status == "data-authored"), Is.True);
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
        Assert.That(group.All(row => string.IsNullOrEmpty(row.SourceRoomViewFileId)), Is.True);
        Assert.That(group.All(row => string.IsNullOrEmpty(row.PassageFileId)), Is.True);
        Assert.That(group.All(row =>
            string.IsNullOrEmpty(row.ApproachX) &&
            string.IsNullOrEmpty(row.ApproachY) &&
            string.IsNullOrEmpty(row.ArrivalX) &&
            string.IsNullOrEmpty(row.ArrivalY)), Is.True);

        Assert.That(drawingOwner, Does.Contain("m_Name: DoorTrigger_DrawingRoom_MusicRoom"));
        Assert.That(drawingOwner, Does.Contain("- component: {fileID: 2300000096}"));
        Assert.That(drawingOwner, Does.Contain("- component: {fileID: 2300000099}"));
        Assert.That(Regex.Matches(drawingOwner, @"(?m)^  - component:").Count, Is.EqualTo(4));
        Assert.That(ReadReferenceFileId(drawingTransform, "m_Father"), Is.EqualTo("2300000009"));
        Assert.That(ReadField(drawingTransform, "m_LocalScale"), Is.EqualTo("{x: 1, y: 1, z: 1}"));
        Assert.That(ReadField(drawingTransform, "m_AnchoredPosition"), Is.EqualTo("{x: -628, y: 55}"));
        Assert.That(ReadField(drawingTransform, "m_SizeDelta"), Is.EqualTo("{x: 163.2982, y: 299.816}"));
        Assert.That(drawingDoors, Does.Contain("m_Name: Doors"));
        Assert.That(ReadReferenceFileId(drawingDoorsTransform, "m_GameObject"), Is.EqualTo("2300000008"));
        Assert.That(ReadReferenceFileId(drawingDoorsTransform, "m_Father"), Is.EqualTo("2300000006"));
        Assert.That(ReadReferenceFileId(drawingRoomTransform, "m_GameObject"), Is.EqualTo("2300000005"));
        Assert.That(ReadReferenceFileId(drawingRoomTransform, "m_Father"), Is.EqualTo("668915133"));
        AssertLegacyTriggerSnapshot(
            drawingTrigger,
            "2300000095",
            "2300000098",
            "Drawing Room",
            "DrawingRoom_MusicRoom",
            "Music Room");

        Assert.That(musicOwner, Does.Contain("m_Name: DoorTrigger_MusicRoom_DrawingRoom"));
        Assert.That(musicOwner, Does.Contain("- component: {fileID: 2300000086}"));
        Assert.That(musicOwner, Does.Contain("- component: {fileID: 2300000089}"));
        Assert.That(Regex.Matches(musicOwner, @"(?m)^  - component:").Count, Is.EqualTo(4));
        Assert.That(ReadReferenceFileId(musicTransform, "m_Father"), Is.EqualTo("2103000011"));
        Assert.That(ReadField(musicTransform, "m_LocalScale"), Is.EqualTo("{x: 0.8625, y: 1, z: 1}"));
        Assert.That(ReadField(musicTransform, "m_AnchoredPosition"), Is.EqualTo("{x: -685, y: -36}"));
        Assert.That(ReadField(musicTransform, "m_SizeDelta"), Is.EqualTo("{x: 210.47021, y: 416.47205}"));
        Assert.That(musicDoors, Does.Contain("m_Name: Doors"));
        Assert.That(ReadReferenceFileId(musicDoorsTransform, "m_GameObject"), Is.EqualTo("2103000010"));
        Assert.That(ReadReferenceFileId(musicDoorsTransform, "m_Father"), Is.EqualTo("354156756"));
        Assert.That(ReadReferenceFileId(musicRoomTransform, "m_GameObject"), Is.EqualTo("354156755"));
        Assert.That(ReadReferenceFileId(musicRoomTransform, "m_Father"), Is.EqualTo("668915133"));
        AssertLegacyTriggerSnapshot(
            musicTrigger,
            "2300000085",
            "2300000088",
            "Music Room",
            "MusicRoom_DrawingRoom",
            "Drawing Room");

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
        Assert.That(documents.Values.Count(document =>
            document.Contains($"guid: {RoomViewGuid}") &&
            document.Contains("m_GameObject: {fileID: 354156755}")), Is.Zero);
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

    private static void AssertLegacyTriggerSnapshot(
        string trigger,
        string gameObjectFileId,
        string imageFileId,
        string sourceRoom,
        string legacyDoorId,
        string destinationRoom)
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
        Assert.That(ReadReferenceFileId(trigger, "navigationManager"), Is.EqualTo("0"));
        Assert.That(trigger, Does.Not.Contain("canonicalPassage:"));
        Assert.That(ReadReferenceFileId(trigger, "image"), Is.EqualTo(imageFileId));
        Assert.That(ReadReferenceFileId(trigger, "doorOpenAudioSource"), Is.EqualTo("0"));
        Assert.That(ReadReferenceFileId(trigger, "player"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "useBottomScreenEdgeInteraction"), Is.EqualTo("0"));
        Assert.That(ReadField(trigger, "requirePlayerProximity"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "walkPlayerToTriggerWhenFar"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "autoActivateAfterApproach"), Is.EqualTo("1"));
        Assert.That(ReadField(trigger, "maxPlayerScreenDistance"), Is.EqualTo("145"));
        Assert.That(ReadField(trigger, "playDoorOpenSound"), Is.EqualTo("1"));
        Assert.That(ReadReferenceFileId(trigger, "doorOpenSoundCatalog"), Is.EqualTo("0"));
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
