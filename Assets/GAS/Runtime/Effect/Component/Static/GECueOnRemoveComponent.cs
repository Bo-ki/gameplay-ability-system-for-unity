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
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            GASManager.EntityManager.SetComponentData(ge, new GECueOnRemoveComponent
            {
                cues = entities
            });
            GASManager.EntityManager.SetComponentEnabled<GECueOnRemoveComponent>(ge, true);
        }
    }
}
