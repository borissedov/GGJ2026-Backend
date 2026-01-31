namespace OhMyHungryGod.Server.Models.Events;

public record OrderStartedEvent(
    Guid OrderId,
    int OrderNumber,
    Dictionary<FruitType, int> Required,
    DateTime EndsAt,
    int DurationSeconds
);
