using Framework.Core;
using GAS.Runtime;
using UnityEngine;

namespace Framework.Unit
{
    /// <summary>
    /// 所有游戏单位的基类。
    /// - 持有并初始化 AbilitySystemBinding（ASC 预设由 Inspector 配置）
    /// - Awake 时自动向 UnitManager 注册，OnDestroy 时注销并广播 UnitDeadEvent
    /// - 子类通过显式读取 ASC 状态驱动自身表现和业务反应
    /// </summary>
    [RequireComponent(typeof(AbilitySystemBinding))]
    public abstract class UnitBase : MonoBehaviour
    {
        /// <summary>ASC 预设 ID，对应 exgas_tbasc.json 中的配置，在 Inspector 中设置</summary>
        [SerializeField] protected int _ascPresetId = 0;

        /// <summary>该单位绑定的 ASC Entity 生命周期组件</summary>
        public AbilitySystemBinding ASC { get; private set; }

        protected virtual void Awake()
        {
            // 1. 获取或添加 ASC Entity 绑定
            ASC = GetComponent<AbilitySystemBinding>();

            // 2. 用 Luban 预设初始化 ASC（加载 Tags、AttrSets、Abilities、Level）
            ASC.Init(XLuban.GetAscConfig(_ascPresetId));

            // 3. 向 UnitManager 注册自身
            UnitManager.Instance.Register(this);
        }

        protected virtual void OnDestroy()
        {
            // 注销并广播死亡事件
            UnitManager.Instance.Unregister(this);
            GameEventBus.Dispatch(new UnitDeadEvent { Unit = gameObject });
        }

        // ──────────────────────────────────────────
        // GAS 快捷接口（供子类使用，避免重复写 ASC.Facade.XXX）
        // ──────────────────────────────────────────

        /// <summary>尝试激活技能</summary>
        public void TryActivateAbility(int abilityId)
            => ASC.Facade.TryActivateAbility(abilityId);

        /// <summary>尝试结束技能</summary>
        public void TryEndAbility(int abilityId)
            => ASC.Facade.TryEndAbility(abilityId);

        /// <summary>判断技能是否激活中</summary>
        public bool IsAbilityActive(int abilityId)
            => ASC.Facade.IsAbilityActive(abilityId);

        /// <summary>请求对自身施加 GameplayEffect</summary>
        public void ApplyEffectToSelf(int effectId)
        {
            ASC.Facade.RequestGameplayEffectToSelf(effectId);
        }

        /// <summary>查询 ASC 是否持有指定 Tag</summary>
        public bool HasTag(int tagId) => ASC.Facade.HasTag(tagId);

        /// <summary>获取指定属性当前值</summary>
        public float GetAttrCurrentValue(int attrSetCode, int attrCode)
            => ASC.Facade.GetAttrCurrentValue(attrSetCode, attrCode);

        // ──────────────────────────────────────────
        // 通用行为虚方法（子类按需重写）
        // ──────────────────────────────────────────

        public virtual void Move(Vector3 direction) { }
        public virtual void StopMove() { }
        public virtual void Attack() { }
    }
}
