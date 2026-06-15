# 16-04：Shell Capability Contract

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-07

本文件只描述目标态 Shell / Adapter capability contract、public seam 和 opaque handle 代码骨架。

## 与 16-06 的 Owner 裁决

`16-04` 是 Shell / Adapter capability、public seam、opaque handle 和 internal resolver contract 的局部规则 owner；`16-06` 只消费这些规则来串端到端消息流。若两者出现分叉，能力分级、public API 禁止项、opaque handle 约束和 internal resolver 限制以本文件为准，`16-06` 只同步最小完整代码骨架。

本文件不维护 fan-in、Debugger evidence、SourceGenerator pure glue、Snapshot capture 或 API health 的完整规则；这些分别归 `16-03` 和 `16-05`。

## 重新划分后的主接口

| Module | Interface | Implementation 可复杂化的部分 | 不允许泄露 |
|---|---|---|---|
| `CommandPort` | `Request*` command intent | direct buffer write、sequence、pending marker、validation reason | `EntityManager`、runtime buffer、立即执行语义 |
| `SnapshotReadModel` | immutable snapshot / copy API | Boundary projection、cursor、ring buffer、event compaction | live `DynamicBuffer` / `EntityQuery` |
| `RuntimeSession` | install / dispose / session state | World、SystemGroup、singleton owner、catalog bootstrap | public `World`、public `EntityManager`、gameplay formula |
| `RunnerSync` | fixed tick / dependency drain / timing split | tick source、measurement fence、drain reason、runner budget | command write、snapshot read、diagnostics export |
| `DefinitionCatalogLifetime` | catalog install / release / version handle | Blob lifetime、schema validation、dispose route | managed row、generated lifecycle、runtime registration |
| `GASFrameKernel` | command / target / effect / modifier / fact records | `NativeStream`、sort、owner-local range、chunk applicator | singleton stream owner、global EventBus |
| `StructuralCommit` | structural intent / ECB phase | custom ECB、bulk query、sort key | scattered create/destroy |
| `DiagnosticsSink` | evidence snapshot | counters、official diff、Profiler metadata | gameplay decision |
| `GeneratedDefinitionGlue` | static pure function | Blob schema、lookup strategy、switch / function pointer candidate | lifecycle system、hidden query |

## Shell Capability Contract

目标态 Shell 不能是一个可取出 ECS 句柄的万能 facade。Shell / Adapter 面向外部业务时必须按 capability 分级，每个 capability 只暴露它自身的业务意图或只读证据，不把 Runtime Core 的 `World`、`EntityManager`、`EntityQuery`、runtime singleton、可写 `DynamicBuffer` 或 raw `Entity` 交给调用方。

| Capability | 对外 Interface | Boundary implementation 可拥有 | 禁止 |
|---|---|---|---|
| Command capability | ability activate / GE apply / ASC destroy / target intent | owner-local command write、sequence、validation reason | 返回 runtime `Entity`、暴露 buffer、同步执行 gameplay |
| Snapshot capability | immutable ASC / battle / presentation snapshot | BoundaryProjection cursor、ring buffer copy、snapshot version | getter 回读 live ECS、把 snapshot key 写成 raw `Entity` |
| Diagnostics capability | structured evidence / counter / official diff snapshot | Debugger gather、Profiler / Journaling state、derived export | 反向写 simulation、与 command port 共用可写 handle |
| RuntimeSession capability | runtime install / dispose / session state | World / SystemGroup / singleton owner / bootstrap validation | 让 UI / Battle report / AI / Network 持有 World 或 `EntityManager` |
| RunnerSync capability | fixed tick request / dependency drain reason / timing split | tick source、measurement fence、runner budget、sync evidence | 承载 command write、snapshot read、diagnostics export 或 gameplay decision |
| DefinitionCatalogLifetime capability | immutable catalog install / release / validation artifact | Blob build、schema check、generated lookup、dispose owner | managed config registry 进入 Core hot path、生成 runtime lifecycle owner |

Shell capability 的验收原则：

1. 一个 public Shell / Adapter API 不能同时返回 command port 与 `EntityManager`。
2. Diagnostics / Editor watcher 只能消费 snapshot 或 evidence，不复用 command capability 的可写句柄。
3. RuntimeSession capability 可以在实现内部拥有 World / SystemGroup，但只能输出 session state、validation evidence 或 lifecycle result。
4. runtime singleton 只能作为 Boundary implementation 的内部 owner；外部业务不得通过 Shell 取得 singleton `Entity`。
5. proof-only compatibility 若必须暴露 ECS identity，必须标注 owner category、禁用业务持久化，并绑定退出条件。
6. Runtime access implementation 可以在内部组合多个 capability，但 public seam、validation evidence 和性能归因必须按 capability 拆开；一个聚合 access API 不能同时作为 session、command、snapshot、diagnostics、definition 和 runner sync 的验收接口。
7. RunnerSync / validation 可以拥有 fixed tick driver，但 tick driver 只输出 timing snapshot 和 evidence，不得复用 command capability 的可写句柄，也不得把 dependency drain 成本并入 CoreSimulation 热路径结论。
8. Adapter topology 必须可追踪：每个 runtime-facing adapter type 要么是被 public boundary 聚合引用的 capability owner，要么是显式归档 / legacy 文件；未被消费的 shadow host、shadow gateway 或 shadow facade 不能作为目标态分层证明。
9. Opaque handle 不得提供 public raw ECS identity 导出口。若 Boundary implementation 需要解析 `Entity`，只能通过 internal resolver 完成；Shell / Demo / UI / AI / Network 不能调用 `TryGetEntity*` 之类方法取得 raw `Entity`。
10. Internal resolver 不是共享 access layer。它只能服务单一 capability owner 的实现细节，不能同时被 command、snapshot、diagnostics、bootstrap、report projection 复用成万能实体查询口；如果多个 capability 都需要同一 runtime identity，必须通过 opaque handle / report key / snapshot key 建立各自的稳定映射。

## Capability Access Matrix

目标态允许 Boundary implementation 在内部接触 ECS handle，但每个 handle 必须有单一 capability owner、单一 timing domain 和单一 evidence 字段。任何实现类即使为了工程组织聚合多个 capability，也必须在 public seam、internal resolver、timing split 和 validation evidence 上拆开证明；聚合类名、目录或 facade 名称不能作为完成证明。

| Capability | Public return | Internal ECS handle scope | Timing domain | Required evidence | Forbidden reuse |
|---|---|---|---|---|---|
| `RuntimeSession` | session id、install / dispose result、session state | World、SystemGroup、bootstrap singleton owner、catalog bootstrap state | session / bootstrap | install result、dispose result、registered singleton count、bootstrap validation reason | command write、snapshot read、diagnostics export、runner drain |
| `CommandPort` | request id、validation failure reason | command buffer owner、target resolver、sequence allocator | boundary command write | request count、rejected count、target resolve miss、write pressure | returning `EntityManager`、running simulation immediately、reading snapshot |
| `SnapshotReadModel` | immutable snapshot、snapshot version、cursor status | BoundaryProjection buffer / ring、snapshot copy arena | boundary read | snapshot version, cursor lag, dropped snapshot count, copy cost | live `DynamicBuffer` read、raw `Entity` key, command write handle |
| `DiagnosticsSink` | structured evidence snapshot、official tool state、derived export handle | Debugger singleton / evidence buffers / official capture source | diagnostics | counter source, official capture state, disabled reason, derived export source | gameplay decision, command carrier, snapshot write |
| `RunnerSync` | tick result、drain result、timing split | fixed tick driver、dependency drain fence, measurement marker | runner / measurement | core / boundary / diagnostics / runner ticks, drain reason, drained job count | command write, catalog install, diagnostics export side effect |
| `DefinitionCatalogLifetime` | catalog handle、catalog version、install / release result | immutable catalog Blob, schema validation, dispose owner | definition lifetime | catalog version, schema hash, install result, release result, orphan / mismatch reason | managed row read in Core hot path, generated lifecycle, system registration |
| `PresentationBridge` | presentation binding id、presentation event cursor | presentation outbox, managed binding registry, view model cache | presentation | outbox lag, binding miss, presentation cost | Core fact carrier, command write, runtime singleton query |

目标态交还包必须给出 capability access matrix 的 before / after。只要任一 public 或 adapter-facing API 仍能取得 `World`、`EntityManager`、runtime singleton、raw `Entity`、`EntityQuery`、可写 `DynamicBuffer`、dependency drain 或 live buffer read，该 capability 只能判定为 proof-only compatibility，并必须绑定退出任务。

## Business Adapter Wrapper 禁止方向

目标态允许业务侧为了工程组织保留一个聚合 wrapper 或 facade，但该 wrapper 只能聚合 capability 对象，不能聚合 ECS handle 解析权。聚合 implementation 的存在不构成 Thin Adapter 完成证明；完成证明来自每个 capability 的 public seam、internal resolver 范围、timing domain 和 evidence 字段都可独立追踪。

| Wrapper 内部需求 | 目标态允许形态 | 禁止方向 |
|---|---|---|
| 启动 / 关闭 Runtime | 调用 `RuntimeSession` capability，返回 session id、install result、dispose result | 返回 `World` / `EntityManager`，让业务层自行注册或更新 SystemGroup |
| 安装 / 释放配置 catalog | 调用 `DefinitionCatalogLifetime` capability，返回 catalog handle、version、schema hash、release result | 用 catalog wrapper 暴露 `EntityManager`、managed row reader、runtime lifecycle system 或 system registration |
| 写入战斗 / Ability 命令 | 调用 `CommandPort` capability，返回 request id 和 validation reason | 同一个方法既返回 command port 又返回 ECS handle，或同步执行 gameplay |
| 读取业务结果 | 调用 `SnapshotReadModel` capability，返回 snapshot version、cursor、copy cost | live 读取 `DynamicBuffer`、用 raw `Entity` 作为稳定业务 key |
| 导出 Debugger / 日志 / Official evidence | 调用 `DiagnosticsSink` capability，返回 structured evidence、official tool state、derived export source | 复用 command write handle，或让 diagnostics 反向写 simulation |
| 驱动固定 tick / drain jobs | 调用 `RunnerSync` capability，返回 tick result、drain reason、runner timing | 把 dependency drain 当作 command、snapshot 或 CoreSimulation hot path 的副作用 |

Business Adapter 的验收规则：

1. 一个 wrapper 可以持有多个 capability 引用，但每个 public 方法必须只属于一个 capability。
2. wrapper 内部若需要共享 opaque session id、target ref 或 catalog handle，只能共享值对象，不共享 `World`、`EntityManager`、singleton、raw `Entity` 或 writable buffer。
3. RunnerSync、DiagnosticsSink 和 SnapshotReadModel 不能互相代替：tick / drain 只产生成本证据，diagnostics 只导出证据，snapshot 只复制 BoundaryProjection 结果。
4. 业务报告、UI、AI、Network 和 validation runner 都只能消费业务动作、opaque handle、snapshot 和 evidence；不得消费 internal resolver。
5. 如果一个 wrapper 方法临时必须代理 ECS handle，交还报告必须把它标为 proof-only compatibility，并给出目标 capability、替代 seam、退出任务和防回流扫描。

### 目标态 Shell / Adapter Capability 代码骨架

下面代码只描述目标态 public seam，不作为实现完成证明。设计目的：让 OOP Shell 只看见业务意图、opaque handle、snapshot 和 evidence；Runtime Boundary implementation 可以在内部解析 ECS identity，但不能把 `World`、`EntityManager`、`EntityQuery`、可写 `DynamicBuffer` 或 raw `Entity` 暴露给业务层。

```csharp
using Unity.Entities;

namespace GAS.Runtime.Boundary
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
        public readonly int Value;
        public readonly int Frame;
    }

    public readonly struct GASSnapshotVersion
    {
        public readonly int Frame;
        public readonly int Sequence;
    }

    public readonly struct GASDefinitionCatalogHandle
    {
        public readonly int Value;
        public readonly int Version;
    }

    public readonly struct GASTargetSetRef
    {
        public readonly int Value;
        public readonly int Count;
    }

    public readonly struct GASSetByCallerSet
    {
        public readonly int Value;
        public readonly int Count;
    }

    public readonly struct GASAscSnapshot
    {
        public readonly ASCTargetRef Target;
        public readonly GASSnapshotVersion Version;
        public readonly int Health;
        public readonly int Energy;
    }

    public readonly struct GASBattleSnapshot
    {
        public readonly GASSnapshotVersion Version;
        public readonly int AliveUnitCount;
        public readonly int FactCount;
    }

    public readonly struct GASDiagnosticsCaptureOptions
    {
        public readonly bool IncludeOfficialDiff;
        public readonly bool IncludeDerivedExports;
    }

    public readonly struct GASRuntimeEvidenceSnapshot
    {
        public readonly GASSnapshotVersion Version;
        public readonly long CoreTicks;
        public readonly long BoundaryTicks;
        public readonly long DiagnosticsTicks;
        public readonly bool OfficialCaptureAvailable;
    }

    public readonly struct GASRuntimeInstallOptions
    {
        public readonly bool AttachToPlayerLoop;
        public readonly bool EnableDiagnostics;
    }

    public readonly struct GASFixedTickOptions
    {
        public readonly int TickCount;
        public readonly bool CaptureDiagnostics;
    }

    public readonly struct GASFixedTickResult
    {
        public readonly int Frame;
        public readonly long CoreTicks;
        public readonly long BoundaryTicks;
        public readonly long DiagnosticsTicks;
        public readonly long RunnerTicks;
        public readonly bool DependencyDrainObserved;
    }

    public readonly struct GASRunnerSyncOptions
    {
        public readonly int SyncReason;
        public readonly bool CaptureTiming;
    }

    public readonly struct GASRunnerSyncResult
    {
        public readonly int Frame;
        public readonly int DrainedJobCount;
        public readonly int SyncReason;
        public readonly long DrainTicks;
        public readonly long MeasurementTicks;
    }

    public interface IGASCommandCapability
    {
        GASRequestId RequestActivateAbility(
            GASRuntimeSessionId session,
            ASCTargetRef source,
            int abilityCode,
            in GASTargetSetRef targets);

        GASRequestId RequestApplyGameplayEffect(
            GASRuntimeSessionId session,
            ASCTargetRef source,
            ASCTargetRef target,
            int gameplayEffectCode,
            in GASSetByCallerSet setByCallers);

        GASRequestId RequestDestroyAsc(
            GASRuntimeSessionId session,
            ASCTargetRef target);
    }

    public interface IGASSnapshotCapability
    {
        bool TryReadAscSnapshot(
            GASRuntimeSessionId session,
            ASCTargetRef target,
            GASSnapshotVersion minimumVersion,
            out GASAscSnapshot snapshot);

        bool TryReadBattleSnapshot(
            GASRuntimeSessionId session,
            GASSnapshotVersion minimumVersion,
            out GASBattleSnapshot snapshot);
    }

    public interface IGASDiagnosticsCapability
    {
        GASRuntimeEvidenceSnapshot CaptureEvidence(
            GASRuntimeSessionId session,
            GASDiagnosticsCaptureOptions options);
    }

    public interface IGASRuntimeSessionCapability
    {
        GASRuntimeSessionId InstallRuntime(in GASRuntimeInstallOptions options);
        void DisposeRuntime(GASRuntimeSessionId session);
    }

    public interface IGASRunnerSyncCapability
    {
        GASFixedTickResult RunFixedTick(GASRuntimeSessionId session, in GASFixedTickOptions options);
        GASRunnerSyncResult DrainForMeasurement(GASRuntimeSessionId session, in GASRunnerSyncOptions options);
    }

    public interface IGASDefinitionCatalogLifetimeCapability
    {
        GASDefinitionCatalogHandle InstallCatalog(
            GASRuntimeSessionId session,
            BlobAssetReference<GASDefinitionCatalogBlob> catalog);

        void ReleaseCatalog(
            GASRuntimeSessionId session,
            GASDefinitionCatalogHandle catalog);
    }

    public sealed class GASRuntimeBoundary
    {
        public GASRuntimeBoundary(
            IGASCommandCapability commands,
            IGASSnapshotCapability snapshots,
            IGASDiagnosticsCapability diagnostics,
            IGASRuntimeSessionCapability sessions,
            IGASRunnerSyncCapability runnerSync,
            IGASDefinitionCatalogLifetimeCapability definitions)
        {
            Commands = commands;
            Snapshots = snapshots;
            Diagnostics = diagnostics;
            Sessions = sessions;
            RunnerSync = runnerSync;
            Definitions = definitions;
        }

        public IGASCommandCapability Commands { get; }
        public IGASSnapshotCapability Snapshots { get; }
        public IGASDiagnosticsCapability Diagnostics { get; }
        public IGASRuntimeSessionCapability Sessions { get; }
        public IGASRunnerSyncCapability RunnerSync { get; }
        public IGASDefinitionCatalogLifetimeCapability Definitions { get; }
    }

    internal readonly struct RuntimeResolvedAsc
    {
        internal readonly Entity Entity;
    }

    internal interface IGASRuntimeEntityResolver
    {
        bool TryResolve(GASRuntimeSessionId session, ASCTargetRef target, out RuntimeResolvedAsc asc);
    }
}
```

约束解释：

| 代码面 | 目标态含义 | 官方规则依据 |
|---|---|---|
| `IGASCommandCapability` | 只接收 command intent，返回 request id；写入 owner-local command record 或 Boundary queue，不同步执行 gameplay | `SYS-01`、`QRY-01`、`SC-01` |
| `IGASSnapshotCapability` | 只读取 BoundaryProjection 产生的 snapshot；不得 live 读 `DynamicBuffer` 或查询 Runtime Core | `SYS-05`、`SEL-05`、`ODF-09` |
| `IGASDiagnosticsCapability` | 只导出 structured evidence、Profiler / Journaling 状态和 derived export source；不得驱动 simulation | `DBG-01..05`、`SYS-05` |
| `IGASRuntimeSessionCapability` | 可以内部拥有 World / SystemGroup / singleton owner，但只能输出 session lifecycle result | `SYS-02`、`ODF-09` |
| `IGASRunnerSyncCapability` | 独立承载 fixed tick、dependency drain reason 和 timing split，不能成为 command / snapshot / diagnostics API 的副作用 | `JOB-02`、`DBG-05`、`ODF-09` |
| `IGASDefinitionCatalogLifetimeCapability` | 只安装 immutable catalog / Blob handle；Runtime Core hot path 只读 catalog，不重新构建 managed config | `BLOB-01`、`BLOB-02`、`ODF-13` |
| `IGASRuntimeEntityResolver` | raw `Entity` 只能停留在 internal implementation，用于 command write 或 snapshot source resolve | `SYS-05`、`QRY-04` |

验收时，如果某个 adapter implementation 为方便实现把上述 capability 聚合在同一个类中，也必须在 public API、evidence 字段、timing split 和交还报告中分开证明。聚合 implementation 不能作为完成证明；只有 capability seam、owner 分类和成本归因都分开时，才算符合 Thin Adapter 目标态。
