# Runtime Core 管线 Spec

> Owner：`01-目标态架构共识` | 状态：目标态总览入口 | 最近拆分：2026-06-07

本文件只保留 Runtime Core 管线目标态的总览、阅读路径和跨文档索引。详细目标态正文已拆到 [03 Runtime Core 管线 Spec 索引](03-RuntimeCore管线/README.md)。拆分前全文快照已归档到 [_归档/2026-06-07-03-RuntimeCore管线Spec拆分前.md](_归档/2026-06-07-03-RuntimeCore管线Spec拆分前.md)，只用于历史追溯。

## 目的

Runtime Core 管线目标态定义 EX-GAS 2.0 的 ECS Gameplay Core：以 Unity DOTS 的 SystemGroup、ISystem、Job、ECB、DynamicBuffer、Blob、NativeContainer 和 Debugger evidence 为第一性约束，形成可调度、可验证、可归因、可扩展的纯 ECS 内核。

本 Spec 不记录当前实现状态。当前事实写入 `../00-当前架构事实/`；可领取任务写入 `../02-主线任务树/`；短期验证与未跑项写入 `../04-当前进度状态/`。

## 核心目标态

1. Runtime Core 物理执行域固定为 `GASFramePrepareSystemGroup -> GASCommandResolveSystemGroup -> GASCoreSimulationSystemGroup -> GASStructuralCommitSystemGroup -> GASBoundaryProjectionSystemGroup`。
2. CoreSimulation 内部以 lane 表达业务阶段，不默认继续拆新 SystemGroup。
3. command、spec、delta、fact、outbox、active store 必须按数据性质选择 owner-local、NativeStream、DynamicBuffer、Blob 或 structural intent carrier。
4. 结构变化只能进入 StructuralCommit playback；hot path 不直接执行 EntityManager 结构变化。
5. Debugger evidence 是 Runtime Core 设计的一部分，必须能解释 query、allocator、dependency、buffer pressure、random lookup、ECB 和 official tool state。
6. Definition / Generation 只输出 immutable catalog、static lookup、pure glue 和 validation graph，不拥有 gameplay lifecycle。

## 子 Spec 阅读路径

| 顺序 | 子 Spec | 何时读 |
|---|---|---|
| 1 | [03A 执行域与数据流](03-RuntimeCore管线/03A-执行域与数据流Spec.md) | 需要理解 Runtime Core phase、SystemGroup、数据流和官方依据 |
| 2 | [03B 业务调用链与配置消费](03-RuntimeCore管线/03B-业务调用链与配置消费Spec.md) | 需要理解业务调用链、Luban 生成链和 generated glue 消费方式 |
| 3 | [03C SystemGroup 合约与核心数据形态](03-RuntimeCore管线/03C-SystemGroup合约与核心数据形态Spec.md) | 需要查看目标代码骨架中的 group contract、frame context 和核心 data/buffer |
| 4 | [03D Command Resolve 与 Target Resolve](03-RuntimeCore管线/03D-CommandResolve与TargetResolveSpec.md) | 需要设计 command ingest、ability producer、target resolve 和 target data buffer |
| 5 | [03E Effect Fan-In / State / Attribute / Fact](03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-FactSpec.md) | 需要设计 effect fan-in、active effect store、attribute reduce 和 gameplay fact |
| 6 | [03F Structural Commit 与 Boundary Projection](03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md) | 需要设计 structural intent、ECB playback、outbox 和 snapshot projection |
| 7 | [03G Component 矩阵 / Frame Arena / Job 拓扑](03-RuntimeCore管线/03G-Component矩阵-FrameArena-Job拓扑Spec.md) | 需要查看 component 读写矩阵、Frame Arena、Unity Entities 承载映射和 Job dependency |
| 8 | [03H DOTS API 策略与 Backbone 验收](03-RuntimeCore管线/03H-DOTSAPI策略与Backbone验收Spec.md) | 需要查看 Enableable / Chunk Component / API budget / Backbone First 验收 |
| 9 | [03I System / Lane Catalog 与禁止方向](03-RuntimeCore管线/03I-SystemLaneCatalog与禁止方向Spec.md) | 需要查看 System/Lane catalog、禁止方向和历史方案定位 |

## 官方依据入口

1. [GAS Runtime Core API 选型基线](../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md)
2. [官方文档覆盖与流程闭环](../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md)
3. [规则编号索引](../../UnityDOTS官方文档参考/主题/90-规则编号索引.md)
4. [DOTS 官方规范复核与性能红线Spec](18-DOTS官方规范复核与性能红线Spec.md)

## 禁止写入

1. 当前代码事实、文件行号、命中数量、generated report 当前数字。
2. 本轮执行流水、下一步任务、完成证明、迁移进度。
3. 旧 proof API 的完成态叙述。
4. 未经 DOTS API 选型表审查的目标代码骨架新增项。

## 验收门槛

1. 任一 Runtime Core 任务能从本总览在两跳内找到对应子 Spec、官方依据和任务入口。
2. 子 Spec 只保留目标态正文，不复制 `00` 当前事实或 `02` 任务计划。
3. 所有涉及 Runtime hot path 的 API 选择都能追溯到官方规则编号和重选型触发条件。
4. Runtime Core design review 能明确回答每个数据的 owner、carrier、lifetime、allocator、dependency、structural policy 和 Debugger evidence。