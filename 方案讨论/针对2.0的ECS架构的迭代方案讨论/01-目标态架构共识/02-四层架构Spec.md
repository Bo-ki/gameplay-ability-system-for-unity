# 四层架构 Spec

## 目的

把 EX-GAS 2.0 的目标态主架构从旧“五平面”口径重构为四层工程架构，并给后续 Runtime Core、配置生成链、Debugger、AutoChess 验收 Demo 和任务树拆分提供统一边界。

旧五平面只能作为历史解释词汇，不再作为主 Spec。后续设计、任务领取和代码命名都应优先使用本文件的四层名称。

## 四层命名

| 层级 | 规范英文名 | 规范中文名 | 旧口径吸收 | 核心职责 |
|---|---|---|---|---|
| Layer 1 | Application Shell Layer | 应用壳层 | Extension / Presentation consumer | UI、输入、AI、网络、场景编排、Demo runner、真实资源和无头 log 占位 |
| Layer 2 | Runtime Boundary Layer | 运行时边界层 | Thin Adapter / Observation / Debugger | OOP/ECS 边界、command gateway、read model、presentation outbox、diagnostics/replay sink |
| Layer 3 | GAS Runtime Core Layer | GAS 运行时核心层 | Simulation | ASC、Ability、GameplayEffect、Attribute、Tag、Target、EffectCommand、StateEvaluate、AttributeReduceApply、GameplayFact |
| Layer 4 | Definition & Generation Layer | 定义与生成层 | Authoring / Definition / Generated | Luban source、schema、generated id、static lookup、Blob/Bake plan、validation summary |

层级编号沿用历史方案的表达习惯，不代表调用方向。目标态调用方向只有两条：应用壳层通过运行时边界层写入命令，GAS Runtime Core 只消费定义与生成层的不可变输入并输出事实。

## 总体数据流

```mermaid
flowchart TB
    Definition["Layer 4: Definition & Generation\nLuban / SourceGenerator / Static Lookup / Bake Plan"]
    Core["Layer 3: GAS Runtime Core\nASC / Ability / Effect / Attribute / Tag / GameplayFact"]
    Boundary["Layer 2: Runtime Boundary\nCommandGateway / ReadModel / PresentationOutboxBridge / DiagnosticsSink"]
    Shell["Layer 1: Application Shell\nUI / AI / Input / Network / Demo Runner / Resource Binding"]

    Definition -->|"immutable definitions"| Core
    Shell -->|"intent, no EntityManager"| Boundary
    Boundary -->|"request entity / command stream"| Core
    Core -->|"typed facts / deltas / diagnostics facts"| Boundary
    Boundary -->|"read model / markers / replay / logs"| Shell
```

## Unity Entities 校准

四层架构只是工程边界，不是自定义 ECS runtime。Layer 3 和 Layer 4 的实现必须遵守 `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`：

1. Layer 3 的物理执行域必须映射到 Unity `ComponentSystemGroup` 和 update order；业务 kernel 只作为 lane system / job chain，不默认新增 group。
2. Layer 3 hot path 默认使用 unmanaged `ISystem` 和 job，不依赖 managed object。
3. Layer 3 结构变化集中到 `GASStructuralCommitSystemGroup`，不在 Target Resolve / Effect Fan-In / State Evaluate / Attribute Apply / Gameplay Fact kernel 直接 create / destroy entity。
4. Layer 4 的 generated artifact 优先落为 Blob、Baker output、static lookup 和 validation graph。
5. Layer 2 / Layer 1 可以有 managed bridge，但不能把 managed bridge 写回 Runtime Core 热路径。

## Layer 4: Definition & Generation Layer

### 职责

1. 维护 Luban Excel / JSON / schema / validation rule。
2. 生成稳定 id、静态 lookup、definition snapshot、bake plan 和 runtime integration plan。
3. 为 Runtime Core 提供不可变、可校验、可 Burst 消费的定义输入。
4. 维护生成链路的显式 pipeline、RowMetadata、Phase manifest、路径审计和 orphan generated file 清理策略。

### 禁止

1. 不生成 Ability / GameplayEffect lifecycle system。
2. 不携带 runtime state、spec、context、cooldown instance、active effect instance。
3. 不依赖 GameObject、MonoBehaviour、Editor window、Odin、XLua 或业务 UI。
4. 不把 Luban managed row、`cfg.*`、`XLuban`、`SimpleJSON` 或 JSON reader 暴露给 Runtime Core assembly。
5. 不用托管数组、managed dictionary 或可变 delegate registry 作为 Runtime Core hot path lookup。

## Layer 3: GAS Runtime Core Layer

### 职责

1. 承载 GAS 权威状态和 gameplay 规则，所有热路径以 unmanaged component、buffer、blob、lookup、command stream、typed fact 表达。
2. 以显式 DOTS kernel 处理 `Boundary Command Ingest -> Target Resolve -> Effect Fan-In -> State Evaluate -> Attribute Reduce/Apply -> Gameplay Fact -> Structural Commit -> Boundary Projection`。
3. 将 Cue、UI、VFX、SFX、FloatingText、Debugger 需要的信息输出为只读 facts / outbox marker。
4. 将 `EffectCommand / Spec / Delta / Fact` 作为语义链，而不是全局 singleton 总线；scale-ready fan-in 默认走 `NativeStream` deterministic merge + compact owner-local buffer。

### 禁止

1. 不持有托管业务对象，不调用 `GameObject`、`MonoBehaviour`、真实 UI/VFX/SFX 资源。
2. 不把全局 `EventBus` 当业务 reaction 主输入，不用 observation stream 反向驱动 simulation。
3. 不跨结构变化持有 `DynamicBuffer` / `ComponentLookup` / `BufferTypeHandle` 的旧句柄。
4. 不把 Frame Arena 写成 query registry / service locator；EntityQuery 归属各 `ISystem`。
5. 不把大容量 singleton DynamicBuffer 或大容量 per-ASC frame buffer 固化为目标态。

## Layer 2: Runtime Boundary Layer

### 职责

1. `CommandGateway`：把应用壳层意图转换为 request entity、command buffer 或等价 command data。
2. `ReadModel`：提供只读镜像，不暴露 `EntityManager`、`EntityQuery`、runtime buffer 可写句柄。
3. `PresentationOutboxBridge`：消费 Core facts，输出 UI/Cue/VFX/SFX/log marker；无头 Demo 也必须走同一 outbox 语义。
4. `DiagnosticsSink` / `ReplaySink`：导出结构化日志、timing、buffer pressure、fact count、scale profile，不参与 gameplay routing。

### 禁止

1. 不做伤害、治疗、Tag requirement、Cooldown、Cost、Stack、Period 等 gameplay 计算。
2. 不把 `Adapter` 写成“边界 + 缓存 + 业务反应 + 调试日志”的混合对象。
3. 不把 `Debugger` / `Logger` / `Replay` 作为 Runtime Core 的实时输入。

## Layer 1: Application Shell Layer

### 职责

1. 面向产品、Demo、自动验收和真实资源接入，组织 UI、输入、AI、网络、场景、MonoBehaviour、Prefab、Timeline 等外部模块。
2. 调用运行时边界层 API 发起意图，订阅 read model、presentation marker、structured log。
3. AutoChess 无头验收 Demo 虽然没有真实画面资源，也必须完整保留 UI/Cue/VFX/SFX/FloatingText marker 逻辑。

### 禁止

1. 不直接写 Runtime Core component / buffer。
2. 不持有 GAS 权威状态副本。
3. 不通过 log/replay 反向修正 gameplay 结果。

## 旧五平面到四层的吸收关系

| 旧术语 | 新归属 | 说明 |
|---|---|---|
| Authoring Plane | Definition & Generation Layer | Authoring 是定义与生成层的输入面，不再单独作为主层 |
| Definition Plane | Definition & Generation Layer | 继续保留“定义数据不可变”的约束 |
| Simulation Plane | GAS Runtime Core Layer | 统一改称 Runtime Core，强调 GAS 语义和 ECS 热路径 |
| Observation Plane | Runtime Boundary Layer | Observation 拆成 read model、presentation outbox、diagnostics/replay sink |
| Extension Plane | Application Shell Layer | Extension 是外部应用壳层，不直接进入 Runtime Core |

## 验收方式

1. 文档验收：目标态 Spec 和任务树不再以“五平面”作为主架构入口；引用旧术语时必须标注为旧口径。
2. Runtime 验收：Core hot path 不依赖 Editor / GameObject / Odin / managed gameplay object；system tick 目标保持 `0.0X - 0.X ms` 级别。
3. 边界验收：CommandGateway 只写命令，ReadModel 只读，PresentationOutboxBridge / Diagnostics / Replay 不反向写 simulation。
4. 配置验收：Luban + SourceGenerator 只输出 Definition & Generation Layer artifact，不生成 runtime lifecycle。
5. Demo 验收：AutoChessDemo 位于 Runtime Core 外部，走真实业务流程，同时支持无头自动结算和十万级以上压力测试预演。

## 历史方案定位

1. 四层原始模型来自 `../历史方案参考/方案15.md:35-50`，但本 Spec 将 `业务层 / 适配层 / ECS核心层 / 数据配置层` 重新命名为更精确的四层工程名。
2. 方案15 的 Layer 2 示例强调“接收 OOP 命令、转换为 ECS 标记/组件、不包含业务逻辑”，对应本 Spec 的 Runtime Boundary Layer：`../历史方案参考/方案15.md:473-490`。
3. 方案15 对四层架构与旧架构的对比指出 OOP 层必须退回外壳层、Ability 行为必须进入 Burst System：`../历史方案参考/方案15.md:1289-1315`。
4. 方案15 对边界单向性的总结可作为应用壳层、运行时边界层和 Runtime Core 的验收图：`../历史方案参考/方案15.md:2843-2857`。
5. 方案14 的四层职责总览和 Luban + SourceGenerator 生成链路提供了 Definition & Generation Layer 的来源参考：`../历史方案参考/方案14.md:100-118`。
6. 方案14 的“架构分层职责最终定义”提供了 Core 全 unmanaged、全 Burst、禁止反向查 OOP 的硬约束：`../历史方案参考/方案14.md:1038-1058`。
