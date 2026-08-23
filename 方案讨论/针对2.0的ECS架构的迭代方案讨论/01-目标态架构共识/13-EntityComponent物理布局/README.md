# 13：Entity / Component 物理布局

> 状态：v1 目标态索引
> 目标：把 GAS 语义映射成唯一、可验证、可扩展的 Entities 物理形态

## 结论

v1 采用“少 Entity、ASC-local 固定布局、长期实例 slab、Tick scratch 非 ECS”的形态：

- Session Entity 持有不可变规则引用、Tick/哈希、Battle/ASC Registry 与 Battle/Session-scope outbox等世界级状态。
- ASC Entity 是稳定 authority owner，OwnerActor 与 AvatarActor 分离。
- Attribute/Tag 是按 Session Layout/Catalog 建立的固定逻辑长度 Buffer。
- Granted Ability、Continuation、Active Effect 是 non-compacting slot+generation slab。
- Boundary Fact 使用 scoped Cleanup Buffer：ASC-scope 位于所属 ASC，Battle/Session-scope 位于唯一 Session；同一 fact只存一处，由单 managed drain消费。
- projectile、hitbox 等只有在需要独立查询/生命周期/Transform/Physics 时才是 Entity；Ability/Effect 实例本身不提升为 Entity。

## 文档索引

| 文档 | 回答的问题 |
|---|---|
| [13-01-Entity清单与运行时布局Spec.md](./13-01-Entity清单与运行时布局Spec.md) | 有哪些 Entity、权威状态放在哪里、生命周期怎样结束 |
| [13-02-Archetype与Component分类Spec.md](./13-02-Archetype与Component分类Spec.md) | Component/Buffer/Cleanup/Enableable 如何选，如何控制 archetype |
| [13-03-Buffer容量与Phase映射Spec.md](./13-03-Buffer容量与Phase映射Spec.md) | 每类 Buffer 的逻辑长度、容量来源、写入 lane 和验收指标 |

## 跨文档硬契约

1. 一个语义只有一个权威数据源；派生缓存必须可重建。
2. 内容差异不改变 ASC archetype，不按 Ability/Attribute/Tag 定义增删 Component。
3. 长期句柄始终校验 `slot index + generation`，slab 不压缩 live slot。
4. Tick 临时容器归 `GasTickKernelSystem` 与 `WorldUpdateAllocator` 所有，不成为 Entity/Singleton。
5. 真实结构变化只进标准 EndFixed ECB；业务热路径在既有 Buffer/Component 内。
6. 所有物理容量和性能门来自版本化 ScaleProfile；本目录不写死通用阈值。

## 不在本目录解决

- UE GAS 语义定义与 Definition bytecode 细节；
- Prediction/rollback（v1 非目标）；
- Presentation/Cue 的托管资产加载与播放；
- 不同项目的具体 IBC、chunk 或毫秒门槛。

这些主题只能引用本目录的权威布局，不能建立平行运行时模型。
