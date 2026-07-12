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
    private const string ForwardPassagePath = "Assets/_Chateau/Data/World/Passages/Passage_GEH_DrawingRoom.asset";
    private const string ReversePassagePath = "Assets/_Chateau/Data/World/Passages/Passage_DrawingRoom_GEH.asset";
    private const string GameDatabasePath = "Assets/_Chateau/Data/GameDatabase.asset";

    [Test]
    public void CanonicalGehDrawingDataAssetsAreExactValidatedAndPassivelyBoundToRoomViews()
    {
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(EntranceRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(DrawingRoomPath), Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ForwardPassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(ReversePassagePath), Is.EqualTo(typeof(PassageDefinition)));
        Assert.That(AssetDatabase.GetMainAssetTypeAtPath(GameDatabasePath), Is.EqualTo(typeof(GameDatabase)));

        CanonicalRoomDefinition entrance = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(EntranceRoomPath);
        CanonicalRoomDefinition drawing = AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>(DrawingRoomPath);
        PassageDefinition forward = AssetDatabase.LoadAssetAtPath<PassageDefinition>(ForwardPassagePath);
        PassageDefinition reverse = AssetDatabase.LoadAssetAtPath<PassageDefinition>(ReversePassagePath);
        GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabasePath);

        Assert.That(entrance, Is.Not.Null);
        Assert.That(drawing, Is.Not.Null);
        Assert.That(forward, Is.Not.Null);
        Assert.That(reverse, Is.Not.Null);
        Assert.That(database, Is.Not.Null);

        Assert.That(AssetDatabase.AssetPathToGUID(EntranceRoomPath), Is.EqualTo("5e4e6adcd42c4058867aaa6c47b84de1"));
        Assert.That(AssetDatabase.AssetPathToGUID(DrawingRoomPath), Is.EqualTo("057575e9763145759aa12184580d27d8"));
        Assert.That(AssetDatabase.AssetPathToGUID(ForwardPassagePath), Is.EqualTo("0344228bb90d4997818e13c84f0bcf63"));
        Assert.That(AssetDatabase.AssetPathToGUID(ReversePassagePath), Is.EqualTo("50ae5112eed74cfda8588ff835b92516"));
        Assert.That(AssetDatabase.AssetPathToGUID(GameDatabasePath), Is.EqualTo("6b7925c3057e11ad688e890ddb547110"));

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

        Assert.That(database.Definitions, Has.Count.EqualTo(4));
        Assert.That(database.Definitions[0], Is.SameAs(entrance));
        Assert.That(database.Definitions[1], Is.SameAs(drawing));
        Assert.That(database.Definitions[2], Is.SameAs(forward));
        Assert.That(database.Definitions[3], Is.SameAs(reverse));

        string[] stableIds = database.Definitions.Select(definition => definition.StableId).ToArray();
        Assert.That(stableIds, Is.EqualTo(new[]
        {
            "room.grand-entrance-hall",
            "room.drawing-room",
            "passage.grand-entrance-hall.drawing-room",
            "passage.drawing-room.grand-entrance-hall"
        }));
        Assert.That(stableIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(4));

        ValidationReport report = new ValidationReport();
        database.ValidateConfiguration(report);
        Assert.That(report.HasErrors, Is.False, string.Join("\n", report.Messages.Select(message => message.ToString())));

        string gameplayText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string entranceRoomObject = ExtractDocument(gameplayText, "--- !u!1 &567115833");
        string drawingRoomObject = ExtractDocument(gameplayText, "--- !u!1 &2300000005");
        string entranceView = ExtractDocument(gameplayText, "--- !u!114 &4100000001");
        string drawingView = ExtractDocument(gameplayText, "--- !u!114 &4100000002");
        string gameRoot = ExtractDocument(gameplayText, "--- !u!114 &1878886998");
        string outboundTrigger = ExtractDocument(gameplayText, "--- !u!114 &109889178");
        string reverseTrigger = ExtractDocument(gameplayText, "--- !u!114 &2300000104");

        Assert.That(CountOccurrences(gameplayText, "guid: ccd2f3bd803e45aa8a1174cc881d6dc0"), Is.EqualTo(2),
            "Only the two characterized room roots may carry passive RoomViews at this gate.");
        Assert.That(gameplayText, Does.Not.Contain("guid: 518dad8adf634786a103bf4e76aa0881"),
            "Passage behavior remains outside the scene until the passive RoomView graft is proven safe.");

        Assert.That(entranceRoomObject, Does.Contain("- component: {fileID: 4100000001}"));
        Assert.That(drawingRoomObject, Does.Contain("- component: {fileID: 4100000002}"));
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

        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000001}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameRoot, "- {fileID: 4100000002}"), Is.EqualTo(1));
        Assert.That(CountOccurrences(gameplayText, "4100000001"), Is.EqualTo(3),
            "The entrance RoomView should occur only on its owner, its document header, and GameRoot registration.");
        Assert.That(CountOccurrences(gameplayText, "4100000002"), Is.EqualTo(3),
            "The drawing-room RoomView should occur only on its owner, its document header, and GameRoot registration.");

        AssertLegacyDoorTriggerUnchanged(
            outboundTrigger,
            "109889176",
            "Grand Entrance Hall",
            "GEH_Drawing_Room",
            "Drawing Room",
            "109889179");
        AssertLegacyDoorTriggerUnchanged(
            reverseTrigger,
            "2300000100",
            "Drawing Room",
            "DrawingRoom_GEH",
            "Grand Entrance Hall",
            "2300000103");
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
        Assert.That(typeof(INavigationService).IsInterface, Is.True);
        Assert.That(typeof(INavigationService).GetProperty("CurrentRoomDefinition")?.PropertyType, Is.EqualTo(typeof(CanonicalRoomDefinition)));
        Assert.That(typeof(INavigationService).GetMethod("CanTraverse")?.ReturnType, Is.EqualTo(typeof(bool)));
        Assert.That(typeof(INavigationService).GetMethod("TryTraverse")?.ReturnType, Is.EqualTo(typeof(bool)));
        Assert.That(typeof(INavigationService).IsAssignableFrom(typeof(RoomNavigationManager)), Is.False,
            "The pure-contract gate must not change the current navigation runtime path.");

        string gameplayText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        Assert.That(CountOccurrences(gameplayText, "guid: ccd2f3bd803e45aa8a1174cc881d6dc0"), Is.EqualTo(2));
        Assert.That(gameplayText, Does.Not.Contain("guid: 518dad8adf634786a103bf4e76aa0881"));
    }

    private static void AssertLegacyDoorTriggerUnchanged(
        string document,
        string gameObjectFileId,
        string sourceRoom,
        string doorName,
        string destinationRoom,
        string imageFileId)
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
        Assert.That(document, Does.Contain("navigationManager: {fileID: 0}"));
        Assert.That(document, Does.Contain($"image: {{fileID: {imageFileId}}}"));
        Assert.That(document, Does.Contain("doorOpenAudioSource: {fileID: 0}"));
        Assert.That(document, Does.Contain("player: {fileID: 0}"));
        Assert.That(document, Does.Contain("requirePlayerProximity: 1"));
        Assert.That(document, Does.Contain("walkPlayerToTriggerWhenFar: 1"));
        Assert.That(document, Does.Contain("autoActivateAfterApproach: 1"));
        Assert.That(document, Does.Contain("maxPlayerScreenDistance: 145"));
        Assert.That(document, Does.Contain("doorOpenSoundCatalog: {fileID: 0}"));
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
