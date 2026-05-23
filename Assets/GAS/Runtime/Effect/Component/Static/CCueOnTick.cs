using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct CCueOnTick : IComponentData  
    {  
        public NativeArray<Entity> cues;  
    }

    public sealed class ConfCueOnTick : ConfCueBase
    {
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var entities = CreateCueEntityArray(ge);
            GASManager.EntityManager.AddComponentData(ge, new CCueOnTick
            {
                cues = entities
            });
        }
    }
}
