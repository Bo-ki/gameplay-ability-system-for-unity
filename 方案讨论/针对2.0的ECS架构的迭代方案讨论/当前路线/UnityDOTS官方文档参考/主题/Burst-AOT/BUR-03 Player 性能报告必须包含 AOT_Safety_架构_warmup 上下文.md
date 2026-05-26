# BUR-03: Player 性能报告必须包含 AOT/Safety/架构/warmup 上下文

**严重度**: P1
**Primary Owner**: Burst-AOT
**来源**: `09-Burst-编译-向量化-AOT.md` — Burst AOT 与 Player

## 规则声明
所有提交的 Player 性能报告必须包含：Burst AOT 是否启用、`OptimizeFor` 设置、Safety Checks 状态、CPU 架构（x64/ARM64）、Burst warmup 帧数。

## 为什么
缺少 AOT 上下文的性能数据无法跨平台比较。Safety Checks 在 Player 中关闭带来的性能收益应被量化记录。Burst warmup 帧（首帧 JIT 编译）的成本极高，必须从稳定帧数据中排除。

## EX-GAS 诊断
AutoChess Player 性能测试脚本自动采集 Burst 配置参数并附加到性能报告中。Debugger 输出 `BurstWarmedUp` 标志，warmup 帧不计入稳定帧统计。

## 检查方法
性能报告头部检查强制字段；Player 启动后首 N 帧的 timing 数据标记为 warmup 阶段。
