using System;
using UnityEngine;

namespace Chateau.World.Rooms.Passages
{
    public interface IRoomViewLocalCoordinateMapper
    {
        bool TryGetLogicalPositionFromActiveRoomViewLocalPoint(
            Vector2 roomViewLocalPosition,
            out Vector2 logicalPosition);
    }

    public enum PassageAnchorCoordinateSpace
    {
        LegacyPlayerLogical = 0,
        RoomViewLocal = 1
    }

    [Serializable]
    public sealed class PassageAnchorData
    {
        [SerializeField] private PassageAnchorCoordinateSpace coordinateSpace;
        [SerializeField] private Vector2 logicalPosition;
        [SerializeField] private Vector2 roomViewLocalPosition;

        public PassageAnchorCoordinateSpace CoordinateSpace => coordinateSpace;
        public Vector2 LogicalPosition => logicalPosition;
        public Vector2 RoomViewLocalPosition => roomViewLocalPosition;
        public bool HasValidCoordinateSpace =>
            coordinateSpace == PassageAnchorCoordinateSpace.LegacyPlayerLogical ||
            coordinateSpace == PassageAnchorCoordinateSpace.RoomViewLocal;
        public bool HasFiniteAuthoredPosition =>
            HasValidCoordinateSpace &&
            IsFinite(coordinateSpace == PassageAnchorCoordinateSpace.LegacyPlayerLogical
                ? logicalPosition
                : roomViewLocalPosition);

        public bool TryResolveLogicalPosition(
            IRoomViewLocalCoordinateMapper mapper,
            out Vector2 resolvedLogicalPosition)
        {
            resolvedLogicalPosition = Vector2.zero;

            if (!HasFiniteAuthoredPosition)
            {
                return false;
            }

            if (coordinateSpace == PassageAnchorCoordinateSpace.LegacyPlayerLogical)
            {
                resolvedLogicalPosition = logicalPosition;
                return true;
            }

            if (mapper == null ||
                !mapper.TryGetLogicalPositionFromActiveRoomViewLocalPoint(
                    roomViewLocalPosition,
                    out resolvedLogicalPosition) ||
                !IsFinite(resolvedLogicalPosition))
            {
                resolvedLogicalPosition = Vector2.zero;
                return false;
            }

            return true;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) &&
                !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) &&
                !float.IsInfinity(value.y);
        }
    }
}
