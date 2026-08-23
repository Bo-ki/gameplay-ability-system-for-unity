# 16 系列导航

本系列解释 v1 如何把“纯 ECS Core + 深 Boundary interface”落成唯一 owner，不重复 GAS 领域细节。

| 文档 | 唯一职责 |
|---|---|
| [16-01](16-01-目标分层官方依据与不变量Spec.md) | 官方依据、分层判据、owner map、消息协议 |
| [16-02](16-02-BoundaryCommand与CoreCommandResolveSpec.md) | 入站 CommandPort 与 Kernel 接管 |
| [16-03](16-03-FanInDebuggerSourceGeneratorSpec.md) | Fan-In、Diagnostics、Generated Glue 的权限分离 |
| [16-04](16-04-ShellCapabilityContractSpec.md) | Session/Shell capability |
| [16-05](16-05-SnapshotIdentityApiHealthSpec.md) | Snapshot、identity、API health |
| [16-06](16-06-端到端消息流代码骨架Spec.md) | 最小端到端代码骨架导航 |
| [16-07](16-07-ISSUE驱动架构瘦身目标裁决Spec.md) | 旧问题的最终保留/删除裁决 |

所有正文共同禁止：raw Entity public API、多个 Runtime authority、五物理阶段复活、跨 System scratch、消费者直读 Core、SourceGenerator 生成 lifecycle。
