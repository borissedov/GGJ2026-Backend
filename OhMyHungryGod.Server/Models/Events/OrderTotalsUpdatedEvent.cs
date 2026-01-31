namespace OhMyHungryGod.Server.Models.Events;

public record OrderTotalsUpdatedEvent(
    Guid OrderId,
    Dictionary<FruitType, int> Submitted,
    DateTime Timestamp
);
