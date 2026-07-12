#if UNITY_EDITOR
using System;
using System.IO;
using Chateau.Architecture;
using Chateau.World.Rooms.Props;
using NUnit.Framework;
using UnityEngine;

public sealed class SetPieceViewTests
{
    [Test]
    public void StaticSetPieceAppliesOneRoomLocalDepthPolicyWithoutMutatingArt()
    {
        RoomPerspectiveProfile profile = CreateProfile();
        GameObject setPieceObject = new GameObject("SetPiece");
        SpriteRenderer renderer = setPieceObject.AddComponent<SpriteRenderer>();
        SetPieceView view = setPieceObject.AddComponent<SetPieceView>();
        Vector2 anchor = new Vector2(-80.26f, -211.67f);
        Vector3 authoredPosition = new Vector3(-80.26f, -211.67f, -6570.105f);
        Vector3 authoredScale = new Vector3(99.52793f, 99.40213f, 73.00117f);
        Color authoredColor = new Color(0.8f, 0.7f, 0.6f, 0.9f);

        try
        {
            setPieceObject.transform.localPosition = authoredPosition;
            setPieceObject.transform.localScale = authoredScale;
            renderer.color = authoredColor;
            view.Configure(renderer, profile, anchor, 7);

            int expectedOrder = profile.GetSortingOrder(anchor, 7);
            Assert.That(view.ApplyPresentation(), Is.True);
            Assert.That(view.CurrentSortingOrder, Is.EqualTo(expectedOrder));
            Assert.That(renderer.sortingOrder, Is.EqualTo(expectedOrder));
            Assert.That(renderer.sortingLayerName, Is.EqualTo(profile.SortingLayerName));
            Assert.That(renderer.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
            Assert.That(setPieceObject.transform.localPosition, Is.EqualTo(authoredPosition));
            Assert.That(setPieceObject.transform.localScale, Is.EqualTo(authoredScale));
            Assert.That(renderer.color, Is.EqualTo(authoredColor));

            renderer.sortingOrder = -12345;
            Assert.That(view.ApplyPresentation(), Is.True);
            Assert.That(renderer.sortingOrder, Is.EqualTo(expectedOrder), "Repeated application must be idempotent.");
            Assert.That(RoomDepthResolver.Resolve(profile, new Vector2(999f, anchor.y), 7), Is.EqualTo(expectedOrder), "Static set-piece depth depends on room-local Y, not X.");
            Assert.That(RoomDepthResolver.Resolve(profile, anchor, 10), Is.EqualTo(expectedOrder + 3), "Sorting offsets remain additive.");

            ValidationReport report = new ValidationReport();
            view.ValidateConfiguration(report);
            Assert.That(report.HasErrors, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(setPieceObject);
            UnityEngine.Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void StaticSetPieceSourceHasNoFrameLoopBoundsLookupOrRuntimeFactory()
    {
        string viewText = File.ReadAllText("Assets/_Chateau/Runtime/World/Rooms/Props/SetPieces/SetPieceView.cs");
        string resolverText = File.ReadAllText("Assets/_Chateau/Runtime/World/Rooms/Props/SetPieces/RoomDepthResolver.cs");

        Assert.That(viewText, Does.Not.Contain("Update()"));
        Assert.That(viewText, Does.Not.Contain("LateUpdate()"));
        Assert.That(viewText, Does.Not.Contain(".bounds"));
        Assert.That(resolverText, Does.Not.Contain(".bounds"));
        Assert.That(viewText, Does.Not.Contain("FindObject"));
        Assert.That(viewText, Does.Not.Contain("GameObject.Find"));
        Assert.That(viewText, Does.Not.Contain("new GameObject"));
        Assert.That(viewText, Does.Not.Contain("AddComponent<"));
    }

    [Test]
    public void TeaTableIsSerializedOnceUnderSetPiecesInEveryDrawingRoomAsset()
    {
        AssertSerializedTeaTable(
            "Assets/Scenes/Gameplay.unity",
            "2088426361",
            "2088426359",
            "2088426360",
            "3930000001",
            "3502000003",
            "roomLocalOcclusionAnchor: {x: -80.26, y: -211.67}",
            "m_SortingOrder: 6627");
        AssertSerializedTeaTable(
            "Assets/Prefabs/Room_Drawing_Room.prefab",
            "4648226041189446053",
            "4469554848413931009",
            "3639458741953199328",
            "3931000001",
            "8198696041881719533",
            "roomLocalOcclusionAnchor: {x: -77.23, y: -208.14}",
            "m_SortingOrder: 6570");
        AssertSerializedTeaTable(
            "Assets/Prefabs/Room_Drawing_Room_Perspective.prefab",
            "2369478294726031537",
            "7736515036983942028",
            "5718819531062794842",
            "3932000001",
            "7119017594806998140",
            "roomLocalOcclusionAnchor: {x: -77.23, y: -208.14}",
            "m_SortingOrder: 6570");

        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        Assert.That(sceneText, Does.Contain("sourceObject: {fileID: 2088426358}"));
        Assert.That(sceneText, Does.Contain("sortSourceRenderers: 0"));
        Assert.That(sceneText, Does.Contain("- - {x: -214.44357, y: -357.79114}"));
        Assert.That(sceneText, Does.Contain("- {x: 53.923557, y: -357.79114}"));
        Assert.That(sceneText, Does.Contain("- {x: 53.923557, y: -270.11847}"));
        Assert.That(sceneText, Does.Contain("- {x: -214.44357, y: -270.11847}"));
        Assert.That(sceneText, Does.Contain("- {fileID: 2088426361}"), "GameRoot must bind the inactive scene SetPieceView.");
    }

    [Test]
    public void PurpleArmchairLegacyProjectionAndCollisionAreFrozenBeforeMigration()
    {
        AssertLegacyPurpleArmchair(
            "Assets/Scenes/Gameplay.unity",
            "216226071",
            "216226072",
            "216226073",
            "216226074",
            "2300000006",
            "m_LocalPosition: {x: 246.18, y: -323.26, z: -6570.105}",
            "m_LocalScale: {x: 98.85839, y: 100.35108, z: 73.00117}",
            "m_SortingOrder: 1000");
        AssertLegacyPurpleArmchair(
            "Assets/Prefabs/Room_Drawing_Room.prefab",
            "6132816832511842994",
            "941830645189917262",
            "6995352408496590697",
            "941830645189917263",
            "4435884376220043763",
            "m_LocalPosition: {x: 243.62, y: -315.58, z: -6570.105}",
            "m_LocalScale: {x: 96.91999, y: 94.67084, z: 73.00117}",
            "m_SortingOrder: 8289");
        AssertLegacyPurpleArmchair(
            "Assets/Prefabs/Room_Drawing_Room_Perspective.prefab",
            "3991942279459765031",
            "7285340557912092851",
            "5812709784311888689",
            "7285340557912092852",
            "6786387123008681009",
            "m_LocalPosition: {x: 243.62, y: -315.58, z: -6570.105}",
            "m_LocalScale: {x: 96.91999, y: 94.67084, z: 73.00117}",
            "m_SortingOrder: 8289");

        string sceneText = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string blockerDocument = ExtractDocument(sceneText, "--- !u!114 &2137426308");
        string colliderDocument = ExtractDocument(sceneText, "--- !u!60 &2137426309");
        Assert.That(blockerDocument, Does.Contain("sourceObject: {fileID: 216226071}"));
        Assert.That(blockerDocument, Does.Contain("sourceObjectName: purple_armchair_back"));
        Assert.That(blockerDocument, Does.Contain("category: Chair"));
        Assert.That(blockerDocument, Does.Contain("footprintHeightFraction: 0.3"));
        Assert.That(blockerDocument, Does.Contain("sortSourceRenderers: 1"));
        Assert.That(colliderDocument, Does.Contain("- - {x: 163.29713, y: -469.27084}"));
        Assert.That(colliderDocument, Does.Contain("- {x: 329.0629, y: -469.27084}"));
        Assert.That(colliderDocument, Does.Contain("- {x: 329.0629, y: -381.66437}"));
        Assert.That(colliderDocument, Does.Contain("- {x: 163.29713, y: -381.66437}"));
    }

    private static RoomPerspectiveProfile CreateProfile()
    {
        RoomPerspectiveProfile profile = ScriptableObject.CreateInstance<RoomPerspectiveProfile>();
        profile.Configure(
            "Drawing Room",
            new Vector2(1366f, 768f),
            -360f,
            140f,
            AnimationCurve.Linear(0f, 1f, 1f, 0.54f),
            null,
            1000,
            8000,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));
        return profile;
    }

    private static void AssertSerializedTeaTable(
        string assetPath,
        string viewFileId,
        string rendererFileId,
        string teaTransformFileId,
        string setPiecesTransformFileId,
        string propsTransformFileId,
        string expectedAnchor,
        string expectedOrder)
    {
        string assetText = File.ReadAllText(assetPath);
        string viewDocument = ExtractDocument(assetText, $"--- !u!114 &{viewFileId}");
        string setPiecesDocument = ExtractDocument(assetText, $"--- !u!4 &{setPiecesTransformFileId}");

        Assert.That(CountOccurrences(assetText, "guid: 5e7a11c7d4b24c68a1f9e2d3c4b5a607"), Is.EqualTo(1), assetPath);
        Assert.That(CountOccurrences(assetText, "m_Name: Set Pieces"), Is.EqualTo(1), assetPath);
        Assert.That(viewDocument, Does.Not.Contain("guid: 361e3658088b41ab98d330ae6457640b"), assetPath);
        Assert.That(viewDocument, Does.Contain($"cutoutRenderer: {{fileID: {rendererFileId}}}"), assetPath);
        Assert.That(viewDocument, Does.Contain(expectedAnchor), assetPath);
        Assert.That(assetText, Does.Contain(expectedOrder), assetPath);
        Assert.That(CountOccurrences(assetText, $"- {{fileID: {setPiecesTransformFileId}}}"), Is.EqualTo(1), assetPath);
        Assert.That(CountOccurrences(assetText, $"m_Father: {{fileID: {setPiecesTransformFileId}}}"), Is.EqualTo(1), assetPath);
        Assert.That(CountOccurrences(assetText, $"- {{fileID: {teaTransformFileId}}}"), Is.EqualTo(1), assetPath);
        Assert.That(setPiecesDocument, Does.Contain($"m_Father: {{fileID: {propsTransformFileId}}}"), assetPath);
        Assert.That(setPiecesDocument, Does.Contain($"- {{fileID: {teaTransformFileId}}}"), assetPath);
    }

    private static void AssertLegacyPurpleArmchair(
        string assetPath,
        string gameObjectFileId,
        string transformFileId,
        string rendererFileId,
        string projectionFileId,
        string expectedParentFileId,
        string expectedPosition,
        string expectedScale,
        string expectedOrder)
    {
        string assetText = File.ReadAllText(assetPath);
        string gameObjectDocument = ExtractDocument(assetText, $"--- !u!1 &{gameObjectFileId}");
        string transformDocument = ExtractDocument(assetText, $"--- !u!4 &{transformFileId}");
        string rendererDocument = ExtractDocument(assetText, $"--- !u!212 &{rendererFileId}");
        string projectionDocument = ExtractDocument(assetText, $"--- !u!114 &{projectionFileId}");

        Assert.That(gameObjectDocument, Does.Contain("m_Name: purple_armchair_back"), assetPath);
        Assert.That(gameObjectDocument, Does.Contain($"- component: {{fileID: {transformFileId}}}"), assetPath);
        Assert.That(gameObjectDocument, Does.Contain($"- component: {{fileID: {rendererFileId}}}"), assetPath);
        Assert.That(gameObjectDocument, Does.Contain($"- component: {{fileID: {projectionFileId}}}"), assetPath);
        Assert.That(transformDocument, Does.Contain(expectedPosition), assetPath);
        Assert.That(transformDocument, Does.Contain(expectedScale), assetPath);
        Assert.That(transformDocument, Does.Contain($"m_Father: {{fileID: {expectedParentFileId}}}"), assetPath);
        Assert.That(rendererDocument, Does.Contain("guid: a97c105638bdf8b4a8650670310a4cd3"), assetPath);
        Assert.That(rendererDocument, Does.Contain("m_SortingLayer: 2"), assetPath);
        Assert.That(rendererDocument, Does.Contain(expectedOrder), assetPath);
        Assert.That(rendererDocument, Does.Contain("m_Sprite: {fileID: 8151572500087809967, guid: 84e185b06bd4d9a19842586e593673e5, type: 3}"), assetPath);
        Assert.That(rendererDocument, Does.Contain("m_Color: {r: 1, g: 1, b: 1, a: 1}"), assetPath);
        Assert.That(rendererDocument, Does.Contain("m_SpriteSortPoint: 1"), assetPath);
        Assert.That(projectionDocument, Does.Contain("guid: 361e3658088b41ab98d330ae6457640b"), assetPath);
        Assert.That(projectionDocument, Does.Contain("roomProfile: {fileID: 11400000, guid: 426f8e326a60b3a0eaeb540d7d670267, type: 2}"), assetPath);
        Assert.That(projectionDocument, Does.Contain("projectionMode: 4"), assetPath);
        Assert.That(projectionDocument, Does.Contain("roomLocalFootPoint: {x: 243.62, y: -315.58}"), assetPath);
        Assert.That(projectionDocument, Does.Contain("applyPosition: 0"), assetPath);
        Assert.That(projectionDocument, Does.Contain("applyScale: 0"), assetPath);
        Assert.That(projectionDocument, Does.Contain("applyTint: 0"), assetPath);
        Assert.That(projectionDocument, Does.Contain("applySorting: 1"), assetPath);
        Assert.That(projectionDocument, Does.Contain("sortingOffset: 0"), assetPath);
        Assert.That(projectionDocument, Does.Not.Contain("guid: 5e7a11c7d4b24c68a1f9e2d3c4b5a607"), assetPath);
    }

    private static string ExtractDocument(string assetText, string header)
    {
        int start = assetText.IndexOf(header, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing document '{header}'.");
        int end = assetText.IndexOf("\n--- !u!", start + header.Length, StringComparison.Ordinal);
        return end >= 0 ? assetText.Substring(start, end - start) : assetText.Substring(start);
    }

    private static int CountOccurrences(string text, string value)
    {
        return text.Split(new[] { value }, StringSplitOptions.None).Length - 1;
    }
}
#endif
