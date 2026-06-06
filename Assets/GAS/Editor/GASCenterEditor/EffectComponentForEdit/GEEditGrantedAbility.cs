using System;
using GAS.Runtime;
using UnityEngine;

namespace GAS.Editor
{
    [Serializable]
    public class GEEditGrantedAbility
    {
        public int abilityID;
        
        [Min(0)]
        public int level;
        
        public GrantedAbilityActivationPolicy ActivationPolicy;
        
        public GrantedAbilityDeactivationPolicy DeactivationPolicy;
        
        public GrantedAbilityRemovePolicy RemovePolicy;
    }
}
