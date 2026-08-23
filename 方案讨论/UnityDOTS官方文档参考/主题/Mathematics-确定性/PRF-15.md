# PRF-15：确定性随机源必须显式归属

**严重度**：P1
**Primary Owner**：Mathematics-确定性
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Mathematics `1.3.3`
**官方来源**：Mathematics `random-numbers.md`、`compatibility.md`
**权威细则**：MAT-01、MAT-02

## 规则声明
影响 gameplay/replay 的随机数使用显式拥有和更新的 `Unity.Mathematics.Random` 或项目确定性随机服务；禁止该路径使用 `UnityEngine.Random` 和不可审计的 static Random state。

## 为什么
Mathematics.Random 是有可变状态的实例 struct，适合按逻辑 stream 隔离；UnityEngine.Random 虽可设置 seed/state，但共享全局状态容易被调用顺序污染。

## EX-GAS 诊断
报告 initial seed、owner、stable stream ID 与 consumption count；并行路径每个逻辑分区持有独立 state，不能按 worker thread 临时编号派生 replay stream。

## 检查方法
按 MAT-01、MAT-02 检查 seed、owner、写回、并行隔离和 Runtime Core 禁用边界。
