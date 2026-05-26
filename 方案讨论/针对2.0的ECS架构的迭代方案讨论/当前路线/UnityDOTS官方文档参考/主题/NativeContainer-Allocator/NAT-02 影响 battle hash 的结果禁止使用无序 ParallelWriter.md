# NAT-02: 影响 battle hash 的结果禁止使用无序 ParallelWriter

**严重度**: P1
**Primary Owner**: NativeContainer-Allocator
**来源**: `10-Collections-Allocator-NativeStream.md` — ParallelWriter 的排序问题

## 规则声明
任何影响 battle 同步、replay、或确定性验证的输出路径，禁止使用 `NativeList.AsParallelWriter()`、`NativeHashMap.ParallelWriter` 等无序并行写入器。必须使用 NativeStream + deterministic merge 或顺序写入。

## 为什么
ParallelWriter 的写入顺序取决于线程调度，每次运行可能不同。battle-deterministic 场景要求相同输入产生完全相同的输出序列。

## EX-GAS 诊断
EffectCommand fan-in 使用 NativeStream + deterministic merge（collect + sort by target ASC），不使用 ParallelWriter。Telemetry/presentation outbox 可使用 ParallelWriter（不参与 battle hash）。

## 检查方法
Grep 搜索 `AsParallelWriter` 在 Runtime Core 中位于 battle-deterministic 路径的使用；若找到且无确定性保证注释则违规。
