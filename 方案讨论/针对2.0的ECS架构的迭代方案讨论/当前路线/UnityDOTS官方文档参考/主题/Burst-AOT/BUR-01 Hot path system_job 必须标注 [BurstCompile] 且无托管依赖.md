# BUR-01: Hot path system/job 必须标注 `[BurstCompile]` 且无托管依赖

**严重度**: P0
**Primary Owner**: Burst-AOT
**来源**: `09-Burst-编译-向量化-AOT.md` — Burst 编译器原理

## 规则声明
Runtime Core 中所有每帧执行的 system 和 job 必须标注 `[BurstCompile]`，其内部执行的代码必须满足 Burst 编译约束（无引用类型、无虚方法调用、无托管对象访问）。

## 为什么
未 Burst 编译的代码在托管环境下运行，性能比 Burst 编译后慢 10-100x。标注 `[BurstCompile]` 但内部调用了托管方法会导致静默降级为解释模式，无编译错误提示。

## EX-GAS 诊断
所有 `IJobEntity`/`IJobChunk`/`ISystem.OnUpdate` 在 Runtime Core 中都必须有 `[BurstCompile]`。GasRuntimeDebugger 应能检测和报告未 Burst 编译的 system。

## 检查方法
Burst Inspector 查看编译状态；Debugger 输出 `BurstCompiledSystemCount` 和 `NonBurstSystemCount`；代码审查搜索 hot path system 的 `[BurstCompile]` 缺失。
