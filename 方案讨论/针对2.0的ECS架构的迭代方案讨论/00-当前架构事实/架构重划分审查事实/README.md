# 架构重划分审查事实子页

> Owner：`00-当前架构事实` | 状态：当前事实子页索引 | 拆分时间：2026-06-08

本目录承载 `架构重划分审查事实.md` 拆分后的事实正文。所有子页仍属于 `00` 当前架构事实 owner，只能记录当前代码事实、证据、DOTS 判定和风险。

## 阅读顺序

| 顺序 | 文件 | 阅读目的 |
|---|---|---|
| 1 | [00-总览与代码证据事实](00-总览与代码证据事实.md) | 先读当前结论、模块职责事实和代码证据矩阵 |
| 2 | [01-DOTSAPI健康Owner分类事实](01-DOTSAPI健康Owner分类事实.md) | 复核 API 健康 owner 分类和静态复核边界 |
| 3 | [02-R1-R6代码证据截面](02-R1-R6代码证据截面.md) | 给 R1-R6 任务领取前做代码证据输入 |
| 4 | [03-当前重划分与目标态差距事实](03-当前重划分与目标态差距事实.md) | 判断当前 Shell / Adapter / Core / Debugger / SourceGenerator 与目标态差距 |
| 5 | [04-整体审查与Owner重划分事实](04-整体审查与Owner重划分事实.md) | 读取最新整体审查、Owner 错位和后续事实验证需求 |
| 6 | [05-BoundarySnapshot与MagnitudeSourceOwner事实](05-BoundarySnapshot与MagnitudeSourceOwner事实.md) | 读取 Boundary snapshot、magnitude source owner 与验证补录 |
| 7 | [06-ActiveEffectSlotMagnitudeSnapshot事实](06-ActiveEffectSlotMagnitudeSnapshot事实.md) | 读取 active effect slot tick 的 SourceAttribute snapshot lane 事实 |
| 8 | [07-端到端重划分事实](07-端到端重划分事实.md) | 读取 Shell -> Boundary -> Core -> Debugger -> Definition 端到端重划分事实 |
| 9 | [08-TagRequirementQueryDefinitionGlue事实](08-TagRequirementQueryDefinitionGlue事实.md) | 读取 Ability / GE tag requirement、RemoveGameplayEffect query、generated catalog / pure evaluator 和 Run5 证据边界 |
| 10 | [09-文档矛盾与过时口径事实](09-文档矛盾与过时口径事实.md) | 读取活动文档中的矛盾、过时架构描述、相似主题合并裁决和防回流事实 |
| 11 | [10-整体架构重审事实](10-整体架构重审事实.md) | 读取本轮整体架构事实卡、保留面 / 退出面、生成报告当前数字和后续事实验证需求 |

整体架构续轮审查默认先消费 `10-整体架构重审事实` 的最新事实卡，再回读 `00-总览与代码证据事实` 的保留面 / 退出面和 `04-整体审查与Owner重划分事实` 的 owner map 现实错位。需要按完整业务消息流审查 Shell、Boundary、Core、Debugger 和 Definition 的职责泄露时，再读 `07`。`05` / `06` 只在任务触达 Boundary snapshot、magnitude source 或 active effect slot tick 时作为专题事实输入。`08` 是 TagRequirement query 当前事实唯一正文，其他事实页只保留摘要入口。发现文档职责混写、相似主题重复正文、旧架构口径或短窗口污染时，先读 `09` 再决定拆分、合并、降权、归档或迁出 owner。

## 写入规则

1. 新增当前代码事实优先写入对应主题子页；只有跨主题摘要才回写根索引。
2. 发现目标态设计缺口时，只在本目录记录事实和判定，目标设计正文写回 `../../01-目标态架构共识/`。
3. 发现可执行任务时，任务正文写回 `../../02-主线任务树/`，本目录只保留事实约束。
4. 子页超过约 300 行或开始混入第二个事实主题时，继续按同 owner 拆分或合并到已有唯一 owner。
5. 文档矛盾和过时口径优先登记到 `09`；只有证据主题已经稳定且需要长事实正文时，才迁入具体专题事实页。
