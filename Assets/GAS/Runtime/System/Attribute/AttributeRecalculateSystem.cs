using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    /// <summary>
    /// Burst 兼容的属性重算 System。遍历 AttributeActiveModifierBuffer Buffer 计算 CurrentValue。
    /// </summary>
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [BurstCompile]
    public partial struct AttributeRecalculateSystem : ISystem
    {
        private const int MainThreadEntityThreshold = 64;

        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<TagMaskComponent, AttributeValueBuffer>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var modifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(true);
            if (_query.CalculateEntityCount() <= MainThreadEntityThreshold)
            {
                foreach (var (attributes, entity) in SystemAPI
                             .Query<DynamicBuffer<AttributeValueBuffer>>()
                             .WithAll<TagMaskComponent>()
                             .WithEntityAccess())
                {
                    RecalculateAttributes(entity, attributes, modifierLookup);
                }

                return;
            }

            state.Dependency = new AttributeRecalculateJob
            {
                ModifierBufferLookup = modifierLookup,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct AttributeRecalculateJob : IJobEntity
        {
            [ReadOnly] public BufferLookup<AttributeActiveModifierBuffer> ModifierBufferLookup;

            private void Execute(
                Entity entity,
                DynamicBuffer<AttributeValueBuffer> attributes)
            {
                RecalculateAttributes(entity, attributes, ModifierBufferLookup);
            }
        }

        private static void RecalculateAttributes(
            Entity entity,
            DynamicBuffer<AttributeValueBuffer> attributes,
            BufferLookup<AttributeActiveModifierBuffer> modifierLookup)
        {
            var hasModifiers = modifierLookup.HasBuffer(entity);

            for (var i = 0; i < attributes.Length; i++)
            {
                var attr = attributes[i];
                if (!attr.Dirty)
                    continue;

                var eventPreviousValue = attr.CurrentValueChangePending
                    ? attr.PreviousCurrentValue
                    : attr.CurrentValue;
                attr.CurrentValue = attr.BaseValue;

                if (hasModifiers)
                {
                    var modifiers = modifierLookup[entity];
                    for (var j = 0; j < modifiers.Length; j++)
                    {
                        var mod = modifiers[j];
                        if (mod.AttrSetCode != attr.AttrSetCode || mod.AttributeCode != attr.Code)
                            continue;

                        attr.CurrentValue = ApplyModifier(attr.CurrentValue, mod.Op, mod.Magnitude);
                    }
                }

                if (attr.IsClampMin)
                    attr.CurrentValue = math.max(attr.CurrentValue, attr.MinValue);
                if (attr.IsClampMax)
                    attr.CurrentValue = math.min(attr.CurrentValue, attr.MaxValue);

                if (eventPreviousValue != attr.CurrentValue)
                {
                    attr.PreviousCurrentValue = eventPreviousValue;
                    attr.CurrentValueChangePending = true;
                }
                else
                {
                    attr.PreviousCurrentValue = attr.CurrentValue;
                    attr.CurrentValueChangePending = false;
                }

                attr.Dirty = false;
                attributes[i] = attr;
            }
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
    }

    /// <summary>
    /// 将 CurrentValue 聚合结果投影为事件事实。它不参与数值计算。
    /// </summary>
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(AttributeRecalculateSystem))]
    public partial struct AttributeChangeEventProjectionSystem : ISystem
    {
        private const int MainThreadChunkThreshold = 2;

        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AttributeValueBuffer>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasBuffer<AttributeChangeEventBuffer>(eventBusEntity))
                return;

            var chunkCount = _query.CalculateChunkCount();
            if (chunkCount <= 0)
                return;

            if (chunkCount <= MainThreadChunkThreshold)
            {
                var attributeEvents = em.GetBuffer<AttributeChangeEventBuffer>(eventBusEntity);
                foreach (var (attributes, entity) in SystemAPI
                             .Query<DynamicBuffer<AttributeValueBuffer>>()
                             .WithEntityAccess())
                {
                    AppendAttributeChangeEvents(entity, attributes, attributeEvents);
                }

                return;
            }

            var eventStream = new NativeStream(chunkCount, Allocator.TempJob);

            try
            {
                var collectJob = new AttributeChangeEventCollectJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    AttributeTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: false),
                    EventWriter = eventStream.AsWriter(),
                };

                state.Dependency = collectJob.ScheduleParallel(_query, state.Dependency);
                state.Dependency.Complete();

                AppendCollectedEvents(eventStream.AsReader(), em.GetBuffer<AttributeChangeEventBuffer>(eventBusEntity));
            }
            finally
            {
                eventStream.Dispose();
            }
        }

        private static void AppendCollectedEvents(
            NativeStream.Reader eventReader,
            DynamicBuffer<AttributeChangeEventBuffer> attributeEvents)
        {
            for (var streamIndex = 0; streamIndex < eventReader.ForEachCount; streamIndex++)
            {
                var eventCount = eventReader.BeginForEachIndex(streamIndex);
                for (var i = 0; i < eventCount; i++)
                    attributeEvents.Add(eventReader.Read<AttributeChangeEventBuffer>());
                eventReader.EndForEachIndex();
            }
        }

        [BurstCompile]
        private struct AttributeChangeEventCollectJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public BufferTypeHandle<AttributeValueBuffer> AttributeTypeHandle;
            public NativeStream.Writer EventWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                EventWriter.BeginForEachIndex(unfilteredChunkIndex);

                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var attributeBuffers = chunk.GetBufferAccessor(ref AttributeTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var attributes = attributeBuffers[entityIndex];
                    for (var attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
                    {
                        var attr = attributes[attributeIndex];
                        if (!attr.CurrentValueChangePending)
                            continue;

                        if (attr.PreviousCurrentValue != attr.CurrentValue)
                        {
                            EventWriter.Write(CreateAttributeChangeEvent(entities[entityIndex], in attr));
                        }

                        attr.PreviousCurrentValue = attr.CurrentValue;
                        attr.CurrentValueChangePending = false;
                        attributes[attributeIndex] = attr;
                    }
                }

                EventWriter.EndForEachIndex();
            }
        }

        private static void AppendAttributeChangeEvents(
            Entity entity,
            DynamicBuffer<AttributeValueBuffer> attributes,
            DynamicBuffer<AttributeChangeEventBuffer> attributeEvents)
        {
            for (var attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
            {
                var attr = attributes[attributeIndex];
                if (!attr.CurrentValueChangePending)
                    continue;

                if (attr.PreviousCurrentValue != attr.CurrentValue)
                    attributeEvents.Add(CreateAttributeChangeEvent(entity, in attr));

                attr.PreviousCurrentValue = attr.CurrentValue;
                attr.CurrentValueChangePending = false;
                attributes[attributeIndex] = attr;
            }
        }

        private static AttributeChangeEventBuffer CreateAttributeChangeEvent(
            Entity asc,
            in AttributeValueBuffer attr)
        {
            return new AttributeChangeEventBuffer
            {
                ASC = asc,
                AttrSetCode = attr.AttrSetCode,
                AttributeCode = attr.Code,
                OldValue = attr.PreviousCurrentValue,
                NewValue = attr.CurrentValue,
                IsBaseValue = false,
            };
        }
    }
}
