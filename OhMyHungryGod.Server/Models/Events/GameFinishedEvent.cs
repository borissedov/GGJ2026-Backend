namespace OhMyHungryGod.Server.Models.Events;

public record PlayerStats(
    string Name,
    int HitCount,
    double ContributionPercentage
);

public record GameFinishedEvent(
    Guid RoomId,
    int TotalOrders,
    int SuccessCount,
    int FailCount,
    GodMood FinalMood,
    PlayerStats[] PlayerStats
);
