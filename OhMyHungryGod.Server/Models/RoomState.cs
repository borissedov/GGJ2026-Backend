namespace OhMyHungryGod.Server.Models;

public enum RoomState
{
    Welcome,      // Waiting for first player
    Lobby,        // Players joining/readying
    Countdown,    // 10s countdown active
    InGame,       // Game running
    GameOver,     // Burnout occurred
    Results,      // Final stats display
    Closed        // Room destroyed
}
