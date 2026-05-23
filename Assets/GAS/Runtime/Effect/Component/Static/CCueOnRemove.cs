using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct CCueOnRemove : IComponentData
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
            GASManager.EntityManager.AddComponentData(ge, new CCueOnRemove
            {
                cues = entities
            });
        }
    }
}
