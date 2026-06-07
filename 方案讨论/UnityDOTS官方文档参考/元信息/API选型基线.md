# API 选型基线（兼容入口）

> 本文件只保留旧路径兼容，不再维护正文。唯一 owner 是
> [../主题/20-GASRuntimeCore-API选型基线.md](../主题/20-GASRuntimeCore-API选型基线.md)。

## 使用规则

1. Runtime Core / Debugger / Luban / AutoChessDemo 任务需要 API selection checkpoint 时，直接读取 `主题/20-GASRuntimeCore-API选型基线.md`。
2. 不要在本文件补充 TypedFacts、ActiveEffectStore、Enableable、DynamicBuffer 或 NativeStream 的新结论。
3. 如果发现 `主题/20` 与当前代码事实或本地 PackageCache 官方文档冲突，按 `主题/21-官方文档覆盖与流程闭环.md` 反哺，而不是修改本兼容入口。

## 迁移原因

旧正文曾保留与当前 `主题/20` 冲突的 TypedFacts / ActiveEffectStore 过时口径。为避免后续 Agent 从旧副本领取任务，本文件不再承载任何 API 结论。
