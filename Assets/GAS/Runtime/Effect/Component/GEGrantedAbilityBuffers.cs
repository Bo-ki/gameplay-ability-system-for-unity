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

    [InternalBufferCapacity(4)]
    public struct GEGrantedAbilityConfigBuffer : IBufferElementData, IEnableableComponent
    {
        public int AbilityCode;
        public int Level;
        public GrantedAbilityActivationPolicy ActivationPolicy;
        public GrantedAbilityDeactivationPolicy DeactivationPolicy;
        public GrantedAbilityRemovePolicy RemovePolicy;
    }

    [InternalBufferCapacity(4)]
    public struct GEGrantedAbilityRuntimeBuffer : IBufferElementData
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
            var configBuffer = _entityManager.GetBuffer<GEGrantedAbilityConfigBuffer>(ge);
            configBuffer.Clear();

            for (var i = 0; i < settings.Length; i++)
            {
                configBuffer.Add(new GEGrantedAbilityConfigBuffer
                {
                    AbilityCode = settings[i].AbilityCode,
                    Level = settings[i].Level,
                    ActivationPolicy = settings[i].ActivationPolicy,
                    DeactivationPolicy = settings[i].DeactivationPolicy,
                    RemovePolicy = settings[i].RemovePolicy,
                });
            }
            _entityManager.SetComponentEnabled<GEGrantedAbilityConfigBuffer>(ge, configBuffer.Length > 0);
        }
    }
}
