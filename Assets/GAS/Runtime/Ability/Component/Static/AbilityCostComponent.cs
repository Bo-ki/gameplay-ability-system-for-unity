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

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.SetComponentData(ability, new AbilityCostComponent
            {
                GameplayEffectCode = GameplayEffectCode,
            });
            _entityManager.SetComponentEnabled<AbilityCostComponent>(ability, true);
        }
    }
}
