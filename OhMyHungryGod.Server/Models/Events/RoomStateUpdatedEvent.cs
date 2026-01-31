namespace OhMyHungryGod.Server.Models.Events;

public record RoomStateUpdatedEvent(
    Guid RoomId,
    RoomState State,
    Player[] Players,
    int ConnectedCount,
    int ReadyCount
);
