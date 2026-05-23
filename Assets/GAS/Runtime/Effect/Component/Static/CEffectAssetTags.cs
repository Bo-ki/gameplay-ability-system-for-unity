using Unity.Entities;

namespace GAS.Runtime
{
    public struct CEffectAssetTags : IComponentData
    {
        /// <summary>
        /// AssetTags,描述GE性质的Tag。用于Tag相关逻辑判断。
        /// </summary>
        public CTagMask Tags;
    }
    
    public sealed class ConfAssetTags:GameplayEffectComponentConfig
    {
        public int[] tags;
        
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.AddComponentData(ge, new CEffectAssetTags
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
        }
    }
}
