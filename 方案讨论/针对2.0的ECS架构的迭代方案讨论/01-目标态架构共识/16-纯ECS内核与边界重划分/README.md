# 16 纯 ECS 内核与边界重划分 Spec 索引

> Owner：`01-目标态架构共识/16-纯ECS内核与边界重划分` | 状态：目标态 Spec 子目录 | 最近更新：2026-06-08

本目录从 `../16-纯ECS内核与边界重划分Spec.md` 拆出纯 ECS Runtime Core、OOP Shell / Thin Adapter、Debugger evidence 和 SourceGenerator pure glue 的目标态正文。根 `16-纯ECS内核与边界重划分Spec.md` 只保留总览、阅读路径和官方依据入口。

## 纯度规则

1. 本目录只回答纯 ECS 内核与边界重划分的目标态应该如何设计、为什么这样设计、如何验收。
2. 当前代码事实、文件行号、generated report 数字、执行流水和下一步任务不得写入本目录正文。
3. 需要引用当前事实时，只能链接到 `../../00-当前架构事实/`；需要拆任务时，只能链接到 `../../02-主线任务树/`。
4. 新增 DOTS API 依据时，先进入 `../../../UnityDOTS官方文档参考/`，再反哺本目录的目标态约束。

## 子 Spec 索引

| 文件 | 职责 |
|---|---|
| [16-01 目标分层、官方依据与不变量](16-01-目标分层官方依据与不变量Spec.md) | 短索引；正文拆入 [16-01 子目录](16-01-目标分层官方依据与不变量/README.md)，分别承载目标分层、Owner Map、消息流协议和最小完整代码骨架判定门。 |
| [16-02 Boundary Command 与 Core Command Resolve](16-02-BoundaryCommand与CoreCommandResolveSpec.md) | Boundary command record 和 Core `IJobChunk` command resolve envelope 目标代码骨架；完整 Ability command / Target Resolve 规则回到 `03D`。 |
| [16-03 Fan-in、Debugger Evidence 与 SourceGenerator Pure Glue](16-03-FanInDebuggerSourceGeneratorSpec.md) | `NativeStream` deterministic merge、Debugger evidence projection 和 generated pure glue 目标代码骨架。 |
| [16-04 Shell Capability Contract](16-04-ShellCapabilityContractSpec.md) | Shell / Adapter capability 分级、public seam、opaque handle 和 internal resolver contract。 |
| [16-05 Snapshot、Identity、API Health 与验收](16-05-SnapshotIdentityApiHealthSpec.md) | Snapshot capture、identity exposure、report projection、API health owner model、验收与禁止方向。 |
| [16-06 子页索引](16-06-端到端消息流代码骨架/README.md) / [16-06A 完整正文](16-06-端到端消息流代码骨架/16-06A-完整端到端消息流代码骨架Spec.md) | Shell intent -> Boundary command -> Core lane -> `NativeStream` fan-in -> owner-local dispatch -> spec / delta / fact lane -> Definition pure glue -> Diagnostics evidence 的目标态代码骨架入口。 |

## 目标态代码入口

需要用完整端到端代码骨架审查新架构时，先读 [16-06 子页索引](16-06-端到端消息流代码骨架/README.md) 的阅读入口和 owner 裁决，再读 [16-06A 完整端到端消息流代码骨架](16-06-端到端消息流代码骨架/16-06A-完整端到端消息流代码骨架Spec.md)，确认 Shell intent、Boundary command、Core lane、owner-local dispatch、spec / delta / fact lane、Definition pure glue、Diagnostics evidence 和 Derived export 的单向消息流。需要拆解局部约束时，再读 `16-01` 子目录的 [16-01C 目标态消息流协议](16-01-目标分层官方依据与不变量/16-01C-目标态消息流协议Spec.md) 和 [16-01D 最小完整代码骨架判定门](16-01-目标分层官方依据与不变量/16-01D-最小完整代码骨架Spec.md)，然后按业务链路跳到 `16-02`、`16-03`、`16-04`、`16-05`。本目录的代码样例只定义目标态 Spec 和验收约束，不记录当前代码做到哪里，也不替代 `00-当前架构事实` 的证据判断。

## 反向入口

- 16 总览：[../16-纯ECS内核与边界重划分Spec.md](../16-纯ECS内核与边界重划分Spec.md)
- 01 总入口：[../README.md](../README.md)
- Runtime Core 管线：[../03-RuntimeCore管线Spec.md](../03-RuntimeCore管线Spec.md)
- Runtime Core Debugger：[../07-RuntimeCoreDebuggerSpec.md](../07-RuntimeCoreDebuggerSpec.md)
- SourceGenerator 职责边界：[../15-SourceGenerator职责边界Spec.md](../15-SourceGenerator职责边界Spec.md)
- DOTS 性能红线：[../18-DOTS官方规范复核与性能红线Spec.md](../18-DOTS官方规范复核与性能红线Spec.md)
