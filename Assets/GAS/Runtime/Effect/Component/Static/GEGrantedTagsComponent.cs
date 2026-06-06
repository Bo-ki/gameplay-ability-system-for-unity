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

        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            entityManager.SetComponentData(ge, new GEGrantedTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
            entityManager.SetComponentEnabled<GEGrantedTagsComponent>(ge, true);
        }
    }
}
