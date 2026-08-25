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

## 阶段 C 选择

- `GasCommandPort` 只持有 world-local `SessionIngressGate`，公开面固定为 Activate/Commit/Cancel/ApplyEffect/RemoveEffect 五个 typed 方法；不存在 raw submit、World、EntityManager 或 Entity 入口。
- Gate 在同一把锁内完成 authority 校验、RequestId 去重、RequestSequence 分配、payload 深复制、journal append 与 FaultClose；`RequestSequence` 仅用于运输/关闭审计，gameplay canonical order 只读取调用方冻结的 `SourceSequence`。
- managed journal 的 `Sealed` 表示 record 已完整接受；搬入 ECS 时统一投影为 `BoundaryCommandInbox.Pending`。Kernel 的 `Sealed` 仅表示本 candidate tick 已 seal，两种状态不共享隐式时点。
- `GasCommandIngressSystem` 是唯一 journal→ECS writer。每个 FixedStep 开头原子切走一个 journal window；cutoff 后接受的请求只留在 tail，最早由下一 ingress window 搬运。
- inbox 原样保存 Battle/Source/Target/typed handle/Definition/payload schema、RequestId/RequestSequence/SourceSequence、AvailableTick、payload/semantic/command hash；payload 字节位于同 Session 的唯一冻结 byte buffer。
- 已消费记录只在 outer batch fence 向 Gate 成功 ack 后，才允许下一 ingress window 稳定压缩；同一 catch-up 批次内尚未 ack 的 Consumed 记录不得提前删除。FaultTerminated 载体保留用于审计。
- Gate 的 command/payload 容量只核算 accepted-outstanding；首次消费确认释放容量，但 lifetime RequestId ledger 与原 RequestSequence 继续保留，用于 exact duplicate 去重和审计。
- Boundary 外部 owner command 的 `SemanticPhaseOrdinal` 冻结为 `1`，`WorkClassOrdinal` 冻结为五种 typed command 的闭世界 ordinal；这两个值来自版本化 Boundary policy，而非 Job、worker、chunk 或 ECB lane。
- Kernel 使用 `WorldUpdateAllocator` 一次创建 profile 定长 scratch；Gather 按 `AvailableTick <= CurrentTick` seal，`CurrentTick + 1` 仅作为成功提交后的 candidate tick。相同完整 canonical key 的 work 必须逐字段、逐 payload byte 等价，不能用哈希碰撞或 RequestSequence tie-break。
- `MaximumDeltaTimeTicks` 必须 `> 0 && <= MaxFixedTicksPerBatch`。World owner 在真实 outer batch 进入 `FixedRateCatchUpManager` 前投影 `TickRate` 与 `World.MaximumDeltaTime`，使一次批次实际 Tick 数不突破 profile 预算。
- Admission 后所有具名 Job 无条件预排并读取同一 `AdmissionResult`；失败 Tick 只允许 latch/lifecycle/diagnostic/inbox seal 等控制证据变化，不推进 Tick、不消费请求、不写 gameplay authority、fact、route 或结构 intent。
- FaultClose 复用完整 FixedStep outer fence：先 ack 已成功消费请求，再与 Port accept 共锁关闭 Gate，冻结全部 accepted-outstanding（含 sealed、future inbox 与 journal tail）摘要，最后把 latch 终结为 `IngressClosed`；DAG 内不新增 completion fence。
- Session fault latch 采用 first-write-wins；同一 outer catch-up 内后续 ingress/Kernel fault 不得覆盖首次 FaultId 与证据。World owner 释放也先在 Gate 锁内关闭 Port，缓存 capability 只能同步拒绝，不能留下无人搬运的 journal。
