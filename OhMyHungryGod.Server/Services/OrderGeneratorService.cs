using OhMyHungryGod.Server.Models;

namespace OhMyHungryGod.Server.Services;

public class OrderGeneratorService
{
    private readonly Random _random = new();
    
    public Order GenerateOrder(int orderDurationSeconds)
    {
        var order = new Order
        {
            Required = new Dictionary<FruitType, int>(),
            Submitted = new Dictionary<FruitType, int>(),
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddSeconds(orderDurationSeconds)
        };
        
        // Initialize all fruit types to 0
        foreach (FruitType fruit in Enum.GetValues<FruitType>())
        {
            order.Required[fruit] = 0;
            order.Submitted[fruit] = 0;
        }
        
        // Randomly assign required fruits (at least one fruit type must be > 0)
        var fruitTypes = Enum.GetValues<FruitType>().ToArray();
        var numFruitTypes = _random.Next(1, fruitTypes.Length + 1); // 1-4 different fruits
        
        var selectedFruits = fruitTypes.OrderBy(_ => _random.Next()).Take(numFruitTypes);
        
        foreach (var fruit in selectedFruits)
        {
            order.Required[fruit] = _random.Next(1, 6); // 1-5 of each selected fruit
        }
        
        return order;
    }
}
