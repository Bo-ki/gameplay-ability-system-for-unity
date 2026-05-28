using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityActivationOwnedTagsComponent : IComponentData, IEnableableComponent
    {
        public TagMaskComponent Tags;
    }

    public sealed class ConfAbilityActivationOwnedTags : AbilityComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.SetComponentData(ability, new AbilityActivationOwnedTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
            _entityManager.SetComponentEnabled<AbilityActivationOwnedTagsComponent>(ability, true);
        }
    }
}
