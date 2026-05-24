using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Public migration-only bridge for callers that have not moved to EffectCommand / SpecStream yet.
    /// </summary>
    public static class GameplayEffectLegacyBridge
    {
        public static Entity ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            string namePrefix = "ApplyGERequest")
        {
            return GameplayEffectRequestWriter.ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
                em,
                request,
                targetAsc,
                targetDataKind,
                namePrefix);
        }

        public static Entity ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            in BSetByCallerValue setByCallerValue,
            string namePrefix = "ApplyGERequest")
        {
            return GameplayEffectRequestWriter.ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
                em,
                request,
                targetAsc,
                targetDataKind,
                setByCallerValue,
                namePrefix);
        }
    }
}
