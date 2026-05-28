using Unity.Entities;

namespace GAS.Runtime
{
    public enum EAttributeThresholdAbilityLifecycleRequestType : byte
    {
        End = 0,
        Cancel = 1,
    }

    /// <summary>
    /// Rule entity or ASC component that requests an ability lifecycle transition when an attribute reaches a threshold.
    /// </summary>
    public struct AbilityAttributeThresholdLifecycleRuleComponent : IComponentData
    {
        public Entity OwnerAsc;
        public int AttrSetCode;
        public int AttrCode;
        public float Threshold;
        public int AbilityCode;
        public EAttributeThresholdAbilityLifecycleRequestType RequestType;
        public EAbilityLifecycleReason Reason;
    }
}
