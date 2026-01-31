using OhMyHungryGod.Server.Models;

namespace OhMyHungryGod.Server.Services;

public class MoodCalculatorService
{
    public GodMood CalculateNewMood(GodMood currentMood, int successCount, int failCount)
    {
        // Mood calculation rules:
        // - Every 2 successful orders → mood +1
        // - Every 1 failed order → mood -1
        
        var moodChange = 0;
        
        // Add +1 for every 2 successes
        moodChange += successCount / 2;
        
        // Subtract 1 for each failure
        moodChange -= failCount;
        
        var newMoodValue = (int)currentMood + moodChange;
        
        // Clamp to valid mood range
        if (newMoodValue > (int)GodMood.Happy)
            newMoodValue = (int)GodMood.Happy;
        if (newMoodValue < (int)GodMood.Burned)
            newMoodValue = (int)GodMood.Burned;
        
        return (GodMood)newMoodValue;
    }
    
    public bool IsBurnout(GodMood mood)
    {
        return mood < GodMood.Angry;
    }
}
