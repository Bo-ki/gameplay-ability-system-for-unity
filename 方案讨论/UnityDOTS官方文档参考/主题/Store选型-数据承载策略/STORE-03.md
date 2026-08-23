# STORE-03：Store 选型按数据性质与运行契约分类

**严重度**：P1
**Primary Owner**：Store选型-数据承载策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`；Collections `2.6.6`
**官方来源**：无官方四分类；各容器事实见 `components-buffer-introducing.md`、`components-nativecontainers.md`、Collections `parallel-readers.md`

## 规则声明
运行时数据先标记 gameplay state、gameplay transient、telemetry 或 presentation，再根据 owner、生命周期、并发、访问和顺序契约选择容器。分类不直接等于某个固定容器。

## 为什么
短生命周期的 EffectCommand 仍影响 gameplay，必须确定；跨帧 gameplay state 也不一定只能在 chunk 内；presentation 是否允许丢帧由具体语义决定。旧文档把分类与容器一一绑定会产生错误决策。

## EX-GAS 诊断
- ActiveEffect/Attribute/Tag 权威状态：gameplay state；owner-local component/buffer 为主要候选。
- EffectCommand/AttributeDelta/SimulationFact：gameplay transient；明确 deterministic merge。
- Debug metrics：telemetry；有容量与采样预算，不反馈 gameplay。
- Cue/UI/VFX request：presentation；明确丢弃/合并/时延策略。
- Blob/generated table：只读定义输入，单独声明创建与释放 owner。

## 检查方法
每个新 Store 决策记录分类及六个选型维度；若只写“transient → NativeStream”或“gameplay → DynamicBuffer”，视为论证不足。
