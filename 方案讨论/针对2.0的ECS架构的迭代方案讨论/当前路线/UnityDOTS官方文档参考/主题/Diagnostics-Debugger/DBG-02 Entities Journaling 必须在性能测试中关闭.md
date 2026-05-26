# DBG-02: Entities Journaling 必须在性能测试中关闭

**严重度**: P1
**Primary Owner**: Diagnostics-Debugger
**来源**: 06-Diagnostics-Profiler-Journaling.md

## 规则声明
执行性能测试或 benchmark 前，必须确认 Entities Journaling 已关闭（Window > Entities > Journaling）。

## 为什么
Journaling 记录所有 ECS 操作流水，开销巨大，开启时跑性能测试得到的数据毫无意义。

## EX-GAS 诊断
AutoChess performance run 前自动检测 Journaling 状态，开启则阻止测试并提示关闭。

## 检查方法
性能测试启动脚本检查 journaling 状态；Debugger 输出 `JournalingActive = false`。
