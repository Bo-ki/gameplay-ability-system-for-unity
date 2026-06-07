# 03E-01：Effect Fan-In

> Owner：`01-目标态架构共识/03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-08

本文件只描述目标态 Effect Fan-In。当前代码事实、验证数字、执行流水和迁移 proof 不写入本文件。

## 定位

Effect Fan-In 负责把 ability command、period/passive command 和 previous-frame reaction command 收敛为统一的 `GEEffectCommandRecord`，通过 frame-local `NativeStream` 并行写入，再执行 deterministic merge，最终写入目标 ASC 的 owner-local command buffer。

## 目标代码骨架

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    public partial struct GASEffectFanInSystem : ISystem
    {
        private EntityQuery _producerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _producerQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AbilityCommandComponent>(),
                    ComponentType.ReadOnly<TargetDataBuffer>()
                }
            });

            state.RequireForUpdate<GlobalTimer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var chunkCount = _producerQuery.CalculateChunkCount();
            if (chunkCount == 0)
                return;

            var commandStream = new NativeStream(chunkCount, Allocator.TempJob);
            var sortedCommands = new NativeList<GEEffectCommandRecord>(Allocator.TempJob);
            var collectJob = new CollectEffectCommandsJob
            {
                CommandType = state.GetComponentTypeHandle<AbilityCommandComponent>(true),
                TargetBufferType = state.GetBufferTypeHandle<TargetDataBuffer>(true),
                CommandWriter = commandStream.AsWriter(),
                Frame = SystemAPI.GetSingleton<GlobalTimer>().Frame
            }.ScheduleParallel(_producerQuery, state.Dependency);

            var ownerCommands = state.GetBufferLookup<GEEffectCommandBuffer>(false);
            var mergeJob = new MergeEffectCommandsJob
            {
                CommandReader = commandStream.AsReader(),
                SortedCommands = sortedCommands,
                OwnerCommandBuffers = ownerCommands
            }.Schedule(collectJob);

            var disposeStreamJob = commandStream.Dispose(mergeJob);
            state.Dependency = sortedCommands.Dispose(disposeStreamJob);
        }

        [BurstCompile]
        private struct CollectEffectCommandsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<AbilityCommandComponent> CommandType;
            [ReadOnly] public BufferTypeHandle<TargetDataBuffer> TargetBufferType;
            public NativeStream.Writer CommandWriter;
            public int Frame;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var commands = chunk.GetNativeArray(ref CommandType);
                var targets = chunk.GetBufferAccessor(ref TargetBufferType);

                CommandWriter.BeginForEachIndex(unfilteredChunkIndex);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    var command = commands[entityIndex];
                    if (command.Status != AbilityCommandStatus.Valid)
                        continue;

                    WriteEffectCommand(
                        command,
                        (command.InputSequence << 16) ^ (unfilteredChunkIndex << 8) ^ 0x01,
                        command.TargetGroupSortKey,
                        command.CostGameplayEffectCode,
                        command.CostGameplayEffectDefinitionIndex,
                        command.SourceAsc);

                    WriteEffectCommand(
                        command,
                        (command.InputSequence << 16) ^ (unfilteredChunkIndex << 8) ^ 0x02,
                        command.TargetGroupSortKey,
                        command.CooldownGameplayEffectCode,
                        command.CooldownGameplayEffectDefinitionIndex,
                        command.SourceAsc);

                    var targetList = targets[entityIndex];

                    for (var targetIndex = 0; targetIndex < targetList.Length; targetIndex++)
                    {
                        var target = targetList[targetIndex];
                        var sequence = (command.InputSequence << 16) ^ (unfilteredChunkIndex << 8) ^ (targetIndex + 0x100);
                        WriteEffectCommand(
                            command,
                            sequence,
                            target.TargetSortKey,
                            command.PrimaryGameplayEffectCode,
                            command.PrimaryGameplayEffectDefinitionIndex,
                            target.TargetAsc);
                    }
                }

                CommandWriter.EndForEachIndex();
            }

            private void WriteEffectCommand(
                in AbilityCommandComponent command,
                int sequence,
                int sortKey,
                int gameplayEffectCode,
                int gameplayEffectDefinitionIndex,
                Entity targetAsc)
            {
                if (gameplayEffectCode == 0 || gameplayEffectDefinitionIndex < 0)
                    return;

                CommandWriter.Write(new GEEffectCommandRecord
                {
                    Sequence = sequence,
                    SortKey = sortKey,
                    Frame = Frame,
                    GameplayEffectCode = gameplayEffectCode,
                    GameplayEffectDefinitionIndex = gameplayEffectDefinitionIndex,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = targetAsc,
                    SourceAbility = command.AbilityEntity,
                    Level = command.Level
                });
            }
        }

        [BurstCompile]
        private struct MergeEffectCommandsJob : IJob
        {
            [ReadOnly] public NativeStream.Reader CommandReader;
            public NativeList<GEEffectCommandRecord> SortedCommands;
            public BufferLookup<GEEffectCommandBuffer> OwnerCommandBuffers;

            public void Execute()
            {
                for (var forEachIndex = 0; forEachIndex < CommandReader.ForEachCount; forEachIndex++)
                {
                    CommandReader.BeginForEachIndex(forEachIndex);
                    while (CommandReader.RemainingItemCount > 0)
                    {
                        SortedCommands.Add(CommandReader.Read<GEEffectCommandRecord>());
                    }
                    CommandReader.EndForEachIndex();
                }

                SortedCommands.Sort(new EffectCommandComparer());

                for (var i = 0; i < SortedCommands.Length; i++)
                {
                    var command = SortedCommands[i];
                    if (!OwnerCommandBuffers.HasBuffer(command.TargetAsc))
                        continue;

                    OwnerCommandBuffers[command.TargetAsc].Add(new GEEffectCommandBuffer
                    {
                        Sequence = command.Sequence,
                        SortKey = command.SortKey,
                        Frame = command.Frame,
                        GameplayEffectCode = command.GameplayEffectCode,
                        GameplayEffectDefinitionIndex = command.GameplayEffectDefinitionIndex,
                        SourceAsc = command.SourceAsc,
                        TargetAsc = command.TargetAsc,
                        SourceAbility = command.SourceAbility,
                        Level = command.Level
                    });
                }
            }
        }

        private struct EffectCommandComparer : System.Collections.Generic.IComparer<GEEffectCommandRecord>
        {
            public int Compare(GEEffectCommandRecord x, GEEffectCommandRecord y)
            {
                var sortCompare = x.SortKey.CompareTo(y.SortKey);
                return sortCompare != 0 ? sortCompare : x.Sequence.CompareTo(y.Sequence);
            }
        }
    }
}
```

## 合理性

1. Producer 并行写 `NativeStream` 的 `GEEffectCommandRecord`，避免全局 DynamicBuffer 写竞争。
2. Primary GE 按目标列表写入 target；cost / cooldown GE 是同一次 activation 的 source-side command seed，目标为 `SourceAsc`。它们共用 Definition Catalog 解析出的 GE index，不在 Fan-In 阶段再查 managed config。
3. Ability command、period/passive command、previous-frame reaction command 都应按同一 record 形态进入 Fan-In；不同 producer 可以使用独立 `NativeStream` 后统一 merge，或使用不重叠的 `forEachIndex` range，禁止用 ECB / singleton buffer 充当 gameplay command bus。
4. Merge 阶段按 deterministic `TargetSortKey / Sequence` 排序，避免依赖 worker thread、entity index 或 `ParallelWriter` 的不确定顺序。
5. Merge 阶段通过 `BufferLookup<GEEffectCommandBuffer>` 对 target ASC 做单线程确定性 append；这仍是随机访问，但被限制在 merge job 内。如果 merge 成本或随机写压力超过 budget，应按 `TargetAsc` 排序后切成 target range，再改为 chunk-local / range-local apply。

## 官方依据

1. `NativeStream` / NativeContainer 适合 frame-local 并行 fan-in，但生命周期、allocator 和 dispose 必须明确。
2. Gameplay command bus 不应由 ECB 或 singleton DynamicBuffer 承担；ECB 只用于结构变化意图回放。
3. 确定性输出不得依赖无序并行写入，必须通过稳定 sort key 和 sequence 合并。

## 反向入口

- 03E 子页索引：[README.md](README.md)
- 03E 根索引：[../03E-EffectFanIn-State-Attribute-FactSpec.md](../03E-EffectFanIn-State-Attribute-FactSpec.md)
