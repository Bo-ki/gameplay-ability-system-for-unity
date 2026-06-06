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

    public struct AbilityCommand
    {
        public Entity Owner;
        public Entity AbilityEntity;
        public Entity TargetAsc;
        public int AbilityCode;
        public EAbilityCommandType CommandType;
    }

    [InternalBufferCapacity(4)]
    public struct AbilityCommandBuffer : IBufferElementData
    {
        public AbilityCommand Command;
    }
}
