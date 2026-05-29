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
        private const int MainThreadChunkThreshold = 2;

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
            var effectChunkCount = _definitionQuery.CalculateChunkCount();
            if (effectChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var effectRecordStream = new NativeStream(effectChunkCount, Allocator.TempJob);
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            try
            {
                if (effectChunkCount <= MainThreadChunkThreshold)
                {
                    foreach (var (_, effect) in SystemAPI
                                 .Query<RefRO<GEEffectSpecComponent>>()
                                 .WithAll<
                                     GEContextComponent,
                                     GEExecutionCalculationDefinitionBuffer,
                                     GEExecutionCalculationValueBuffer>()
                                 .WithEntityAccess())
                    {
                        EvaluateEffect(em, effect, ref eventWriter);
                    }

                    return;
                }

                var scanJob = new GEExecutionCalculationScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    EffectRecordWriter = effectRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_definitionQuery, state.Dependency);
                state.Dependency.Complete();

                var effectRecordReader = effectRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < effectRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = effectRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var record = effectRecordReader.Read<GEExecutionCalculationRecord>();
                        EvaluateEffect(em, record.Effect, ref eventWriter);
                    }
                    effectRecordReader.EndForEachIndex();
                }
            }
            finally
            {
                eventWriter.Dispose();
                effectRecordStream.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state) { }

        private struct GEExecutionCalculationRecord
        {
            public Entity Effect;
        }

        private struct GEExecutionCalculationScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public NativeStream.Writer EffectRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                EffectRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var effects = chunk.GetNativeArray(EntityTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    EffectRecordWriter.Write(new GEExecutionCalculationRecord
                    {
                        Effect = effects[entityIndex],
                    });
                }
                EffectRecordWriter.EndForEachIndex();
            }
        }

        private static void EvaluateEffect(
            EntityManager em,
            Entity effect,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (effect == Entity.Null || !em.Exists(effect))
                return;

            var context = em.GetComponentData<GEContextComponent>(effect);
            var spec = em.GetComponentData<GEEffectSpecComponent>(effect);
            var definitions = em.GetBuffer<GEExecutionCalculationDefinitionBuffer>(effect);
            var outputs = em.GetBuffer<GEExecutionCalculationValueBuffer>(effect);
            var hasInputs = em.IsComponentEnabled<GEExecutionCalculationInputDefinitionBuffer>(effect);
            var inputs = hasInputs
                ? em.GetBuffer<GEExecutionCalculationInputDefinitionBuffer>(effect)
                : default;

            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                var rawValue = hasInputs && HasInputsForOutput(inputs, definition.CalculationCode, definition.OutputKey)
                    ? ResolveInputAggregate(em, effect, in context, in spec, inputs, definition.CalculationCode, definition.OutputKey)
                    : ResolveInput(
                        em,
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
                    ExecutionCalculationRuntimeActions.EnqueueOutputUpdatedFact(
                        ref eventWriter,
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

        private static float ResolveInputAggregate(
            EntityManager em,
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
                    em,
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

        private static float ResolveInput(
            EntityManager em,
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
                EExecutionCalculationInputSource.SetByCaller =>
                    EffectMagnitudeResolver.ResolveSetByCaller(em, effect, key, fallbackValue),
                EExecutionCalculationInputSource.SourceAttribute =>
                    EffectMagnitudeResolver.ResolveAttributeCapture(
                        em,
                        effect,
                        inputIndex,
                        EMagnitudeSource.SourceAttribute,
                        context.SourceAsc,
                        attrSetCode,
                        attributeCode,
                        captureTiming,
                        fallbackValue),
                EExecutionCalculationInputSource.TargetAttribute =>
                    EffectMagnitudeResolver.ResolveAttributeCapture(
                        em,
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
