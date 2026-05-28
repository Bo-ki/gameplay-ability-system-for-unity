using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC Entity 上的 granted ability slot 列表。
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AbilitySlotBuffer : IBufferElementData
    {
        public Entity AbilityEntity;
    }
}
