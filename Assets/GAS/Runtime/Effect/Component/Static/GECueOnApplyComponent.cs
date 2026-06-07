using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnApplyComponent : IComponentData, IEnableableComponent
    {
    }

    public sealed class ConfCueOnApply : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            MarkLegacyCueComponent<GECueOnApplyComponent>(entityManager, ge);
        }
    }

    public struct GECueRequestOnApplyComponent : IComponentData, IEnableableComponent
    {
        public int CueCode;
    }

    public sealed class ConfGameplayEffectCueRequestOnApply : GameplayEffectComponentConfig
    {
        public int CueCode;

        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            entityManager.SetComponentData(ge, new GECueRequestOnApplyComponent
            {
                CueCode = CueCode,
            });
            entityManager.SetComponentEnabled<GECueRequestOnApplyComponent>(ge, true);
        }
    }
}
