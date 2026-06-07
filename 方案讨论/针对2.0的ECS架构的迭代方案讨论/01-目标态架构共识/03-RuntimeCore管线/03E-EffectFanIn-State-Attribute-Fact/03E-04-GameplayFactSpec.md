# 03E-04：Gameplay Fact

> Owner：`01-目标态架构共识/03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-08

本文件只描述目标态 Gameplay Fact 投影。当前代码事实、验证数字、执行流水和迁移 proof 不写入本文件。

## 定位

Gameplay Fact 是 Core reaction 输入，不是 Presentation event bus。Fact projection 只读 committed AttributeSet 与 dirty mask，写本 target 的 fact buffer；Ability trigger / reactive GE 可以消费 fact 并在后续帧重新进入 Effect Fan-In。

## 目标代码骨架

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASAttributeSetReduceApplySystem))]
    public partial struct GASDeathFactProjectionSystem : ISystem
    {
        private EntityQuery _targetQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<CombatAttributeCurrentSetComponent>(),
                    ComponentType.ReadOnly<AttributeDirtyMaskComponent>(),
                    ComponentType.ReadWrite<GameplayEventBuffer>()
                }
            });

            state.RequireForUpdate(_targetQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ProjectDeathFactsJob
            {
                EntityType = state.GetEntityTypeHandle(),
                CombatCurrentType = state.GetComponentTypeHandle<CombatAttributeCurrentSetComponent>(true),
                DirtyMaskType = state.GetComponentTypeHandle<AttributeDirtyMaskComponent>(true),
                FactBufferType = state.GetBufferTypeHandle<GameplayEventBuffer>(false)
            }.ScheduleParallel(_targetQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ProjectDeathFactsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<CombatAttributeCurrentSetComponent> CombatCurrentType;
            [ReadOnly] public ComponentTypeHandle<AttributeDirtyMaskComponent> DirtyMaskType;
            public BufferTypeHandle<GameplayEventBuffer> FactBufferType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var combatValues = chunk.GetNativeArray(ref CombatCurrentType);
                var dirtyMasks = chunk.GetNativeArray(ref DirtyMaskType);
                var factsByTarget = chunk.GetBufferAccessor(ref FactBufferType);

                if (!useEnabledMask)
                {
                    for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                    {
                        ProjectEntity(entityIndex, unfilteredChunkIndex, entities, combatValues, dirtyMasks, factsByTarget);
                    }

                    return;
                }

                var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    ProjectEntity(entityIndex, unfilteredChunkIndex, entities, combatValues, dirtyMasks, factsByTarget);
                }
            }

            private static void ProjectEntity(
                int entityIndex,
                int unfilteredChunkIndex,
                NativeArray<Entity> entities,
                NativeArray<CombatAttributeCurrentSetComponent> combatValues,
                NativeArray<AttributeDirtyMaskComponent> dirtyMasks,
                BufferAccessor<GameplayEventBuffer> factsByTarget)
            {
                var dirty = dirtyMasks[entityIndex];
                if ((dirty.CombatWord & AttributeDirtyBits.Health) == 0)
                    return;

                var combat = combatValues[entityIndex];
                if (combat.Health > 0f)
                    return;

                factsByTarget[entityIndex].Add(new GameplayEventBuffer
                {
                    Sequence = unfilteredChunkIndex << 16 | entityIndex,
                    EventCode = GameplayEventCodes.DamageResolved,
                    SourceAsc = Entity.Null,
                    TargetAsc = entities[entityIndex],
                    Value = 0f
                });
            }
        }
    }
}
```

## 合理性

1. Gameplay Fact 是 Core reaction 输入，不是 Presentation event bus；Ability trigger / reactive GE 可以消费该 fact 并重新进入 Effect Fan-In。
2. 该 system 只读 committed AttributeSet 与 dirty mask、写本 target 的 fact buffer，不做结构变化；死亡销毁、grant / remove 等进入 Structural Commit。
3. `AttributeDirtyMaskComponent` 将 AttributeSet 打包后的业务变化重新收敛到具体 AttributeCode，避免 Boundary / Fact 因 set 打包而全量扫描所有属性字段。
4. Fact projection 与 Boundary Projection 分离，Presentation / Replay / Debugger 只能观察 fact，不反向驱动 simulation。

## 与 Structural Commit / Boundary Projection 的关系

1. [03F Structural Commit 与 Boundary Projection](../03F-StructuralCommit与BoundaryProjectionSpec.md) 负责结构变化意图、ECB playback、presentation outbox 和只读 projection。
2. 本文件只维护 Core reaction fact 的生成规则，不维护表现层事件格式或结构变化回放策略。
3. 如果 fact 被用于表现层或 replay，必须先由 Boundary Projection 生成只读派生，不能让 Presentation 反写 Core。

## 反向入口

- 03E 子页索引：[README.md](README.md)
- 03E 根索引：[../03E-EffectFanIn-State-Attribute-FactSpec.md](../03E-EffectFanIn-State-Attribute-FactSpec.md)
