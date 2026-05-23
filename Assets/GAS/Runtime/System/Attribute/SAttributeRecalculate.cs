using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    /// <summary>
    /// Burst 兼容的属性重算 System。遍历 BActiveModifier Buffer 计算 CurrentValue。
    /// </summary>
    [UpdateInGroup(typeof(GASAttributeGroup))]
    [BurstCompile]
    public partial struct SAttributeRecalculate : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CTagMask, BAttribute>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var modifierLookup = SystemAPI.GetBufferLookup<BActiveModifier>(true);

            state.Dependency = new RecalculateJob
            {
                ModifierBufferLookup = modifierLookup,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct RecalculateJob : IJobEntity
        {
            [ReadOnly] public BufferLookup<BActiveModifier> ModifierBufferLookup;

            private void Execute(
                Entity entity,
                DynamicBuffer<BAttribute> attributes)
            {
                bool hasModifiers = ModifierBufferLookup.HasBuffer(entity);

                for (int i = 0; i < attributes.Length; i++)
                {
                    var attr = attributes[i];
                    if (!attr.Dirty) continue;

                    var eventPreviousValue = attr.CurrentValueChangePending
                        ? attr.PreviousCurrentValue
                        : attr.CurrentValue;
                    attr.CurrentValue = attr.BaseValue;

                    if (hasModifiers)
                    {
                        var modifiers = ModifierBufferLookup[entity];
                        for (int j = 0; j < modifiers.Length; j++)
                        {
                            var mod = modifiers[j];
                            if (mod.AttrSetCode != attr.AttrSetCode || mod.AttributeCode != attr.Code) continue;

                            attr.CurrentValue = ApplyModifier(attr.CurrentValue, mod.Op, mod.Magnitude);
                        }
                    }

                    if (attr.IsClampMin) attr.CurrentValue = math.max(attr.CurrentValue, attr.MinValue);
                    if (attr.IsClampMax) attr.CurrentValue = math.min(attr.CurrentValue, attr.MaxValue);

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
    }

    /// <summary>
    /// 将 CurrentValue 聚合结果投影为事件事实。它不参与数值计算。
    /// </summary>
    [UpdateInGroup(typeof(GASAttributeGroup))]
    [UpdateAfter(typeof(SAttributeRecalculate))]
    public partial struct SAttributeChangeEventProjection : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<BAttribute>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasBuffer<BAttributeChangeEvent>(eventBusEntity))
                return;

            var attributeEvents = em.GetBuffer<BAttributeChangeEvent>(eventBusEntity);
            foreach (var (attributeBuffer, asc) in SystemAPI.Query<DynamicBuffer<BAttribute>>()
                         .WithEntityAccess())
            {
                var attributes = attributeBuffer;
                for (var j = 0; j < attributes.Length; j++)
                {
                    var attr = attributes[j];
                    if (!attr.CurrentValueChangePending)
                        continue;

                    if (attr.PreviousCurrentValue != attr.CurrentValue)
                    {
                        attributeEvents.Add(new BAttributeChangeEvent
                        {
                            ASC = asc,
                            AttrSetCode = attr.AttrSetCode,
                            AttributeCode = attr.Code,
                            OldValue = attr.PreviousCurrentValue,
                            NewValue = attr.CurrentValue,
                            IsBaseValue = false,
                        });
                    }

                    attr.PreviousCurrentValue = attr.CurrentValue;
                    attr.CurrentValueChangePending = false;
                    attributes[j] = attr;
                }
            }
        }
    }
}
