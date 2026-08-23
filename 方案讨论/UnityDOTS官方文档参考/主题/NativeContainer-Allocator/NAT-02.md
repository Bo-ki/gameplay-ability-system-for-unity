# NAT-02：确定性结果不得依赖 ParallelWriter 的物理写入顺序

**严重度**：P0
**Primary Owner**：NativeContainer-Allocator
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Collections `2.6.6`
**官方来源**：Collections `parallel-readers.md`

## 规则声明
ParallelWriter 可以用于影响 battle hash 的数据收集，但消费者不得把其物理追加/迭代顺序当作业务顺序。必须在消费前按稳定全序排序、按稳定范围写入，或证明操作与顺序无关。

## 为什么
ParallelWriter 保证并发安全，不保证写入顺序；调度由操作系统和运行时决定。 blanket“禁止 ParallelWriter”会误伤排序后消费、按唯一 key 写 map 等合法模式。

## EX-GAS 诊断
EffectCommand 若用 ParallelWriter 收集，merge 必须使用含 tie-breaker 的业务 total key。Telemetry/Presentation 若不参与 gameplay，可明确标记顺序无关。

## 检查方法
审计 `AsParallelWriter` 和并行 map：检查消费者的顺序语义、完整 total key、重复 key 策略和 hash 输入是否规范化。
