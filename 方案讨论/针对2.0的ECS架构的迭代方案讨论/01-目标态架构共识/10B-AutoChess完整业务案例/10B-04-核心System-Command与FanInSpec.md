# 10B-04 Command、Activation 与 Effect Fan-In 投影

## 结论

AutoChess 不新增核心 System。普攻、技能和 AI 命令全部成为通用 Kernel 的输入/Job 数据；Kernel 先执行按 ASC owner 分组的 `AscOwnerCommandWave`，再执行按 target ASC 分组的 `TargetPrepare -> SessionFaultReduce -> TargetPublish`。“核心 System”仅保留为业务概念标题。

## Owner 提交与 Target 三段事务

- `OwnerPlanBuild` 按 owner ASC 与稳定 request identity 在 shadow 中完成 CanActivate/Commit 业务检查和 canonical read-your-writes，并产出完整生成上界；它不写权威状态。
- `WholeTickInfraAdmission` 在所有 owner/target work展开后统一预留 downstream scratch/slab/payload/pending/fact/outbox。失败是 tick-level `InfraAdmissionFault`：全部 gameplay 权威零写，DAG 内只写固定大小 `SessionFaultLatch=Detected` 与 sealed subset 证据。本 Tick不写逐 request Boundary fact；outer completion 后 FaultClose 与 CommandPort accept 在同一 `SessionIngressGate` 线性化，冻结关闭前 accepted-outstanding 的 first/last/count/hash摘要；包含 unsealed/future tail 的精确成员由 Request ledger判定。这些请求由同一 FaultId统一终结，原 inbox/接收 journal保留供诊断审计且不重放；关闭后请求同步拒绝。
- `AscOwnerCommandWave` 只提交已通过整 Tick admission 的 no-fail CommitPlan；cost/cooldown、tag、target 引用等业务条件已在 plan 中闭合，提交不得再发生容量失败或部分 mutation。
- Commit 后远端 application 进入 `TargetPrepare`，按 target ASC 与 application identity 稳定排序并在 shadow 内 canonical read-your-writes；全部 target prepare 完成后，经唯一 `SessionFaultReduce` 成功才由 `TargetPublish` 无失败发布。
- TargetPrepare 产生的普通 reaction/cross-owner dynamic child 统一进入 `T+1`；越过证明上界的 overflow 是 Session-fatal，不作为延迟队列；Definition 静态闭合的 `DirectEffectProgram` 可在当前 tick 完成。

## 普攻

1. AI 按 `ActiveDue > Finisher > Primary` 选择 ability，并以稳定 `ScenarioUnitId` 选择 target，提交 Attack/Activate intent。
2. Kernel resolve GrantedAbilityHandle，执行 CanActivate。
3. Activation 进入 RunningUncommitted；Commit 重查攻速/cooldown/stun/cost。
4. Definition DirectEffectProgram 生成 damage application command。
5. target grouping 前固定 source/target identity、ApplicationId、target ordinal 和 capture input；AutoChess 使用 frozen target ASC，非法 target typed reject，绝不 fallback self。

## 技能

盾击产生 damage+stun 静态程序；冰霜新星 target rule 输出至多 3 个稳定目标；毒刃产生 stackable poison application。一个 Activation 可以并行等待动画/逻辑 wake 或 target data，但表现动画 completion 不作为权威 commit 条件，除非通过明确 Continuation command 回到后续 tick。

## Fan-In

来自普攻、技能、period、羁绊和上一 tick reaction 的 commands 统一写 tick scratch，按 target ASC/canonical key 分组。一个目标 invocation 串行处理自身 range，不同目标并行。

排序字段至少覆盖 target、available tick、source ASC、source sequence、causality、program node、target ordinal、application id。不得以单位数组顺序、chunk 顺序或 Job 完成顺序决定伤害先后。

## 多目标与原子性

一次冰霜新星命中 3 个目标共享 Activation/Causality，但三个 target application 各自产生独立 typed业务 outcome；v1 不承诺跨三个 ASC 的分布式业务回滚。每个目标拥有独立 target capture/ApplicationId/result。若任一 TargetPrepare 产生 stabilization/identity/proof fatal，则它不是某一目标的业务失败：SessionFaultReduce 丢弃本 Tick全部 target shadow并阻止 Publish。

## Target life policy 与 committed-work-wins

- AutoChess 主业务固定 `FrozenAsc + AliveOnly`；Commit 前 target 已失效则 Activate/Commit 拒绝。
- Commit 后 source 死亡不撤回已发出的远端工作。
- target canonical range 内首次 `Alive -> Dead` crossing 仍由致死 application 结算 damage、overkill 与 provenance；后续 `AliveOnly` application typed reject，不计 damage、assist 或 Cue Executed。
- corpse ASC 保留至结果快照与 teardown；不得用即时销毁代替 life-policy 判定。

## Wait 线性化

- Wait 在 observed ASC writer 内执行 `sample + register`；Level 条件已满足可立即完成，Edge/Event 不追溯历史。
- completion recipients 使用稳定全序；completion fact 可在当前 tick 产生，但 continuation 恢复按动态 child 规则进入 `T+1`。

## 验收

- OwnerPlanBuild 的 Activate/Commit业务检查有 per-request typed outcome；WholeTickInfraAdmission失败产生 tick-level `SessionFaultLatch`，其 IngressClosed first/last/count/hash仅作摘要，关闭点前全部已接受未终结请求由 exact ledger membership逐项终结；不往未准入 gameplay fact/outbox写回执，且整 Tick gameplay权威零写；admitted CommitPlan一次性 no-fail提交。
- 同 tick 多单位集火同一目标仍只有 target single writer。
- Commit 失败不消耗 Mana/冷却、不产生 Effect command。
- 目标死亡/stale/binding change 返回稳定 reject reason。
- 同输入重跑 command range、target order 和 result hash 一致。
- source Commit 后死亡不撤回远端 application；首次死亡后的 AliveOnly application 稳定拒绝且不污染伤害/助攻统计。
