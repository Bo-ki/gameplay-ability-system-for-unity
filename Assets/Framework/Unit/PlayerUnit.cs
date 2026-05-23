// Assets/Framework/Unit/PlayerUnit.cs
using Framework.Core;
using GAS.Runtime;
using UnityEngine;

namespace Framework.Unit
{
    /// <summary>
    /// 玩家单位。
    /// - 继承 UnitBase，Awake 时自动完成 ASC 初始化和 UnitManager 注册
    /// - 暴露语义化 Ability 触发方法供 PlayerController 调用
    /// - Move 使用主摄像机方向（而非 transform.forward），与 DemoPlayer 保持一致
    /// - 获取实例：UnitManager.Instance.GetUnit&lt;PlayerUnit&gt;()
    /// </summary>
    public class PlayerUnit : UnitBase
    {
        private Camera _mainCamera;

        private CharacterController _controller;

        protected override void Awake()
        {
            base.Awake(); // → ASC 初始化 + UnitManager 注册

            _mainCamera = Camera.main;
            _controller = GetComponent<CharacterController>();

            // 添加固有阵营 Tag（Faction.Player）
            ASC.Facade.AddFixedTag(XTag.Faction_Player);
            // 添加 Ability 根 Tag，部分 Ability 激活条件依赖此 Tag
            ASC.Facade.AddFixedTag(XTag.Ability);

            // 施加初始 Buff，例如耐力自动回复（effect id 1007）
            ApplyEffectToSelf(1007);
        }

        private bool _hasSpSnapshot;
        private float _lastSp;

        private void OnEnable()
        {
            ResetSpSnapshot();
        }

        private void Update()
        {
            TickSpState();
        }

        private void ResetSpSnapshot()
        {
            _lastSp = ASC.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Sp);
            _hasSpSnapshot = true;
        }

        private void TickSpState()
        {
            if (!_hasSpSnapshot)
            {
                ResetSpSnapshot();
                return;
            }

            var sp = ASC.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Sp);
            if (!Mathf.Approximately(sp, _lastSp) && sp <= 0)
                StopRun();
            _lastSp = sp;
        }

        // ──────────────────────────────────────────
        // Ability 触发方法（由 PlayerController 调用）
        // ──────────────────────────────────────────

        /// <summary>
        /// 每帧持续调用。使用相机方向作为视角前方，保证角色朝向正确。
        /// 参照 DemoPlayer.Move() 实现。
        /// </summary>
        public override void Move(Vector3 direction)
        {
            if (!IsAbilityActive(XAbility.ABILITY_move))
                TryActivateAbility(XAbility.ABILITY_move);

            var viewFwd = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized;
            if (viewFwd.sqrMagnitude > 0f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(viewFwd), Time.deltaTime * 10f);

            if (_controller != null && direction.sqrMagnitude > 0f)
                _controller.Move(direction * GetSpeed() * Time.deltaTime);
        }

        public override void StopMove()
        {
            if (IsAbilityActive(XAbility.ABILITY_move))
                TryEndAbility(XAbility.ABILITY_move);
        }

        public void StartRun()
        {
            // RunSpeedUp 需要 Event.Moving (3001) Tag 才能激活（见 exgas_tbability.json）
            if (!IsAbilityActive(XAbility.ABILITY_RunSpeedUp))
                TryActivateAbility(XAbility.ABILITY_RunSpeedUp);
        }

        public void StopRun()
        {
            if (IsAbilityActive(XAbility.ABILITY_RunSpeedUp))
                TryEndAbility(XAbility.ABILITY_RunSpeedUp);
        }

        public override void Attack()
        {
            // Attack 自带 ActivationBlockedTags = [Event.Attacking]，防止重复激活
            ASC.Facade.TryActivateAbility(XAbility.ABILITY_Attack);
        }

        /// <summary>获取当前移速属性（供 PlayerController 的速度插值使用）</summary>
        public float GetSpeed()
            => ASC.Facade.GetAttrCurrentValue(XAttrSet.FightUnit, XAttribute.Spd);
    }
}
