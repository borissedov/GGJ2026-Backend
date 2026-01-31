namespace OhMyHungryGod.Server.Models;

public record CreateRoomResponse(
    Guid RoomId,
    string JoinCode
);
