using Unity.Entities;

namespace GAS.Runtime
{
    public struct GEApplyRequestComponent : IComponentData
    {
        public Entity SourceAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public Entity Instigator;
        public Entity Causer;
        public int GameplayEffectCode;
        public int Level;
        public int ParentContextId;
        public int DurationFrameOverride;
    }

    [InternalBufferCapacity(8)]
    public struct GESetByCallerRequestValueBuffer : IBufferElementData
    {
        public int Key;
        public float Value;
    }
}
