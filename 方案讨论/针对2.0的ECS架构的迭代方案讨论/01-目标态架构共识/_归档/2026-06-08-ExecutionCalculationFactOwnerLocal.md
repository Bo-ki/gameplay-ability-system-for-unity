# ExecutionCalculation fact owner-local 目标态兑现记录

> 日期：2026-06-08

本记录对应 `03E-04-GameplayFactSpec` 中 “Core reaction fact 归属 ASC owner-local carrier，Boundary observation 由导出系统单向投影” 的落地切片。

## 兑现点

- `GEExecutionCalculationSystem` 保留 `NativeStream` 并行收集 + 单 job deterministic merge，但 merge 输出从 singleton fact stream 改为 ASC-local `OwnerLocalGameplayFactBuffer`。
- fact owner 规则为 `TargetAsc` 优先，`SourceAsc` 兜底；无 owner 的 record 被丢弃，不再污染 singleton stream。
- `GASAttributeModifierDeltaApplySystem` 在 ASC chunk 内同时完成 pending delta apply、linked execution fact patch 和 Attribute change fact append，fact lane 不再跨 owner 随机回写 singleton stream。
- `AutoChessExecuteDamageCalculationSystem` 的真实业务 damage output 与 pending delta 共享目标 ASC owner-local fact lane，`SourceDeltaSequence` 仍可被 core apply 阶段回填 old/new/value。
- 诊断门把 Runtime Core writer 与 AutoChess adapter writer 一起纳入 owner-local fact 防回流。

## 未完成点

- 本切片未运行 Unity headless AutoChess，也未输出 x100 / x1000 规模性能证据。
- Boundary export 仍兼容 legacy stream fact migration input；这只是迁移期读面，不是本切片移除目标。
- command/spec/delta/fact 的剩余 proof-only stream carrier 仍需继续按 P0-D 拆除。
