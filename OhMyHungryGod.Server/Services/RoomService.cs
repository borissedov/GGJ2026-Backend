using OhMyHungryGod.Server.Models;
using OhMyHungryGod.Server.State;

namespace OhMyHungryGod.Server.Services;

public class RoomService
{
    private readonly InMemoryRoomStore _store;
    private const int CountdownSeconds = 6;
    private const int OrderSeconds = 10;
    private const int ResultsTimeoutSeconds = 30;
    private const int RoomInactivityTimeoutMinutes = 5;
    
    public RoomService(InMemoryRoomStore store)
    {
        _store = store;
    }
    
    public Room CreateRoom()
    {
        var joinCode = GenerateJoinCode();
        var room = _store.CreateRoom(joinCode);
        room.State = RoomState.Welcome;
        return room;
    }
    
    public Room? GetRoomByJoinCode(string joinCode)
    {
        return _store.GetRoomByJoinCode(joinCode);
    }
    
    public Room? GetRoomById(Guid roomId)
    {
        return _store.GetRoomById(roomId);
    }
    
    public void AddPlayer(Room room, Player player)
    {
        room.Players[player.PlayerId] = player;
        room.LastActivityAt = DateTime.UtcNow;
        
        // Transition from Welcome to Lobby when first player joins
        if (room.State == RoomState.Welcome && room.Players.Count > 0)
        {
            room.State = RoomState.Lobby;
        }
    }
    
    public void RemovePlayer(Room room, Guid playerId)
    {
        room.Players.Remove(playerId);
        room.LastActivityAt = DateTime.UtcNow;
    }
    
    public void UpdatePlayerConnection(Room room, Guid playerId, string connectionId, bool connected)
    {
        if (room.Players.TryGetValue(playerId, out var player))
        {
            player.ConnectionId = connectionId;
            player.IsConnected = connected;
            player.LastPingAt = DateTime.UtcNow;
            room.LastActivityAt = DateTime.UtcNow;
        }
    }
    
    public void CleanupInactiveRooms()
    {
        var now = DateTime.UtcNow;
        var roomsToRemove = new List<Guid>();
        
        foreach (var room in _store.GetAllRooms())
        {
            // Remove rooms in Results state that have timed out
            if (room.State == RoomState.Results)
            {
                var resultsDuration = (now - room.LastActivityAt).TotalSeconds;
                if (resultsDuration > ResultsTimeoutSeconds)
                {
                    roomsToRemove.Add(room.RoomId);
                    continue;
                }
            }
            
            // Remove rooms with no connected players and inactive for 5+ minutes
            var hasConnectedPlayers = room.Players.Values.Any(p => p.IsConnected);
            if (!hasConnectedPlayers)
            {
                var inactiveDuration = (now - room.LastActivityAt).TotalMinutes;
                if (inactiveDuration > RoomInactivityTimeoutMinutes)
                {
                    roomsToRemove.Add(room.RoomId);
                }
            }
        }
        
        foreach (var roomId in roomsToRemove)
        {
            _store.RemoveRoom(roomId);
        }
    }
    
    private string GenerateJoinCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";  // No ambiguous chars
        var random = new Random();
        var joinCode = string.Empty;
        
        // Keep generating until we get a unique code
        do
        {
            joinCode = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());
        } while (_store.GetRoomByJoinCode(joinCode) != null);
        
        return joinCode;
    }
    
    public int GetCountdownSeconds() => CountdownSeconds;
    public int GetOrderSeconds() => OrderSeconds;
}
