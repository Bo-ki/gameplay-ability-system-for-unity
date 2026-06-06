using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityCostComponent : IComponentData, IEnableableComponent
    {
        public int GameplayEffectCode;
    }

    public sealed class ConfAbilityCost:AbilityComponentConfig
    {
        public int GameplayEffectCode;

        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            entityManager.SetComponentData(ability, new AbilityCostComponent
            {
                GameplayEffectCode = GameplayEffectCode,
            });
            entityManager.SetComponentEnabled<AbilityCostComponent>(ability, true);
        }
    }
}
