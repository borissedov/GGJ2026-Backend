namespace OhMyHungryGod.Server.Models.Events;

public record GameStartedEvent(
    Guid RoomId,
    DateTime StartedAt
);
