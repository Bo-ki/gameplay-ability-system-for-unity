using Unity.Entities;

namespace GAS.Runtime
{
    public struct CAbilityCost : IComponentData
    {
        public int GameplayEffectCode;
    }

    public sealed class ConfAbilityCost:AbilityComponentConfig
    {
        public int GameplayEffectCode;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponentData(ability, new CAbilityCost
            {
                GameplayEffectCode = GameplayEffectCode,
            });
        }
    }
}
