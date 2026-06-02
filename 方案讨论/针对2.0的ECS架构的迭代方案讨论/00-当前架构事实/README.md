# 00 当前架构事实

> 上次更新：2026-06-02 | 审查范围：`Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime` + `Assets/AutoChessDemo`

本目录维护当前版本的架构事实、核心问题诊断和合规审查。当前事实以现实代码为第一性参考；旧路线文档、旧目标态文档和旧 issue 结论只能作为历史背景。

## 文件索引

| 文件 | 内容 |
|---|---|
| [Runtime主链事实](Runtime主链事实.md) | 当前 5 段 GAS 主链、generated runtime 接入、command/spec/delta/fact 链 |
| [当前架构图](当前架构图.md) | 当前实际链路图、EffectCommand 链路、DOTS 对照热图 |
| [模块索引](模块索引.md) | Runtime / Generated Runtime / AutoChessDemo / 配置生成当前索引 |
| [AutoChessDemo事实](AutoChessDemo事实.md) | 当前业务 demo 分层、GAS bridge、demo ECS 扩展和风险 |
| [Definition配置事实](Definition配置事实.md) | runtime catalog blob、managed table、generated glue、baking contract 当前状态 |
| [P0-致命缺陷](P0-致命缺陷.md) | 当前最高风险：boundary 写入口、`Complete()`、generated runtime proof、singleton stream |
| [P1-高风险缺陷](P1-高风险缺陷.md) | 物理/逻辑 phase 并存、EventBus 迁移、managed registry、AutoChess bridge |
| [P2-改进建议](P2-改进建议.md) | 文档口径、证据拆分、旧文件名清理、contract/proof 标注 |
| ISSUE-001~011 | 当前核心问题按现实代码重审后的单项诊断 |

## 当前核心事实

1. Runtime 主链已经不是旧 `GASEffectGroup / GASAbilityGroup / GASCueGroup / GasStructuralPlaybackSystemGroup`。
2. 当前物理主链是：
   - `GASFramePrepareSystemGroup`
   - `GASCommandResolveSystemGroup`
   - `GASCoreSimulationSystemGroup`
   - `GASStructuralCommitSystemGroup`
   - `GASBoundaryProjectionSystemGroup`
3. `GEExecutionCalculationExtensionSystemGroup` 是 CoreSimulation 内扩展插槽。
4. `RuntimeSystemRegistration.gen.cs` 已把 7 个 generated systems 注册进主链，generated runtime 是当前执行事实的一部分。
5. `GASDefinitionCatalogBlob` 已被 generated runtime 读取；旧 managed config/prototype path 不能再代表 hot path。
6. `ToEntityArray` 主要留在 Debugger observation 和 AutoChess catalog 低频安装路径；当前热路径风险重点是 `SystemAPI.Query`、DynamicBuffer for loop、`state.Dependency.Complete()`、singleton stream owner。
7. AutoChessDemo 当前已恢复为业务分层 demo，不是“删除后待重构”状态。

## 核心问题看板

| ID | 当前问题 | 状态 | 严重度 |
|---|---|---|---|
| ISSUE-001 | GE 生命周期从 request/entity pipeline 迁入 command/spec/active store，但 legacy fallback 和 generated proof 仍未闭合 | Active | P0 |
| ISSUE-002 | Observation 已进入 BoundaryProjection，但 legacy EventBus 与 typed fact 仍并存 | Active | P1 |
| ISSUE-003 | Debugger/Official diff 已有工具，但证据还需区分 Core 与 Boundary 成本 | Active | P1 |
| ISSUE-004 | StructuralCommit gate 已真实存在，但 boundary facade/bridge 仍能直接结构变化 | Active | P0 |
| ISSUE-005 | Generated 链路已反哺 Runtime Core，但也成为新的 DOTS 审查对象 | Active | P0 |
| ISSUE-006 | AutoChessDemo 已移出 Runtime Core；当前风险转为 bridge 直接 `EntityManager` | Mitigated | P1 |
| ISSUE-007 | 文档口径仍需持续防止旧事实回流 | Active | P2 |
| ISSUE-008 | DOTS 官方机制已部分进入规则，但 contract 与 runtime proof 需继续拆分 | Active | P1 |
| ISSUE-009 | 5 段主链已存在；frame/query/stream owner 仍未完全目标态化 | Active | P1 |
| ISSUE-010 | 执行范式从大量 `ToEntityArray` 转为主线程 `SystemAPI.Query` / buffer loop / `Complete()` 风险 | Active | P0 |
| ISSUE-011 | 临时 query 泛滥旧口径已缓解；当前 API 承载风险集中在 singleton owner 和 global facade | Active | P1 |

## 关键数据点

- `Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime` 当前共检出 39 个 `ISystem` 类型。
- Runtime 中仍有 `GASManagerInputSystem : SystemBase`，但未进入 5 段 GAS 主链注册。
- `state.Dependency.Complete()` 当前命中 10 处，涉及 destroy/finalize、ExecutionCalculation、generated ActiveEffect tick/remove 等路径。
- `ToEntityArray()` 当前命中主要在 `GasRuntimeDebugger` 和 `AutoChessBattleDefinitionCatalogBuilder`。
- AutoChessDemo 当前 2 个 demo ECS systems 插入 current GAS groups：command drive 和 execute calculation extension。

## 当前总诊断

当前架构已经从旧 lifecycle/request entity 堆叠推进到“5 段物理主链 + generated catalog runtime + command/spec/delta/fact proof + typed fact / legacy bridge 并存”的迁移期。方向有实质进展，但不能宣称架构已优秀。

当下最需要治理的是：

1. Runtime Boundary 仍直接创建 request entity。
2. generated runtime 进入主链后仍缺少 DOTS 级 job/dependency/ordering 证据。
3. singleton DynamicBuffer stream 是 proof carrier，不是 scale-ready 终局。
4. StructuralCommit gate 需要 Journaling/Profiler 证明来源和相位。
5. AutoChess bridge 需要把直接 `EntityManager` 操作从业务 adapter 中继续收口。

## 边界

1. 只写已对照当前代码成立的事实。
2. 不写目标态设想，目标态见 [01-目标态架构共识](../01-目标态架构共识/README.md)。
3. 不写任务状态，任务状态见 [02-主线任务树](../02-主线任务树/README.md)。
4. Contract、Plan、Spec 不是完成证明；完成度必须来自当前执行链和证据工具。
