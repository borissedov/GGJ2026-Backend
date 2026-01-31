namespace OhMyHungryGod.Server.Models.Events;

public record CountdownStartedEvent(
    Guid RoomId,
    DateTime StartsAt,
    int DurationSeconds
);
