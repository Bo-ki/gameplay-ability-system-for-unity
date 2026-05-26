# GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget

## 父节点

[AM2B-FrameBackbone](README.md)

## 任务ID

`T1-RuntimeCore-AM2B-B`

## 状态

已完成（contract-first / budget-first，Unity验证待补跑）

## 当前问题

1. `EffectCommandSpecStream.TryGetSingleton` 和 `ResolveCurrentFrame` 仍有 helper 级临时 query，但 AM2B-B 已把它们写入 frame budget risk contract。
2. Debugger 已输出 query / lookup / allocator / dependency budget 字段。
3. Runtime Core 已有 frame arena owner contract；真实 `GasRuntimeFramePrepareSystemGroup` / allocator 生命周期落地仍留给后续节点。

## 目标 / 目的

1. 建立 Frame Arena / Query Preparation 的契约或最小实现。
2. 明确 Runtime Core 每帧 query / lookup / allocator / dependency 的 owner。
3. 输出可被 Debugger 消费的 budget 数据结构或 contract。

## 执行范围

1. `Assets/GAS/Runtime/System/SystemGroup`
2. `Assets/GAS/Runtime/System/Effect`
3. `Assets/GAS/Runtime/Debugger`
4. 对应 Runtime tests。

## 执行细则

1. 不在本任务迁移所有 helper。
2. 优先将 helper 级 query 标记并收束到 frame prepare contract。
3. allocator 必须说明 `WorldUpdateAllocator`、system group allocator 或 Rewindable allocator 的 owner 和生命周期。
4. dependency wait 先做可记录口径，不要求一次性全 job 化。

## API 选型

| 候选 | 本任务用途 | 采用 / 拒绝口径 |
|---|---|---|
| EntityQuery / EntityQueryBuilder | query contract | 必须声明 All / Any / None / filter |
| ComponentLookup / BufferLookup | lookup budget | 必须记录 update count 和随机访问风险 |
| WorldUpdateAllocator | frame scratch | 优先评估 |
| RewindableAllocator | 批量 frame scratch | 需要 owner 和 rewind 时机 |

## 验收标准

1. Runtime Core 有 query / lookup / allocator / dependency budget 口径。
2. helper 临时 query 风险被记录或迁移到 frame prepare 入口。
3. Debugger 后续可读取这些预算字段。

## 测试链路

1. `git diff --check`
2. `rg -n "QueryBudget|LookupBudget|AllocatorOwner|DependencyBudget|WorldUpdateAllocator|Rewindable" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`

## 本轮进展

1. 新增 `GASRuntimeFrameBudgetContract`，以 contract-first / budget-first 方式声明 Runtime Core 当前 query / lookup / allocator / dependency 预算。
2. 预算表覆盖 FramePrepare owner、`EffectCommandSpecStream` helper query、Runtime Debugger counter query、Presentation / Replay projection 等。
3. `EGasRuntimeFrameBudgetRisk` 已显式标记 helper temp query、sync query、dependency wait、ToEntityArray temp、random lookup、Debugger observation 等风险。
4. `GasRuntimeDebugger` 已将 frame budget totals 接入 diagnostic counters、snapshot 和文本导出。
5. 新增 `RuntimeFrameBudgetContractTests`。
