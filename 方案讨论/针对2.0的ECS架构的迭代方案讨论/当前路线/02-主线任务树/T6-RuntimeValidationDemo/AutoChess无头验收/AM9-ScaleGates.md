# Runtime Validation Demo - AutoChess 无头验收 - x50/x100/x1000/x10w/x100w Scale Gates

## 父节点

[AutoChess 无头验收](README.md)

## 任务ID

`T6-AutoChess-AM9`

## 状态

`后置`

## 关联主线

T5

## 当前问题

大规模压力测试需要 ScaleProfile 驱动和 generated runtime glue 支撑，当前 Runtime Core 语义仍在重构，规模 gate 后置。

## 目标态参考

1. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
2. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`

## 非目标

1. 不在 Runtime Core 未稳定前进行规模优化。
2. 不让 scale test 绕过 Ability / GE / Attribute / Facts contracts。

## 前置依赖

1. T1 Runtime Core 语义稳定。
2. T2 Luban 配置链路验收完成。
3. T5 Generated Runtime Glue 就位。
4. T4 Diagnostics 能解释 scale 热点。

## 执行范围

1. `Assets/AutoChessDemo/Config` ScaleProfile
2. AutoChess x50 / x100 / x1000 / x10w / x100w profile
3. Diagnostics export 与热点归因

## 验收标准

1. x50 / x100 / x1000 可自动运行并输出 diagnostics。
2. x10w / x100w 具备配置入口和采样策略。
3. 所有 scale 级别不绕过 command/spec/delta/fact contracts。
