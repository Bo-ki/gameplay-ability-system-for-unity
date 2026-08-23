# BUR-05：Editor Burst 结果不能替代目标 Player AOT 验证

**严重度**：P1
**Primary Owner**：Burst-AOT
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Unity `6000.3.14f1`；Burst `1.8.29`
**官方来源**：Burst `getting-started.md`、`building-projects.md`、`building-aot-settings.md`、`csharp-function-pointers.md`

## 规则声明
所有目标平台都必须以实际 Player 构建验证 Burst AOT 编译和运行结果；Editor Play Mode 的 JIT 编译结果不能替代 Player。使用 FunctionPointer 时还要覆盖 IL2CPP delegate/calling-convention 回调边界。

## 为什么
Editor 与 Player 使用不同编译阶段、构建设置、CPU 目标和平台工具链。差异不应概括成“只有 iOS/consoles 才 AOT”或未经证实的泛型限制；Burst 支持的 Player 入口在桌面等目标同样由构建流程 AOT 编译。

## EX-GAS 诊断
CI 至少构建所有发布目标；对关键平台运行 smoke/performance 验证。Android 是否使用 IL2CPP 以及具体 CPU 架构必须记录，不能用平台名代替构建后端。

## 检查方法
检查目标 Player 构建日志、Burst 编译诊断和运行时 smoke test；FunctionPointer 路径在实际 IL2CPP 目标上执行一次。
