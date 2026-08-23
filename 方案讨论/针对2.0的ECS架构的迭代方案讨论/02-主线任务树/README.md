# 02 主线任务树

> Owner：`02-主线任务树` | 最近更新：2026-08-24 | 当前默认路线：Runtime v1 不可兼容迁移

本目录只回答“领取什么、修改什么、如何验收”。当前事实归 `../00-当前架构事实/`，目标规范归 `../01-目标态架构共识/`，本轮交接与验证归 `../04-当前进度状态/`。

## 当前默认入口

[Runtime v1 不可兼容迁移](RuntimeV1不可兼容迁移/README.md) 是唯一可领取的 GAS Runtime 重构路线。

事实与规范前置：

- [Runtime v1 不可兼容迁移基线事实](../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [GAS 业务链路破坏性重划分 Spec](../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)

## V0-V7 路线

| 顺序 | 任务 | 状态 | 退出结果 |
|---|---|---|---|
| V0 | [语义冻结与真实测试基线](RuntimeV1不可兼容迁移/V0-语义冻结与真实测试基线.md) | 优先就绪 | 当前行为矩阵、真实 tests、semantic golden |
| V1 | [Definition Catalog 与稳定身份](RuntimeV1不可兼容迁移/V1-DefinitionCatalog与稳定身份.md) | V0 后 | phase schema、Owner/Avatar、stable handle |
| V2 | [ASC 稳定 Slab 与 Handle](RuntimeV1不可兼容迁移/V2-ASC稳定Slab与Handle.md) | V1 后 | Ability/Activation/Continuation/Effect 单一 ASC authority |
| V3 | [单 TickKernel 与 Ability Commit](RuntimeV1不可兼容迁移/V3-单TickKernel与AbilityCommit.md) | V2 后 | 单 writer、标准 EndFixed、Activate/Commit/Cancel |
| V4 | [Effect / Attribute / Tag 语义闭合](RuntimeV1不可兼容迁移/V4-EffectAttributeTag语义闭合.md) | V3 后 | capture/inhibition/stack/period/aggregator/tag count |
| V5 | [Boundary Drain / Cue / 销毁交接](RuntimeV1不可兼容迁移/V5-BoundaryDrainCue与销毁交接.md) | V3/V4 后 | 单 Drain、immutable ring、Cue 四阶段、两阶段 destroy |
| V6 | [AutoChess 迁移与旧链删除](RuntimeV1不可兼容迁移/V6-AutoChess迁移与旧链删除.md) | V3-V5 后 | runner/evaluator/report迁移、删除门 |
| V7 | [确定性、规模与 Profiler 门](RuntimeV1不可兼容迁移/V7-确定性规模与Profiler门.md) | V6 后 | release evidence |

## 路线约束

1. V1-V4 可在隔离集成分支内分步开发，但旧/新 Runtime 不得同时注册或双写权威状态。
2. V3/V4 删除门通过前不得把不完整新 Runtime 合入稳定分支。
3. 不提供 compatibility mode、Definition backend flag 或旧数据 fallback。
4. 每个任务只引用 `00/01` owner 文档，不在任务文件复写设计规范。
5. 当前代码变化反哺 `00`，规范变化反哺 `01/17`，任务状态与退出条件留在本目录，短期结果写 `04`。
6. 没有验证证据不得标记完成；“编译通过”“x50 跑通”“字段存在”不能替代对应语义/Profiler/删除门。

## 旧路线处置

- [R0-R8](R0-R8/README.md) 已被 Runtime v1 路线取代，仅保留历史证据、旧脚本和风险来源。
- `T0/T1/T2/T3/T4/T5/T6`、旧 `AM*`、`Chain-*`、`Export-*` 继续作为历史输入，不可直接领取。
- [GAS 架构重划分主线任务](GAS架构重划分主线任务.md) 仅作当前 v1 路由别名。

## 通用交还包

每个 V 任务至少交还：

1. 触达路径与明确未触达范围。
2. 删除/新增 authority 清单，证明没有双写或 fallback。
3. 对应语义、静态门、编译域和运行验证结果。
4. 未跑项、失败项与下一任务入口。
5. `00/01/02/04` 反哺清单。
