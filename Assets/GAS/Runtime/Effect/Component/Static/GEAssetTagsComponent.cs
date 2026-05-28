using Unity.Entities;

namespace GAS.Runtime
{
    public struct GEAssetTagsComponent : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// AssetTags,描述GE性质的Tag。用于Tag相关逻辑判断。
        /// </summary>
        public TagMaskComponent Tags;
    }
    
    public sealed class ConfAssetTags:GameplayEffectComponentConfig
    {
        public int[] tags;
        
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.SetComponentData(ge, new GEAssetTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
            GASManager.EntityManager.SetComponentEnabled<GEAssetTagsComponent>(ge, true);
        }
    }
}
