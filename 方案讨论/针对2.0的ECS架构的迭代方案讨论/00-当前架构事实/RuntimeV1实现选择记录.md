# Runtime v1 实现选择记录

> 本文只记录冻结文档未指定的实现选择，不新增语义权威；冻结 Spec 与任务树仍是裁决来源。

## 阶段 B 选择

- `AttributeValueSlot.Base/Current` 使用 `float`，与当前 Unity Runtime 和 generated 数据保持单一标量表示；本实现不并存定点镜像。
- Catalog、AttributeLayout 与 TagCatalog 的持久哈希使用 `ulong`；安装时必须精确匹配，不提供旧 schema fallback。
- `GasScaleProfile` 以 `ProfileId + ProfileVersion + ProfileHash` 冻结，且显式保存 outer batch 的 `MaxFixedTicksPerBatch/MaximumDeltaTimeTicks`；任何哈希或版本缺失均拒绝安装。
- Runtime buffers 的 `InternalBufferCapacity(0)` 只是阶段 B 的可测基线，不是逻辑容量；全部可变容量只由 Session 的版本化 `GasScaleProfile` 给出。阶段 G 性能验收必须以代表性档位测量后逐 buffer 固化 IBC，未完成测量不得宣称产品档位收口。
- 无类型稳定句柄先按 `Epoch → Kind → Owner → Index/Range → Live → Generation` 校验；先拒绝错 store 用途，避免以错误 kind 读取同 index 的另一物理表。
- Slab 与 payload range 使用 non-compacting high-water + free-list；`Live → Tombstone` 保持 generation，只有 `Tombstone → Free` 递增，溢出与容量不足均失败且不修改 allocator。
- Payload range 只 exact-size、same-kind 复用；不拆分、不合并、不压缩 live range。碎片率由 ScaleProfile 指标暴露，不能通过移动 live payload 消除。
- Ability continuation、EffectSpec、ActiveEffect payload/capture 与 aggregator contribution 统一存入 ASC-local `GasPayloadValueSlot[]` 64-bit word store；`PayloadKind + generated schema` 唯一解释连续 words，`GasPayloadRangeRecord[]` 是唯一 generation/free-list/high-water 元数据。不存在另一个 typed payload/capture slab 或第二套容量字段。
- SpawnBatch 的 Attribute 初值来自 immutable Catalog `DefaultValue`；每条 Pending Tag 表示一次 exact grant并传播 ancestor；Pending Ability 直接引用已校验 Ability definition index。
- Pending 初始化项的 `ConfigOrdinal` 只承担批内唯一、确定性规范顺序，不作为第二份配置数据源。
- Pending Session 保存原始 Battle/ASC/三类初始化计数、不可变 member ranges 与内容哈希；ASC 同时预挂 enableable batch work marker。Finalize 必须同时匹配 manifest、registry 与 runtime 快照，被同步裁短的自洽子集也会整批失败；marker 只用于损坏映射后的 teardown 兜底，Ready/teardown 后禁用但保留不可变诊断字段，并清空临时 member 映射。
- Session 基数只统计携带 `GasActiveSessionAuthority` 的实体；保留的 `Disposed/Faulted` 诊断记录在失去 authority marker 后不参与单 active Session 判定。
- 阶段 B 的 internal recorder 只验证最小 Attribute/Tag/Grant bootstrap；它不是单位配置公开入口。初始 ActiveEffect 必须由阶段 E 的 target transaction 与 generated SpawnInitializationProgram 接管后，完整单位 Spawn 才可对 Demo 开放。
