using System;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GrantedAbilityConfigSetting
    {
        public int AbilityCode;
        public int Level;
        public GrantedAbilityActivationPolicy ActivationPolicy;
        public GrantedAbilityDeactivationPolicy DeactivationPolicy;
        public GrantedAbilityRemovePolicy RemovePolicy;
    }

    public struct BGrantedAbilityConfig : IBufferElementData
    {
        public int AbilityCode;
        public int Level;
        public GrantedAbilityActivationPolicy ActivationPolicy;
        public GrantedAbilityDeactivationPolicy DeactivationPolicy;
        public GrantedAbilityRemovePolicy RemovePolicy;
    }

    public struct BGrantedAbilityRuntime : IBufferElementData
    {
        public int ConfigIndex;
        public Entity AbilityEntity;
    }

    public sealed class ConfGrantedAbilityConfig : GameplayEffectComponentConfig
    {
        public GrantedAbilityConfigSetting[] GrantedAbilities;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            var settings = GrantedAbilities ?? Array.Empty<GrantedAbilityConfigSetting>();
            var configBuffer = _entityManager.AddBuffer<BGrantedAbilityConfig>(ge);

            for (var i = 0; i < settings.Length; i++)
            {
                configBuffer.Add(new BGrantedAbilityConfig
                {
                    AbilityCode = settings[i].AbilityCode,
                    Level = settings[i].Level,
                    ActivationPolicy = settings[i].ActivationPolicy,
                    DeactivationPolicy = settings[i].DeactivationPolicy,
                    RemovePolicy = settings[i].RemovePolicy,
                });
            }
        }
    }
}
