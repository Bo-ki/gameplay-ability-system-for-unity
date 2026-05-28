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

        public TagMaskComponent AssetTags;
        public TagMaskComponent GrantedTags;
        public bool HasApplicationRequiredTags;
        public TagRequirementMask ApplicationRequiredTags;
        public bool HasOngoingRequiredTags;
        public TagRequirementMask OngoingRequiredTags;
        public bool HasRemoveGameplayEffectsWithTags;
        public TagRequirementMask RemoveGameplayEffectsWithTags;
        public bool HasImmunityTags;
        public TagRequirementMask ImmunityTags;

        public bool HasCueRequestOnApply;
        public int CueRequestOnApplyCode;

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
                || !entityManager.HasComponent<GEPrototypeComponent>(prototype)
                || !entityManager.IsComponentEnabled<GEPrototypeComponent>(prototype))
                return default;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GEStaticDefinitionBlob>();

            root.GameplayEffectCode = entityManager
                .GetComponentData<GEPrototypeComponent>(prototype)
                .GameplayEffectCode;

            CopyDuration(entityManager, prototype, ref root);
            CopyPeriod(builder, entityManager, prototype, ref root);
            CopyStacking(builder, entityManager, prototype, ref root);
            CopyTags(entityManager, prototype, ref root);
            CopyCueRequests(entityManager, prototype, ref root);
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
            if (!entityManager.IsComponentEnabled<GEDurationDefinitionComponent>(prototype))
                return;

            var duration = entityManager.GetComponentData<GEDurationDefinitionComponent>(prototype);
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
            if (entityManager.IsComponentEnabled<GEPeriodDefinitionComponent>(prototype))
            {
                var period = entityManager.GetComponentData<GEPeriodDefinitionComponent>(prototype);
                root.HasPeriod = true;
                root.Period = new GEPeriodDefinition
                {
                    Period = period.Period,
                    ResetTimeCountWhenDeactivated = period.ResetTimeCountWhenDeactivated,
                };
            }

            var length = entityManager.IsComponentEnabled<GEPeriodConfigBuffer>(prototype)
                ? entityManager.GetBuffer<GEPeriodConfigBuffer>(prototype).Length
                : 0;
            var target = builder.Allocate(ref root.PeriodEffectCodes, length);
            if (length == 0)
                return;

            var source = entityManager.GetBuffer<GEPeriodConfigBuffer>(prototype);
            for (var i = 0; i < source.Length; i++)
                target[i] = source[i].GameplayEffectCode;
        }

        private static void CopyStacking(
            BlobBuilder builder,
            EntityManager entityManager,
            Entity prototype,
            ref GEStaticDefinitionBlob root)
        {
            if (entityManager.IsComponentEnabled<GEStackingDefinitionComponent>(prototype))
            {
                var stacking = entityManager.GetComponentData<GEStackingDefinitionComponent>(prototype);
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

            var length = entityManager.IsComponentEnabled<GEOverflowConfigBuffer>(prototype)
                ? entityManager.GetBuffer<GEOverflowConfigBuffer>(prototype).Length
                : 0;
            var target = builder.Allocate(ref root.OverflowEffectCodes, length);
            if (length == 0)
                return;

            var source = entityManager.GetBuffer<GEOverflowConfigBuffer>(prototype);
            for (var i = 0; i < source.Length; i++)
                target[i] = source[i].GameplayEffectCode;
        }

        private static void CopyTags(
            EntityManager entityManager,
            Entity prototype,
            ref GEStaticDefinitionBlob root)
        {
            if (entityManager.IsComponentEnabled<GEAssetTagsComponent>(prototype))
                root.AssetTags = entityManager.GetComponentData<GEAssetTagsComponent>(prototype).Tags;

            if (entityManager.IsComponentEnabled<GEGrantedTagsComponent>(prototype))
                root.GrantedTags = entityManager.GetComponentData<GEGrantedTagsComponent>(prototype).Tags;
            else if (entityManager.IsComponentEnabled<GEGrantedTagConfigBuffer>(prototype))
                root.GrantedTags = CreateMask(entityManager.GetBuffer<GEGrantedTagConfigBuffer>(prototype));

            if (entityManager.IsComponentEnabled<GEApplicationRequiredTagsComponent>(prototype))
            {
                root.HasApplicationRequiredTags = true;
                root.ApplicationRequiredTags = entityManager
                    .GetComponentData<GEApplicationRequiredTagsComponent>(prototype)
                    .requirement;
            }

            if (entityManager.IsComponentEnabled<GEOngoingRequiredTagsComponent>(prototype))
            {
                root.HasOngoingRequiredTags = true;
                root.OngoingRequiredTags = entityManager
                    .GetComponentData<GEOngoingRequiredTagsComponent>(prototype)
                    .requirement;
            }

            if (entityManager.IsComponentEnabled<GERemoveEffectWithTagsComponent>(prototype))
            {
                root.HasRemoveGameplayEffectsWithTags = true;
                root.RemoveGameplayEffectsWithTags = entityManager
                    .GetComponentData<GERemoveEffectWithTagsComponent>(prototype)
                    .requirement;
            }

            if (entityManager.IsComponentEnabled<GEImmunityTagsComponent>(prototype))
            {
                root.HasImmunityTags = true;
                root.ImmunityTags = entityManager
                    .GetComponentData<GEImmunityTagsComponent>(prototype)
                    .requirement;
            }
        }

        private static TagMaskComponent CreateMask(DynamicBuffer<GEGrantedTagConfigBuffer> grantedTags)
        {
            var mask = new TagMaskComponent();
            for (var i = 0; i < grantedTags.Length; i++)
                mask.AddTag(grantedTags[i].TagIndex);
            return mask;
        }

        private static void CopyCueRequests(
            EntityManager entityManager,
            Entity prototype,
            ref GEStaticDefinitionBlob root)
        {
            if (!entityManager.IsComponentEnabled<GECueRequestOnApplyComponent>(prototype))
                return;

            var cue = entityManager.GetComponentData<GECueRequestOnApplyComponent>(prototype);
            if (cue.CueCode <= 0)
                return;

            root.HasCueRequestOnApply = true;
            root.CueRequestOnApplyCode = cue.CueCode;
        }

        private static void CopyModifiers(
            BlobBuilder builder,
            EntityManager entityManager,
            Entity prototype,
            ref BlobArray<GEModifierDefinition> targetArray)
        {
            var length = entityManager.IsComponentEnabled<GEModifierConfigBuffer>(prototype)
                ? entityManager.GetBuffer<GEModifierConfigBuffer>(prototype).Length
                : 0;
            var target = builder.Allocate(ref targetArray, length);
            if (length == 0)
                return;

            var modifiers = entityManager.GetBuffer<GEModifierConfigBuffer>(prototype);
            var hasMagnitudeDefinitions = entityManager.IsComponentEnabled<GEMagnitudeDefinitionBuffer>(prototype);
            var magnitudeDefinitions = hasMagnitudeDefinitions
                ? entityManager.GetBuffer<GEMagnitudeDefinitionBuffer>(prototype)
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
            DynamicBuffer<GEMagnitudeDefinitionBuffer> definitions,
            int modifierIndex,
            out GEMagnitudeDefinitionBuffer definition)
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
            var length = entityManager.IsComponentEnabled<GEGrantedAbilityConfigBuffer>(prototype)
                ? entityManager.GetBuffer<GEGrantedAbilityConfigBuffer>(prototype).Length
                : 0;
            var target = builder.Allocate(ref targetArray, length);
            if (length == 0)
                return;

            var source = entityManager.GetBuffer<GEGrantedAbilityConfigBuffer>(prototype);
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
