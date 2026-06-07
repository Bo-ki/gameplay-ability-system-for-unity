# 10B AutoChess 完整业务案例 Spec 索引

> Owner：`01-目标态架构共识/10B-AutoChess完整业务案例` | 状态：目标态 Spec 子目录 | 最近拆分：2026-06-07

本目录从 `../10B-AutoChess完整业务案例设计Spec.md` 拆出 AutoChess 完整业务案例目标态正文。根 `10B-AutoChess完整业务案例设计Spec.md` 只保留总览、阅读路径和跨文档索引；本目录内文件才是各主题的目标态正文 owner。

## 纯度规则

1. 本目录只回答 AutoChess 完整业务案例目标态应该如何设计、为什么这样设计、如何验收。
2. 当前代码事实、文件行号、generated report 数字、`MigrationProofOnly` 实现证据、执行流水和下一步任务不得写入本目录正文。
3. 需要引用当前事实时，只能链接到 `../../00-当前架构事实/`；需要拆任务时，只能链接到 `../../02-主线任务树/`。
4. 新增 DOTS API 依据时，先进入 `../../../UnityDOTS官方文档参考/`，再反哺本目录的目标态约束。

## 子 Spec 索引

| 文件 | 职责 |
|---|---|
| [10B-01 场景、单位、属性与 Tag](10B-01-场景单位属性TagSpec.md) | AutoChess 验收场景、棋子、属性、Tag 和 generated config shape。 |
| [10B-02 技能与 GameplayEffect](10B-02-技能与GESpec.md) | Ability、GE、BlobAsset、MMC static evaluator 和配置到 Runtime 的目标链路。 |
| [10B-03 羁绊与 Runtime 基础设施](10B-03-羁绊与Runtime基础设施Spec.md) | Synergy detection、Runtime Core 基础设施 component / buffer / owner shape。 |
| [10B-04 Command 与 Effect Fan-In 核心 System](10B-04-核心System-Command与FanInSpec.md) | 普攻、技能激活、Effect fan-in lane 的目标代码样例和数据流。 |
| [10B-05 Attribute / ActiveEffect / Death 核心 System](10B-05-核心System-AttributeActiveEffectDeathSpec.md) | Attribute reduce/apply、ActiveEffect lifecycle、死亡检测 lane 的目标代码样例。 |
| [10B-06 业务流程走查](10B-06-业务流程走查Spec.md) | 盾击、冰霜新星、毒刃三条完整业务走查。 |
| [10B-07 执行链、矩阵、生成物与不变量](10B-07-执行链矩阵生成物不变量Spec.md) | System 执行链、单位机制矩阵、SourceGenerator 生成物清单、DOTS 规则映射和历史方案定位。 |

## 反向入口

- 10B 总览：[../10B-AutoChess完整业务案例设计Spec.md](../10B-AutoChess完整业务案例设计Spec.md)
- 01 总入口：[../README.md](../README.md)
- AutoChess 无头验收：[../10-AutoChess无头验收Spec.md](../10-AutoChess无头验收Spec.md)
- AutoChess Luban 配置方案：[../11-AutoChessDemo-Luban配置方案Spec.md](../11-AutoChessDemo-Luban配置方案Spec.md)
