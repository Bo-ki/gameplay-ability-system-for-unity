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
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var entities = CreateCueEntityArray(entityManager, ge);
            entityManager.SetComponentData(ge, new GECueOnDeactivateComponent
            {
                cues = entities
            });
            entityManager.SetComponentEnabled<GECueOnDeactivateComponent>(ge, true);
        }
    }
}
