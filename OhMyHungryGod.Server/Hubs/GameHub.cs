using Microsoft.AspNetCore.SignalR;
using OhMyHungryGod.Server.Models;
using OhMyHungryGod.Server.Models.Events;
using OhMyHungryGod.Server.Services;

namespace OhMyHungryGod.Server.Hubs;

public class GameHub : Hub
{
    private readonly RoomService _roomService;
    private readonly GameEngineService _gameEngine;
    private readonly Dictionary<string, Guid> _connectionToRoomId = new();
    private readonly Dictionary<string, Guid> _connectionToPlayerId = new();
    
    public GameHub(RoomService roomService, GameEngineService gameEngine)
    {
        _roomService = roomService;
        _gameEngine = gameEngine;
    }
    
    // Display client method
    public async Task<CreateRoomResponse> CreateRoom()
    {
        var room = _roomService.CreateRoom();
        
        // Add display connection to room group
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId.ToString());
        _connectionToRoomId[Context.ConnectionId] = room.RoomId;
        
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
        
        // Add player to room group
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId.ToString());
        _connectionToRoomId[Context.ConnectionId] = room.RoomId;
        _connectionToPlayerId[Context.ConnectionId] = player.PlayerId;
        
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
        if (room == null) return;
        
        if (!_connectionToPlayerId.TryGetValue(Context.ConnectionId, out var playerId))
            return;
        
        if (!room.Players.TryGetValue(playerId, out var player))
            return;
        
        var wasReady = player.IsReady;
        player.IsReady = ready;
        
        await BroadcastRoomStateUpdate(room);
        
        // Check if we should start or cancel countdown
        if (room.State == RoomState.Lobby && ready && AreAllPlayersReady(room))
        {
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
        
        if (_connectionToPlayerId.TryGetValue(Context.ConnectionId, out var playerId))
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
        
        if (_connectionToPlayerId.TryGetValue(Context.ConnectionId, out var playerId))
        {
            _roomService.RemovePlayer(room, playerId);
            _connectionToPlayerId.Remove(Context.ConnectionId);
            
            await BroadcastRoomStateUpdate(room);
            
            // Cancel countdown if we were in countdown and someone left
            if (room.State == RoomState.Countdown)
            {
                await _gameEngine.CancelCountdown(room);
            }
        }
        
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId.ToString());
        _connectionToRoomId.Remove(Context.ConnectionId);
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connectionToRoomId.TryGetValue(Context.ConnectionId, out var roomId))
        {
            var room = _roomService.GetRoomById(roomId);
            if (room != null && _connectionToPlayerId.TryGetValue(Context.ConnectionId, out var playerId))
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
