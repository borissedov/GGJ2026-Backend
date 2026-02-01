# Oh My Hungry God - Backend Server

.NET 9 backend with SignalR for the multiplayer AR game.

## Features

- **SignalR WebSocket Communication**: Real-time bidirectional communication
- **Authoritative Server**: All game logic runs on server
- **In-Memory State**: Fast room and player management
- **Player Name Tracking**: Players join with custom names
- **Per-Player Statistics**: Track individual contributions and display in results
- **Background Services**: Automatic countdown, order timeout, and room cleanup
- **No Authentication**: GUID-based room/player identification

## Project Structure

```
OhMyHungryGod.Server/
├── Hubs/
│   └── GameHub.cs                  # SignalR hub (all client methods)
├── Services/
│   ├── RoomService.cs              # Room lifecycle management
│   ├── GameEngineService.cs        # Core game logic & state machine
│   ├── OrderGeneratorService.cs    # Order generation
│   ├── MoodCalculatorService.cs    # Mood system logic
│   └── BackgroundTimerService.cs   # Background timers
├── Models/
│   ├── Room.cs, Player.cs, Order.cs
│   └── Events/                     # Event DTOs
├── State/
│   └── InMemoryRoomStore.cs        # Concurrent room storage
└── Program.cs                      # Entry point
```

## Running Locally

```bash
cd OhMyHungryGod.Server
dotnet run
```

Server will start on `http://localhost:5000` (or `https://localhost:5001` for HTTPS).

SignalR hub endpoint: `/gamehub`

## Configuration

Game settings (hardcoded in `RoomService.cs` and `GameEngineService.cs`):

- `OrdersPerGame`: 10 orders per game
- `OrderDurationSeconds`: 10 seconds per order
- `CountdownDurationSeconds`: 6 seconds lobby countdown
- `ResultsTimeoutSeconds`: 30 seconds before room cleanup
- `RoomInactivityTimeoutMinutes`: 5 minutes before inactive room cleanup

## API Endpoints

### SignalR Hub Methods (Client → Server)

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `CreateRoom` | - | `{ roomId, joinCode }` | Display creates room |
| `JoinRoom` | `joinCode` | `{ roomId, playerId }` | Player joins via code |
| `SetReady` | `roomId, ready` | - | Toggle ready state |
| `ReportHit` | `roomId, hitId, fruit` | - | Submit fruit hit |
| `Ping` | `roomId` | - | Keep-alive |
| `LeaveRoom` | `roomId` | - | Explicit disconnect |

### Server Events (Server → Clients)

| Event | Target | Trigger |
|-------|--------|---------|
| `RoomStateUpdated` | Display | Player join/leave/ready |
| `CountdownStarted` | Display | All players ready |
| `CountdownCancelled` | Display | Player unready during countdown |
| `GameStarted` | Both | Countdown completes |
| `OrderStarted` | Both | New order begins |
| `OrderTotalsUpdated` | Display | Hit processed |
| `OrderResolved` | Both | Order success/fail |
| `MoodChanged` | Display | Mood calculation |
| `GameOver` | Both | Early termination (not used) |
| `GameFinished` | Both | 10 orders complete (includes player stats) |
| `StateSnapshot` | Mobile | On join or reconnect |
| `Error` | Both | Validation errors |

## Deployment to Azure App Service

### Required Configuration

1. **Runtime Stack**: .NET 9
2. **Always On**: Enabled (prevents cold starts)
3. **WebSockets**: Enabled (required for SignalR)
4. **CORS**: Already configured in code to allow all origins

### Application Settings

In Azure Portal → Configuration → Application settings:

- `ASPNETCORE_ENVIRONMENT`: `Production`
- `WEBSITE_TIME_ZONE`: `UTC` (or your timezone)

### Health Check

Configure health check endpoint: `/health`

## Game Rules

- **10 orders per game** (always completes all orders)
- **10 seconds per order**
- **6 seconds countdown** before game starts
- **Immediate failure** if over-submitted
- **Immediate success** if exact match
- **Mood system**: +1 per 2 successes, -1 per failure (visual feedback only)
- **Per-player tracking**: Hit counts and contribution percentages
- **Team rating**: Final mood determines stars (Happy=3, Neutral=2, Angry=1)
