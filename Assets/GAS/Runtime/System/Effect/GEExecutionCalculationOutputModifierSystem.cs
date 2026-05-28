using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationExtensionSystemGroup))]
    public partial struct GEExecutionCalculationOutputModifierSystem : ISystem
    {
        private EntityQuery _outputQuery;

        public void OnCreate(ref SystemState state)
        {
            _outputQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    GEContextComponent,
                    GEEffectSpecComponent,
                    GEExecutionCalculationValueBuffer,
                    GEResolvedModifierBuffer,
                    GEExecutionCalculationOutputModifierDefinitionBuffer>()
                .WithNone<GEExecutionCalculationOutputModifierAppliedComponent>()
                .Build();
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
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
            var effectRecordStream = new NativeStream(effectChunkCount, Allocator.TempJob);
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            try
            {
                var scanJob = new GEExecutionCalculationOutputModifierScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    EffectRecordWriter = effectRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_outputQuery, state.Dependency);
                state.Dependency.Complete();

                var effectRecordReader = effectRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < effectRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = effectRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var record = effectRecordReader.Read<GEExecutionCalculationOutputModifierRecord>();
                        var deltaCount = ApplyOutputModifiers(em, record.Effect, ref stream, deltas, frame);
                        ecb.SetComponent(record.Effect, new GEExecutionCalculationOutputModifierAppliedComponent
                        {
                            Frame = frame,
                            DeltaCount = deltaCount,
                        });
                        ecb.SetComponentEnabled<GEExecutionCalculationOutputModifierAppliedComponent>(record.Effect, true);
                    }
                    effectRecordReader.EndForEachIndex();
                }

                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                effectRecordStream.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state) { }

        private struct GEExecutionCalculationOutputModifierRecord
        {
            public Entity Effect;
        }

        private struct GEExecutionCalculationOutputModifierScanJob : IJobChunk
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
                    EffectRecordWriter.Write(new GEExecutionCalculationOutputModifierRecord
                    {
                        Effect = effects[entityIndex],
                    });
                }
                EffectRecordWriter.EndForEachIndex();
            }
        }

        private static int ApplyOutputModifiers(
            EntityManager em,
            Entity effect,
            ref GEEffectCommandStreamComponent stream,
            DynamicBuffer<AttributeModifierBuffer> deltas,
            int frame)
        {
            if (effect == Entity.Null || !em.Exists(effect))
                return 0;

            var context = em.GetComponentData<GEContextComponent>(effect);
            if (context.TargetAsc == Entity.Null
                || !em.Exists(context.TargetAsc)
                || !em.HasBuffer<AttributeValueBuffer>(context.TargetAsc))
            {
                return 0;
            }

            var spec = em.GetComponentData<GEEffectSpecComponent>(effect);
            var values = em.GetBuffer<GEExecutionCalculationValueBuffer>(effect);
            var definitions = em.GetBuffer<GEExecutionCalculationOutputModifierDefinitionBuffer>(effect);
            var resolved = em.GetBuffer<GEResolvedModifierBuffer>(effect);
            var attributes = em.GetBuffer<AttributeValueBuffer>(context.TargetAsc);

            resolved.Clear();
            var deltaCount = 0;
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                var rawMagnitude = ExecutionCalculationRuntimeActions.TryGetOutputValue(
                    values,
                    definition.OutputKey,
                    out var outputValue)
                    ? outputValue
                    : definition.FallbackMagnitude;
                var magnitude = ApplyTransform(
                    rawMagnitude,
                    definition.Coefficient,
                    definition.PreAdd,
                    definition.PostAdd);

                resolved.Add(new GEResolvedModifierBuffer
                {
                    AttrSetCode = definition.AttrSetCode,
                    AttributeCode = definition.AttributeCode,
                    Op = definition.Op,
                    Magnitude = magnitude,
                    SourceEffect = effect,
                });

                if (ApplyModifierDelta(
                        attributes,
                        definition.AttrSetCode,
                        definition.AttributeCode,
                        definition.Op,
                        magnitude,
                        out var oldValue,
                        out var newValue))
                {
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
            }

            return deltaCount;
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
            newValue = AttributeHelper.ApplyModifier(attribute.BaseValue, op, magnitude);
            attribute.CurrentValue = newValue;
            AttributeHelper.Clamp(ref attribute);
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
