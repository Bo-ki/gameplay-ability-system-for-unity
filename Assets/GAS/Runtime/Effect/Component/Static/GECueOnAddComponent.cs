using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnAddComponent : IComponentData, IEnableableComponent
    {
    }

    public sealed class ConfCueOnAdd : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            MarkLegacyCueComponent<GECueOnAddComponent>(entityManager, ge);
        }
    }
}
