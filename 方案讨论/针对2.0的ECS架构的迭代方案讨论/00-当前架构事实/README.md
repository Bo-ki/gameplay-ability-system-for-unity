# 00 当前架构事实

本目录维护当前版本的架构事实、核心问题诊断和合规审查。回答"当前版本到底长什么样、问题在哪里、哪些事实已经成立"。

> 上次更新：2026-05-26 | 审查范围：Assets/GAS/Runtime ~223 个 C# 文件，39 个 ISystem

## 文件索引

| 文件 | 内容 |
|---|---|
| [AutoChessDemo事实](AutoChessDemo事实.md) | 21 条事实 + 12 项专属缺陷 + 16 项 Spec 对照 |
| [Definition配置事实](Definition配置事实.md) | 9 条事实 + 15 项 Spec 对照 + 9 项合规缺陷 |
| [Runtime主链事实](Runtime主链事实.md) | 24 条事实 + DOTS 合规风险分析 |
| [当前架构图](当前架构图.md) | 当前实际链路图、DOTS 规范对照热图 |
| [模块索引](模块索引.md) | Runtime/Editor/Config 目录索引 |
| [P0-致命缺陷](P0-致命缺陷.md) | 7 个致命合规缺陷 |
| [P1-高风险缺陷](P1-高风险缺陷.md) | 14 个高风险合规缺陷 |
| [P2-改进建议](P2-改进建议.md) | 8 个改进建议 |
| ISSUE-001~011 | 11 个核心问题诊断文档 |

## 核心问题看板

| ID | 问题 | 状态 | 严重度 |
|----|------|------|--------|
| ISSUE-001 | GE 生命周期管线过重 | Active | P0 |
| ISSUE-002 | Observation 与 Runtime Core 热路径耦合 | Active | P0 |
| ISSUE-003 | Runtime Core Debugger 证据不足 | Active | P1 |
| ISSUE-004 | 结构变化边界脆弱 | Mitigated | P0 |
| ISSUE-005 | Generated 链路未反哺 Runtime Core | Active | P1 |
| ISSUE-006 | AutoChess Demo 边界混入 Runtime Core | Active | P1 |
| ISSUE-007 | 任务上下文与目标态 Spec 断链 | Active | P1 |
| ISSUE-008 | 目标态 Spec 尚未充分 Unity Entities 机制化 | Mitigated | P1 |
| ISSUE-009 | Runtime Core Frame Backbone 缺失 | Active | P1 |
| ISSUE-010 | 代码执行范式未切换到 DOTS | Active | P0 |
| ISSUE-011 | 临时 EntityQuery 泛滥与 API 承载选型错误 | Active | P0 |

## 当前总诊断

当前不应继续把问题描述为"缺少某个业务机制"。根本问题是 Runtime Core 主流程尚未从旧 lifecycle / global observation stream 迁移到 phase + stream 驱动模型；同时缺少 Unity DOTS 意义上的 Runtime Core Frame Backbone。

**关键数据点**：仅 2/39 System 使用 IJobEntity；22 文件使用 ToEntityArray；24 文件直接 EM 结构变化；15+ ECB PlaybackAndReset 反模式。

**AutoChessDemo**：2026-05-26 已破坏性删除 24 个需大重构的文件，从 34 文件/~19000 行缩减至 11 文件/~8400 行。后续按 Spec 10/10B 从 Config 链逐层重构。

## 边界

1. 只写已对照当前代码/验证成立的事实
2. 不写目标态设想 → 见 [01-目标态架构共识](../01-目标态架构共识/README.md)
3. 不写任务状态 → 见 [02-主线任务树](../02-主线任务树/README.md)
4. 流程规范 → 见 [规范手册](../规范手册.md)
