namespace OhMyHungryGod.Server.Models;

public class Order
{
    public Guid OrderId { get; init; } = Guid.NewGuid();
    public Dictionary<FruitType, int> Required { get; set; } = new();
    public Dictionary<FruitType, int> Submitted { get; set; } = new();
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Active;
}
