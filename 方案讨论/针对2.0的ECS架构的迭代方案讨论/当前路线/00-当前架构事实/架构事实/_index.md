# 架构事实

本子目录维护当前版本已成立的架构事实文档。每个文件回答"当前版本在这一方面已经证明了什么"。

## 文件索引

| 文件 | 覆盖范围 | 关键事实数 |
|------|----------|-----------|
| [Runtime主链事实](Runtime主链事实.md) | Runtime、Ability、GE、Attribute、Observation 事实 | 7 |
| [Definition配置事实](Definition配置事实.md) | Definition、Luban、generated、bake/integration 事实 | 5 |
| [当前架构图](当前架构图.md) | 当前实际链路 mermaid 图、问题边界、目标迁移方向 | 4 张图 |
| [模块索引](模块索引.md) | Runtime/Editor/Config 目录索引 | 3 张表 |

## 事实摘要

1. Runtime 外部接入以 `AbilitySystemBinding + AbilitySystemFacade` 为主；写操作通过 request entity 进入 ECS。
2. AM2 已落地 `EffectCommandSpecStream` 数据契约；AM3 simple instant 局部 proof；AM5 owner-local `ActiveEffectStore` mirror。
3. `GASDefinitionTable` 已统一 Ability/GE/Attribute/Tag/Cue summary contract；generated → bake → runtime integration contract 链已形成。
4. 当前 6 个旧 SystemGroup 与目标态 8 个新 SystemGroup 不匹配。
5. 旧 GE lifecycle pipeline（request → runtime GE entity → lifecycle → eventbus）仍是待迁移对象。

## 维护规则

1. 只写已经对照当前代码、验证记录或当前 profile 成立的事实。
2. 不写目标态设想（目标态进入 `../../01-目标态架构共识/`）。
3. 不写任务状态（任务状态进入 `../../02-主线任务树/`）。
4. 事实证据更新时同步更新本文档摘要。
