# Runtime Core 管线导航

本目录按唯一职责拆解一个 `GasFixedTickSystemGroup + GasTickKernelSystem`。字母编号表示逻辑 contract，不表示执行时存在多个 SystemGroup。

推荐阅读顺序：

1. [03A 执行域与数据流](03A-执行域与数据流Spec.md)
2. [03C SystemGroup 合约与核心数据形态](03C-SystemGroup合约与核心数据形态Spec.md)
3. [03D Command/Ability/Target](03D-CommandResolve与TargetResolveSpec.md)
4. [03E Effect/State/Attribute/Fact](03E-EffectFanIn-State-Attribute-FactSpec.md)
5. [03F Structural/Boundary](03F-StructuralCommit与BoundaryProjectionSpec.md)
6. [03G Component/TickScratch/Job](03G-Component矩阵-TickScratch-Job拓扑Spec.md)
7. [03H API 与验收](03H-DOTSAPI策略与Backbone验收Spec.md)
8. [03I System/Lane 禁止方向](03I-SystemLaneCatalog与禁止方向Spec.md)

业务与配置入口另见 [03B](03B-业务调用链与配置消费Spec.md)。Effect 子链唯一正文见 [03E 子目录](03E-EffectFanIn-State-Attribute-Fact/README.md)。

目录共同遵守：单 Kernel 拥有 query、allocator、scratch 和 dependency；长期权威只在 ASC-local 数据；结构变化走标准 EndFixed ECB；managed 世界只有一个 Boundary Drain。
