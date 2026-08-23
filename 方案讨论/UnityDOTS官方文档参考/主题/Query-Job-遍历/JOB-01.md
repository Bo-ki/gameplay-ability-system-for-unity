# JOB-01: 并行批处理说明 IJobEntity/IJobChunk/主线程选择理由

**严重度**: P0
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-systemapi-query.md`、`iterating-data-ijobentity.md`、`iterating-data-ijobchunk-implement.md`

## 规则声明
每个重要遍历路径应在设计或评审中说明为何选择 `SystemAPI.Query`、`IJobEntity` 或 `IJobChunk`。选择依据是数据访问形态、依赖关系、可并行工作量和 Profiler 结果，而不是统一的实体数量区间。

## 为什么
三种方式各有适用场景与性能特征，选择错误导致性能退化、丧失并行度或增加不必要实现复杂度。统一的选择标准和理由文档帮助团队维护一致的高性能架构。

**选择指南：**

| 方式 | 执行位置 | 主要用途 | 依赖行为 |
|------|----------|----------|----------|
| `SystemAPI.Query` | 主线程 | 简洁的逐实体主线程逻辑 | foreach 前自动完成必要依赖；依赖已完成时不会额外等待 |
| `IJobEntity` | `Run` 时主线程；`Schedule*` 时 job system | 常规逐实体变换 | 调度版本通过 `JobHandle` 传递依赖 |
| `IJobChunk` | `Run` 时主线程；`Schedule*` 时 job system | chunk 级访问、可选组件、显式 mask/遍历控制 | 调度版本通过 `JobHandle` 传递依赖 |

**调度模式选择：**

| 方式 | 并行度 | 适用场景 |
|------|--------|----------|
| `ScheduleParallel` | 多 worker 线程 | 默认首选，无 entity 间依赖 |
| `Schedule` | 单个 job 顺序处理匹配 chunk | 需要顺序处理或不值得并行调度 |
| `Run` | 主线程同步 | 明确需要立即得到结果；会完成该 job 的必要依赖 |

## EX-GAS 诊断
Runtime Core 所有遍历路径应注明选择理由，Code review 检查。新增遍历路径 PR 必须附带选择说明。

## 检查方法
- Code review 确认每个遍历选择有理由文档
- 检查 Runtime Core 中是否使用不恰当的遍历方式
