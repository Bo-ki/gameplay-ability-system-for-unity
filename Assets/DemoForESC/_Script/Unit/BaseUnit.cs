using EXToyLib;
using GAS.Runtime;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;

namespace DemoForESC._Script
{
    /// <summary>
    /// 基础单位
    /// </summary>
    public class BaseUnit : MonoBehaviour
    {
        public AbilitySystemBinding AbilitySystem { get; private set; }

        [ShowInInspector]
        [LabelText("ASC预设")]
        [ValueDropdown("@GasXlsxChoice.Ascs()")]
        public int _ascPresetId = 0;
        private bool _hasSpSnapshot;
        private float _lastSp;
        
        protected virtual void Awake()
        {
            AbilitySystem = transform.GetOrAddComponent<AbilitySystemBinding>();
            AbilitySystem.Init(XLuban.GetAscConfig(_ascPresetId));
        }
        
        protected virtual void OnEnable()
        {
            GravityForCharacterController.Instance.Register(GetComponent<CharacterController>());
            ResetSpSnapshot();
        }
        
        protected virtual void OnDisable()
        {
            GravityForCharacterController.Instance.Unregister(GetComponent<CharacterController>());
            _hasSpSnapshot = false;
        }

        protected virtual void Update()
        {
            TickAttributeState();
        }
        
        public virtual void Move(Vector3 direction)
        {
            if(!AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_move))
                AbilitySystem.Facade.TryActivateAbility(XAbility.ABILITY_move);
            
            var viewPointForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            ApplyMove(direction, viewPointForward);
        }
        
        public virtual void StopMove()
        {
            if(AbilitySystem.Facade.IsAbilityActive(XAbility.ABILITY_move)) 
                AbilitySystem.Facade.TryEndAbility(XAbility.ABILITY_move);
        }
        
        public virtual void Jump()
        {
        }
        
        public virtual void Attack()
        {
            AbilitySystem.Facade.TryActivateAbility(XAbility.ABILITY_Attack);
        }

        public bool IsMoving()
        {
            var tagMoving = 1; //GTagLib.Event_Moving.HashCode
            return AbilitySystem.Facade.HasTag(tagMoving);
        }

        #region Attributes

        public float GetSpeed()
        {
            return AbilitySystem.Facade.GetAttrCurrentValue(XAttrSet.FightUnit ,XAttribute.Spd);
        }

        protected void ApplyMove(Vector3 direction, Vector3 viewPointForward)
        {
            if (viewPointForward.sqrMagnitude > 0f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(viewPointForward), Time.deltaTime * 10f);

            var controller = GetComponent<CharacterController>();
            if (controller != null && direction.sqrMagnitude > 0f)
                controller.Move(direction * GetSpeed() * Time.deltaTime);
        }

        private void ResetSpSnapshot()
        {
            _lastSp = AbilitySystem.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Sp);
            _hasSpSnapshot = true;
        }

        private void TickAttributeState()
        {
            if (!_hasSpSnapshot)
            {
                ResetSpSnapshot();
                return;
            }

            var sp = AbilitySystem.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Sp);
            if (!Mathf.Approximately(sp, _lastSp))
                OnSpChangeAfter(_lastSp, sp);
            _lastSp = sp;
        }
        
        protected virtual void OnSpChangeAfter(float lastSp,float newSp)
        {
        }
        
        
        #endregion
    }
}
