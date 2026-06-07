# AutoChess 完整业务案例设计 Spec

> Owner：`01-目标态架构共识` | 状态：目标态总览入口 | 最近拆分：2026-06-07

本文件只保留 AutoChess 完整业务案例目标态的总览、阅读路径和跨文档索引。详细目标态正文已拆到 [10B AutoChess 完整业务案例 Spec 索引](10B-AutoChess完整业务案例/README.md)。拆分前全文快照已归档到 [_归档/2026-06-07-10B-AutoChess完整业务案例设计Spec拆分前.md](_归档/2026-06-07-10B-AutoChess完整业务案例设计Spec拆分前.md)，只用于历史追溯。

## 目的

10B 定义 AutoChess Demo 作为完整 GAS 业务样例时，目标态应该怎样用 Luban、SourceGenerator、Blob、纯 ECS Runtime Core、Boundary Projection 和 Debugger evidence 承载真实棋子、属性、技能、GE、羁绊、业务走查和验收矩阵。

本文件不记录当前实现状态。当前事实写入 `../00-当前架构事实/`；可领取任务写入 `../02-主线任务树/`；短期验证与未跑项写入 `../04-当前进度状态/`。

## 范围摘要

1. 具名 AutoChess 战斗场景、棋子、属性、Tag、技能、GameplayEffect 和羁绊。
2. 配置进入 Runtime Core 的目标链路：Luban row -> SourceGenerator -> Blob / static lookup / pure evaluator。
3. Command、Effect Fan-In、Attribute、ActiveEffect、Gameplay Fact 等 Runtime lane 的目标代码样例。
4. 盾击、冰霜新星、毒刃三条业务流程走查。
5. System 执行链、机制矩阵、生成物清单、目标态不变量和历史方案定位。

## 子 Spec 阅读路径

| 顺序 | 子 Spec | 何时读 |
|---|---|---|
| 1 | [10B-01 场景、单位、属性与 Tag](10B-AutoChess完整业务案例/10B-01-场景单位属性TagSpec.md) | 需要理解 AutoChess 验收战斗、棋子数据、属性和 Tag shape |
| 2 | [10B-02 技能与 GameplayEffect](10B-AutoChess完整业务案例/10B-02-技能与GESpec.md) | 需要理解 Ability / GE 配置、Blob 和 MMC static evaluator |
| 3 | [10B-03 羁绊与 Runtime 基础设施](10B-AutoChess完整业务案例/10B-03-羁绊与Runtime基础设施Spec.md) | 需要理解 Synergy 系统和 Runtime Core 基础设施 component / buffer |
| 4 | [10B-04 Command 与 Effect Fan-In 核心 System](10B-AutoChess完整业务案例/10B-04-核心System-Command与FanInSpec.md) | 需要查看普攻、技能激活和 Effect fan-in lane 目标实现 |
| 5 | [10B-05 Attribute / ActiveEffect / Death 核心 System](10B-AutoChess完整业务案例/10B-05-核心System-AttributeActiveEffectDeathSpec.md) | 需要查看 Attribute reduce/apply、ActiveEffect lifecycle 和死亡检测目标实现 |
| 6 | [10B-06 业务流程走查](10B-AutoChess完整业务案例/10B-06-业务流程走查Spec.md) | 需要阅读三条完整业务流程 |
| 7 | [10B-07 执行链、矩阵、生成物与不变量](10B-AutoChess完整业务案例/10B-07-执行链矩阵生成物不变量Spec.md) | 需要查看执行链总览、机制矩阵、生成物清单、规则映射和历史方案定位 |

## 官方依据入口

1. [GAS Runtime Core API 选型基线](../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md)
2. [官方文档覆盖与流程闭环](../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md)
3. [规则编号索引](../../UnityDOTS官方文档参考/主题/90-规则编号索引.md)
4. [AutoChess 无头验收 Spec](10-AutoChess无头验收Spec.md)
5. [AutoChessDemo Luban 配置方案 Spec](11-AutoChessDemo-Luban配置方案Spec.md)
6. [AutoChess 配置验收样例 Spec](21-AutoChessDemo策划配置验收样例Spec.md)

## 禁止写入

1. 当前代码事实、文件行号、命中数量、generated report 当前数字。
2. 本轮执行流水、下一步任务、完成证明、迁移进度。
3. 旧 Demo / 旧 OOP registry 的完成态叙述。
4. 未经 DOTS API 选型表审查的目标代码骨架新增项。

## 验收门槛

1. 任一 AutoChess 业务验收任务能从本总览在两跳内找到对应业务样例、配置链路、Runtime lane、官方规则和任务入口。
2. 子 Spec 只保留目标态正文，不复制 `00` 当前事实或 `02` 任务计划。
3. AutoChess 业务样例必须能解释 config -> generated catalog -> Runtime lane -> structured evidence -> validation assertion 的完整目标链路。
4. 完整业务案例设计必须能覆盖 Instant、Duration、Period、Stack、AoE、Synergy reaction、Attribute delta、ActiveEffect lifecycle 和 Gameplay Fact。