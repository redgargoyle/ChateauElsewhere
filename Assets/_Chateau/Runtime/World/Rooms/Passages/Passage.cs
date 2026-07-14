using Chateau.Architecture;
using Chateau.World.Rooms;
using UnityEngine;

namespace Chateau.World.Rooms.Passages
{
    public enum PassageAnchorMigrationStage
    {
        LegacySampling = 0,
        AuthoredArrival = 1,
        AuthoredAnchors = 2
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Chateau/World/Rooms/Passages/Passage")]
    public sealed class Passage : RoomElementBase
    {
        [SerializeField] private PassageDefinition definition;
        [SerializeField] private RoomView sourceRoomView;
        [SerializeField] private Passage reversePassage;
        [SerializeField] private PassageAnchorData approachAnchor;
        [SerializeField] private PassageAnchorData arrivalAnchor;
        [SerializeField] private PassageAnchorMigrationStage anchorMigrationStage = PassageAnchorMigrationStage.LegacySampling;

        public PassageDefinition Definition => definition;
        public RoomView SourceRoomView => sourceRoomView;
        public Passage ReversePassage => reversePassage;
        public PassageAnchorMigrationStage AnchorMigrationStage => anchorMigrationStage;
        public bool HasValidAnchorMigrationStage =>
            anchorMigrationStage == PassageAnchorMigrationStage.LegacySampling ||
            anchorMigrationStage == PassageAnchorMigrationStage.AuthoredArrival ||
            anchorMigrationStage == PassageAnchorMigrationStage.AuthoredAnchors;
        public bool UsesAuthoredArrival =>
            anchorMigrationStage == PassageAnchorMigrationStage.AuthoredArrival ||
            anchorMigrationStage == PassageAnchorMigrationStage.AuthoredAnchors;
        public bool UsesAuthoredApproach =>
            anchorMigrationStage == PassageAnchorMigrationStage.AuthoredAnchors;
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

                if (!reversePassage.HasValidAnchorMigrationStage)
                {
                    report.AddError("Passage reverse has an unknown anchor migration stage.", this);
                }
                else if (reversePassage.anchorMigrationStage != anchorMigrationStage)
                {
                    report.AddError("Passage reciprocal pair must share one anchor migration stage.", this);
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

            if (!HasValidAnchorMigrationStage)
            {
                report.AddError("Passage has an unknown anchor migration stage.", this);
            }

            if (approachAnchor == null)
            {
                report.AddError("Passage requires authored approach data.", this);
            }
            else if (!approachAnchor.HasValidCoordinateSpace)
            {
                report.AddError("Passage approach has an unknown coordinate space.", this);
            }
            else if (UsesAuthoredApproach && !approachAnchor.HasFiniteAuthoredPosition)
            {
                report.AddError("Passage approach requires a finite authored position.", this);
            }

            if (arrivalAnchor == null)
            {
                report.AddError("Passage requires authored arrival data.", this);
            }
            else if (!arrivalAnchor.HasValidCoordinateSpace)
            {
                report.AddError("Passage arrival has an unknown coordinate space.", this);
            }
            else if (UsesAuthoredArrival && !arrivalAnchor.HasFiniteAuthoredPosition)
            {
                report.AddError("Passage arrival requires a finite authored position.", this);
            }
        }
    }
}
