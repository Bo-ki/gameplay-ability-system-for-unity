# DBG-02: Entities Journaling 必须在性能测试中关闭

**严重度**: P1
**Primary Owner**: Diagnostics-Debugger
**来源**: 06-Diagnostics-Profiler-Journaling.md

## 规则声明
执行性能测试或 benchmark 前，必须确认 Entities Journaling 已关闭（Window > Entities > Journaling）。Functional x1 的 official diff 可以临时启用 Journaling，但该次运行只能作为诊断证据，不能作为性能 benchmark。

## 为什么
Journaling 记录所有 ECS 操作流水，开销巨大，开启时跑性能测试得到的数据毫无意义。

## EX-GAS 诊断
AutoChess performance run 前自动检测 Journaling 状态，开启则阻止测试并提示关闭。Headless official diff 若临时启用 Journaling，结束时必须恢复原状态并清理本次采样产生的官方工具持久状态。

## 检查方法
性能测试启动脚本检查 journaling 状态；Debugger 输出 `JournalingActive = false`。诊断模式输出 `journalingCaptured=true` 时，必须同时输出该结果不参与 benchmark 的说明，并通过 Native leak trace 验证没有把 Journaling 缓存留给 Runtime Core 泄漏口径。
