# GAS ECS Runtime - Runtime Core 重构 - Freeze Safety Gate

## 节点定位

本节点是整个 Runtime Core 重构的入口闸门。承接旧 pipeline 的现状诊断（ISSUE-001/004/008），冻结旧 GE lifecycle fast path 扩张，建立目标态命名和契约边界。输出给 AM1（机制校准）和 AM2（数据契约）作为稳定的迁移边界。

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM0`

## 状态

契约已确立

## 兄弟关系

`sequential`。前置：无（本支线首个叶子节点）。后继：AM1（机制校准）、AM2（数据契约）依赖本节点建立的命名和边界。

## 拆分历史

从 `RuntimeCore重构` 拆分（S4 依赖链：必须先冻结旧 pipeline 扩张才能开始新路径迁移），2026-05-20。

## 领取轮次

第 2 轮（累计 1 轮已完成，契约已确立，Unity 验证待补跑）

## 当前进展

`GameplayEffectRuntimePipelineContract` 已建立，将 `LegacyInstantEntityLifecycle` 标记为 `LegacyFrozen`、`MigrationOnly`、`NoNewFeatureExpansion`、`NotDefaultNewBusinessPath`。旧 instant direct bypass API 已收敛命名为 `TryApplyLegacyInstantModifierBypass` / `ApplyLegacyInstantBypassOrCreateSingleTargetRequest`。`GameplayEffectLegacyBridge` 作为公开 migration-only 桥接已就位。`RuntimeCoreFreezeSafetyGateTests` 契约测试已覆盖。

2026-05-26 审查确认：`GASManager.cs` 已收口为 130 行纯 ECS 启动器，不再包含旧 pipeline 扩张入口。

## 本轮目标

本轮只补 `dotnet build` 验证和契约测试通过证据，不新增功能代码。如 LicensingClient 阻塞则记录环境阻塞。

## 当前问题

1. 旧 GE lifecycle pipeline 仍承担 simple instant GE、runtime entity、event projection 和 structural change 成本——这是 ISSUE-001/004 的根因。
2. 历史 fast path 没有解决 x50 曲线失真（ISSUE-001 中 `avgTickMs=13.77ms`），只是在旧管线上继续补丁。
3. 新业务如果继续接入旧 instant GE entity lifecycle，会扩大后续 AM2/AM3/AM5 迁移面。

## 目标

1. 冻结旧 GE lifecycle fast path 扩张——`LegacyInstantEntityLifecycle` 标记为 `LegacyFrozen`、`MigrationOnly`。
2. 新业务不得继续依赖 global observation stream 作为 high-frequency reaction 主输入。
3. 新增或外部保留入口必须用 `LegacyInstant...Bypass` 或 `EffectCommand / InstantEffectSpec / AttributeDelta` 术语，禁止使用 `FastInstant` 作为目标态命名。
4. 为 AM1/AM2 提供稳定迁移边界——旧管道不再接受新功能接入。

## 目标态约束摘要

（从关联 Spec 提取，与本任务直接相关的硬约束）

1. 新增业务默认不依赖旧 instant GE entity lifecycle；旧 pipeline 是 migration-only 冻结路径。
   > 来源：`03-RuntimeCore管线Spec.md`

2. 命名必须使用目标态术语：`EffectCommand / InstantSpec / AttributeDelta / ActiveEffectStore`，禁止 `FastInstant`。
   > 来源：`12-命名规范Spec.md`、`04-EffectCommand-SpecStream-AttributeDeltaSpec.md`

3. 新增或外部保留入口不得绕过 `GASSystemScheduleContract` 的系统调度方式。
   > 来源：`03-RuntimeCore管线Spec.md`

## 当前事实约束

（从关联 ISSUE 提取，本任务必须知道的架构事实）

1. 旧 GE lifecycle 的 per-hit entity create/destroy 是核心热点（ISSUE-001），`avgTickMs=13.77ms`。旧 pipeline 的 fast path 补丁是死胡同。
   > 来源：`ISSUE-001`、`ISSUE-004`

## 适用规则摘要

| 规则 | 本任务约束 |
|---|---|
| `SYS-02` | 新 System 必须绑定到 `GASSystemScheduleContract` 的 8 个 phase group，禁止挂到旧 6-group 结构 |
| `SEL-02` | 外部观察只通过 fact stream / presentation outbox / replay sink / read model，不通过旧 EventBus |
| `DEF-01` | pipeline contract 是只读定义层，不参与 runtime simulation |

## 非目标

1. 不在本任务迁移所有 GE（由 AM3/AM5 负责）。
2. 不改写完整 Attribute pipeline。
3. 不新增 AutoChess 业务机制。

## 执行范围

1. `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs` — 8-phase 管线契约
2. `Assets/GAS/Runtime/System/Effect/` — 旧 pipeline 入口的注释、guard、命名收敛
3. `Assets/GAS/Runtime/Effect/` — 旧 GE lifecycle 相关文件命名审计
4. `Assets/_Test/GAS/Runtime/` — 契约测试

## 执行细则

1. **命名必须使用目标态术语**：`EffectCommand / InstantSpec / AttributeDelta / ActiveEffectStore`。旧 pipeline 入口必须加 `Legacy` 前缀。
2. **不引入新的 managed event bus**：禁止新增 `EventBusHelper.Enqueue*` 静态调用路径。
3. **不绕过调度契约**：新系统必须显式绑定 `[UpdateInGroup]`，不隐式依赖默认 group。
4. **旧路径入口守卫**：如发现旧路径新增入口，必须写入禁止方向或契约测试。

## 验收标准

1. `dotnet build` Runtime + Tests 通过；如 LicensingClient 阻塞，记录 return code 和关键日志。
2. 文档明确旧 `LegacyInstantEntityLifecycle` 是 `MigrationOnly` + `NoNewFeatureExpansion`。
3. `rg "FastInstant" Assets/GAS/Runtime -g "*.cs"` 无新增匹配（已有旧路径保留 `Legacy` 前缀）。
4. `02` 看板明确 AM1/AM2 顺序，旧 pipeline 不再有新任务入口。

## 测试链路

1. `git diff --check`
2. `dotnet build` Runtime + Tests
3. `rg "FastInstant" Assets/GAS/Runtime -g "*.cs"`
4. Unity Runtime EditMode tests；如 LicensingClient 阻塞，记录 return code 和关键日志。

## 交还内容

1. 更新本文件 `当前进展`、`领取轮次`、`状态` 字段。
2. 更新 `02-主线任务树/README.md` 根看板 AM0 条目。
3. 更新 `04-当前进度状态/当前窗口.md`。
4. 若 ISSUE-001/004 相关事实变化，更新 `00-当前架构事实/核心问题诊断/`。
