# GAS ECS Runtime - Runtime Core 重构 - Unity Entities 机制校准

## 节点定位

本节点承接 AM0（Freeze Safety Gate），负责把 Unity Entities 1.4.6 的具体机制（SystemGroup、ISystem/IJobEntity、ECB playback、DynamicBuffer、Enableable、Blob/Baker、Query filter）校准为 Runtime Core 的实现模板。输出给 AM2（数据契约）和 AM3/AM5（实现迁移）作为机制级验收检查表。

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM1`

## 状态

契约已确立

## 兄弟关系

`sequential`。前置：AM0（Freeze Safety Gate）。后继：AM2（数据契约）、AM3（Instant Spec Evaluation）、AM5（ActiveEffectStore）依赖本节点的机制模板和 API 选型表。

## 拆分历史

从 `RuntimeCore重构` 拆分（S3 多关注点：机制校准与具体功能迁移关注点独立），2026-05-20。

## 领取轮次

第 2 轮（累计 1 轮已完成，契约已确立，Unity 验证待补跑）

## 当前进展

机制校准已在代码中产生合规范例（2026-05-26 审查确认）：
- `SAbilityTick`(64行) — 首个 `[BurstCompile]` + `IJobEntity` + `ScheduleParallel`，零 EM 操作（A+ 评级），为后续 System 迁移的目标模板
- `SEffectCommandSpecStreamPhases`(619行) — 6 个 partial ISystem，游标式 for 循环遍历 DynamicBuffer，无 ToEntityArray，无 ECB 碎片化——为 AM3 的正面实现范本
- `TagRequirementEvaluator` — 纯位掩码操作，零结构变化，可直接标记 `[BurstCompile]`
- `GASManager.cs` — 收口为 130 行纯 ECS 启动器
- `GASSystemScheduleContract.cs`(447行) — 8-phase 管线契约完整落地

这些合规范例验证了 AM1 机制校准的正确性。

## 本轮目标

本轮只补 `dotnet build` 验证和文档审计，不新增功能代码。如 LicensingClient 阻塞则记录环境阻塞。

## 当前问题

1. 当前目标态概念已明确，但 Runtime Core phase / stream 仍需要持续对照 Unity Entities 1.4.6 机制验证。
2. 如果不先校准 SystemGroup、ISystem/IJobEntity、ECB playback、DynamicBuffer、Enableable、Blob/Baker、Query filter，后续 AM2/AM3/AM5 可能继续以 request entity、runtime GE entity 或全局 buffer 扫描实现高频链路。
3. ISSUE-008 已将"目标态 Spec 尚未充分 Unity Entities 机制化"标记为 P1。

## 目标

1. 为 AM2 / AM3 / AM5 提供 Unity Entities 机制级实现模板。
2. 明确 Runtime Core SystemGroup 和 ECB playback 边界。
3. 明确高频 command data、DynamicBuffer、Enableable、Blob / Baker 的默认使用规则。
4. 建立后续代码改造前的机制级验收检查表和规则编号引用方式。

## 目标态约束摘要

（从关联 Spec 提取，与本任务直接相关的硬约束）

1. 所有 Runtime Core System 必须显式声明 `[UpdateInGroup]`，绑定到 `GASSystemScheduleContract` 的 8-phase 管线。
   > 来源：`03-RuntimeCore管线Spec.md`

2. 结构变化只能发生在 `GasStructuralPlaybackSystemGroup`，hot path 禁止直接 EntityManager 结构变化。
   > 来源：`03-RuntimeCore管线Spec.md`、`SC-01`

3. request entity 只作为边界低频入口，不作为 high-frequency instant GE 默认承载。
   > 来源：`04-EffectCommand-SpecStream-AttributeDeltaSpec.md`、不变量 #20

## 当前事实约束

（从关联 ISSUE 提取，本任务必须知道的架构事实）

1. ISSUE-008：目标态 Spec 尚未充分 Unity Entities 机制化。AM2 的数据契约是机制化的第一步，必须落到具体 unmanaged 类型。
   > 来源：`ISSUE-008`

2. ISSUE-004：结构变化边界脆弱——hot path 中散落的 `EntityManager.CreateEntity/DestroyEntity/AddComponent` 是 P0 缺陷。AM1 的机制模板必须明确"结构变化只有 StructuralPlaybackGate 可以做"。
   > 来源：`ISSUE-004`

## 适用规则摘要

| 规则 | 本任务约束 |
|---|---|
| `SYS-01` | System 必须显式 `[UpdateInGroup]`，不隐式依赖默认 group |
| `SYS-02` | SystemGroup 层次按 `GASSystemScheduleContract` 8-phase 定义，禁止旧 6-group 结构 |
| `SC-01` | 结构变化集中到 `GasStructuralPlaybackSystemGroup`，hot path 禁止 |
| `ECB-01` | ECB Playback 统一在 System 末尾，禁止中间 playback |
| `PRF-05` | 热路径优先 `IJobEntity`/`IJobChunk`，参考 `SAbilityTick` 模板 |
| `BUF-02` | singleton DynamicBuffer 仅主线程访问，不跨 job 共享写入 |
| `JOB-01` | Runtime Core 计算优先 Burst Job，参考 `SAbilityTick` 的 `[BurstCompile]` + `ScheduleParallel` |

## 非目标

1. 不在本任务迁移完整 EffectCommand 实现。
2. 不重写所有 Runtime system。
3. 不修改 Unity PackageCache。

## 执行范围

1. 目标态 Spec 与任务树——机制模板和 API 选型表。
2. 如进入代码实现阶段，可新增最小 `GASSystemScheduleContract` / SystemGroup skeleton / contract tests，但本任务优先保证设计校准完整。

## 执行细则

1. **SystemGroup 显式声明**：所有 Runtime Core System 必须说明使用的 SystemGroup，禁止隐式依赖默认 SimulationSystemGroup。
2. **Job 形态声明**：每个 System 必须说明使用 `ISystem` 主线程还是 `IJobEntity`/`IJobChunk` 并行，以及是否 `[BurstCompile]`。
3. **结构变化边界**：hot path 禁止 `EntityManager` 结构变化。request entity 只作为边界低频入口。
4. **API 选型表**：行动报告必须列出 API 选型表，覆盖 `SEL-01`~`SEL-05`、`SEL-09` 中与任务相关的规则。
5. **禁止 change filter 当事件语义**：不得把 Unity change filter 当实体级 gameplay event 语义使用。
6. **规则编号引用**：行动报告必须列出适用规则编号。

## 验收标准

1. AM2 / AM3 / AM5 的任务描述能直接引用 Unity Entities 机制校准 Spec。
2. Runtime Core 任务模板能要求 SystemGroup / ECB / buffer / enableable / blob / query / debugger evidence 和规则编号。
3. 当前事实 ISSUE-008 有退出条件和目标态入口。
4. `dotnet build` Runtime + Tests 通过；如 LicensingClient 阻塞，记录 return code 和关键日志。

## 测试链路

1. `git diff --check`
2. `dotnet build` Runtime + Tests
3. `rg -n "SystemGroup\|ISystem\|ECB playback\|Enableable\|DynamicBuffer\|Blob\|Baker\|Query filter" 当前路线`
4. Runtime EditMode tests；如 LicensingClient 阻塞，记录 return code 和关键日志。

## 交还内容

1. 更新本文件 `当前进展`、`领取轮次`、`状态` 字段。
2. 更新 `02-主线任务树/README.md` 根看板 AM1 条目。
3. 更新 `04-当前进度状态/当前窗口.md`。
4. 如发现 Spec 缺口，按 `04-当前进度状态/迭代摘要反哺流程规范.md` 反哺到 `01` 或 `00`。
5. 若 ISSUE-008 状态变化，更新 `00-当前架构事实/核心问题诊断/ISSUE-008`。
