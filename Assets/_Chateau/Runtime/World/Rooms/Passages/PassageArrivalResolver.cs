using System;
using System.Collections.Generic;
using Chateau.World.Rooms;
using UnityEngine;

namespace Chateau.World.Rooms.Passages
{
    [Serializable]
    public sealed class PassageArrivalRegionData
    {
        private const float MinimumCornerCrossMagnitude = 0.0001f;

        [SerializeField] private Vector2 bottomLeft;
        [SerializeField] private Vector2 topLeft;
        [SerializeField] private Vector2 topRight;
        [SerializeField] private Vector2 bottomRight;

        public Vector2 BottomLeft => bottomLeft;
        public Vector2 TopLeft => topLeft;
        public Vector2 TopRight => topRight;
        public Vector2 BottomRight => bottomRight;
        public bool HasValidRoomViewLocalCorners =>
            PassageArrivalResolver.IsFinite(bottomLeft) &&
            PassageArrivalResolver.IsFinite(topLeft) &&
            PassageArrivalResolver.IsFinite(topRight) &&
            PassageArrivalResolver.IsFinite(bottomRight) &&
            HasClockwiseTurn(bottomLeft, topLeft, topRight) &&
            HasClockwiseTurn(topLeft, topRight, bottomRight) &&
            HasClockwiseTurn(topRight, bottomRight, bottomLeft) &&
            HasClockwiseTurn(bottomRight, bottomLeft, topLeft);

        private static bool HasClockwiseTurn(Vector2 first, Vector2 second, Vector2 third)
        {
            Vector2 firstEdge = second - first;
            Vector2 secondEdge = third - second;
            float cross = firstEdge.x * secondEdge.y - firstEdge.y * secondEdge.x;
            return cross < -MinimumCornerCrossMagnitude;
        }
    }

    public readonly struct PassageArrivalRegionCorner
    {
        public PassageArrivalRegionCorner(Vector2 worldPosition, Vector2 screenPosition)
        {
            WorldPosition = worldPosition;
            ScreenPosition = screenPosition;
        }

        public Vector2 WorldPosition { get; }
        public Vector2 ScreenPosition { get; }
        public bool IsFinite =>
            PassageArrivalResolver.IsFinite(WorldPosition) &&
            PassageArrivalResolver.IsFinite(ScreenPosition);

        public static PassageArrivalRegionCorner Lerp(
            PassageArrivalRegionCorner from,
            PassageArrivalRegionCorner to,
            float t)
        {
            return new PassageArrivalRegionCorner(
                Vector2.Lerp(from.WorldPosition, to.WorldPosition, t),
                Vector2.Lerp(from.ScreenPosition, to.ScreenPosition, t));
        }
    }

    public readonly struct PassageArrivalRuntimeRegion
    {
        public PassageArrivalRuntimeRegion(
            PassageArrivalRegionCorner bottomLeft,
            PassageArrivalRegionCorner topLeft,
            PassageArrivalRegionCorner topRight,
            PassageArrivalRegionCorner bottomRight)
        {
            BottomLeft = bottomLeft;
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
        }

        public PassageArrivalRegionCorner BottomLeft { get; }
        public PassageArrivalRegionCorner TopLeft { get; }
        public PassageArrivalRegionCorner TopRight { get; }
        public PassageArrivalRegionCorner BottomRight { get; }

        public bool TryGetScreenBounds(out Vector2 min, out Vector2 max)
        {
            min = Vector2.zero;
            max = Vector2.zero;

            if (!BottomLeft.IsFinite ||
                !TopLeft.IsFinite ||
                !TopRight.IsFinite ||
                !BottomRight.IsFinite)
            {
                return false;
            }

            min = Vector2.Min(
                Vector2.Min(BottomLeft.ScreenPosition, TopLeft.ScreenPosition),
                Vector2.Min(TopRight.ScreenPosition, BottomRight.ScreenPosition));
            max = Vector2.Max(
                Vector2.Max(BottomLeft.ScreenPosition, TopLeft.ScreenPosition),
                Vector2.Max(TopRight.ScreenPosition, BottomRight.ScreenPosition));

            return max.x > min.x && max.y > min.y;
        }
    }

    public readonly struct PassageArrivalMovementQuery
    {
        public PassageArrivalMovementQuery(
            Vector2 destination,
            bool exactPointWalkable,
            bool hasReachableDestination)
        {
            Destination = destination;
            ExactPointWalkable = exactPointWalkable;
            HasReachableDestination = hasReachableDestination;
        }

        public Vector2 Destination { get; }
        public bool ExactPointWalkable { get; }
        public bool HasReachableDestination { get; }
    }

    public interface IPassageArrivalQuery
    {
        bool TryEvaluateReachableDestinationAtScreenPoint(
            Vector2 screenPosition,
            out PassageArrivalMovementQuery movementQuery);

        bool TryGetScreenPointFromLogicalPosition(
            Vector2 logicalPosition,
            out Vector2 screenPosition);

        bool TryFindClosestReachableDestinationToWorldPointTowardRoomCenter(
            Vector2 worldPosition,
            out Vector2 destination);
    }

    public static class PassageArrivalResolver
    {
        public const float RegionDistanceWeight = 10f;
        public const float PlayerDistanceWeight = 0.01f;
        public const float ProjectedPointPenalty = 25f;
        public const float DuplicateSampleDistance = 1f;
        public const float MinimumOutwardSampleOffset = 36f;

        public static bool TryBuildRuntimeRegion(
            PassageArrivalRegionData authoredRegion,
            RoomView destinationRoomView,
            Camera canvasCamera,
            out PassageArrivalRuntimeRegion runtimeRegion)
        {
            runtimeRegion = default;

            if (authoredRegion == null ||
                !authoredRegion.HasValidRoomViewLocalCorners ||
                destinationRoomView == null ||
                destinationRoomView.Root == null ||
                !TryProjectRoomViewLocalCorner(
                    authoredRegion.BottomLeft,
                    destinationRoomView.Root,
                    canvasCamera,
                    out PassageArrivalRegionCorner bottomLeft) ||
                !TryProjectRoomViewLocalCorner(
                    authoredRegion.TopLeft,
                    destinationRoomView.Root,
                    canvasCamera,
                    out PassageArrivalRegionCorner topLeft) ||
                !TryProjectRoomViewLocalCorner(
                    authoredRegion.TopRight,
                    destinationRoomView.Root,
                    canvasCamera,
                    out PassageArrivalRegionCorner topRight) ||
                !TryProjectRoomViewLocalCorner(
                    authoredRegion.BottomRight,
                    destinationRoomView.Root,
                    canvasCamera,
                    out PassageArrivalRegionCorner bottomRight))
            {
                return false;
            }

            runtimeRegion = new PassageArrivalRuntimeRegion(
                bottomLeft,
                topLeft,
                topRight,
                bottomRight);
            return runtimeRegion.TryGetScreenBounds(out _, out _);
        }

        public static bool TryResolveBestReachableDestination(
            PassageArrivalRuntimeRegion region,
            Vector2 playerScreenPosition,
            IPassageArrivalQuery query,
            out Vector2 destination)
        {
            destination = Vector2.zero;

            if (query == null ||
                !IsFinite(playerScreenPosition) ||
                !region.TryGetScreenBounds(out Vector2 min, out Vector2 max))
            {
                return false;
            }

            if (TryResolveFromOrderedScreenSamples(
                min,
                max,
                playerScreenPosition,
                query,
                out destination))
            {
                return true;
            }

            return TryResolveFromFallbackWorldSamples(region, query, out destination);
        }

        private static bool TryResolveFromOrderedScreenSamples(
            Vector2 min,
            Vector2 max,
            Vector2 playerScreenPosition,
            IPassageArrivalQuery query,
            out Vector2 destination)
        {
            destination = Vector2.zero;
            List<Vector2> samples = new List<Vector2>(32);
            CollectOrderedScreenSamples(samples, playerScreenPosition, min, max);

            bool foundDestination = false;
            float bestScore = float.MaxValue;
            Vector2 bestDestination = Vector2.zero;

            for (int i = 0; i < samples.Count; i++)
            {
                Vector2 samplePoint = samples[i];
                if (!query.TryEvaluateReachableDestinationAtScreenPoint(
                        samplePoint,
                        out PassageArrivalMovementQuery movementQuery) ||
                    !movementQuery.HasReachableDestination ||
                    !IsFinite(movementQuery.Destination) ||
                    !query.TryGetScreenPointFromLogicalPosition(
                        movementQuery.Destination,
                        out Vector2 destinationScreenPoint) ||
                    !IsFinite(destinationScreenPoint))
                {
                    continue;
                }

                Vector2 closestRegionPoint = GetClosestLowerEdgePoint(
                    destinationScreenPoint,
                    min,
                    max);
                float regionDistance = Vector2.Distance(destinationScreenPoint, closestRegionPoint);
                float playerDistance = Vector2.Distance(playerScreenPosition, destinationScreenPoint);
                float score = regionDistance * RegionDistanceWeight +
                    playerDistance * PlayerDistanceWeight +
                    (movementQuery.ExactPointWalkable ? 0f : ProjectedPointPenalty);

                if (score >= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestDestination = movementQuery.Destination;
                foundDestination = true;
            }

            if (!foundDestination)
            {
                return false;
            }

            destination = bestDestination;
            return true;
        }

        private static bool TryResolveFromFallbackWorldSamples(
            PassageArrivalRuntimeRegion region,
            IPassageArrivalQuery query,
            out Vector2 destination)
        {
            destination = Vector2.zero;
            bool foundDestination = false;
            float bestScore = float.MaxValue;
            Vector2 bestDestination = Vector2.zero;

            ScoreFallbackSample(
                PassageArrivalRegionCorner.Lerp(region.BottomLeft, region.BottomRight, 0.5f),
                query,
                ref foundDestination,
                ref bestScore,
                ref bestDestination);
            ScoreFallbackSample(
                PassageArrivalRegionCorner.Lerp(region.BottomLeft, region.BottomRight, 0.25f),
                query,
                ref foundDestination,
                ref bestScore,
                ref bestDestination);
            ScoreFallbackSample(
                PassageArrivalRegionCorner.Lerp(region.BottomLeft, region.BottomRight, 0.75f),
                query,
                ref foundDestination,
                ref bestScore,
                ref bestDestination);
            ScoreFallbackSample(
                PassageArrivalRegionCorner.Lerp(region.TopLeft, region.TopRight, 0.5f),
                query,
                ref foundDestination,
                ref bestScore,
                ref bestDestination);
            ScoreFallbackSample(
                PassageArrivalRegionCorner.Lerp(region.BottomLeft, region.TopRight, 0.5f),
                query,
                ref foundDestination,
                ref bestScore,
                ref bestDestination);
            ScoreFallbackSample(
                region.BottomLeft,
                query,
                ref foundDestination,
                ref bestScore,
                ref bestDestination);
            ScoreFallbackSample(
                region.BottomRight,
                query,
                ref foundDestination,
                ref bestScore,
                ref bestDestination);

            if (!foundDestination)
            {
                return false;
            }

            destination = bestDestination;
            return true;
        }

        private static void ScoreFallbackSample(
            PassageArrivalRegionCorner sample,
            IPassageArrivalQuery query,
            ref bool foundDestination,
            ref float bestScore,
            ref Vector2 bestDestination)
        {
            if (!sample.IsFinite ||
                !query.TryFindClosestReachableDestinationToWorldPointTowardRoomCenter(
                    sample.WorldPosition,
                    out Vector2 candidateDestination) ||
                !IsFinite(candidateDestination) ||
                !query.TryGetScreenPointFromLogicalPosition(
                    candidateDestination,
                    out Vector2 candidateScreenPoint) ||
                !IsFinite(candidateScreenPoint))
            {
                return;
            }

            float score = Vector2.SqrMagnitude(candidateScreenPoint - sample.ScreenPosition);

            if (foundDestination && score >= bestScore)
            {
                return;
            }

            foundDestination = true;
            bestScore = score;
            bestDestination = candidateDestination;
        }

        private static void CollectOrderedScreenSamples(
            List<Vector2> samples,
            Vector2 playerScreenPosition,
            Vector2 min,
            Vector2 max)
        {
            float centerX = (min.x + max.x) * 0.5f;
            float centerY = (min.y + max.y) * 0.5f;
            float lowerY = min.y;
            float upperY = max.y;
            float leftX = min.x;
            float rightX = max.x;

            AddDistinctSample(samples, GetClosestLowerEdgePoint(playerScreenPosition, min, max));
            AddDistinctSample(samples, new Vector2(centerX, lowerY));
            AddDistinctSample(samples, new Vector2(Mathf.Lerp(leftX, rightX, 0.25f), lowerY));
            AddDistinctSample(samples, new Vector2(Mathf.Lerp(leftX, rightX, 0.75f), lowerY));
            AddDistinctSample(samples, new Vector2(leftX, lowerY));
            AddDistinctSample(samples, new Vector2(rightX, lowerY));
            AddDistinctSample(samples, new Vector2(centerX, centerY));
            AddDistinctSample(samples, new Vector2(leftX, centerY));
            AddDistinctSample(samples, new Vector2(rightX, centerY));
            AddDistinctSample(samples, new Vector2(centerX, upperY));
            AddDistinctSample(samples, new Vector2(leftX, upperY));
            AddDistinctSample(samples, new Vector2(rightX, upperY));

            float width = Mathf.Max(1f, rightX - leftX);
            float height = Mathf.Max(1f, upperY - lowerY);
            float offset = Mathf.Max(MinimumOutwardSampleOffset, Mathf.Min(width, height) * 0.35f);

            AddEdgeSamples(samples, leftX, rightX, centerX, lowerY, -offset);
            AddEdgeSamples(samples, leftX, rightX, centerX, lowerY, -offset * 2f);
            AddEdgeSamples(samples, leftX, rightX, centerX, upperY, offset);
            AddEdgeSamples(samples, leftX, rightX, centerX, upperY, offset * 2f);
            AddDistinctSample(samples, new Vector2(leftX - offset, centerY));
            AddDistinctSample(samples, new Vector2(leftX - offset * 2f, centerY));
            AddDistinctSample(samples, new Vector2(rightX + offset, centerY));
            AddDistinctSample(samples, new Vector2(rightX + offset * 2f, centerY));
        }

        private static void AddEdgeSamples(
            List<Vector2> samples,
            float leftX,
            float rightX,
            float centerX,
            float edgeY,
            float yOffset)
        {
            float sampleY = edgeY + yOffset;
            AddDistinctSample(samples, new Vector2(centerX, sampleY));
            AddDistinctSample(samples, new Vector2(Mathf.Lerp(leftX, rightX, 0.25f), sampleY));
            AddDistinctSample(samples, new Vector2(Mathf.Lerp(leftX, rightX, 0.75f), sampleY));
        }

        private static void AddDistinctSample(List<Vector2> samples, Vector2 sample)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (Vector2.Distance(samples[i], sample) <= DuplicateSampleDistance)
                {
                    return;
                }
            }

            samples.Add(sample);
        }

        private static Vector2 GetClosestLowerEdgePoint(
            Vector2 screenPosition,
            Vector2 min,
            Vector2 max)
        {
            return new Vector2(Mathf.Clamp(screenPosition.x, min.x, max.x), min.y);
        }

        private static bool TryProjectRoomViewLocalCorner(
            Vector2 roomViewLocalCorner,
            Transform destinationRoomViewRoot,
            Camera canvasCamera,
            out PassageArrivalRegionCorner corner)
        {
            corner = default;
            Vector3 worldPosition = destinationRoomViewRoot.TransformPoint(
                new Vector3(roomViewLocalCorner.x, roomViewLocalCorner.y, 0f));
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(
                canvasCamera,
                worldPosition);
            Vector2 worldPosition2D = worldPosition;

            if (!IsFinite(worldPosition2D) || !IsFinite(screenPosition))
            {
                return false;
            }

            corner = new PassageArrivalRegionCorner(worldPosition2D, screenPosition);
            return true;
        }

        internal static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) &&
                !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) &&
                !float.IsInfinity(value.y);
        }
    }
}
