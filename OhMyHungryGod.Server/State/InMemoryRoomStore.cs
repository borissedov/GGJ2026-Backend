using System.Collections.Concurrent;
using OhMyHungryGod.Server.Models;

namespace OhMyHungryGod.Server.State;

public class InMemoryRoomStore
{
    private readonly ConcurrentDictionary<Guid, Room> _roomsById = new();
    private readonly ConcurrentDictionary<string, Guid> _joinCodeToRoomId = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, bool>> _processedHits = new();
    
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
}
