# GAS ECS Runtime - Runtime Core Frame Backbone - AM3 / AM5 Rebind 与交还验证

## 父节点

[AM2B-FrameBackbone](README.md)

## 任务ID

`T1-RuntimeCore-AM2B-F`

## 状态

已完成（contract-first / rebind handoff，Unity验证待补跑）

## 当前问题

1. AM3 / AM5 当前任务描述仍保留 proof 小闭环历史。
2. Frame backbone 完成后，后续功能迁移必须重新绑定到新 phase、stream owner 和 debugger gate。

## 目标 / 目的

1. 更新 AM3 / AM5 任务描述，使其显式依赖 AM2B backbone。
2. 更新 `02-主线任务树/README.md` 当前看板，决定下一推荐领取。
3. 更新 `04-当前进度状态/当前窗口.md` 和 `ISSUE-009` 状态。

## 执行范围

1. `02-主线任务树/README.md`
2. `02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构/README.md`
3. `00-当前架构事实/核心问题诊断/ISSUE-009-RuntimeCoreFrameBackbone缺失.md`
4. `04-当前进度状态/当前窗口.md`

## 验收标准

1. AM2B 子任务链完成状态明确。
2. AM3 / AM5 后续任务不再允许绕过 frame backbone。
3. 当前窗口推荐领取下一个功能迁移任务，并说明原因。

## 测试链路

1. `git diff --check`
2. `rg -n "AM2B|Runtime Core Frame Backbone|AM3|AM5|当前推荐领取" "方案讨论/针对2.0的ECS架构的迭代方案讨论/当前路线"`

## 本轮进展

1. 新增 `GASRuntimeFrameBackboneRebindContract`，登记 AM3 / AM5 后续任务必须复用的 AM2B-A 到 AM2B-E 证据。
2. AM3 entry 已绑定 `CommandIngest / SpecEvaluation / DeltaApply / TypedFactProjection`，强制行动报告区分四类 command。
3. AM5 entry 已绑定 `ActiveEffectLifecycle / DeltaApply / StructuralPlayback`，强制行动报告区分四类 store surface。
4. 新增 `RuntimeFrameBackboneRebindContractTests`。
5. 当前下一推荐任务仍为 AM3。理由：AM3 已有 activation/cost/Timeline ApplyEffects proof，适合作为 backbone 后的第一条功能链路验证。
