using System;
using UnityEngine;

namespace Chateau.World.Rooms.Props
{
    public static class RoomDepthResolver
    {
        public static int Resolve(
            global::RoomPerspectiveProfile profile,
            Vector2 roomLocalOcclusionAnchor,
            int sortingOffset = 0)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return profile.GetSortingOrder(roomLocalOcclusionAnchor, sortingOffset);
        }
    }
}
