using System.Collections.Concurrent;
using OhMyHungryGod.Server.Models;

namespace OhMyHungryGod.Server.State;

public class InMemoryRoomStore
{
    private readonly ConcurrentDictionary<Guid, Room> _roomsById = new();
    private readonly ConcurrentDictionary<string, Guid> _joinCodeToRoomId = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, bool>> _processedHits = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionToRoomId = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionToPlayerId = new();
    
    public Room CreateRoom(string joinCode)
    {
        var room = new Room { JoinCode = joinCode };
        _roomsById[room.RoomId] = room;
        _joinCodeToRoomId[joinCode] = room.RoomId;
        _processedHits[room.RoomId] = new ConcurrentDictionary<Guid, bool>();
        return room;
    }
    
    public Room? GetRoomById(Guid roomId)
    {
        return _roomsById.TryGetValue(roomId, out var room) ? room : null;
    }
    
    public Room? GetRoomByJoinCode(string joinCode)
    {
        if (_joinCodeToRoomId.TryGetValue(joinCode, out var roomId))
        {
            return GetRoomById(roomId);
        }
        return null;
    }
    
    public IEnumerable<Room> GetAllRooms()
    {
        return _roomsById.Values;
    }
    
    public bool RemoveRoom(Guid roomId)
    {
        if (_roomsById.TryRemove(roomId, out var room))
        {
            _joinCodeToRoomId.TryRemove(room.JoinCode, out _);
            _processedHits.TryRemove(roomId, out _);
            return true;
        }
        return false;
    }
    
    public bool IsHitProcessed(Guid roomId, Guid hitId)
    {
        if (_processedHits.TryGetValue(roomId, out var hits))
        {
            return !hits.TryAdd(hitId, true);
        }
        return false;
    }
    
    public void ClearProcessedHits(Guid roomId)
    {
        if (_processedHits.TryGetValue(roomId, out var hits))
        {
            hits.Clear();
        }
    }
    
    // Connection mapping methods
    public void MapConnectionToRoom(string connectionId, Guid roomId)
    {
        _connectionToRoomId[connectionId] = roomId;
    }
    
    public void MapConnectionToPlayer(string connectionId, Guid playerId)
    {
        _connectionToPlayerId[connectionId] = playerId;
    }
    
    public bool TryGetRoomIdForConnection(string connectionId, out Guid roomId)
    {
        return _connectionToRoomId.TryGetValue(connectionId, out roomId);
    }
    
    public bool TryGetPlayerIdForConnection(string connectionId, out Guid playerId)
    {
        return _connectionToPlayerId.TryGetValue(connectionId, out playerId);
    }
    
    public void RemoveConnectionMappings(string connectionId)
    {
        _connectionToRoomId.TryRemove(connectionId, out _);
        _connectionToPlayerId.TryRemove(connectionId, out _);
    }
}
