using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

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
            _definitionQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    GEContextComponent,
                    GEEffectSpecComponent,
                    GEExecutionCalculationDefinitionBuffer,
                    GEExecutionCalculationValueBuffer>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var effectChunkCount = _definitionQuery.CalculateChunkCountWithoutFiltering();
            if (effectChunkCount <= 0)
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var factEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();
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
                EventBusEntity = eventBusEntity,
                FactEcb = factEcb,
            };
            state.Dependency = calculationJob.ScheduleParallel(_definitionQuery, state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { }

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
            public Entity EventBusEntity;
            public EntityCommandBuffer.ParallelWriter FactEcb;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var effects = chunk.GetNativeArray(EntityTypeHandle);
                var contexts = chunk.GetNativeArray(ref ContextTypeHandle);
                var specs = chunk.GetNativeArray(ref SpecTypeHandle);
                var definitions = chunk.GetBufferAccessorRO(ref DefinitionTypeHandle);
                var outputs = chunk.GetBufferAccessor(ref OutputTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    EvaluateEffect(
                        unfilteredChunkIndex,
                        effects[entityIndex],
                        contexts[entityIndex],
                        specs[entityIndex],
                        definitions[entityIndex],
                        outputs[entityIndex]);
                }
            }

            private void EvaluateEffect(
                int sortKey,
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
                            sortKey,
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
                int sortKey,
                Entity effect,
                in GEContextComponent context,
                int eventCode,
                float value)
            {
                if (EventBusEntity == Entity.Null)
                    return;

                FactEcb.AppendToBuffer(sortKey, EventBusEntity, new GameplayEventBusEventBuffer
                {
                    Type = EGameplayEventType.ExecutionCalculationOutputUpdated,
                    SourceAsc = context.SourceAsc,
                    TargetAsc = context.TargetAsc,
                    SourceAbility = context.SourceAbility,
                    GameplayEffect = effect,
                    ContextId = context.ContextId,
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
    }
}
