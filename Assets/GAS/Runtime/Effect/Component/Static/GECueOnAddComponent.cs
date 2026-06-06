using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GECueOnAddComponent : IComponentData, IEnableableComponent
    {
        /// <summary>
        ///     cue entity
        /// </summary>
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnAdd : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var entities = CreateCueEntityArray(entityManager, ge);
            entityManager.SetComponentData(ge, new GECueOnAddComponent
            {
                cues = entities
            });
            entityManager.SetComponentEnabled<GECueOnAddComponent>(ge, true);
        }
    }
}
