# 03E 子链导航

本目录维护单个 target-owned transaction 的四段唯一正文：

1. [Effect Fan-In](03E-01-EffectFanInSpec.md)
2. [ActiveEffect 与稳定化](03E-02-StateEvaluateActiveEffectStoreSpec.md)
3. [Attribute Aggregator 与 Apply](03E-03-AttributeReduceApplySpec.md)
4. [GameplayFact 与 deferred reaction](03E-04-GameplayFactSpec.md)

四段不能拆为相互独立的状态权威或多个物理 SystemGroup。它们共享同一目标 ASC writer、同一 canonical command range、同一事务内 dirty set 与同一最终 fact writer。
