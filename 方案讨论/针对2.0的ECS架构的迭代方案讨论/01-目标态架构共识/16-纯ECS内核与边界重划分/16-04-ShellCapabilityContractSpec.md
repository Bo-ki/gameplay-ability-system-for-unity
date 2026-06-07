# 16-04：Shell Capability Contract

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-07

本文件只描述目标态 Shell / Adapter capability contract、public seam 和 opaque handle 代码骨架。

## 重新划分后的主接口

| Module | Interface | Implementation 可复杂化的部分 | 不允许泄露 |
|---|---|---|---|
| `CommandPort` | `Request*` command intent | direct buffer write、sequence、pending marker、validation reason | `EntityManager`、runtime buffer、立即执行语义 |
| `SnapshotReadModel` | immutable snapshot / copy API | Boundary projection、cursor、ring buffer、event compaction | live `DynamicBuffer` / `EntityQuery` |
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
| Bootstrap capability | runtime session install / dispose / fixed tick owner | World / SystemGroup / catalog session implementation | 让 UI / Battle report / AI / Network 持有 World 或 `EntityManager` |
| Definition capability | definition catalog install / validation artifact | Blob build、generated lookup、lifecycle owner evidence | managed config registry 进入 Core hot path |

Shell capability 的验收原则：

1. 一个 public Shell / Adapter API 不能同时返回 command port 与 `EntityManager`。
2. Diagnostics / Editor watcher 只能消费 snapshot 或 evidence，不复用 command capability 的可写句柄。
3. Bootstrap capability 可以在实现内部拥有 World / SystemGroup，但只能输出 session state、validation evidence 或 timing snapshot。
4. runtime singleton 只能作为 Boundary implementation 的内部 owner；外部业务不得通过 Shell 取得 singleton `Entity`。
5. proof-only compatibility 若必须暴露 ECS identity，必须标注 owner category、禁用业务持久化，并绑定退出条件。
6. Runtime access implementation 可以在内部组合多个 capability，但 public seam、validation evidence 和性能归因必须按 capability 拆开；一个聚合 access API 不能同时作为 bootstrap、command、snapshot、diagnostics、definition 和 job-drain 的验收接口。
7. Runner / validation 可以拥有 fixed tick driver，但 tick driver 只输出 timing snapshot 和 evidence，不得复用 command capability 的可写句柄，也不得把 dependency drain 成本并入 CoreSimulation 热路径结论。
8. Adapter topology 必须可追踪：每个 runtime-facing adapter type 要么是被 public boundary 聚合引用的 capability owner，要么是显式归档 / legacy 文件；未被消费的 shadow host、shadow gateway 或 shadow facade 不能作为目标态分层证明。
9. Opaque handle 不得提供 public raw ECS identity 导出口。若 Boundary implementation 需要解析 `Entity`，只能通过 internal resolver 完成；Shell / Demo / UI / AI / Network 不能调用 `TryGetEntity*` 之类方法取得 raw `Entity`。
10. Internal resolver 不是共享 access layer。它只能服务单一 capability owner 的实现细节，不能同时被 command、snapshot、diagnostics、bootstrap、report projection 复用成万能实体查询口；如果多个 capability 都需要同一 runtime identity，必须通过 opaque handle / report key / snapshot key 建立各自的稳定映射。

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

    public interface IGASBootstrapCapability
    {
        GASRuntimeSessionId InstallRuntime(in GASRuntimeInstallOptions options);
        void DisposeRuntime(GASRuntimeSessionId session);
        GASFixedTickResult RunFixedTick(GASRuntimeSessionId session, in GASFixedTickOptions options);
    }

    public interface IGASDefinitionCapability
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
            IGASBootstrapCapability bootstrap,
            IGASDefinitionCapability definitions)
        {
            Commands = commands;
            Snapshots = snapshots;
            Diagnostics = diagnostics;
            Bootstrap = bootstrap;
            Definitions = definitions;
        }

        public IGASCommandCapability Commands { get; }
        public IGASSnapshotCapability Snapshots { get; }
        public IGASDiagnosticsCapability Diagnostics { get; }
        public IGASBootstrapCapability Bootstrap { get; }
        public IGASDefinitionCapability Definitions { get; }
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
| `IGASBootstrapCapability` | 可以内部拥有 World / SystemGroup / fixed tick driver，但只能输出 session、tick result 和 dependency drain evidence | `SYS-02`、`JOB-02`、`ODF-09` |
| `IGASDefinitionCapability` | 只安装 immutable catalog / Blob handle；Runtime Core hot path 只读 catalog，不重新构建 managed config | `BLOB-01`、`BLOB-02`、`ODF-13` |
| `IGASRuntimeEntityResolver` | raw `Entity` 只能停留在 internal implementation，用于 command write 或 snapshot source resolve | `SYS-05`、`QRY-04` |

验收时，如果某个 adapter implementation 为方便实现把上述 capability 聚合在同一个类中，也必须在 public API、evidence 字段、timing split 和交还报告中分开证明。聚合 implementation 不能作为完成证明；只有 capability seam、owner 分类和成本归因都分开时，才算符合 Thin Adapter 目标态。
