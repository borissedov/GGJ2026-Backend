using Microsoft.AspNetCore.SignalR;
using OhMyHungryGod.Server.Models;
using OhMyHungryGod.Server.Models.Events;
using OhMyHungryGod.Server.Services;
using OhMyHungryGod.Server.State;

namespace OhMyHungryGod.Server.Hubs;

public class GameHub : Hub
{
    private readonly RoomService _roomService;
    private readonly GameEngineService _gameEngine;
    private readonly InMemoryRoomStore _store;
    
    public GameHub(RoomService roomService, GameEngineService gameEngine, InMemoryRoomStore store)
    {
        _roomService = roomService;
        _gameEngine = gameEngine;
        _store = store;
    }
    
    // Display client method
    public async Task<CreateRoomResponse> CreateRoom()
    {
        var room = _roomService.CreateRoom();
        
        // Add display connection to room group
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId.ToString());
        _store.MapConnectionToRoom(Context.ConnectionId, room.RoomId);
        
        room.DisplayClientId = Guid.NewGuid();
        
        return new CreateRoomResponse(room.RoomId, room.JoinCode);
    }
    
    // Player method
    public async Task<JoinResponse> JoinRoom(string joinCode)
    {
        var room = _roomService.GetRoomByJoinCode(joinCode);
        
        if (room == null)
        {
            await Clients.Caller.SendAsync("Error", new ErrorEvent("ROOM_NOT_FOUND", "Room not found"));
            throw new HubException("Room not found");
        }
        
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            ConnectionId = Context.ConnectionId,
            IsConnected = true,
            IsReady = false
        };
        
        _roomService.AddPlayer(room, player);
        
        // Add player to room group and track mappings
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId.ToString());
        _store.MapConnectionToRoom(Context.ConnectionId, room.RoomId);
        _store.MapConnectionToPlayer(Context.ConnectionId, player.PlayerId);
        
        // Transition to Lobby if this is the first player
        if (room.State == RoomState.Welcome)
        {
            await _gameEngine.TransitionToLobby(room);
        }
        else
        {
            // Broadcast room state update to all clients
            await BroadcastRoomStateUpdate(room);
        }
        
        // Send state snapshot to the new player
        await _gameEngine.SendStateSnapshot(room, Context.ConnectionId);
        
        return new JoinResponse(room.RoomId, player.PlayerId);
    }
    
    public async Task SetReady(Guid roomId, bool ready)
    {
        var room = _roomService.GetRoomById(roomId);
        if (room == null)
        {
            Console.WriteLine($"❌ SetReady: Room {roomId} not found");
            return;
        }
        
        if (!_store.TryGetPlayerIdForConnection(Context.ConnectionId, out var playerId))
        {
            Console.WriteLine($"❌ SetReady: No player ID found for connection {Context.ConnectionId}");
            return;
        }
        
        if (!room.Players.TryGetValue(playerId, out var player))
        {
            Console.WriteLine($"❌ SetReady: Player {playerId} not found in room {roomId}");
            return;
        }
        
        Console.WriteLine($"✅ SetReady: Player {playerId} marking ready={ready}");
        var wasReady = player.IsReady;
        player.IsReady = ready;
        
        await BroadcastRoomStateUpdate(room);
        
        // Check if we should start or cancel countdown
        if (room.State == RoomState.Lobby && ready && AreAllPlayersReady(room))
        {
            Console.WriteLine($"🎯 All players ready! Starting countdown for room {roomId}");
            await _gameEngine.StartCountdown(room);
        }
        else if (room.State == RoomState.Countdown && (!ready || !AreAllPlayersReady(room)))
        {
            await _gameEngine.CancelCountdown(room);
        }
    }
    
    public async Task ReportHit(Guid roomId, Guid hitId, string fruitTypeName)
    {
        if (!Enum.TryParse<FruitType>(fruitTypeName, true, out var fruitType))
        {
            await Clients.Caller.SendAsync("Error", new ErrorEvent("INVALID_FRUIT", "Invalid fruit type"));
            return;
        }
        
        var result = await _gameEngine.ProcessHit(roomId, hitId, fruitType);
        
        // Optionally send feedback to caller about hit result
        // (already handled by game engine broadcasts)
    }
    
    public async Task Ping(Guid roomId)
    {
        var room = _roomService.GetRoomById(roomId);
        if (room == null) return;
        
        if (_store.TryGetPlayerIdForConnection(Context.ConnectionId, out var playerId))
        {
            if (room.Players.TryGetValue(playerId, out var player))
            {
                player.LastPingAt = DateTime.UtcNow;
                room.LastActivityAt = DateTime.UtcNow;
            }
        }
    }
    
    public async Task LeaveRoom(Guid roomId)
    {
        var room = _roomService.GetRoomById(roomId);
        if (room == null) return;
        
        if (_store.TryGetPlayerIdForConnection(Context.ConnectionId, out var playerId))
        {
            _roomService.RemovePlayer(room, playerId);
            
            await BroadcastRoomStateUpdate(room);
            
            // Cancel countdown if we were in countdown and someone left
            if (room.State == RoomState.Countdown)
            {
                await _gameEngine.CancelCountdown(room);
            }
        }
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId.ToString());
        _store.RemoveConnectionMappings(Context.ConnectionId);
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_store.TryGetRoomIdForConnection(Context.ConnectionId, out var roomId))
        {
            var room = _roomService.GetRoomById(roomId);
            if (room != null && _store.TryGetPlayerIdForConnection(Context.ConnectionId, out var playerId))
            {
                // Mark player as disconnected but don't remove them
                if (room.Players.TryGetValue(playerId, out var player))
                {
                    player.IsConnected = false;
                    player.IsReady = false;
                }
                
                await BroadcastRoomStateUpdate(room);
                
                // Cancel countdown if we were in countdown
                if (room.State == RoomState.Countdown)
                {
                    await _gameEngine.CancelCountdown(room);
                }
            }
        }
        
        _store.RemoveConnectionMappings(Context.ConnectionId);
        
        await base.OnDisconnectedAsync(exception);
    }
    
    private async Task BroadcastRoomStateUpdate(Room room)
    {
        var connectedPlayers = room.Players.Values.Where(p => p.IsConnected).ToList();
        
        var roomStateEvent = new RoomStateUpdatedEvent(
            room.RoomId,
            room.State,
            room.Players.Values.ToArray(),
            connectedPlayers.Count,
            connectedPlayers.Count(p => p.IsReady)
        );
        
        await Clients.Group(room.RoomId.ToString()).SendAsync("RoomStateUpdated", roomStateEvent);
    }
    
    private bool AreAllPlayersReady(Room room)
    {
        var connectedPlayers = room.Players.Values.Where(p => p.IsConnected).ToList();
        if (connectedPlayers.Count == 0) return false;
        
        return connectedPlayers.All(p => p.IsReady);
    }
}
