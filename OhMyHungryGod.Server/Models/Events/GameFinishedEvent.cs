namespace OhMyHungryGod.Server.Models.Events;

public record GameFinishedEvent(
    Guid RoomId,
    int TotalOrders,
    int SuccessCount,
    int FailCount,
    GodMood FinalMood
);
