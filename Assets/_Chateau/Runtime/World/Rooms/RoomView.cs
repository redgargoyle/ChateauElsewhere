using Chateau.Architecture;
using UnityEngine;

namespace Chateau.World.Rooms
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Chateau/World/Rooms/Room View")]
    public sealed class RoomView : RoomElementBase
    {
        [SerializeField] private RoomDefinition definition;
        [SerializeField] private global::RoomContentGroup legacyContentGroup;

        public RoomDefinition Definition => definition;
        public global::RoomContentGroup LegacyContentGroup => legacyContentGroup;
        public Transform Root => transform;
        public bool IsVisible => gameObject.activeSelf;

        public override void ValidateConfiguration(ValidationReport report)
        {
            base.ValidateConfiguration(report);

            if (definition == null)
            {
                report.AddError("RoomView requires its RoomDefinition.", this);
            }

            if (legacyContentGroup == null)
            {
                report.AddError("RoomView requires its temporary RoomContentGroup compatibility edge.", this);
                return;
            }

            if (legacyContentGroup.transform != transform)
            {
                report.AddError("RoomView and its compatibility RoomContentGroup must share the same room root.", this);
            }

            if (definition != null && !definition.MatchesLegacyName(legacyContentGroup.RoomName))
            {
                report.AddError(
                    $"RoomView definition '{definition.StableId}' does not match legacy room '{legacyContentGroup.RoomName}'.",
                    this);
            }
        }
    }
}
