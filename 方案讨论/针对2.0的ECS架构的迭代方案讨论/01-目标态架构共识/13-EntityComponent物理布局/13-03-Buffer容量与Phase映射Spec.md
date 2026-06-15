# 13-03：Buffer 容量与 Phase 映射 Spec

> Owner：`01-目标态架构共识/13-EntityComponent物理布局` | 状态：目标态 Spec 子页 | 来源：`../13-EntityComponent物理布局Spec.md` 同 owner 拆分

本文件只描述目标态 Entity / Component 物理布局，不记录当前实现状态、迁移进度、验证数字或任务计划。现实代码事实必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

## Buffer 容量策略汇总

| Buffer | InternalBufferCapacity | 内联字节 | 逻辑上限 | Spill 告警 | 说明 |
|---|---|---|---|---|---|
| `GEEffectCommandBuffer` | 4-16（目标 compact）/ 256（proof-only singleton） | ~128-512 bytes / ~8 KB | 16-64 per owner / 1024 proof | > 50% 或 singleton pressure | frame-local command range；scale-ready 优先 `NativeStream` |
| `GESetByCallerValueBuffer` | 8-32（目标 range）/ 256（proof-only singleton） | ~128-512 bytes / ~4 KB | 32-128 per range / 512 proof | > 50% | command/spec 附属 range |
| `GEEffectSpecBuffer` | 目标态优先 NativeContainer / 256（proof-only singleton） | 由 allocator budget 控制 / ~12 KB | 按 fan-in budget / 1024 proof | allocator budget / proof spill | frame scratch，不固定为全局 DynamicBuffer |
| `AttributeModifierBuffer` | 8-32（target grouped）/ 512（proof-only singleton） | ~192-768 bytes / ~12 KB | 32-128 per target range / 2048 proof | > 50% 或 random lookup count | target-grouped reduce/apply |
| `ActiveEffectMutationBuffer` | 4-16（owner-local）/ 128（proof-only singleton） | ~128-512 bytes / ~4 KB | 16-64 per owner / 512 proof | > 50% | active slot mutation |
| `GameplayEventBuffer` | 8-32（per-owner/fact range）/ 256（proof-only singleton） | ~256 bytes-1 KB / ~8 KB | 32-128 per range / 1024 proof | cursor lag / backpressure / > 50% | Core fact / Boundary observation 分流 |
| `AbilitySlotBuffer` | 8 | ~128 bytes | 32 slots | > 50% | **跨帧存储**，ASC→Ability 反向查找 |
| `PresentationEventBuffer` | 4 | ~128 bytes | 16 events | > 50% | per-ASC outbox，UI 直接读取 |
| `ActiveGameplayEffectBuffer` | 8 | ~512 bytes | 64 slots | spill（溢出）或 > 32 slots | **跨帧存储** |
| `TargetDataBuffer` | 16 | ~128 bytes | 32 targets | > 50% | frame-local，Boundary request 低量目标解析结果；高频/高目标数走 `AbilityTargetRecord` |

**`ActiveGameplayEffectBuffer` 超限策略：**
- < 8 slot → chunk inline，fast
- 8-32 slot → externalized，每访问多一次间接跳转
- > 32 slot → 考虑 GlobalIndexedStore（stable entity）方案
- > 64 slot → **架构告警**，说明 active effect 数量不合理，需要更早的 expire/cleanup

---

## 物理布局与 Phase 的对应关系

| Entity | 创建 Phase | 写入 Phase | 读取 Phase | 销毁 Phase |
|---|---|---|---|---|
| FrameArenaSingleton | World init | FramePrepare | 所有 phase | World dispose |
| DefinitionCatalogSingleton | Bake / World init | Bootstrap only | BoundaryCommandIngest, TargetResolve, EffectFanIn, StateEvaluate, AttributeReduceApply, GameplayFact | World dispose / scene unload |
| Frame Fan-In Scratch（NativeContainer，非 Entity） | 每帧由 owner system 创建 | BoundaryCommandIngest, EffectFanIn, StateEvaluate, AttributeReduceApply, GameplayFact | owner system / downstream kernel | 当帧 dispose/rewind |
| `GEStreamOwnerSingleton`（proof-only） | World init | BoundaryCommandIngest, EffectFanIn, StateEvaluate, AttributeReduceApply, GameplayFact | EffectFanIn, AttributeReduceApply, GameplayFact, StructuralCommit, BoundaryProjection | World dispose |
| ASC Entity | StructuralCommit | StateEvaluate, AttributeReduceApply, StructuralCommit | 所有 kernel | StructuralCommit |
| Ability Entity | StructuralCommit（grant） | BoundaryCommandIngest, StateEvaluate, StructuralCommit | BoundaryCommandIngest, GameplayFact | StructuralCommit（revoke） |
| Request Entity | Boundary ECB (Application Shell, low-frequency only) | BoundaryCommandIngest（归一化 command）, TargetResolve（写 target buffer） | BoundaryCommandIngest, TargetResolve, EffectFanIn | StructuralCommit |
| Frame-local Command / Target Records（非 Entity） | owner `ISystem` 当帧分配 | BoundaryCommandIngest, Core producer, TargetResolve, EffectFanIn | TargetResolve, EffectFanIn, AttributeReduceApply | owner `ISystem` dispose / allocator rewind |
| Active Effect Query Entity（可选） | StructuralCommit | StateEvaluate | StateEvaluate, AttributeReduceApply | StructuralCommit |

---

## 不变量

1. 物理 archetype 数量目标 < 10，告警 > 20。
2. 所有 Component 必须明确类型（Data/Buffer/Enableable/Chunk），不能以"ECS component"笼统称呼。
3. Tag 状态通过 bitmask（`TagMaskComponent`）表达，不使用独立 tag component。
4. 每个 DynamicBuffer 必须有 `InternalBufferCapacity` 声明和 spill 监控。
5. Per-ASC 数据挂 ASC entity；frame-local fan-in 数据目标态由 owner `ISystem` 的 NativeContainer 承载，proof 阶段才允许挂 `GEStreamOwnerSingleton`。
6. 不使用 Prefab 承载 GE/Ability 定义（用 BlobAsset + static table）。
7. 不使用 `ISharedComponent` 除非通过三条件检查。
8. 不使用 `ICleanupComponent` 做普通状态——只用于 destroy 后清理（`PRF-12`）。
9. NativeContainer（`NativeArray`/`NativeHashMap` 等）放在 `IComponentData` 上时，**禁止**对该 component 所在 entity 调度 `IJobChunk`/`IJobEntity`（`PRF-34`）。安全系统无法追踪 component 内嵌容器的读写依赖，正确做法是主线程提取容器后单独调度 job。
10. **禁止**依赖 ECS Child Buffer 的 sibling index 做确定性排序（`PRF-31`）。Buffer 遍历顺序不保证帧间/平台间一致。若 GAS 需要确定性 multi-effect 执行顺序，必须自建排序键（如 command sequence、sortKey 等）。
11. Status / Buff / Debuff 类标记默认进入 `TagMaskComponent`；若派生 `TagStatusFlagsComponent`，它必须是所有 ASC 共有的 cache component，禁止按 status 类型增删 component。
12. Frame Fan-In scratch 是 owner system 拥有的 NativeContainer 生命周期，不是全局 manager / singleton message bus；新 scale-ready 任务不得依赖 `GEStreamOwnerSingleton` 的大容量 DynamicBuffer。
13. Definition Catalog 是只读 singleton，而不是 Runtime registry manager；Runtime Core 不写 `GASDefinitionCatalogComponent`，不把 `NativeHashMap` / `NativeArray` 塞进 catalog component，且不以 per-definition entity query 作为 hot path lookup。

## 验收

1. Debugger 输出 archetype 总数、单 entity archetype 列表、prefab archetype 列表。
2. Debugger 输出 buffer length/capacity/spill/externalized 全部指标。
3. x50 AutoChess profile 中 archetype 数量不随单位数线性增长。
4. 代码审查可验证：无 tag component、无 managed component 在 hot path、无 prefab 用于 GE 定义。
5. 每个 Component type 能说清它是 Data/Buffer/Enableable/Chunk 中的哪一类、为什么。
6. Status flag 派生 cache 与 `TagMaskComponent` 一致性采样通过，且不增加按 status 分裂的 archetype。
