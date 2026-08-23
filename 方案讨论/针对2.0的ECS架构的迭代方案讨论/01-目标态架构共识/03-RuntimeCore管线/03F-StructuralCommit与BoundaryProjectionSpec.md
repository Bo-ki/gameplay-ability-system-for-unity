# 03F：Structural Commit 与 Boundary Projection Spec

> 状态：v1 目标态裁决
> 核心选择：标准 EndFixed 结构提交 + scoped Cleanup Buffer outbox + 两阶段单 managed drain

## 1. 结论

EX-GAS v1 不自建 StructuralCommit SystemGroup/ECB playback 点。所有真正的结构变化进入 Unity 标准 `EndFixedStepSimulationEntityCommandBufferSystem`；Runtime Core 的大多数变更通过既有 ASC Buffer/Component 就地完成。

Boundary Fact 使用 scoped `ICleanupBufferElementData` outbox：ASC-scope 事实写所属 ASC outbox，BattleInstance/Session-scope 事实写唯一 Session outbox；同一事实恰有一个物理 outbox owner，禁止复制。一个托管 drain 在固定步进批次后读取 live owner 与 destroy cleanup shell，但必须先让 managed staging 以 `BatchId + watermark` 幂等接管，成功后才能清 ECS outbox并推进 `BoundaryDrainState`。drain 本身绝不在 Post-Fixed 时段排一个跨批次 ECB；destroy shell 由**下一次 Kernel cleanup prepass**把移除命令记录到该 Tick 的标准 EndFixed。

## 2. Structural 与非 Structural 的硬边界

| 操作 | v1 形态 | 是否 ECB |
|---|---|---:|
| Attribute Base/Current 变化 | 固定 Buffer 元素写 | 否 |
| Tag exact/inclusive count 变化 | 固定 Buffer 元素写 | 否 |
| Ability/Continuation/Effect 激活、结束、复用 | slab 槽状态与 generation 写 | 否 |
| dirty bit / pre-attached enableable 状态 | 数据或 enable bit 写 | 否 |
| Boundary Fact append | Cleanup Buffer append | 否 |
| Drain receipt/state 更新 | 预挂载 Cleanup Component 数据写 | 否 |
| 创建/销毁 ASC 或派生 gameplay Entity | Entity 结构变化 | 是 |
| 增删 Component/Buffer | Entity 结构变化 | 是 |
| 首次为出生 Entity 建立固定布局 | spawn ECB / 初始化阶段 | 是 |

禁止把“业务阶段结束”误当作“需要结构提交”。只有 archetype/Entity 生命周期改变才进入 ECB。

## 3. 标准 EndFixed 合约

Kernel 获取 `EndFixedStepSimulationEntityCommandBufferSystem.Singleton`，创建并行 ECB，记录真实结构变化，并把生产 JobHandle 纳入依赖。播放发生在标准 EndFixed 点。

可见性契约：

- Kernel 内同 Tick 后续 lane 只能读取已存在的 slab/Buffer 状态，不能读取尚未 playback 的新 Entity/Component。
- EndFixed 后的系统可以看到结构结果；下一个 GAS Tick 必然可以看到。
- GAS 是 PostPhysics，EndFixed 创建/销毁的 Physics/Transform 结构不会倒流影响刚结束的 Physics Tick。
- Activate、Commit、Cancel 的 same-tick 语义必须落在既有 ASC 槽内。以 ECB 创建 Ability Instance Entity 再继续执行的设计不合法。
- `WholeTickInfraAdmission` 必须先覆盖本 Tick 的 ECB command/payload/outbox 上界；admission 失败不进入 gameplay 权威 wave，也不记录半 Tick structural intent。

独立 World 或手动 runner 必须让 TickBatch owner 更新完整 `FixedStepSimulationSystemGroup` 父链，包含组 allocator、Physics、GAS 与标准 EndFixed。禁止只手工调用若干 GAS 组或直接调用 Kernel。

## 4. Boundary Fact 数据契约

```csharp
/// <summary>
/// 自包含的持久边界事实；Cleanup Buffer 让物理 outbox owner 销毁后仍能被单一 drain 接管。
/// </summary>
public struct BoundaryFactBuffer : ICleanupBufferElementData
{
    public BoundaryFactScopeKind ScopeKind;
    public BoundaryFactPlane FactPlane;
    public ulong ScopeStableId;
    public uint ScopeGeneration;
    public ulong BattleInstanceId;
    public uint BattleInstanceGeneration;
    public ulong OwnerScenarioUnitId;
    public ulong SourceAscStableId;
    public uint SourceAscGeneration;
    public ulong TargetAscStableId;
    public uint TargetAscGeneration;
    public BoundaryEventId EventId;
    public ActiveEffectHandle CueActiveEffectHandle;
    public uint CueActiveCycleOrdinal;
    public ushort CueDefinitionOrdinal;
    public ulong SimulationTick;
    public ushort SemanticPhaseOrdinal;
    public ushort WorkClassOrdinal;
    public ulong ParentCausalityId;
    public ulong SemanticId;
    public BoundaryFactKind Kind;
    public BoundaryFactPayload Payload;
}

/// <summary>
/// 事实交付的全局去重键；物理 owner 与其持久单调序号共同避免跨 outbox 碰撞。
/// </summary>
public struct BoundaryEventId
{
    public ulong SimulationEpoch;
    public BoundaryOutboxOwnerKind OwnerKind;
    public ulong OwnerStableId;
    public uint OwnerGeneration;
    public ulong OwnerSequence;
}

/// <summary>
/// 记录 ECS outbox 与 managed staging 的接管状态；冻结物理 owner 身份，使空 shell 也能生成 NoFactReceipt。
/// </summary>
public struct BoundaryDrainState : ICleanupComponentData
{
    public ulong SimulationEpoch;
    public BoundaryOutboxOwnerKind OwnerKind;
    public ulong OwnerStableId;
    public uint OwnerGeneration;
    public ulong NextOwnerSequence;
    public DrainStatus Status;
    public ulong BatchId;
    public ulong InFlightWatermark;
}
```

字段要求：

- `ScopeKind` 决定唯一物理 owner：`Asc` 写该 ASC outbox，`BattleInstance/Session` 写 Session outbox；`ScopeStableId/Generation` 保存语义 identity scope，同一事实不得同时写两处。`FactPlane=Gameplay/TeardownAudit` 与 identity scope 正交：物理路由只读 `ScopeKind/ScopeStableId`，BattleHash/teardown audit inclusion 只读 `FactPlane`。
- 事实自包含 `BattleInstanceId/Generation`、source/target ASC stable id/generation 与相关 ScenarioUnit 标识，不能依赖销毁后已被移除的 membership/identity Component。Epoch 内即使 stable id 复用，也必须以 generation 区分生命周期。
- `BoundaryEventId=(SimulationEpoch, PhysicalOwnerKind, PhysicalOwnerStableId, PhysicalOwnerGeneration, OwnerSequence)` 是事实交付去重键；consumer/staging 不得只用裸 `ulong`。仅 lifecycle fact 使用 `CueLifecycleKey=(SimulationEpoch, CueActiveEffectHandle, CueActiveCycleOrdinal, CueDefinitionOrdinal)` 配对 OnActive/WhileActive/Removed。Executed 使用 Application/PeriodExecution identity，不复用 lifecycle 字段。
- `BoundaryDrainState.NextOwnerSequence` 是物理 owner内唯一序号源，append 先分配后单调递增，Accepted→Idle、prefix clear 与 empty shell 均不重置；回绕前 fatal fault。`InFlightWatermark` 只冻结本 Batch 最大 OwnerSequence，不兼任序号源。Session outbox即使混合多个 Battle scope也共享同一条物理 owner sequence。
- fact 的 Epoch 只存在 `EventId.SimulationEpoch`，禁止再放一份顶层 Epoch 导致排序/去重分裂。drain 使用 `EventId.SimulationEpoch + SimulationTick + FactPlane + ScopeKind + ScopeStableId + ScopeGeneration + SemanticPhaseOrdinal + WorkClassOrdinal + EventId` 得到全局稳定顺序，不能依赖 chunk/query 遍历顺序。两个 ordinal 来自 schema/catalog hash，不是 Job lane。
- `BoundaryDrainState` 只冻结物理 owner key：Session owner 没有唯一 Battle identity，ASC owner 的 Battle membership 也仅供 fact/诊断。真实 Battle identity 由每个 fact 自带；NoFactReceipt 仅确认某 physical owner/range 无事实，不伪造 semantic Battle scope。
- `BoundaryFactPayload` 表示由边界 schema 生成的 unmanaged、版本化 tagged payload；其物理大小由 ScaleProfile 复核，本 Spec 不写固定字节预算。UnityEngine.Object、委托与托管字符串只存在于 drain 之后。
- `BoundaryProject` 先按唯一物理 owner 分区：ASC partitions 可跨 ASC 并行，Session partition 由唯一 writer 合并 Battle/Session facts。OwnerWave 只写 owner fact intent，TargetPrepare 只写 shadow intent，TargetPublish 才发布 target fact partition；各 lane 都不得随机 append outbox。

Cleanup Buffer 不应假设会从 prefab/原型实例化中自动复制。Session 与 ASC spawn 路径都必须显式添加 `BoundaryFactBuffer` 与 `BoundaryDrainState` 并纳入结构验收；Session outbox 保证零 ASC、全部 ASC 已销毁或显式 stop 时仍可交付 Battle/Session 终局事实。

`BoundaryDrainState` 的唯一合法转换为：

```text
Idle --Boundary append with persistent NextOwnerSequence--> Pending
Pending --drain freeze range--> InFlight(BatchId, InFlightWatermark)
InFlight --staging failure/retry--> InFlight(same BatchId, same InFlightWatermark)
InFlight --Accepted, clear <= InFlightWatermark, no tail--> Accepted
InFlight --Accepted, clear <= InFlightWatermark, has tail--> Pending
Accepted live outbox owner (ASC/Session) --next Kernel prepass--> Idle
Accepted cleanup shell --next Kernel prepass/EndFixed--> removed
```

Boundary append 从不重置 `NextOwnerSequence`，只把 Idle推进 Pending，不冻结批次。managed drain 在 catch-up batch后对当时稳定的 source range冻结 `BatchId/InFlightWatermark` 并进入 InFlight；所有重试复用同一 identity/range，不得因 retry或后续 append生成新 BatchId。receipt 尚未返回时 source记录全部保留；Accepted后只清 `<=InFlightWatermark`。live ASC/Session owner若已有更晚 tail则回到 Pending，不能清整段 outbox或误标空闲。

## 5. 生命周期状态机

### 5.1 Live outbox owner

1. Owner/Target/TerminalResolve 写 fact partition，StableFactMerge 后由 `BoundaryProject` 按唯一物理 owner range 写 ASC 或 Session Boundary Fact outbox。
2. 固定步进批次结束后，单一 managed drain 查询 live ASC 与 Session outbox。
3. drain 对本次稳定 source range冻结稳定 `BatchId` 与 per-owner `InFlightWatermark`，把排序后的范围交给 managed staging；FixedStep catch-up 后续 append只能落在 watermark之后。
4. staging 以 `BatchId + physical owner key + InFlightWatermark + BoundaryEventId` 幂等接管并返回 receipt；只有 receipt 成功后才能清除 live outbox的 `<=InFlightWatermark` 范围。
5. 清除后若仍有 `>InFlightWatermark` tail，状态回到 Pending等待新 Batch；无 tail才写 `BoundaryDrainState=Accepted`，下一次 Kernel cleanup prepass再折叠回 Idle。`NextOwnerSequence` 始终保留，live outbox类型始终保留。

### 5.2 Destroy outbox owner

1. 销毁前所需死亡/Remove Cue 事实先写入 cleanup outbox。
2. EndFixed 执行 `DestroyEntity`；普通 Component/Buffer 被移除，cleanup buffer 保留，Entity 成为 cleanup shell。
3. drain 查询 cleanup shell并在所有 producer完成后冻结当前完整 range；有事实则按同一 accept-before-clear协议接管。
4. staging receipt 成功后，drain只清 `<=InFlightWatermark` 并写 `BoundaryDrainState=Accepted`；失败则事实与 InFlight identity保持可重试。若 shell 从未产生事实，drain必须在 producer completion后用 state 内冻结的 physical owner key 和 empty sequence range 生成显式 `NoFactReceipt` 再标 Accepted，不填造 Battle scope；禁止空 shell 永久停在 Idle，也禁止未经无事实确认直接移除。
5. 下一次正常 Kernel 的 cleanup prepass 查询 `Accepted` shell，并把移除 `BoundaryFactBuffer + BoundaryDrainState` 记录到**当前 Tick**的标准 EndFixed；最后一个 cleanup component 消失后 Entity 才最终销毁。

Post-Fixed drain 禁止创建“等下一批 playback”的 ECB，也禁止保留上一次 `EntityCommandBuffer` 给下一批复用。若 World shutdown 后不再有下一次 FixedStep，teardown 必须先让当前 TickBatch 完整经过 EndFixed、完成全部 outbox/ECB producer，再执行 FinalDrain；全部 staging receipt 成功后，由停止世界的 teardown 直接移除 cleanup buffer/state。这是显式停止世界清理，不是 Runtime 热路径的第二个 playback 点。

不能用 `EntityManager.Exists` 判断 ASC/Session 业务存活。Registry/引用校验以对应 live identity/generation 是否仍存在为准；cleanup shell 只为完成边界清理而存在。

### 5.3 World shutdown

本节是 Session shutdown 物理顺序、`DisposedReceipt` 与最终 validation 封印的唯一 owner；公开方法只由 [16-04 Shell Capability Contract](../16-纯ECS内核与边界重划分/16-04-ShellCapabilityContractSpec.md) 暴露。一个 World 同时只有一个 active GasSession Tick domain，关闭路径固定为：

```text
Running
  --BeginClose--> Terminalizing
  -> FinalDrain
  --staging failure--> FinalDrainBlocked --PumpShutdown/retry same Batch--> FinalDrain
  --all receipts accepted--> CleanupAudited
  -> Disposing
  -> Disposed
  -> DisposedReceipt
  -> ValidationResultSeal
```

`Faulted` 只改变 close reason 与 terminal semantics，不提供跳过上述路径的捷径。`SpawnBatch` 是操作名，不是 lifecycle 枚举。World/Session 停止必须按以下顺序：

1. `BeginClose` 在 `SessionIngressGate` 上幂等关闭新的 gameplay ingress；已 sealed 的当前事务完成，已 Accepted 但尚无 terminal outcome 的 tail 按 [16-02 Request ledger](../16-纯ECS内核与边界重划分/16-02-BoundaryCommand与CoreCommandResolveSpec.md) 逐项终结。关闭线性化点后不再运行新的 gameplay Kernel。
2. 让已经开始的 TickBatch 完整经过标准 EndFixed，并只在 [18](../18-DOTS官方规范复核与性能红线Spec.md) 冻结的 `EndFixedPlayback/OuterBatchToDrain/TerminalFinalDrain/DiagnosticsCaptureOnly` fence 完成 Kernel、outbox 与 ECB producer 依赖；禁止在 Kernel lane 内或通过隐式 `EntityManager` sync 补做完成。
3. `PumpShutdown` 执行/重入 FinalDrain：若存在 InFlight，必须先以相同 `BatchId/InFlightWatermark` 重试；它不推进 FixedStep、Kernel 或 `SimulationTick`。先让所有 gameplay terminal、Request terminal 与此时已产生的 teardown audit range 取得 managed staging receipt。
4. 上述 receipt 完整后直接清理 `Accepted` cleanup shell，关闭 managed Cue/Presentation/resource owner，并产生最后的 cleanup audit；`PumpShutdown` 继续以相同 accept-before-clear 协议接管这些新 audit。只有全部可审计清理完成且最后 audit receipt 已接受，才进入 `CleanupAudited`，进入下一步后禁止再产生 Boundary audit。
5. `Disposing` 才释放 runtime-created Definition Blob 与 World，使 Epoch 失效；完成后进入 `Disposed`，生成不属于 Boundary fact stream 的 managed `DisposedReceipt`。
6. `ValidationResultSeal` 最后冻结 Battle outcome seal 引用、Boundary/teardown hash、`DisposedReceipt` digest 与验证状态。seal 之后不得新增 gameplay、Boundary 或 teardown fact，也不得改写既有 outcome。

headless 模式也必须安装同一个 drain 合约，可以接到日志/网络/丢弃 sink；“没有画面”不等于可以遗留 cleanup shell。

staging 接管失败不回滚已经提交的 gameplay，也不能清 outbox；Session 必须进入 `FinalDrainBlocked` 并阻止 `CleanupAudited/Disposing/Disposed`，直到 `PumpShutdown` 对同一 InFlight identity 重试成功。不存在“记录 fatal evidence 后强制 Disposed”的产品策略逃生口；运维若放弃，只能报告未完成 shutdown，不能产出 `DisposedReceipt/ValidationResultSeal`。

## 6. 为什么 v1 选择 scoped cleanup outbox

| 方案 | 优点 | 成本/风险 |
|---|---|---|
| scoped Cleanup Buffer（采用） | ASC facts owner-local；Battle/Session facts 在唯一 Session owner；死亡事实随 ASC shell 保留；零 ASC 终局仍可表达 | drain 查询 ASC/Session live owner 与 shell；两类 spawn 都必须显式加 cleanup 类型；按 scope 路由必须唯一 |
| 所有事实集中到 Session outbox | drain 只扫一个 store | 把高频 ASC facts 引入全局持久单写者，成为容量、背压与争用中心 |
| 同一事实同时写 ASC 与 Session | 看似便于不同消费者 | 形成双权威、重复交付与不可能统一的 receipt；禁止 |

v1 固定采用 scoped 方案。ASC 事实不进入 Session 镜像；Battle/Session 终局事实也不依附任意“代表 ASC”。若 profiling 证明查询/Chunk 分散是瓶颈，后续 ADR 可以整体改 owner 策略，但任何时刻同一事实仍只能有一个 outbox owner。

## 7. 单消费者与 retention

Runtime Core 只认识一个 managed drain，不为 UI、音频、网络、调试分别维护游标。

```text
Cleanup/Live Outbox -> Single Drain -> Managed Dispatch Queue -> Cue | UI | Audio | Log | Network adapter
```

- drain 是 Core outbox 的唯一接管协调者；它只更新 receipt 状态，不提交 post-Fixed ECB。shell remove command 由下一 Kernel cleanup prepass 提交给同 Tick 标准 EndFixed。
- 多消费者、重试与长期 retention 在托管 dispatch 层实现，不反向持有 ECS Buffer。
- v1 不在 Core 内做每消费者 offset，也不让消费者直接竞争清 Buffer。
- 若托管接管失败，outbox 保留并显式上报背压/故障；禁止静默丢弃，也禁止 FinalDrain 宣告完成。
- 具体 retention 数量和时长属于产品/ScaleProfile 决策，本 Spec 不写死。

## 8. same-tick 与 next-tick

| 生产源 | 能否 same-tick 消费 | 约束 |
|---|---:|---|
| Ability 直接输出 | 是 | 仍在 Kernel 既有槽/Buffer 闭包内 |
| Definition pre-apply program | 是 | 生成期证明无环、有限且有静态最大深度 |
| apply 后 Core Fact reaction | 否 | 转下一 Tick Command |
| EndFixed 新结构 | 否 | Kernel 已结束；下一 Tick 可见 |
| Boundary Fact | 否（Core） | 只供固定批次后的托管投影 |

“EndFixed 下一 Tick 可见”足以支撑结构生命周期，但不足以支撑 Activate/Commit/Cancel 的 same-tick 业务链。因此三者必须设计为 slab 状态跃迁，而不是结构变化。

## 9. 禁止方向

- 自建 `EndGASStructuralCommitECBSystem` 或多次 playback。
- phase 之间播放 ECB 以制造 same-tick Entity。
- 每个 Boundary 消费者直接扫描/清理 ECS outbox。
- 销毁 ASC 后仍从普通 Component 读取事实身份。
- live outbox 尚未成功接管就清空，或 cleanup shell 未 drain 就移除 cleanup buffer。
- Post-Fixed drain 创建跨 batch ECB，或 World shutdown 时依赖 ECB system `OnDestroy` 播放 pending command。
- managed staging 未返回幂等 receipt 就推进 watermark/`Accepted`。
- 把同一 Boundary fact 同时写入 ASC 与 Session outbox，或让 Battle/Session fact 随机选择代表 ASC。

## 10. 验收

- `0..N` 固定 Tick 后，drain 保持事实的 Tick/稳定序号顺序且不重不漏。
- live owner 成功 drain 后 accepted prefix 被清除且 late tail 保留；失败时冻结 range 原样保留并显式报错。
- `BatchId/InFlightWatermark` 重试不会重复分发；staging receipt 之前绝不清 outbox。
- ASC 在事实写入同 Tick 被销毁时，cleanup shell 仍可输出事实，并由下一 Kernel prepass + 同 Tick EndFixed 最终消失。
- Session/ASC spawn 测试确认 cleanup buffer 与 drain state 被显式添加；零 ASC Session stop 仍产生并交付 Session terminal fact。
- headless shutdown 在最后完整 EndFixed 后 FinalDrain；失败保持同一 InFlight 并阻止 Disposed，成功时 cleanup audit 已接管、无遗留 shell，且只在 World/Blob/managed resource 释放后生成 `DisposedReceipt/ValidationResultSeal`。
- `BeginClose/PumpShutdown` 路径不再运行 Kernel或增加 SimulationTick；最终 seal 后尝试新增任意 gameplay/Boundary/teardown fact 必须失败。
- ECB 只有标准 EndFixed playback；Kernel 内没有依赖 playback 的 same-tick读取。
