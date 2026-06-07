# 2026-06-08 ASC / ActiveEffect Fact Owner-Local 决策归档

## 决策

Core fact 的 source lane 必须写 owner-local carrier，不得把 singleton `GameplayEventBuffer` 当作 Core reaction 总线。ASC command resolve 与 ActiveEffect lifecycle 都归入 Runtime Core owner lane，fact 输出落到目标 ASC 的 `OwnerLocalGameplayFactBuffer`。

## 架构含义

1. ASC command resolve 产出的 tag、attribute base value 和 ability lifecycle request fact 是 Core reaction 输入，写 ASC owner-local fact buffer。
2. ActiveEffect apply、remove、stack、period、granted ability cleanup 相关 fact 是 ActiveEffect lifecycle lane 的 Core fact，也写目标 ASC owner-local fact buffer。
3. SourceGenerator / CodeGen 只能生成 owner-local writer glue 和 pure record 构造，不得生成 singleton fact stream 写入路径。
4. Boundary observation 只能由 `GameplayBoundaryFactExportSystem` 从 owner-local facts 单向导出到 `BoundaryObservationFactBuffer`。
5. Presentation、Replay、Debugger 和 AutoChess report 继续只读 Boundary observation 或 diagnostics snapshot，不直接读取 Core owner-local facts。

## 当前妥协

- `GEEffectCommandStreamComponent` 仍承担 fact sequence 分配和 legacy migration cursor；这是过渡事实，不是目标态 owner。
- `GameplayBoundaryFactExportSystem` 仍读取 legacy `GameplayEventBuffer` 作为迁移输入。所有 legacy writer 迁完后应移除该输入面。

## 下一步

继续迁移剩余 direct `GameplayEventBuffer` writer，优先处理 Runtime Core 内部 writer，再处理 AutoChess demo adapter writer。

