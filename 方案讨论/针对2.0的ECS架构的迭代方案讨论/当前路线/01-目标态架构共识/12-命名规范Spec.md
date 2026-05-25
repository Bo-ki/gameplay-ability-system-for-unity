# 命名规范 Spec

## 目的

建立 EX-GAS 2.0 的职责命名规范，避免类名、文件名、任务名继续使用“Facade / Adapter / Helper / Manager / EventBus / Scenario”等模糊词承载多重职责。命名必须让 Agent 在领取任务时直接判断对象所属层级、读写方向和允许行为。

## 核心原则

1. 名称先表达职责，再表达实现技术。
2. 一个类型名只能对应一个边界职责；如果名称需要用 `And`、`Or`、`Manager`、`Helper` 才能解释，通常说明需要拆分。
3. 类型名必须能回答三件事：属于哪一层、读还是写、是否拥有状态。
4. Runtime Core 热路径命名优先表达数据流：`Command`、`Spec`、`Delta`、`Store`、`Fact`、`Projector`。
5. 边界层命名优先表达方向：`Gateway` 写入 Core，`ReadModel` 读取 Core，`Bridge` 做跨边界执行，`Sink` 只写外部导出。

## 层级命名约束

| 层级 | 推荐命名 | 禁止误用 |
|---|---|---|
| Application Shell Layer | `*Shell`、`*Presenter`、`*Controller`、`*Runner`、`*SceneInstaller`、`*ResourceBinding` | 不使用 `*System` 表示 OOP 业务对象，不暴露 `EntityManager` |
| Runtime Boundary Layer | `*CommandGateway`、`*ReadModel`、`*OutboxBridge`、`*DiagnosticsSink`、`*ReplaySink`、`*BoundarySystem` | 不用泛化 `*Adapter` 承载业务计算、缓存、日志和事件分发 |
| GAS Runtime Core Layer | `C*`、`B*`、`S*`、`*Command`、`*Spec`、`*Delta`、`*Store`、`*Fact`、`*Resolver`、`*Projector` | 不使用 `Manager`、托管 `Helper`、`Facade`、真实资源名 |
| Definition & Generation Layer | `*Definition`、`*DefinitionRow`、`*DefinitionTable`、`*GeneratedIds`、`*StaticLookup`、`*BakePlan`、`*ValidationReport` | 不使用 `Runtime` 命名承载生成产物，不生成 `*LifecycleSystem` |

## 后缀职责字典

| 后缀 | 允许职责 | 禁止职责 | 示例 |
|---|---|---|---|
| `Facade` | 面向应用壳层的窄 API 聚合，只做语义便捷入口 | 不持有权威状态，不做 gameplay 计算，不混合 read/write/cache/log | `AbilitySystemFacade` 后续应收缩为壳层 API 或拆成 `GASCommandGateway` + `GASReadModel` |
| `Gateway` | 跨层写入入口，把外部意图转换为 command/request | 不读事实、不分发表现、不缓存业务状态 | `GASCommandGateway` |
| `ReadModel` | 面向外层的只读镜像 | 不写 ECS，不返回可写 buffer，不暴露 `EntityManager` | `GASUnitReadModel` |
| `Bridge` | 跨技术边界执行副作用，如 ECS facts 到 UI/Cue/VFX/SFX marker | 不计算 gameplay 结果 | `GameplayCueOutboxBridge` |
| `Sink` | 只写外部导出目标，如日志、Replay、诊断文件 | 不作为 Runtime Core 输入 | `RuntimeDiagnosticsSink` |
| `Projector` | 把 Core 数据投影为 fact、read model 或 outbox | 不拥有生命周期，不做 command 写入 | `AttributeDeltaProjector` |
| `Store` | Runtime Core 内稳定状态容器 | 不表示静态注册表，不访问 OOP | `ActiveEffectStore` |
| `Registry` | 静态定义注册和查找 | 不保存 runtime state | `GameplayEffectDefinitionRegistry` |
| `Resolver` | 纯计算解析，输入明确，输出明确 | 不执行结构变化，不调用外部资源 | `ModifierMagnitudeResolver` |
| `Builder` | 构建 command、spec、blob、report | 不拥有运行时生命周期 | `GameplayEffectSpecBuilder` |
| `Runner` | 测试或场景执行入口 | 不承载业务规则巨类 | `AutoChessHeadlessRunner` |
| `Scenario` | 场景数据或测试编排描述 | 不成为配置、逻辑、表现、报告混合容器 | `AutoChessScenarioDefinition` |

## 限制词

### `Manager`

默认禁止。只有管理 Unity 资源生命周期、Editor window 生命周期或进程级服务生命周期时才允许使用。Runtime Core 内不得出现 `Manager` 作为业务权威对象。

当前 `GASManager` 更接近 `GASRuntimeWorldHost` / `GASRuntimeBootstrap` / `GASRuntimeServiceLocator` 的混合职责；后续需要按职责拆分，而不是继续扩大 `Manager`。

### `Helper`

默认禁止作为新增 Runtime 类型后缀。允许保留纯函数、小范围、无状态、无结构变化、无外部副作用的工具，但名称应尽量替换成具体职责。

示例重命名方向：

| 当前命名 | 问题 | 目标命名方向 |
|---|---|---|
| `EventBusHelper` | 同时表达 metadata、sequence、append、batch capability，`Helper` 掩盖写入方向 | `GameplayFactStreamWriter` / `GameplayFactAppendService` |
| `AttributeHelper` | 包含属性查找、重算、modifier 应用，容易进入 Core 热路径 | `AttributeValueResolver` + `AttributeDeltaProjector` |
| `CueHelper` | 真实职责是 Cue 类型注册、实例创建和 presentation bridge 执行细节 | `GameplayCueFactory` + `GameplayCueOutboxBridge` |
| `EntityHelper` | 当前主要是 GameObject presentation binding 和调试取名 | `PresentationEntityBindingRegistry` |

### `Adapter`

只允许表示协议或数据形态转换。不得同时负责 command 写入、结果分发、read model、debug log 和业务计算。历史方案中的 `Thin Adapter` 在本路线中统一收敛为 `Runtime Boundary Layer`，具体类型优先使用 `Gateway / ReadModel / Bridge / Sink`。

### `EventBus`

不允许作为实时 simulation 业务中心。Core 内可以存在 fact stream / transient event buffer，但命名必须说明流向和用途。

推荐：

1. Core 输出：`GameplayFactStream`、`AttributeDeltaStream`、`PresentationMarkerOutbox`。
2. 边界读取：`GameplayFactReadCursor`、`PresentationOutboxBridge`。
3. 外部分发：`ApplicationEventDispatcher`，仅属于 Application Shell Layer。

### `Debugger` / `Logger`

只能观察、采样、导出和可视化。不得参与 gameplay routing，不得作为 Core 输入源。Runtime Core Debugger 的内部对象应优先命名为 `DiagnosticsCounter`、`TraceEvent`、`TimingSample`、`BufferPressureSample`、`ReplayFrame`。

## 当前命名问题样本

1. `AbilitySystemFacade` 位于 `Assets/GAS/Runtime/AbilitySystem/AbilitySystemFacade.cs:7-27`，同时暴露 Entity、GameObject 和 Observation，后续应拆成应用壳层 API、CommandGateway 和 ReadModel。
2. `EventBusHelper` 位于 `Assets/GAS/Runtime/Event/EventBusHelper.cs:7-45`，实际职责是 fact append / sequence / batch capability，不应继续以 Helper 掩盖边界写入语义。
3. `GASManager` 位于 `Assets/GAS/Runtime/General/GASManager.cs:6-36`，承担 world host、entity manager、global singleton、bootstrap 多种职责，命名过宽。
4. `HeadlessAutoChessScenario` 位于 `Assets/GAS/Runtime/Demo/AutoChess/HeadlessAutoChessScenario.cs:12-45`，当前文件还承担配置、执行、报告、压力参数等职责，后续 AutoChessDemo 重构应拆为 ScenarioDefinition、Runner、ValidationReport、ScaleProfile。
5. 历史方案中 `ShadowMark` “实为 Debuff 却放 Buff 命名空间”是配置命名与职责不符的典型反例：`../历史方案参考/方案14.md:1251-1258`。

## 任务命名规范

任务名采用“主线名 - 支线名 - 任务名”的中文职责拼接，不使用抽象代号作为主名称。代号只允许作为附加追踪字段。

示例：

| 旧风格 | 新风格 |
|---|---|
| `T6-REAL-B-DemoMigration` | `Runtime验收Demo - AutoChessDemo架构迁移 - Runtime目录迁出` |
| `T4-Debugger-AM1` | `Observation与Debugger - RuntimeCore诊断基线 - SystemTiming与BufferPressure采样` |
| `T1-RuntimeCore-AM0` | `GAS Runtime Core - 结构变化边界 - 禁止跨结构变化持有DynamicBuffer` |

## 验收方式

1. 新增类型必须能从名称判断所属四层和读写方向。
2. 新增 Runtime Core 类型不得使用 `Manager`、`Facade`、泛化 `Helper`、真实 UI/VFX/SFX 资源名。
3. 新增边界层类型不得用 `Adapter` 作为万能名，必须在 `Gateway / ReadModel / Bridge / Sink / Projector` 中选择或补充等价精确后缀。
4. 任务树主线、支线、任务节点都必须带目标、Spec 引用、历史方案定位、验收标准和测试链路。

## 历史方案定位

1. 方案15 中 `UnitFacade / GASAdapter / GASEventBus / GASDebugger` 的示例提供了四层原始雏形，但本 Spec 将其拆成更精确的 Gateway、ReadModel、Bridge、Sink：`../历史方案参考/方案15.md:53-88`。
2. 方案15 对适配层“翻译而非计算”的描述是 Runtime Boundary 命名约束的来源：`../历史方案参考/方案15.md:473-490`。
3. 方案15 对 `AbilityLogicBase` 与 `AbilityBehaviorSystem` 的对比说明命名应反映 Burst ECS 行为归属：`../历史方案参考/方案15.md:1302-1315`。
4. 方案14 的职责最终定义强调 Core 禁止 OOP 反查，支持本 Spec 对 Core 命名和边界命名的约束：`../历史方案参考/方案14.md:1038-1058`。
