# 架构事实

> 上次更新：2026-05-26 | 基于全量代码审查

本子目录维护当前版本已成立的架构事实文档。每个文件回答"当前版本在这一方面已经证明了什么"。

## 文件索引

| 文件 | 覆盖范围 | 关键事实数 |
|------|----------|-----------|
| [Runtime主链事实](Runtime主链事实.md) | Runtime、Ability、GE、Attribute、Observation、System/Job 状态 | 24 条事实 + DOTS 合规风险分析 |
| [Definition配置事实](Definition配置事实.md) | Definition、Luban、generated、bake/integration 事实 + Spec 对照 | 9 条事实 + 15 项 Spec 对照 + 9 项合规缺陷 |
| [当前架构图](当前架构图.md) | 当前实际链路 mermaid 图、DOTS 规范对照热图、问题边界、目标迁移方向 | 5 张图 |
| [AutoChessDemo事实](AutoChessDemo事实.md) | AutoChessDemo 34 文件全量审查、四层分析、Spec 10/10B 对照、DOTS 合规缺陷 | 21 条事实 + 12 项专属缺陷 + 16 项 Spec 对照 |
| [模块索引](模块索引.md) | Runtime/Editor/Config 目录索引 + Luban 配置链文件对照 | 5 张表 |

## 事实摘要

1. Runtime 外部接入以 `AbilitySystemBinding + AbilitySystemFacade` 为主；写操作通过 request entity 进入 ECS。
2. 39 个 ISystem 已全面取代 SystemBase；`GASManager.cs`(130行) 是纯 ECS 启动器。
3. `GASSystemScheduleContract`(447行) 完整定义 8-phase 管线契约；`GASGroups.cs`(101行) 定义 9 个 SystemGroup。
4. AM2 已落地 `CEffectCommandSpecStream`（全局 singleton + 6 DynamicBuffer）；AM3 simple instant proof；AM5 `CActiveEffectStore` mirror。
5. `SEffectCommandSpecStreamPhases`(619行) 使用 cursor 驱动 for 循环——**新代码正面范例**。
6. Typed Simulation Fact 桥接模式已实现模拟-表现分离。
7. **核心缺口**: 仅 2/39 System 使用 IJobEntity (5%)；22 文件使用 ToEntityArray；24 文件直接 EM 结构变化；15+ ECB PlaybackAndReset 反模式。
8. 8-phase 契约已定义但 System 仍按旧 Group 名称注册，未真实搬迁。

## 维护规则

1. 只写已经对照当前代码、验证记录或当前 profile 成立的事实。
2. 不写目标态设想（目标态进入 `../../01-目标态架构共识/`）。
3. 不写任务状态（任务状态进入 `../../02-主线任务树/`）。
4. 事实证据更新时同步更新本文档摘要。
