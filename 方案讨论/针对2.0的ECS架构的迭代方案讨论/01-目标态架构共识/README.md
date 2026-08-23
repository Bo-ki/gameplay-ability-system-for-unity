# EX-GAS Runtime v1 目标态架构共识

## 结论

默认目标路线是不可兼容的 Runtime v1：单 FixedStep/PostPhysics Kernel、ASC-local generational slabs、target single writer、stable-state deferred reaction、cleanup outbox + single Drain、非预测。旧五组/Ability Entity/legacy GE 路线不再是目标态。

## 首读

1. [00 总览](00-总览Spec.md)
2. [90 不变量](90-目标态不变量.md)
3. [91 术语表](91-术语表.md)
4. [01 GAS 概念模型](01-GAS概念模型Spec.md)
5. [01B GAS 业务语义链](01B-GAS业务语义链路概念设计Spec.md)
6. [03 Runtime Core](03-RuntimeCore管线Spec.md)
7. [13 物理布局](13-EntityComponent物理布局Spec.md)
8. [16 Core/Boundary 重划分](16-纯ECS内核与边界重划分Spec.md)
9. [17 破坏性迁移](17-GAS业务链路破坏性重划分Spec.md)
10. [24 UE GAS 对照](24-GAS官方概念对照复核Spec.md)
11. [10B-08 真实业务链二轮裁决](10B-AutoChess完整业务案例/10B-08-真实业务链二轮审查与疑点裁决Spec.md)

## 专题 Owner

| 主题 | 唯一正文 |
|---|---|
| 四层与 Session 边界 | [02](02-四层架构Spec.md) |
| Ability/Target/Continuation | [03D](03-RuntimeCore管线/03D-CommandResolve与TargetResolveSpec.md) |
| Effect/State/Attribute/Fact | [03E](03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-FactSpec.md) |
| ApplicationSpec/Capture | [04](04-EffectCommand-SpecStream-AttributeDeltaSpec.md) |
| ActiveEffect slab | [05](05-ActiveEffectStoreSpec.md) |
| Boundary/Cue/Replay | [06](06-Observation-Presentation-ReplaySpec.md) |
| DOTS API/性能红线 | [18](18-DOTS官方规范复核与性能红线Spec.md) |
| Definition/Luban/SourceGenerator | [08](08-Luban-SourceGenerator配置生成链路Spec.md)、[14](14-DefinitionCodeGen目标链路Spec.md)、[15](15-SourceGenerator职责边界Spec.md) |
| AutoChess 真实业务链裁决 | [10B-08](10B-AutoChess完整业务案例/10B-08-真实业务链二轮审查与疑点裁决Spec.md) |

## 文档治理

本目录只写理想 contract、owner、时序、数据布局、禁止方向和验收门。当前代码/测试事实写 `../00-当前架构事实/`；实施任务写 `../02-主线任务树/`；短交接写 `../04-当前进度状态/`；历史设计写归档，不反向覆盖本目录。

目录里的 `_归档` 仅供追溯，不参与当前目标裁决。
