# AutoChess Demo

`Assets/AutoChessDemo` 是 EX-GAS 2.0 的游戏业务 Demo。它首先是一条可运行的自走棋业务链，其次才是为了测试和性能采样暂时保持“无资源表现”。当前场景只提供一个战斗日志画面；棋子模型、特效、音效和美术资源继续空置，但房间、阵容、战斗规则、运行编排和日志表现都按真实业务层拆分。

## 模块布局

- `GameRoom`: 房间、玩家席位、阵容、棋子展示名、技能/GE 规则码。
- `Battle`: 对局契约、对局入口、战场会话、结果快照和战斗日志翻译。
- `Battle/Flow`: 对局生命周期、预热、测量窗口、tick 推进、胜负收口和 official diff 捕获。
- `Battle/Report`: Runtime structured log 到 AutoChess 业务战报的投影。它只消费 Core facts，不补写伤害或死亡结果。
- `Battle/Validation`: headless / scene 共用的验证 pass 编排和验证报告，负责 process warmup、repeat-run cleanup probe、diagnostic pass、official diff separate pass、Runtime chain gate、`AutoChessValidationRunResult` 和 machine-readable evidence。
- `Battle/Ecs`: AutoChess 专用 ECS 核心扩展，包含 command drive、driver structural destroy、definition/config catalog、execution calculation。Command drive 只写 ability code + target ASC，不缓存或传递 GAS internal ability entity。
- `Integration/GasCore`: AutoChessDemo 到 GAS Core 的唯一接入层；`AutoChessBattleRuntime` 是 Flow 侧窄运行时入口，`AutoChessGasCoreBridge` 只保留 Session 侧 entity lifecycle/read model/report projection composition。内部按 `AutoChessGasRuntimeHost`、`AutoChessGasCatalogSession`、`AutoChessGasObservationGateway`、`AutoChessGasBattleEntityLifecycle`、`AutoChessGasRuntimeTicker`、`AutoChessGasBattleReportFactProjector` 和 `AutoChessGasCoreContracts` 分 owner 承担 bootstrap、catalog/definition、diagnostics/export、fixed tick runner、dependency drain、unit lifecycle 和 report fact projection 等访问。不再使用一个 wide access 聚合所有 direct ECS 能力。
- `AutoRunner`: batchmode / Player launch / GAS system bootstrap。
- `Presentation`: 日志画面、PresentationOutboxBridge 和 Cue 占位。场景只消费表现快照，不承载战斗模拟。
- `Generated`: codegen 输出的强类型配置数据。生成物提供常量、验证 profile、基础单位 spawn plan static lookup 和 execution calculation 参数行，不拥有房间构造、缩放阵容或展示文案。

## Demo 与 GAS Core 职责

- AutoChessDemo owns: 房间、玩家席位、阵容、目标策略、战斗循环、胜负收口、业务日志和日志画面。
- `GameRoom` owns: 从生成配置行组装真实业务房间、玩家席位、镜像 battle group 和棋子展示信息。`Generated` 不反向创建房间。
- `Battle/Flow` owns: 一场对局的运行时段、测量时段和收口策略。同步 batchmode 和场景 stepped 模式必须复用同一套 flow。
- `Battle/Report` owns: 把 GAS Core structured log 里的 ability/effect/attribute/fact 投影成 AutoChess 战报事件。
- `Battle/Validation` owns: 把不同 runner 的 warmup / measured / diagnostic / official diff pass 收敛到同一套 validation module。`AutoChessBattleValidationReport` 负责 evidence/hash/summary/Mermaid，人读字符串只是导出视图。
- `Battle/Ecs` owns: Demo 在 GAS 物理组里的自走棋扩展系统，包括 chunk-based 技能指令驱动、driver structural destroy、斩杀 execution calculation 和 Demo 专用 definition/config catalog。
- `Integration/GasCore` owns: Demo 概念到 GAS Runtime API 的翻译，以及对 Core 观测数据的读取。上层业务不直接触碰 `GASManager`、`EntityManager`、`ASCCommandPort`、`GasRuntimeDebugger`、`GasStructuredLogExporter`、`ASCHandle` 或 raw ASC `Entity`；战斗单位 handle 只携带 opaque battle unit key，key 到 ASC 的解析只存在于 `AutoChessGasBattleEntityLifecycle`。
- GAS Core owns: Ability / GameplayEffect / Attribute / Cue / Replay / Runtime Diagnostics 的执行与事实流。AutoChessDemo 不修改 Core 规则，只通过集成层装配本 Demo 的业务定义和扩展系统。
- Runtime generated Core owns: 把 `AbilityCommandBuffer` 转成 `AbilityCommitRequestComponent`、`GEEffectCommandBuffer`、spec、delta 和 typed fact。AutoChessDemo 只能用 commands/facts/deltas/cues 这些结果验证链路；Demo 专用 execution extension 必须从 ECS 配置实体读取参数，不在系统里硬编码公式常量。

## 当前业务链

1. `AutoChessGameRoomFactory` 创建标准双人房间：玩家A和玩家B各 2 名棋子，x50 等规模通过 battle group 镜像扩展。
2. `AutoChessBattleContracts` 定义对局输入、结果、统计、单位快照和 timing 数据契约。
3. `AutoChessBattleManager` 只作为 batchmode / stepped 模式的对局入口，不再持有 tick 细节。
4. `AutoChessBattleFlow` 打开本场对局：建立预热 tick、测量窗口、胜负 flush 和 official diff 捕获策略。
5. `AutoChessBattleRuntime` 打开 Runtime World、重置观测状态、推进固定 tick，并导出 Diagnostics；`AutoChessGasCoreBridge` 只作为 Session 侧 composition root。
6. `AutoChessBattleSession` 打开房间：按阵容创建本场战斗单位句柄和 driver handle，并在收口时交还给 Integration/GasCore 清理；业务层不保存 raw ASC/driver `Entity`，战报只消费 GasCore projection 后的 `AutoChessBattleReportFact`。
7. `AutoChessBattleDefinitionCatalogBuilder` 安装最小 `GASDefinitionCatalogBlob` 和 `AutoChessExecuteDamageCalculationComponent`：普攻走 instant modifier，斩杀走 generated config row + ECS config entity + execution extension。
8. `AutoChessBattleCommandDriveSystem` 按 battle group 收集存活棋子，缓存 frontline / lowest-health 目标，再按 chunk 写入只含 ability code + target ASC 的 `AbilityCommandBuffer`；ability code 到 internal ability entity 的解析归 Runtime Core，不由 Demo ECS extension 缓存。
9. Runtime Core 执行 Ability / GE / Attribute / Cue / Replay 管线。
10. `AutoChessExecuteDamageCalculationSystem` 从 ECS singleton 配置读取斩杀公式参数，处理斩杀伤害，并写入 typed fact。
11. `AutoChessGasObservationGateway` 导出 Replay / Diagnostics / Official diff 观测数据，`AutoChessGasBattleReportFactProjector` 把 Runtime structured log 投影成 Demo 业务 facts，`AutoChessBattleReportBuilder` 不接触 raw `Entity` 或 runtime resolver。
12. `AutoChessBattleResultBuilder` 组合 `AutoChessBattleResult`，同时保留 Runner 需要的 Runtime diagnostics/official diff 诊断数据。
13. `AutoChessBattleLogBuilder` 只消费 `AutoChessBattleReport`，渲染中文业务日志，例如“玩家A的霜卫先锋对玩家B的荒原斗士发动了破阵斩，造成 12 点伤害”。
14. `AutoChessBattleValidationRun` 统一提供 warmup、repeat-run cleanup、diagnostic、official diff pass 和 `AutoChessValidationRunResult` 构建；headless runner 与 scene runner 不再各自维护这部分验证逻辑。
15. `AutoChessBattleValidationReport` 统一生成 `AutoChessValidationEvidence`、summary、hotspot、timing、official diff、Mermaid 数据流/时序图；`AutoChessRuntimeRunner` 只保留 batch entry 和 fail policy。
16. `AutoChessLogPresentationOutboxBridge` 把 battle result 中的边界派生日志转成 `AutoChessPresentationSnapshot`，输出 marker count、展示行数、丢弃行数和 disabled reason。
17. `AutoChessDemoSceneRunner` 只绘制 bridge 输出的日志面板，并导出 `AutoChessBattleLog.txt`。

## 无资源表现约束

- 当前没有人物、特效、音效资源，但仍保留业务上需要的展示名、技能名、伤害、死亡、胜负日志。
- Presentation 不反推战斗状态，不遍历 ECS World，只消费 `AutoChessPresentationSnapshot`；默认 bridge 输入来自 Boundary / report 派生结果，未来真实 UI/VFX/SFX 只替换 bridge。
- 日志层不直接读取 GAS Runtime structured log；死亡、伤害、技能释放等展示语义由 `AutoChessBattleReport` 承接。
- 验证层继续使用 Runtime Diagnostics、official tool diff 和 structured log，但不替代游戏业务模块。

## 验收命令

```powershell
E:\Unity\UnityEditor\6000.3.14f1\Editor\Unity.exe -batchmode -quit -projectPath E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity -executeMethod GAS.AutoChessDemo.Editor.AutoChessDemoBatchRunner.RunAutoChessBattleOnceAndExit -logFile E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\Temp\AutoChessBattleValidation.log
```

真实 Editor PlayMode + 官方 Profiler 链路使用 AIBridge：

```powershell
$CLI = 'Library\PackageCache\cn.lys.aibridge@f203de2ec848\Tools~\CLI\win-x64\AIBridgeCLI.exe'
& $CLI compile unity --timeout 120000 --pretty
& $CLI scene load --scenePath 'Assets/AutoChessDemo/Presentation/Scenes/AutoChessLogDemo.unity' --mode single --on-dialog discard --timeout 60000 --pretty
& $CLI editor play --timeout 30000 --pretty
& $CLI get_logs --regex 'AutoChessDemo|Exception|Error|failed|Profiler' --count 120 --pretty
& $CLI get_logs --logType Error --count 50 --pretty
& .\Tools\Diagnostics\Analyze-AutoChessProfile.ps1 -PrintMarkdown
```

一次有效跑通至少需要满足：

- `DriverIssuedCommands > 0`
- `EventCounts.AttributeChanges > 0`
- `EventCounts.ExecutionCalculationOutputUpdated > 0`
- `EventCounts.CueRequests > 0`
- `Completed == true`
- `RuntimeDiagnostics.EventCount > 0`
- `OfficialToolDiff.JournalingCaptured == true`
- `blockingDebugErrors == 0`
- `AutoChessDemoValidationRunResult: passed=True`，并输出 `thresholdsPassed / runtimeChainPassed / repeatRunPassed / presentationMarkers / presentationDroppedLines`
- `RuntimeDiagnostics.CoreCounters.RequestCount > 0`
- `RuntimeDiagnostics.CoreCounters.FactCount > 0`
- `AutoChessDemoRepeatRunEvidence: passed=True`，同一 Runtime World 连跑两局时 commands / attribute changes / execution outputs / cue requests 不漂移
- `AutoChessDemoRuntimeHotspots` 输出 `commandResolveAvgMs`、`coreSimulationAvgMs`、`structuralCommitAvgMs`、`boundaryProjectionAvgMs` 和 `dependencyDrainAvgMs`
- `AutoChessDemoBattlePresentation` 输出 `markers / sourceLines / displayedLines / droppedLines / disabledReason`
- 日志包含“游戏开始 / 发动了 / 受到致命伤害，死亡 / 战斗结束”
- batchmode 与场景 stepped 模式复用 `AutoChessBattleFlow` 和 `AutoChessBattleValidationRun`，不得再各自维护 tick / diagnostic / official diff 收口逻辑

## 当前性能基线

2026-06-07 batchmode x50 验收基线：200 units、`battleTicks=9`、`commands=700`、`attributeChanges=650`、`executionOutputs=250`、`cueRequests=700`、`debugErrors=0`、`blockingDebugErrors=0`、`AutoChessDemoValidationRunResult passed=True`、`AutoChessDemoRepeatRunEvidence passed=True`、`presentationMarkers=4600`、`presentationSourceLines=72`、`presentationDroppedLines=0`、`GASTickTotal avgMs≈0.779`、`GASCommandResolveSystemGroup avgMs≈0.077`、`GASCoreSimulationSystemGroup avgMs≈0.290`、`GASStructuralCommitSystemGroup avgMs≈0.011`、`GASBoundaryProjectionSystemGroup avgMs≈0.312`、`GASDependencyDrain avgMs≈0.042`、`JournalingCaptured=True`。这组数据来自无头 Runtime 验收 `Temp/AutoChessBattleValidation-P5P6-8.log`；真实 Editor PlayMode/Profiler 仍需使用 AIBridge 命令单独采样。
