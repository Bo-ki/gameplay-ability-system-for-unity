# 01 目标态架构共识归档

本目录只保存已经退出当前阅读入口的目标态 Spec 拆分前快照和历史正文。

归档文件可以用于追溯旧章节语境，但不能作为当前 Spec 入口。当前目标态入口以 `../README.md` 和对应拆分后的 Spec 文件为准。

## 归档索引

| 文件 | 归档原因 | 当前入口 |
|---|---|---|
| [2026-06-07-03-RuntimeCore管线Spec拆分前](2026-06-07-03-RuntimeCore管线Spec拆分前.md) | Runtime Core 管线总览和多主题正文已拆到 `../03-RuntimeCore管线/` | `../03-RuntimeCore管线Spec.md` |
| [2026-06-08-03B-业务调用链与配置消费Spec拆分前](2026-06-08-03B-业务调用链与配置消费Spec拆分前.md) | 03B 业务调用链、Luban Runtime 消费链和 Generated Runtime Glue 接口正文已拆到 `../03-RuntimeCore管线/03B-业务调用链与配置消费/` | `../03-RuntimeCore管线/03B-业务调用链与配置消费Spec.md` |
| [2026-06-08-03E-EffectFanIn-State-Attribute-FactSpec拆分前](2026-06-08-03E-EffectFanIn-State-Attribute-FactSpec拆分前.md) | 03E Effect Fan-In、State Evaluate、Attribute Reduce / Apply 和 Gameplay Fact 正文已拆到 `../03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact/` | `../03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-FactSpec.md` |
| [2026-06-08-16-01-目标分层官方依据与不变量Spec拆分前](2026-06-08-16-01-目标分层官方依据与不变量Spec拆分前.md) | 16-01 目标分层、Owner Map、消息流协议和最小完整代码骨架正文已拆到 `../16-纯ECS内核与边界重划分/16-01-目标分层官方依据与不变量/` | `../16-纯ECS内核与边界重划分/16-01-目标分层官方依据与不变量Spec.md` |
| [2026-06-08-16-06-端到端消息流代码骨架Spec拆分前](2026-06-08-16-06-端到端消息流代码骨架Spec拆分前.md) | 16-06 入口、局部 Owner Map、完整 C# 骨架、代码解读、完整性审查和验收正文已拆成根入口 + `16-06A` 完整代码骨架正文 | `../16-纯ECS内核与边界重划分/16-06-端到端消息流代码骨架Spec.md` |
| [2026-06-08-13-EntityComponent物理布局Spec拆分前](2026-06-08-13-EntityComponent物理布局Spec拆分前.md) | 13 Entity / Component 物理布局、Archetype / Component 分类、Buffer 容量和 Phase 映射正文已拆到 `../13-EntityComponent物理布局/` | `../13-EntityComponent物理布局Spec.md` |
| [2026-06-08-RequestInstantOwnerLocalCommand](2026-06-08-RequestInstantOwnerLocalCommand.md) | `GameplayEffectRequestWriter` instant producer 首跳 singleton 退出与 owner-local flush 中间闭环已完成归档 | `../../00-当前架构事实/P0-致命缺陷.md` |
| [2026-06-08-AbilityPeriodInstantOwnerLocalCommand](2026-06-08-AbilityPeriodInstantOwnerLocalCommand.md) | generated ability commit 与 active effect period instant producer 首跳 singleton 退出已完成归档 | `../../00-当前架构事实/P0-致命缺陷.md` |
| [2026-06-08-OverflowInstantNextFrameOwnerLocalCommand](2026-06-08-OverflowInstantNextFrameOwnerLocalCommand.md) | active effect overflow instant producer 首跳 singleton 退出与 next-frame owner-local 晋升闭环已完成归档 | `../../00-当前架构事实/P0-致命缺陷.md` |
| [2026-06-08-InstantSpecBuildOwnerLocalConsumer](2026-06-08-InstantSpecBuildOwnerLocalConsumer.md) | instant spec build 直接消费 ASC owner-local command/payload，旧 flush 中间层退场 | `../../00-当前架构事实/P0-致命缺陷.md` |
| [2026-06-08-InstantSpecCarrierOwnerLocal](2026-06-08-InstantSpecCarrierOwnerLocal.md) | instant spec carrier、generated AttributeReduce 和 cue fact projection 退出 singleton spec stream | `../../00-当前架构事实/P0-致命缺陷.md` |
| [2026-06-08-ExecutionOnlyGEInstantSpecInvariant](2026-06-08-ExecutionOnlyGEInstantSpecInvariant.md) | execution-only / cue-only GE 不得因 `ModifierCount == 0` 被送入 ActiveMutation，必须保持 instant spec 链 | `../14-DefinitionCodeGen目标链路Spec.md` |
| [2026-06-08-BoundaryObservationFactCarrier](2026-06-08-BoundaryObservationFactCarrier.md) | BoundaryObservationFact carrier 接管表现、回放、typed event bridge 和 Debugger 读面 | `../03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact/03E-04-GameplayFactSpec.md` |
| [2026-06-08-AbilityLifecycleFactOwnerLocal](2026-06-08-AbilityLifecycleFactOwnerLocal.md) | generated ability lifecycle fact 退出 singleton fact direct append，改写 ASC owner-local fact lane | `../../00-当前架构事实/P0-致命缺陷.md` |
| [2026-06-08-HandwrittenAbilityFactOwnerLocal](2026-06-08-HandwrittenAbilityFactOwnerLocal.md) | hand-written ASC / ability lifecycle fact producer 退出 singleton fact direct append，改写 ASC owner-local fact lane | `../03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact/03E-04-GameplayFactSpec.md` |
| [2026-06-07-10B-AutoChess完整业务案例设计Spec拆分前](2026-06-07-10B-AutoChess完整业务案例设计Spec拆分前.md) | AutoChess 完整业务案例根 Spec 已拆到 `../10B-AutoChess完整业务案例/` | `../10B-AutoChess完整业务案例设计Spec.md` |
| [2026-06-07-10B-03-羁绊与Runtime基础设施Spec拆分前](2026-06-07-10B-03-羁绊与Runtime基础设施Spec拆分前.md) | 10B-03 同时承载羁绊业务机制和 Runtime 基础设施，已拆为 10B-03A / 10B-03B | `../10B-AutoChess完整业务案例/10B-03-羁绊与Runtime基础设施Spec.md` |
| [2026-06-07-16-纯ECS内核与边界重划分Spec拆分前](2026-06-07-16-纯ECS内核与边界重划分Spec拆分前.md) | 纯 ECS 内核与边界重划分根 Spec 已拆到 `../16-纯ECS内核与边界重划分/` | `../16-纯ECS内核与边界重划分Spec.md` |
