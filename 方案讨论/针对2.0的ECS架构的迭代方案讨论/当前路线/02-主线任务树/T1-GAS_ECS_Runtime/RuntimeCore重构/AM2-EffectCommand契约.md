# GAS ECS Runtime - Runtime Core 重构 - EffectCommand 与 SpecStream 契约

## 节点定位

本节点承接 AM1（机制校准）和 AM0（Freeze Safety Gate），负责定义 `BEffectCommand → BInstantEffectSpec → BAttributeDelta → BTypedSimulationFact` 四阶段主链的 ECS 数据契约和 phase skeleton。输出给 AM3（Instant Spec Evaluation）作为迁移的稳定输入输出边界，输出给 AM5（ActiveEffectStore）作为 store-driven lifecycle 的数据契约。

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM2`

## 状态

契约已确立

## 兄弟关系

`sequential`。前置：AM1（机制校准）、AM0（Freeze Safety Gate）。后继：AM3（Instant Spec Evaluation）直接依赖本契约的四阶段数据定义和 phase skeleton；AM5 依赖 command stream 的 derived command 路径。

## 拆分历史

从 `RuntimeCore重构` 拆分（S3 多关注点：数据契约定义与 instant evaluation 迁移关注点独立），2026-05-20。

## 领取轮次

第 2 轮（累计 1 轮已完成，契约已确立，Unity 验证待补跑）

## 当前进展

ECS 数据契约已落地：`CEffectCommandSpecStream` singleton、`BEffectCommand` / `BEffectCommandSetByCallerValue` / `BInstantEffectSpec` / `BAttributeDelta` / `BActiveEffectMutation` / `BTypedSimulationFact` buffer 全部定义。`EffectCommandSpecStream` helper 提供 singleton / buffer ensure 与 legacy request bridge command 写入。Phase skeleton 已就位：`SEffectCommandIngest`、`SInstantEffectSpecBuild`、`SActiveEffectMutationApply`、`SAttributeDeltaApply`、`STypedSimulationFactProjection`，并接入 `GASSystemScheduleContract`(447行) 的 8-phase 管线。`GASRuntimeQueryLayoutPlan` 新增 `GameplayEffectCommandSpecStream` entry。`GasRuntimeDebugger` 在 AM2 stream 存在时把 command/spec/delta/fact 作为权威计数输入。`EffectCommandSpecStreamContractTests` 覆盖契约、context/SetByCaller 连续性、非默认实体化、schedule/layout 和 Debugger stream 计数。

2026-05-26 审查确认：`SEffectCommandSpecStreamPhases`(619行) 6 个 partial ISystem 基于游标 for 循环遍历 DynamicBuffer，无 ToEntityArray，无 ECB 碎片化——为 AM3 的正面实现范本。

## 本轮目标

本轮只补 `dotnet build` 验证和 runtime test 通过证据，不新增功能代码。如 LicensingClient 阻塞则记录环境阻塞。

## 当前问题

1. 旧 `CApplyGameplayEffectRequest → runtime GE entity → lifecycle` 仍是 simple instant GE 的主要落点（AM3 已部分迁移 activation/cost/Timeline）。
2. RuntimeCoreDebugger 的 AM1 counters 已有 baseline，但 spec / delta / fact 的权威 stream 计数仍需验证在 x50 场景下的计数正确性。
3. singleton DynamicBuffer 是当前 frame-local 契约落点，不得被任务交还描述为最终高规模承载——需要在验收中显式标记为 proof-only。

## 目标

1. `BEffectCommand → BInstantEffectSpec → BAttributeDelta → BTypedSimulationFact` 的具体 ECS 数据承载已定义。
2. 目标态 pipeline contract、schedule contract、query layout contract 都引用具体类型。
3. high-frequency instant GE 默认走 command data / DynamicBuffer，不默认创建 request entity 或 runtime GE entity。
4. 为 AM3 的 simple instant spec evaluation 迁移提供稳定输入输出边界。

## 目标态约束摘要

（从关联 Spec 提取，与本任务直接相关的硬约束）

1. simple instant GE 默认走 `BEffectCommand → BInstantEffectSpec → BAttributeDelta → BTypedSimulationFact` 四阶段主链，不创建 `CApplyGameplayEffectRequest` 或 runtime GE entity。
   > 来源：`04-EffectCommand-SpecStream-AttributeDeltaSpec.md`

2. `ContextId`、`ParentContextId`、`SetByCaller` 必须从 command 连续传递到 spec → delta → fact，不允许断链。
   > 来源：`04-EffectCommand-SpecStream-AttributeDeltaSpec.md`

3. 新 phase skeleton 可以进入显式 schedule，但不得在 AM2 中做 gameplay 写入副作用。
   > 来源：`03-RuntimeCore管线Spec.md`

4. singleton `DynamicBuffer` 是当前 frame-local 契约落点，不得在文档或报告中描述为 scale-ready 最终方案。
   > 来源：`20-GASRuntimeCore-API选型基线.md`、AM2B-C

## 当前事实约束

（从关联 ISSUE 提取，本任务必须知道的架构事实）

1. 旧 GE lifecycle 的 per-hit entity create/destroy 是核心热点（ISSUE-001）。AM2 的数据契约必须确保 command 写入时不产生 request entity。
   > 来源：`ISSUE-001`、`ISSUE-004`

2. 当前目标态 Spec 尚未充分 Unity Entities 机制化（ISSUE-008），AM2 的数据契约是机制化的第一步，必须落到具体 unmanaged `IComponentData` / `IBufferElementData`。
   > 来源：`ISSUE-008`

## 适用规则摘要

| 规则 | 本任务约束 |
|---|---|
| `SYS-01` | phase skeleton 显式声明 `[UpdateInGroup]`，绑定到 `GASSystemScheduleContract` 的 8 个 phase group |
| `BUF-01` | DynamicBuffer 容量在 `EnsureSingleton` 时显式指定，不允许隐式扩容 |
| `BUF-02` | singleton DynamicBuffer 仅主线程访问，作为 frame-local 契约落点 |
| `DEF-01` | definition 数据只读；command/spec/delta/fact 数据契约均为 unmanaged struct，不包含托管引用 |
| `QRY-01` | Query layout contract 使用 `RefRO`/`RefRW` 显式标记 command stream 的读写意图 |
| `SC-01` | phase skeleton 不做结构变化，仅做数据读取和 buffer 写入 |
| `SEL-01` | singleton DynamicBuffer 的 API 选型必须在交还中说明当前承载（proof-of-concept）和切换条件 |

## 非目标

1. 不在本任务迁移完整 GameplayEffect 执行链。
2. 不重写 Active Effect Store（由 AM5 负责）。
3. 不继续推进 AutoChess 业务拆分。

## 执行范围

1. `Assets/GAS/Runtime/Effect/Component/Dynamic/` — ECS 数据契约类型定义
2. `Assets/GAS/Runtime/System/Effect/` — phase skeleton ISystem
3. `Assets/GAS/Runtime/System/SystemGroup/` — schedule contract 绑定
4. `Assets/GAS/Runtime/Debugger/` — Debugger stream 计数
5. `Assets/_Test/GAS/Runtime/Effect/` — 契约测试

## 执行细则

1. **数据契约优先 unmanaged**：使用 unmanaged `IComponentData` / `IBufferElementData`，stream owner 使用 singleton entity。
2. **Context 连续性**：`ContextId`、`ParentContextId`、`SetByCaller` 必须从 command/spec/delta/fact 连续传递，契约测试覆盖断链检测。
3. **Phase skeleton 零副作用**：新 phase skeleton 只做数据读取和 buffer 写入，不在 AM2 中做 gameplay 写入副作用。
4. **Debugger 权威计数**：Debugger counters 优先统计 command/spec/delta/fact stream，而非旧 EventBus buffer。
5. **Proof-only 标记**：AM2 的 singleton DynamicBuffer 作为契约落点，交还时必须显式标记为 proof-of-concept，不声称 scale-ready。
6. **禁止临时 EntityQuery**：phase skeleton 中的 Query 必须通过 `SystemState.GetEntityQuery` 在 `OnCreate` 中创建，禁止在 `OnUpdate` 中调用 `EntityManager.CreateEntityQuery`（`PRF-33`）。

## 验收标准

1. `dotnet build` Runtime + Tests 通过；如 LicensingClient 阻塞，记录 return code 和关键日志。
2. Runtime pipeline contract 指向 `BEffectCommand`、`BInstantEffectSpec`、`BAttributeDelta`、`BTypedSimulationFact`。
3. Query layout 有 `GameplayEffectCommandSpecStream` entry，标记 `CommandDataBacked`、`NoPerHitStructuralChange`。
4. 契约测试证明 bridge command 写入 stream 时不创建 `CApplyGameplayEffectRequest` 或 runtime GE lifecycle entity。

## 测试链路

1. `git diff --check`
2. `dotnet build` Runtime + Tests
3. `rg -n "EffectCommandSpecStream|BEffectCommand|BInstantEffectSpec|BAttributeDelta|BTypedSimulationFact" Assets/GAS/Runtime Assets/_Test/GAS/Runtime -g "*.cs"`
4. Runtime EditMode tests；如 Unity LicensingClient 阻塞，记录 return code 和关键日志。

## 交还内容

1. 更新本文件 `当前进展`、`领取轮次`、`状态` 字段。
2. 更新 `02-主线任务树/README.md` 根看板 AM2 条目。
3. 更新 `04-当前进度状态/当前窗口.md`。
4. 若 ISSUE-008 状态变化，更新 `00-当前架构事实/核心问题诊断/ISSUE-008`。
