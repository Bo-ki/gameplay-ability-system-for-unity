# 03D：Command Resolve 与 Target Resolve

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec | 拆分来源：`../03-RuntimeCore管线Spec.md` | 最近拆分：2026-06-07

本文件只描述理想 Runtime Core 目标态。禁止写入当前代码事实、迁移流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

定位：目标态 Ability command ingest、Core Ability Producer、NativeStream command records、target resolve 和 request-owned TargetDataBuffer。

### Ability Command Ingest：request-owned command normalization

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    public partial struct AbilityCommandIngestSystem : ISystem
    {
        private EntityQuery _requestQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _requestQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AbilityActivationRequestComponent>(),
                    ComponentType.ReadWrite<AbilityCommandComponent>(),
                    ComponentType.ReadWrite<TargetDataBuffer>()
                }
            });

            state.RequireForUpdate(_requestQuery);
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var definitionCatalog = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>().DefinitionCatalogBlob;
            if (!definitionCatalog.IsCreated)
                return;

            state.Dependency = new IngestAbilityCommandsJob
            {
                RequestType = state.GetComponentTypeHandle<AbilityActivationRequestComponent>(true),
                CommandType = state.GetComponentTypeHandle<AbilityCommandComponent>(false),
                AbilityStates = state.GetComponentLookup<AbilityStateComponent>(true),
                DefinitionCatalogBlob = definitionCatalog
            }.ScheduleParallel(_requestQuery, state.Dependency);
        }

        [BurstCompile]
        private struct IngestAbilityCommandsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<AbilityActivationRequestComponent> RequestType;
            public ComponentTypeHandle<AbilityCommandComponent> CommandType;
            [ReadOnly] public ComponentLookup<AbilityStateComponent> AbilityStates;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> DefinitionCatalogBlob;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var requests = chunk.GetNativeArray(ref RequestType);
                var commands = chunk.GetNativeArray(ref CommandType);
                ref var catalog = ref DefinitionCatalogBlob.Value;

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    var request = requests[entityIndex];
                    var command = new AbilityCommandComponent
                    {
                        SourceAsc = request.SourceAsc,
                        AbilityEntity = request.AbilityEntity,
                        ExplicitTargetAsc = request.ExplicitTargetAsc,
                        InputSequence = request.InputSequence,
                        RequestFrame = request.RequestFrame,
                        TargetGroupSortKey = request.TargetGroupSortKey,
                        TargetMode = request.TargetMode,
                        Status = AbilityCommandStatus.Rejected
                    };

                    if (!AbilityStates.HasComponent(request.AbilityEntity))
                    {
                        commands[entityIndex] = command;
                        continue;
                    }

                    var ability = AbilityStates[request.AbilityEntity];
                    var executable = (ability.Flags & AbilityRuntimeFlags.Executable) != 0;
                    var blocked = (ability.Flags & AbilityRuntimeFlags.Blocked) != 0;
                    var abilityDefinitionIndex = ability.AbilityDefinitionIndex;

                    if (ability.OwnerAsc != request.SourceAsc ||
                        ability.State != AbilityRuntimeState.Ready ||
                        !executable ||
                        blocked)
                    {
                        commands[entityIndex] = command;
                        continue;
                    }

                    if (abilityDefinitionIndex < 0 ||
                        abilityDefinitionIndex >= catalog.Abilities.Length)
                    {
                        if (!GASGeneratedDefinitionLookup.TryGetAbilityIndex(ref catalog, ability.AbilityCode, out abilityDefinitionIndex))
                        {
                            commands[entityIndex] = command;
                            continue;
                        }
                    }
                    else
                    {
                        ref readonly var cachedAbilityDefinition = ref catalog.Abilities[abilityDefinitionIndex];
                        if (cachedAbilityDefinition.AbilityCode != ability.AbilityCode &&
                            !GASGeneratedDefinitionLookup.TryGetAbilityIndex(ref catalog, ability.AbilityCode, out abilityDefinitionIndex))
                        {
                            commands[entityIndex] = command;
                            continue;
                        }
                    }

                    ref readonly var abilityDefinition = ref GASGeneratedDefinitionLookup.GetAbility(ref catalog, abilityDefinitionIndex);
                    if (!GASGeneratedDefinitionLookup.TryGetGameplayEffectIndex(
                            ref catalog,
                            abilityDefinition.PrimaryGameplayEffectCode,
                            out var primaryEffectIndex))
                    {
                        commands[entityIndex] = command;
                        continue;
                    }

                    var costEffectIndex = -1;
                    if (abilityDefinition.CostGameplayEffectCode != 0)
                    {
                        GASGeneratedDefinitionLookup.TryGetGameplayEffectIndex(
                            ref catalog,
                            abilityDefinition.CostGameplayEffectCode,
                            out costEffectIndex);
                    }

                    var cooldownEffectIndex = -1;
                    if (abilityDefinition.CooldownGameplayEffectCode != 0)
                    {
                        GASGeneratedDefinitionLookup.TryGetGameplayEffectIndex(
                            ref catalog,
                            abilityDefinition.CooldownGameplayEffectCode,
                            out cooldownEffectIndex);
                    }

                    command.AbilityDefinitionIndex = abilityDefinitionIndex;
                    command.PrimaryGameplayEffectCode = abilityDefinition.PrimaryGameplayEffectCode;
                    command.PrimaryGameplayEffectDefinitionIndex = primaryEffectIndex;
                    command.CostGameplayEffectCode = abilityDefinition.CostGameplayEffectCode;
                    command.CostGameplayEffectDefinitionIndex = costEffectIndex;
                    command.CooldownGameplayEffectCode = abilityDefinition.CooldownGameplayEffectCode;
                    command.CooldownGameplayEffectDefinitionIndex = cooldownEffectIndex;
                    command.Level = ability.Level;
                    command.Status = AbilityCommandStatus.Valid;
                    commands[entityIndex] = command;
                }
            }
        }
    }
}
```

合理性：

1. Boundary 创建 request archetype 时一次性带上 `AbilityActivationRequestComponent`、`AbilityCommandComponent`、`TargetDataBuffer`，Ingest 阶段只写同 chunk 的 command component，不在热路径中 AddComponent。
2. `ComponentLookup<AbilityStateComponent>` 只读访问 request 指向的 Ability Entity，符合官方 `systems-looking-up-data.md` 对 lookup 的约束：lookup 是随机访问，除非必须不要用；这里的随机读是边界 request → granted ability 的必要校验，写入仍保持 request-local。
3. Definition Catalog singleton 只读获取 BlobRef 后传入 job；singleton API 不完成依赖的风险由“bootstrap 后无 writer”这个不变量消除，不能在 Runtime tick 中写 `GASDefinitionCatalogComponent`。
4. Ingest 通过 `AbilityDefinitionIndex` 优先读取 `ref readonly AbilityDefinitionBlob`；index 无效才回退到 generated code -> index lookup。这样把 Luban 配置消费压缩到一次 Blob 读取，不反查 managed row / dictionary。
5. Ingest 只归一化命令和生成 GE seed，不把目标解析结果塞回 Ability Entity；Rejected / Consumed request 由 Structural Commit 统一销毁。

### Core Ability Producer：NativeStream command records

`AbilityActivationRequestComponent` 是 Boundary 入口，不是 Core 内部高频 command bus。AI autocast、passive、period、reaction 这类来源应直接在 Core lane 中并行写 record，避免每次触发都创建/销毁 request entity。

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    public partial struct GASAbilityCommandRecordProduceSystem : ISystem
    {
        private EntityQuery _autoCastAbilityQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _autoCastAbilityQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AbilityStateComponent>()
                }
            });

            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var definitionCatalog = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>().DefinitionCatalogBlob;
            if (!definitionCatalog.IsCreated)
                return;

            var chunkCount = _autoCastAbilityQuery.CalculateChunkCount();
            if (chunkCount == 0)
                return;

            var commandStream = new NativeStream(chunkCount, Allocator.TempJob);
            var commands = new NativeList<AbilityActivationCommandRecord>(Allocator.TempJob);

            var produceJob = new ProduceAutoCastAbilityCommandsJob
            {
                EntityType = state.GetEntityTypeHandle(),
                AbilityType = state.GetComponentTypeHandle<AbilityStateComponent>(true),
                CommandWriter = commandStream.AsWriter(),
                Frame = SystemAPI.GetSingleton<GlobalTimer>().Frame,
                DefinitionCatalogBlob = definitionCatalog
            }.ScheduleParallel(_autoCastAbilityQuery, state.Dependency);

            var mergeJob = new MergeAbilityCommandRecordsJob
            {
                CommandReader = commandStream.AsReader(),
                Commands = commands
            }.Schedule(produceJob);

            // 真实目标态中，同一个 owner pipeline 会继续把 commands 交给 Target Resolve。
            // 示例只展示 producer/merge 生命周期；下游 job 完成后再 dispose。
            var disposeStreamJob = commandStream.Dispose(mergeJob);
            state.Dependency = commands.Dispose(disposeStreamJob);
        }

        [BurstCompile]
        private struct ProduceAutoCastAbilityCommandsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<AbilityStateComponent> AbilityType;
            public NativeStream.Writer CommandWriter;
            public int Frame;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> DefinitionCatalogBlob;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var entities = chunk.GetNativeArray(EntityType);
                var abilities = chunk.GetNativeArray(ref AbilityType);
                ref var catalog = ref DefinitionCatalogBlob.Value;

                CommandWriter.BeginForEachIndex(unfilteredChunkIndex);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    var ability = abilities[entityIndex];
                    if (ability.State != AbilityRuntimeState.Ready ||
                        (ability.Flags & AbilityRuntimeFlags.Executable) == 0 ||
                        (ability.Flags & AbilityRuntimeFlags.Blocked) != 0)
                    {
                        continue;
                    }

                    var abilityDefinitionIndex = ability.AbilityDefinitionIndex;
                    if (abilityDefinitionIndex < 0 ||
                        abilityDefinitionIndex >= catalog.Abilities.Length)
                    {
                        if (!GASGeneratedDefinitionLookup.TryGetAbilityIndex(ref catalog, ability.AbilityCode, out abilityDefinitionIndex))
                            continue;
                    }
                    else
                    {
                        ref readonly var cachedAbilityDefinition = ref catalog.Abilities[abilityDefinitionIndex];
                        if (cachedAbilityDefinition.AbilityCode != ability.AbilityCode &&
                            !GASGeneratedDefinitionLookup.TryGetAbilityIndex(ref catalog, ability.AbilityCode, out abilityDefinitionIndex))
                        {
                            continue;
                        }
                    }

                    ref readonly var abilityDefinition = ref GASGeneratedDefinitionLookup.GetAbility(ref catalog, abilityDefinitionIndex);
                    if (!GASGeneratedDefinitionLookup.TryGetGameplayEffectIndex(
                            ref catalog,
                            abilityDefinition.PrimaryGameplayEffectCode,
                            out var primaryEffectIndex))
                    {
                        continue;
                    }

                    var costEffectIndex = -1;
                    if (abilityDefinition.CostGameplayEffectCode != 0)
                    {
                        GASGeneratedDefinitionLookup.TryGetGameplayEffectIndex(
                            ref catalog,
                            abilityDefinition.CostGameplayEffectCode,
                            out costEffectIndex);
                    }

                    var cooldownEffectIndex = -1;
                    if (abilityDefinition.CooldownGameplayEffectCode != 0)
                    {
                        GASGeneratedDefinitionLookup.TryGetGameplayEffectIndex(
                            ref catalog,
                            abilityDefinition.CooldownGameplayEffectCode,
                            out cooldownEffectIndex);
                    }

                    CommandWriter.Write(new AbilityActivationCommandRecord
                    {
                        Sequence = (unfilteredChunkIndex << 16) | entityIndex,
                        Frame = Frame,
                        SourceAsc = ability.OwnerAsc,
                        AbilityEntity = entities[entityIndex],
                        ExplicitTargetAsc = Entity.Null,
                        AbilityDefinitionIndex = abilityDefinitionIndex,
                        PrimaryGameplayEffectCode = abilityDefinition.PrimaryGameplayEffectCode,
                        PrimaryGameplayEffectDefinitionIndex = primaryEffectIndex,
                        CostGameplayEffectCode = abilityDefinition.CostGameplayEffectCode,
                        CostGameplayEffectDefinitionIndex = costEffectIndex,
                        CooldownGameplayEffectCode = abilityDefinition.CooldownGameplayEffectCode,
                        CooldownGameplayEffectDefinitionIndex = cooldownEffectIndex,
                        Level = ability.Level,
                        TargetGroupSortKey = 0,
                        TargetMode = 1
                    });
                }

                CommandWriter.EndForEachIndex();
            }
        }

        [BurstCompile]
        private struct MergeAbilityCommandRecordsJob : IJob
        {
            [ReadOnly] public NativeStream.Reader CommandReader;
            public NativeList<AbilityActivationCommandRecord> Commands;

            public void Execute()
            {
                for (var forEachIndex = 0; forEachIndex < CommandReader.ForEachCount; forEachIndex++)
                {
                    CommandReader.BeginForEachIndex(forEachIndex);
                    while (CommandReader.RemainingItemCount > 0)
                    {
                        Commands.Add(CommandReader.Read<AbilityActivationCommandRecord>());
                    }
                    CommandReader.EndForEachIndex();
                }

                Commands.Sort(new AbilityCommandRecordComparer());
            }
        }

        private struct AbilityCommandRecordComparer : System.Collections.Generic.IComparer<AbilityActivationCommandRecord>
        {
            public int Compare(AbilityActivationCommandRecord x, AbilityActivationCommandRecord y)
            {
                return x.Sequence.CompareTo(y.Sequence);
            }
        }
    }
}
```

合理性：

1. 这条路径没有 request entity 创建/销毁，没有 ECB playback 压力，也不需要给每个内部触发挂 `TargetDataBuffer`。
2. `AbilityStateComponent` 在目标态保持为跨帧 Ability Entity 状态；单次激活上下文是 frame-local record，生命周期由 owner system 的 `Allocator.TempJob` 控制。
3. 多个 producer 可以使用不重叠的 `NativeStream` for-each index range，或各自独立 stream 后统一 merge；无论哪种方式，都必须在 merge 后生成 deterministic sequence。

### Target Resolve：NativeStream target records

高目标数 AoE、链式技能、period/passive 批量触发默认输出 `AbilityTargetRecord`，而不是把所有 target 塞进 request entity buffer。

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    [BurstCompile]
    public struct ResolveAbilityCommandTargetsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AbilityActivationCommandRecord> Commands;
        public NativeStream.Writer TargetWriter;

        public void Execute(int index)
        {
            var command = Commands[index];
            TargetWriter.BeginForEachIndex(index);

            if (command.ExplicitTargetAsc != Entity.Null)
            {
                TargetWriter.Write(new AbilityTargetRecord
                {
                    Sequence = command.Sequence,
                    TargetIndex = 0,
                    TargetSortKey = command.TargetGroupSortKey,
                    SourceAsc = command.SourceAsc,
                    SourceAbility = command.AbilityEntity,
                    TargetAsc = command.ExplicitTargetAsc,
                    GameplayEffectCode = command.PrimaryGameplayEffectCode,
                    GameplayEffectDefinitionIndex = command.PrimaryGameplayEffectDefinitionIndex,
                    Level = command.Level
                });
            }

            // AoE / physics query path writes N AbilityTargetRecord values here,
            // each with an explicit TargetIndex and deterministic TargetSortKey.

            TargetWriter.EndForEachIndex();
        }
    }
}
```

合理性：

1. DynamicBuffer 的 internal capacity 适合稳定小数组；官方文档明确超过 capacity 后会外移并产生长期间接访问。高目标数目标解析属于 frame-local fan-out，不应默认用 entity buffer 承载。
2. `AbilityTargetRecord` 把 `Sequence / TargetIndex / TargetSortKey` 显式化，Fan-In merge 不依赖 chunk 顺序、worker 调度顺序或 ECB append playback 顺序。
3. request-owned `TargetDataBuffer` 保留为 Boundary 物化路径和低/中量目标解析路径；Profiler 发现 spill 或 request structural pressure 后，应切到本 record path。

### Target Resolve：request-owned TargetDataBuffer

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    public partial struct AbilityTargetResolveSystem : ISystem
    {
        private EntityQuery _commandQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _commandQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AbilityCommandComponent>(),
                    ComponentType.ReadWrite<TargetDataBuffer>()
                }
            });

            state.RequireForUpdate(_commandQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ResolveExplicitTargetsJob
            {
                CommandType = state.GetComponentTypeHandle<AbilityCommandComponent>(true),
                TargetBufferType = state.GetBufferTypeHandle<TargetDataBuffer>(false)
            }.ScheduleParallel(_commandQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ResolveExplicitTargetsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<AbilityCommandComponent> CommandType;
            public BufferTypeHandle<TargetDataBuffer> TargetBufferType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var commands = chunk.GetNativeArray(ref CommandType);
                var targetsByRequest = chunk.GetBufferAccessor(ref TargetBufferType);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    var command = commands[entityIndex];
                    var targets = targetsByRequest[entityIndex];

                    targets.Clear();

                    if (command.Status != AbilityCommandStatus.Valid ||
                        command.ExplicitTargetAsc == Entity.Null)
                    {
                        continue;
                    }

                    targets.Add(new TargetDataBuffer
                    {
                        TargetAsc = command.ExplicitTargetAsc,
                        TargetSortKey = command.TargetGroupSortKey
                    });
                }
            }
        }
    }
}
```

合理性：

1. 目标解析结果写在 request/command entity 的 `TargetDataBuffer`，Effect Fan-In 顺序消费；不需要每个 target 创建 request entity，但也不把单次调用上下文写回跨帧 Ability Entity（`PRF-01`）。
2. Physics / Area query 可替换 `ResolveExplicitTargetsJob` 的输入，但输出仍是 deterministic `TargetDataBuffer`，符合 `PHY-02` / `MAT-05`。
3. Target sort key 在 Target Resolve 阶段产生，后续 fan-in merge 不依赖 entity index 或 chunk 顺序。
