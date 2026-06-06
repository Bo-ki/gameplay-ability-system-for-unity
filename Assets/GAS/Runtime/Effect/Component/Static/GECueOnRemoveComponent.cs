using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnRemoveComponent : IComponentData, IEnableableComponent
    {
        /// <summary>
        ///     cue entity
        /// </summary>
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnRemove : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var entities = CreateCueEntityArray(entityManager, ge);
            entityManager.SetComponentData(ge, new GECueOnRemoveComponent
            {
                cues = entities
            });
            entityManager.SetComponentEnabled<GECueOnRemoveComponent>(ge, true);
        }
    }
}
