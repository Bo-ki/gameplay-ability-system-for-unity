using Unity.Entities;

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

}
