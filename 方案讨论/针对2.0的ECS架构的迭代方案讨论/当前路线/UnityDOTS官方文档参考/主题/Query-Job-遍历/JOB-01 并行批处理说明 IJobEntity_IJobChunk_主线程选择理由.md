# JOB-01: 并行批处理说明 IJobEntity/IJobChunk/主线程选择理由

**严重度**: P0
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — 核心概念：三种遍历方式对比、调度模式对比

## 规则声明
每个遍历路径必须明确选择 IJobEntity、IJobChunk 或 主线程并附选择理由。默认选择是 IJobEntity（IJobChunk 用于 chunk 级操作，主线程仅用于小规模/debug）。设计文档/代码注释中说明选用理由。

## 为什么
三种方式各有适用场景与性能特征，选择错误导致性能退化、丧失并行度或增加不必要实现复杂度。统一的选择标准和理由文档帮助团队维护一致的高性能架构。

**选择指南：**

| 方式 | 执行线程 | Burst | 适用规模 | Sync Point |
|------|----------|-------|----------|------------|
| `SystemAPI.Query` | 主线程 | 否 | 小规模（<100） | **是** |
| `IJobEntity` | Worker 线程 | 是 | 中大规模（100-10K） | 否 |
| `IJobChunk` | Worker 线程 | 是 | 大规模（>10K） | 否 |

**调度模式选择：**

| 方式 | 并行度 | 适用场景 |
|------|--------|----------|
| `ScheduleParallel` | 多 worker 线程 | 默认首选，无 entity 间依赖 |
| `ScheduleSingle` | 单 worker 线程 | 需要顺序处理、全局状态 |
| `Schedule`（按 chunk） | 每个 chunk 一个 job | chunk 间无依赖的批处理 |
| `Run` | 主线程同步 | proof/debug 小规模，触发 sync point |

## EX-GAS 诊断
Runtime Core 所有遍历路径应注明选择理由，Code review 检查。新增遍历路径 PR 必须附带选择说明。

## 检查方法
- Code review 确认每个遍历选择有理由文档
- 检查 Runtime Core 中是否使用不恰当的遍历方式
