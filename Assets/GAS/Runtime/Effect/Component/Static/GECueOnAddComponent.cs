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
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            GASManager.EntityManager.SetComponentData(ge, new GECueOnAddComponent
            {
                cues = entities
            });
            GASManager.EntityManager.SetComponentEnabled<GECueOnAddComponent>(ge, true);
        }
    }
}
