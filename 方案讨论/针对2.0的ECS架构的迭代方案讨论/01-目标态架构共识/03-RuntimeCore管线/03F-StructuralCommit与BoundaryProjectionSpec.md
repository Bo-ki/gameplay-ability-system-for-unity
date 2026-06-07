# 03F：Structural Commit 与 Boundary Projection

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec | 拆分来源：`../03-RuntimeCore管线Spec.md` | 最近拆分：2026-06-07

本文件只描述理想 Runtime Core 目标态。禁止写入当前代码事实、迁移流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

定位：目标态 structural intent、ECB playback、presentation outbox 和 boundary projection 只读派生。

### Structural Commit：只提交结构变化

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASStructuralCommitSystemGroup))]
    public partial struct GASExpiredAbilityDestroySystem : ISystem
    {
        private EntityQuery _expiredAbilityQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _expiredAbilityQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AbilityStateComponent>(),
                    ComponentType.ReadOnly<AbilityPendingDestroyComponent>()
                }
            });

            state.RequireForUpdate(_expiredAbilityQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new DestroyExpiredAbilitiesJob
            {
                EntityType = state.GetEntityTypeHandle(),
                ECB = ecb
            }.ScheduleParallel(_expiredAbilityQuery, state.Dependency);
        }

        [BurstCompile]
        private struct DestroyExpiredAbilitiesJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var entities = chunk.GetNativeArray(EntityType);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    ECB.DestroyEntity(unfilteredChunkIndex, entities[entityIndex]);
                }
            }
        }
    }
}
```

合理性：

1. ECB 只用于 `DestroyEntity` 这类结构变化，不当 Gameplay Event Bus（`ECB-01`）。
2. Playback 位置属于明确的 `GASStructuralCommitSystemGroup`（`ECB-03`）。
3. 若有多个并行 job 产生结构变化，每个 job 单独创建 ECB；不复用同一个 ECB 造成 sortKey 域交错（`PRF-25`）。

### Boundary Projection：只读派生

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    public partial struct GASPresentationOutboxProjectionSystem : ISystem
    {
        private EntityQuery _factQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _factQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<GameplayEventBuffer>(),
                    ComponentType.ReadWrite<PresentationEventBuffer>()
                }
            });

            state.RequireForUpdate(_factQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ProjectPresentationEventsJob
            {
                FactBufferType = state.GetBufferTypeHandle<GameplayEventBuffer>(true),
                PresentationBufferType = state.GetBufferTypeHandle<PresentationEventBuffer>(false)
            }.ScheduleParallel(_factQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ProjectPresentationEventsJob : IJobChunk
        {
            [ReadOnly] public BufferTypeHandle<GameplayEventBuffer> FactBufferType;
            public BufferTypeHandle<PresentationEventBuffer> PresentationBufferType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var factsByAsc = chunk.GetBufferAccessor(ref FactBufferType);
                var presentationByAsc = chunk.GetBufferAccessor(ref PresentationBufferType);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    var facts = factsByAsc[entityIndex];
                    var presentation = presentationByAsc[entityIndex];

                    for (var i = 0; i < facts.Length; i++)
                    {
                        var fact = facts[i];
                        presentation.Add(new PresentationEventBuffer
                        {
                            Sequence = fact.Sequence,
                            EventCode = fact.EventCode,
                            TargetAsc = fact.TargetAsc,
                            Value = fact.Value
                        });
                    }
                }
            }
        }
    }
}
```

合理性：

1. Projection 只读 Core fact，写 boundary outbox，不参与 gameplay reaction（`SYS-05` `DBG-01`）。
2. 不访问 GameObject / VFX / UI 资源；真实 side effect 留给 Runtime Boundary / Application Shell。
3. Debugger / Replay 可在同组中采样或截断，但不得反向写 Core state。
