public interface ICharacterDisplayScaleContext
{
    string CurrentRoomId { get; }
    float CurrentRoomLocalFootY { get; }
    CharacterDisplayState CurrentDisplayState { get; }
}
