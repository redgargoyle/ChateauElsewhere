using System;
using UnityEngine;

namespace Chateau.World.Rooms.Passages
{
    [Serializable]
    public sealed class PassageAnchorData
    {
        [SerializeField] private Vector2 logicalPosition;

        public Vector2 LogicalPosition => logicalPosition;
    }
}
