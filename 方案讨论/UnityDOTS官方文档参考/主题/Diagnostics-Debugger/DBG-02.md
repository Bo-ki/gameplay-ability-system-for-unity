# DBG-02: Entities Journaling 必须在性能测试中关闭

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**严重度**: P1
**Primary Owner**: Diagnostics-Debugger
**来源**: `entities-journaling.md`

## 规则声明
执行性能测试或 benchmark 前，必须确认 Entities Journaling 已关闭（Window > Entities > Journaling）。Functional x1 的 official diff 可以临时启用 Journaling，但该次运行只能作为诊断证据，不能作为性能 benchmark。

## 为什么
Journaling 记录 ECS 操作并消耗额外 CPU 与内存。开启时的数据仍可用于诊断，但不能与关闭 Journaling 的基准混为同一性能口径。

## EX-GAS 诊断
AutoChess performance run 前自动检测 Journaling 状态，开启则阻止测试并提示关闭。Headless official diff 若临时启用 Journaling，结束时必须恢复原状态并清理本次采样产生的官方工具持久状态。

## 检查方法
性能测试启动脚本检查 journaling 状态；Debugger 输出 `JournalingActive = false`。诊断模式输出 `journalingCaptured=true` 时，必须同时输出该结果不参与 benchmark 的说明，并通过 Native leak trace 验证没有把 Journaling 缓存留给 Runtime Core 泄漏口径。
