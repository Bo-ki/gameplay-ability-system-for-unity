# 03E-02：State Evaluate / ActiveEffect Store

> Owner：`01-目标态架构共识/03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-08

本文件只描述目标态 State Evaluate 与 ActiveEffect Store PostApply 变更边界。当前代码事实、验证数字、执行流水和迁移 proof 不写入本文件。

## 定位

State Evaluate 负责在 GE apply 后推进 owner-local active effect slot 状态。period due 的 GE command seed 必须回到 Effect Fan-In lane；PostApply 只修改 active effect store 内部时间状态和移除标记，不绕过 deterministic merge。

## 目标代码骨架

```csharp
using Unity.Burst;
using Unity.Entities;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASEffectFanInSystem))]
    public partial struct GASActiveEffectPostApplySystem : ISystem
    {
        private EntityQuery _ascQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ascQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ActiveGameplayEffectBuffer>()
                }
            });

            state.RequireForUpdate(_ascQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new TickActiveEffectsJob
            {
                EffectBufferType = state.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(false),
                Frame = SystemAPI.GetSingleton<GlobalTimer>().Frame
            }.ScheduleParallel(_ascQuery, state.Dependency);
        }

        [BurstCompile]
        private struct TickActiveEffectsJob : IJobChunk
        {
            public BufferTypeHandle<ActiveGameplayEffectBuffer> EffectBufferType;
            public int Frame;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var effectsByAsc = chunk.GetBufferAccessor(ref EffectBufferType);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    var effects = effectsByAsc[entityIndex];

                    for (var slotIndex = 0; slotIndex < effects.Length; slotIndex++)
                    {
                        var slot = effects[slotIndex];

                        switch (slot.State)
                        {
                            case ActiveEffectSlotState.Active:
                                TickActiveSlot(ref slot, Frame);
                                break;

                            case ActiveEffectSlotState.Inhibited:
                                if ((slot.Flags & ActiveEffectSlotFlags.TicksWhenInhibited) != 0)
                                    TickActiveSlot(ref slot, Frame);
                                break;

                            case ActiveEffectSlotState.PendingRemove:
                                slot.RemainingFrame = 0;
                                break;
                        }

                        effects[slotIndex] = slot;
                    }
                }
            }

            private static void TickActiveSlot(ref ActiveGameplayEffectBuffer slot, int frame)
            {
                if ((slot.Flags & ActiveEffectSlotFlags.HasDuration) != 0)
                {
                    slot.RemainingFrame -= 1;
                    if (slot.RemainingFrame <= 0)
                    {
                        slot.PreviousState = slot.State;
                        slot.State = ActiveEffectSlotState.PendingRemove;
                    }
                }

                // Period due detection is owned by GASActiveEffectPreTickSystem so
                // period commands enter the same deterministic Fan-In merge as all
                // other GE commands. PostApply only mutates owner-local store state.
            }
        }
    }
}
```

## 合理性

1. ActiveEffect 生命周期状态数少，单 job enum switch 是默认策略。
2. period due 的 `GEEffectCommandRecord` 由 Effect Fan-In lane 的 producer 写入 `NativeStream`；本系统只提交 owner-local slot 时间状态和 chunk skip 元数据，避免 State lane 在 Fan-In 之后绕过 deterministic merge。
3. expire / remove 不创建临时 entity；真正的 destroy / add / remove 进入 Structural Commit。
4. slot flags 表达 granted tags / abilities / period / stack 等状态，不按状态增删 component，避免 archetype 爆炸。

## 与 ActiveEffectStore 总体 Spec 的关系

1. [05 ActiveEffectStore](../../05-ActiveEffectStoreSpec.md) 维护 store 总体目标规则、生命周期不变量和外部访问边界。
2. 本文件只维护 State Evaluate lane 的 PostApply 代码骨架和 owner-local 状态推进规则。
3. 如果出现 store 数据结构、slot schema 或 lifetime 总体规则变化，先更新 `05`，再由本文件引用，不复制正文。

## 反向入口

- 03E 子页索引：[README.md](README.md)
- 03E 根索引：[../03E-EffectFanIn-State-Attribute-FactSpec.md](../03E-EffectFanIn-State-Attribute-FactSpec.md)
