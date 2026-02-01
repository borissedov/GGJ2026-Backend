using Microsoft.AspNetCore.SignalR;
using OhMyHungryGod.Server.Hubs;
using OhMyHungryGod.Server.Models;
using OhMyHungryGod.Server.Models.Events;
using OhMyHungryGod.Server.State;

namespace OhMyHungryGod.Server.Services;

public class GameEngineService
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly InMemoryRoomStore _store;
    private readonly RoomService _roomService;
    private readonly OrderGeneratorService _orderGenerator;
    private readonly MoodCalculatorService _moodCalculator;
    private const int OrdersPerGame = 10;
    
    public GameEngineService(
        IHubContext<GameHub> hubContext,
        InMemoryRoomStore store,
        RoomService roomService,
        OrderGeneratorService orderGenerator,
        MoodCalculatorService moodCalculator)
    {
        _hubContext = hubContext;
        _store = store;
        _roomService = roomService;
        _orderGenerator = orderGenerator;
        _moodCalculator = moodCalculator;
    }
    
    public async Task TransitionToLobby(Room room)
    {
        room.State = RoomState.Lobby;
        await BroadcastRoomStateUpdated(room);
    }
    
    public async Task StartCountdown(Room room)
    {
        if (!AreAllPlayersReady(room))
            return;
        
        room.State = RoomState.Countdown;
        room.CountdownStartedAt = DateTime.UtcNow;
        
        var countdownEvent = new CountdownStartedEvent(
            room.RoomId,
            room.CountdownStartedAt.Value,
            _roomService.GetCountdownSeconds()
        );
        
        await _hubContext.Clients.Group(room.RoomId.ToString())
            .SendAsync("CountdownStarted", countdownEvent);
        
        await BroadcastRoomStateUpdated(room);
    }
    
    public async Task CancelCountdown(Room room)
    {
        if (room.State != RoomState.Countdown)
            return;
        
        room.State = RoomState.Lobby;
        room.CountdownStartedAt = null;
        
        await _hubContext.Clients.Group(room.RoomId.ToString())
            .SendAsync("CountdownCancelled");
        
        await BroadcastRoomStateUpdated(room);
    }
    
    public async Task StartGame(Room room)
    {
        room.State = RoomState.InGame;
        room.OrderIndex = 0;
        room.SuccessCount = 0;
        room.FailCount = 0;
        room.Mood = GodMood.Neutral;
        
        var gameStartedEvent = new GameStartedEvent(room.RoomId, DateTime.UtcNow);
        
        await _hubContext.Clients.Group(room.RoomId.ToString())
            .SendAsync("GameStarted", gameStartedEvent);
        
        // Start first order
        await StartNextOrder(room);
    }
    
    public async Task<HitResult> ProcessHit(Guid roomId, Guid hitId, FruitType fruit, Guid? playerId)
    {
        var room = _store.GetRoomById(roomId);
        if (room == null) return HitResult.InvalidState;
        
        // Validation
        if (room.State != RoomState.InGame) return HitResult.InvalidState;
        if (room.CurrentOrder == null) return HitResult.NoActiveOrder;
        
        // Idempotency check
        if (_store.IsHitProcessed(roomId, hitId))
            return HitResult.AlreadyProcessed;
        
        // Track player stats
        if (playerId.HasValue && room.Players.TryGetValue(playerId.Value, out var player))
        {
            player.HitCount++;
        }
        
        // Increment submitted count
        room.CurrentOrder.Submitted[fruit]++;
        room.LastActivityAt = DateTime.UtcNow;
        
        // Immediate failure check
        if (room.CurrentOrder.Submitted[fruit] > room.CurrentOrder.Required[fruit])
        {
            await ResolveOrder(room, OrderStatus.FailOver);
            return HitResult.OrderFailedImmediate;
        }
        
        // Immediate success check
        if (IsOrderComplete(room.CurrentOrder))
        {
            await ResolveOrder(room, OrderStatus.SuccessExact);
            return HitResult.OrderSuccessImmediate;
        }
        
        // Broadcast updated totals
        await BroadcastOrderTotals(room);
        return HitResult.Counted;
    }
    
    public async Task CheckOrderTimeout(Room room)
    {
        if (room.State != RoomState.InGame || room.CurrentOrder == null)
            return;
        
        if (DateTime.UtcNow >= room.CurrentOrder.EndsAt && room.CurrentOrder.Status == OrderStatus.Active)
        {
            await ResolveOrder(room, OrderStatus.FailTimeout);
        }
    }
    
    public async Task ResolveOrder(Room room, OrderStatus status)
    {
        if (room.CurrentOrder == null) return;
        
        room.CurrentOrder.Status = status;
        
        // Update success/fail counts
        if (status == OrderStatus.SuccessExact)
            room.SuccessCount++;
        else
            room.FailCount++;
        
        // Calculate new mood
        var oldMood = room.Mood;
        room.Mood = _moodCalculator.CalculateNewMood(GodMood.Neutral, room.SuccessCount, room.FailCount);
        
        // Broadcast order resolved event
        var orderResolvedEvent = new OrderResolvedEvent(
            room.CurrentOrder.OrderId,
            status,
            room.CurrentOrder.Required,
            room.CurrentOrder.Submitted,
            room.Mood
        );
        
        await _hubContext.Clients.Group(room.RoomId.ToString())
            .SendAsync("OrderResolved", orderResolvedEvent);
        
        // Broadcast mood change if it changed
        if (oldMood != room.Mood)
        {
            var moodChangedEvent = new MoodChangedEvent(room.RoomId, oldMood, room.Mood);
            await _hubContext.Clients.Group(room.RoomId.ToString())
                .SendAsync("MoodChanged", moodChangedEvent);
        }
        
        // Clear processed hits for next order
        _store.ClearProcessedHits(room.RoomId);
        
        // Check if game is complete
        room.OrderIndex++;
        if (room.OrderIndex >= OrdersPerGame)
        {
            await EndGame(room, burnout: false);
        }
        else
        {
            // Start next order after brief delay
            await Task.Delay(1000);
            await StartNextOrder(room);
        }
    }
    
    public async Task EndGame(Room room, bool burnout)
    {
        if (burnout)
        {
            room.State = RoomState.GameOver;
            
            var gameOverEvent = new GameOverEvent(
                room.RoomId,
                "Burnout - God's mood dropped too low!",
                room.OrderIndex,
                room.SuccessCount,
                room.FailCount
            );
            
            await _hubContext.Clients.Group(room.RoomId.ToString())
                .SendAsync("GameOver", gameOverEvent);
        }
        
        room.State = RoomState.Results;
        
        // Calculate per-player stats
        var totalHits = room.Players.Values.Sum(p => p.HitCount);
        var playerStats = room.Players.Values
            .Where(p => p.IsConnected || p.HitCount > 0) // Include players who contributed
            .Select(p => new PlayerStats(
                p.Name,
                p.HitCount,
                totalHits > 0 ? Math.Round((double)p.HitCount / totalHits * 100, 1) : 0
            ))
            .OrderByDescending(ps => ps.HitCount)
            .ToArray();
        
        var gameFinishedEvent = new GameFinishedEvent(
            room.RoomId,
            room.OrderIndex,
            room.SuccessCount,
            room.FailCount,
            room.Mood,
            playerStats
        );
        
        await _hubContext.Clients.Group(room.RoomId.ToString())
            .SendAsync("GameFinished", gameFinishedEvent);
    }
    
    private async Task StartNextOrder(Room room)
    {
        var order = _orderGenerator.GenerateOrder(_roomService.GetOrderSeconds());
        room.CurrentOrder = order;
        room.OrderEndsAt = order.EndsAt;
        
        var orderStartedEvent = new OrderStartedEvent(
            order.OrderId,
            room.OrderIndex + 1,
            order.Required,
            order.EndsAt,
            _roomService.GetOrderSeconds()
        );
        
        await _hubContext.Clients.Group(room.RoomId.ToString())
            .SendAsync("OrderStarted", orderStartedEvent);
    }
    
    private bool AreAllPlayersReady(Room room)
    {
        var connectedPlayers = room.Players.Values.Where(p => p.IsConnected).ToList();
        if (connectedPlayers.Count == 0) return false;
        
        return connectedPlayers.All(p => p.IsReady);
    }
    
    private bool IsOrderComplete(Order order)
    {
        return order.Required.All(kvp => order.Submitted[kvp.Key] == kvp.Value);
    }
    
    private async Task BroadcastRoomStateUpdated(Room room)
    {
        var connectedPlayers = room.Players.Values.Where(p => p.IsConnected).ToList();
        
        var roomStateEvent = new RoomStateUpdatedEvent(
            room.RoomId,
            room.State,
            room.Players.Values.ToArray(),
            connectedPlayers.Count,
            connectedPlayers.Count(p => p.IsReady)
        );
        
        await _hubContext.Clients.Group(room.RoomId.ToString())
            .SendAsync("RoomStateUpdated", roomStateEvent);
    }
    
    private async Task BroadcastOrderTotals(Room room)
    {
        if (room.CurrentOrder == null) return;
        
        var orderTotalsEvent = new OrderTotalsUpdatedEvent(
            room.CurrentOrder.OrderId,
            room.CurrentOrder.Submitted,
            DateTime.UtcNow
        );
        
        await _hubContext.Clients.Group(room.RoomId.ToString())
            .SendAsync("OrderTotalsUpdated", orderTotalsEvent);
    }
    
    public async Task SendStateSnapshot(Room room, string connectionId)
    {
        var snapshot = new StateSnapshotEvent(
            room.RoomId,
            room.State,
            room.Mood,
            room.CurrentOrder,
            room.OrderIndex,
            room.OrderEndsAt,
            room.Players.Values.ToArray()
        );
        
        await _hubContext.Clients.Client(connectionId).SendAsync("StateSnapshot", snapshot);
    }
}
