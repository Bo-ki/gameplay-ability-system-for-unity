using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnTickComponent : IComponentData, IEnableableComponent
    {
    }

    public sealed class ConfCueOnTick : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            MarkLegacyCueComponent<GECueOnTickComponent>(entityManager, ge);
        }
    }
}
