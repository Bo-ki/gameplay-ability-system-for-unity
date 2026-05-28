using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnActivateComponent : IComponentData, IEnableableComponent
    {
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnActivate : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            GASManager.EntityManager.SetComponentData(ge, new GECueOnActivateComponent
            {
                cues = entities
            });
            GASManager.EntityManager.SetComponentEnabled<GECueOnActivateComponent>(ge, true);
        }
    }
}
