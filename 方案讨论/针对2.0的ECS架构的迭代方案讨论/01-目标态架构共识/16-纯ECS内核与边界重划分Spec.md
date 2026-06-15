# 纯 ECS 内核与边界重划分 Spec

> Owner：`01-目标态架构共识` | 状态：目标态总览入口 | 最近更新：2026-06-08

本文件只保留纯 ECS 内核与边界重划分目标态的总览、阅读路径和跨文档索引。详细目标态正文已拆到 [16 纯 ECS 内核与边界重划分 Spec 索引](16-纯ECS内核与边界重划分/README.md)。拆分前全文快照已归档到 [_归档/2026-06-07-16-纯ECS内核与边界重划分Spec拆分前.md](_归档/2026-06-07-16-纯ECS内核与边界重划分Spec拆分前.md)，只用于历史追溯。

## 目的

16 定义 EX-GAS 2.0 的核心目标态：Gameplay 权威状态和热路径计算必须落在纯 ECS Runtime Core；OOP 只作为 Application Shell 和 Runtime Boundary 的外壳；Debugger / 日志是证据系统；Luban + SourceGenerator 只生成不可变定义、静态 lookup、pure glue 和 validation artifact。

本文件不记录当前实现状态。当前事实写入 `../00-当前架构事实/`；可领取任务写入 `../02-主线任务树/`；短期验证与未跑项写入 `../04-当前进度状态/`。

## 范围摘要

1. Application Shell、Runtime Boundary、Pure ECS GAS Runtime Core、Definition & Generation 的目标分层。
2. Boundary command record、Core command resolve、Effect fan-in、Debugger evidence、SourceGenerator pure glue 的目标代码骨架。
3. Shell / Adapter capability 分级、opaque handle、snapshot read model、report projection identity 和 API health owner model。
4. 对 DOTS 官方规则的 API 选型约束、验收门槛和禁止方向。
5. 完整代码骨架阅读链：Shell capability public seam -> Boundary command record -> Core `IJobChunk` resolve -> `NativeStream` deterministic fan-in -> Debugger evidence projection -> SourceGenerator pure glue -> Snapshot / Identity / API Health validation。

## 子 Spec 阅读路径

| 顺序 | 子 Spec | 何时读 |
|---|---|---|
| 1 | [16-01 目标分层、官方依据与不变量](16-纯ECS内核与边界重划分/16-01-目标分层官方依据与不变量Spec.md) | 需要理解四层目标分层、官方规则映射和基础不变量 |
| 2 | [16-02 Boundary Command 与 Core Command Resolve](16-纯ECS内核与边界重划分/16-02-BoundaryCommand与CoreCommandResolveSpec.md) | 需要查看 Shell intent 如何进入 owner-local command record 与 Core `IJobChunk` resolve |
| 3 | [16-03 Fan-in、Debugger Evidence 与 SourceGenerator Pure Glue](16-纯ECS内核与边界重划分/16-03-FanInDebuggerSourceGeneratorSpec.md) | 需要查看 deterministic merge、Debugger evidence projection 和 generated pure glue |
| 4 | [16-04 Shell Capability Contract](16-纯ECS内核与边界重划分/16-04-ShellCapabilityContractSpec.md) | 需要审查 OOP Shell / Thin Adapter 的 capability 分级和 public seam |
| 5 | [16-05 Snapshot、Identity、API Health 与验收](16-纯ECS内核与边界重划分/16-05-SnapshotIdentityApiHealthSpec.md) | 需要审查 snapshot、identity、report projection、API health owner model、验收和禁止方向 |
| 6 | [16-06 端到端消息流代码骨架](16-纯ECS内核与边界重划分/16-06-端到端消息流代码骨架/README.md) | 需要按完整消息流审查 Shell intent、Boundary command、Core lane、fan-in、Definition pure glue 和 Diagnostics evidence |

## 官方依据入口

1. [GAS Runtime Core API 选型基线](../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md)
2. [官方文档覆盖与流程闭环](../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md)
3. [规则编号索引](../../UnityDOTS官方文档参考/主题/90-规则编号索引.md)
4. [DOTS 官方规范复核与性能红线](18-DOTS官方规范复核与性能红线Spec.md)
5. [SourceGenerator 职责边界](15-SourceGenerator职责边界Spec.md)
6. [Runtime Core 管线](03-RuntimeCore管线Spec.md)
7. [Runtime Core Debugger](07-RuntimeCoreDebuggerSpec.md)

## 禁止写入

1. 当前代码事实、文件行号、命中数量、generated report 当前数字。
2. 本轮执行流水、下一步任务、完成证明、迁移进度。
3. 旧 OOP Runtime 主链、旧 singleton stream 或旧 global facade 的完成态叙述。
4. 未经 DOTS API 选型表审查的目标代码骨架新增项。

## 验收门槛

1. 任一 Shell / Boundary / Core / Debugger / SourceGenerator 设计讨论能从本总览在两跳内找到对应目标态 Spec、官方依据和禁止方向。
2. 子 Spec 只保留目标态正文，不复制 `00` 当前事实或 `02` 任务计划。
3. Shell / Adapter capability、snapshot read model、identity projection 和 API health owner model 必须能解释 raw ECS identity 如何被限制在 Boundary implementation 或 Debugger gather 阶段。
4. Runtime Core hot path、Effect fan-in、structural commit、Debugger evidence 和 generated pure glue 的目标代码骨架必须能对照 DOTS API 选型基线说明 API 选择、proof-only 风险和重新选型触发条件。
