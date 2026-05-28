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
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            GASManager.EntityManager.SetComponentData(ge, new GECueOnApplyComponent
            {
                cues = entities
            });
            GASManager.EntityManager.SetComponentEnabled<GECueOnApplyComponent>(ge, true);
        }
    }

    public struct GECueRequestOnApplyComponent : IComponentData, IEnableableComponent
    {
        public int CueCode;
    }

    public sealed class ConfGameplayEffectCueRequestOnApply : GameplayEffectComponentConfig
    {
        public int CueCode;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.SetComponentData(ge, new GECueRequestOnApplyComponent
            {
                CueCode = CueCode,
            });
            GASManager.EntityManager.SetComponentEnabled<GECueRequestOnApplyComponent>(ge, true);
        }
    }
}
