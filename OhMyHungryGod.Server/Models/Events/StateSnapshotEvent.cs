namespace OhMyHungryGod.Server.Models.Events;

public record StateSnapshotEvent(
    Guid RoomId,
    RoomState State,
    GodMood Mood,
    Order? CurrentOrder,
    int OrderIndex,
    DateTime? OrderEndsAt,
    Player[] Players
);
