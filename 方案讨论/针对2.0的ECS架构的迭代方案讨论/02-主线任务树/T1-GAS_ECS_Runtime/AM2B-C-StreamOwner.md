# GAS ECS Runtime - Runtime Core Frame Backbone - Stream Owner 与 Deterministic Merge

## 父节点

[AM2B-FrameBackbone](README.md)

## 任务ID

`T1-RuntimeCore-AM2B-C`

## 状态

已完成（contract-first，Unity验证待补跑）

## 当前问题

1. `EffectCommandSpecStream` 当前以 singleton DynamicBuffer 承载 command/spec/delta/fact。
2. 当前 stream 有 frame-local clear，但没有统一 owner 表和 deterministic merge policy。
3. 百万实体目标下，单一全局 buffer 需要被证明为 proof-only 或重新选型。

## 目标 / 目的

1. 定义 command / spec / delta / fact / active mutation 的 frame owner 表。
2. 明确 clear / write / read / merge phase。
3. 给出 singleton DynamicBuffer、per-owner buffer、NativeStream、ECB append 的采用 / 拒绝理由和重新选型触发条件。

## 执行范围

1. `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs`
2. `Assets/GAS/Runtime/System/Effect/SEffectCommandSpecStreamPhases.cs`
3. `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs`

## 执行细则

1. 不必立即切换 NativeStream，但必须明确何时切换。
2. 影响 gameplay result 的 merge 必须 deterministic。
3. Debug telemetry 和 presentation marker 不得混入 gameplay deterministic stream。

## 验收标准

1. 存在 stream owner 表。
2. `EffectCommandSpecStream` 被明确标记为 proof-only、migration carrier 或 scale-ready carrier。
3. 有 deterministic output policy 和 battle hash / equivalent evidence 入口。

## 测试链路

1. `git diff --check`
2. `rg -n "StreamOwner|DeterministicMerge|ProofOnly|NativeStream|MergePolicy" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`

## 本轮进展

1. 新增 `GASRuntimeFrameStreamOwnerContract`，覆盖 `EffectCommand`、`EffectCommandSetByCaller`、`InstantEffectSpec`、`ActiveEffectMutation`、`AttributeDelta` 和 `TypedSimulationFact` 六类 stream。
2. 当前 `EffectCommandSpecStream` 六个 buffer slot 均被标记为 `SingletonDynamicBuffer` + `MigrationCarrier`；目标承载按访问模式分别指向 `PerThreadNativeStream`、`OwnerLocalDynamicBuffer` 或 command range auxiliary buffer。
3. 每个 stream owner entry 已声明 clear / write / read / merge phase、internal buffer capacity、deterministic merge policy、sort key、battle hash 输入和重新选型触发条件。
4. 重新选型触发覆盖 x50 buffer pressure、x1000 scale gate 等。
5. 新增 `RuntimeStreamOwnerContractTests`。
