using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnDeactivateComponent : IComponentData, IEnableableComponent
    {
    }

    public sealed class ConfCueOnDeactivate : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            MarkLegacyCueComponent<GECueOnDeactivateComponent>(entityManager, ge);
        }
    }
}
