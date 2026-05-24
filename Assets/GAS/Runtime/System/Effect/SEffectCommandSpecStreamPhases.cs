using Unity.Entities;
using static GAS.Runtime.EffectCommandSpecStreamPhaseUtility;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SRemoveGameplayEffectRequest))]
    public partial struct SEffectCommandIngest : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SEffectCommandIngest))]
    public partial struct SInstantEffectSpecBuild : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var commands = em.GetBuffer<BEffectCommand>(streamEntity);
            var specs = em.GetBuffer<BInstantEffectSpec>(streamEntity);
            var setByCallerValues = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);

            var start = ClampCursor(stream.SpecBuildCommandCursor, commands.Length);
            for (var i = start; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.Kind != EEffectCommandKind.Instant
                    || !CanBuildSimpleInstantSpec(em, in command))
                {
                    continue;
                }

                var spec = new BInstantEffectSpec
                {
                    Sequence = Allocate(ref stream.NextSpecSequence),
                    SourceCommandSequence = command.Sequence,
                    Frame = command.Frame,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = command.SourceEffect,
                    Instigator = command.Instigator,
                    Causer = command.Causer,
                    GameplayEffectCode = command.GameplayEffectCode,
                    Level = command.Level,
                    StackCount = 1,
                    DurationFrameOverride = command.DurationFrameOverride,
                    ContextId = command.ContextId,
                    ParentContextId = command.ParentContextId,
                    TargetDataKind = command.TargetDataKind,
                    SetByCallerStart = command.SetByCallerStart,
                    SetByCallerCount = command.SetByCallerCount,
                    Flags = command.Flags,
                };

                AssignSpecSequence(
                    setByCallerValues,
                    spec.SetByCallerStart,
                    spec.SetByCallerCount,
                    spec.SourceCommandSequence,
                    spec.Sequence);
                specs.Add(spec);
            }

            stream.SpecBuildCommandCursor = commands.Length;
            em.SetComponentData(streamEntity, stream);
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SActiveEffectMutationApply))]
    public partial struct SAttributeDeltaApply : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var specs = em.GetBuffer<BInstantEffectSpec>(streamEntity);
            var deltas = em.GetBuffer<BAttributeDelta>(streamEntity);
            var setByCallerValues = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);

            var start = ClampCursor(stream.DeltaApplySpecCursor, specs.Length);
            for (var i = start; i < specs.Length; i++)
                ApplySimpleInstantSpec(em, ref stream, specs[i], setByCallerValues, deltas);

            stream.DeltaApplySpecCursor = specs.Length;
            em.SetComponentData(streamEntity, stream);
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SAttributeDeltaApply))]
    [UpdateBefore(typeof(SAscDestroyRequest))]
    public partial struct STypedSimulationFactProjection : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var deltas = em.GetBuffer<BAttributeDelta>(streamEntity);
            var facts = em.GetBuffer<BTypedSimulationFact>(streamEntity);

            var start = ClampCursor(stream.FactProjectionDeltaCursor, deltas.Length);
            for (var i = start; i < deltas.Length; i++)
            {
                var delta = deltas[i];
                facts.Add(new BTypedSimulationFact
                {
                    Sequence = Allocate(ref stream.NextFactSequence),
                    SourceCommandSequence = delta.SourceCommandSequence,
                    SourceSpecSequence = delta.SourceSpecSequence,
                    SourceDeltaSequence = delta.Sequence,
                    Frame = delta.Frame,
                    EventType = EGameplayEventType.AttributeBaseValueChanged,
                    Domain = EGameplayFactDomain.Attribute,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = delta.SourceAsc,
                    TargetAsc = delta.TargetAsc,
                    SourceAbility = delta.SourceAbility,
                    SourceEffect = delta.SourceEffect,
                    GameplayEffectCode = delta.GameplayEffectCode,
                    ContextId = delta.ContextId,
                    ParentContextId = delta.ParentContextId,
                    AttrSetCode = delta.AttrSetCode,
                    AttributeCode = delta.AttributeCode,
                    Value = delta.Magnitude,
                    OldValue = delta.OldValue,
                    NewValue = delta.NewValue,
                });
            }

            stream.FactProjectionDeltaCursor = deltas.Length;
            em.SetComponentData(streamEntity, stream);
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SInstantEffectSpecBuild))]
    public partial struct SActiveEffectMutationApply : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var commands = em.GetBuffer<BEffectCommand>(streamEntity);
            stream.ActiveMutationCommandCursor = commands.Length;
            em.SetComponentData(streamEntity, stream);
        }
    }

    internal static class EffectCommandSpecStreamPhaseUtility
    {
        public static bool CanBuildSimpleInstantSpec(
            EntityManager em,
            in BEffectCommand command)
        {
            if (command.GameplayEffectCode <= 0
                || command.TargetAsc == Entity.Null
                || IsUnavailableAsc(em, command.SourceAsc)
                || IsUnavailableAsc(em, command.TargetAsc)
                || !GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                    em,
                    command.GameplayEffectCode,
                    out var blob,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        command.GameplayEffectCode,
                        ConfigRegistryReferenceKind.ApplyGameplayEffectRequest)))
            {
                return false;
            }

            ref var definition = ref blob.Value;
            return CanApplySimpleInstantSpec(ref definition);
        }

        public static bool CanApplySimpleInstantSpec(ref GEStaticDefinitionBlob definition)
        {
            if (definition.HasDuration
                || definition.HasPeriod
                || definition.HasStacking
                || definition.HasApplicationRequiredTags
                || definition.HasOngoingRequiredTags
                || definition.HasRemoveGameplayEffectsWithTags
                || definition.HasImmunityTags
                || definition.HasCueRequestOnApply
                || !definition.GrantedTags.IsEmpty
                || definition.GrantedAbilities.Length != 0
                || definition.Modifiers.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < definition.Modifiers.Length; i++)
            {
                if (!CanResolveSimpleInstantMagnitude(in definition.Modifiers[i]))
                    return false;
            }

            return true;
        }

        public static bool CanResolveSimpleInstantMagnitude(in GEModifierDefinition modifier)
        {
            return modifier.MagnitudeSource == EMagnitudeSource.Constant
                   || modifier.MagnitudeSource == EMagnitudeSource.SetByCaller;
        }

        public static void ApplySimpleInstantSpec(
            EntityManager em,
            ref CEffectCommandSpecStream stream,
            in BInstantEffectSpec spec,
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues,
            DynamicBuffer<BAttributeDelta> deltas)
        {
            if (spec.TargetAsc == Entity.Null
                || !em.Exists(spec.TargetAsc)
                || !em.HasBuffer<BAttribute>(spec.TargetAsc)
                || !GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                    em,
                    spec.GameplayEffectCode,
                    out var blob,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        spec.GameplayEffectCode,
                        ConfigRegistryReferenceKind.ApplyGameplayEffectRequest)))
            {
                return;
            }

            ref var definition = ref blob.Value;
            if (!CanApplySimpleInstantSpec(ref definition))
                return;

            var attributes = em.GetBuffer<BAttribute>(spec.TargetAsc);
            for (var i = 0; i < definition.Modifiers.Length; i++)
            {
                var modifier = definition.Modifiers[i];
                if (!TryResolveSimpleInstantMagnitude(
                        in spec,
                        setByCallerValues,
                        in modifier,
                        out var magnitude))
                {
                    continue;
                }

                var attrIndex = attributes.IndexOfAttribute(modifier.AttrSetCode, modifier.AttributeCode);
                if (attrIndex < 0)
                    continue;

                var attribute = attributes[attrIndex];
                var oldValue = attribute.BaseValue;
                var oldCurrentValue = attribute.CurrentValue;
                var newValue = AttributeHelper.ApplyModifier(attribute.BaseValue, modifier.Operation, magnitude);
                attribute.CurrentValue = newValue;
                AttributeHelper.Clamp(ref attribute);
                newValue = attribute.CurrentValue;
                attribute.BaseValue = newValue;
                attribute.CurrentValue = newValue;

                if (newValue != oldValue)
                {
                    attribute.Dirty = true;
                    if (oldCurrentValue != attribute.CurrentValue)
                    {
                        attribute.PreviousCurrentValue = oldCurrentValue;
                        attribute.CurrentValueChangePending = true;
                    }

                    deltas.Add(new BAttributeDelta
                    {
                        Sequence = Allocate(ref stream.NextDeltaSequence),
                        SourceCommandSequence = spec.SourceCommandSequence,
                        SourceSpecSequence = spec.Sequence,
                        Frame = spec.Frame,
                        SourceAsc = spec.SourceAsc,
                        TargetAsc = spec.TargetAsc,
                        SourceAbility = spec.SourceAbility,
                        SourceEffect = spec.SourceEffect,
                        GameplayEffectCode = spec.GameplayEffectCode,
                        ContextId = spec.ContextId,
                        ParentContextId = spec.ParentContextId,
                        AttrSetCode = modifier.AttrSetCode,
                        AttributeCode = modifier.AttributeCode,
                        Op = modifier.Operation,
                        ValueKind = EAttributeDeltaValueKind.BaseValue,
                        Magnitude = magnitude,
                        OldValue = oldValue,
                        NewValue = newValue,
                    });
                }

                attributes[attrIndex] = attribute;
            }
        }

        public static bool TryResolveSimpleInstantMagnitude(
            in BInstantEffectSpec spec,
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues,
            in GEModifierDefinition modifier,
            out float magnitude)
        {
            switch (modifier.MagnitudeSource)
            {
                case EMagnitudeSource.Constant:
                    magnitude = ApplySimpleInstantMagnitudeTransform(modifier.Magnitude, in modifier);
                    return true;
                case EMagnitudeSource.SetByCaller:
                    var value = modifier.FallbackMagnitude;
                    var start = spec.SetByCallerStart;
                    var end = start + spec.SetByCallerCount;
                    if (start < 0)
                        start = 0;
                    if (end > setByCallerValues.Length)
                        end = setByCallerValues.Length;

                    for (var i = start; i < end; i++)
                    {
                        var setByCaller = setByCallerValues[i];
                        if (setByCaller.Key != modifier.MagnitudeKey
                            || setByCaller.CommandSequence != spec.SourceCommandSequence)
                        {
                            continue;
                        }

                        value = setByCaller.Value;
                        break;
                    }

                    magnitude = ApplySimpleInstantMagnitudeTransform(value, in modifier);
                    return true;
                default:
                    magnitude = 0f;
                    return false;
            }
        }

        public static float ApplySimpleInstantMagnitudeTransform(
            float rawMagnitude,
            in GEModifierDefinition modifier)
        {
            var coefficient = modifier.Coefficient == 0f ? 1f : modifier.Coefficient;
            return ((rawMagnitude + modifier.PreAdd) * coefficient) + modifier.PostAdd;
        }

        public static void AssignSpecSequence(
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues,
            int start,
            int count,
            int commandSequence,
            int specSequence)
        {
            if (count <= 0)
                return;

            var end = start + count;
            if (start < 0)
                start = 0;
            if (end > setByCallerValues.Length)
                end = setByCallerValues.Length;

            for (var i = start; i < end; i++)
            {
                var value = setByCallerValues[i];
                if (value.CommandSequence != commandSequence)
                    continue;

                value.SpecSequence = specSequence;
                setByCallerValues[i] = value;
            }
        }

        public static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0)
                return 0;
            return cursor > length ? length : cursor;
        }

        public static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }

        private static bool IsUnavailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null && (!em.Exists(asc) || em.HasComponent<CAscDestroying>(asc));
        }
    }
}
