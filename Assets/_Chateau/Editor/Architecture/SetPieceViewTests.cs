#if UNITY_EDITOR
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
            Object.DestroyImmediate(setPieceObject);
            Object.DestroyImmediate(profile);
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
}
#endif
