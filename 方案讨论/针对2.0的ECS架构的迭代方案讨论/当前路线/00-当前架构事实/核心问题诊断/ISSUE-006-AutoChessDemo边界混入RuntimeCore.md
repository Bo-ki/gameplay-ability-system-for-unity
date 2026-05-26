# ISSUE-006 AutoChess Demo 边界混入 Runtime Core

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active（部分缓解） |
| 严重度 | P1 |
| 最近复核 | 2026-05-24 |
| 所属层 | Application Shell Layer / Runtime Boundary Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `SYS-01` | 权威计算落在 ECS System/Job 数据流，禁止托管 manager 驱动 | AutoChess 不应混入 Runtime Core 目录和 asmdef |
| `SYS-05` | World 边界：Debugger/Demo/Presentation 只能通过 Boundary 观察 Core | Demo 属于 Application Shell + Boundary，非 Runtime Core |
| `ODF-18` | 无头 Demo 只省略真实资源，不省略 Cue/UI/VFX/SFX 的 Boundary 链路 | 无头验收应保留表现链路，只替换 side effect 为结构化日志 |
| `CASE-17` | ICustomBootstrap 多世界 | AutoChess headless validation 应使用独立 world 或 ICustomBootstrap |
| `DBG-01` | Debugger 不驱动 Runtime Core | Scenario 的 validation report 不应混入 Runtime Core 目录 |
| `PRF-16` | System 创建依赖用 CreateAfter；ISystem 优于 SystemBase | Demo 系统不应注册到 Core GAS groups |

## 问题陈述

AutoChess 验收 Demo 的目标是自动化全链路验收，不是 Runtime Core 内部样例。目录层面的 Runtime Core 混入已在本轮缓解：AutoChess 代码已迁移到 `Assets/AutoChessDemo`，并新增独立 asmdef / bootstrap；文件也已按 Config / Simulation / Observation / Presentation / Validation / Debugging 完成第一层归位。`HeadlessAutoChessScenario` 已开始拆出 constants / state / bootstrap / variants / unit definitions / unit resolution / runtime timing / runtime lifecycle / types，但 generated rows、unit bootstrap、validation report、event/outbox counting、presentation marker 等职责仍在若干大文件内部混杂，内部拆分尚未完成。

## 当前证据

已缓解证据：

1. AutoChess 根目录已迁移到 `Assets/AutoChessDemo`。
2. `Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef` 依赖 Runtime Core，Runtime Core asmdef 不依赖 AutoChessDemo。
3. `GASSystemScheduleContract.cs` 已移除 `SHeadlessAutoChess*` 系统类型。
4. `GASRuntimeQueryLayoutPlan.cs` 已移除 `SHeadlessAutoChessPresentationCueMarkerProjection`。
5. `HeadlessAutoChessRuntimeSystemBootstrap` 由 Demo 自己注册业务系统。
6. 静态扫描 `Assets/GAS/Runtime` 已确认无 AutoChess 直接源码引用。
7. AutoChessDemo 文件级目录已拆分：`Config`、`Config/Generated`、`Simulation`、`Observation`、`Presentation`、`Presentation/Scenes`、`Validation`、`Debugging`。
8. `HeadlessAutoChessScenario` 已拆为 partial runner：
   - `HeadlessAutoChessScenarioConstants.cs`
   - `HeadlessAutoChessScenarioState.cs`
   - `HeadlessAutoChessScenarioBootstrap.cs`
   - `HeadlessAutoChessScenarioVariants.cs`
   - `HeadlessAutoChessScenarioUnitDefinitions.cs`
   - `HeadlessAutoChessScenarioUnitResolution.cs`
   - `HeadlessAutoChessScenarioRuntimeTiming.cs`
   - `HeadlessAutoChessScenarioRuntimeLifecycle.cs`
   - `HeadlessAutoChessScenarioTypes.cs`

仍存在证据：

1. `Validation/HeadlessAutoChessScenario.cs` 仍承担 runner、unit bootstrap、validation report、event counting 和 presentation outbox accumulation 等多种职责。
2. `Config/Generated/HeadlessAutoChessGeneratedDefinitionRows.cs` 仍承载大体量 generated rows / source generator 样板。
3. Unity 编译、默认 headless validation 和 SceneRuntime runner 尚未通过当前环境验证；历史阻塞是本机 Unity batchmode license，本轮先遇到项目已被 Unity Editor 打开，随后最新阻塞为 LicensingClient IPC 超时。

## 执行路径

当前路径：

```text
Runtime Core package
-> Core GAS groups / core systems only
-> AutoChessDemo validation scenario
-> HeadlessAutoChessRuntimeSystemBootstrap registers AutoChess systems into GAS groups
-> Scenario 创建单位、注册配置、运行战斗、采集 timing、导出日志
```

## 影响

1. Runtime Core 与 AutoChess Demo 的 assembly / schedule 反向依赖已解除，后续 Core 重构不再必须编译 Demo 业务系统。
2. AutoChessDemo 已具备文件级层级边界，Scenario 巨类内部拆分已开始，但 report / event-outbox counting / unit bootstrap / generated rows 等关键职责仍需继续分解，继续拆分前仍难以作为真实业务 Demo 样板。
3. 无头验收和 SceneRuntime runner 的链路已迁移到新根目录与 `Presentation/Scenes`，但 Unity 编译和场景运行尚需补验证。

## 根因反推

AutoChessDemo 应属于 Application Shell Layer + Runtime Boundary Layer 的验收样板，不能放在 GAS Runtime Core Layer 内。无头不是删掉 UI/VFX/SFX/Cue，而是通过 outbox/log marker 保留真实表现链路。

## 目标态入口

1. `../../01-目标态架构共识/10-AutoChess无头验收Spec.md`
2. `../../01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
3. `../../01-目标态架构共识/02-四层架构Spec.md`

## 任务入口

`../../02-主线任务树/T6-RuntimeValidationDemo/AutoChess无头验收/README.md`

## 退出条件

1. `Assets/AutoChessDemo` 成为 Demo 根目录。已完成。
2. Runtime Core 不依赖 AutoChessDemo，AutoChessDemo 只通过公开 Runtime Boundary / Definition contract 使用 GAS。已完成静态边界拆除，待 Unity 编译验证。
3. 文件级目录拆为 Config / Simulation / Observation / Presentation / Validation / Debugging。已完成。
4. `Scenario` 巨类拆为 ScenarioDefinition、Runner、Bootstrap、ValidationReport、ScaleProfile、PresentationOutboxBridge 等明确职责。部分完成：constants / state / bootstrap / variants / unit definitions / unit resolution / runtime timing / runtime lifecycle / types 已拆出；unit bootstrap / report builder / event-outbox counting 仍未完成。
