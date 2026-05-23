using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Ability 静态配置的 BlobAsset 引用，零拷贝共享。
    /// 包含 Code、Level、Cost、Cooldown 等只读配置。
    /// </summary>
    public struct CAbilityConfig : IComponentData
    {
        public BlobAssetReference<BlobAbilityConfig> Config;
    }

    public struct BlobAbilityConfig
    {
        public int Code;
        public int MaxLevel;
        public CTagMask AssetTags;
        public TagRequirementMask ActivationRequiredTags;
        public TagRequirementMask ActivationBlockedTags;
        public CTagMask ActivationOwnedTags;
        public CTagMask CancelAbilityTags;
        public CTagMask BlockAbilityTags;
        public CTagMask CooldownTags;
        public BlobArray<BlobAbilityCostModifier> CostModifiers;
        public float Cooldown;
        public float Cost;
    }

    public struct BlobAbilityCostModifier
    {
        public int AttrSetCode;
        public int AttributeCode;
        public float Magnitude;
        public EModifierOp Op;
    }
}
