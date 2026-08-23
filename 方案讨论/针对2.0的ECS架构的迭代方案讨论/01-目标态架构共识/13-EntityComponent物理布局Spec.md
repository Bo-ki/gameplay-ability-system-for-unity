# Entity / Component 物理布局总纲

## 结论

v1 的 gameplay authority 以 ASC Entity 为聚合根，Ability/Activation/Continuation/Subscription/ActiveEffect 使用 ASC-local 非压缩 slab；Attribute/Tag 使用 Session 固定布局 buffer。除 ASC、Session/Boundary 持久载体和真实空间/物理对象外，不为领域瞬时状态创建 Entity。

完整正文见 [13 子目录](13-EntityComponent物理布局/README.md)：

1. [13-01 Entity 清单与 Runtime 布局](13-EntityComponent物理布局/13-01-Entity清单与运行时布局Spec.md)
2. [13-02 Archetype 与 Component 分类](13-EntityComponent物理布局/13-02-Archetype与Component分类Spec.md)
3. [13-03 Buffer 容量与 Phase 映射](13-EntityComponent物理布局/13-03-Buffer容量与Phase映射Spec.md)

## 物理原则

- ASC 的长期 buffer/slab 由 target-owned mutation lane 写，禁止 global authority mirror。
- slab 只 tombstone/free-list，不 RemoveAt/SwapBack/compact。
- Attribute/Tag buffer 在 spawn 时一次定长，tick 内不 resize。
- Buffer InternalCapacity、Catalog 上限和 scratch 算法不在框架 Spec 硬编码，由 ScaleProfile/生成报告决定。
- tick scratch 归 `GasTickKernelSystem` 且使用 WorldUpdateAllocator，不是 Entity/Singleton/FrameArena。
- Boundary outbox 是 cleanup buffer，ASC Destroy 后由唯一 Drain 交接，并通过下一标准 EndFixed 或 shutdown teardown 释放 shell。
- 标准 EndFixed 创建的 projectile/aura/zone 是派生物理对象，不是 Ability/Effect 权威。

Component 矩阵和 Job 拓扑由 [03G](03-RuntimeCore管线/03G-Component矩阵-TickScratch-Job拓扑Spec.md) 维护；本系列只维护持久物理布局和容量契约。
