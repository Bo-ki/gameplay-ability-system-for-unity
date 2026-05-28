using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnDeactivateComponent : IComponentData, IEnableableComponent
    {
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnDeactivate : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            GASManager.EntityManager.SetComponentData(ge, new GECueOnDeactivateComponent
            {
                cues = entities
            });
            GASManager.EntityManager.SetComponentEnabled<GECueOnDeactivateComponent>(ge, true);
        }
    }
}
