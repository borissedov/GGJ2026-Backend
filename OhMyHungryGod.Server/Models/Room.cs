namespace OhMyHungryGod.Server.Models;

public class Room
{
    public Guid RoomId { get; init; } = Guid.NewGuid();
    public string JoinCode { get; set; } = string.Empty;
    public RoomState State { get; set; } = RoomState.Welcome;
    public Dictionary<Guid, Player> Players { get; set; } = new();
    public Guid? DisplayClientId { get; set; }
    
    // Game-specific
    public GodMood Mood { get; set; } = GodMood.Neutral;
    public Order? CurrentOrder { get; set; }
    public int OrderIndex { get; set; }
    public DateTime? OrderEndsAt { get; set; }
    public DateTime? CountdownStartedAt { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
