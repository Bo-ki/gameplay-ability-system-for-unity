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
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var entities = CreateCueEntityArray(entityManager, ge);
            entityManager.SetComponentData(ge, new GECueOnTickComponent
            {
                cues = entities
            });
            entityManager.SetComponentEnabled<GECueOnTickComponent>(ge, true);
        }
    }
}
