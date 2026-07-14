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

    public enum PassageArrivalPlacementMode
    {
        ExactAuthoredPoint = 0,
        BestReachableInAuthoredRegion = 1
    }

    public enum PassageApproachPlacementMode
    {
        ExactAuthoredPoint = 0,
        BestReachableInSourceRegion = 1
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
        [SerializeField] private PassageApproachPlacementMode approachPlacementMode =
            PassageApproachPlacementMode.ExactAuthoredPoint;
        [SerializeField] private PassageArrivalPlacementMode arrivalPlacementMode =
            PassageArrivalPlacementMode.ExactAuthoredPoint;
        [SerializeField] private PassageArrivalRegionData arrivalRegion;

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
        public PassageApproachPlacementMode ApproachPlacementMode => approachPlacementMode;
        public bool HasValidApproachPlacementMode =>
            approachPlacementMode == PassageApproachPlacementMode.ExactAuthoredPoint ||
            approachPlacementMode == PassageApproachPlacementMode.BestReachableInSourceRegion;
        public bool UsesBestReachableApproachRegion =>
            approachPlacementMode == PassageApproachPlacementMode.BestReachableInSourceRegion;
        public PassageArrivalRegionData ApproachRegion =>
            UsesBestReachableApproachRegion && reversePassage != null
                ? reversePassage.ArrivalRegion
                : null;
        public bool HasMatchingApproachRegionGeometry =>
            anchorMigrationStage == PassageAnchorMigrationStage.AuthoredAnchors &&
            UsesBestReachableApproachRegion &&
            reversePassage != null &&
            reversePassage != this &&
            reversePassage.reversePassage == this &&
            reversePassage.anchorMigrationStage == PassageAnchorMigrationStage.AuthoredAnchors &&
            reversePassage.UsesBestReachableArrivalRegion &&
            PassageArrivalResolver.DoesAuthoredRegionMatchTransform(
                reversePassage.ArrivalRegion,
                sourceRoomView,
                transform as RectTransform);
        public PassageArrivalPlacementMode ArrivalPlacementMode => arrivalPlacementMode;
        public bool HasValidArrivalPlacementMode =>
            arrivalPlacementMode == PassageArrivalPlacementMode.ExactAuthoredPoint ||
            arrivalPlacementMode == PassageArrivalPlacementMode.BestReachableInAuthoredRegion;
        public bool UsesBestReachableArrivalRegion =>
            arrivalPlacementMode == PassageArrivalPlacementMode.BestReachableInAuthoredRegion;
        public PassageArrivalRegionData ArrivalRegion => arrivalRegion;
        public PassageAnchorData ApproachAnchor => approachAnchor;
        public PassageAnchorData ArrivalAnchor => arrivalAnchor;

        public bool TryBuildApproachRuntimeRegion(
            Camera canvasCamera,
            out PassageArrivalRuntimeRegion runtimeRegion)
        {
            runtimeRegion = default;

            return HasMatchingApproachRegionGeometry &&
                PassageArrivalResolver.TryBuildRuntimeRegion(
                    reversePassage.ArrivalRegion,
                    sourceRoomView,
                    transform as RectTransform,
                    canvasCamera,
                    out runtimeRegion);
        }

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

                if (!reversePassage.HasValidArrivalPlacementMode)
                {
                    report.AddError("Passage reverse has an unknown arrival placement mode.", this);
                }

                if (!reversePassage.HasValidApproachPlacementMode)
                {
                    report.AddError("Passage reverse has an unknown approach placement mode.", this);
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

            if (!HasValidApproachPlacementMode)
            {
                report.AddError("Passage has an unknown approach placement mode.", this);
            }
            else if (UsesBestReachableApproachRegion)
            {
                if (anchorMigrationStage != PassageAnchorMigrationStage.AuthoredAnchors)
                {
                    report.AddError(
                        "Passage best-reachable source approach region requires fully authored anchors.",
                        this);
                }

                if (reversePassage == null || !reversePassage.UsesBestReachableArrivalRegion)
                {
                    report.AddError(
                        "Passage best-reachable source approach region requires its reverse " +
                        "Passage to own a best-reachable arrival region.",
                        this);
                }
                else if (reversePassage.ArrivalRegion == null ||
                    !reversePassage.ArrivalRegion.HasValidRoomViewLocalCorners)
                {
                    report.AddError(
                        "Passage best-reachable source approach region requires finite, " +
                        "nondegenerate reciprocal RoomView-local corners.",
                        this);
                }

                if (!(transform is RectTransform))
                {
                    report.AddError(
                        "Passage best-reachable source approach region requires its own RectTransform.",
                        this);
                }
                else if (reversePassage != null &&
                    reversePassage.UsesBestReachableArrivalRegion &&
                    reversePassage.ArrivalRegion != null &&
                    reversePassage.ArrivalRegion.HasValidRoomViewLocalCorners &&
                    !HasMatchingApproachRegionGeometry)
                {
                    report.AddError(
                        "Passage best-reachable source approach region must match its own " +
                        "RectTransform in the source RoomView.",
                        this);
                }
            }

            if (!HasValidArrivalPlacementMode)
            {
                report.AddError("Passage has an unknown arrival placement mode.", this);
            }
            else if (UsesBestReachableArrivalRegion)
            {
                if (anchorMigrationStage != PassageAnchorMigrationStage.AuthoredAnchors)
                {
                    report.AddError(
                        "Passage best-reachable arrival region requires fully authored anchors.",
                        this);
                }

                if (arrivalRegion == null)
                {
                    report.AddError("Passage requires its authored RoomView-local arrival region.", this);
                }
                else if (!arrivalRegion.HasValidRoomViewLocalCorners)
                {
                    report.AddError(
                        "Passage authored RoomView-local arrival region requires finite, " +
                        "nondegenerate clockwise bottom-left/top-left/top-right/bottom-right corners.",
                        this);
                }

                if (reversePassage == null || reversePassage.SourceRoomView == null)
                {
                    report.AddError(
                        "Passage best-reachable arrival region requires its destination " +
                        "through the reverse Passage source RoomView.",
                        this);
                }
            }

            if (!UsesBestReachableApproachRegion)
            {
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
            }

            if (!UsesBestReachableArrivalRegion)
            {
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
}
