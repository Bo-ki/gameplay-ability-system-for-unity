using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityCooldownComponent : IComponentData, IEnableableComponent
    {
        public int Cooldown;
        public int GameplayEffectCode;
    }

    public sealed class ConfAbilityCooldown : AbilityComponentConfig
    {
        public int Cooldown;
        public int GameplayEffectCode;

        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            entityManager.SetComponentData(ability, new AbilityCooldownComponent
            {
                Cooldown = Cooldown,
                GameplayEffectCode = GameplayEffectCode,
            });
            entityManager.SetComponentEnabled<AbilityCooldownComponent>(ability, true);
        }
    }
}
