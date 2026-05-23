using Unity.Entities;

namespace GAS.Runtime
{
    public struct CAbilityCooldown : IComponentData
    {
        public int Cooldown;
        public int GameplayEffectCode;
    }

    public sealed class ConfAbilityCooldown : AbilityComponentConfig
    {
        public int Cooldown;
        public int GameplayEffectCode;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponentData(ability, new CAbilityCooldown
            {
                Cooldown = Cooldown,
                GameplayEffectCode = GameplayEffectCode,
            });
        }
    }
}
