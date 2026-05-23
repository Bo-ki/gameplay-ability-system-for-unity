using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GameplayEffect 施加请求。由 Ability / 外部命令写入，SApplyGameplayEffectRequest 消费。
    /// </summary>
    public struct CApplyGameplayEffectRequest : IComponentData
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

    /// <summary>
    /// SetByCaller 数值通道。可挂在 request 或 GE instance 上。
    /// </summary>
    public struct BSetByCallerValue : IBufferElementData
    {
        public int Key;
        public float Value;
    }
}
