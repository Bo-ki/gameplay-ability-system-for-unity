# QRY-03: Optional 分支用 chunk 级判断

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: 与 `元信息/版本与PackageCache.md` 当前基线一致

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `iterating-data-ijobchunk-implement.md`

## 规则声明
当多个 archetype 共享同一主流程、但部分 chunk 含有可选 component 时，可用 `IJobChunk` + `chunk.Has(ref TypeHandle)` 在同一 system 内按 chunk 分支。是否合并 system/query，必须同时评估 query 选择性、每个 chunk 的分支工作量、依赖链和 Profiler 结果。

## 为什么
按 component 组合机械拆分 system 会增加 system 固定开销；机械合并也可能抓取无用数据或造成分支浪费。`chunk.Has` 每个 chunk 判断一次，可避免逐 entity 重复检查，但 Unity 没有给出固定占比阈值。`WithAll`、可选 component、enableable component 或拆分 query 的选择，应依据 archetype/chunk 分布、过滤收益、enableable 的依赖与迭代成本以及目标平台测量结果。

## EX-GAS 诊断
审查 Effect application 路径中按 effect type 拆分的 system，确认拆分是否换来了有效过滤或更简单的依赖；若主流程高度一致，再评估合并为 `IJobChunk` 并按 chunk 读取可选数据。

## 检查方法
- 搜索 Runtime Core 中是否存在 `[WithAll(typeof(A))]` 和 `[WithAll(typeof(A), typeof(B))]` 两个独立 system 处理同一类逻辑
- 对比拆分与合并方案的 chunk 命中率、抓取数据量、调度/等待成本和实际耗时
