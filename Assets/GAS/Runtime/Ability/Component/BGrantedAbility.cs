using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC Entity 上持有的 Ability 列表，替代原 BAbility。
    /// 每个元素引用一个 Ability Entity。
    /// </summary>
    public struct BGrantedAbility : IBufferElementData
    {
        public Entity AbilityEntity;
    }
}
