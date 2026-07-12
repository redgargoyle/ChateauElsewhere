using Chateau.World.Rooms.Passages;

namespace Chateau.World.Navigation
{
    public interface INavigationService
    {
        global::Chateau.World.Rooms.RoomDefinition CurrentRoomDefinition { get; }

        bool CanTraverse(Passage passage);
        bool TryTraverse(Passage passage);
    }
}
