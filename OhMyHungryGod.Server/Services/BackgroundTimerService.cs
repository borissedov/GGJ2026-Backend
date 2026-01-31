using OhMyHungryGod.Server.Models;
using OhMyHungryGod.Server.State;

namespace OhMyHungryGod.Server.Services;

public class BackgroundTimerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly InMemoryRoomStore _store;
    private readonly ILogger<BackgroundTimerService> _logger;
    
    public BackgroundTimerService(
        IServiceProvider serviceProvider,
        InMemoryRoomStore store,
        ILogger<BackgroundTimerService> logger)
    {
        _serviceProvider = serviceProvider;
        _store = store;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundTimerService started");
        
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckCountdowns();
                await CheckOrderTimeouts();
                CheckAndCleanupRooms();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BackgroundTimerService");
            }
        }
        
        _logger.LogInformation("BackgroundTimerService stopped");
    }
    
    private async Task CheckCountdowns()
    {
        using var scope = _serviceProvider.CreateScope();
        var gameEngine = scope.ServiceProvider.GetRequiredService<GameEngineService>();
        var roomService = scope.ServiceProvider.GetRequiredService<RoomService>();
        
        foreach (var room in _store.GetAllRooms())
        {
            if (room.State == RoomState.Countdown && room.CountdownStartedAt.HasValue)
            {
                var elapsed = (DateTime.UtcNow - room.CountdownStartedAt.Value).TotalSeconds;
                
                if (elapsed >= roomService.GetCountdownSeconds())
                {
                    await gameEngine.StartGame(room);
                }
            }
        }
    }
    
    private async Task CheckOrderTimeouts()
    {
        using var scope = _serviceProvider.CreateScope();
        var gameEngine = scope.ServiceProvider.GetRequiredService<GameEngineService>();
        
        foreach (var room in _store.GetAllRooms())
        {
            if (room.State == RoomState.InGame)
            {
                await gameEngine.CheckOrderTimeout(room);
            }
        }
    }
    
    private void CheckAndCleanupRooms()
    {
        using var scope = _serviceProvider.CreateScope();
        var roomService = scope.ServiceProvider.GetRequiredService<RoomService>();
        
        roomService.CleanupInactiveRooms();
    }
}
