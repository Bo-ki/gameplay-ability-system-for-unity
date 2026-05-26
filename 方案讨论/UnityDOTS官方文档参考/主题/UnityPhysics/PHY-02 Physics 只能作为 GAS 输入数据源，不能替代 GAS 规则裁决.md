# PHY-02: Physics 只能作为 GAS 输入数据源，不能替代 GAS 规则裁决

**严重度**: P0
**Primary Owner**: UnityPhysics
**来源**: 项目架构决定（EX-GAS Runtime Core 不依赖 Physics 做规则裁决）

## 规则声明
Physics 查询结果（碰撞命中、重叠检测）只能作为 GAS 候选目标列表的输入。伤害/治疗数值、Buff/BuffStack 决策、状态移除等必须由 GAS Attribute 系统、GameplayEffect 系统和 TargetData 规则系统裁决。Physics 不能直接修改 GAS 核心 state。

## 为什么
1) 无头 AutoChess 默认不启用 Physics，但必须保持 Core simulation hash 与有头模式一致；2) Physics 的 broadphase 延迟和浮点精度差异引入不确定性，影响 battle hash；3) 规则变化时（如修改伤害公式）只需要改动 GAS 层，不需要触及 Physics。

## EX-GAS 诊断
Debugger 输出 "Physics -> candidate list -> GAS rule filter" 的链路审计。

## 检查方法
Code Review 确认 Physics system 不直接读写 Attribute / GameplayEffect / Tag 等 GAS Core component。所有 Physics 输出经过 TargetData 规则层过滤。
