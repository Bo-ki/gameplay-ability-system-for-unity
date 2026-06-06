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
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [BurstCompile]
    public partial struct AttributeRecalculateSystem : ISystem
    {
        private EntityQuery _dirtyQuery;
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _dirtyQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    TagMaskComponent,
                    AttributeValueBuffer,
                    AttributeDirtyComponent>()
                .Build();

            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<TagMaskComponent>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadWrite<AttributeDirtyComponent>(),
                    ComponentType.ReadWrite<AttributeChangeEventPendingComponent>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate(_dirtyQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var modifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(true);
            state.Dependency = new AttributeRecalculateJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                AttributeTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(),
                DirtyTypeHandle = SystemAPI.GetComponentTypeHandle<AttributeDirtyComponent>(),
                ChangeEventPendingTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AttributeChangeEventPendingComponent>(),
                ModifierBufferLookup = modifierLookup,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct AttributeRecalculateJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public BufferTypeHandle<AttributeValueBuffer> AttributeTypeHandle;
            public ComponentTypeHandle<AttributeDirtyComponent> DirtyTypeHandle;
            public ComponentTypeHandle<AttributeChangeEventPendingComponent> ChangeEventPendingTypeHandle;
            [ReadOnly] public BufferLookup<AttributeActiveModifierBuffer> ModifierBufferLookup;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var attributes = chunk.GetBufferAccessor(ref AttributeTypeHandle);
                var dirtyMask = chunk.GetEnabledMask(ref DirtyTypeHandle);
                var changeEventPendingMask = chunk.GetEnabledMask(ref ChangeEventPendingTypeHandle);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    if (!dirtyMask.GetBit(entityIndex))
                        continue;

                    if (RecalculateAttributes(
                            entities[entityIndex],
                            attributes[entityIndex],
                            ModifierBufferLookup))
                    {
                        changeEventPendingMask[entityIndex] = true;
                    }

                    dirtyMask[entityIndex] = false;
                }
            }
        }

        private static bool RecalculateAttributes(
            Entity entity,
            DynamicBuffer<AttributeValueBuffer> attributes,
            BufferLookup<AttributeActiveModifierBuffer> modifierLookup)
        {
            var hasModifiers = modifierLookup.HasBuffer(entity);
            var modifiers = hasModifiers ? modifierLookup[entity] : default;
            var hasPendingChange = false;

            for (var i = 0; i < attributes.Length; i++)
            {
                var attr = attributes[i];
                if (!attr.Dirty)
                {
                    if (attr.CurrentValueChangePending)
                        hasPendingChange = true;
                    continue;
                }

                var eventPreviousValue = attr.CurrentValueChangePending
                    ? attr.PreviousCurrentValue
                    : attr.CurrentValue;
                attr.CurrentValue = attr.BaseValue;

                if (hasModifiers)
                {
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
                    hasPendingChange = true;
                }
                else
                {
                    attr.PreviousCurrentValue = attr.CurrentValue;
                    attr.CurrentValueChangePending = false;
                }

                attr.Dirty = false;
                attributes[i] = attr;
            }

            return hasPendingChange;
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
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(AttributeRecalculateSystem))]
    public partial struct AttributeChangeEventProjectionSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AttributeValueBuffer, AttributeChangeEventPendingComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBusEntity))
                return;

            state.Dependency = new AttributeChangeEventProjectionJob
            {
                EventBusEntity = eventBusEntity,
                AttributeEventsLookup = SystemAPI.GetBufferLookup<AttributeChangeEventBuffer>(),
            }.Schedule(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct AttributeChangeEventProjectionJob : IJobEntity
        {
            public Entity EventBusEntity;
            public BufferLookup<AttributeChangeEventBuffer> AttributeEventsLookup;

            private void Execute(
                Entity entity,
                DynamicBuffer<AttributeValueBuffer> attributes,
                EnabledRefRW<AttributeChangeEventPendingComponent> changeEventPending)
            {
                if (EventBusEntity != Entity.Null && AttributeEventsLookup.HasBuffer(EventBusEntity))
                {
                    var attributeEvents = AttributeEventsLookup[EventBusEntity];
                    AppendAttributeChangeEvents(entity, attributes, attributeEvents);
                }

                changeEventPending.ValueRW = false;
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
