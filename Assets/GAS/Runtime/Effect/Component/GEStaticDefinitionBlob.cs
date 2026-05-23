using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GameplayEffect 的静态定义 Blob。只承载 Definition，不承载本次施加的 Spec/Context 或运行时状态。
    /// </summary>
    public struct GEStaticDefinitionBlob
    {
        public int GameplayEffectCode;

        public bool HasDuration;
        public GEDurationDefinition Duration;

        public bool HasPeriod;
        public GEPeriodDefinition Period;
        public BlobArray<int> PeriodEffectCodes;

        public bool HasStacking;
        public GEStackingDefinition Stacking;
        public BlobArray<int> OverflowEffectCodes;

        public CTagMask AssetTags;
        public CTagMask GrantedTags;
        public bool HasApplicationRequiredTags;
        public TagRequirementMask ApplicationRequiredTags;
        public bool HasOngoingRequiredTags;
        public TagRequirementMask OngoingRequiredTags;
        public bool HasRemoveGameplayEffectsWithTags;
        public TagRequirementMask RemoveGameplayEffectsWithTags;
        public bool HasImmunityTags;
        public TagRequirementMask ImmunityTags;

        public BlobArray<GEModifierDefinition> Modifiers;
        public BlobArray<GEGrantedAbilityDefinition> GrantedAbilities;
    }

    public struct GEDurationDefinition
    {
        public int Duration;
        public TimeUnit TimeUnit;
        public bool ResetStartTimeWhenActivated;
        public bool StopTickWhenDeactivated;
    }

    public struct GEPeriodDefinition
    {
        public int Period;
        public bool ResetTimeCountWhenDeactivated;
    }

    public struct GEStackingDefinition
    {
        public EffectStackType StackType;
        public int StackingCode;
        public int LimitCount;
        public EffectDurationRefreshPolicy EffectDurationRefreshPolicy;
        public EffectPeriodResetPolicy EffectPeriodResetPolicy;
        public EffectExpirationPolicy EffectExpirationPolicy;
        public bool DenyOverflowApplication;
        public bool ClearStackOnOverflow;
    }

    public struct GEModifierDefinition
    {
        public int ModifierIndex;
        public int AttrSetCode;
        public int AttributeCode;
        public EModifierOp Operation;
        public float Magnitude;

        public EMagnitudeSource MagnitudeSource;
        public int MagnitudeAttributeSetCode;
        public int MagnitudeAttributeCode;
        public int MagnitudeKey;
        public EAttributeCaptureTiming MagnitudeCaptureTiming;
        public float FallbackMagnitude;
        public float Coefficient;
        public float PreAdd;
        public float PostAdd;
    }

    public struct GEGrantedAbilityDefinition
    {
        public int AbilityCode;
        public int Level;
        public GrantedAbilityActivationPolicy ActivationPolicy;
        public GrantedAbilityDeactivationPolicy DeactivationPolicy;
        public GrantedAbilityRemovePolicy RemovePolicy;
    }

    public static class GEStaticDefinitionBlobBuilder
    {
        public static BlobAssetReference<GEStaticDefinitionBlob> BuildFromPrototype(
            EntityManager entityManager,
            Entity prototype,
            Allocator allocator = Allocator.Persistent)
        {
            if (prototype == Entity.Null
                || !entityManager.Exists(prototype)
                || !entityManager.HasComponent<CGameplayEffectPrototype>(prototype))
                return default;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GEStaticDefinitionBlob>();

            root.GameplayEffectCode = entityManager
                .GetComponentData<CGameplayEffectPrototype>(prototype)
                .GameplayEffectCode;

            CopyDuration(entityManager, prototype, ref root);
            CopyPeriod(builder, entityManager, prototype, ref root);
            CopyStacking(builder, entityManager, prototype, ref root);
            CopyTags(entityManager, prototype, ref root);
            CopyModifiers(builder, entityManager, prototype, ref root.Modifiers);
            CopyGrantedAbilities(builder, entityManager, prototype, ref root.GrantedAbilities);

            var blob = builder.CreateBlobAssetReference<GEStaticDefinitionBlob>(allocator);
            builder.Dispose();
            return blob;
        }

        private static void CopyDuration(
            EntityManager entityManager,
            Entity prototype,
            ref GEStaticDefinitionBlob root)
        {
            if (!entityManager.HasComponent<CDurationDefinition>(prototype))
                return;

            var duration = entityManager.GetComponentData<CDurationDefinition>(prototype);
            root.HasDuration = true;
            root.Duration = new GEDurationDefinition
            {
                Duration = duration.Duration,
                TimeUnit = duration.TimeUnit,
                ResetStartTimeWhenActivated = duration.ResetStartTimeWhenActivated,
                StopTickWhenDeactivated = duration.StopTickWhenDeactivated,
            };
        }

        private static void CopyPeriod(
            BlobBuilder builder,
            EntityManager entityManager,
            Entity prototype,
            ref GEStaticDefinitionBlob root)
        {
            if (entityManager.HasComponent<CPeriodDefinition>(prototype))
            {
                var period = entityManager.GetComponentData<CPeriodDefinition>(prototype);
                root.HasPeriod = true;
                root.Period = new GEPeriodDefinition
                {
                    Period = period.Period,
                    ResetTimeCountWhenDeactivated = period.ResetTimeCountWhenDeactivated,
                };
            }

            var length = entityManager.HasBuffer<BPeriodGEConfig>(prototype)
                ? entityManager.GetBuffer<BPeriodGEConfig>(prototype).Length
                : 0;
            var target = builder.Allocate(ref root.PeriodEffectCodes, length);
            if (length == 0)
                return;

            var source = entityManager.GetBuffer<BPeriodGEConfig>(prototype);
            for (var i = 0; i < source.Length; i++)
                target[i] = source[i].GameplayEffectCode;
        }

        private static void CopyStacking(
            BlobBuilder builder,
            EntityManager entityManager,
            Entity prototype,
            ref GEStaticDefinitionBlob root)
        {
            if (entityManager.HasComponent<CStackingDefinition>(prototype))
            {
                var stacking = entityManager.GetComponentData<CStackingDefinition>(prototype);
                root.HasStacking = true;
                root.Stacking = new GEStackingDefinition
                {
                    StackType = stacking.StackType,
                    StackingCode = stacking.StackingCode,
                    LimitCount = stacking.LimitCount,
                    EffectDurationRefreshPolicy = stacking.EffectDurationRefreshPolicy,
                    EffectPeriodResetPolicy = stacking.EffectPeriodResetPolicy,
                    EffectExpirationPolicy = stacking.EffectExpirationPolicy,
                    DenyOverflowApplication = stacking.DenyOverflowApplication,
                    ClearStackOnOverflow = stacking.ClearStackOnOverflow,
                };
            }

            var length = entityManager.HasBuffer<BOverflowGEConfig>(prototype)
                ? entityManager.GetBuffer<BOverflowGEConfig>(prototype).Length
                : 0;
            var target = builder.Allocate(ref root.OverflowEffectCodes, length);
            if (length == 0)
                return;

            var source = entityManager.GetBuffer<BOverflowGEConfig>(prototype);
            for (var i = 0; i < source.Length; i++)
                target[i] = source[i].GameplayEffectCode;
        }

        private static void CopyTags(
            EntityManager entityManager,
            Entity prototype,
            ref GEStaticDefinitionBlob root)
        {
            if (entityManager.HasComponent<CEffectAssetTags>(prototype))
                root.AssetTags = entityManager.GetComponentData<CEffectAssetTags>(prototype).Tags;

            if (entityManager.HasComponent<CEffectGrantedTags>(prototype))
                root.GrantedTags = entityManager.GetComponentData<CEffectGrantedTags>(prototype).Tags;
            else if (entityManager.HasBuffer<BGrantedTagConfig>(prototype))
                root.GrantedTags = CreateMask(entityManager.GetBuffer<BGrantedTagConfig>(prototype));

            if (entityManager.HasComponent<CApplicationRequiredTags>(prototype))
            {
                root.HasApplicationRequiredTags = true;
                root.ApplicationRequiredTags = entityManager
                    .GetComponentData<CApplicationRequiredTags>(prototype)
                    .requirement;
            }

            if (entityManager.HasComponent<COngoingRequiredTags>(prototype))
            {
                root.HasOngoingRequiredTags = true;
                root.OngoingRequiredTags = entityManager
                    .GetComponentData<COngoingRequiredTags>(prototype)
                    .requirement;
            }

            if (entityManager.HasComponent<CRemoveEffectWithTags>(prototype))
            {
                root.HasRemoveGameplayEffectsWithTags = true;
                root.RemoveGameplayEffectsWithTags = entityManager
                    .GetComponentData<CRemoveEffectWithTags>(prototype)
                    .requirement;
            }

            if (entityManager.HasComponent<CEffectImmunityTags>(prototype))
            {
                root.HasImmunityTags = true;
                root.ImmunityTags = entityManager
                    .GetComponentData<CEffectImmunityTags>(prototype)
                    .requirement;
            }
        }

        private static CTagMask CreateMask(DynamicBuffer<BGrantedTagConfig> grantedTags)
        {
            var mask = new CTagMask();
            for (var i = 0; i < grantedTags.Length; i++)
                mask.AddTag(grantedTags[i].TagIndex);
            return mask;
        }

        private static void CopyModifiers(
            BlobBuilder builder,
            EntityManager entityManager,
            Entity prototype,
            ref BlobArray<GEModifierDefinition> targetArray)
        {
            var length = entityManager.HasBuffer<BModifierConfig>(prototype)
                ? entityManager.GetBuffer<BModifierConfig>(prototype).Length
                : 0;
            var target = builder.Allocate(ref targetArray, length);
            if (length == 0)
                return;

            var modifiers = entityManager.GetBuffer<BModifierConfig>(prototype);
            var hasMagnitudeDefinitions = entityManager.HasBuffer<BMagnitudeDefinition>(prototype);
            var magnitudeDefinitions = hasMagnitudeDefinitions
                ? entityManager.GetBuffer<BMagnitudeDefinition>(prototype)
                : default;

            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                var definition = new GEModifierDefinition
                {
                    ModifierIndex = i,
                    AttrSetCode = modifier.AttrSetCode,
                    AttributeCode = modifier.AttributeCode,
                    Operation = modifier.Op,
                    Magnitude = modifier.Magnitude,
                    MagnitudeSource = EMagnitudeSource.Constant,
                    FallbackMagnitude = modifier.Magnitude,
                    Coefficient = 1f,
                };

                if (hasMagnitudeDefinitions
                    && TryGetMagnitudeDefinition(magnitudeDefinitions, i, out var magnitudeDefinition))
                {
                    definition.MagnitudeSource = magnitudeDefinition.Source;
                    definition.MagnitudeAttributeSetCode = magnitudeDefinition.AttributeSetCode;
                    definition.MagnitudeAttributeCode = magnitudeDefinition.AttributeCode;
                    definition.MagnitudeKey = magnitudeDefinition.Key;
                    definition.MagnitudeCaptureTiming = magnitudeDefinition.CaptureTiming;
                    definition.FallbackMagnitude = magnitudeDefinition.FallbackMagnitude;
                    definition.Coefficient = magnitudeDefinition.Coefficient == 0 ? 1f : magnitudeDefinition.Coefficient;
                    definition.PreAdd = magnitudeDefinition.PreAdd;
                    definition.PostAdd = magnitudeDefinition.PostAdd;
                }

                target[i] = definition;
            }
        }

        private static bool TryGetMagnitudeDefinition(
            DynamicBuffer<BMagnitudeDefinition> definitions,
            int modifierIndex,
            out BMagnitudeDefinition definition)
        {
            for (var i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].ModifierIndex != modifierIndex)
                    continue;

                definition = definitions[i];
                return true;
            }

            definition = default;
            return false;
        }

        private static void CopyGrantedAbilities(
            BlobBuilder builder,
            EntityManager entityManager,
            Entity prototype,
            ref BlobArray<GEGrantedAbilityDefinition> targetArray)
        {
            var length = entityManager.HasBuffer<BGrantedAbilityConfig>(prototype)
                ? entityManager.GetBuffer<BGrantedAbilityConfig>(prototype).Length
                : 0;
            var target = builder.Allocate(ref targetArray, length);
            if (length == 0)
                return;

            var source = entityManager.GetBuffer<BGrantedAbilityConfig>(prototype);
            for (var i = 0; i < source.Length; i++)
            {
                target[i] = new GEGrantedAbilityDefinition
                {
                    AbilityCode = source[i].AbilityCode,
                    Level = source[i].Level,
                    ActivationPolicy = source[i].ActivationPolicy,
                    DeactivationPolicy = source[i].DeactivationPolicy,
                    RemovePolicy = source[i].RemovePolicy,
                };
            }
        }
    }
}
