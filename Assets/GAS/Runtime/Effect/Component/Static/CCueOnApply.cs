using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct CCueOnApply : IComponentData
    {
        public NativeArray<Entity> cues;
    }

    public sealed class ConfCueOnApply : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            GASManager.EntityManager.AddComponentData(ge, new CCueOnApply
            {
                cues = entities
            });
        }
    }
}
