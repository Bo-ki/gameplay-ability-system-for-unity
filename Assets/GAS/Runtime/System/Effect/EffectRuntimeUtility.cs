using Unity.Entities;

namespace GAS.Runtime
{
    internal static class EffectRuntimeUtility
    {
        public static bool TryGetStaticDefinitionBlob(
            EntityManager em,
            Entity ge,
            out BlobAssetReference<GEStaticDefinitionBlob> blob)
        {
            blob = default;

            if (!em.Exists(ge) || !em.HasComponent<CEffectSpecData>(ge))
                return false;

            var spec = em.GetComponentData<CEffectSpecData>(ge);
            return spec.GameplayEffectCode > 0
                   && GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                       em,
                       spec.GameplayEffectCode,
                       out blob);
        }

        public static Entity ApplyInstantEffect(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc,
            Entity sourceAbility = default,
            Entity instigator = default)
        {
            return Entity.Null;
        }

        public static void ApplyInactiveDurationEffect(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc) { }

        public static bool HasOngoingRequirements(EntityManager em, Entity ge)
        {
            return false;
        }

        public static bool TryMergeStackingApplication(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc,
            out bool wasMerged)
        {
            wasMerged = false;
            return false;
        }

        public static void HandleDurationExpired(
            EntityManager em,
            Entity ge) { }

        public static void ActivateDurationEffect(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc) { }

        public static void DeactivateOngoingEffect(
            EntityManager em,
            Entity ge) { }

        public static void ReactivateOngoingEffect(
            EntityManager em,
            Entity ge) { }

        public static void MarkEffectForRemoval(
            EntityManager em,
            Entity ge,
            int reasonCode = 0) { }

        public static void CleanupActiveEffect(
            EntityManager em,
            Entity ge) { }

        public static bool ShouldReject(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc)
        {
            return false;
        }

        public static void RemoveActiveGameplayEffectsWithTags(
            EntityManager em,
            Entity target,
            int tagIndex) { }

        public static void FinalizeEffectDestroy(
            EntityManager em,
            Entity ge) { }
    }
}
