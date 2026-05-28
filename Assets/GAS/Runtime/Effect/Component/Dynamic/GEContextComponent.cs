using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    /// <summary>
    /// GE instance 的上下文事实源。
    /// </summary>
    public struct GEContextComponent : IComponentData
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public Entity Instigator;
        public Entity Causer;
        public int ContextId;
        public int ParentContextId;
        public ETargetDataKind TargetDataKind;
    }

    /// <summary>
    /// GE instance 上保留的 TargetData 点位摘要。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct GETargetPointBuffer : IBufferElementData
    {
        public float3 Position;
    }

    /// <summary>
    /// GE instance 上保留的 TargetData 方向摘要。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct GETargetDirectionBuffer : IBufferElementData
    {
        public float3 Direction;
    }

    /// <summary>
    /// GE instance 上保留的 TargetData 命中摘要。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct GETargetHitBuffer : IBufferElementData
    {
        public Entity HitEntity;
        public float3 Position;
        public float3 Normal;
        public int SurfaceCode;
    }
}
