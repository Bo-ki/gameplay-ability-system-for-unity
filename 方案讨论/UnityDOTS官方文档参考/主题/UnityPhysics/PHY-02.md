# PHY-02：Physics 适配器只产出输入，GAS Core 拥有规则裁决

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：EX-GAS 当前 Runtime Core；Unity Physics 1.4.6
**严重度**：P0
**Primary Owner**：UnityPhysics
**来源**：EX-GAS TargetData / Runtime Core 分层设计（非 Unity Physics 官方通用限制）

## 规则声明

Physics 查询和事件系统不得直接写 Attribute、GameplayEffect、Tag、Buff 等 GAS Core state。Physics adapter 只把候选实体及必要命中值转换成 TargetData 输入，最终状态变化由 GAS Core 系统执行。

## 确定性边界

Physics 候选会影响战斗结果时，必须满足至少一种项目协议：

1. Physics 输入在目标平台上被定义为权威输入，并纳入 battle hash/replay 记录；或
2. Physics 只做可替换的加速查询，候选集合与确定性空间索引产生的集合等价；或
3. 无头验收直接回放已经记录的 TargetData，而不重新运行 Physics。

不能仅凭“Physics 是输入层”推导有头/无头 battle hash 必然一致。

## 检查方法

确认 Physics assembly 只写 adapter/output 组件；所有 GAS Core state 修改都有明确的 Command/TargetData 入口，并检查候选顺序或集合语义是否稳定。
