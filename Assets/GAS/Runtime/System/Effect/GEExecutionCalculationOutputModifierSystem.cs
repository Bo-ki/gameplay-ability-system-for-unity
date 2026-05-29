using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationExtensionSystemGroup))]
    public partial struct GEExecutionCalculationOutputModifierSystem : ISystem
    {
        private const int MainThreadEffectThreshold = 32;
        private const int MainThreadAttributeThreshold = 64;

        private EntityQuery _outputQuery;
        private EntityQuery _attributeQuery;

        public void OnCreate(ref SystemState state)
        {
            _outputQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    GEContextComponent,
                    GEEffectSpecComponent,
                    GEExecutionCalculationValueBuffer,
                    GEResolvedModifierBuffer,
                    GEExecutionCalculationOutputModifierDefinitionBuffer>()
                .WithDisabled<GEExecutionCalculationOutputModifierAppliedComponent>()
                .Build();
            _attributeQuery = SystemAPI.QueryBuilder()
                .WithAll<AttributeValueBuffer>()
                .Build();
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate(_outputQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var effectChunkCount = _outputQuery.CalculateChunkCount();
            if (effectChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var deltas = em.GetBuffer<AttributeModifierBuffer>(streamEntity);
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var effectEntityCount = _outputQuery.CalculateEntityCount();
            if (effectEntityCount <= MainThreadEffectThreshold
                && _attributeQuery.CalculateEntityCount() <= MainThreadAttributeThreshold)
            {
                ApplyOutputModifiersOnMainThread(ref state, em, streamEntity, ref stream, deltas, frame);
                return;
            }

            var effectStream = new NativeStream(effectChunkCount, Allocator.TempJob);
            var modifierStream = new NativeStream(effectChunkCount, Allocator.TempJob);
            var pendingEffects = new NativeList<PendingOutputEffectRecord>(Allocator.TempJob);
            var modifierRecords = new NativeList<PendingOutputModifierRecord>(Allocator.TempJob);

            try
            {
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
                state.Dependency = collectJob.ScheduleParallel(_outputQuery, state.Dependency);
                state.Dependency.Complete();

                ReadPendingEffects(effectStream.AsReader(), pendingEffects);
                ReadModifierRecords(modifierStream.AsReader(), modifierRecords);

                var appliedDeltaCounts =
                    new NativeParallelHashMap<Entity, int>(math.max(1, pendingEffects.Length), Allocator.TempJob);
                try
                {
                    if (modifierRecords.Length > 0)
                    {
                        modifierRecords.Sort(new PendingOutputModifierRecordComparer());
                        var targetRanges =
                            new NativeParallelHashMap<Entity, OutputModifierRecordRange>(
                                math.max(1, modifierRecords.Length),
                                Allocator.TempJob);
                        var deltaChunkCount = _attributeQuery.CalculateChunkCount();
                        var deltaStream = deltaChunkCount > 0
                            ? new NativeStream(deltaChunkCount, Allocator.TempJob)
                            : default;

                        try
                        {
                            BuildTargetRanges(modifierRecords.AsArray(), targetRanges);

                            if (deltaChunkCount > 0)
                            {
                                var applyJob = new TargetAttributeOutputModifierApplyJob
                                {
                                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                                    AttributeTypeHandle =
                                        SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: false),
                                    ModifierRecords = modifierRecords.AsArray(),
                                    TargetRanges = targetRanges,
                                    DeltaWriter = deltaStream.AsWriter(),
                                    Frame = frame,
                                };
                                state.Dependency = applyJob.ScheduleParallel(_attributeQuery, state.Dependency);
                                state.Dependency.Complete();

                                AppendPendingDeltas(deltaStream.AsReader(), deltas, ref stream, appliedDeltaCounts);
                            }
                        }
                        finally
                        {
                            if (deltaStream.IsCreated)
                                deltaStream.Dispose();
                            targetRanges.Dispose();
                        }
                    }

                    em.SetComponentData(streamEntity, stream);
                    MarkPendingEffectsApplied(
                        SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged),
                        pendingEffects.AsArray(),
                        appliedDeltaCounts,
                        frame);
                }
                finally
                {
                    appliedDeltaCounts.Dispose();
                }
            }
            finally
            {
                modifierRecords.Dispose();
                pendingEffects.Dispose();
                modifierStream.Dispose();
                effectStream.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state) { }

        private void ApplyOutputModifiersOnMainThread(
            ref SystemState state,
            EntityManager em,
            Entity streamEntity,
            ref GEEffectCommandStreamComponent stream,
            DynamicBuffer<AttributeModifierBuffer> deltas,
            int frame)
        {
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (contextRef, specRef, values, definitions, resolvedModifiers, effect)
                     in SystemAPI
                         .Query<
                             RefRO<GEContextComponent>,
                             RefRO<GEEffectSpecComponent>,
                             DynamicBuffer<GEExecutionCalculationValueBuffer>,
                             DynamicBuffer<GEExecutionCalculationOutputModifierDefinitionBuffer>,
                             DynamicBuffer<GEResolvedModifierBuffer>>()
                         .WithDisabled<GEExecutionCalculationOutputModifierAppliedComponent>()
                         .WithEntityAccess())
            {
                var context = contextRef.ValueRO;
                var spec = specRef.ValueRO;
                var deltaCount = 0;
                var hasTargetAttributes = context.TargetAsc != Entity.Null
                                          && em.Exists(context.TargetAsc)
                                          && em.HasBuffer<AttributeValueBuffer>(context.TargetAsc);
                var targetAttributes = hasTargetAttributes
                    ? em.GetBuffer<AttributeValueBuffer>(context.TargetAsc)
                    : default;

                resolvedModifiers.Clear();
                for (var definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++)
                {
                    var definition = definitions[definitionIndex];
                    var magnitude = ResolveOutputMagnitude(values, definition);
                    resolvedModifiers.Add(new GEResolvedModifierBuffer
                    {
                        AttrSetCode = definition.AttrSetCode,
                        AttributeCode = definition.AttributeCode,
                        Op = definition.Op,
                        Magnitude = magnitude,
                        SourceEffect = effect,
                    });

                    if (!hasTargetAttributes
                        || !ApplyModifierDelta(
                            targetAttributes,
                            definition.AttrSetCode,
                            definition.AttributeCode,
                            definition.Op,
                            magnitude,
                            out var oldValue,
                            out var newValue))
                    {
                        continue;
                    }

                    deltas.Add(new AttributeModifierBuffer
                    {
                        Sequence = EffectCommandSpecStreamPhaseUtility.Allocate(ref stream.NextDeltaSequence),
                        Frame = frame,
                        SourceAsc = context.SourceAsc,
                        TargetAsc = context.TargetAsc,
                        SourceAbility = context.SourceAbility,
                        SourceEffect = effect,
                        GameplayEffectCode = spec.GameplayEffectCode,
                        ContextId = context.ContextId,
                        ParentContextId = context.ParentContextId,
                        AttrSetCode = definition.AttrSetCode,
                        AttributeCode = definition.AttributeCode,
                        Op = definition.Op,
                        ValueKind = AttributeDeltaValueKind.BaseValue,
                        Magnitude = magnitude,
                        OldValue = oldValue,
                        NewValue = newValue,
                    });
                    deltaCount++;
                }

                ecb.SetComponent(effect, new GEExecutionCalculationOutputModifierAppliedComponent
                {
                    Frame = frame,
                    DeltaCount = deltaCount,
                });
                ecb.SetComponentEnabled<GEExecutionCalculationOutputModifierAppliedComponent>(effect, true);
            }

            em.SetComponentData(streamEntity, stream);
        }

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

        private struct OutputModifierRecordRange
        {
            public int Start;
            public int Count;
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
        private struct TargetAttributeOutputModifierApplyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public BufferTypeHandle<AttributeValueBuffer> AttributeTypeHandle;
            [ReadOnly] public NativeArray<PendingOutputModifierRecord> ModifierRecords;
            [ReadOnly] public NativeParallelHashMap<Entity, OutputModifierRecordRange> TargetRanges;
            public NativeStream.Writer DeltaWriter;
            public int Frame;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                DeltaWriter.BeginForEachIndex(unfilteredChunkIndex);

                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var attributeBuffers = chunk.GetBufferAccessor(ref AttributeTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var asc = entities[entityIndex];
                    if (!TargetRanges.TryGetValue(asc, out var range))
                        continue;

                    var attributes = attributeBuffers[entityIndex];
                    var end = range.Start + range.Count;
                    for (var recordIndex = range.Start; recordIndex < end; recordIndex++)
                    {
                        var record = ModifierRecords[recordIndex];
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

                        DeltaWriter.Write(new PendingAttributeModifierDeltaRecord
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
                    }
                }

                DeltaWriter.EndForEachIndex();
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

        private static void BuildTargetRanges(
            NativeArray<PendingOutputModifierRecord> modifierRecords,
            NativeParallelHashMap<Entity, OutputModifierRecordRange> targetRanges)
        {
            if (modifierRecords.Length == 0)
                return;

            var start = 0;
            var target = modifierRecords[0].TargetAsc;
            for (var i = 1; i < modifierRecords.Length; i++)
            {
                var record = modifierRecords[i];
                if (record.TargetAsc.Equals(target))
                    continue;

                targetRanges.TryAdd(target, new OutputModifierRecordRange
                {
                    Start = start,
                    Count = i - start,
                });
                start = i;
                target = record.TargetAsc;
            }

            targetRanges.TryAdd(target, new OutputModifierRecordRange
            {
                Start = start,
                Count = modifierRecords.Length - start,
            });
        }

        private static void AppendPendingDeltas(
            NativeStream.Reader deltaReader,
            DynamicBuffer<AttributeModifierBuffer> deltas,
            ref GEEffectCommandStreamComponent stream,
            NativeParallelHashMap<Entity, int> appliedDeltaCounts)
        {
            var pendingDeltas = new NativeList<PendingAttributeModifierDeltaRecord>(Allocator.Temp);
            try
            {
                for (var streamIndex = 0; streamIndex < deltaReader.ForEachCount; streamIndex++)
                {
                    var recordCount = deltaReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                        pendingDeltas.Add(deltaReader.Read<PendingAttributeModifierDeltaRecord>());
                    deltaReader.EndForEachIndex();
                }

                if (pendingDeltas.Length == 0)
                    return;

                pendingDeltas.Sort(new PendingAttributeModifierDeltaRecordComparer());
                for (var i = 0; i < pendingDeltas.Length; i++)
                {
                    var record = pendingDeltas[i];
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

                    IncrementAppliedDeltaCount(appliedDeltaCounts, record.Effect);
                }
            }
            finally
            {
                pendingDeltas.Dispose();
            }
        }

        private static void MarkPendingEffectsApplied(
            EntityCommandBuffer ecb,
            NativeArray<PendingOutputEffectRecord> pendingEffects,
            NativeParallelHashMap<Entity, int> appliedDeltaCounts,
            int frame)
        {
            for (var i = 0; i < pendingEffects.Length; i++)
            {
                var effect = pendingEffects[i].Effect;
                var deltaCount = appliedDeltaCounts.TryGetValue(effect, out var count) ? count : 0;
                ecb.SetComponent(effect, new GEExecutionCalculationOutputModifierAppliedComponent
                {
                    Frame = frame,
                    DeltaCount = deltaCount,
                });
                ecb.SetComponentEnabled<GEExecutionCalculationOutputModifierAppliedComponent>(effect, true);
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
