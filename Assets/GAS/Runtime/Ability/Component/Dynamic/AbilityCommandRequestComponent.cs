using Unity.Entities;

namespace GAS.Runtime
{
    public enum EAbilityCommandType : byte
    {
        Grant,
        Activate,
        End,
        Cancel,
        Remove,
    }

    public struct AbilityCommandRequestComponent : IComponentData
    {
        public Entity Owner;
        public Entity AbilityEntity;
        public Entity TargetAsc;
        public int AbilityCode;
        public EAbilityCommandType CommandType;
    }

    [InternalBufferCapacity(0)]
    public struct AbilityCommandBuffer : IBufferElementData
    {
        public AbilityCommandRequestComponent Command;
    }
}
