# V5 Boundary Drain / Cue / 销毁交接

> 状态：V3/V4 后领取 | 前置：V3、V4

## Owner 输入

- [当前 Boundary/Cue 基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标 Spec：单 Drain、Cue、Destroy handoff](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)

## 目标

以单 ECS Drain 和 managed immutable ring 替换多 Boundary ECS consumer，闭合 managed 接管、Cue 四阶段、gameplay FinalDrain 与 teardown 销毁交接。

## 执行范围

1. per-ASC Boundary fact、owner sequence、dirty marker。
2. catch-up 后 deterministic Drain 与有界 managed batch ring。
3. UI/Cue/Replay/Debugger/Headless consumer protocol。
4. OnActive/WhileActive/Executed/Removed；生命周期 key=`SimulationEpoch+ActiveEffectHandle+ActiveCycleOrdinal+CueDefinitionOrdinal`。Executed 另用 `(EffectApplicationId,CueDefinitionOrdinal)` 或 `(SimulationEpoch,ActiveEffectHandle,PeriodExecutionOrdinal,CueDefinitionOrdinal)`，payload 含 Avatar binding generation。
5. managed staging 成功接管 immutable batch 后才清 ECS outbox；`DrainState=Accepted` 后由下次 Kernel prepass 记录 EndFixed removal，无下一 tick 则 teardown 直接清理。
6. gameplay `FinalDrain` 只接管 gameplay-scope facts；接管完成后允许冻结 BattleOutcome/hash。cleanup/teardown facts 使用独立 scope 与 audit，不改 battle hash。
7. Death 不立即 destroy corpse ASC；结果快照完成后由 Session teardown 统一销毁并完成零残留审计。

## 验收

- 多 fixed tick catch-up 不丢 fact，consumer收到同一 immutable batch。
- consumer 不访问/清 ECS DynamicBuffer，不取得 raw Entity/World/EntityManager。
- Null presentation resource不丢 Core Cue request。
- 接管失败不清 outbox；retry 不重复 delivery，overflow/backpressure 产生 typed outcome。
- terminal fact 在 ASC physical destroy 前完成 gameplay FinalDrain；cleanup shell 在下一标准 EndFixed 或 teardown 释放。
- Validation/Headless overflow失败；Presentation overflow记录 dropped range并请求 reconcile。
- FinalDrain 不依赖 wall-clock 或固定 gameplay flush tick；最终 ValidationResult 等 teardown audit 后返回。

## 交还

Batch schema、retention/overflow evidence、Cue lifecycle tests、destroy handoff Journaling 与 V6 adapter API。
