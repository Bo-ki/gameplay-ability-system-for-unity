# V3 单 TickKernel 与 Ability Commit

> 状态：V2 后；与 V2/V4 同一集成窗口 | 前置：V2

## Owner 输入

- [当前 schedule/runner 基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标 Spec：物理调度、single writer、Ability](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)

## 目标

用单 `GasFixedTickSystemGroup`、单 Core `GasTickKernelSystem` 和标准 EndFixed 替换旧五组主链，实现 `AscOwnerCommandWave -> AscTargetStateWave`、target-owned single writer 与 Activate/Commit/Cancel transaction。

## 执行范围

1. Ingress、Kernel 命名 Job DAG、tick scratch owner 与最终 dependency。
2. `OwnerPlanBuild` 按 ASC/stable request canonical grouping，在 shadow 中完成 Activate/Commit 业务二次检查、同 ASC read-your-writes、post-commit capture candidate 与完整生成上界；零权威写。
3. `WholeTickInfraAdmission` 在全部 owner/target work展开后、任何 gameplay mutation前统一预留 scratch/slab/payload/pending/fact/outbox；失败时整 Tick gameplay 权威零写并产生 `InfraAdmissionFault`。
4. `AscOwnerCommandWave` 只提交 admitted no-fail CommitPlan；`AscTargetStateWave` 按 frozen target ASC/application identity regroup，target 内 read-your-writes；AutoChess 默认 FrozenASC + AliveOnly，非法 target typed reject，禁止 implicit self fallback。
5. committed-work-wins：source Commit 后死亡不撤回远端 application；target 首次 death crossing 后后续 AliveOnly application typed reject。
6. Wait 在 observed ASC writer 内 `sample+register` 线性化；Level 可立即完成，Edge/Event 不追溯，recipient 稳定全序，continuation resume 进入 T+1。
7. DirectEffectProgram 静态闭合工作可 same tick；post-apply overflow/reaction/cross-owner dynamic child 统一 T+1。
8. 独立 World 显式注册标准 EndFixed；提供唯一 session tick API。

## 验收

- 无跨 System scratch、phase 间 `Complete()`、static NativeContainer 或跨 ASC 随机并行写。
- business Commit reject 无部分 cost/cooldown；InfraAdmissionFault 时整个 Tick gameplay权威零写；admitted wave不再有容量失败。重复 Commit/Cancel 不重复生效。
- 两波 command/application range、wait recipient 与 typed outcome 在输入顺序扰动下稳定。
- 首次致死 application 结算 damage/overkill；后续 AliveOnly reject 不记 damage/assist/Cue Executed。
- ECB-created Entity 当前 Kernel 不可见，下一 simulation tick才进入 Core。
- 独立 World/headless test 证明标准 EndFixed实际 playback。
- 五段旧 group和自定义 ECB不再作为运行调度。

## 交还

Job DAG、writer ownership、Profiler marker/counter、schedule deletion list 与 V4 stabilization 接口。
