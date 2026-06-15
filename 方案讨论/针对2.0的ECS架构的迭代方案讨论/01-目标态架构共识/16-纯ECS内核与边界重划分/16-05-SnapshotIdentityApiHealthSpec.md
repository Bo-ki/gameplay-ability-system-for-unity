# 16-05：Snapshot、Identity、API Health 与验收

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-07

本文件只描述 Snapshot Capture、Identity Exposure、Report Projection、API Health owner model、验收和禁止方向。

## 与 16-06 的 Owner 裁决

`16-05` 是 Snapshot capture、Identity exposure、Report projection、API Health owner model 和验收门的局部规则 owner；`16-06` 只消费这些规则来呈现完整端到端代码骨架。若两者出现分叉，snapshot 是否 copy、identity 是否 opaque、diagnostic-only 字段如何隔离、API health 如何按 owner 归因和验收门槛以本文件为准。

本文件不维护 Shell capability public seam 的完整代码，也不维护 fan-in / Debugger / SourceGenerator pure glue 的完整代码；这些分别归 `16-04` 和 `16-03`。

## Snapshot Capture Contract

目标态的 read model 必须是 Runtime Boundary 的 snapshot 产物，不是 Shell 按需 live capture。Snapshot 由 BoundaryProjection、presentation outbox 或同等级只读投影阶段生产，并以 snapshot version、opaque target key 和数据副本对外发布。

约束：

1. Shell 只能请求 snapshot by key / version，不能持有 `EntityManager`、`EntityQuery`、runtime singleton 或 live `DynamicBuffer`。
2. Boundary implementation 可以在投影阶段短暂解析 runtime identity，但 public snapshot 类型只能包含值类型字段、稳定 key、显示数据和版本号。
3. Snapshot capability 与 diagnostics capability 必须分离。Diagnostics 可以 live gather 或导出 raw ECS identity，但字段必须标记 diagnostic-only，不能被业务 UI、AI、战报、回放或网络同步当成权威状态。
4. 按需 live capture 只能作为测试夹具或 proof-only compatibility，不能作为目标态 SnapshotReadModel 的主路径；保留时必须有独立 capability、独立成本归因和退出门。
5. Snapshot 生产必须有 owner、phase、version、capacity、copy cost 和 stale policy；缺少这些字段时，read model 只能算临时观察面，不能进入目标态验收。

验收时，任何 read model 都必须能回答：它由哪个投影 phase 生产、版本如何推进、数据是否是 copy、是否允许跨帧缓存、是否包含 diagnostic-only 字段、以及 Shell 是否无法反向取得 live ECS 句柄。

## Identity Exposure Contract

目标态必须把 ECS runtime identity 和业务 identity 分开。`Entity` 可以作为 Runtime Core 内部状态地址，也可以在 Runtime Boundary implementation 内短暂用于写 command、抓 snapshot 或执行 bootstrap；但它不能成为 Application Shell、Demo session、业务 report、UI、AI 或网络层的稳定 key。

| 场景 | 对外 identity | Boundary 内部可用 identity | 禁止 |
|---|---|---|---|
| Shell command | `ASCHandle` / `ASCTargetRef` / 业务 unit ref | owner ASC `Entity`、target ASC `Entity` | Shell 持有 `EntityManager` 或 raw `Entity` |
| Read model / snapshot | snapshot version + opaque ASC key | projection source `Entity` | getter 回读 live `DynamicBuffer` |
| Demo report | battle unit id / report key / structured log key | structured fact 中的 source / target ASC `Entity` | `Dictionary<Entity, ...>` 作为业务报告长期索引 |
| Network / save / replay | deterministic gameplay id / frame-local event id | Runtime projection 阶段的 transient `Entity` | 把 `Entity.Index/Version` 序列化为业务权威 |
| Debugger evidence | owner category + system / lane / component key | query gathered `Entity` | 将 Debugger gather key 反向喂给 simulation |

验收时，任何 raw ECS identity 泄露都必须被 owner 分类：Core internal、Boundary implementation、Debugger gather、Demo Adapter proof-only compatibility。未分类的 raw `Entity`、`EntityManager` 或 `EntityQuery` 出现在 Shell / Demo business / report 模型中，视为 Thin Adapter 失败。

Opaque handle 的验收门更严格：handle 可以保存 internal ECS identity，但 public API 只能用于 equality、validity、display-safe debug text 或 request/snapshot lookup。任何 public 方法返回 `Entity`、`EntityManager`、`EntityQuery`、runtime singleton entity 或 live buffer，都不再是 opaque handle，而是 Boundary implementation 细节泄露。

### Report Projection Identity Contract

业务战报、Replay 导出、UI 日志和网络同步不能直接消费 raw ECS identity。Runtime Core 可以在 typed fact 中保留 source / target runtime identity 供 Boundary implementation 内部解析，但对 Shell / Demo / UI / Network 暴露的报告数据必须先投影为稳定的 report key、battle unit id、snapshot key 或 event id。

目标态投影顺序如下：

```text
Core typed fact
  -> Boundary report fact projector
  -> stable report fact / battle event
  -> business report / UI log / replay export
```

约束：

1. Business report builder 只能消费 stable report facts、unit snapshots、event ids 或 display data，不接收 `Entity`、`EntityManager`、`EntityQuery` 或 live buffer。
2. Boundary report fact projector 可以在内部解析 runtime `Entity`，但 public 输出必须是 battle unit id、report key、snapshot key 或 event id。
3. structured log 如果需要跨帧、跨 world、回放或网络复用，必须包含 deterministic report key；不能要求消费者用 `Entity.Index/Version` 反查业务对象。
4. Debugger 可以导出 raw ECS identity 作为诊断字段，但该字段必须标记为 diagnostic-only，不能作为 battle result、UI log、save、replay 或 network authority。
5. 验收时必须能证明业务战报由 stable report fact 派生；raw ECS identity 只能停留在 Boundary implementation 或 Debugger gather 阶段。

## API Health Owner Model

目标态的 API health evidence 必须先按 owner 分类，再进入性能结论。没有 owner 的 `EntityManager`、query materialization、sync point、managed allocation、NativeContainer、ECB 和 singleton carrier 命中，不能直接被解释为 Core hot path 成本，也不能被 Debugger / Demo / Boundary 成本稀释成平均 tick。

| Owner | 允许的 API 健康信号 | 必须输出的证据 | 禁止混写 |
|---|---|---|---|
| Runtime Core | job schedule、lookup update、owner-local buffer pressure、NativeStream segment、ECB command、random lookup、chunk skip | SystemGroup / lane / system / job / component / carrier / reselect trigger | Shell command、UI、Demo runner、Debugger string log |
| Runtime Boundary | command write、snapshot copy、presentation outbox、managed Cue bridge | Shell intent count、Boundary queue length、snapshot version、managed side-effect cost | damage / cost / cooldown / stack / period / execution calculation |
| Debugger / Observation | query gather、Profiler / Journaling state、counter export、diagram / log derivation | captured / disabled reason、TopN source、overhead split、derived export source | 反向驱动 simulation、替代 Core evidence |
| Definition / Generation | Blob build、lookup generation、validation gate、Baker / Bootstrap artifact | artifact category、lifecycle owner、dispose owner、boundary hit gate | generated runtime lifecycle、hidden query、hidden ECB、NativeContainer owner |
| Demo Adapter | runtime host、catalog session、battle lifecycle request、validation evidence export | headless / scene 同构 evidence、core / boundary / debugger / runner / physics / render split | raw ECS handle 作为业务读写接口、把 demo direct EM 当通用 Core 实现 |

验收时必须能把同一条 summary 拆回上述 owner。`avgTickMs`、`blockingDebugErrors=0`、字符串日志或 Mermaid 图只能作为导出结果；真正的 validation source 必须是结构化 evidence。

## 验收

1. Runtime Core hot path 不依赖 OOP manager / shell / adapter。
2. Shell 发起命令时不暴露 `EntityManager`；read model 不暴露 live ECS buffer。
3. Shell、Demo session、business report、UI、AI、Network 不使用 raw ECS `Entity` 作为稳定 identity；只允许 opaque handle、battle unit id、report key 或 snapshot key。
4. Shell / Adapter capability 必须分级：command、snapshot、diagnostics、bootstrap、definition 不能共享同一个可写 ECS 句柄出口。
5. Effect fan-in / active mutation 不得以 singleton DynamicBuffer 作为 scale-ready 方案；proof-only 例外不能构成目标态完成证明。
6. 所有结构变化能归因到 `GASStructuralCommitSystemGroup`，并由 Debugger / Journaling / Profiler 证明。
7. Debugger evidence 至少覆盖 API health、proof-only API、random lookup、buffer pressure、structural playback、official tool state。
8. SourceGenerator 输出不包含 runtime lifecycle owner；目标态 runtime-visible generated code 不包含 `ISystem` / `EntityManager` / ECB / query owner。
9. AutoChess 验收矩阵必须覆盖 x50 / x100 / x1000 等规模档，并输出 core / boundary / debugger / runner timing split、battle hash / summary hash、blockingDebugErrors。

## 禁止方向

1. 不恢复旧 OOP Runtime 主链。
2. 不把 Debugger / Logger / Replay 写成实时 gameplay event bus。
3. 不把 singleton stream、global facade、managed config registry 作为目标态。
4. 不让 SourceGenerator 生成 Runtime lifecycle、query owner 或结构变化 owner。
5. 不把 AutoChess adapter 的 direct `EntityManager` 操作当作通用 Runtime Core 实现。
6. 不把功能跑通或 `blockingDebugErrors=0` 当作 DOTS 性能优秀证明。
