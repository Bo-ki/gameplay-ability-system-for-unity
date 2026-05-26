# Runtime Validation Demo - AutoChess 无头验收 - Freeze Safety Gate 验收

## 节点定位

本节点是 AutoChess 验收支线的入口闸门，承接 T1 AM0（Freeze Safety Gate）的契约，负责在 Runtime Core Frame Backbone 闭合后验证 AutoChess 不依赖已冻结的旧 pipeline API，不绕过 command/spec/delta/fact 主链。

## 父节点

[AutoChess 无头验收](README.md)

## 任务ID

`T6-AutoChess-AM0`

## 状态

就绪

## 兄弟关系

`sequential`。前置：T1 AM0 Freeze Safety Gate 契约已确立、T1 Runtime Core Frame Backbone 闭合。后继：T6-AutoChess-AM1（Debugger 基线验收）依赖本节点确认的 freeze 边界。

## 拆分历史

从 `AutoChess无头验收` 拆分（S4 依赖链：必须先确认 freeze safety 边界才能进入后续验收），2026-05-20。

## 领取轮次

第 1 轮（首次领取）

## 当前进展

尚未领取。前置依赖：T1 AM0（契约已确立）、T1 Runtime Core Frame Backbone（AM2B-A~F 已完成）。

## 本轮目标

首次执行：在 Runtime Core Frame Backbone 闭合后，对 AutoChess 做静态依赖扫描，确认不调用已冻结的旧 pipeline API（`LegacyInstantEntityLifecycle`），不绕过 `BEffectCommand` / `CActiveEffectStore` / typed facts 主链。完成后输出 freeze safety 验收报告。

## 当前问题

AutoChess 作为 Runtime Core 的全链路验收载体，需要在 Runtime Core Frame Backbone 闭合后重新确认 freeze safety gate 条件：不再依赖旧 pipeline、不再绕过 command/spec/delta/fact 主链。

## 目标

1. 静态依赖扫描确认 AutoChess 不调用已冻结的旧 pipeline API。
2. AutoChess 的 GE apply/attribute/cue 路径全部走 `BEffectCommand` 主链或明确标记的 Boundary request。
3. 验收报告可供 T6-AutoChess-AM1（Debugger 基线验收）作为前置条件引用。

## 目标态约束摘要

（从关联 Spec 提取，与本任务直接相关的硬约束）

1. 旧 `LegacyInstantEntityLifecycle` 标记为 `LegacyFrozen`、`MigrationOnly`、`NoNewFeatureExpansion`。AutoChess 不得新增对旧 pipeline API 的调用。
   > 来源：`03-RuntimeCore管线Spec.md`、T1 AM0

2. AutoChess 作为 Demo 层只能依赖 Runtime Core public contract，不允许 Runtime Core 反向引用 Demo。
   > 来源：`10-AutoChess无头验收Spec.md`

## 当前事实约束

（从关联 ISSUE 提取，本任务必须知道的架构事实）

1. T1 AM0 Freeze Safety Gate 已建立 `GameplayEffectRuntimePipelineContract`，旧 pipeline 入口已收敛为 `TryApplyLegacyInstantModifierBypass` / `LegacyInstantEntityLifecycle`。
   > 来源：T1 `AM0-FreezeSafetyGate.md`

## 适用规则摘要

| 规则 | 本任务约束 |
|---|---|
| `SEL-02` | AutoChess 的 observation/presentation 只能通过 fact stream / presentation outbox，不通过旧 EventBus |
| `SC-01` | Demo 层不做结构变化，不绕过 `GasStructuralPlaybackSystemGroup` |

## 非目标

1. 不新增 AutoChess 业务机制。
2. 不修改 Runtime Core freeze safety contract（由 T1 AM0 负责）。

## 执行范围

1. `Assets/AutoChessDemo/` — 全量静态依赖扫描
2. AutoChess 对 `Assets/GAS/Runtime/` 的 API 调用审计

## 执行细则

1. **静态依赖扫描**：`rg` 扫描 AutoChessDemo 中所有对 `LegacyInstant`、`FastInstant`、`EventBusHelper.Enqueue` 的引用。
2. **API 审计**：逐文件确认 AutoChess 的 GE apply/attribute/cue 路径是否走 `BEffectCommand` 主链。
3. **禁止新增旧路径调用**：如发现 AutoChess 代码中有新增的旧 pipeline API 调用，必须标记并写入验收报告。
4. **输出 freeze safety 验收报告**：记录扫描结果、违规项（如有）和处理建议。

## 验收标准

1. `rg "LegacyInstant|FastInstant" Assets/AutoChessDemo -g "*.cs"` 无新增匹配（已有旧路径保留 `Legacy` 前缀可接受）。
2. `rg "EventBusHelper\.Enqueue" Assets/AutoChessDemo -g "*.cs"` 无匹配。
3. AutoChess 的 GE apply 路径不绕过 `BEffectCommand` / `CActiveEffectStore` / typed facts 主链。
4. `dotnet build` Runtime + Tests 通过；如 LicensingClient 阻塞，记录为环境阻塞。

## 测试链路

1. `rg "LegacyInstant|FastInstant|EventBusHelper\.Enqueue" Assets/AutoChessDemo -g "*.cs"`
2. `dotnet build` Runtime + Tests
3. AutoChess 默认 validation；如 Unity LicensingClient 阻塞，记录 return code 和关键日志。

## 交还内容

1. 更新本文件 `当前进展`、`领取轮次`、`状态` 字段。
2. 更新 `AutoChess无头验收/README.md` 看板 AM0 条目。
3. 更新 `04-当前进度状态/当前窗口.md` 推荐领取。
4. 若发现旧 pipeline 违规调用，更新 `00-当前架构事实/` 相关 ISSUE。
