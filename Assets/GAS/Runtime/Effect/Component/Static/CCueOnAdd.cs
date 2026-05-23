using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct CCueOnAdd : IComponentData
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
            GASManager.EntityManager.AddComponentData(ge, new CCueOnAdd
            {
                cues = entities
            });
        }
    }
}
