namespace OhMyHungryGod.Server.Models;

public class Player
{
    public Guid PlayerId { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool IsReady { get; set; }
    public int HitCount { get; set; } = 0;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastPingAt { get; set; }
}
