using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnRemoveComponent : IComponentData, IEnableableComponent
    {
    }

    public sealed class ConfCueOnRemove : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            MarkLegacyCueComponent<GECueOnRemoveComponent>(entityManager, ge);
        }
    }
}
