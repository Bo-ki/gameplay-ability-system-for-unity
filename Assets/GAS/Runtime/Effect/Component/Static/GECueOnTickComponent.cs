using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnTickComponent : IComponentData, IEnableableComponent
    {  
        public NativeArray<Entity> cues;  
    }

    public sealed class ConfCueOnTick : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            GASManager.EntityManager.SetComponentData(ge, new GECueOnTickComponent
            {
                cues = entities
            });
            GASManager.EntityManager.SetComponentEnabled<GECueOnTickComponent>(ge, true);
        }
    }
}
