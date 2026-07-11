using Chateau.Architecture;
using UnityEngine;

namespace Chateau.World.Rooms.Props
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Chateau/World/Rooms/Props/Set Piece View")]
    public sealed class SetPieceView : RoomElementBase
    {
        [SerializeField] private SpriteRenderer cutoutRenderer;
        [SerializeField] private global::RoomPerspectiveProfile depthProfile;
        [SerializeField] private Vector2 roomLocalOcclusionAnchor;
        [SerializeField] private int sortingOffset;

        public SpriteRenderer CutoutRenderer => cutoutRenderer;
        public global::RoomPerspectiveProfile DepthProfile => depthProfile;
        public Vector2 RoomLocalOcclusionAnchor => roomLocalOcclusionAnchor;
        public int SortingOffset => sortingOffset;
        public int CurrentSortingOrder { get; private set; }

        private void OnEnable()
        {
            ApplyPresentation();
        }

        private void OnValidate()
        {
            ApplyPresentation();
        }

        protected override void OnGameContextBound(GameContext context)
        {
            ApplyPresentation();
        }

        public void Configure(
            SpriteRenderer renderer,
            global::RoomPerspectiveProfile profile,
            Vector2 occlusionAnchor,
            int offset = 0)
        {
            cutoutRenderer = renderer;
            depthProfile = profile;
            roomLocalOcclusionAnchor = occlusionAnchor;
            sortingOffset = offset;
            ApplyPresentation();
        }

        public bool ApplyPresentation()
        {
            if (cutoutRenderer == null || depthProfile == null)
            {
                return false;
            }

            CurrentSortingOrder = RoomDepthResolver.Resolve(
                depthProfile,
                roomLocalOcclusionAnchor,
                sortingOffset);
            cutoutRenderer.sortingLayerName = depthProfile.SortingLayerName;
            cutoutRenderer.sortingOrder = CurrentSortingOrder;
            cutoutRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            return true;
        }

        public override void ValidateConfiguration(ValidationReport report)
        {
            base.ValidateConfiguration(report);

            if (cutoutRenderer == null)
            {
                report.AddError("SetPieceView requires its cutout SpriteRenderer.", this);
            }
            else if (cutoutRenderer.transform != transform && !cutoutRenderer.transform.IsChildOf(transform))
            {
                report.AddError("SetPieceView must own its cutout SpriteRenderer in the same prop hierarchy.", this);
            }

            if (depthProfile == null)
            {
                report.AddError("SetPieceView requires a room depth profile.", this);
            }
        }
    }
}
