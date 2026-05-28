using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE 静态配置的 BlobAsset 引用。
    /// </summary>
    public struct GEConfigRefComponent : IComponentData
    {
        public BlobAssetReference<BlobGEConfig> Config;
    }

    /// <summary>
    /// GE 静态定义 prototype 标记。runtime instance 由 prototype clone 后会移除此组件。
    /// </summary>
    public struct GEPrototypeComponent : IComponentData, IEnableableComponent
    {
        public int GameplayEffectCode;
    }

    public struct BlobGEConfig
    {
        public int DurationPolicy; // 0=Instant, 1=Duration, 2=Infinite
        public int DurationFrames;
        public int PeriodFrames;
        public BlobArray<ModifierDef> Modifiers;
        public BlobArray<int> GrantTags;
        public BlobArray<int> RemoveTags;
        public BlobArray<int> ApplicationRequiredTags;
        public BlobArray<int> ImmunityTags;
    }

    public struct ModifierDef
    {
        public int AttributeCode;
        public EModifierOp Op;
        public float Magnitude;
    }
}
