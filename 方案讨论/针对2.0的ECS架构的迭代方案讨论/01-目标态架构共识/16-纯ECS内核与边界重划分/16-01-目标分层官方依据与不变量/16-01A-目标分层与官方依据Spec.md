# 16-01A：目标分层与官方依据

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分/16-01` | 状态：目标态 Spec 子页 | 拆分来源：`../16-01-目标分层官方依据与不变量Spec.md` | 最近拆分：2026-06-08

本文件只描述纯 ECS 内核与边界重划分的目的、DOTS 官方依据、目标分层图和分层不变量。Owner Map、验收表、消息流协议和完整代码骨架分别由相邻子页维护。

## 目的

定义 EX-GAS 2.0 的理想目标态：Gameplay 权威状态和热路径计算必须落在纯 ECS Runtime Core；OOP 只作为 Application Shell 和 Runtime Boundary 的外壳；Debugger / 日志是证据系统；Luban + SourceGenerator 只生成不可变定义、静态 lookup、pure glue 和 validation artifact。

本 Spec 是对 `00-总览Spec.md`、`02-四层架构Spec.md`、`03-RuntimeCore管线Spec.md`、`07-RuntimeCoreDebuggerSpec.md` 和 `08-Luban-SourceGenerator配置生成链路Spec.md` 的一次收口，不替代它们的细节。官方 DOTS 复核门槛以 `18-DOTS官方规范复核与性能红线Spec.md` 为准；本文件中的代码骨架必须能通过该门槛。

## 官方依据

| 规则 | 本 Spec 采用结论 |
|---|---|
| `SYS-01`、`SYS-02`、`SYS-05` | Core 权威计算落在 ECS System / Job；SystemGroup 是 phase owner；Debugger / Demo / Presentation 只能通过 Boundary 观察 Core |
| `SEL-01`、`SEL-02`、`SEL-04` | 先按数据性质选 API；proof-only carrier 不能固化为 scale-ready；每个选择必须有重选型触发条件 |
| `QRY-01`、`QRY-04`、`JOB-01` | hot path 默认 job 化，避免高频 random lookup；主线程 query 仅限 debug / low-scale / boundary |
| `SC-01`、`ECB-03`、`PRF-04` | hot path 结构变化集中到明确 `GASStructuralCommitSystemGroup` playback |
| `BUF-02`、`NAT-03`、`MAT-05` | singleton DynamicBuffer 只能 proof；并行 fan-in 必须 deterministic merge |
| `DBG-01..05` | Debugger 是 counter / official diff / evidence owner，不是 runtime 控制层 |
| `BLOB-01`、`BLOB-02`、`BUR-01` | 静态定义进入 Blob / generated lookup；SourceGenerator 不生成 runtime lifecycle owner |

## 目标分层

```mermaid
flowchart TD
    Shell["Application Shell\nUI / AI / Network / Demo / Editor"]
    Boundary["Runtime Boundary\nRuntimeSession / CommandPort / SnapshotReadModel / DiagnosticsSink / RunnerSync / DefinitionCatalogLifetime / PresentationOutbox"]
    Core["Pure ECS GAS Runtime Core\nSystemGroup / ISystem / Job / Buffer / Blob / ECB"]
    Generation["Definition & Generation\nLuban / SourceGenerator / Blob / Pure Glue / Validation"]

    Shell -->|"intent only"| Boundary
    Boundary -->|"command record / owner-local buffer"| Core
    Generation -->|"immutable catalog + generated pure glue"| Core
    Core -->|"typed fact / snapshot / marker"| Boundary
    Boundary -->|"read model / replay / debugger evidence"| Shell
```

### 分层不变量

1. Application Shell 不持有 `EntityManager`、`EntityQuery` 或可写 `DynamicBuffer`。
2. Runtime Boundary 可以是 OOP，但只能翻译 command、导出 snapshot、转发 presentation / diagnostics；不做 damage、cooldown、tag requirement、stack、period 等 gameplay 计算。
3. Runtime Core 不调用 `GameObject`、`MonoBehaviour`、UI、VFX、SFX、日志字符串或 managed config registry。
4. Debugger 只读 Core facts / counters，不反向驱动 simulation。
5. SourceGenerator 只生成数据和纯函数，不生成 gameplay lifecycle system、hidden query、hidden ECB 或 hidden `EntityManager` write。

### 目标态整体重划分

目标态不是按目录把代码重新命名，而是按 owner graph 重新划分。每个 owner 必须同时回答三件事：它拥有哪类数据、哪条执行时序、哪套 evidence。不能回答这三件事的模块只能算迁移期 helper 或 facade。

| 目标层 | 允许拥有 | 禁止拥有 | 验收方式 |
|---|---|---|---|
| Application Shell | UI / AI / Network / Demo 的业务 intent、opaque handle、业务 report、用户操作时序 | `World`、`EntityManager`、raw `Entity`、runtime singleton、live buffer、gameplay calculation | public API 静态扫描和 capability 授权表 |
| Runtime Boundary | identity resolve、command record append、snapshot projection、diagnostics capture、runner sync、catalog install handle | damage / heal / cost / cooldown / tag requirement / stack / period / execution calculation | command / snapshot / diagnostics / runner / definition capability 分开计时 |
| Pure ECS Runtime Core | SystemGroup phase、lane query、type handle、lookup refresh、job chain、allocator、carrier、structural policy、owner-local store、counter | OOP manager 中间层、managed config row、GameObject / MonoBehaviour、字符串日志、generated lifecycle owner | lane owner table、API selection table、Profiler / Journaling / validation evidence |
| Definition & Generation | Luban schema、stable id、Blob catalog、static lookup、pure evaluator、Baker / Editor metadata、validation graph | runtime `ISystem`、`OnUpdate`、query、ECB、NativeContainer allocator、system registration | generated artifact scan：lifecycle / ownership hit 为 0 或被判定为目标态失败 |
| Diagnostics / Derived Export | counter、official capture state、validation evidence、derived text / graph / report source | gameplay decision、command write、Core lane ownership、structural commit ownership | cost domain 分组：Core / Boundary / Diagnostics / Runner / Presentation / Official Capture / Derived Export |

设计判定时优先看消息方向，而不是看 namespace。合法消息方向只有：

```text
Shell intent -> Boundary command record -> ECS Core lane
Definition immutable data -> ECS Core lane
ECS Core fact/counter -> Boundary snapshot/evidence -> Shell/Debugger/Report
Structural intent -> StructuralCommit playback
```

任何反向依赖都必须判定失败：Core 不能调用 Shell；Debugger 不能反向驱动 Core；SourceGenerator 不能生成 lifecycle owner；Boundary 不能同步读取 live Core state 再决定 gameplay 结果。
