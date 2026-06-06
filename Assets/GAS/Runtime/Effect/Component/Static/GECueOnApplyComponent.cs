using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnApplyComponent : IComponentData, IEnableableComponent
    {
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnApply : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var entities = CreateCueEntityArray(entityManager, ge);
            entityManager.SetComponentData(ge, new GECueOnApplyComponent
            {
                cues = entities
            });
            entityManager.SetComponentEnabled<GECueOnApplyComponent>(ge, true);
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
