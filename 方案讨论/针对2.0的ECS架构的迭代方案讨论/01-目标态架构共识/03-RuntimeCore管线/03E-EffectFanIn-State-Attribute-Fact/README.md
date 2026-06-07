# 03E Effect Fan-In / State / Attribute / Fact 子 Spec 索引

> Owner：`01-目标态架构共识/03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact` | 状态：目标态 Spec 子目录 | 最近拆分：2026-06-08

本目录从 `../03E-EffectFanIn-State-Attribute-FactSpec.md` 拆出 03E 目标态正文。根 `03E` 文件只保留短索引，本目录内文件才是各主题正文 owner。

## 纯度规则

1. 本目录只回答 Runtime Core 中 effect fan-in、state evaluate、attribute reduce/apply 和 gameplay fact 应该如何设计。
2. 当前代码事实、文件行号、generated report 数字、执行流水、迁移进度和下一步任务不得写入本目录正文。
3. 现实证据必须回到 `../../../00-当前架构事实/`；任务拆分必须回到 `../../../02-主线任务树/`；短期接力必须回到 `../../../04-当前进度状态/`。
4. 需要 DOTS API 依据时，优先引用 `../../../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`、`21-官方文档覆盖与流程闭环.md` 和 `90-规则编号索引.md`。

## 子页索引

| 文件 | 职责 |
|---|---|
| [03E-01 Effect Fan-In](03E-01-EffectFanInSpec.md) | 目标态 GE command producer、NativeStream fan-in、deterministic merge 和 target ASC command buffer 写入。 |
| [03E-02 State Evaluate / ActiveEffect Store](03E-02-StateEvaluateActiveEffectStoreSpec.md) | 目标态 active effect slot 状态推进、period due owner 和 PostApply store 变更边界。 |
| [03E-03 Attribute Reduce / Apply](03E-03-AttributeReduceApplySpec.md) | 目标态 AttributeSet target grouped 写入、dirty mask、modifier buffer 和 attribute fact 输出。 |
| [03E-04 Gameplay Fact](03E-04-GameplayFactSpec.md) | 目标态 Core reaction fact 投影、death fact 示例和 Boundary Projection 分离规则。 |

## 反向入口

- 03E 根索引：[../03E-EffectFanIn-State-Attribute-FactSpec.md](../03E-EffectFanIn-State-Attribute-FactSpec.md)
- 03 Runtime Core 管线索引：[../README.md](../README.md)
- 01 总入口：[../../README.md](../../README.md)
