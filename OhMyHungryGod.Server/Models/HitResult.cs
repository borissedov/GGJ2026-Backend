namespace OhMyHungryGod.Server.Models;

public enum HitResult
{
    Counted,
    OrderSuccessImmediate,
    OrderFailedImmediate,
    AlreadyProcessed,
    InvalidState,
    NoActiveOrder
}
