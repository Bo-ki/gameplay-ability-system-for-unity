using Unity.Burst;
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
        private const int MainThreadEntityThreshold = 256;

        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<TagMaskComponent, AttributeValueBuffer, AttributeDirtyComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var modifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(true);
            if (_query.CalculateEntityCount() <= MainThreadEntityThreshold)
            {
                foreach (var (attributes, dirty, changeEventPending, entity) in SystemAPI
                             .Query<DynamicBuffer<AttributeValueBuffer>, EnabledRefRW<AttributeDirtyComponent>, EnabledRefRW<AttributeChangeEventPendingComponent>>()
                             .WithAll<TagMaskComponent, AttributeDirtyComponent>()
                             .WithEntityAccess())
                {
                    if (RecalculateAttributes(entity, attributes, modifierLookup))
                        changeEventPending.ValueRW = true;
                    dirty.ValueRW = false;
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
                DynamicBuffer<AttributeValueBuffer> attributes,
                EnabledRefRW<AttributeDirtyComponent> dirty,
                EnabledRefRW<AttributeChangeEventPendingComponent> changeEventPending)
            {
                if (RecalculateAttributes(entity, attributes, ModifierBufferLookup))
                    changeEventPending.ValueRW = true;
                dirty.ValueRW = false;
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

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasBuffer<AttributeChangeEventBuffer>(eventBusEntity))
                return;

            var attributeEvents = em.GetBuffer<AttributeChangeEventBuffer>(eventBusEntity);
            foreach (var (attributes, changeEventPending, entity) in SystemAPI
                         .Query<DynamicBuffer<AttributeValueBuffer>, EnabledRefRW<AttributeChangeEventPendingComponent>>()
                         .WithAll<AttributeChangeEventPendingComponent>()
                         .WithEntityAccess())
            {
                AppendAttributeChangeEvents(entity, attributes, attributeEvents);
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
