///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime.Generated
{
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(OwnerLocalInstantCommandFlushSystem))]
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

            state.Dependency = new InstantSpecBuildJob
            {
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: true),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(),
                EntityStorageInfoLookup = SystemAPI.GetEntityStorageInfoLookup(),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: true),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct InstantSpecBuildJob : IJob
        {
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            [ReadOnly] public EntityStorageInfoLookup EntityStorageInfoLookup;
            [ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;
            [ReadOnly] public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;

            public void Execute()
            {
                if (!Catalog.IsCreated
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !SpecLookup.HasBuffer(StreamEntity)
                    || !SetByCallerLookup.HasBuffer(StreamEntity))
                    return;

                var stream = StreamLookup[StreamEntity];
                var commands = CommandLookup[StreamEntity];
                var specs = SpecLookup[StreamEntity];
                var setByCallerValues = SetByCallerLookup[StreamEntity];
                ref var catalog = ref Catalog.Value;
                var start = ClampCursor(stream.SpecBuildCommandCursor, commands.Length);
                for (var i = start; i < commands.Length; i++)
                {
                    var command = commands[i];
                    if (command.Kind != GEEffectCommandKind.Instant)
                        continue;
                    if (!CanBuildInstantSpec(ref catalog, in command, EntityStorageInfoLookup, DestroyingLookup, TagMaskLookup, out var gameplayEffectIndex))
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
                StreamLookup[StreamEntity] = stream;
            }
        }

        private static bool CanBuildInstantSpec(
            ref GASDefinitionCatalogBlob catalog,
            in GEEffectCommandBuffer command,
            EntityStorageInfoLookup entityStorageInfoLookup,
            ComponentLookup<ASCDestroyingComponent> destroyingLookup,
            ComponentLookup<TagMaskComponent> tagMaskLookup,
            out int gameplayEffectIndex)
        {
            gameplayEffectIndex = -1;
            if (command.GameplayEffectCode <= 0
                || command.TargetAsc == Entity.Null
                || IsUnavailableAsc(entityStorageInfoLookup, destroyingLookup, command.SourceAsc)
                || IsUnavailableAsc(entityStorageInfoLookup, destroyingLookup, command.TargetAsc)
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out gameplayEffectIndex))
                return false;

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            if (gameplayEffect.DurationFrames > 0
                || gameplayEffect.PeriodFrames > 0
                || gameplayEffect.StackLimitCount > 0
                || gameplayEffect.GrantedTagMaskIndex >= 0
                || !gameplayEffect.RemoveGameplayEffectTagQuery.IsEmpty
                || gameplayEffect.GrantedAbilityCount > 0
                || (gameplayEffect.ModifierCount <= 0
                    && gameplayEffect.GameplayCueCode <= 0))
                return false;

            if (gameplayEffect.RequirementCount > 0)
            {
                var targetTags = tagMaskLookup.HasComponent(command.TargetAsc)
                    ? tagMaskLookup[command.TargetAsc]
                    : default;
                if (!GASGeneratedRequirementEvaluator.EvaluateGameplayEffectRequirements(ref catalog, in gameplayEffect, in targetTags, out _))
                    return false;
            }

            return true;
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

        private static bool IsUnavailableAsc(
            EntityStorageInfoLookup entityStorageInfoLookup,
            ComponentLookup<ASCDestroyingComponent> destroyingLookup,
            Entity asc)
        {
            return asc == Entity.Null
                || !entityStorageInfoLookup.Exists(asc)
                || IsDestroyingAsc(destroyingLookup, asc);
        }

        private static bool IsDestroyingAsc(
            ComponentLookup<ASCDestroyingComponent> destroyingLookup,
            Entity asc)
        {
            return asc != Entity.Null
                && destroyingLookup.HasComponent(asc)
                && destroyingLookup.IsComponentEnabled(asc);
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

    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]
    [UpdateBefore(typeof(GASAttributeModifierDeltaApplySystem))]
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

            state.Dependency = new AttributeSetReduceApplyJob
            {
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: true),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct AttributeSetReduceApplyJob : IJob
        {
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<OwnerLocalGameplayFactBuffer> OwnerFactLookup;
            [ReadOnly] public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            public BufferLookup<AttributeValueBuffer> AttributeLookup;
            [ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;

            public void Execute()
            {
                if (!Catalog.IsCreated
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !SpecLookup.HasBuffer(StreamEntity)
                    || !SetByCallerLookup.HasBuffer(StreamEntity))
                    return;

                var stream = StreamLookup[StreamEntity];
                var specs = SpecLookup[StreamEntity];
                var setByCallerValues = SetByCallerLookup[StreamEntity];
                ref var catalog = ref Catalog.Value;
                var start = ClampCursor(stream.DeltaApplySpecCursor, specs.Length);
                for (var i = start; i < specs.Length; i++)
                    ApplySpec(ref stream, ref catalog, specs[i], setByCallerValues, OwnerFactLookup, AttributeLookup, DestroyingLookup);

                stream.DeltaApplySpecCursor = specs.Length;
                StreamLookup[StreamEntity] = stream;
            }
        }

        private static void ApplySpec(
            ref GEEffectCommandStreamComponent stream,
            ref GASDefinitionCatalogBlob catalog,
            in GEEffectSpecBuffer spec,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            BufferLookup<OwnerLocalGameplayFactBuffer> ownerFactLookup,
            BufferLookup<AttributeValueBuffer> attributeLookup,
            ComponentLookup<ASCDestroyingComponent> destroyingLookup)
        {
            if (spec.TargetAsc == Entity.Null
                || IsDestroyingAsc(destroyingLookup, spec.TargetAsc)
                || !attributeLookup.HasBuffer(spec.TargetAsc)
                || !ownerFactLookup.HasBuffer(spec.TargetAsc)
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, spec.GameplayEffectCode, out var gameplayEffectIndex))
                return;

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            var attributes = attributeLookup[spec.TargetAsc];
            var facts = ownerFactLookup[spec.TargetAsc];
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
                    attribute.Dirty = true;
                    if (oldCurrentValue != attribute.CurrentValue)
                    {
                        attribute.PreviousCurrentValue = oldCurrentValue;
                        attribute.CurrentValueChangePending = true;
                    }

                    var deltaSequence = Allocate(ref stream.NextDeltaSequence);
                    facts.Add(new OwnerLocalGameplayFactBuffer
                    {
                        Fact = new GameplayEventBuffer
                        {
                            Sequence = Allocate(ref stream.NextFactSequence),
                            SourceCommandSequence = spec.SourceCommandSequence,
                            SourceSpecSequence = spec.Sequence,
                            SourceDeltaSequence = deltaSequence,
                            Frame = spec.Frame,
                            EventType = EGameplayEventType.AttributeBaseValueChanged,
                            Domain = EGameplayFactDomain.Attribute,
                            Category = EGameplayFactCategory.StateChange,
                            Severity = EGameplayFactSeverity.Info,
                            SourceAsc = spec.SourceAsc,
                            TargetAsc = spec.TargetAsc,
                            SourceAbility = spec.SourceAbility,
                            SourceEffect = spec.SourceEffect,
                            GameplayEffectCode = spec.GameplayEffectCode,
                            ContextId = spec.ContextId,
                            ParentContextId = spec.ParentContextId,
                            AttrSetCode = modifier.AttributeSetCode,
                            AttributeCode = modifier.AttributeCode,
                            Value = magnitude,
                            OldValue = oldValue,
                            NewValue = newValue,
                        },
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

        private static bool IsUnavailableAsc(
            EntityStorageInfoLookup entityStorageInfoLookup,
            ComponentLookup<ASCDestroyingComponent> destroyingLookup,
            Entity asc)
        {
            return asc == Entity.Null
                || !entityStorageInfoLookup.Exists(asc)
                || IsDestroyingAsc(destroyingLookup, asc);
        }

        private static bool IsDestroyingAsc(
            ComponentLookup<ASCDestroyingComponent> destroyingLookup,
            Entity asc)
        {
            return asc != Entity.Null
                && destroyingLookup.HasComponent(asc)
                && destroyingLookup.IsComponentEnabled(asc);
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
