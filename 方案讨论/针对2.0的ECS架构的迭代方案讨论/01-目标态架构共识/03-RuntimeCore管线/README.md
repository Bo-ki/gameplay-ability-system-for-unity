# 03 Runtime Core 管线 Spec 索引

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态 Spec 子目录 | 最近拆分：2026-06-07

本目录从 `../03-RuntimeCore管线Spec.md` 拆出 Runtime Core 管线目标态正文。根 `03-RuntimeCore管线Spec.md` 只保留总览、阅读路径和跨文档索引；本目录内文件才是各主题的目标态正文 owner。

## 纯度规则

1. 本目录只回答 Runtime Core 目标态应该如何设计、为什么这样设计、如何验收。
2. 当前代码事实、文件行号、generated report 数字、`MigrationProofOnly` 实现证据、执行流水和下一步任务不得写入本目录正文。
3. 需要引用当前事实时，只能链接到 `../../00-当前架构事实/`；需要拆任务时，只能链接到 `../../02-主线任务树/`。
4. 新增 DOTS API 依据时，先进入 `../../../UnityDOTS官方文档参考/`，再反哺本目录的目标态约束。

## 子 Spec 索引
| 文件 | 职责 |
|---|---|
| [03A 执行域与数据流](03A-执行域与数据流Spec.md) | 目标态 Runtime Core 的目的、官方依据、物理执行域、数据流、SystemGroup、Phase 和官方交叉审查。 |
| [03B 业务调用链与配置消费](03B-业务调用链与配置消费Spec.md) / [03B 子页索引](03B-业务调用链与配置消费/README.md) | 目标态业务调用链、Luban 配置生成链 Runtime 消费链和 Generated Runtime Glue 真实消费接口；根文件只保留短索引，正文在同名子目录。 |
| [03C SystemGroup 合约与核心数据形态](03C-SystemGroup合约与核心数据形态Spec.md) | 目标代码骨架中的 SystemGroup 合约、核心组件、buffer、frame context、command/fact/outbox 数据形态。 |
| [03D Command Resolve 与 Target Resolve](03D-CommandResolve与TargetResolveSpec.md) | 目标态 Ability command ingest、Core Ability Producer、NativeStream command records、target resolve 和 request-owned TargetDataBuffer。 |
| [03E Effect Fan-In / State / Attribute / Fact](03E-EffectFanIn-State-Attribute-FactSpec.md) | 目标态 effect fan-in、active effect store、attribute reduce/apply 和 gameplay fact 数据流。 |
| [03F Structural Commit 与 Boundary Projection](03F-StructuralCommit与BoundaryProjectionSpec.md) | 目标态 structural intent、ECB playback、presentation outbox 和 boundary projection 只读派生。 |
| [03G Component 矩阵 / Frame Arena / Job 拓扑](03G-Component矩阵-FrameArena-Job拓扑Spec.md) | 目标态 per-phase component 读写矩阵、Frame Arena 物理设计、Unity Entities 承载映射和 Job 依赖拓扑。 |
| [03H DOTS API 策略与 Backbone 验收](03H-DOTSAPI策略与Backbone验收Spec.md) | 目标态 DOTS API 选型修正、Enableable 策略、Chunk Component 策略、DOTS Backbone First 顺序和 API 预算。 |
| [03I System / Lane Catalog 与禁止方向](03I-SystemLaneCatalog与禁止方向Spec.md) | 目标态 System/Lane Catalog、禁止方向和历史方案定位。 |
## 反向入口

- 03 总览：[../03-RuntimeCore管线Spec.md](../03-RuntimeCore管线Spec.md)
- 01 总入口：[../README.md](../README.md)
- DOTS API 选型基线：[../../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md](../../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md)
- 官方文档覆盖流程：[../../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md](../../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md)
