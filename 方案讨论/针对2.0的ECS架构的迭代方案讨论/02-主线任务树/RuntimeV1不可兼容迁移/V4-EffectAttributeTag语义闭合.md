# V4 Effect / Attribute / Tag 语义闭合

> 状态：V3 后；与 V2/V3 同一集成窗口 | 前置：V1-V3

## Owner 输入

- [Requirement/Effect 当前基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标 Spec：Spec/Active、Capture、Inhibition、Aggregator、TagCount](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- [Typed contract/Live/Grant/CapacityProof Owner](../../01-目标态架构共识/25-配置语义编译契约与CapacityProof统一裁决Spec.md)

## 目标

在 target-owned apply/stabilization 内闭合经典 GE、Attribute 和 Tag 语义，删除旧 execution/写入旁路。

## 执行范围

1. Definition -> Spec -> Active 与四类 capture。
2. application/ongoing/immunity、inhibit/reactivate 和 cycle/operation budget。
3. stacking key、overflow、absolute duration/period、pause/reset/catch-up。
4. 单 Aggregator apply lane 与稳定 channel/override 顺序。
5. TagCount/ancestor count/presence mask cache/contribution ledger。
6. AutoChess damage 公式的 pure evaluator 接口，不在本任务迁移 runner/report。
7. owner-local PeriodDue 在 DueTick 同 tick执行；Due=End 的定义内顺序、execute-on-apply、refresh/reset、expiry、inhibit skip/no-catch-up 全部由 Definition 冻结。
8. Death 首次 crossing 冻结 killer/overkill provenance，取消未 Commit 行动并清 ActiveEffect/Cue；corpse ASC 保留到结果快照/teardown。
9. Granted Ability 的 `LeaveGranted` slot 保持可调用；其 UE `DoNothing` 可观察语义通过内部 detach ownership 与冻结 provenance 实现。仅 `RemoveWhenAllActivationsEnd` 绑定 activation lifetime，`EmittedApplicationRefs` 独立。
10. Cross-ASC Live 只消费 generated `FrozenProjectionPayload`，保证 revision/value 原子配对，并按 consumer field canonical coalesce；缺 projection bytes/fanout/update 上界的 Definition 不进入 Catalog。
11. Grant removal -> child End -> OwnedContribution/Continuation/Subscription cleanup 纳入 generated signed dependency graph；`EmittedApplicationRef` 区分 `AuditOnly/CleanupRight`，retention watermark 与每 Activation/ASC 上界进入 CapacityProof。
12. Target apply/stabilization 只写 V3 的 target shadow，generated program/CapacityProof 定界 touched slots、overlay/publish bytes、transition/work、projection/cleanup、Fact/Cue/ECB intent。

## AutoChess 必过语义

- 9203：`StackKey=(Definition,TargetASC,SourceASC)`、AggregateBySource、ReplaceLatest；Source Attack 在 `SourceSpecProjection` snapshot，每次成功 application 用本次 snapshot 替换 payload/provenance；`Attack*0.3*StackCount`、Duration 8、Period 2、limit 3、ExecuteOnApply=false。
- 9203：成功 reapply/cap reapply reset next due 但不刷新 duration；Due=End 先 period 后 expiry；expiry remove-one + refresh duration + reset period；inhibit duration 继续、period skip/no catch-up。
- 9207：Instant pure evaluator，显式 `Health.BaseValue/CurrentValue/MaxValue` View 契约，不创建无限 ActiveEffect marker。
- current 9203 非法 `StackingType=9203`、sourcegen hidden overlay、9204 固定 `-1` 与 9207 marker 全部 breaking/bake-fail，无兼容支路。

## 验收

- requirement/capture/stack/period/inhibition/aggregator/tag count semantic tests 全绿。
- 相同输入、不同物理输入顺序得到相同 semantic hash。
- Instant/Execution/ActiveEffect 不再分别写最终 Attribute。
- legacy GE execution/entity 与 shared requirement range 无运行 fallback。
- stabilization cycle/预算溢出显式失败并产出 evidence。
- poison cap/expiry/inhibit/period-kill provenance、9207 Base/Current、death fan-in 与 LeaveGranted 三种 policy 的固定微场景全绿。
- Source/Target × Snapshot/Live 的 projection payload、revision/value、bytes/fanout/coalesce 固定向量全绿；缺任一 proof 的负例 publish-fail，不使用 FallbackMagnitude/per-phase sampling 降级。
- Grant 三种 remove policy、child active、Continuation/Subscription cleanup、AuditOnly/CleanupRight 与 watermark/硬上界固定向量全绿。
- 257+ Tag fixture 按 LayoutProof 合法承载或显式失败；未知/越界 Tag 不得截断、no-op 或让固定 mask 成为 authority。
- Target stabilization fault 不产生 durable target写/Fact/Cue/ECB intent，物理切分不改变 FaultId/CommittedPrefixHash。

## 交还

语义矩阵、projection/dependency/CapacityProof consumer证据、pure evaluator清单、旧旁路删除清单、V5 fact/cue payload。
