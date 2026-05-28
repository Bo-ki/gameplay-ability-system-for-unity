using Unity.Entities;
using Unity.Collections;

namespace GAS.Runtime
{
    internal static class GameplayEffectLegacyBridge
    {
        public static bool TryCreateLegacyRequestEntity(
            EntityManager em,
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            out Entity requestEntity)
        {
            requestEntity = Entity.Null;
            return false;
        }
    }
}
