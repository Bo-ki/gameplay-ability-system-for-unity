# T5 Burst / Generated 后置优化

## 节点定位

本主线负责语义稳定后的 Burst、Job、chunk 化、generated runtime glue 和规模化 gates。它是后置优化主线，不负责提前改变 GAS 语义。

## 当前问题

1. 当前 Runtime Core 语义仍在重构，过早 jobify 会固化错误管线。
2. ECS 优势需要在稳定数据契约、静态 lookup 和批处理 phase 上释放，而不是用生成代码掩盖旧生命周期成本。

## 目标态参考

1. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
3. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`

## 历史方案参考

1. `历史方案参考/方案10.md`、`方案11.md` 的 Burst-friendly、Luban -> Blob、static lookup 信号可参考。
2. `方案14.md`、`方案15.md` 的 x50 / x100 / x1000 规模 gate 可参考。
3. SourceGenerator 生成 gameplay lifecycle 的方向不可照搬。

## 主线目标

在 Runtime Core 语义稳定后推进 jobify、chunk 化、generated runtime glue 和 scale gates。

## 非目标

1. 不为了 Burst 提前压缩 GAS 概念。
2. 不绕过 Runtime Core Spec 生成 lifecycle。
3. 不把性能优化作为修复错误架构边界的替代方案。

## 执行范围

1. generated lookup / query glue。
2. Runtime Core 稳定后的 job 化系统。
3. AutoChess x50 / x100 / x1000 / 更高量级 gates。

## 执行细则

1. 只有当 T1 / T2 / T4 的关键 gates 成立后才推进。
2. generated glue 只能服务 static lookup、query 描述和 contract 检查。
3. 性能报告必须区分 bootstrap、export、observation 和 core simulation。

## 验收门槛

1. Core simulation 平均 tick 回到 ECS 合理区间。
2. Scale gate 放大后热点可被 diagnostics 精确解释。
3. generated glue 不引入 lifecycle 权威。

## 测试链路

1. AutoChess x50 / x100 / x1000 profile。
2. Runtime Core diagnostics summary。
3. Burst / jobs 安全性测试。

## 支线索引

| 支线 | 文档 | 状态 |
|---|---|---|
| Generated Runtime Glue | [GeneratedRuntimeGlue.md](GeneratedRuntimeGlue.md) | 后置 |

## 交还规则

交还时必须附带规模 gate 数据、diagnostics 解释、影响的 generated contract 和是否改变 Runtime Core 语义。
