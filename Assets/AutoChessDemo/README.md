# AutoChess Demo

`Assets/AutoChessDemo` 是 EX-GAS 2.0 的游戏业务 Demo。它首先是一条可运行的自走棋业务链，其次才是为了测试和性能采样暂时保持“无资源表现”。当前场景只提供一个战斗日志画面；棋子模型、特效、音效和美术资源继续空置，但房间、阵容、战斗规则、运行编排和日志表现都按真实业务层拆分。

## 模块布局

- `GameRoom`: 房间、玩家席位、阵容、棋子展示名、技能/GE 规则码。
- `Battle`: 对局契约、对局编排、战场会话、结果快照、战斗日志翻译。它消费 Runtime structured log，输出业务可读日志。
- `Battle/Ecs`: AutoChess 专用 ECS 适配层，包含 command drive、definition catalog、execution calculation。
- `Integration/GasCore`: AutoChessDemo 到 GAS Core 的唯一接入层，集中 Runtime 初始化、ASC 创建、定义安装、tick 推进、Replay/Diagnostics/Official diff 导出。
- `AutoRunner`: batchmode / Player launch / GAS system bootstrap。
- `Presentation`: 日志画面和 Cue 占位。场景只显示日志，不承载战斗模拟。
- `Config`: 后续 Luban / SourceGenerator 产物入口，生成目录继续保持忽略。

## Demo 与 GAS Core 职责

- AutoChessDemo owns: 房间、玩家席位、阵容、目标策略、战斗循环、胜负收口、业务日志和日志画面。
- `Battle/Ecs` owns: Demo 在 GAS 物理组里的自走棋扩展系统，包括技能指令驱动、斩杀 execution calculation 和 Demo 专用 definition catalog。
- `Integration/GasCore` owns: Demo 概念到 GAS Runtime API 的翻译，以及对 Core 观测数据的读取。上层业务不直接触碰 `GASManager`、`ASCCommandGateway`、`GasRuntimeDebugger` 或 `GasStructuredLogExporter`。
- GAS Core owns: Ability / GameplayEffect / Attribute / Cue / Replay / Runtime Diagnostics 的执行与事实流。AutoChessDemo 不修改 Core 规则，只通过集成层装配本 Demo 的业务定义和扩展系统。

## 当前业务链

1. `AutoChessGameRoomFactory` 创建标准双人房间：玩家A和玩家B各 2 名棋子，x50 等规模通过 battle group 镜像扩展。
2. `AutoChessBattleContracts` 定义对局输入、结果、统计、单位快照和 timing 数据契约。
3. `AutoChessBattleManager` 只负责对局运行编排：打开/关闭采样窗口、推进战斗循环、决定何时收口。
4. `AutoChessGasCoreBridge` 初始化 GAS Runtime、注册 Demo ECS 系统、安装 definition catalog、按物理组推进 tick，并重置事件/调试观测状态。
5. `AutoChessBattleSession` 打开房间：按阵容创建本场战斗单位句柄，并在收口时交还给 `AutoChessGasCoreBridge` 清理。
6. `AutoChessBattleDefinitionCatalogBuilder` 安装最小 `GASDefinitionCatalogBlob`：普攻走 instant modifier，斩杀走 execution extension。
7. `AutoChessBattleCommandDriveSystem` 按 battle group 收集存活棋子，缓存 frontline / lowest-health 目标，向 `AbilityCommandBuffer` 写入技能指令。
8. Runtime Core 执行 Ability / GE / Attribute / Cue / Replay 管线。
9. `AutoChessExecuteDamageCalculationSystem` 处理斩杀伤害，并写入 typed fact。
10. `AutoChessGasCoreBridge` 导出 Replay / Diagnostics / Official diff 观测数据，`AutoChessBattleResultBuilder` 只负责组合 `AutoChessBattleResult`。
11. `AutoChessBattleLogBuilder` 把 Runtime structured log 翻译成中文业务日志，例如“玩家A的霜卫先锋对玩家B的荒原斗士发动了破阵斩，造成 12 点伤害”。
12. `AutoChessDemoSceneRunner` 只绘制一个日志面板，并导出 `AutoChessBattleLog.txt`。

## 无资源表现约束

- 当前没有人物、特效、音效资源，但仍保留业务上需要的展示名、技能名、伤害、死亡、胜负日志。
- Presentation 不反推战斗状态，不遍历 ECS World，只消费 `AutoChessBattleResult.BattleLog`。
- 验证层继续使用 Runtime Diagnostics、official tool diff 和 structured log，但不替代游戏业务模块。

## 验收命令

```powershell
E:\Unity\UnityEditor\6000.3.14f1\Editor\Unity.exe -batchmode -quit -projectPath E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity -executeMethod GAS.AutoChessDemo.AutoChessRuntimeRunner.RunAutoChessBattleOnce -logFile E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\Temp\AutoChessBattleValidation.log
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

## 当前性能基线

2026-05-29 的 x50 PlayMode 30 秒采样仍是当前参考基线：200 units、`battleTicks=4130`、`avgTickMs=1.561`、`GASTickTotal avgMs=1.548`。这组数据用于 Runtime Core 热点定位；本轮业务重排的验收重点是编译、无头链路和日志画面，不重新宣称性能结论。
