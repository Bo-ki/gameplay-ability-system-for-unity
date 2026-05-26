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
    }
}
