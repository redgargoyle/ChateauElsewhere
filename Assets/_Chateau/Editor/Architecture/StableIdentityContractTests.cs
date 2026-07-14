#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Chateau.Architecture;
using Chateau.World.Rooms.Passages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

using CanonicalRoomDefinition = Chateau.World.Rooms.RoomDefinition;

public sealed class StableIdentityContractTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void RoomIdAcceptsCanonicalRoomIdentityAndTrimsOnlyOuterWhitespace()
    {
        Assert.That(RoomId.TryParse("  room.grand-entrance-hall  ", out RoomId id), Is.True);
        Assert.That(id.Value, Is.EqualTo("room.grand-entrance-hall"));
        Assert.That(id.IsValid, Is.True);
        Assert.That(id.IsEmpty, Is.False);
        Assert.That(id.ToString(), Is.EqualTo(id.Value));
    }

    [Test]
    public void PassageIdAcceptsCanonicalDirectedPassageIdentity()
    {
        PassageId id = PassageId.Parse(" passage.grand-entrance-hall.drawing-room ");

        Assert.That(id.Value, Is.EqualTo("passage.grand-entrance-hall.drawing-room"));
        Assert.That(id.IsValid, Is.True);
        Assert.That(id.IsEmpty, Is.False);
    }

    [Test]
    public void ActorIdStartsNewActorIdentityWithAnExplicitDomainPrefix()
    {
        ActorId id = ActorId.Parse(" actor.butler ");

        Assert.That(id.Value, Is.EqualTo("actor.butler"));
        Assert.That(id.IsValid, Is.True);
        Assert.That(ActorId.TryParse("guest_1", out _), Is.False);
    }

    [Test]
    public void ChapterIdPreservesTheApprovedDurableChapterFormat()
    {
        Assert.That(ChapterId.TryParse(" chapter_01_arrivals ", out ChapterId id), Is.True);
        Assert.That(id.Value, Is.EqualTo("chapter_01_arrivals"));
        Assert.That(ChapterId.Parse("chapter_02_guest_search").IsValid, Is.True);
        Assert.That(ChapterId.Parse("chapter_03_dinner_pending").IsValid, Is.True);
    }

    [Test]
    public void BeatIdRequiresAnExplicitBeatIdentity()
    {
        BeatId id = BeatId.Parse(" beat.chapter-01.first-arrival ");

        Assert.That(id.Value, Is.EqualTo("beat.chapter-01.first-arrival"));
        Assert.That(id.IsValid, Is.True);
    }

    [Test]
    public void ObjectiveIdRequiresAnExplicitObjectiveIdentity()
    {
        ObjectiveId id = ObjectiveId.Parse(" objective.chapter-01.store-coat ");

        Assert.That(id.Value, Is.EqualTo("objective.chapter-01.store-coat"));
        Assert.That(id.IsValid, Is.True);
    }

    [Test]
    public void DisplayLabelsLegacyAliasesAndWrongDomainsAreRejected()
    {
        Assert.That(RoomId.TryParse("Grand Entrance Hall", out _), Is.False);
        Assert.That(PassageId.TryParse("GEH_Drawing_Room", out _), Is.False);
        Assert.That(ActorId.TryParse("Butler", out _), Is.False);
        Assert.That(ChapterId.TryParse("Chapter 1", out _), Is.False);
        Assert.That(BeatId.TryParse("Arrival Schedule", out _), Is.False);
        Assert.That(ObjectiveId.TryParse("Store Guest Coat", out _), Is.False);
        Assert.That(RoomId.TryParse("passage.entrance.drawing-room", out _), Is.False);
        Assert.That(PassageId.TryParse("room.drawing-room", out _), Is.False);
    }

    [Test]
    public void NullBlankUppercaseAndMalformedStableTokensAreRejectedWithoutRepair()
    {
        string[] invalidRoomIds =
        {
            null,
            string.Empty,
            "   ",
            "Room.grand-entrance-hall",
            "room.Grand-entrance-hall",
            "room.grand entrance hall",
            "room.grand/entrance",
            "room..grand-entrance-hall",
            "room.grand-entrance-hall-",
            "room."
        };

        Assert.That(invalidRoomIds.All(value => !RoomId.TryParse(value, out _)), Is.True);
        Assert.Throws<ArgumentException>(() => RoomId.Parse("Drawing Room"));
        Assert.Throws<ArgumentException>(() => PassageId.Parse("passage.Drawing.Room"));
        Assert.Throws<ArgumentException>(() => ActorId.Parse("actor."));
        Assert.Throws<ArgumentException>(() => ChapterId.Parse("chapter__01"));
        Assert.Throws<ArgumentException>(() => BeatId.Parse("beat.arrival/one"));
        Assert.Throws<ArgumentException>(() => ObjectiveId.Parse("objective.arrival-"));
    }

    [Test]
    public void DefaultStableIdsAreExplicitlyEmptyAndInvalid()
    {
        AssertInvalidDefault(default(RoomId));
        AssertInvalidDefault(default(PassageId));
        AssertInvalidDefault(default(ActorId));
        AssertInvalidDefault(default(ChapterId));
        AssertInvalidDefault(default(BeatId));
        AssertInvalidDefault(default(ObjectiveId));
    }

    [Test]
    public void EqualityHashingAndOperatorsAreOrdinalAndTypeSeparated()
    {
        RoomId first = RoomId.Parse("room.library");
        RoomId same = RoomId.Parse(" room.library ");
        RoomId different = RoomId.Parse("room.music-room");
        Dictionary<RoomId, string> index = new Dictionary<RoomId, string>
        {
            [first] = "Library"
        };

        Assert.That(first, Is.EqualTo(same));
        Assert.That(first == same, Is.True);
        Assert.That(first != different, Is.True);
        Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
        Assert.That(index[same], Is.EqualTo("Library"));
        Assert.That(first.Equals((object)PassageId.Parse("passage.library.music-room")), Is.False);

        Type[] idTypes =
        {
            typeof(RoomId),
            typeof(PassageId),
            typeof(ActorId),
            typeof(ChapterId),
            typeof(BeatId),
            typeof(ObjectiveId)
        };
        Assert.That(
            idTypes.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Any(method => method.Name == "op_Implicit"),
            Is.False,
            "Stable IDs must not silently cross a string or domain boundary.");
    }

    [Test]
    public void UnitySerializationRoundTripsAllSixConcreteValueTypes()
    {
        IdentityEnvelope source = new IdentityEnvelope
        {
            room = RoomId.Parse("room.library"),
            passage = PassageId.Parse("passage.library.music-room"),
            actor = ActorId.Parse("actor.butler"),
            chapter = ChapterId.Parse("chapter_01_arrivals"),
            beat = BeatId.Parse("beat.chapter-01.first-arrival"),
            objective = ObjectiveId.Parse("objective.chapter-01.store-coat")
        };

        string json = JsonUtility.ToJson(source);
        IdentityEnvelope restored = JsonUtility.FromJson<IdentityEnvelope>(json);

        Assert.That(restored, Is.Not.Null);
        Assert.That(restored.room, Is.EqualTo(source.room));
        Assert.That(restored.passage, Is.EqualTo(source.passage));
        Assert.That(restored.actor, Is.EqualTo(source.actor));
        Assert.That(restored.chapter, Is.EqualTo(source.chapter));
        Assert.That(restored.beat, Is.EqualTo(source.beat));
        Assert.That(restored.objective, Is.EqualTo(source.objective));
        Assert.That(json, Does.Contain("room.library"));
        Assert.That(json, Does.Contain("chapter_01_arrivals"));
    }

    [Test]
    public void DefinitionsAndStoryBeatExposeTypedSeamsWithoutChangingStringCompatibility()
    {
        CanonicalRoomDefinition room = ScriptableObject.CreateInstance<CanonicalRoomDefinition>();
        PassageDefinition passage = ScriptableObject.CreateInstance<PassageDefinition>();

        try
        {
            SetStableId(room, "  room.grand-entrance-hall  ");
            SetStableId(passage, "  passage.grand-entrance-hall.drawing-room  ");

            Assert.That(room.StableId, Is.EqualTo("room.grand-entrance-hall"));
            Assert.That(room.TryGetId(out RoomId roomId), Is.True);
            Assert.That(room.Id, Is.EqualTo(roomId));
            Assert.That(room.Id.Value, Is.EqualTo(room.StableId));

            Assert.That(passage.StableId, Is.EqualTo("passage.grand-entrance-hall.drawing-room"));
            Assert.That(passage.TryGetId(out PassageId passageId), Is.True);
            Assert.That(passage.Id, Is.EqualTo(passageId));
            Assert.That(passage.Id.Value, Is.EqualTo(passage.StableId));

            AssertCurrentCanonicalDefinitionAssets();

            TestStoryBeat legacyBeat = new TestStoryBeat("legacy beat label");
            Assert.That(legacyBeat.BeatId, Is.EqualTo("legacy beat label"));
            Assert.That(legacyBeat.TryGetId(out BeatId legacyTypedId), Is.False);
            Assert.That(legacyTypedId, Is.EqualTo(default(BeatId)));
            Assert.Throws<InvalidOperationException>(() => legacyBeat.Id.GetHashCode());

            BeatId typedId = BeatId.Parse("beat.chapter-01.first-arrival");
            TestStoryBeat typedBeat = new TestStoryBeat(typedId);
            Assert.That(typedBeat.BeatId, Is.EqualTo(typedId.Value));
            Assert.That(typedBeat.Id, Is.EqualTo(typedId));
            Assert.That(typedBeat.TryGetId(out BeatId recoveredTypedId), Is.True);
            Assert.That(recoveredTypedId, Is.EqualTo(typedId));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(passage);
        }
    }

    private static void AssertCurrentCanonicalDefinitionAssets()
    {
        string[] expectedRoomIds =
        {
            "room.grand-entrance-hall",
            "room.drawing-room",
            "room.music-room",
            "room.library",
            "room.ballroom",
            "room.dining-room",
            "room.butlers-pantry",
            "room.billiard-room",
            "room.service-corridor",
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
        };
        string[] expectedPassageIds =
        {
            "passage.grand-entrance-hall.drawing-room",
            "passage.drawing-room.grand-entrance-hall",
            "passage.drawing-room.music-room",
            "passage.music-room.drawing-room",
            "passage.music-room.library",
            "passage.library.music-room",
            "passage.library.ballroom",
            "passage.ballroom.library",
            "passage.grand-entrance-hall.dining-room",
            "passage.dining-room.grand-entrance-hall",
            "passage.dining-room.butlers-pantry",
            "passage.butlers-pantry.dining-room",
            "passage.butlers-pantry.billiard-room",
            "passage.billiard-room.butlers-pantry",
            "passage.butlers-pantry.service-corridor",
            "passage.service-corridor.butlers-pantry",
            "passage.service-corridor.kitchen",
            "passage.kitchen.service-corridor"
        };

        CanonicalRoomDefinition[] rooms = AssetDatabase.FindAssets(
                string.Empty,
                new[] { "Assets/_Chateau/Data/World/Rooms" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CanonicalRoomDefinition>)
            .Where(definition => definition != null)
            .ToArray();
        PassageDefinition[] passages = AssetDatabase.FindAssets(
                string.Empty,
                new[] { "Assets/_Chateau/Data/World/Passages" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<PassageDefinition>)
            .Where(definition => definition != null)
            .ToArray();

        Assert.That(rooms, Has.Length.EqualTo(19));
        Assert.That(passages, Has.Length.EqualTo(18));
        Assert.That(rooms.Select(definition => definition.StableId), Is.EquivalentTo(expectedRoomIds));
        Assert.That(passages.Select(definition => definition.StableId), Is.EquivalentTo(expectedPassageIds));

        HashSet<RoomId> typedRoomIds = new HashSet<RoomId>();

        foreach (CanonicalRoomDefinition room in rooms)
        {
            Assert.That(room.TryGetId(out RoomId roomId), Is.True, room.name);
            Assert.That(roomId.Value, Is.EqualTo(room.StableId), room.name);
            Assert.That(typedRoomIds.Add(roomId), Is.True, $"Duplicate typed room ID '{roomId}'.");
        }

        HashSet<PassageId> typedPassageIds = new HashSet<PassageId>();

        foreach (PassageDefinition passage in passages)
        {
            Assert.That(passage.TryGetId(out PassageId passageId), Is.True, passage.name);
            Assert.That(passageId.Value, Is.EqualTo(passage.StableId), passage.name);
            Assert.That(typedPassageIds.Add(passageId), Is.True, $"Duplicate typed passage ID '{passageId}'.");
        }
    }

    private static void AssertInvalidDefault<T>(T id)
    {
        Type type = typeof(T);
        Assert.That((string)type.GetProperty("Value").GetValue(id), Is.EqualTo(string.Empty));
        Assert.That((bool)type.GetProperty("IsEmpty").GetValue(id), Is.True);
        Assert.That((bool)type.GetProperty("IsValid").GetValue(id), Is.False);
        Assert.That(id.ToString(), Is.EqualTo(string.Empty));
    }

    private static void SetStableId(DefinitionAssetBase definition, string stableId)
    {
        FieldInfo field = typeof(DefinitionAssetBase).GetField("stableId", PrivateInstance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(definition, stableId);
    }

    [Serializable]
    private sealed class IdentityEnvelope
    {
        public RoomId room;
        public PassageId passage;
        public ActorId actor;
        public ChapterId chapter;
        public BeatId beat;
        public ObjectiveId objective;
    }

    private sealed class TestStoryBeat : StoryBeatBase
    {
        public TestStoryBeat(string beatId)
            : base(beatId)
        {
        }

        public TestStoryBeat(BeatId beatId)
            : base(beatId)
        {
        }

        public override void Enter()
        {
        }

        public override void Tick(float unscaledDeltaTime)
        {
        }

        public override void Cancel()
        {
        }
    }
}
#endif
