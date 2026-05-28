using Unity.Entities;

namespace GAS.Runtime
{
    public struct GEGrantedTagsComponent : IComponentData, IEnableableComponent
    {
        public TagMaskComponent Tags;
    }

    public sealed class ConfEffectGrantedTags : GameplayEffectComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.SetComponentData(ge, new GEGrantedTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
            GASManager.EntityManager.SetComponentEnabled<GEGrantedTagsComponent>(ge, true);
        }
    }
}
