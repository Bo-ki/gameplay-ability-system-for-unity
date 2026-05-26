# PRF-25: 每个并行 Job 使用独立 ECB

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-entity-command-buffers.md`

## 规则声明

多个并行 job（IJobEntity / IJobChunk）各自使用独立的 `EntityCommandBuffer` 实例，禁止多个 job 复用同一 ECB。若复用，当 job 使用相同 sortKey 域（如均为 `[ChunkIndexInQuery]`）时，命令会交错而非依序排列。

## 为什么

复用 ECB 不会产生异常或安全错误——静默的交错排序。确定性回放在复用 ECB 时被破坏（CASE-35 原则）。不同 job 记录的 ECB 命令应独立回放，交错后导致 playback 顺序不可预测。

## EX-GAS 诊断

GAS frame backbone 中，Attribute delta 计算 job 和 GE application job 若使用同一 ECB → 命令交错。每个并行 job 必须从 ECB System 独立 `CreateCommandBuffer`。

## 检查方法

审计 `ISystem.OnUpdate` 中 `CreateCommandBuffer` 调用次数与并行 job 数量的对应关系。
