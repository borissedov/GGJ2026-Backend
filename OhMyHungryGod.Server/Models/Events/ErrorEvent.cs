namespace OhMyHungryGod.Server.Models.Events;

public record ErrorEvent(
    string ErrorCode,
    string Message
);
