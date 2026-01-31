namespace OhMyHungryGod.Server.Models;

public record JoinResponse(
    Guid RoomId,
    Guid PlayerId
);
