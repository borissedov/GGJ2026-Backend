namespace OhMyHungryGod.Server.Models.Events;

public record GameOverEvent(
    Guid RoomId,
    string Reason,
    int CompletedOrders,
    int SuccessCount,
    int FailCount
);
