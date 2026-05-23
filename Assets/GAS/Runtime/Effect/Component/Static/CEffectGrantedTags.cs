using Unity.Entities;

namespace GAS.Runtime
{
    public struct CEffectGrantedTags : IComponentData
    {
        public CTagMask Tags;
    }

    public sealed class ConfEffectGrantedTags : GameplayEffectComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.AddComponentData(ge, new CEffectGrantedTags
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
        }
    }
}
