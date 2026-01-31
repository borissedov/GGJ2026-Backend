namespace OhMyHungryGod.Server.Models.Events;

public record MoodChangedEvent(
    Guid RoomId,
    GodMood OldMood,
    GodMood NewMood
);
