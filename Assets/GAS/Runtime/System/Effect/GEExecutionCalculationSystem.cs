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
    [UpdateBefore(typeof(GEExecutionCalculationExtensionSystemGroup))]
    public partial struct GEExecutionCalculationSystem : ISystem
    {
        private EntityQuery _definitionQuery;

        public void OnCreate(ref SystemState state)
        {
            _definitionQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<GEContextComponent>(),
                    ComponentType.ReadOnly<GEEffectSpecComponent>(),
                    ComponentType.ReadOnly<GEExecutionCalculationDefinitionBuffer>(),
                    ComponentType.ReadWrite<GEExecutionCalculationValueBuffer>(),
                },
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var effectChunkCount = _definitionQuery.CalculateChunkCountWithoutFiltering();
            if (effectChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var streamEntity = SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var resolvedStream)
                ? resolvedStream
                : Entity.Null;
            var currentFrame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var factStream = new NativeStream(effectChunkCount, Allocator.TempJob);
            var pendingFacts = new NativeList<PendingExecutionOutputFactRecord>(
                effectChunkCount > 0 ? effectChunkCount * 16 : 1,
                Allocator.TempJob);
            var calculationJob = new GEExecutionCalculationJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ContextTypeHandle = SystemAPI.GetComponentTypeHandle<GEContextComponent>(isReadOnly: true),
                SpecTypeHandle = SystemAPI.GetComponentTypeHandle<GEEffectSpecComponent>(isReadOnly: true),
                DefinitionTypeHandle =
                    SystemAPI.GetBufferTypeHandle<GEExecutionCalculationDefinitionBuffer>(isReadOnly: true),
                OutputTypeHandle = SystemAPI.GetBufferTypeHandle<GEExecutionCalculationValueBuffer>(),
                InputLookup = SystemAPI.GetBufferLookup<GEExecutionCalculationInputDefinitionBuffer>(isReadOnly: true),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerRequestValueBuffer>(isReadOnly: true),
                CaptureLookup = SystemAPI.GetBufferLookup<GEAttributeCaptureValueBuffer>(isReadOnly: true),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true),
                StreamEntity = streamEntity,
                FactWriter = factStream.AsWriter(),
                Frame = currentFrame,
            };
            var calculationHandle = calculationJob.ScheduleParallel(_definitionQuery, state.Dependency);
            state.Dependency = new GEExecutionCalculationFactMergeJob
            {
                FactReader = factStream.AsReader(),
                PendingFacts = pendingFacts,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
                StreamEntity = streamEntity,
            }.Schedule(calculationHandle);
            state.Dependency = factStream.Dispose(state.Dependency);
            state.Dependency = pendingFacts.Dispose(state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { }

        private struct PendingExecutionOutputFactRecord
        {
            public int Order;
            public int Frame;
            public Entity SourceAsc;
            public Entity TargetAsc;
            public Entity SourceAbility;
            public Entity SourceEffect;
            public int ContextId;
            public int ParentContextId;
            public int EventCode;
            public float Value;
        }

        [BurstCompile]
        private struct GEExecutionCalculationJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<GEContextComponent> ContextTypeHandle;
            [ReadOnly] public ComponentTypeHandle<GEEffectSpecComponent> SpecTypeHandle;
            [ReadOnly] public BufferTypeHandle<GEExecutionCalculationDefinitionBuffer> DefinitionTypeHandle;
            public BufferTypeHandle<GEExecutionCalculationValueBuffer> OutputTypeHandle;
            [ReadOnly] public BufferLookup<GEExecutionCalculationInputDefinitionBuffer> InputLookup;
            [ReadOnly] public BufferLookup<GESetByCallerRequestValueBuffer> SetByCallerLookup;
            [ReadOnly] public BufferLookup<GEAttributeCaptureValueBuffer> CaptureLookup;
            [ReadOnly] public BufferLookup<AttributeValueBuffer> AttributeLookup;
            public Entity StreamEntity;
            public NativeStream.Writer FactWriter;
            public int Frame;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                FactWriter.BeginForEachIndex(unfilteredChunkIndex);

                var effects = chunk.GetNativeArray(EntityTypeHandle);
                var contexts = chunk.GetNativeArray(ref ContextTypeHandle);
                var specs = chunk.GetNativeArray(ref SpecTypeHandle);
                var definitions = chunk.GetBufferAccessorRO(ref DefinitionTypeHandle);
                var outputs = chunk.GetBufferAccessor(ref OutputTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    EvaluateEffect(
                        effects[entityIndex],
                        contexts[entityIndex],
                        specs[entityIndex],
                        definitions[entityIndex],
                        outputs[entityIndex]);
                }

                FactWriter.EndForEachIndex();
            }

            private void EvaluateEffect(
                Entity effect,
                in GEContextComponent context,
                in GEEffectSpecComponent spec,
                DynamicBuffer<GEExecutionCalculationDefinitionBuffer> definitions,
                DynamicBuffer<GEExecutionCalculationValueBuffer> outputs)
            {
                var hasInputs = InputLookup.HasBuffer(effect)
                                && InputLookup.IsBufferEnabled(effect);
                var inputs = hasInputs ? InputLookup[effect] : default;
                for (var i = 0; i < definitions.Length; i++)
                {
                    var definition = definitions[i];
                    var rawValue = hasInputs && HasInputsForOutput(
                            inputs,
                            definition.CalculationCode,
                            definition.OutputKey)
                        ? ResolveInputAggregate(
                            effect,
                            in context,
                            in spec,
                            inputs,
                            definition.CalculationCode,
                            definition.OutputKey)
                        : ResolveInput(
                            effect,
                            definition.Source,
                            definition.AttributeSetCode,
                            definition.AttributeCode,
                            definition.Key,
                            definition.CaptureTiming,
                            definition.ConstantValue,
                            definition.FallbackValue,
                            in context,
                            in spec,
                            inputIndex: i);

                    var value = ApplyTransform(
                        rawValue,
                        definition.Coefficient,
                        definition.PreAdd,
                        definition.PostAdd);

                    if (ExecutionCalculationRuntimeActions.WriteOutputValue(outputs, definition.OutputKey, value))
                    {
                        AppendOutputUpdatedFact(
                            effect,
                            in context,
                            definition.CalculationCode != 0 ? definition.CalculationCode : definition.OutputKey,
                            value);
                    }
                }
            }

            private static bool HasInputsForOutput(
                DynamicBuffer<GEExecutionCalculationInputDefinitionBuffer> inputs,
                int calculationCode,
                int outputKey)
            {
                for (var i = 0; i < inputs.Length; i++)
                {
                    var input = inputs[i];
                    if (input.CalculationCode == calculationCode && input.OutputKey == outputKey)
                        return true;
                }

                return false;
            }

            private float ResolveInputAggregate(
                Entity effect,
                in GEContextComponent context,
                in GEEffectSpecComponent spec,
                DynamicBuffer<GEExecutionCalculationInputDefinitionBuffer> inputs,
                int calculationCode,
                int outputKey)
            {
                var value = 0f;
                for (var i = 0; i < inputs.Length; i++)
                {
                    var input = inputs[i];
                    if (input.CalculationCode != calculationCode || input.OutputKey != outputKey)
                        continue;

                    var rawInput = ResolveInput(
                        effect,
                        input.Source,
                        input.AttributeSetCode,
                        input.AttributeCode,
                        input.Key,
                        input.CaptureTiming,
                        input.ConstantValue,
                        input.FallbackValue,
                        in context,
                        in spec,
                        input.InputIndex);

                    value += ApplyTransform(
                        rawInput,
                        input.Coefficient,
                        input.PreAdd,
                        input.PostAdd);
                }

                return value;
            }

            private float ResolveInput(
                Entity effect,
                EExecutionCalculationInputSource source,
                int attrSetCode,
                int attributeCode,
                int key,
                EAttributeCaptureTiming captureTiming,
                float constantValue,
                float fallbackValue,
                in GEContextComponent context,
                in GEEffectSpecComponent spec,
                int inputIndex)
            {
                return source switch
                {
                    EExecutionCalculationInputSource.Constant => constantValue,
                    EExecutionCalculationInputSource.SetByCaller => ResolveSetByCaller(effect, key, fallbackValue),
                    EExecutionCalculationInputSource.SourceAttribute => ResolveAttributeCapture(
                        effect,
                        inputIndex,
                        EMagnitudeSource.SourceAttribute,
                        context.SourceAsc,
                        attrSetCode,
                        attributeCode,
                        captureTiming,
                        fallbackValue),
                    EExecutionCalculationInputSource.TargetAttribute => ResolveAttributeCapture(
                        effect,
                        inputIndex,
                        EMagnitudeSource.TargetAttribute,
                        context.TargetAsc,
                        attrSetCode,
                        attributeCode,
                        captureTiming,
                        fallbackValue),
                    EExecutionCalculationInputSource.SpecLevel => spec.Level,
                    EExecutionCalculationInputSource.StackCount => EffectMagnitudeResolver.ResolveStackCount(in spec),
                    _ => fallbackValue,
                };
            }

            private float ResolveSetByCaller(Entity effect, int key, float fallbackValue)
            {
                if (!SetByCallerLookup.HasBuffer(effect))
                    return fallbackValue;

                var values = SetByCallerLookup[effect];
                for (var i = 0; i < values.Length; i++)
                {
                    if (values[i].Key == key)
                        return values[i].Value;
                }

                return fallbackValue;
            }

            private float ResolveAttributeCapture(
                Entity effect,
                int inputIndex,
                EMagnitudeSource source,
                Entity asc,
                int attrSetCode,
                int attributeCode,
                EAttributeCaptureTiming captureTiming,
                float fallbackValue)
            {
                if (captureTiming == EAttributeCaptureTiming.CurrentValue)
                    return ReadAttributeValue(asc, attrSetCode, attributeCode, fallbackValue);

                if (!CaptureLookup.HasBuffer(effect))
                    return fallbackValue;

                var captures = CaptureLookup[effect];
                for (var i = 0; i < captures.Length; i++)
                {
                    var capture = captures[i];
                    if (capture.ModifierIndex == inputIndex
                        && capture.Source == source
                        && capture.AttributeSetCode == attrSetCode
                        && capture.AttributeCode == attributeCode)
                    {
                        return capture.Value;
                    }
                }

                return fallbackValue;
            }

            private float ReadAttributeValue(
                Entity asc,
                int attrSetCode,
                int attributeCode,
                float fallbackValue)
            {
                if (asc == Entity.Null || !AttributeLookup.HasBuffer(asc))
                    return fallbackValue;

                var attributes = AttributeLookup[asc];
                for (var i = 0; i < attributes.Length; i++)
                {
                    var attribute = attributes[i];
                    if (attribute.AttrSetCode == attrSetCode && attribute.Code == attributeCode)
                        return attribute.CurrentValue;
                }

                return fallbackValue;
            }

            private void AppendOutputUpdatedFact(
                Entity effect,
                in GEContextComponent context,
                int eventCode,
                float value)
            {
                if (StreamEntity == Entity.Null)
                    return;

                FactWriter.Write(new PendingExecutionOutputFactRecord
                {
                    Frame = Frame,
                    SourceAsc = context.SourceAsc,
                    TargetAsc = context.TargetAsc,
                    SourceAbility = context.SourceAbility,
                    SourceEffect = effect,
                    ContextId = context.ContextId,
                    ParentContextId = context.ParentContextId,
                    EventCode = eventCode,
                    Value = value,
                });
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

        [BurstCompile]
        private struct GEExecutionCalculationFactMergeJob : IJob
        {
            public NativeStream.Reader FactReader;
            public NativeList<PendingExecutionOutputFactRecord> PendingFacts;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;
            public Entity StreamEntity;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                PendingFacts.Clear();
                ReadFactRecords(FactReader, PendingFacts);
                if (PendingFacts.Length == 0)
                    return;

                PendingFacts.Sort(new PendingExecutionOutputFactRecordComparer());
                var stream = StreamLookup[StreamEntity];
                var facts = FactLookup[StreamEntity];
                for (var i = 0; i < PendingFacts.Length; i++)
                {
                    var record = PendingFacts[i];
                    facts.Add(new GameplayEventBuffer
                    {
                        Sequence = EffectCommandSpecStreamPhaseUtility.Allocate(ref stream.NextFactSequence),
                        Frame = record.Frame,
                        EventType = EGameplayEventType.ExecutionCalculationOutputUpdated,
                        Domain = EGameplayFactDomain.ExecutionCalculation,
                        Category = EGameplayFactCategory.StateChange,
                        Severity = EGameplayFactSeverity.Info,
                        SourceAsc = record.SourceAsc,
                        TargetAsc = record.TargetAsc,
                        SourceAbility = record.SourceAbility,
                        SourceEffect = record.SourceEffect,
                        ContextId = record.ContextId,
                        ParentContextId = record.ParentContextId,
                        EventCode = record.EventCode,
                        Value = record.Value,
                    });
                }

                StreamLookup[StreamEntity] = stream;
            }
        }

        private static void ReadFactRecords(
            NativeStream.Reader factReader,
            NativeList<PendingExecutionOutputFactRecord> pendingFacts)
        {
            for (var streamIndex = 0; streamIndex < factReader.ForEachCount; streamIndex++)
            {
                var recordCount = factReader.BeginForEachIndex(streamIndex);
                for (var i = 0; i < recordCount; i++)
                {
                    var record = factReader.Read<PendingExecutionOutputFactRecord>();
                    record.Order = pendingFacts.Length;
                    pendingFacts.Add(record);
                }
                factReader.EndForEachIndex();
            }
        }

        private struct PendingExecutionOutputFactRecordComparer : IComparer<PendingExecutionOutputFactRecord>
        {
            public int Compare(PendingExecutionOutputFactRecord x, PendingExecutionOutputFactRecord y)
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
    }
}
