using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateBefore(typeof(GASAttributeSetReduceApplySystem))]
    [UpdateBefore(typeof(GEExecutionCalculationSystem))]
    public partial struct GEEffectSpecBuildSystem : ISystem
    {
        private EntityQuery _ownerInstantCommandQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerInstantCommandQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                    ComponentType.ReadOnly<GEEffectCommandBuffer>(),
                    ComponentType.ReadOnly<GESetByCallerValueBuffer>(),
                    ComponentType.ReadOnly<GEEffectSpecBuffer>(),
                },
            });
            state.RequireForUpdate(_ownerInstantCommandQuery);
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();

            var records = new NativeList<OwnerLocalInstantSpecCommandRecord>(1, state.WorldUpdateAllocator);
            var payloads = new NativeList<GESetByCallerValueBuffer>(1, state.WorldUpdateAllocator);
            var collectHandle = new CollectOwnerLocalInstantSpecCommandsJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                CommandType = SystemAPI.GetBufferTypeHandle<GEEffectCommandBuffer>(isReadOnly: true),
                SetByCallerType = SystemAPI.GetBufferTypeHandle<GESetByCallerValueBuffer>(isReadOnly: true),
                Records = records,
                Payloads = payloads,
            }.Schedule(_ownerInstantCommandQuery, state.Dependency);

            var buildHandle = new BuildOwnerLocalInstantSpecsJob
            {
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(),
                EntityStorageInfoLookup = SystemAPI.GetEntityStorageInfoLookup(),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: true),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
                Records = records,
                Payloads = payloads,
            }.Schedule(collectHandle);

            state.Dependency = buildHandle;
        }

        private struct OwnerLocalInstantSpecCommandRecord
        {
            public Entity Owner;
            public int LocalIndex;
            public int PayloadStart;
            public int PayloadCount;
            public GEEffectCommandBuffer Command;
        }

        [BurstCompile]
        private struct CollectOwnerLocalInstantSpecCommandsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public BufferTypeHandle<GEEffectCommandBuffer> CommandType;
            [ReadOnly] public BufferTypeHandle<GESetByCallerValueBuffer> SetByCallerType;
            public NativeList<OwnerLocalInstantSpecCommandRecord> Records;
            public NativeList<GESetByCallerValueBuffer> Payloads;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var commands = chunk.GetBufferAccessor(ref CommandType);
                var setByCallerValues = chunk.GetBufferAccessor(ref SetByCallerType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = entities[entityIndex];
                    var ownerCommands = commands[entityIndex];
                    var ownerSetByCallerValues = setByCallerValues[entityIndex];
                    for (var commandIndex = 0; commandIndex < ownerCommands.Length; commandIndex++)
                    {
                        var command = ownerCommands[commandIndex];
                        if (command.Kind != GEEffectCommandKind.Instant
                            || command.Sequence <= 0)
                        {
                            continue;
                        }

                        var payloadStart = Payloads.Length;
                        var payloadCount = CopySetByCallerValues(ownerSetByCallerValues, in command, Payloads);
                        Records.Add(new OwnerLocalInstantSpecCommandRecord
                        {
                            Owner = owner,
                            LocalIndex = commandIndex,
                            PayloadStart = payloadStart,
                            PayloadCount = payloadCount,
                            Command = command,
                        });
                    }
                }
            }
        }

        [BurstCompile]
        private struct BuildOwnerLocalInstantSpecsJob : IJob
        {
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            [ReadOnly] public EntityStorageInfoLookup EntityStorageInfoLookup;
            [ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;
            [ReadOnly] public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;
            public NativeList<OwnerLocalInstantSpecCommandRecord> Records;
            [ReadOnly] public NativeList<GESetByCallerValueBuffer> Payloads;

            public void Execute()
            {
                if (!Catalog.IsCreated
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                    return;

                if (Records.Length == 0)
                    return;

                Records.Sort(new OwnerLocalInstantSpecCommandRecordComparer());
                var stream = StreamLookup[StreamEntity];
                ref var catalog = ref Catalog.Value;
                var builtCount = 0;
                for (var i = 0; i < Records.Length; i++)
                {
                    var record = Records[i];
                    var command = record.Command;
                    if (!CanBuildInstantSpec(
                            ref catalog,
                            in command,
                            EntityStorageInfoLookup,
                            DestroyingLookup,
                            TagMaskLookup,
                            out var gameplayEffectIndex))
                    {
                        continue;
                    }

                    if (!SpecLookup.HasBuffer(record.Owner)
                        || !SetByCallerLookup.HasBuffer(record.Owner))
                    {
                        continue;
                    }

                    var specs = SpecLookup[record.Owner];
                    var setByCallerValues = SetByCallerLookup[record.Owner];
                    var specSequence = GASRuntimeSequenceAllocator.AllocateSpecSequence(ref stream);
                    var setByCallerStart = setByCallerValues.Length;
                    var setByCallerCount = CopySetByCallerValues(
                        Payloads,
                        record.PayloadStart,
                        record.PayloadCount,
                        command.Sequence,
                        specSequence,
                        setByCallerValues);
                    specs.Add(new GEEffectSpecBuffer
                    {
                        Sequence = specSequence,
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
                        SetByCallerStart = setByCallerCount > 0 ? setByCallerStart : 0,
                        SetByCallerCount = setByCallerCount,
                        Flags = gameplayEffectIndex,
                    });
                    builtCount++;
                }

                stream.OwnerLocalSpecCount += builtCount;
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
                || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                    ref catalog,
                    command.GameplayEffectCode,
                    out gameplayEffectIndex))
            {
                return false;
            }

            ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            if (gameplayEffect.DurationFrames > 0
                || gameplayEffect.PeriodFrames > 0
                || gameplayEffect.StackLimitCount > 0
                || gameplayEffect.GrantedTagMaskIndex >= 0
                || !gameplayEffect.RemoveGameplayEffectTagQuery.IsEmpty
                || gameplayEffect.GrantedAbilityCount > 0)
            {
                return false;
            }

            if (gameplayEffect.RequirementCount > 0)
            {
                var targetTags = tagMaskLookup.HasComponent(command.TargetAsc)
                    ? tagMaskLookup[command.TargetAsc]
                    : default;
                if (!GASRuntimeRequirementEvaluator.EvaluateGameplayEffectRequirements(
                        ref catalog,
                        in gameplayEffect,
                        in targetTags,
                        out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetCueRequestOnApply(ref GASDefinitionCatalogBlob catalog, int gameplayEffectIndex)
        {
            if ((uint)gameplayEffectIndex >= (uint)catalog.GameplayEffects.Length)
                return 0;
            return catalog.GameplayEffects[gameplayEffectIndex].GameplayCueCode;
        }

        private struct OwnerLocalInstantSpecCommandRecordComparer : IComparer<OwnerLocalInstantSpecCommandRecord>
        {
            public int Compare(OwnerLocalInstantSpecCommandRecord x, OwnerLocalInstantSpecCommandRecord y)
            {
                var result = x.Command.Sequence.CompareTo(y.Command.Sequence);
                if (result != 0)
                    return result;

                result = CompareEntity(x.Owner, y.Owner);
                if (result != 0)
                    return result;

                return x.LocalIndex.CompareTo(y.LocalIndex);
            }
        }

        private static int CopySetByCallerValues(
            DynamicBuffer<GESetByCallerValueBuffer> source,
            in GEEffectCommandBuffer command,
            NativeList<GESetByCallerValueBuffer> target)
        {
            if (!source.IsCreated || command.SetByCallerCount <= 0)
                return 0;

            var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
            if (start >= source.Length)
                return 0;

            var count = command.SetByCallerCount;
            if (start + count > source.Length)
                count = source.Length - start;

            var copied = 0;
            for (var i = 0; i < count; i++)
            {
                var value = source[start + i];
                if (value.CommandSequence != command.Sequence)
                    continue;

                value.CommandSequence = command.Sequence;
                value.SpecSequence = 0;
                target.Add(value);
                copied++;
            }

            return copied;
        }

        private static int CopySetByCallerValues(
            NativeList<GESetByCallerValueBuffer> source,
            int start,
            int count,
            int commandSequence,
            int specSequence,
            DynamicBuffer<GESetByCallerValueBuffer> target)
        {
            if (!source.IsCreated || count <= 0)
                return 0;

            if (start < 0)
                start = 0;
            if (start >= source.Length)
                return 0;
            if (start + count > source.Length)
                count = source.Length - start;

            var copied = 0;
            for (var i = 0; i < count; i++)
            {
                var value = source[start + i];
                if (value.CommandSequence != commandSequence)
                    continue;

                value.CommandSequence = commandSequence;
                value.SpecSequence = specSequence;
                target.Add(value);
                copied++;
            }

            return copied;
        }

        private static int CompareEntity(Entity left, Entity right)
        {
            var result = left.Index.CompareTo(right.Index);
            return result != 0 ? result : left.Version.CompareTo(right.Version);
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
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]
    [UpdateAfter(typeof(GEEffectSpecBuildSystem))]
    [UpdateBefore(typeof(GASAttributeModifierDeltaApplySystem))]
    [UpdateBefore(typeof(GameplayFactProjectionSystem))]
    public partial struct GASAttributeSetReduceApplySystem : ISystem
    {
        private EntityQuery _ownerSpecQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerSpecQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                    ComponentType.ReadOnly<ASCDestroyingComponent>(),
                    ComponentType.ReadOnly<GEEffectSpecBuffer>(),
                    ComponentType.ReadOnly<GESetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate(_ownerSpecQuery);
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            state.Dependency = new AttributeSetReduceApplyJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                DestroyingType = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),
                SpecType = SystemAPI.GetBufferTypeHandle<GEEffectSpecBuffer>(isReadOnly: true),
                SetByCallerType = SystemAPI.GetBufferTypeHandle<GESetByCallerValueBuffer>(isReadOnly: true),
                AttributeType = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(),
                OwnerFactType = SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
            }.Schedule(_ownerSpecQuery, state.Dependency);
        }

        [BurstCompile]
        private struct AttributeSetReduceApplyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<ASCDestroyingComponent> DestroyingType;
            [ReadOnly] public BufferTypeHandle<GEEffectSpecBuffer> SpecType;
            [ReadOnly] public BufferTypeHandle<GESetByCallerValueBuffer> SetByCallerType;
            public BufferTypeHandle<AttributeValueBuffer> AttributeType;
            public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactType;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                    return;

                var stream = StreamLookup[StreamEntity];
                var owners = chunk.GetNativeArray(EntityType);
                var destroyingMask = chunk.GetEnabledMask(ref DestroyingType);
                var specBuffers = chunk.GetBufferAccessor(ref SpecType);
                var setByCallerBuffers = chunk.GetBufferAccessor(ref SetByCallerType);
                var attributeBuffers = chunk.GetBufferAccessor(ref AttributeType);
                var ownerFactBuffers = chunk.GetBufferAccessor(ref OwnerFactType);
                ref var catalog = ref Catalog.Value;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (destroyingMask[entityIndex])
                        continue;

                    var owner = owners[entityIndex];
                    var specs = specBuffers[entityIndex];
                    if (specs.Length == 0)
                        continue;

                    var setByCallerValues = setByCallerBuffers[entityIndex];
                    var attributes = attributeBuffers[entityIndex];
                    var facts = ownerFactBuffers[entityIndex];
                    for (var i = 0; i < specs.Length; i++)
                        ApplySpec(ref stream, ref catalog, owner, specs[i], setByCallerValues, attributes, facts);
                }

                StreamLookup[StreamEntity] = stream;
            }
        }

        private static void ApplySpec(
            ref GEEffectCommandStreamComponent stream,
            ref GASDefinitionCatalogBlob catalog,
            Entity owner,
            in GEEffectSpecBuffer spec,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            DynamicBuffer<AttributeValueBuffer> attributes,
            DynamicBuffer<OwnerLocalGameplayFactBuffer> facts)
        {
            if (spec.TargetAsc == Entity.Null
                || CompareEntity(spec.TargetAsc, owner) != 0
                || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                    ref catalog,
                    spec.GameplayEffectCode,
                    out var gameplayEffectIndex))
            {
                return;
            }

            ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            for (var i = 0; i < gameplayEffect.ModifierCount; i++)
            {
                var modifierIndex = gameplayEffect.ModifierStart + i;
                if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                    continue;

                var modifier = catalog.Modifiers[modifierIndex];
                var context = BuildMagnitudeContext(in spec, setByCallerValues, in modifier);
                if (!GASRuntimeMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
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

                    var deltaSequence = GASRuntimeSequenceAllocator.AllocateDeltaSequence(ref stream);
                    facts.Add(new OwnerLocalGameplayFactBuffer
                    {
                        Fact = new GameplayEventBuffer
                        {
                            Sequence = GASRuntimeSequenceAllocator.AllocateFactSequence(ref stream),
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

        private static int CompareEntity(Entity left, Entity right)
        {
            var result = left.Index.CompareTo(right.Index);
            return result != 0 ? result : left.Version.CompareTo(right.Version);
        }

    }
}
