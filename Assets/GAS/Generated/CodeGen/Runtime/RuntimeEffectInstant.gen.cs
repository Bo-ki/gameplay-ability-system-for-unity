///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEEffectCommandCatalogNormalizeSystem))]
    [UpdateBefore(typeof(GASActiveEffectMutationApplySystem))]
    public partial struct GEEffectSpecBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;
            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            var specs = em.GetBuffer<GEEffectSpecBuffer>(streamEntity);
            var setByCallerValues = em.GetBuffer<GESetByCallerValueBuffer>(streamEntity);
            ref var catalog = ref catalogComponent.Catalog.Value;

            var start = ClampCursor(stream.SpecBuildCommandCursor, commands.Length);
            for (var i = start; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.Kind != GEEffectCommandKind.Instant)
                    continue;
                if (!CanBuildInstantSpec(ref catalog, in command, em, out var gameplayEffectIndex))
                    continue;

                specs.Add(new GEEffectSpecBuffer
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
                    CueRequestOnApplyCode = GetCueRequestOnApply(ref catalog, gameplayEffectIndex),
                    Level = command.Level,
                    StackCount = 1,
                    DurationFrameOverride = command.DurationFrameOverride,
                    ContextId = command.ContextId,
                    ParentContextId = command.ParentContextId,
                    TargetDataKind = command.TargetDataKind,
                    SetByCallerStart = command.SetByCallerStart,
                    SetByCallerCount = command.SetByCallerCount,
                    Flags = gameplayEffectIndex,
                });
                AssignSpecSequence(setByCallerValues, command.SetByCallerStart, command.SetByCallerCount, command.Sequence, specs[specs.Length - 1].Sequence);
            }

            stream.SpecBuildCommandCursor = commands.Length;
            em.SetComponentData(streamEntity, stream);
        }

        private static bool CanBuildInstantSpec(ref GASDefinitionCatalogBlob catalog, in GEEffectCommandBuffer command, EntityManager em, out int gameplayEffectIndex)
        {
            gameplayEffectIndex = -1;
            if (command.GameplayEffectCode <= 0
                || command.TargetAsc == Entity.Null
                || IsUnavailableAsc(em, command.SourceAsc)
                || IsUnavailableAsc(em, command.TargetAsc)
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out gameplayEffectIndex))
                return false;

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            return gameplayEffect.DurationFrames <= 0
                && gameplayEffect.PeriodFrames <= 0
                && gameplayEffect.StackLimitCount <= 0
                && gameplayEffect.GrantedTagMaskIndex < 0
                && gameplayEffect.RemoveGameplayEffectTagMaskIndex < 0
                && gameplayEffect.GrantedAbilityCount == 0
                && gameplayEffect.ModifierCount > 0;
        }

        private static int GetCueRequestOnApply(ref GASDefinitionCatalogBlob catalog, int gameplayEffectIndex)
        {
            if ((uint)gameplayEffectIndex >= (uint)catalog.GameplayEffects.Length)
                return 0;
            return catalog.GameplayEffects[gameplayEffectIndex].GameplayCueCode;
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;
            return next++;
        }

        private static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0) return 0;
            if (cursor > length) return length;
            return cursor;
        }

        private static bool IsUnavailableAsc(EntityManager em, Entity asc)
        {
            return asc == Entity.Null
                || !em.Exists(asc)
                || ASCEntityFactory.IsDestroying(em, asc);
        }

        private static void AssignSpecSequence(
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            int start,
            int count,
            int commandSequence,
            int specSequence)
        {
            if (count <= 0)
                return;
            if (start < 0) start = 0;
            var end = start + count;
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
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectMutationApplySystem))]
    [UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]
    [UpdateBefore(typeof(GameplayFactProjectionSystem))]
    public partial struct GASAttributeSetReduceApplySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;
            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var specs = em.GetBuffer<GEEffectSpecBuffer>(streamEntity);
            var deltas = em.GetBuffer<AttributeModifierBuffer>(streamEntity);
            var setByCallerValues = em.GetBuffer<GESetByCallerValueBuffer>(streamEntity);
            ref var catalog = ref catalogComponent.Catalog.Value;

            var start = ClampCursor(stream.DeltaApplySpecCursor, specs.Length);
            for (var i = start; i < specs.Length; i++)
                ApplySpec(em, ref stream, ref catalog, specs[i], setByCallerValues, deltas);

            stream.DeltaApplySpecCursor = specs.Length;
            em.SetComponentData(streamEntity, stream);
        }

        private static void ApplySpec(
            EntityManager em,
            ref GEEffectCommandStreamComponent stream,
            ref GASDefinitionCatalogBlob catalog,
            in GEEffectSpecBuffer spec,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            DynamicBuffer<AttributeModifierBuffer> deltas)
        {
            if (spec.TargetAsc == Entity.Null
                || !em.Exists(spec.TargetAsc)
                || !em.HasBuffer<AttributeValueBuffer>(spec.TargetAsc)
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, spec.GameplayEffectCode, out var gameplayEffectIndex))
                return;

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            var attributes = em.GetBuffer<AttributeValueBuffer>(spec.TargetAsc);
            for (var i = 0; i < gameplayEffect.ModifierCount; i++)
            {
                var modifierIndex = gameplayEffect.ModifierStart + i;
                if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                    continue;

                var modifier = catalog.Modifiers[modifierIndex];
                var context = BuildMagnitudeContext(in spec, setByCallerValues, in modifier);
                if (!GASGeneratedMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
                    continue;

                var attrIndex = attributes.IndexOfAttribute(modifier.AttributeSetCode, modifier.AttributeCode);
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
                    if (oldCurrentValue != attribute.CurrentValue)
                    {
                        attribute.PreviousCurrentValue = oldCurrentValue;
                        attribute.CurrentValueChangePending = true;
                    }

                    AttributeHelper.MarkDirectCurrentValueChanged(em, spec.TargetAsc, ref attribute);

                    deltas.Add(new AttributeModifierBuffer
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
                        AttrSetCode = modifier.AttributeSetCode,
                        AttributeCode = modifier.AttributeCode,
                        Op = modifier.Operation,
                        ValueKind = AttributeDeltaValueKind.BaseValue,
                        Magnitude = magnitude,
                        OldValue = oldValue,
                        NewValue = newValue,
                    });
                }

                attributes[attrIndex] = attribute;
            }
        }

        private static MagnitudeEvalContext BuildMagnitudeContext(
            in GEEffectSpecBuffer spec,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            in GASCatalogModifierDefinitionBlob modifier)
        {
            var context = new MagnitudeEvalContext
            {
                SourceAsc = spec.SourceAsc,
                TargetAsc = spec.TargetAsc,
                GameplayEffectCode = spec.GameplayEffectCode,
                Level = spec.Level,
                StackCount = spec.StackCount <= 0 ? 1 : spec.StackCount,
                SetByCallerKey = modifier.MagnitudeKey,
            };

            if (modifier.MagnitudeSource == EMagnitudeSource.SetByCaller
                && TryFindSetByCallerValue(in spec, setByCallerValues, modifier.MagnitudeKey, out var setByCallerValue))
            {
                context.HasSetByCallerValue = 1;
                context.SetByCallerValue = setByCallerValue;
            }

            return context;
        }

        private static bool TryFindSetByCallerValue(
            in GEEffectSpecBuffer spec,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            int key,
            out float value)
        {
            var start = spec.SetByCallerStart < 0 ? 0 : spec.SetByCallerStart;
            var end = start + spec.SetByCallerCount;
            if (end > setByCallerValues.Length)
                end = setByCallerValues.Length;
            for (var i = start; i < end; i++)
            {
                var setByCaller = setByCallerValues[i];
                if (setByCaller.Key != key || setByCaller.SpecSequence != spec.Sequence)
                    continue;
                value = setByCaller.Value;
                return true;
            }
            value = 0f;
            return false;
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;
            return next++;
        }

        private static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0) return 0;
            if (cursor > length) return length;
            return cursor;
        }

        private static bool IsUnavailableAsc(EntityManager em, Entity asc)
        {
            return asc == Entity.Null
                || !em.Exists(asc)
                || ASCEntityFactory.IsDestroying(em, asc);
        }

        private static void AssignSpecSequence(
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            int start,
            int count,
            int commandSequence,
            int specSequence)
        {
            if (count <= 0)
                return;
            if (start < 0) start = 0;
            var end = start + count;
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
    }
}
