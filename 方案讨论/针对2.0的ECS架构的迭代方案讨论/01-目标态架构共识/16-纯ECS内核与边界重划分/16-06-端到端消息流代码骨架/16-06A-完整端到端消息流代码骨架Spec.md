# 16-06A：完整端到端消息流代码骨架 Spec

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分/16-06` | 状态：目标态 Spec 子页正文 | 写入时间：2026-06-08

本文件只描述理想目标态的端到端消息流、完整代码骨架、代码级解读和验收门，是 `16-06` 下完整代码骨架的唯一正文 owner。这里不记录当前实现状态、当前文件行号、验证数字、执行流水或下一步任务。

## 目的

定义 EX-GAS 2.0 目标态的一条最小但完整的端到端链路：

```text
Application Shell intent
  -> Runtime Boundary capability
  -> owner-local command record
  -> Pure ECS Core lane / IJobChunk
  -> NativeStream deterministic fan-in
  -> target owner-local command dispatch
  -> spec / delta / owner-local fact lane
  -> ActiveEffect / Attribute / GameplayFact owner
  -> Boundary observation fact projection
  -> Boundary snapshot / Diagnostics evidence
  -> Derived export
```

这条链路用于审查新架构是否真正把 OOP Shell、Runtime Boundary、Pure ECS Runtime Core、Debugger evidence 和 Definition & Generation 分开。它不是实现完成证明；它是可作为框架设计 Spec 的目标代码形态。

## 局部 Owner Map

`16-06A` 是完整端到端代码骨架 owner，只负责把 Shell、Boundary、Core、Definition、Diagnostics 和 Derived export 串成一条可审查消息流。局部规则不得在本文件和相邻子页维护两份正文。

| 局部主题 | 唯一正文 owner | `16-06` 的职责 |
|---|---|---|
| Boundary command envelope / Core command consume | [16-02](../16-02-BoundaryCommand与CoreCommandResolveSpec.md) | 串起 Shell intent 进入 Core 的最小骨架 |
| Fan-in、Debugger evidence、SourceGenerator pure glue、magnitude snapshot lane | [16-03](../16-03-FanInDebuggerSourceGeneratorSpec.md) | 展示端到端数据流如何消费这些规则 |
| Shell / Adapter capability、public seam、opaque handle、internal resolver | [16-04](../16-04-ShellCapabilityContractSpec.md) | 保证完整代码骨架不泄露 ECS handle |
| Snapshot、identity、report projection、API health、验收门 | [16-05](../16-05-SnapshotIdentityApiHealthSpec.md) | 在完整链路中保留对应 evidence / gate |

如果 `16-06A` 的代码骨架和上述局部 owner 出现冲突，优先修正 `16-06A` 的骨架表达；不要在 `16-06A` 复制或改写第二份局部规则。

## 官方依据

| 规则 | 本 Spec 采用结论 |
|---|---|
| `SYS-01` / `SYS-02` / `SYS-05` | Gameplay 权威计算只在 ECS System / Job 数据流中发生；Shell / Debugger / Presentation 只能通过 Boundary 读写 |
| `SEL-01` / `SEL-02` | 先按数据性质选 API；proof-only carrier 不得固化为目标承载 |
| `QRY-01` / `JOB-01` | Core hot path 默认 job 化；query owner 必须归具体 lane system |
| `SC-01` / `ECB-03` | 结构变化只能通过明确 StructuralCommit owner 发生 |
| `BUF-02` / `NAT-03` / `MAT-05` | 全局 singleton buffer 只能 proof；fan-in 必须有 deterministic merge、allocator owner 和预算 |
| `DBG-01..05` | Debugger 输出机器可读 evidence；日志、图表、战报都是 derived export |
| `BLOB-01` / `BLOB-02` / `BUR-01` | Definition 进入 immutable Blob / lookup / pure glue；SourceGenerator 不生成 runtime lifecycle owner |

## 代码骨架纯度护栏

本文件的代码是目标态框架设计骨架，不是当前实现镜像。维护时必须保持以下纯度：

1. 只出现理想 owner、interface、record、system/job、evidence 和禁止方向。
2. 不写当前文件名、当前行号、当前命中数、当前日志、当前任务进度或迁移完成度。
3. 不用当前类名的存在证明目标态成立；目标态只看 owner 是否闭合。
4. 不把 `NativeStream`、`IJobChunk`、Blob 或 SourceGenerator 名称本身当完成标准；必须同时给出 owner、allocator、dependency、merge、capacity、evidence 和退出门。
5. 不把 Debugger / derived export / AutoChess report 当 Core evidence source；它们只能消费机器 evidence。

目标态代码骨架的完整性来自 owner 链闭合：

```text
Shell intent
  -> Boundary command record
  -> Core lane record
  -> fan-in merge / owner-local dispatch
  -> spec / delta / Core fact
  -> Boundary observation fact
  -> snapshot / diagnostics evidence
  -> derived export
```

其中 SourceGenerator 只能在 Core lane 需要不可变定义时提供 `catalog lookup / pure evaluator / validation metadata`，不能插入 runtime lifecycle 或调度 owner。

## 重划分判定准则

本代码骨架的目标不是多写一层 facade，而是把调用方必须知道的细节压缩到正确 owner 内。判断一段实现是否符合目标态时，必须先看 interface 是否变窄，再看 implementation 是否承担了足够深的 DOTS 责任。

| 目标 owner | 外部可见 interface | 内部必须拥有的 implementation | 判定为合理的原因 | 失败信号 |
|---|---|---|---|---|
| `IGASRuntimeSession` | install / fixed tick / dispose / tick timing result | World lifetime、SystemGroup install、bootstrap singleton、catalog install、shutdown cleanup | Shell 只管理运行会话，不知道 `World`、`EntityManager` 或 singleton entity | Shell 取得 `EntityManager` 后自行调 system / singleton |
| `IGASCommandPort` | `GASIntent`、`GASRequestId`、reject reason | intent validation、opaque target resolve、request entity / owner-local command append、sequence、write pressure evidence | OOP Shell 只表达业务意图，gameplay 计算留在 Core | port 同步计算 damage、cooldown、tag requirement 或返回 writable buffer |
| `IGASSnapshotReadModel` | immutable snapshot、version、minimum version | BoundaryProjection、snapshot ring、copy arena、staleness、drop counter | UI / AI / Demo 读取的是投影结果，不读取 live Core buffer | read model 暴露 `DynamicBuffer`、raw `Entity` 或 query scan |
| `GASBoundaryCommandResolveSystem` | Core command record | `EntityQuery`、type handle、enableable mask、`NativeStream` producer、dependency、evidence counter | Boundary command 进入 ECS 数据流；query owner 在手写 Runtime System | command resolve 藏在 OOP adapter 或 generated `OnUpdate` 中 |
| `MergeEffectCommandFanInJob` | sorted owner-local command range | `NativeStream.Reader`、sort key、deterministic merge、target owner dispatch、capacity / segment counters | fan-in 的并行写和确定性合并归同一 owner，可被 Debugger 归因 | 依赖全局 singleton buffer 顺序、无 sort key、无 allocator / spill 证据 |
| `GASEffectSpecBuildSystem` | owner-local command -> delta / fact record | immutable catalog read、generated pure glue call、owner-local spec / delta / fact writer、failure fact | SourceGenerator 只解释 definition，System 拥有 query / writer / dependency | generated code 拥有 query、lookup refresh、ECB 或 lifecycle |
| `GASAttributeDeltaApplySystem` | owner-local delta apply | owner chunk iteration、attribute write、fact append、enableable state | attribute 写入集中在 ASC owner chunk，减少 random lookup 和竞态 | 每条 spec 通过跨 entity `ComponentLookup` 随机写目标 |
| `GASBoundaryObservationProjectionSystem` | Core fact -> Boundary fact | cursor、projection filter、outbox / replay / diagnostics split | Observation 是只读派生链，不能反向驱动 Core | Presentation / Replay 写回 gameplay component |
| `IGASDiagnosticsSink` | evidence snapshot | counters、timing、buffer pressure、official capture state、derived export source | Debugger 输出性能证据和 API 健康度，不作为 runtime route | hot loop 拼字符串、日志总线驱动 gameplay、用文本日志替代 counters |
| `IGASDefinitionCatalogLifetime` | catalog handle、schema / version、install / release result | Blob lifetime、Baking / Bootstrap materialization、dispose owner、hot path 禁用证据 | Definition 构建期与 Core tick 分离，Runtime 只读 immutable catalog | hot path 调 `BlobBuilder`、读取 row / JSON / managed registry |

### 代码判定规则

1. Shell / Boundary interface 中出现 `World`、`EntityManager`、raw `Entity`、`EntityQuery`、writable `DynamicBuffer` 或 NativeContainer owner，目标态失败。
2. Core System / Job 不显式拥有 query、type handle refresh、dependency、allocator、writer、clear / dispose phase 和 evidence counter，目标态失败。
3. `NativeStream` 或 owner-local buffer 没有 deterministic sort key、capacity / spill counter、merge order 和 battle hash 关系，不能称为 scale-ready。
4. generated code 只允许是 catalog / lookup / pure evaluator / validation metadata；一旦拥有 `ISystem`、`OnUpdate`、query、ECB、NativeContainer 或 lifecycle registration，必须判为 generated lifecycle 回流。
5. Debugger / Replay / Presentation / structured log 只消费 Boundary fact 和 evidence；任何反向写 Core 的链路都必须被拒绝。

## 目标代码组织

目标态代码可以拆成多个 assembly 或 namespace，但每个包的 interface 必须比 implementation 窄。拆分的目的不是让文件变多，而是让调用方不再知道 ECS handle、carrier、allocator、dependency 和 generated artifact 细节。

| 目标代码区 | public interface | internal implementation | 允许依赖 | 禁止依赖 |
|---|---|---|---|---|
| `GAS.Runtime.Shell` | `IGASRuntimeSession`、`IGASCommandPort`、`IGASSnapshotReadModel`、`IGASDiagnosticsSink` | Unity / GameObject / UI / AI / Network adapter，opaque handle registry | Boundary capability、derived snapshot、evidence snapshot | `World`、`EntityManager`、raw `Entity`、`DynamicBuffer`、Core store |
| `GAS.Runtime.Boundary` | command envelope、snapshot DTO、presentation outbox DTO、diagnostics DTO | identity resolver、request append、snapshot ring、projection cursor、drop / stale counter | Core component type definitions、opaque id map、BoundaryProjection facts | gameplay formula、cooldown / tag requirement calculation、managed config row lookup |
| `GAS.Runtime.Core` | frame-local record、owner-local buffer、structural intent、Core reaction fact | `ISystem`、`IJobChunk`、query / type handle owner、dependency chain、fan-in merge、owner-local store、ECB intent | Definition catalog handle、generated pure glue、Boundary command records | OOP adapter、Debugger controller、Presentation / Replay writer、generated lifecycle |
| `GAS.Runtime.Definition` | catalog handle、schema / version、pure lookup / evaluator | Blob lifetime、static lookup、pure generated glue、validation metadata、Baker / Bootstrap install | Luban-generated schema、SourceGenerator pure artifact、Baking / Bootstrap | runtime lifecycle、query owner、ECB owner、NativeContainer lifecycle owner |
| `GAS.Runtime.Diagnostics` | structured evidence snapshot、official capture state、derived export source | counters、timing split、TopN attribution、buffer pressure、allocator stats、observation materialization | Boundary facts、Core evidence buffers、Profiler / Journaling state | command writer、Core gameplay write、text log as validation source |

目标态最小代码包必须满足两个删除测试：

1. 删除 Shell adapter 后，业务集成复杂度应回到 Boundary capability implementation，而不是扩散到 Core System 或 generated glue。
2. 删除 generated pure glue 后，复杂度应回到 Definition codegen template 和 hand-written Core caller 的窄消费点，而不是要求 Shell、Debugger 或 Presentation 了解 definition row / JSON / catalog layout。

## 完整代码骨架

以下代码是目标态端到端切片骨架，重点是职责和数据流方向。真实实现可以拆成多个文件，但不得改变这些 owner 和禁止泄露规则。

```csharp
using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.TargetSpec
{
    public readonly struct GASRuntimeSessionId
    {
        public readonly int Value;
        public readonly int Version;
    }

    public readonly struct ASCTargetRef
    {
        public readonly int Value;
        public readonly int Version;
    }

    public readonly struct GASRequestId
    {
        public readonly int Frame;
        public readonly int Sequence;
    }

    public readonly struct GASSnapshotVersion
    {
        public readonly int Frame;
        public readonly int Sequence;
    }

    public readonly struct GASIntent
    {
        public readonly GASRuntimeSessionId Session;
        public readonly ASCTargetRef Source;
        public readonly ASCTargetRef Target;
        public readonly int AbilityCode;
        public readonly int GameplayEffectCode;
        public readonly float SetByCallerMagnitude;
    }

    public readonly struct GASIntentReject
    {
        public readonly GASRequestId RequestId;
        public readonly int ReasonCode;
    }

    public readonly struct GASAscSnapshot
    {
        public readonly ASCTargetRef Target;
        public readonly GASSnapshotVersion Version;
        public readonly float Health;
        public readonly float Energy;
        public readonly int TagMaskLow;
    }

    public readonly struct GASRuntimeEvidenceSnapshot
    {
        public readonly GASSnapshotVersion Version;
        public readonly long CoreTicks;
        public readonly long BoundaryTicks;
        public readonly long DiagnosticsTicks;
        public readonly int CommandCount;
        public readonly int SpecCount;
        public readonly int DeltaCount;
        public readonly int FactCount;
        public readonly int OwnerLocalFactOwnerGroupCount;
        public readonly int OwnerLocalFactMaxOwnerRange;
        public readonly int BoundaryObservationFactCount;
        public readonly int StructuralPlaybackCount;
        public readonly int ProofOnlyCarrierCount;
        public readonly int RandomLookupCount;
        public readonly int NativeStreamSegmentCount;
        public readonly int MergeCostMicroseconds;
        public readonly int ObservationMaterializationCount;
        public readonly bool OfficialCaptureAvailable;
    }

    public interface IGASRuntimeSession
    {
        GASRuntimeSessionId Install();
        GASFixedTickResult RunFixedTick(GASRuntimeSessionId session, int tickCount);
        void Dispose(GASRuntimeSessionId session);
    }

    public readonly struct GASFixedTickResult
    {
        public readonly GASSnapshotVersion Version;
        public readonly long CommandTicks;
        public readonly long CoreTicks;
        public readonly long StructuralTicks;
        public readonly long BoundaryTicks;
        public readonly long DiagnosticsTicks;
        public readonly long RunnerTicks;
    }

    public interface IGASCommandPort
    {
        GASRequestId Send(in GASIntent intent, out GASIntentReject reject);
    }

    public interface IGASSnapshotReadModel
    {
        bool TryReadAsc(
            GASRuntimeSessionId session,
            ASCTargetRef target,
            GASSnapshotVersion minimumVersion,
            out GASAscSnapshot snapshot);
    }

    public interface IGASDiagnosticsSink
    {
        GASRuntimeEvidenceSnapshot Capture(GASRuntimeSessionId session);
    }

    public interface IGASDefinitionCatalogLifetime
    {
        GASDefinitionCatalogHandle Install(
            GASRuntimeSessionId session,
            BlobAssetReference<GASDefinitionCatalogBlob> catalog);

        void Release(GASRuntimeSessionId session, GASDefinitionCatalogHandle handle);
    }

    public readonly struct GASDefinitionCatalogHandle
    {
        public readonly int Value;
        public readonly int Version;
        public readonly int SchemaVersion;
    }

    internal readonly struct ResolvedRuntimeAsc
    {
        internal readonly Entity Entity;

        internal ResolvedRuntimeAsc(Entity entity)
        {
            Entity = entity;
        }
    }

    internal interface IGASRuntimeAscResolver
    {
        bool TryResolve(
            GASRuntimeSessionId session,
            ASCTargetRef target,
            out ResolvedRuntimeAsc asc);
    }

    internal struct GASBoundaryCommandPending : IComponentData, IEnableableComponent
    {
    }

    internal struct GASBoundaryCommandBuffer : IBufferElementData
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int AbilityCode;
        public int GameplayEffectCode;
        public float SetByCallerMagnitude;
        public int Frame;
        public int Sequence;
        public GASBoundaryCommandKind Kind;
    }

    internal enum GASBoundaryCommandKind : byte
    {
        ActivateAbility = 1,
        ApplyGameplayEffect = 2,
        RemoveGameplayEffect = 3,
        DestroyAsc = 4,
    }

    internal struct GASEffectCommandRecord
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int AbilityCode;
        public int GameplayEffectCode;
        public float SetByCallerMagnitude;
        public int Frame;
        public int Sequence;
        public int ProducerChunkIndex;
        public int ProducerLocalIndex;
    }

    internal struct GASEffectCommandSortKey : IComparable<GASEffectCommandSortKey>
    {
        public int TargetIndex;
        public int TargetVersion;
        public int Frame;
        public int Sequence;
        public int ProducerChunkIndex;
        public int ProducerLocalIndex;

        public int CompareTo(GASEffectCommandSortKey other)
        {
            var result = TargetIndex.CompareTo(other.TargetIndex);
            if (result != 0) return result;
            result = TargetVersion.CompareTo(other.TargetVersion);
            if (result != 0) return result;
            result = Frame.CompareTo(other.Frame);
            if (result != 0) return result;
            result = Sequence.CompareTo(other.Sequence);
            if (result != 0) return result;
            result = ProducerChunkIndex.CompareTo(other.ProducerChunkIndex);
            if (result != 0) return result;
            return ProducerLocalIndex.CompareTo(other.ProducerLocalIndex);
        }
    }

    internal struct GASSortedEffectCommand
    {
        public GASEffectCommandSortKey SortKey;
        public GASEffectCommandRecord Command;
    }

    internal struct GASOwnerEffectCommandRange : IBufferElementData
    {
        public int Start;
        public int Count;
    }

    internal struct GASOwnerEffectCommandPending : IComponentData, IEnableableComponent
    {
    }

    internal struct GASOwnerEffectCommandBuffer : IBufferElementData
    {
        public Entity SourceAsc;
        public int AbilityCode;
        public int GameplayEffectCode;
        public float SetByCallerMagnitude;
        public int Frame;
        public int Sequence;
    }

    internal struct GASAttributeDeltaPending : IComponentData, IEnableableComponent
    {
    }

    internal struct GASAttributeDeltaBuffer : IBufferElementData
    {
        public int AttributeCode;
        public float AdditiveValue;
        public int Frame;
        public int Sequence;
        public int ReasonCode;
    }

    internal struct GASAttributeValueBuffer : IBufferElementData
    {
        public int AttributeCode;
        public float CurrentValue;
    }

    internal struct GASDefinitionCatalogComponent : IComponentData
    {
        public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
        public int SchemaVersion;
    }

    internal struct GASOwnerLocalFactPending : IComponentData, IEnableableComponent
    {
    }

    internal struct GASOwnerLocalFactBuffer : IBufferElementData
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int Domain;
        public int Category;
        public int Code;
        public float OldValue;
        public float NewValue;
        public int Frame;
        public int Sequence;
    }

    internal struct GASBoundaryObservationFactBuffer : IBufferElementData
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int Domain;
        public int Category;
        public int Code;
        public float OldValue;
        public float NewValue;
        public int Frame;
        public int Sequence;
    }

    internal struct GASStructuralIntentBuffer : IBufferElementData
    {
        public Entity Owner;
        public Entity Target;
        public int Kind;
        public int Sequence;
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    public partial struct GASBoundaryCommandResolveSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GASBoundaryCommandPending>(),
                    ComponentType.ReadWrite<GASBoundaryCommandBuffer>(),
                },
            });
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var chunkCount = _query.CalculateChunkCountWithoutFiltering();
            var commandStream = new NativeStream(chunkCount, state.WorldUpdateAllocator);
            var segmentCounts = CollectionHelper.CreateNativeArray<int>(
                chunkCount,
                state.WorldUpdateAllocator,
                NativeArrayOptions.ClearMemory);

            var produceDependency = new ResolveBoundaryCommandJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                PendingType = SystemAPI.GetComponentTypeHandle<GASBoundaryCommandPending>(),
                CommandType = SystemAPI.GetBufferTypeHandle<GASBoundaryCommandBuffer>(),
                Writer = commandStream.AsWriter(),
                SegmentCounts = segmentCounts,
            }.ScheduleParallel(_query, state.Dependency);

            var sortedCommands = new NativeList<GASSortedEffectCommand>(
                AllocatorManager.ToAllocator(state.WorldUpdateAllocator));

            state.Dependency = new MergeEffectCommandFanInJob
            {
                Reader = commandStream.AsReader(),
                SortedCommands = sortedCommands,
                SegmentCounts = segmentCounts,
                OwnerCommands = SystemAPI.GetBufferLookup<GASOwnerEffectCommandBuffer>(),
                OwnerCommandPending = SystemAPI.GetComponentLookup<GASOwnerEffectCommandPending>(),
            }.Schedule(produceDependency);
        }

        [BurstCompile]
        private struct ResolveBoundaryCommandJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public ComponentTypeHandle<GASBoundaryCommandPending> PendingType;
            public BufferTypeHandle<GASBoundaryCommandBuffer> CommandType;
            public NativeStream.Writer Writer;
            [NativeDisableParallelForRestriction] public NativeArray<int> SegmentCounts;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var owners = chunk.GetNativeArray(EntityType);
                var commands = chunk.GetBufferAccessor(ref CommandType);
                var pending = chunk.GetEnabledMask(ref PendingType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                var produced = 0;

                Writer.BeginForEachIndex(unfilteredChunkIndex);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var owner = owners[i];
                    var ownerCommands = commands[i];
                    for (var commandIndex = 0; commandIndex < ownerCommands.Length; commandIndex++)
                    {
                        var command = ownerCommands[commandIndex];
                        Writer.Write(new GASEffectCommandRecord
                        {
                            SourceAsc = owner,
                            TargetAsc = command.TargetAsc,
                            AbilityCode = command.AbilityCode,
                            GameplayEffectCode = command.GameplayEffectCode,
                            SetByCallerMagnitude = command.SetByCallerMagnitude,
                            Frame = command.Frame,
                            Sequence = command.Sequence,
                            ProducerChunkIndex = unfilteredChunkIndex,
                            ProducerLocalIndex = commandIndex,
                        });
                        produced++;
                    }

                    ownerCommands.Clear();
                    pending[i] = false;
                }
                Writer.EndForEachIndex();

                if ((uint)unfilteredChunkIndex < (uint)SegmentCounts.Length)
                    SegmentCounts[unfilteredChunkIndex] = produced;
            }
        }
    }

    [BurstCompile]
    internal struct MergeEffectCommandFanInJob : IJob
    {
        public NativeStream.Reader Reader;
        public NativeList<GASSortedEffectCommand> SortedCommands;
        [ReadOnly] public NativeArray<int> SegmentCounts;
        public BufferLookup<GASOwnerEffectCommandBuffer> OwnerCommands;
        public ComponentLookup<GASOwnerEffectCommandPending> OwnerCommandPending;

        public void Execute()
        {
            SortedCommands.Clear();
            var commandCount = 0;
            for (var i = 0; i < SegmentCounts.Length; i++)
                commandCount += SegmentCounts[i];

            if (commandCount > SortedCommands.Capacity)
                SortedCommands.SetCapacity(commandCount);

            for (var streamIndex = 0; streamIndex < Reader.ForEachCount; streamIndex++)
            {
                Reader.BeginForEachIndex(streamIndex);
                while (Reader.RemainingItemCount > 0)
                {
                    var command = Reader.Read<GASEffectCommandRecord>();
                    SortedCommands.Add(new GASSortedEffectCommand
                    {
                        SortKey = new GASEffectCommandSortKey
                        {
                            TargetIndex = command.TargetAsc.Index,
                            TargetVersion = command.TargetAsc.Version,
                            Frame = command.Frame,
                            Sequence = command.Sequence,
                            ProducerChunkIndex = command.ProducerChunkIndex,
                            ProducerLocalIndex = command.ProducerLocalIndex,
                        },
                        Command = command,
                    });
                }
                Reader.EndForEachIndex();
            }

            SortedCommands.Sort(new Comparer());

            var groupStart = 0;
            while (groupStart < SortedCommands.Length)
            {
                var target = SortedCommands[groupStart].Command.TargetAsc;
                var groupEnd = groupStart + 1;
                while (groupEnd < SortedCommands.Length
                       && SortedCommands[groupEnd].Command.TargetAsc == target)
                {
                    groupEnd++;
                }

                if (OwnerCommands.HasBuffer(target) && OwnerCommandPending.HasComponent(target))
                {
                    var ownerCommands = OwnerCommands[target];
                    ownerCommands.Clear();
                    for (var i = groupStart; i < groupEnd; i++)
                    {
                        var command = SortedCommands[i].Command;
                        ownerCommands.Add(new GASOwnerEffectCommandBuffer
                        {
                            SourceAsc = command.SourceAsc,
                            AbilityCode = command.AbilityCode,
                            GameplayEffectCode = command.GameplayEffectCode,
                            SetByCallerMagnitude = command.SetByCallerMagnitude,
                            Frame = command.Frame,
                            Sequence = command.Sequence,
                        });
                    }

                    OwnerCommandPending.SetComponentEnabled(target, ownerCommands.Length > 0);
                }

                groupStart = groupEnd;
            }
        }

        private struct Comparer : IComparer<GASSortedEffectCommand>
        {
            public int Compare(GASSortedEffectCommand x, GASSortedEffectCommand y)
            {
                return x.SortKey.CompareTo(y.SortKey);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    public partial struct GASEffectSpecBuildSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GASOwnerEffectCommandPending>(),
                    ComponentType.ReadWrite<GASOwnerEffectCommandBuffer>(),
                    ComponentType.ReadWrite<GASAttributeDeltaPending>(),
                    ComponentType.ReadWrite<GASAttributeDeltaBuffer>(),
                    ComponentType.ReadWrite<GASOwnerLocalFactPending>(),
                    ComponentType.ReadWrite<GASOwnerLocalFactBuffer>(),
                    ComponentType.ReadOnly<GASDefinitionCatalogComponent>(),
                },
            });
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var catalog = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>().Catalog;
            state.Dependency = new BuildEffectSpecAndDeltaJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                CommandPendingType = SystemAPI.GetComponentTypeHandle<GASOwnerEffectCommandPending>(),
                DeltaPendingType = SystemAPI.GetComponentTypeHandle<GASAttributeDeltaPending>(),
                CommandType = SystemAPI.GetBufferTypeHandle<GASOwnerEffectCommandBuffer>(),
                DeltaType = SystemAPI.GetBufferTypeHandle<GASAttributeDeltaBuffer>(),
                FactPendingType = SystemAPI.GetComponentTypeHandle<GASOwnerLocalFactPending>(),
                FactType = SystemAPI.GetBufferTypeHandle<GASOwnerLocalFactBuffer>(),
                Catalog = catalog,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct BuildEffectSpecAndDeltaJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public ComponentTypeHandle<GASOwnerEffectCommandPending> CommandPendingType;
            public ComponentTypeHandle<GASAttributeDeltaPending> DeltaPendingType;
            public BufferTypeHandle<GASOwnerEffectCommandBuffer> CommandType;
            public BufferTypeHandle<GASAttributeDeltaBuffer> DeltaType;
            public ComponentTypeHandle<GASOwnerLocalFactPending> FactPendingType;
            public BufferTypeHandle<GASOwnerLocalFactBuffer> FactType;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var owners = chunk.GetNativeArray(EntityType);
                var commandPending = chunk.GetEnabledMask(ref CommandPendingType);
                var deltaPending = chunk.GetEnabledMask(ref DeltaPendingType);
                var commands = chunk.GetBufferAccessor(ref CommandType);
                var deltas = chunk.GetBufferAccessor(ref DeltaType);
                var factPending = chunk.GetEnabledMask(ref FactPendingType);
                var facts = chunk.GetBufferAccessor(ref FactType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    var ownerCommands = commands[i];
                    var ownerDeltas = deltas[i];
                    var ownerFacts = facts[i];
                    ownerDeltas.Clear();

                    for (var commandIndex = 0; commandIndex < ownerCommands.Length; commandIndex++)
                    {
                        var command = ownerCommands[commandIndex];
                        if (!GASGeneratedDefinitionGlue.TryBuildAttributeDelta(
                                ref Catalog.Value,
                                in command,
                                out var delta))
                        {
                            ownerFacts.Add(new GASOwnerLocalFactBuffer
                            {
                                SourceAsc = command.SourceAsc,
                                TargetAsc = owners[i],
                                Domain = 1,
                                Category = 400,
                                Code = command.GameplayEffectCode,
                                Frame = command.Frame,
                                Sequence = command.Sequence,
                            });
                            continue;
                        }

                        ownerDeltas.Add(delta);
                        ownerFacts.Add(new GASOwnerLocalFactBuffer
                        {
                            SourceAsc = command.SourceAsc,
                            TargetAsc = owners[i],
                            Domain = 1,
                            Category = 100,
                            Code = command.GameplayEffectCode,
                            NewValue = delta.AdditiveValue,
                            Frame = delta.Frame,
                            Sequence = delta.Sequence,
                        });
                    }

                    ownerCommands.Clear();
                    commandPending[i] = false;
                    deltaPending[i] = ownerDeltas.Length > 0;
                    factPending[i] = ownerFacts.Length > 0;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    public partial struct GASAttributeDeltaApplySystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GASAttributeDeltaPending>(),
                    ComponentType.ReadWrite<GASAttributeDeltaBuffer>(),
                    ComponentType.ReadWrite<GASAttributeValueBuffer>(),
                    ComponentType.ReadWrite<GASOwnerLocalFactPending>(),
                    ComponentType.ReadWrite<GASOwnerLocalFactBuffer>(),
                },
            });
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ApplyAttributeDeltaJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                DeltaPendingType = SystemAPI.GetComponentTypeHandle<GASAttributeDeltaPending>(),
                DeltaType = SystemAPI.GetBufferTypeHandle<GASAttributeDeltaBuffer>(),
                AttributeType = SystemAPI.GetBufferTypeHandle<GASAttributeValueBuffer>(),
                FactPendingType = SystemAPI.GetComponentTypeHandle<GASOwnerLocalFactPending>(),
                FactType = SystemAPI.GetBufferTypeHandle<GASOwnerLocalFactBuffer>(),
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyAttributeDeltaJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            public ComponentTypeHandle<GASAttributeDeltaPending> DeltaPendingType;
            public BufferTypeHandle<GASAttributeDeltaBuffer> DeltaType;
            public BufferTypeHandle<GASAttributeValueBuffer> AttributeType;
            public ComponentTypeHandle<GASOwnerLocalFactPending> FactPendingType;
            public BufferTypeHandle<GASOwnerLocalFactBuffer> FactType;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var owners = chunk.GetNativeArray(EntityType);
                var deltaPending = chunk.GetEnabledMask(ref DeltaPendingType);
                var deltas = chunk.GetBufferAccessor(ref DeltaType);
                var attributes = chunk.GetBufferAccessor(ref AttributeType);
                var factPending = chunk.GetEnabledMask(ref FactPendingType);
                var facts = chunk.GetBufferAccessor(ref FactType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    var ownerDeltas = deltas[i];
                    var ownerAttributes = attributes[i];
                    var ownerFacts = facts[i];

                    for (var deltaIndex = 0; deltaIndex < ownerDeltas.Length; deltaIndex++)
                    {
                        var delta = ownerDeltas[deltaIndex];
                        for (var attributeIndex = 0; attributeIndex < ownerAttributes.Length; attributeIndex++)
                        {
                            var attribute = ownerAttributes[attributeIndex];
                            if (attribute.AttributeCode != delta.AttributeCode)
                                continue;

                            var oldValue = attribute.CurrentValue;
                            attribute.CurrentValue += delta.AdditiveValue;
                            ownerAttributes[attributeIndex] = attribute;

                            ownerFacts.Add(new GASOwnerLocalFactBuffer
                            {
                                TargetAsc = owners[i],
                                Domain = 2,
                                Category = delta.ReasonCode,
                                Code = delta.AttributeCode,
                                OldValue = oldValue,
                                NewValue = attribute.CurrentValue,
                                Frame = delta.Frame,
                                Sequence = delta.Sequence,
                            });
                            break;
                        }
                    }

                    ownerDeltas.Clear();
                    deltaPending[i] = false;
                    factPending[i] = ownerFacts.Length > 0;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    [UpdateBefore(typeof(PresentationOutboxProjectionSystem))]
    public partial struct GASBoundaryFactProjectionSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GASOwnerLocalFactPending>(),
                    ComponentType.ReadWrite<GASOwnerLocalFactBuffer>(),
                    ComponentType.ReadWrite<GASBoundaryObservationFactBuffer>(),
                },
            });
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ProjectBoundaryFactJob
            {
                FactPendingType = SystemAPI.GetComponentTypeHandle<GASOwnerLocalFactPending>(),
                CoreFactType = SystemAPI.GetBufferTypeHandle<GASOwnerLocalFactBuffer>(),
                BoundaryFactType = SystemAPI.GetBufferTypeHandle<GASBoundaryObservationFactBuffer>(),
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ProjectBoundaryFactJob : IJobChunk
        {
            public ComponentTypeHandle<GASOwnerLocalFactPending> FactPendingType;
            public BufferTypeHandle<GASOwnerLocalFactBuffer> CoreFactType;
            public BufferTypeHandle<GASBoundaryObservationFactBuffer> BoundaryFactType;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var factPending = chunk.GetEnabledMask(ref FactPendingType);
                var coreFacts = chunk.GetBufferAccessor(ref CoreFactType);
                var boundaryFacts = chunk.GetBufferAccessor(ref BoundaryFactType);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    var sourceFacts = coreFacts[i];
                    var targetFacts = boundaryFacts[i];
                    targetFacts.Clear();

                    for (var factIndex = 0; factIndex < sourceFacts.Length; factIndex++)
                    {
                        var fact = sourceFacts[factIndex];
                        targetFacts.Add(new GASBoundaryObservationFactBuffer
                        {
                            SourceAsc = fact.SourceAsc,
                            TargetAsc = fact.TargetAsc,
                            Domain = fact.Domain,
                            Category = fact.Category,
                            Code = fact.Code,
                            OldValue = fact.OldValue,
                            NewValue = fact.NewValue,
                            Frame = fact.Frame,
                            Sequence = fact.Sequence,
                        });
                    }

                    sourceFacts.Clear();
                    factPending[i] = false;
                }
            }
        }
    }

    public static class GASGeneratedDefinitionGlue
    {
        public static bool TryBuildEffectCommand(
            ref GASDefinitionCatalogBlob catalog,
            in GASIntent intent,
            int frame,
            int sequence,
            out GASEffectCommandRecord command)
        {
            command = default;
            if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                    ref catalog,
                    intent.GameplayEffectCode,
                    out var effectIndex))
            {
                return false;
            }

            ref readonly var effect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(
                ref catalog,
                effectIndex);

            command = new GASEffectCommandRecord
            {
                GameplayEffectCode = effect.GameplayEffectCode,
                SetByCallerMagnitude = intent.SetByCallerMagnitude,
                Frame = frame,
                Sequence = sequence,
            };
            return true;
        }

        public static bool TryResolveMagnitude(
            in GASCatalogModifierDefinitionBlob modifier,
            in GASMagnitudeSnapshot snapshot,
            out float magnitude)
        {
            var raw = modifier.MagnitudeSource switch
            {
                EMagnitudeSource.Constant => modifier.BaseMagnitude,
                EMagnitudeSource.SetByCaller => snapshot.SetByCallerValue,
                EMagnitudeSource.SourceAttribute => snapshot.SourceAttributeValue,
                EMagnitudeSource.TargetAttribute => snapshot.TargetAttributeValue,
                EMagnitudeSource.ExecutionCalculation => snapshot.ExecutionValue,
                EMagnitudeSource.StackCount => snapshot.StackCount,
                _ => modifier.BaseMagnitude,
            };

            var coefficient = modifier.Coefficient == 0f ? 1f : modifier.Coefficient;
            magnitude = ((raw + modifier.PreAdd) * coefficient) + modifier.PostAdd;
            return true;
        }

        public static bool TryBuildAttributeDelta(
            ref GASDefinitionCatalogBlob catalog,
            in GASOwnerEffectCommandBuffer command,
            out GASAttributeDeltaBuffer delta)
        {
            delta = default;
            if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                    ref catalog,
                    command.GameplayEffectCode,
                    out var effectIndex))
            {
                return false;
            }

            ref readonly var effect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(
                ref catalog,
                effectIndex);

            delta = new GASAttributeDeltaBuffer
            {
                AttributeCode = effect.PrimaryAttributeCode,
                AdditiveValue = command.SetByCallerMagnitude,
                Frame = command.Frame,
                Sequence = command.Sequence,
                ReasonCode = effect.GameplayEffectCode,
            };
            return true;
        }
    }

    public struct GASMagnitudeSnapshot
    {
        public float SetByCallerValue;
        public float SourceAttributeValue;
        public float TargetAttributeValue;
        public float ExecutionValue;
        public int StackCount;
    }

    internal struct GASRuntimeEvidenceBuffer : IBufferElementData
    {
        public int Frame;
        public int CommandCount;
        public int NativeStreamSegmentCount;
        public int MergeCostMicroseconds;
        public int ProofOnlyCarrierCount;
        public int RandomLookupCount;
        public int StructuralPlaybackCount;
        public int ObservationMaterializationCount;
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    public partial struct GASDiagnosticsEvidenceProjectionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // 目标态中这里只投影 counter / official capture state。
            // 字符串、Mermaid、战报和 UI 必须由 derived export 再消费 evidence。
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GASStructuralCommitSystemGroup))]
    public partial struct GASStructuralCommitOwnerSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            _ = ecb;
        }
    }
}
```

## 代码解读

| 代码面 | 目标态含义 | 为什么合理 |
|---|---|---|
| `IGASRuntimeSession` | session / fixed tick / dispose 是独立 capability | World、SystemGroup、catalog lifetime 和 tick source 不泄露给 UI / AI / Demo / Network |
| `IGASCommandPort` | Shell 只能发送 intent，拿 request id / reject reason | 外部业务不能同步执行 damage / cost / cooldown / requirement，也拿不到 ECS 写句柄 |
| `IGASSnapshotReadModel` | snapshot 只来自 BoundaryProjection | 读模型不 live 读 `DynamicBuffer` 或 `EntityQuery`，避免业务层持有 Runtime Core 细节 |
| `IGASDiagnosticsSink` | Debugger 输出 structured evidence | performance pass、diagnostic pass、official diff pass 可以分域对账，不靠字符串日志证明性能 |
| `IGASDefinitionCatalogLifetime` | catalog install / release 独立于 command 与 runner sync | Definition 生命周期不成为 generated lifecycle、managed row lookup 或 hot path BlobBuilder 通道 |
| `GASBoundaryCommandBuffer` | Boundary 只把 intent 压成 owner-local command record | Boundary implementation 可以解析 `Entity`，但解析结果不向 Shell 扩散 |
| `GASBoundaryCommandResolveSystem` | Core 用 `IJobChunk` 批处理 command | query owner、type handle、enableable mask、dependency 都在 lane system 内可审查 |
| `NativeStream` + `SegmentCounts` + `MergeEffectCommandFanInJob` | 多 producer fan-in、per-segment evidence 与 deterministic merge 分开 | 不在 parallel job 中共享写计数器；不依赖 worker 写入顺序；sort key、segment count、merge cost 可以进入 evidence |
| `GASOwnerEffectCommandBuffer` | deterministic merge 后把 command dispatch 到 target owner-local buffer | 后续 spec / delta / fact lane 不再读 singleton 总线，也不要求 Shell 或 Debugger 理解 stream carrier |
| `GASEffectSpecBuildSystem` | owner chunk 内把 command 投影为 attribute delta 与 owner-local fact | Definition pure glue 只提供 immutable lookup / evaluator；spec build owner 拥有 query、buffer、enableable mask 和 dependency |
| `GASAttributeDeltaApplySystem` | owner chunk 内线性 apply delta 并写 owner-local fact | Attribute 权威状态只在 owner-local buffer 中变化，Core reaction fact 从权威数据派生 |
| `GASBoundaryFactProjectionSystem` | BoundaryProjection 把 owner-local fact 复制为只读 observation fact 后清理 Core fact | Presentation / Replay / Debugger 读 Boundary observation，不直接读取 Core carrier |
| `GASGeneratedDefinitionGlue` | SourceGenerator 只生成 Blob lookup 和 pure evaluator | 没有 `ISystem`、`OnUpdate`、query、ECB、NativeContainer owner 或 runtime registration |
| `GASStructuralCommitOwnerSystem` | 结构变化只在 StructuralCommit owner 发生 | 其他 Core lane 写 structural intent，不直接 create / destroy / add / remove |

## 完整性审查口径

这份代码骨架的完整性不来自代码行数，而来自 owner 是否闭合。实现 Agent 可以拆文件、换类型名或扩展字段，但不能改变下列 owner 关系。

| 必备 owner | 代码骨架中的最小表现 | 深化时应扩展到哪里 | 不能补到哪里 |
|---|---|---|---|
| Session / Bootstrap | `IGASRuntimeSession` + `GASFixedTickResult` | World / SystemGroup install、catalog install、fixed tick policy、timing split、dispose proof | Application Shell public API 暴露 `World` / `EntityManager` |
| Command capability | `IGASCommandPort.Send(...)` + `GASBoundaryCommandBuffer` | intent validation、request id、owner-local append、reject reason、command pressure evidence | Shell 同步执行 damage / cooldown / requirement |
| Snapshot capability | `IGASSnapshotReadModel.TryReadAsc(...)` | BoundaryProjection snapshot ring、version cursor、staleness / compaction counter | live `DynamicBuffer` getter、`EntityQuery` scan |
| Core command lane | `GASBoundaryCommandResolveSystem` + `ResolveBoundaryCommandJob` | query contract、chunk-local scratch、lane evidence、dependency chain | OOP manager、static helper、generated lifecycle |
| Fan-in store | `NativeStream` + `SegmentCounts` + `MergeEffectCommandFanInJob` + owner-local dispatch | segment budget、capacity / spill、merge cost、battle hash、reselect trigger、owner group count | singleton `DynamicBuffer` 作为 scale-ready 总线；parallel job 共享写单个 counter |
| Spec / Delta / Fact lane | `GASEffectSpecBuildSystem` + `GASAttributeDeltaApplySystem` + `GASBoundaryFactProjectionSystem` | generated pure definition lookup、owner-local delta buffer、owner-local CoreReactionFact、BoundaryObservationFact export、owner-local attribute apply | generated lifecycle system、OOP callback、cross-owner random write lookup、Presentation 直读 Core fact |
| Definition glue | `GASGeneratedDefinitionGlue` | immutable Blob lookup、static evaluator、validation metadata、Editor/Baker binding | generated `ISystem`、`OnUpdate`、query、ECB、NativeContainer owner |
| ActiveEffect lifecycle | 本文件只规定 owner 位置，具体 lane 由 `ActiveEffectStore` / `ActiveEffectLifecycleLane` 承担 | period / stack / duration / grant cleanup、magnitude snapshot、slot capacity、cleanup fact | generated runtime lifecycle、static helper、Debugger export |
| Diagnostics evidence | `IGASDiagnosticsSink` + `GASRuntimeEvidenceBuffer` | Core / Boundary / Diagnostics / Runner / Presentation / Official Capture / Derived Export 分域计时 | 字符串日志、Mermaid、战报作为机器验收源 |
| StructuralCommit | `GASStructuralCommitOwnerSystem` | structural intent buffer、ECB playback phase、Journaling / Profiler source TopN | Core lane / Shell / generated glue 直接 create / destroy |

因此，目标态代码应该按“Shell 只提交 intent、Boundary 只投影 record、Core 只跑 ECS data、Definition 只提供 pure data、Debugger 只产 evidence”的方向扩展。任何为了方便实现而新增的 `RuntimeAccess`、`RuntimeShell`、`GeneratedLifecycleSystem` 或 `DebuggerController`，只要让调用方重新理解 ECS handle、query、allocator、dependency、carrier、capacity、merge 或 structural playback，就不是目标态实现。

## 重新划分合理性

1. **Shell 更深**：业务层只知道 session、target ref、intent、request id、snapshot 和 evidence。删掉 Shell capability 时，复杂度回到 Boundary implementation，而不是散落到 UI、AI、Demo 和 Debugger。
2. **Boundary 更薄**：Boundary 只负责 identity resolve、command append、snapshot projection 和 diagnostics export。它不承载 damage、cooldown、tag requirement、stack、period 或 execution calculation。
3. **Core 更纯**：Core lane owner 拥有 query、type handle、carrier、allocator、dependency、structural policy 和 evidence counter；OOP 不参与 gameplay 热路径。
4. **Owner-local 更深**：fan-in merge 的输出立即 dispatch 到 target owner-local command buffer，后续 spec build、attribute delta 和 CoreReactionFact 都在 owner chunk 内完成；BoundaryObservationFact 只由 BoundaryProjection 派生，调用方不需要理解 singleton stream、buffer cursor 或 merge helper。
5. **Fan-in 可证明**：`NativeStream` 只是承载，deterministic merge、sort key、segment / capacity / spill counter、owner group count 和 battle hash 才是 scale-ready 证明。
6. **Debugger 可归因**：Debugger evidence 是机器可读事实源；文本、图表和战报只能派生，不反向驱动 simulation。
7. **SourceGenerator 收权**：生成层只把不可变定义转为 lookup / record / evaluator / validation metadata，不拥有 runtime lifecycle 或调度。

## 验收门

| Owner | 必须证明 | 禁止通过 |
|---|---|---|
| Shell / Adapter | public seam 不返回 `World`、`EntityManager`、raw `Entity`、`EntityQuery` 或 writable buffer | 用 internal shared resolver 充当多 capability facade |
| Boundary Command | command write 和 snapshot read 分权、分计时、分 evidence | command API 同步读取 live state 或返回 gameplay 结果 |
| Core Lane | query / lookup / allocator / dependency / carrier / structural policy 明确 | generated lifecycle、static helper 或 OOP manager 隐式代管 lane owner |
| Fan-in | `NativeStream` segment、deterministic sort key、merge cost、spill / capacity、battle hash | singleton DynamicBuffer 作为 scale-ready 总线 |
| Gameplay Fact | CoreReactionFact owner-local、BoundaryObservationFact 只读导出、fact export cursor 和 owner group evidence | Presentation / Replay / Debugger 直接读取或清理 Core owner-local fact |
| ActiveEffect | owner-local slot、timing key、magnitude snapshot、capacity / spill、cleanup intent | slot lifecycle 继续由 generated `OnUpdate` 或 static helper 掩盖 |
| Diagnostics | evidence tier、official capture state、observation materialization timing、drop / disabled reason | 字符串日志、Mermaid、summary hash 或 disabled profiler reason 单独证明性能 |
| Definition Glue | generated artifact scan 证明无 lifecycle / query / ECB owner | 用 pure resolver 正向证据掩盖 generated runtime system |

## 禁止方向

1. 不创建新的万能 `RuntimeShell` / `RuntimeAccess` / `RuntimeAdapter` 来包装 `EntityManager`。
2. 不让 SourceGenerator 实现 `IGASCoreLaneOwner` 或生成 runtime `ISystem` lifecycle。
3. 不把 Debugger observation materialization 混入 CoreSimulation 性能结论。
4. 不把 AutoChess / Demo 的 report key、战报、Mermaid 图或中文 summary 当 Core evidence source。
5. 不把 `NativeStream` 名称本身当性能证明；缺 merge policy 和 evidence 时仍是 proof-only。
6. 不把 Core owner-local fact buffer 暴露给 Application Shell、Presentation、Replay 或 Debugger derived export。
