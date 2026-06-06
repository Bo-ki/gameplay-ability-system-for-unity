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
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var entities = CreateCueEntityArray(entityManager, ge);
            entityManager.SetComponentData(ge, new GECueOnActivateComponent
            {
                cues = entities
            });
            entityManager.SetComponentEnabled<GECueOnActivateComponent>(ge, true);
        }
    }
}
