using Chateau.Architecture;
using Chateau.World.Rooms;
using UnityEngine;

namespace Chateau.World.Rooms.Passages
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Chateau/World/Rooms/Passages/Passage")]
    public sealed class Passage : RoomElementBase
    {
        [SerializeField] private PassageDefinition definition;
        [SerializeField] private RoomView sourceRoomView;
        [SerializeField] private Passage reversePassage;
        [SerializeField] private PassageAnchorData approachAnchor;
        [SerializeField] private PassageAnchorData arrivalAnchor;

        public PassageDefinition Definition => definition;
        public RoomView SourceRoomView => sourceRoomView;
        public Passage ReversePassage => reversePassage;
        public PassageAnchorData ApproachAnchor => approachAnchor;
        public PassageAnchorData ArrivalAnchor => arrivalAnchor;

        public override void ValidateConfiguration(ValidationReport report)
        {
            base.ValidateConfiguration(report);

            if (definition == null)
            {
                report.AddError("Passage requires its PassageDefinition.", this);
            }

            if (sourceRoomView == null)
            {
                report.AddError("Passage requires its source RoomView.", this);
            }
            else if (transform == sourceRoomView.transform || !transform.IsChildOf(sourceRoomView.transform))
            {
                report.AddError("Passage must be a descendant of its source RoomView.", this);
            }

            if (definition != null && sourceRoomView != null &&
                sourceRoomView.Definition != definition.SourceRoom)
            {
                report.AddError("Passage source RoomView does not match its definition source room.", this);
            }

            if (reversePassage == null)
            {
                report.AddError("Passage requires its reverse scene passage.", this);
            }
            else if (reversePassage == this)
            {
                report.AddError("Passage cannot reverse to itself.", this);
            }
            else
            {
                if (reversePassage.reversePassage != this)
                {
                    report.AddError("Passage reverse scene link must be reciprocal.", this);
                }

                if (definition != null && reversePassage.definition != definition.Reverse)
                {
                    report.AddError("Passage reverse scene definition does not match its definition reverse.", this);
                }

                if (definition != null && reversePassage.sourceRoomView != null &&
                    reversePassage.sourceRoomView.Definition != definition.DestinationRoom)
                {
                    report.AddError("Passage reverse scene owner does not match its destination room.", this);
                }
            }

            if (approachAnchor == null)
            {
                report.AddError("Passage requires authored logical approach data.", this);
            }

            if (arrivalAnchor == null)
            {
                report.AddError("Passage requires authored logical arrival data.", this);
            }
        }
    }
}
