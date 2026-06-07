using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnActivateComponent : IComponentData, IEnableableComponent
    {
    }

    public sealed class ConfCueOnActivate : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            MarkLegacyCueComponent<GECueOnActivateComponent>(entityManager, ge);
        }
    }
}
