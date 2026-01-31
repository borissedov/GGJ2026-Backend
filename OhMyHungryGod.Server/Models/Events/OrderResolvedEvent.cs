namespace OhMyHungryGod.Server.Models.Events;

public record OrderResolvedEvent(
    Guid OrderId,
    OrderStatus Result,
    Dictionary<FruitType, int> Required,
    Dictionary<FruitType, int> Submitted,
    GodMood NewMood
);
