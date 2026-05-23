namespace GAS.Runtime
{
    public enum AbilityActivationResult
    {
        Success = 0,
        FailHasActivated = 1,
        FailTagRequirement = 2,
        FailCost = 3,
        FailCooldown = 4,
        FailOtherReason = 5,
        FailBlockedByActiveAbility = 6,
        FailActivationRequiredTags = 7,
        FailActivationBlockedTags = 8,
    }
}
