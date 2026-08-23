# 目标分层与官方依据 Spec

## 结论

Unity 官方 API 不直接规定 GAS 架构，但明确了 System/World 更新、Job dependency、Allocator 生命周期、ECB playback、Cleanup component 和 DynamicBuffer 的物理约束。EX-GAS 在这些约束上作出一个项目级选择：固定步 PostPhysics 单 Kernel + 标准 EndFixed + Cleanup Outbox + 单 Drain。

## 依据到决策

| 官方约束 | v1 项目决策 |
|---|---|
| FixedStep 可在一渲染帧运行 0..N 次 | inbox/outbox 跨 RenderFrame 持久；权威时间用 SimulationTick |
| System 必须传播 Job dependency | 一个 Kernel 拥有完整 DAG 与最终 handle，stage 间不 Complete |
| World update allocator 随 World update 生命周期回收 | tick scratch 使用 `SystemState.WorldUpdateAllocator`，不建 FrameArena |
| 结构变化延迟到 ECB playback | 使用标准 EndFixed ECB；新派生 Entity 下一 tick 可查询 |
| Cleanup component 在 Destroy 后保留 shell | ASC outbox 使用 cleanup buffer，Drain 后在下一标准 EndFixed 移除；shutdown 用显式 teardown |
| DynamicBuffer change version 属于整个 buffer | Attribute/Tag 使用显式 slot dirty/revision |

项目选择不是“Unity 唯一正确写法”。若改变 FixedStep/PostPhysics、dense layout 或 cleanup outbox，必须另立 ADR、更新 ScaleProfile 和全套时序测试，不能在 Definition 级开关中并存。

## 多 PhysicsWorld 边界

`GasFixedTickSystemGroup` 是主 `FixedStepSimulationSystemGroup` 直接子组并排在 `PhysicsSystemGroup` 后；不挂 `AfterPhysicsSystemGroup`，避免 `CustomPhysicsSystemGroup` 对多个 PhysicsWorld 复制全局 GAS 更新。

## 官方参考入口

- [Entities 系统与 World](../../../../UnityDOTS官方文档参考/主题/01-Entities系统与World.md)
- [结构变化与 ECB](../../../../UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md)
- [Buffer/Chunk/Store](../../../../UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md)
- [Allocator/NativeStream](../../../../UnityDOTS官方文档参考/主题/10-Collections-Allocator-NativeStream.md)
- [API 选型基线](../../../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md)
