# BUR-03：Player 性能报告必须包含 AOT、构建配置、架构和预热上下文

**严重度**：P1
**Primary Owner**：Burst-AOT
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Unity `6000.3.14f1`；Burst `1.8.29`
**官方来源**：Burst `building-aot-settings.md`、`compilation-synchronous.md`、`getting-started.md`

## 规则声明
Player 性能报告必须记录精确包版本、目标平台/构建后端、Development/Release、CPU 架构、Burst AOT/优化设置、安全检查相关配置、测试规模、采样区间和被排除的工作负载预热帧。

## 为什么
这些上下文会影响代码生成和测量结果。Burst 1.8.29 的默认 `Optimize For` 是 `Balanced`，不能默认写成 `Performance`。Player 中 Burst 入口是 AOT 编译，不存在“首帧 Burst JIT”；预热帧用于排除场景初始化、资源加载和缓存冷启动。

## EX-GAS 诊断
AutoChess 性能测试脚本将上述字段写入报告头，并区分预热阶段与稳定采样阶段。预热帧数量由测试场景收敛条件决定，不设伪装成官方结论的固定值。

## 检查方法
拒绝缺少构建配置或把 Editor 数据冒充 Player 数据的报告；检查稳定采样区间内实体规模与工作负载不再变化。
