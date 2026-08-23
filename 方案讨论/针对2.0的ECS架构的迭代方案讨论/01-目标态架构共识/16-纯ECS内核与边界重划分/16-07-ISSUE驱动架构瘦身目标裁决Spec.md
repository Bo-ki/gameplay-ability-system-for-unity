# ISSUE 驱动架构瘦身最终裁决 Spec

## 结论

v1 不在旧管线上继续“瘦身”，而是把所有问题归结为四个结构性根因并一次切除：多物理阶段、runtime Entity 双权威、边界多消费者、生成/调试越权。

## 裁决矩阵

| 问题族 | v1 裁决 | 不接受的折中 |
|---|---|---|
| 五组与大量 System | 单 `GasFixedTickSystemGroup` + 单 Kernel，业务拆 Job | 保留空壳组、按功能开关两套 schedule |
| Ability/GE/ActiveEffect Entity | ASC-local generational slab | definition 选择 Entity/slot backend、镜像状态 |
| global fan-in/store | tick scratch canonical merge → target-owned range | singleton buffer、managed list、random BufferLookup write |
| 自定义 Structural Commit | 标准 EndFixed ECB | 新建另一个自定义 playback group |
| FrameArena | WorldUpdateAllocator | 手工 rewind、跨 System temp owner |
| Tag bitmask 权威 | Exact/Inclusive count + derived bitset | 固定 256 bit 与 count 双权威 |
| Attribute per-component 路线 | Session fixed buffer + aggregator | generated component mirror 快路径 |
| Cue/EventBus 断裂 | cleanup outbox + one Drain + immutable batch | Cue/UI/Replay 各读一套 ECS buffer |
| AbilityTask 缺失 | N Continuation + N Subscription slots | Activation 单 PC/WaitMask |
| Requirement phase flatten | Application/Ongoing/Removal/Immunity 分区 | 一个通用 mask 丢失时序 |
| UE 同步 reaction | v1 明示 stable-state deferred reaction | 隐式有限 pass 恢复同 tick call-stack |
| Prediction | v1 完全删除 schema/API | 保留恒零 PredictionKey“为以后兼容” |
| SourceGenerator 越权 | immutable catalog + pure glue | generated Runtime System/lifecycle |

## 一次切换

Definition/identity、slots 和 Kernel 可在隔离分支分阶段开发，但进入稳定分支时必须通过删除门：旧 schedule 与新 Kernel 不能同时可更新，旧 authority 类型不能继续被 query，不能存在 Runtime selector/feature flag/fallback。

## 重新选型

ScaleProfile 若证明 dense Attribute/Tag 或某 fan-in 算法不达标，应以 ADR 替换单一 backend。重新选型必须保持领域 contract、identity、deterministic order 和 Boundary protocol，不把实验后端并入 release runtime。

## 最终证明

- 静态扫描：旧 group/authority/generated lifecycle/public raw Entity/Prediction 零目标态引用。
- 语义测试：Ability、Continuation、Effect、Capture、Tag、Aggregator、Cue、Reaction。
- 运行测试：完整 FixedStep/EndFixed/Drain、Destroy terminal facts、AutoChess golden。
- 规模测试：replicated groups、hot target、period burst、mass-death teardown 与 Boundary retry 的内存/pressure/stabilization evidence；x50/x100/x1000 仅是具体 Profile 参数。
- 确定性：同输入/content hash 的 trace/state/fact/battle hash 一致。
