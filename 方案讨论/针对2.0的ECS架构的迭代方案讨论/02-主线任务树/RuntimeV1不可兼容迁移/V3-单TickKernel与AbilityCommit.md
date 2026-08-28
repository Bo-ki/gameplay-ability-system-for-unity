# V3 单 TickKernel 与 Ability Commit

> 状态：V2 后；与 V2/V4 同一集成窗口 | 前置：V2

## Owner 输入

- [当前 schedule/runner 基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标 Spec：物理调度、single writer、Ability](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- [配置 program/CapacityProof Owner](../../01-目标态架构共识/25-配置语义编译契约与CapacityProof统一裁决Spec.md)

## 目标

用单 `GasFixedTickSystemGroup`、单 Core `GasTickKernelSystem` 和标准 EndFixed 替换旧五组主链，实现 `AscOwnerCommandWave -> TargetPrepare -> SessionFaultReduce -> TargetPublish`、target-owned single writer 与 Activate/Commit/Cancel transaction。

## 执行范围

1. Ingress、Kernel 命名 Job DAG、tick scratch owner 与最终 dependency。
2. `OwnerPlanBuild` 按 ASC/stable request canonical grouping，在 shadow 中完成 Activate/Commit 业务二次检查、同 ASC read-your-writes、post-commit capture candidate 与完整生成上界；零权威写。
3. `WholeTickInfraAdmission` 逐字段消费 generated CapacityProof，在全部 owner/target work展开后、任何 gameplay mutation前同时预留 target sparse overlay、durable publish credit、scratch/slab/payload/pending/fact/Cue/ECB intent；失败时整 Tick gameplay 权威零写并产生 `InfraAdmissionFault`。
4. `AscOwnerCommandWave` 只提交 admitted no-fail CommitPlan。`TargetPrepare` 按 frozen target ASC/application identity regroup，每 target 单 writer只写 tick-local shadow/intent，按 generated canonical op 保持 read-your-writes，禁止 durable target 写。
5. 全部 prepare 完成后，`SessionFaultReduce` 以 Session 为粒度按 `(TargetAscStableId, ApplicationId, StabilizationRound, StateHash)` 选择唯一首因；任一 fatal 丢弃全 Tick target shadow/intent，已提交的 source Cost/Gate/Committed state保留。
6. 仅 `SessionFaultReduce=Success` 时执行 per-target并行 no-fail `TargetPublish`，一次写入 durable authority，随后才开放 StableFact/Boundary/ECB lane；AutoChess 默认 FrozenASC + AliveOnly，非法 target typed reject，禁止 implicit self fallback。
7. committed-work-wins：source Commit 后死亡不撤回远端 application；target 首次 death crossing 后后续 AliveOnly application typed reject。
8. Wait 在 observed ASC writer 内 `sample+register` 线性化；Level 可立即完成，Edge/Event 不追溯，recipient 稳定全序，continuation resume 进入 T+1。
9. DirectEffectProgram 静态闭合工作可 same tick；post-apply overflow/reaction/cross-owner dynamic child 统一 T+1。
10. 独立 World 显式注册标准 EndFixed；提供唯一 session tick API。

## 验收

- 无跨 System scratch、phase 间 `Complete()`、static NativeContainer 或跨 ASC 随机并行写。
- business Commit reject 无部分 cost/cooldown；InfraAdmissionFault 时整个 Tick gameplay权威零写；admitted wave不再有容量失败。重复 Commit/Cancel 不重复生效。
- fault 注入时 target durable Attribute/Tag/Effect/Grant/Activation、Fact、Cue、ECB intent 均零写；OwnerWave 已成功 Cost/Gate 保留。
- 不同 worker/chunk/batch 切分得到相同 FaultId、CommittedPrefixHash 与 raw durable state。
- CapacityProof 的 target/program expansion、touched slots、overlay/publish bytes、projection/cleanup/fact/Cue/ECB intent 全部有实际 admission consumer；只生成 report 不算完成。
- Owner command range、target application range、wait recipient 与 typed outcome 在输入顺序扰动下稳定；TargetPrepare/SessionFaultReduce/TargetPublish 的 ready/fatal/publish cut 可复现。
- 首次致死 application 结算 damage/overkill；后续 AliveOnly reject 不记 damage/assist/Cue Executed。
- ECB-created Entity 当前 Kernel 不可见，下一 simulation tick才进入 Core。
- 独立 World/headless test 证明标准 EndFixed实际 playback。
- 五段旧 group和自定义 ECB不再作为运行调度。

## 交还

Job DAG、TargetPrepare/SessionFaultReduce/TargetPublish writer ownership、CapacityProof consumer map、Profiler marker/counter、schedule deletion list 与 V4 stabilization 接口。
