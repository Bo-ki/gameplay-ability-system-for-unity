using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    public enum ETargetDataKind : byte
    {
        Self = 0,
        Entity = 1,
        EntityList = 2,
    }

    /// <summary>
    /// TargetData 语义头。第一版 request 实体自身就是 TargetData 容器。
    /// </summary>
    public struct TargetDataHeaderComponent : IComponentData
    {
        public Entity SourceAsc;
        public Entity SourceAbility;
        public ETargetDataKind Kind;
    }

    /// <summary>
    /// TargetData 的目标 ASC 列表。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TargetEntityBuffer : IBufferElementData
    {
        public Entity TargetAsc;
    }

    /// <summary>
    /// TargetData 的点位事实。由输入、Timeline、Projectile 或测试 producer 写入。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TargetPointBuffer : IBufferElementData
    {
        public float3 Position;
    }

    /// <summary>
    /// TargetData 的方向事实。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TargetDirectionBuffer : IBufferElementData
    {
        public float3 Direction;
    }

    /// <summary>
    /// TargetData 的命中事实摘要。完整命中生命周期后续再提升为 ContextDataEntity。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TargetHitBuffer : IBufferElementData
    {
        public Entity HitEntity;
        public float3 Position;
        public float3 Normal;
        public int SurfaceCode;
    }
}
