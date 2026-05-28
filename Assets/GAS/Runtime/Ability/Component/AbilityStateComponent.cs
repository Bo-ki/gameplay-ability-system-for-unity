using Unity.Entities;

namespace GAS.Runtime
{
    public enum EAbilityPhase : byte
    {
        Ready = 0,
        Activating = 1,
        Active = 2,
        Ending = 3,
    }

    /// <summary>
    /// Ability Entity 上唯一的跨帧状态组件。
    /// Definition identity、owner 反向引用和运行期 phase 放在同一个 component，避免 grant 后再维护拆分状态。
    /// </summary>
    public struct AbilityStateComponent : IComponentData
    {
        public int Code;
        public int Level;
        public Entity Owner;
        public EAbilityPhase Phase;
        public int RemainingFrame;
        public float Timer;

        public static AbilityStateComponent Create(int code, int level, Entity owner)
        {
            return new AbilityStateComponent
            {
                Code = code,
                Level = level > 0 ? level : 1,
                Owner = owner,
                Phase = EAbilityPhase.Ready,
                RemainingFrame = 0,
                Timer = 0f,
            };
        }
    }

    public sealed class ConfAbilityBaseInfo:AbilityComponentConfig
    {
        public int Code;
        public int Level;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.SetComponentData(ability, AbilityStateComponent.Create(Code, Level, Entity.Null));
        }
    }
}
