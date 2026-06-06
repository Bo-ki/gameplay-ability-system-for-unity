using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationExtensionSystemGroup))]
    public partial struct GEExecutionCalculationOutputModifierSystem : ISystem
    {
        private EntityQuery _outputQuery;

        public void OnCreate(ref SystemState state)
        {
            _outputQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<GEContextComponent>(),
                    ComponentType.ReadOnly<GEEffectSpecComponent>(),
                    ComponentType.ReadOnly<GEExecutionCalculationValueBuffer>(),
                    ComponentType.ReadWrite<GEResolvedModifierBuffer>(),
                    ComponentType.ReadOnly<GEExecutionCalculationOutputModifierDefinitionBuffer>(),
                },
                Disabled = new[]
                {
                    ComponentType.ReadWrite<GEExecutionCalculationOutputModifierAppliedComponent>(),
                },
            });
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate(_outputQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var effectChunkCount = _outputQuery.CalculateChunkCountWithoutFiltering();
            if (effectChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);

            var effectStream = new NativeStream(effectChunkCount, Allocator.TempJob);
            var modifierStream = new NativeStream(effectChunkCount, Allocator.TempJob);
            var initialRecordCapacity = math.max(1, effectChunkCount * 128);
            var pendingEffects = new NativeList<PendingOutputEffectRecord>(initialRecordCapacity, Allocator.TempJob);
            var modifierRecords = new NativeList<PendingOutputModifierRecord>(initialRecordCapacity, Allocator.TempJob);
            var pendingDeltas = new NativeList<PendingAttributeModifierDeltaRecord>(initialRecordCapacity, Allocator.TempJob);
            var appliedDeltaCounts =
                new NativeParallelHashMap<Entity, int>(initialRecordCapacity, Allocator.TempJob);
            var attributeOwnerMarkerRequests =
                new NativeList<AttributeOwnerMarkerRequestRecord>(initialRecordCapacity, Allocator.TempJob);

            var collectJob = new GEExecutionCalculationOutputModifierCollectJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ContextTypeHandle = SystemAPI.GetComponentTypeHandle<GEContextComponent>(isReadOnly: true),
                SpecTypeHandle = SystemAPI.GetComponentTypeHandle<GEEffectSpecComponent>(isReadOnly: true),
                ValueTypeHandle = SystemAPI.GetBufferTypeHandle<GEExecutionCalculationValueBuffer>(isReadOnly: true),
                DefinitionTypeHandle =
                    SystemAPI.GetBufferTypeHandle<GEExecutionCalculationOutputModifierDefinitionBuffer>(
                        isReadOnly: true),
                ResolvedModifierTypeHandle =
                    SystemAPI.GetBufferTypeHandle<GEResolvedModifierBuffer>(isReadOnly: false),
                EffectWriter = effectStream.AsWriter(),
                ModifierWriter = modifierStream.AsWriter(),
            };
            var collectHandle = collectJob.ScheduleParallel(_outputQuery, state.Dependency);

            var applyJob = new GEExecutionCalculationOutputModifierApplyJob
            {
                EffectReader = effectStream.AsReader(),
                ModifierReader = modifierStream.AsReader(),
                PendingEffects = pendingEffects,
                ModifierRecords = modifierRecords,
                PendingDeltas = pendingDeltas,
                AppliedDeltaCounts = appliedDeltaCounts,
                AttributeOwnerMarkerRequests = attributeOwnerMarkerRequests,
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                DeltaBufferLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(isReadOnly: false),
                StreamEntity = streamEntity,
                Frame = frame,
            };
            var applyHandle = applyJob.Schedule(collectHandle);
            var appliedHandle = new GEExecutionCalculationOutputModifierAppliedJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                AppliedTypeHandle =
                    SystemAPI.GetComponentTypeHandle<GEExecutionCalculationOutputModifierAppliedComponent>(),
                PendingEffects = pendingEffects,
                AppliedDeltaCounts = appliedDeltaCounts,
                Frame = frame,
            }.Schedule(_outputQuery, applyHandle);
            state.Dependency = new AttributeOwnerMarkerRequestFlushJob
            {
                RequestLookup = SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),
                AttributeOwnerMarkerRequests = attributeOwnerMarkerRequests,
                EventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                    ? resolvedEventBus
                    : Entity.Null,
            }.Schedule(appliedHandle);
            state.Dependency = effectStream.Dispose(state.Dependency);
            state.Dependency = modifierStream.Dispose(state.Dependency);
            state.Dependency = pendingEffects.Dispose(state.Dependency);
            state.Dependency = modifierRecords.Dispose(state.Dependency);
            state.Dependency = pendingDeltas.Dispose(state.Dependency);
            state.Dependency = appliedDeltaCounts.Dispose(state.Dependency);
            state.Dependency = attributeOwnerMarkerRequests.Dispose(state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { }

        private static float ResolveOutputMagnitude(
            DynamicBuffer<GEExecutionCalculationValueBuffer> values,
            in GEExecutionCalculationOutputModifierDefinitionBuffer definition)
        {
            var rawMagnitude = TryGetOutputValue(values, definition.OutputKey, out var outputValue)
                ? outputValue
                : definition.FallbackMagnitude;
            return ApplyTransform(
                rawMagnitude,
                definition.Coefficient,
                definition.PreAdd,
                definition.PostAdd);
        }

        private struct PendingOutputEffectRecord
        {
            public Entity Effect;
        }

        private struct PendingOutputModifierRecord
        {
            public int Order;
            public Entity Effect;
            public Entity SourceAsc;
            public Entity TargetAsc;
            public Entity SourceAbility;
            public int GameplayEffectCode;
            public int ContextId;
            public int ParentContextId;
            public int AttrSetCode;
            public int AttributeCode;
            public EModifierOp Op;
            public float Magnitude;
        }

        private struct PendingAttributeModifierDeltaRecord
        {
            public int Order;
            public int Frame;
            public Entity Effect;
            public Entity SourceAsc;
            public Entity TargetAsc;
            public Entity SourceAbility;
            public int GameplayEffectCode;
            public int ContextId;
            public int ParentContextId;
            public int AttrSetCode;
            public int AttributeCode;
            public EModifierOp Op;
            public float Magnitude;
            public float OldValue;
            public float NewValue;
        }

        private struct AttributeOwnerMarkerRequestRecord
        {
            public int Sequence;
            public Entity ASC;
            public EAttributeOwnerMarkerRequestKind RequestKind;
        }

        [BurstCompile]
        private struct GEExecutionCalculationOutputModifierCollectJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<GEContextComponent> ContextTypeHandle;
            [ReadOnly] public ComponentTypeHandle<GEEffectSpecComponent> SpecTypeHandle;
            [ReadOnly] public BufferTypeHandle<GEExecutionCalculationValueBuffer> ValueTypeHandle;
            [ReadOnly] public BufferTypeHandle<GEExecutionCalculationOutputModifierDefinitionBuffer> DefinitionTypeHandle;
            public BufferTypeHandle<GEResolvedModifierBuffer> ResolvedModifierTypeHandle;
            public NativeStream.Writer EffectWriter;
            public NativeStream.Writer ModifierWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                EffectWriter.BeginForEachIndex(unfilteredChunkIndex);
                ModifierWriter.BeginForEachIndex(unfilteredChunkIndex);

                var effects = chunk.GetNativeArray(EntityTypeHandle);
                var contexts = chunk.GetNativeArray(ref ContextTypeHandle);
                var specs = chunk.GetNativeArray(ref SpecTypeHandle);
                var values = chunk.GetBufferAccessor(ref ValueTypeHandle);
                var definitions = chunk.GetBufferAccessor(ref DefinitionTypeHandle);
                var resolvedModifiers = chunk.GetBufferAccessor(ref ResolvedModifierTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var effect = effects[entityIndex];
                    var context = contexts[entityIndex];
                    var spec = specs[entityIndex];
                    var valueBuffer = values[entityIndex];
                    var definitionBuffer = definitions[entityIndex];
                    var resolvedBuffer = resolvedModifiers[entityIndex];

                    EffectWriter.Write(new PendingOutputEffectRecord
                    {
                        Effect = effect,
                    });

                    if (context.TargetAsc == Entity.Null)
                        continue;

                    resolvedBuffer.Clear();
                    for (var definitionIndex = 0; definitionIndex < definitionBuffer.Length; definitionIndex++)
                    {
                        var definition = definitionBuffer[definitionIndex];
                        var rawMagnitude = TryGetOutputValue(valueBuffer, definition.OutputKey, out var outputValue)
                            ? outputValue
                            : definition.FallbackMagnitude;
                        var magnitude = ApplyTransform(
                            rawMagnitude,
                            definition.Coefficient,
                            definition.PreAdd,
                            definition.PostAdd);

                        resolvedBuffer.Add(new GEResolvedModifierBuffer
                        {
                            AttrSetCode = definition.AttrSetCode,
                            AttributeCode = definition.AttributeCode,
                            Op = definition.Op,
                            Magnitude = magnitude,
                            SourceEffect = effect,
                        });

                        ModifierWriter.Write(new PendingOutputModifierRecord
                        {
                            Effect = effect,
                            SourceAsc = context.SourceAsc,
                            TargetAsc = context.TargetAsc,
                            SourceAbility = context.SourceAbility,
                            GameplayEffectCode = spec.GameplayEffectCode,
                            ContextId = context.ContextId,
                            ParentContextId = context.ParentContextId,
                            AttrSetCode = definition.AttrSetCode,
                            AttributeCode = definition.AttributeCode,
                            Op = definition.Op,
                            Magnitude = magnitude,
                        });
                    }
                }

                ModifierWriter.EndForEachIndex();
                EffectWriter.EndForEachIndex();
            }
        }

        [BurstCompile]
        private struct GEExecutionCalculationOutputModifierApplyJob : IJob
        {
            public NativeStream.Reader EffectReader;
            public NativeStream.Reader ModifierReader;
            public NativeList<PendingOutputEffectRecord> PendingEffects;
            public NativeList<PendingOutputModifierRecord> ModifierRecords;
            public NativeList<PendingAttributeModifierDeltaRecord> PendingDeltas;
            public NativeParallelHashMap<Entity, int> AppliedDeltaCounts;
            public NativeList<AttributeOwnerMarkerRequestRecord> AttributeOwnerMarkerRequests;
            public BufferLookup<AttributeValueBuffer> AttributeLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<AttributeModifierBuffer> DeltaBufferLookup;
            public Entity StreamEntity;
            public int Frame;

            public void Execute()
            {
                PendingEffects.Clear();
                ModifierRecords.Clear();
                PendingDeltas.Clear();
                AppliedDeltaCounts.Clear();
                AttributeOwnerMarkerRequests.Clear();

                ReadPendingEffects(EffectReader, PendingEffects);
                ReadModifierRecords(ModifierReader, ModifierRecords);

                if (ModifierRecords.Length > 0)
                {
                    ModifierRecords.Sort(new PendingOutputModifierRecordComparer());
                    for (var recordIndex = 0; recordIndex < ModifierRecords.Length; recordIndex++)
                    {
                        var record = ModifierRecords[recordIndex];
                        if (record.TargetAsc == Entity.Null
                            || !AttributeLookup.HasBuffer(record.TargetAsc))
                        {
                            continue;
                        }

                        var attributes = AttributeLookup[record.TargetAsc];
                        if (!ApplyModifierDelta(
                                attributes,
                                record.AttrSetCode,
                                record.AttributeCode,
                                record.Op,
                                record.Magnitude,
                                out var oldValue,
                                out var newValue))
                        {
                            continue;
                        }

                        PendingDeltas.Add(new PendingAttributeModifierDeltaRecord
                        {
                            Order = record.Order,
                            Frame = Frame,
                            Effect = record.Effect,
                            SourceAsc = record.SourceAsc,
                            TargetAsc = record.TargetAsc,
                            SourceAbility = record.SourceAbility,
                            GameplayEffectCode = record.GameplayEffectCode,
                            ContextId = record.ContextId,
                            ParentContextId = record.ParentContextId,
                            AttrSetCode = record.AttrSetCode,
                            AttributeCode = record.AttributeCode,
                            Op = record.Op,
                            Magnitude = record.Magnitude,
                            OldValue = oldValue,
                            NewValue = newValue,
                        });
                        EnqueueAttributeOwnerMarkerRequest(record.TargetAsc);
                        IncrementAppliedDeltaCount(AppliedDeltaCounts, record.Effect);
                    }
                }

                AppendPendingDeltas();
            }

            private void AppendPendingDeltas()
            {
                if (PendingDeltas.Length == 0
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !DeltaBufferLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                PendingDeltas.Sort(new PendingAttributeModifierDeltaRecordComparer());
                var stream = StreamLookup[StreamEntity];
                var deltas = DeltaBufferLookup[StreamEntity];
                for (var i = 0; i < PendingDeltas.Length; i++)
                {
                    var record = PendingDeltas[i];
                    deltas.Add(new AttributeModifierBuffer
                    {
                        Sequence = EffectCommandSpecStreamPhaseUtility.Allocate(ref stream.NextDeltaSequence),
                        Frame = record.Frame,
                        SourceAsc = record.SourceAsc,
                        TargetAsc = record.TargetAsc,
                        SourceAbility = record.SourceAbility,
                        SourceEffect = record.Effect,
                        GameplayEffectCode = record.GameplayEffectCode,
                        ContextId = record.ContextId,
                        ParentContextId = record.ParentContextId,
                        AttrSetCode = record.AttrSetCode,
                        AttributeCode = record.AttributeCode,
                        Op = record.Op,
                        ValueKind = AttributeDeltaValueKind.BaseValue,
                        Magnitude = record.Magnitude,
                        OldValue = record.OldValue,
                        NewValue = record.NewValue,
                    });
                }
                StreamLookup[StreamEntity] = stream;
            }

            private void EnqueueAttributeOwnerMarkerRequest(Entity asc)
            {
                if (asc == Entity.Null)
                    return;

                AttributeOwnerMarkerRequests.Add(new AttributeOwnerMarkerRequestRecord
                {
                    Sequence = AttributeOwnerMarkerRequests.Length,
                    ASC = asc,
                    RequestKind = EAttributeOwnerMarkerRequestKind.MarkDirty,
                });
            }
        }

        [BurstCompile]
        private struct GEExecutionCalculationOutputModifierAppliedJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<GEExecutionCalculationOutputModifierAppliedComponent> AppliedTypeHandle;
            [ReadOnly] public NativeList<PendingOutputEffectRecord> PendingEffects;
            [ReadOnly] public NativeParallelHashMap<Entity, int> AppliedDeltaCounts;
            public int Frame;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var effects = chunk.GetNativeArray(EntityTypeHandle);
                var applied = chunk.GetNativeArray(ref AppliedTypeHandle);
                var appliedMask = chunk.GetEnabledMask(ref AppliedTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var effect = effects[entityIndex];
                    if (!ContainsPendingEffect(effect))
                        continue;

                    var deltaCount = AppliedDeltaCounts.TryGetValue(effect, out var count) ? count : 0;
                    applied[entityIndex] = new GEExecutionCalculationOutputModifierAppliedComponent
                    {
                        Frame = Frame,
                        DeltaCount = deltaCount,
                    };
                    appliedMask[entityIndex] = true;
                }
            }

            private bool ContainsPendingEffect(Entity effect)
            {
                for (var i = 0; i < PendingEffects.Length; i++)
                {
                    if (PendingEffects[i].Effect == effect)
                        return true;
                }

                return false;
            }
        }

        [BurstCompile]
        private struct AttributeOwnerMarkerRequestFlushJob : IJob
        {
            public BufferLookup<AttributeOwnerMarkerRequestBuffer> RequestLookup;
            [ReadOnly] public NativeList<AttributeOwnerMarkerRequestRecord> AttributeOwnerMarkerRequests;
            public Entity EventBusEntity;

            public void Execute()
            {
                if (EventBusEntity == Entity.Null || !RequestLookup.HasBuffer(EventBusEntity))
                    return;

                var requests = RequestLookup[EventBusEntity];
                for (var i = 0; i < AttributeOwnerMarkerRequests.Length; i++)
                {
                    var record = AttributeOwnerMarkerRequests[i];
                    if (record.ASC == Entity.Null)
                        continue;

                    requests.Add(new AttributeOwnerMarkerRequestBuffer
                    {
                        Sequence = requests.Length,
                        ASC = record.ASC,
                        RequestKind = record.RequestKind,
                        Value = 1,
                    });
                }
            }
        }

        private static void ReadPendingEffects(
            NativeStream.Reader effectReader,
            NativeList<PendingOutputEffectRecord> pendingEffects)
        {
            for (var streamIndex = 0; streamIndex < effectReader.ForEachCount; streamIndex++)
            {
                var recordCount = effectReader.BeginForEachIndex(streamIndex);
                for (var i = 0; i < recordCount; i++)
                    pendingEffects.Add(effectReader.Read<PendingOutputEffectRecord>());
                effectReader.EndForEachIndex();
            }
        }

        private static void ReadModifierRecords(
            NativeStream.Reader modifierReader,
            NativeList<PendingOutputModifierRecord> modifierRecords)
        {
            for (var streamIndex = 0; streamIndex < modifierReader.ForEachCount; streamIndex++)
            {
                var recordCount = modifierReader.BeginForEachIndex(streamIndex);
                for (var i = 0; i < recordCount; i++)
                {
                    var record = modifierReader.Read<PendingOutputModifierRecord>();
                    record.Order = modifierRecords.Length;
                    modifierRecords.Add(record);
                }
                modifierReader.EndForEachIndex();
            }
        }

        private static void IncrementAppliedDeltaCount(
            NativeParallelHashMap<Entity, int> appliedDeltaCounts,
            Entity effect)
        {
            if (appliedDeltaCounts.TryGetValue(effect, out var count))
            {
                appliedDeltaCounts[effect] = count + 1;
                return;
            }

            appliedDeltaCounts.TryAdd(effect, 1);
        }

        private struct PendingOutputModifierRecordComparer : IComparer<PendingOutputModifierRecord>
        {
            public int Compare(PendingOutputModifierRecord x, PendingOutputModifierRecord y)
            {
                var targetCompare = x.TargetAsc.Index.CompareTo(y.TargetAsc.Index);
                if (targetCompare != 0)
                    return targetCompare;

                targetCompare = x.TargetAsc.Version.CompareTo(y.TargetAsc.Version);
                if (targetCompare != 0)
                    return targetCompare;

                return x.Order.CompareTo(y.Order);
            }
        }

        private struct PendingAttributeModifierDeltaRecordComparer : IComparer<PendingAttributeModifierDeltaRecord>
        {
            public int Compare(PendingAttributeModifierDeltaRecord x, PendingAttributeModifierDeltaRecord y)
            {
                return x.Order.CompareTo(y.Order);
            }
        }

        private static bool ApplyModifierDelta(
            DynamicBuffer<AttributeValueBuffer> attributes,
            int attrSetCode,
            int attrCode,
            EModifierOp op,
            float magnitude,
            out float oldValue,
            out float newValue)
        {
            oldValue = 0f;
            newValue = 0f;

            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
            if (attrIndex < 0)
                return false;

            var attribute = attributes[attrIndex];
            oldValue = attribute.BaseValue;
            var oldCurrentValue = attribute.CurrentValue;
            newValue = ApplyModifier(attribute.BaseValue, op, magnitude);
            attribute.CurrentValue = newValue;
            Clamp(ref attribute);
            newValue = attribute.CurrentValue;
            attribute.BaseValue = newValue;
            attribute.CurrentValue = newValue;

            if (newValue == oldValue)
            {
                attributes[attrIndex] = attribute;
                return false;
            }

            attribute.Dirty = true;
            if (oldCurrentValue != attribute.CurrentValue)
            {
                attribute.PreviousCurrentValue = oldCurrentValue;
                attribute.CurrentValueChangePending = true;
            }

            attributes[attrIndex] = attribute;
            return true;
        }

        private static void Clamp(ref AttributeValueBuffer attribute)
        {
            if (attribute.IsClampMin)
                attribute.CurrentValue = math.max(attribute.CurrentValue, attribute.MinValue);
            if (attribute.IsClampMax)
                attribute.CurrentValue = math.min(attribute.CurrentValue, attribute.MaxValue);
        }

        private static float ApplyModifier(float currentValue, EModifierOp op, float magnitude)
        {
            return op switch
            {
                EModifierOp.Add => currentValue + magnitude,
                EModifierOp.Subtract => currentValue - magnitude,
                EModifierOp.Multiply => currentValue * magnitude,
                EModifierOp.Divide => currentValue / magnitude,
                EModifierOp.Override => magnitude,
                _ => currentValue,
            };
        }

        private static bool TryGetOutputValue(
            DynamicBuffer<GEExecutionCalculationValueBuffer> values,
            int outputKey,
            out float value)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i].Key != outputKey)
                    continue;

                value = values[i].Value;
                return true;
            }

            value = 0f;
            return false;
        }

        private static float ApplyTransform(
            float value,
            float coefficient,
            float preAdd,
            float postAdd)
        {
            var resolvedCoefficient = coefficient == 0f ? 1f : coefficient;
            return ((value + preAdd) * resolvedCoefficient) + postAdd;
        }
    }
}
