# V5 Boundary Drain / Cue / 销毁交接

> 状态：V3/V4 后领取 | 前置：V3、V4

## Owner 输入

- [当前 Boundary/Cue 基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [第三轮审查增量事实](../../00-当前架构事实/RuntimeV1第三轮多Agent架构与性能审查事实.md)
- [Request ledger、per-Battle Gate 与 close cut](../../01-目标态架构共识/16-纯ECS内核与边界重划分/16-02-BoundaryCommand与CoreCommandResolveSpec.md)
- [单 Drain、FinalDrain 与销毁交接](../../01-目标态架构共识/03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md)
- [Snapshot/Cue/API health](../../01-目标态架构共识/16-纯ECS内核与边界重划分/16-05-SnapshotIdentityApiHealthSpec.md)
- [Headless 证据与 ValidationResultSeal](../../01-目标态架构共识/10-AutoChess无头验收Spec.md)

## 目标

以单 ECS Drain 和 managed immutable ring 替换多 Boundary ECS consumer，闭合 Accepted Request 终态、per-Battle 双切面封印、Snapshot/Cue reconcile、可重入 FinalDrain、DisposedReceipt 与 ValidationResultSeal。

## 执行范围

1. CommandPort 在 `SessionIngressGate` 上冻结 `RequestKey/PayloadHash/RequestSequence/AssignedAvailableTick`；exact sparse ledger 为每个 Accepted Request 保留唯一 terminal envelope、BoundaryAccepted、retire 与 bounded tombstone。
2. per-ASC/Session cleanup outbox、owner sequence、accepted watermark 与有界 managed immutable batch ring；每个 outer update 在 `0..N` FixedStep 后执行一次 Drain，0 FixedStep 也推进同 BatchId retry且不增加 SimulationTick。
3. Battle terminal 使用 `CoreOutboxCut + GateRequestCut` 双切面：先关闭该 Battle gate并逐项终结 accepted tail；两cut对应range均被managed staging接受且ReadModel达到同identity `SnapshotCut`后，才生成BattleOutcomeSeal。`ManagedBoundaryBatchSequence`只证明cut接管，不能替代SnapshotCut；其他 Battle继续接收。
4. UI/Cue/Replay/Debugger/Headless 只消费 immutable Boundary batch、SnapshotCut 或 typed API。SnapshotCut 与 Boundary Cut identity一致；overflow/gap按 typed reconcile协议恢复，禁止 presentation回写 Core。
5. OnActive/WhileActive/Executed/Removed；生命周期 key=`SimulationEpoch+ActiveEffectHandle+ActiveCycleOrdinal+CueDefinitionOrdinal`。Executed 另用 `(EffectApplicationId,CueDefinitionOrdinal)` 或 `(SimulationEpoch,ActiveEffectHandle,PeriodExecutionOrdinal,CueDefinitionOrdinal)`，payload 含 Avatar binding generation。managed resource load、取消与 late callback使用 owner token/generation，资源迟到不能回写已撤销生命周期。
6. managed staging 成功接管 immutable batch 后才清 ECS accepted prefix；late tail保留并回 Pending。`DrainState=Accepted` 后由下次 Kernel prepass记录 EndFixed removal，无下一 tick则 teardown直接清理。
7. `BeginClose` 只关闭新 ingress并冻结 session cut；`PumpShutdown` 以同 BatchId/InFlightWatermark 可重入重试 FinalDrain，不推进 FixedStep/Kernel/SimulationTick。先接管 gameplay/request terminal，再清 cleanup shell/resource并接管独立 teardown audit。
8. 全部 audit receipt成功后才释放 World/Blob/registry并生成 managed `DisposedReceipt`；`ValidationResultSeal` 最后绑定 Battle/Session outcome、Request terminal membership与canonical digests、`FinalDrainAccepted`及其 accepted batch digests、TeardownAuditHash 与 DisposedReceipt。Seal 后禁止新增 Boundary/Cue/audit。

## 验收

- 多 fixed tick catch-up 不丢 fact，consumer收到同一 immutable batch。
- consumer 不访问/清 ECS DynamicBuffer，不取得 raw Entity/World/EntityManager。
- Null presentation resource不丢 Core Cue request。
- 接管失败不清 outbox；retry 不重复 delivery，overflow/backpressure 产生 typed outcome。
- 同 RequestKey 同/异 PayloadHash、sparse hole、normal/fault/closing terminal、BoundaryAccepted/retire/history-expired 均恰一次且可对账。
- Battle A 的 Core/Gate双cut被managed accepted且ReadModel达到SnapshotCut后可独立Seal，Battle B继续运行；cut前accepted tail全部有`RejectedBattleTerminal`或正常terminal，cut后同步拒绝且不入journal。
- terminal fact 在 ASC physical destroy 前完成 gameplay FinalDrain；cleanup shell 在下一标准 EndFixed 或 teardown 释放。
- Validation/Headless overflow失败；Presentation overflow记录 dropped range并请求 reconcile。
- FinalDrain 不依赖 wall-clock 或固定 gameplay flush tick；staging失败时重复 PumpShutdown不改变 SimulationTick或语义 hash。
- ValidationResultSeal 只能发生在 `FinalDrainAccepted`、TeardownAuditHash 与 DisposedReceipt 齐备之后；Seal 后零新 BoundaryBatch/GameplayFact/Cue/TeardownAudit。

## 交还

Request ledger/gate schema、Batch/SnapshotCut schema、retention/overflow evidence、Cue lifecycle与late-callback tests、FinalDrain/Disposed/ValidationResultSeal Journaling及 V6 adapter API。
