using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PointClickStandaloneMovementRegressionTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void RoomStagePixelCoordinatesPreserveAuthoredWorldMovementSpeed()
    {
        GameObject player = new GameObject("StandaloneMovementScaleTest");

        try
        {
            PointClickPlayerMovement movement = player.AddComponent<PointClickPlayerMovement>();
            float standaloneStageScaleRatio = 10f / 1440f;
            float authoredWorldStep = 3.2f * 0.02f;

            SetPrivateField(movement, "hasRoomStageVisualReference", true);
            SetPrivateField(movement, "currentRoomStageScaleRatio", standaloneStageScaleRatio);

            float logicalStep = InvokeDistanceConversion(movement, authoredWorldStep);

            Assert.That(logicalStep, Is.GreaterThan(authoredWorldStep * 100f));
            Assert.That(
                logicalStep * standaloneStageScaleRatio,
                Is.EqualTo(authoredWorldStep).Within(0.000001f),
                "Room-stage pixel coordinates must not reduce the Butler to a few pixels per second.");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void WorldCoordinatesKeepAuthoredMovementDistance()
    {
        GameObject player = new GameObject("WorldMovementScaleTest");

        try
        {
            PointClickPlayerMovement movement = player.AddComponent<PointClickPlayerMovement>();
            const float authoredWorldStep = 0.064f;

            SetPrivateField(movement, "hasRoomStageVisualReference", false);
            SetPrivateField(movement, "currentRoomStageScaleRatio", 1f);

            Assert.That(
                InvokeDistanceConversion(movement, authoredWorldStep),
                Is.EqualTo(authoredWorldStep).Within(0.000001f));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void StartupWaitsForCanvasLayoutAndDiscardsTransientStageScale()
    {
        GameObject player = new GameObject("StandaloneStartupScaleTest");

        try
        {
            player.transform.position = new Vector3(2f, -3f, 0f);
            PointClickPlayerMovement movement = player.AddComponent<PointClickPlayerMovement>();
            SetPrivateField(movement, "allowMovementWithoutWalkableFloor", true);
            SetPrivateField(movement, "useCurrentRoomBoundary", false);
            SetPrivateField(movement, "walkableFloorName", "__MissingStandaloneTestBoundary__");

            IEnumerator startup = InvokeStartup(movement);
            Assert.That(startup.MoveNext(), Is.True, "Startup must wait one frame for the Canvas layout pass.");
            Assert.That(startup.Current, Is.Null);

            SetPrivateField(movement, "hasRoomStageVisualReference", true);
            SetPrivateField(movement, "roomStageReferenceScale", 1f);
            SetPrivateField(movement, "currentRoomStageScaleRatio", 10f / 1440f);

            Assert.That(startup.MoveNext(), Is.False, "Startup should finish after rebasing the finalized layout.");
            Assert.That(GetPrivateField<float>(movement, "currentRoomStageScaleRatio"), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(movement.LogicalPosition.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(movement.LogicalPosition.y, Is.EqualTo(-3f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void RoomStageResetRebasesAStaleLogicalPositionFromTheVisibleWorldPoint()
    {
        GameObject player = new GameObject("StandaloneRoomRebaseTest");

        try
        {
            PointClickPlayerMovement movement = player.AddComponent<PointClickPlayerMovement>();
            SetPrivateField(movement, "logicalPosition", new Vector2(-2.67f, -68.69f));
            SetPrivateField(movement, "hasRoomStageVisualReference", true);
            SetPrivateField(movement, "roomStageReferenceScale", 1f);
            SetPrivateField(movement, "currentRoomStageScaleRatio", 10f / 1440f);

            InvokeStageReset(movement);
            Vector2 visibleWorldPoint = new Vector2(-2.67f, -3.1f);
            Vector2 rebased = InvokeWorldToLogical(movement, visibleWorldPoint);

            Assert.That(rebased.x, Is.EqualTo(visibleWorldPoint.x).Within(0.0001f));
            Assert.That(rebased.y, Is.EqualTo(visibleWorldPoint.y).Within(0.0001f));
            Assert.That(
                Mathf.Abs(rebased.y),
                Is.LessThan(20f),
                "A finalized room-stage reset must not retain the stale standalone canvas-pixel coordinate.");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    private static float InvokeDistanceConversion(PointClickPlayerMovement movement, float worldDistance)
    {
        MethodInfo method = typeof(PointClickPlayerMovement).GetMethod(
            "ConvertWorldDistanceToLogicalDistance",
            PrivateInstance);

        Assert.That(method, Is.Not.Null);
        return (float)method.Invoke(movement, new object[] { worldDistance });
    }

    private static IEnumerator InvokeStartup(PointClickPlayerMovement movement)
    {
        MethodInfo method = typeof(PointClickPlayerMovement).GetMethod("Start", PrivateInstance);
        Assert.That(method, Is.Not.Null);
        return (IEnumerator)method.Invoke(movement, null);
    }

    private static void InvokeStageReset(PointClickPlayerMovement movement)
    {
        MethodInfo method = typeof(PointClickPlayerMovement).GetMethod(
            "ResetRoomStageVisualReference",
            PrivateInstance);
        Assert.That(method, Is.Not.Null);
        method.Invoke(movement, null);
    }

    private static Vector2 InvokeWorldToLogical(PointClickPlayerMovement movement, Vector2 worldPoint)
    {
        MethodInfo method = typeof(PointClickPlayerMovement).GetMethod(
            "WalkableWorldToLogicalPoint",
            PrivateInstance);
        Assert.That(method, Is.Not.Null);
        return (Vector2)method.Invoke(movement, new object[] { worldPoint });
    }

    private static void SetPrivateField<T>(PointClickPlayerMovement movement, string fieldName, T value)
    {
        FieldInfo field = typeof(PointClickPlayerMovement).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"PointClickPlayerMovement is missing private field '{fieldName}'.");
        field.SetValue(movement, value);
    }

    private static T GetPrivateField<T>(PointClickPlayerMovement movement, string fieldName)
    {
        FieldInfo field = typeof(PointClickPlayerMovement).GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"PointClickPlayerMovement is missing private field '{fieldName}'.");
        return (T)field.GetValue(movement);
    }
}
