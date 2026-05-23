using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 能力结束请求。自然结束、Timeline 结束和外部显式 End 都统一进入 cleanup。
    /// </summary>
    public struct CAbilityInTryEnd : IComponentData
    {
        public EAbilityLifecycleReason Reason;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public int SourceAbilityCode;
    }
}
