# NAT-03: NativeStream 并行 fan-in 必须定义 merge 顺序和内存预算

**严重度**: P0
**Primary Owner**: NativeContainer-Allocator
**来源**: `10-Collections-Allocator-NativeStream.md` — NativeStream / Deterministic Merge 要求

## 规则声明
使用 NativeStream 进行并行 fan-in 时，必须：1) 定义明确的 deterministic merge 策略（collect → sort by 固定 key → dispatch）；2) 设定 segment 数量和每个 segment 的内存预算上限；3) merge 结果必须经过排序后再写入目标 store。

## 为什么
NativeStream 的 segment 数量随线程数变化而波动。forEachCount 遍历顺序不是确定性的。不排序直接 merge 会产生非确定性输出，破坏 battle hash。

## EX-GAS 诊断
EffectCommand fan-in 的管线设计：Command Ingest phase 并行写入 NativeStream → Spec Evaluation phase 收集排序 → Delta Apply phase 顺序应用。Debugger 报告 NativeStream segment count / 帧、merge cost。

## 检查方法
每个 NativeStream 使用点检查 merge 阶段是否有排序步骤；Debugger 报告 segment 数量和 merge 耗时。
