using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityGrantedByEffectComponent : IComponentData
    {
        public Entity SourceEffect;                              // 来源GE Entity
        public int SourceSequence;
        public int SourceGameplayEffectCode;
        public GrantedAbilityActivationPolicy ActivationPolicy;
        public GrantedAbilityDeactivationPolicy DeactivationPolicy;
        public GrantedAbilityRemovePolicy RemovePolicy;
    }
}
