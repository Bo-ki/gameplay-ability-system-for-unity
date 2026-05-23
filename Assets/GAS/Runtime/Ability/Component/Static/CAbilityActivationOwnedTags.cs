using Unity.Entities;

namespace GAS.Runtime
{
    public struct CAbilityActivationOwnedTags : IComponentData
    {
        public CTagMask Tags;
    }

    public sealed class ConfAbilityActivationOwnedTags : AbilityComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponentData(ability, new CAbilityActivationOwnedTags
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
        }
    }
}
