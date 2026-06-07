# 16-01B：判定准则与 Owner Map

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分/16-01` | 状态：目标态 Spec 子页 | 拆分来源：`../16-01-目标分层官方依据与不变量Spec.md` | 最近拆分：2026-06-08

本文件只描述目标态架构重划分判定准则、Module Owner Map、Owner Map 不变量、职责重划分验收表和目标态代码骨架判定准则。消息流协议和完整代码骨架不在本页重复维护。

## 架构重划分判定准则

目标态评审不以“是否用了 ECS API”作为合格标准，而以 owner、数据局部性、接口深度和证据可归因为准。任一设计必须同时满足下列判定：

| 领域 | 合格形态 | 不合格形态 |
|---|---|---|
| Shell / Adapter | public seam 只暴露业务 intent、opaque handle、snapshot、diagnostics evidence 和 session/tick result | public API 返回 `World`、`EntityManager`、runtime singleton、raw `Entity`、`EntityQuery` 或 live buffer |
| Runtime Boundary | implementation 可以短暂解析 ECS identity，但每个 resolver 只服务单一 capability owner | 一个 internal access layer 同时服务 command、snapshot、diagnostics、bootstrap、definition 和 dependency drain |
| Runtime Core | SystemGroup 表达物理 phase；lane system / job 拥有 query、lookup、allocator、carrier、dependency 和 structural policy | OOP manager、generated lifecycle 或 singleton facade 隐藏 query、ECB、NativeContainer owner 或 scheduling |
| Frame-local data | command、target、spec、delta、fact 按数据性质选择 `NativeStream`、owner-local range、bounded buffer 或 chunk-local scratch | 全局 singleton DynamicBuffer 被当成默认 scale-ready fan-in 或跨语义总线 |
| Cross-owner access | 先按 target / owner 分组，再 deterministic merge 或 chunk-local apply | 在 hot path 中用 wide `ComponentLookup` / `BufferLookup` 任意写其他 owner，且无 alias / ordering 证明 |
| Debugger / 日志 | evidence 是机器可读结构，导出日志和图表从 evidence 派生 | hot path 直接拼接字符串，或用字符串 summary / Mermaid 图替代 runtime counter 与官方工具状态 |
| SourceGenerator | 生成 immutable catalog、static lookup、pure glue、validation、Baker / Bootstrap glue | 生成 `ISystem`、`OnUpdate`、runtime lifecycle、hidden query、hidden ECB、system registration 或 NativeContainer owner |

### Module depth / deletion test 判定补充

目标态 Module 必须是深 Module：Interface 足够窄，Implementation 足够厚，复杂度具备 locality。评审时不能只看文件夹、namespace、class 名或 facade 名称，而要按调用方必须理解多少实现细节来判定。

| 判定项 | 合格形态 | 不合格形态 |
|---|---|---|
| Interface depth | 调用方只理解业务 intent、opaque handle、record、snapshot、evidence 和错误码 | 调用方必须理解 query、lookup、allocator、dependency、carrier、capacity、merge 或 structural playback |
| Locality | 修改某个 lane / store / capability 时，知识、bug 和验证集中在单一 owner 内 | 修改一个语义需要同时改 Shell、generated artifact、Debugger、Demo adapter 和多个 helper |
| Deletion test | 删除某个 Module 时，复杂度应集中回一个 owner，证明该 Module 真正在隐藏实现 | 删除后复杂度只是不变地散落到多个调用方，说明它只是 pass-through 或浅 facade |
| DOTS API owner | `ISystem` / job 显式拥有 query、type handle、lookup refresh、allocator、dependency、carrier 和 evidence counter | Shell、SourceGenerator、Debugger、static helper 或 Adapter 隐式拥有 Runtime Core DOTS API |
| Diagnostics locality | observation、official capture、validation evidence 和 derived export 有独立 cost domain 和 reader capability | Debugger 与 command / snapshot / gameplay decision 共享同一个 seam，导致成本和权限不可归因 |

## 目标态模块 Owner Map

目标态不是在 Runtime 外再包一层更大的 OOP facade，而是把每个 capability、lane、store 和 evidence owner 切成独立 Module。每个 Module 的 Interface 必须足够深：调用方只理解业务意图、record、snapshot 或 evidence，不理解 query、lookup、allocator、dependency、capacity、merge 和 structural playback 的实现细节。

| Owner Module | Interface | Implementation ownership | 禁止泄露 |
|---|---|---|---|
| `RuntimeSession` | install / dispose / fixed tick result | World、SystemGroup、catalog lifetime、tick source、dependency drain evidence | `World`、`EntityManager`、live system group |
| `RunnerSync` | fixed tick request / dependency drain reason / timing split | tick source、dependency drain、sync reason、runner budget、measurement fence | command writer、snapshot reader、gameplay decision |
| `DefinitionCatalogLifetime` | catalog handle / schema version / install-dispose token | Blob lifetime、schema validation、bootstrap install、dispose route | managed row、runtime lifecycle system、system registration |
| `CommandPort` | ability / GE / destroy request id | owner-local command buffer、sequence、pending marker、request validation | raw `Entity`、writable buffer、同步 gameplay 结果 |
| `SnapshotReadModel` | immutable ASC / battle / presentation snapshot | BoundaryProjection、snapshot ring、cursor、version、compaction | live `DynamicBuffer`、`EntityQuery`、runtime singleton |
| `GASFrameKernel` | frame-local command / target / spec / delta / fact records | lane system、query owner、lookup refresh、job chain、frame allocator | OOP manager、global facade、hidden query |
| `EffectFanInStore` | deterministic command/spec merge result | `NativeStream` segment、sort key、merge cost、owner range | singleton DynamicBuffer as scale-ready default |
| `ActiveEffectStore` | owner-local slot / lifecycle record | slot capacity、period/stack/state flags、cleanup intent、skip evidence | static helper lifecycle、generated lifecycle owner |
| `StructuralCommit` | structural intent / playback evidence | ECB owner、bulk query、sort key、Journaling / Profiler route | scattered create / destroy / add / remove |
| `DiagnosticsSink` | evidence snapshot / official capture state | counters、TopN、buffer pressure、API health、derived export source | gameplay decision、hot path string log |
| `GeneratedDefinitionGlue` | code -> index -> immutable definition -> pure record | Blob schema、static lookup、unmanaged resolver、validation artifact | `ISystem`、`OnUpdate`、ECB、query、NativeContainer owner |
| `PresentationBridge` | presentation outbox / cue marker / replay marker | Boundary fact projection、resource binding key、headless marker | Core gameplay write、resource dependency inside Core |

### Runtime lifecycle owner 判定

目标态中，Ability、GameplayEffect、ActiveEffect、Attribute、Tag、Cue 和 GameplayFact 的 lifecycle owner 必须是手写 ECS Core lane 或明确 store owner，不得由 OOP Shell、SourceGenerator、Debugger、Presentation 或 static helper 代管。

| Lifecycle domain | 目标 owner | 允许的 generated 输入 | 禁止形态 |
|---|---|---|---|
| Ability activation / commit / cancel / end | `AbilityLifecycleLane` + owner-local ability slot / request buffer | ability plan record、cost / cooldown GE code、target rule lookup、pure requirement evaluator | generated `ISystem`、OOP action object、Shell 同步结算、Debugger 反写 |
| Instant GE spec / delta / fact | `EffectSpecLane` + `AttributeDeltaLane` + `GameplayFactLane` | GE seed、modifier record、magnitude evaluator、tag requirement evaluator | runtime GE entity churn、singleton stream 终局、generated delta apply lifecycle |
| ActiveEffect period / stack / duration / grant cleanup | `ActiveEffectStore` + `ActiveEffectLifecycleLane` | active slot seed、stack policy、duration policy、generated pure magnitude glue | static helper lifecycle、generated `OnUpdate`、global index 驱动状态 |
| Tag / requirement / immunity | `TagStateLane` + owner-local tag snapshot / query evaluator | immutable tag taxonomy、requirement range、pure all/any/none evaluator | managed tag registry、runtime string query、per-definition entity query |
| Cue / Presentation | `PresentationBridge` + Boundary outbox | cue code、cue parameters、presentation binding metadata | Core 直接加载资源、Cue managed lifecycle 写 gameplay state |

该判定的核心不是“生成物是否能编译”，而是 lifecycle 的 query、lookup refresh、dependency、allocator、carrier、structural policy 和 evidence counter 是否归属于手写 Core owner。若 generated artifact 只输出 pure record / lookup / evaluator，它属于 `GeneratedDefinitionGlue`；若它拥有 `OnUpdate`、query、ECB 或 NativeContainer lifecycle，则目标态失败。

### Owner Map 不变量

1. `RuntimeSession` 可以拥有 ECS runtime implementation，但 public seam 只能输出 session id、tick result、timing split 和 evidence。
2. `CommandPort` 与 `SnapshotReadModel` 不共享同一个 public ECS handle；写能力和读能力必须可独立授权、独立计时、独立验证。
3. `GASFrameKernel` 不调用 Shell / Adapter。它只消费 ECS data、Blob、frame-local record 和 owner-local store。
4. `EffectFanInStore` 与 `ActiveEffectStore` 是不同 Module：前者解决本帧确定性 fan-in，后者解决跨帧 GE 生命周期状态。
5. `DiagnosticsSink` 只能从 facts / counters / official capture 派生输出；Mermaid、字符串日志、战报和 UI 都是 derived export。
6. `GeneratedDefinitionGlue` 不能成为 runtime lifecycle owner。需要扩展语义时，先扩 hand-written Core lane，再让 generator 输出 pure record / lookup。
7. `PresentationBridge` 可以是 managed / OOP / GameObject 边界，但它只能消费 Boundary outbox，不回写 gameplay state。
8. Diagnostics owner discovery 必须来自 `RuntimeSession` bootstrap 注册的 diagnostics owner 或显式 diagnostics capability；禁止为了查找 Debugger / diagnostics singleton 在运行期扫描 World 或创建 fallback query。
9. Debugger observation materialization 只能属于 diagnostics pass 或 bounded Boundary evidence export；performance pass 必须能禁用、隔离或单独计量这类 observation 成本，不能把它混入 CoreSimulation tick 结论。
10. Bootstrap / Session 可以在实现内部创建 World、SystemGroup、singleton owner 和 generated catalog，但这些都不是 Application Shell interface 的一部分。
11. 任一 capability 的 public seam 必须只暴露该 capability 的业务词汇。Command capability 不返回 snapshot；Snapshot capability 不写 command；Diagnostics capability 不泄露 command writer；Runner sync capability 不成为业务 API。
12. Runtime Core lane owner 必须拥有自己的 query、type handle、lookup refresh、allocator、carrier、dependency 和 evidence counter；禁止把这些实现细节藏进 Shell、SourceGenerator、Debugger 或 static helper。
13. 目标代码骨架必须能执行 deletion test：删掉某个 Module 时，复杂度应集中回一个 owner，而不是散落到 Shell、generated artifact、Debugger 和 Demo adapter 多处。
14. `RunnerSync` 只处理 tick 驱动、dependency drain reason 和 timing split；它不得承载业务 command、snapshot read 或 diagnostics export public seam。
15. `DefinitionCatalogLifetime` 只处理 immutable catalog 的 install、schema/version 校验和 dispose；它不得成为 generated lifecycle、runtime registration 或 managed row 访问通道。
16. 目标态 dependency graph 必须能证明 Shell / Adapter、Runtime Core、Diagnostics、Presentation、Definition Glue 之间没有反向依赖环；目录名不能替代 Module owner 证明。

## 目标态职责重划分验收表

以下表格只描述理想架构应如何判定，不记录当前代码做到哪里。

| 目标 owner | 必须拥有 | 不得拥有 | 验收证据 |
|---|---|---|---|
| `RuntimeSession` | World lifetime、SystemGroup install、catalog install/dispose、fixed tick runner、timing split | gameplay formula、UI / Demo policy、Debugger 字符串导出 | install / tick / dispose API 不返回 ECS handle；tick result 拆 Core / Boundary / Diagnostics / Runner |
| `RunnerSync` | tick source、dependency drain reason、sync fence、runner timing split | command write、snapshot read、业务计算、Debugger 导出格式 | drain reason、sync count、runner budget、Core / Boundary / Diagnostics 分域耗时 |
| `DefinitionCatalogLifetime` | Blob catalog install、schema/version check、catalog handle、dispose owner | managed config row、runtime lifecycle system、generated registration | catalog version、install result、dispose proof、hot path builder 命中为 0 |
| `CommandPort` | intent validation、opaque handle 解析、command sequence、owner-local command append | snapshot read、damage calculation、cooldown / cost 立即结算 | command count、reject reason、owner command pressure；外部只拿 request id |
| `SnapshotReadModel` | BoundaryProjection snapshot ring、version cursor、report identity projection | live buffer view、`EntityQuery`、直接读 Core component | snapshot version、staleness、projection cost；外部只拿 immutable snapshot |
| `GASFrameKernel` | lane query、job chain、frame allocator、dependency ownership、carrier ownership | OOP callback、GameObject / MonoBehaviour、generated lifecycle owner | 每个 lane 的 data nature、query contract、allocator owner、Burst / job evidence |
| `EffectFanInStore` | transient command/spec merge、sort key、merge budget、spill / segment counters | singleton DynamicBuffer scale-ready 默认、managed list hot merge | `NativeStream` segment count、deterministic merge order、merge cost、reselect trigger |
| `ActiveEffectStore` | owner-local slot、period / stack / granted state、cleanup intent、capacity policy | static helper lifecycle、global index 反向驱动 state | slot count / capacity / externalized ratio、idle skip、cleanup fact |
| `StructuralCommit` | structural intent、ECB owner、playback phase、official diff route | 分散 `EntityManager` create / destroy / add / remove | structural source TopN、playback count、Journaling / Profiler state |
| `DiagnosticsSink` | runtime counter、official capture state、validation evidence、derived export source | gameplay decision、hot path string 拼接、Core 反写 | evidence tier 分层、observation materialization timing、drop / disabled reason |
| `GeneratedDefinitionGlue` | Blob schema、code -> index lookup、static switch / pure evaluator、validation artifact | `ISystem`、`OnUpdate`、query、ECB、NativeContainer owner、runtime registration | generated artifact static scan、schema version、pure resolver tests |
| `PresentationBridge` | cue / outbox / replay / resource binding marker | Core gameplay write、真实资源依赖进入 Core | headless marker 与 rendered profile 同源，Presentation cost 独立计时 |

## 目标态代码骨架判定准则

本组 Spec 中的完整代码骨架必须按职责阅读，而不是按文件名或语言特性阅读：

1. Interface 层只出现 session、opaque handle、request id、snapshot version、evidence snapshot，不出现 `World`、`EntityManager`、raw `Entity`、`EntityQuery` 或 writable `DynamicBuffer`。
2. Boundary implementation 可以短暂解析 raw ECS identity，但解析结果必须立即写成 command record 或 snapshot record，不能继续向 Shell 外泄。
3. Core system / job 代码必须说明 query owner、enableable mask 处理、carrier owner、dependency 串联和 allocator / dispose 位置；缺任一项时，代码只能算 proof skeleton。
4. Fan-in 代码必须同时说明并行写入和确定性 merge。只使用 `NativeStream` 但没有 sort key / merge policy / budget counter，不算 scale-ready。
5. Debugger 代码只能读取 counters / facts / official capture state 并写 evidence snapshot。字符串、Mermaid、战报、UI 是 derived export，不是 evidence source。
6. SourceGenerator 代码只能从 immutable definition 生成 unmanaged record / pure evaluator / validation metadata。若出现 lifecycle system、query、ECB、NativeContainer owner 或 registration 逻辑，目标态判定失败。
