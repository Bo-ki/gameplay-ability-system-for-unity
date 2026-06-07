# GameplayEvent stream carrier 移除目标态兑现记录

> 日期：2026-06-08

本记录对应 `03E-04-GameplayFactSpec` 中 “Core reaction fact 归属 ASC owner-local carrier，Boundary observation 由导出系统单向投影” 的 carrier 清理切片。

## 兑现点

- singleton `EffectCommandStream` 不再拥有 `GameplayEventBuffer`，stream 只保留 command / set-by-caller 等仍在迁移中的 owner 调度状态。
- FramePrepare 不再清理 stream fact buffer，`GEEffectCommandStreamComponent` 也不再保存 legacy fact projection / event bridge cursor。
- Boundary fact export 不再把 stream `GameplayEventBuffer` 当 legacy migration input，只从 owner-local core fact flush 后的边界记录链路工作。
- `EBoundaryObservationFactSource` 不再保留 `LegacyStream` 入口，避免目标态枚举继续表达已删除 carrier。
- AutoChess execution damage calculation 不再从 singleton command stream 扫描 execution command，而是在 ASC owner chunk 内消费 owner-local command buffer、写 owner-local pending delta，并尽量写 owner-local execution fact。
- 诊断脚本已经把 stream fact carrier、legacy fact lookup、AutoChess singleton execution command reader 列为 forbidden。

## 未完成点

- `GameplayEventBuffer` 命名仍保留为 fact payload 结构，后续可以在 carrier 清理稳定后再评估是否重命名。
- 仍需继续审查 command、set-by-caller、instant spec、generated reduce、active mutation 和 Boundary observation 的剩余 singleton owner 面。
- 本切片未运行 Unity headless AutoChess、Unity Test Runner 或性能 profile。
