# MAT-02：Random state 必须显式归属并写回

**严重度**：P1
**Primary Owner**：Mathematics-确定性
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Mathematics `1.3.3`
**官方来源**：Mathematics `random-numbers.md`

## 规则声明
每个 `Unity.Mathematics.Random` 必须有明确 owner、非零初始 seed、稳定逻辑 stream ID 和更新写回路径。owner 可以是 component、system-owned NativeContainer 或单次 job 的显式输入/输出；不得用不可审计的静态共享字段。

## 为什么
`Unity.Mathematics.Random` 是有可变状态的 struct。按值读取后调用 `Next*` 只推进副本；若不写回 owner，后续会重复序列。多个 worker 也不能并发修改同一个实例。

## EX-GAS 诊断
Debugger 可记录 initial seed、owner、stream ID 和 consumption count。计数是项目诊断信息，不是 Random API 的组成部分。

## 检查方法
审查构造 seed 非零、并行分区使用稳定业务键、state 修改后写回，以及不存在共享可变 Random 的并发写。
