using GAS.Runtime;
using UnityEngine;

namespace DemoForESC._Script
{
    public class DemoPlayer : BaseUnit
    {
        private static DemoPlayer _player;
        public static DemoPlayer Player()
        {
            if (_player == null)
                _player = FindObjectOfType<DemoPlayer>();
            
            return _player;
        }
        
        Camera _mainCamera;
        
        protected override void Awake()
        {
            base.Awake();
            _mainCamera = UnityEngine.Camera.main;

            
            // 自动恢复耐力Buff
            AbilitySystem.Facade.RequestGameplayEffectToSelf(1007);
        }

        public override void Move(Vector3 direction)
        {
            if(!AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_move))
                AbilitySystem.Facade.TryActivateAbility(XAbility.ABILITY_move);
            
            var viewPointForward = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized;
            ApplyMove(direction, viewPointForward);
        }

        public void StartRun()
        {
            if(!AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_RunSpeedUp))
                AbilitySystem.Facade.TryActivateAbility(XAbility.ABILITY_RunSpeedUp);
        }

        public void StopRun()
        {
            if(AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_RunSpeedUp)) 
                AbilitySystem.Facade.TryEndAbility(XAbility.ABILITY_RunSpeedUp);
        }
        
        public void StartDebugGE1()
        {
            if(!AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_debug_ge_ability)) 
                AbilitySystem.Facade.TryActivateAbility(XAbility.ABILITY_debug_ge_ability);
        }
        
        public void StartDebugGE2()
        {
            if(!AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_debug_ge_2)) 
                AbilitySystem.Facade.TryActivateAbility(XAbility.ABILITY_debug_ge_2);
        }
        
        public void StopDebugGE1()
        {
            if(AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_debug_ge_ability)) 
                AbilitySystem.Facade.TryEndAbility(XAbility.ABILITY_debug_ge_ability);
        }
        
        public void StopDebugGE2()
        {
            if(AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_debug_ge_2)) 
                AbilitySystem.Facade.TryEndAbility(XAbility.ABILITY_debug_ge_2);
        }

        public void Dodge()
        {
            AbilitySystem.Facade.TryActivateAbility(XAbility.ABILITY_Dodge);
        }

        protected override void OnSpChangeAfter(float lastSp, float newSp)
        {
            base.OnSpChangeAfter(lastSp, newSp);
            if(newSp<=0) StopRun();
        }
    }
}
