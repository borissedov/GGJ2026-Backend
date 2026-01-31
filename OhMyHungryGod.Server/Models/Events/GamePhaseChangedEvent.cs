namespace OhMyHungryGod.Server.Models.Events;

public record GamePhaseChangedEvent(
    Guid RoomId,
    RoomState OldState,
    RoomState NewState
);
