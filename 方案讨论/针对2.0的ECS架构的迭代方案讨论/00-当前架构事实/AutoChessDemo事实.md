# AutoChessDemo 实现事实

> 上次更新：2026-06-06 | 状态：当前代码已恢复为可运行业务 Demo | 事实源：`Assets/AutoChessDemo`

本文件只记录当前代码事实。2026-05-26 旧文档中“已破坏性删除、待重构、HeadlessAutoChess/SHeadless* 系统清单”的结论已经过期，不能再作为当前架构事实。

## 当前总览

`Assets/AutoChessDemo` 现在是一条业务导向的自走棋 demo 链路：

1. `GameRoom` 创建房间、玩家席位、阵容、棋子展示名、技能/GE/属性/tag 规则码。
2. `Battle` 定义对局输入、输出、统计、timing、unit snapshot 和业务日志。
3. `Battle/Ecs` 提供 Demo 专用 ECS 系统和 `GASDefinitionCatalogBlob` 安装器。
4. `Integration/GasCore` 是 AutoChess 到 GAS Core 的唯一 adapter。
5. `AutoRunner` 提供 batchmode / player launch / system bootstrap。
6. `Presentation` 只显示战斗日志，不承载模拟，不回读 ECS World。

## 当前事实分层

| 层级 | 当前 owner | 事实 | 边界 |
|---|---|---|---|
| business-active | `GameRoom`、`Battle`、`AutoRunner`、`Presentation` | demo 已恢复为可运行业务链，不是“删除后待重构” | 业务层不应直接散落调用 GAS Core |
| runtime-extension | `Battle/Ecs` 两个 `ISystem` | command drive 与 execute calculation 已插入当前 GAS group，并已迁到 scheduled job 路径 | 仍是 demo extension，不是通用 Runtime Core 终局证明 |
| bridge/direct-EM | `Integration/GasCore/AutoChessGasCoreBridge` | GAS init/tick/catalog/ASC lifecycle 已集中到 adapter | 内部直接 `EntityManager` 是集中后的风险，不是目标态完成 |
| observation-only | Replay/Diagnostics/Official diff/structured log/result builder | 能导出日志和结果，方便业务验证 | 不等于 CoreSimulation 性能证明 |
| demo-only catalog | `AutoChessBattleDefinitionCatalogBuilder` | 手写最小 `GASDefinitionCatalogBlob` 已能驱动 demo | 不能替代 Luban/SourceGenerator/Baker 配置链 |

## 官方规则对照

| Demo 层级 | 采用规则 | 当前判定 |
|---|---|---|
| business-active | `SYS-05`、`DBG-04` | 业务日志和结果可作为 Boundary 验证，不作为 Core hot path 成本 |
| runtime-extension systems | `SYS-02`、`QRY-01`、`PRF-05`、`PRF-09`、`CASE-01/02/03` | 插入当前 GAS groups 是正向事实；主线程单位扫描和执行伤害 buffer 写入已迁到 scheduled job |
| bridge/direct-EM | `SC-01`、`PRF-02`、`PRF-04`、`ECB-03` | adapter 集中可接受，但 unit/effect cleanup 不应长期绕过 structural commit owner |
| observation/export | `SYS-04`、`SYS-05`、`DBG-01..05` | Replay/Diagnostics/Official diff 必须和 CoreSimulation 分组报告 |
| demo catalog | `BLOB-01/02`、`CASE-24`、`SEL-02` | 手写 catalog 是 demo proof；不能替代配置生成/Baker 验收 |

## 当前目录事实

| 路径 | 当前职责 | 关键文件 |
|---|---|---|
| `GameRoom` | 房间、阵容、棋子定义、规则码 | `AutoChessGameRoomDefinition.cs` |
| `Battle` | 对局契约、编排、session、结果、日志翻译 | `AutoChessBattleContracts.cs`, `AutoChessBattleManager.cs`, `AutoChessBattleSession.cs`, `AutoChessBattleResultBuilder.cs`, `AutoChessBattleLog.cs` |
| `Battle/Ecs` | Demo ECS 扩展、catalog 安装、战斗指令驱动、斩杀 execution | `AutoChessBattleCommandDriveSystem.cs`, `AutoChessExecuteDamageCalculationSystem.cs`, `AutoChessBattleDefinitionCatalogBuilder.cs`, `AutoChessBattleDriverComponents.cs` |
| `Integration/GasCore` | GAS 初始化、ASC 创建/销毁、tick 推进、diagnostics/replay/official diff 导出 | `AutoChessGasCoreBridge.cs` |
| `AutoRunner` | 无头运行和 GAS 系统注册 | `AutoChessRuntimeRunner.cs`, `AutoChessRuntimeSystemBootstrap.cs` |
| `Presentation` | 日志场景、noop cue | `AutoChessDemoSceneRunner.cs`, `AutoChessNoopCue.cs`, `Scenes/AutoChessLogDemo.unity` |
| `Config` | 后续配置产物入口 | 当前无旧 Headless generated `.cs` 文件 |

## 当前执行链

```mermaid
flowchart TD
    Room["AutoChessGameRoomFactory"] --> Manager["AutoChessBattleManager"]
    Manager --> Session["AutoChessBattleSession"]
    Session --> Bridge["AutoChessGasCoreBridge"]
    Bridge --> Runtime["GASManager / EX_GAS_World"]
    Bridge --> Catalog["AutoChessBattleDefinitionCatalogBuilder"]
    Bridge --> Bootstrap["AutoChessRuntimeSystemBootstrap"]
    Bootstrap --> Drive["AutoChessBattleCommandDriveSystem"]
    Bootstrap --> Execute["AutoChessExecuteDamageCalculationSystem"]
    Drive --> AbilityCommands["ASC owner-local\nAbilityCommandBuffer"]
    Execute --> DeltasFacts["AttributeModifierBuffer + GameplayEventBuffer"]
    Runtime --> Observation["Replay / Diagnostics / Official diff"]
    Observation --> Result["AutoChessBattleResultBuilder"]
    Result --> Log["AutoChessBattleLogBuilder"]
    Log --> Scene["AutoChessDemoSceneRunner"]
```

## 已成立事实

1. `AutoChessRuntimeSystemBootstrap.RegisterSystems(World)` 不再使用旧 `GASCommandGroup / GASExecutionCalculationExtensionGroup` 名称；它从 `GASSystemGroups` 取当前 5 段主链 group，并把：
   - `AutoChessBattleCommandDriveSystem` 加入 `GASCommandResolveSystemGroup`
   - `AutoChessExecuteDamageCalculationSystem` 加入 `GEExecutionCalculationExtensionSystemGroup`
2. `AutoChessGasCoreBridge.EnsureRuntimeInitialized()` 调用 `GASManager.Initialize(attachToPlayerLoop: false)`，注册 Demo systems，并安装 demo catalog。
3. `AutoChessBattleDefinitionCatalogBuilder` 直接构建并安装最小 `GASDefinitionCatalogBlob`，为 demo 提供 ability / GE / modifier / requirement / tag mask / granted ability catalog。
4. `AutoChessBattleCommandDriveSystem` 是 `ISystem`，使用预创建 `_driverQuery` / `_unitQuery`，通过 scheduled `IJobChunk` 收集存活单位，并由 scheduled `IJob` 按 battle group target cache 写入 source ASC owner-local `AbilityCommandBuffer`、启用 `ASCCommandPendingComponent`。
5. `AutoChessExecuteDamageCalculationSystem` 是 `ISystem`，位于 execution extension slot，通过 scheduled `IJob` 读取 `GEEffectCommandBuffer`，处理 demo 斩杀伤害，写入 `AttributeValueBuffer` / `AttributeModifierBuffer` / `GameplayEventBuffer`，并通过 lookup 写 legacy EventBus 兼容 observation。
6. `AutoChessBattleSession` 负责创建/缓存/销毁本场单位句柄，业务层不直接散落 `GASManager` 调用。
7. `AutoChessBattleResultBuilder` 与 `AutoChessBattleLogBuilder` 消费 runtime structured log 和 result snapshot，输出中文战斗叙述。
8. `AutoChessDemoSceneRunner` 只消费 `AutoChessBattleResult.BattleLog`，不会重新遍历 ECS World 推导表现状态。

## 代码证据矩阵

| 事实 | 代码证据 | 证据等级 |
|---|---|---|
| Demo systems 通过 bootstrap 插入当前 GAS group | `Assets/AutoChessDemo/AutoRunner/AutoChessRuntimeSystemBootstrap.cs:20-33` | runtime-extension |
| bridge 初始化 GAS、注册 systems、安装 catalog | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasCoreBridge.cs:14-20` | adapter-active |
| bridge 手动推进 5 段 GAS group 并记录 timing | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasCoreBridge.cs:43-64`、`:388-441` | demo-runner/observation |
| command drive 是 `ISystem` 并挂入 CommandResolve | `Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleCommandDriveSystem.cs:7-11` | runtime-extension |
| command drive 使用 scheduled `CollectUnitTargetStatesJob` 读取单位/属性 | `Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleCommandDriveSystem.cs:52-60`、`:87-149` | runtime-extension |
| command drive 使用 scheduled `FlushCommandRequestsJob` 写 ASC owner-local `AbilityCommandBuffer` 并启用 pending marker | `Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleCommandDriveSystem.cs:62-80`、`:152-236` | runtime-extension |
| execute calculation 插入 execution extension slot | `Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs:8-10` | runtime-extension |
| execute calculation 使用 scheduled `ExecuteDamageCalculationJob` 读取 GE command、写 attribute/delta/fact | `Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs:42-59`、`:66-213` | runtime-extension |
| execute calculation 仍写 legacy EventBus | `Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs:98-106`、`:182-202` | migration-bridge |
| demo catalog 用 `BlobBuilder` 手写构建 | `Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleDefinitionCatalogBuilder.cs:59-112` | demo-only |
| demo catalog singleton 查找使用 `ToEntityArray` | `Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleDefinitionCatalogBuilder.cs:122-141` | init-only risk |
| bridge 直接创建/销毁 driver 和 ASC 相关 entity | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasCoreBridge.cs:145-211`、`:317-360` | bridge/direct-EM |
| bridge observation reset/export 直接读写 singleton/buffer | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasCoreBridge.cs:67-75`、`:236-254`、`:507-511` | observation-only |

## DOTS 合规现状

| 项 | 当前状态 | 结论 |
|---|---|---|
| 系统数量 | AutoChessDemo 当前 2 个 Demo ECS `ISystem` | 不再是旧“16 个 SHeadless system” |
| 主链接入 | command drive 和 execute calculation 都挂入当前 GAS 5 段主链 | 正向 |
| Job 化 | command drive 与 execute calculation 主体已改为 scheduled job，避免主线程 `SystemAPI.Query` 和属性 buffer dependency 冲突 | 正向，但仍是 demo extension |
| `ToEntityArray` | 只在 `AutoChessBattleDefinitionCatalogBuilder` 安装/卸载 catalog 时查询 singleton entity | 低频初始化路径，可接受但需后续 owner 化 |
| 结构变化 | `AutoChessGasCoreBridge` 创建/销毁 driver、ASC、granted ability、active effect 时直接用 `EntityManager` | Demo 集成层 P1/P0 边界风险 |
| Definition | 代码安装最小 catalog，不依赖旧 Headless generated rows | 当前事实已变更 |
| Observation | 使用 Replay/Diagnostics/Official diff/structured log | 正向，但 observation 不等于性能证明 |

## 当前风险

### AC-01：Bridge 仍是直接 EntityManager 集中点

`AutoChessGasCoreBridge` 是正确的 adapter seam，但内部仍大量使用 `GASManager.EntityManager`：

- `CreateBattleDriver()` 直接 `CreateEntity()` / `AddComponentData()`
- `DestroyBattleDriver()` / `DestroyBattleUnit()` 直接 `DestroyEntity()`
- `AddBattleUnitComponent()` 直接给 ASC 写 Demo component
- `DestroyGrantedAbilities()` / `DestroyActiveEffects()` 直接清理 runtime entity
- `ResetObservationState()` 直接清 buffer / set singleton data

这比旧代码“业务各处散落 GASManager”更好，但还不是目标态 command sink / structural commit owner。

当前分类必须细化：

| 操作 | 代码证据 | 分类 | 后续要求 |
|---|---|---|---|
| `EnsureRuntimeInitialized()` / `ShutdownRuntime()` | `AutoChessGasCoreBridge.cs:14-30` | 初始化 adapter，可暂接受 | 保持唯一入口 |
| `TickRuntime()` 手动 update groups | `AutoChessGasCoreBridge.cs:43-48` | demo runner，可暂接受 | 与 PlayerLoop/固定步 owner 分开 |
| `CreateBattleDriver()` / `DestroyBattleDriver()` | `AutoChessGasCoreBridge.cs:145-199` | 结构变化风险 | 迁到 request/structural commit owner |
| `DestroyBattleUnit()` / granted ability / active effect cleanup | `AutoChessGasCoreBridge.cs:202-211`、`:317-360` | 接近 Core lifecycle P0 边界 | 统一为 ASC destroy request 链 |
| `ResetObservationState()` / `ClearBuffer<T>()` | `AutoChessGasCoreBridge.cs:67-75`、`:507-511` | observation-only | 不计入 CoreSimulation 成本 |

### AC-02：Demo command drive 仍是主线程扫描

`AutoChessBattleCommandDriveSystem` 使用 `SystemAPI.Query` 遍历所有 battle unit，并通过 `NativeList`/`NativeArray` 做目标缓存。当前实现能表达业务，但不是 Burst job / chunk pipeline 终局。

需要注意的是，它已经避免了逐单位创建 request entity，并改为向 ASC owner-local `AbilityCommandBuffer` 追加 command；这是正向变化。但 `SystemAPI.Query`、`CalculateEntityCount()`、`NativeList(Allocator.Temp)` 和 direct buffer resolve 仍说明它是迁移期 Demo extension，不是 Runtime Core 的高规模模板。

### AC-03：Execution extension 同时写 delta/fact/legacy event

`AutoChessExecuteDamageCalculationSystem` 在 execution extension 中：

1. 读取 `GEEffectCommandBuffer`
2. 直接修改目标 `AttributeValueBuffer`
3. 写 `AttributeModifierBuffer`
4. 写 `GameplayEventBuffer`
5. 用 `EventBusHelper.BeginGameplayEventBatch()` 写 legacy event

这符合迁移期“typed fact + legacy bridge”现实，但后续需要明确 execution extension 的写权限和 deterministic ordering。

这里不是单纯的 Demo 风险：execution extension 已插入 `GEExecutionCalculationExtensionSystemGroup`，会与 CoreSimulation 中的 generated GE/attribute systems 同步工作。后续需要把 extension 的允许写入对象收窄到“execution output/delta/fact”，并明确 legacy EventBus 只是观察兼容层。

### AC-04：Catalog 安装仍是代码内最小样本

`AutoChessBattleDefinitionCatalogBuilder` 是当前 demo 的最小 catalog builder，不是完整 Luban/SourceGenerator 链路。旧 `HeadlessAutoChessGeneratedDefinitionRows.cs`、`HeadlessAutoChessDefinitionSource.cs` 不存在，不能再作为当前 AutoChess facts。

它当前最有价值的证据是：demo 不是依赖 managed registry 临时跑通，而是实际安装了 `GASDefinitionCatalogBlob`。它当前最大的限制是：catalog 内容是手写常量和 builder 代码，且 install/uninstall 仍通过临时 query 定位 singleton catalog entity。

### AC-05：业务设计已稳定为日志可视化 demo，不是资源表现 demo

当前场景 `AutoChessLogDemo.unity` 只显示日志。没有棋子模型、VFX、SFX 资源；这不是缺失的 gameplay 链路，而是当前 demo 的无资源表现约束。表现层不能被用来证明 CoreSimulation 性能。

## 与 Runtime Core 的边界

| 层 | 当前 owner | 不应越界 |
|---|---|---|
| 房间/阵容/胜负/日志 | AutoChessDemo | 不进入 `Assets/GAS/Runtime` |
| ASC/Ability/GE/Attribute/Cue 执行 | GAS Runtime | 不依赖 AutoChess 业务类型 |
| Demo 到 Core 的翻译 | `AutoChessGasCoreBridge` | 上层业务不直接调用 `GASManager` |
| Demo ECS 扩展系统 | `Battle/Ecs` | 只通过 current GAS groups 插入，不发明旧 group |
| 表现 | `Presentation` | 只消费 result/log，不反向写 Core state |

## 当前结论

AutoChessDemo 已经不是“已删除后待重构”的状态。它现在是一条可运行的业务链，并且职责划分比旧 Headless 方案更清晰：业务层、GAS adapter、Demo ECS 扩展和日志表现已经拆开。

但它仍不是 DOTS 目标态证明：

1. Bridge 内直接 `EntityManager` 是当前最集中的边界风险。
2. 两个 Demo ECS 系统还没有 job/chunk 化。
3. execution extension 同时写 delta/fact/legacy event，需要纳入 Runtime 写权限治理。
4. Demo catalog 是代码安装的最小样本，不是完整 generated/baking 配置链。
5. 当前日志场景证明业务可读性，不证明资源表现、不证明高规模性能。
