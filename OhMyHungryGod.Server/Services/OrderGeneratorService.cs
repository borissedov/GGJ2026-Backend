using System.Linq;
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
        
        // Randomly assign required fruits with total limit of 5
        var fruitTypes = Enum.GetValues<FruitType>().ToArray();
        var numFruitTypes = _random.Next(1, fruitTypes.Length + 1); // 1-4 different fruits
        
        var selectedFruits = fruitTypes.OrderBy(_ => _random.Next()).Take(numFruitTypes).ToList();
        
        // Total fruits to distribute (2-5) - minimum 2 to make it interesting
        var totalFruits = _random.Next(2, 6);
        
        // Distribute fruits among selected types
        for (int i = 0; i < totalFruits; i++)
        {
            var randomFruit = selectedFruits[_random.Next(selectedFruits.Count)];
            order.Required[randomFruit]++;
        }
        
        // Safety check - ensure at least one fruit is required
        if (order.Required.Values.Sum() == 0)
        {
            var randomFruit = fruitTypes[_random.Next(fruitTypes.Length)];
            order.Required[randomFruit] = 2;
        }
        
        return order;
    }
}
