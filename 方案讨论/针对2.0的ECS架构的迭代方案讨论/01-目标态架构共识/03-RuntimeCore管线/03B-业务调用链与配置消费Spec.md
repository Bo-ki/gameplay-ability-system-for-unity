# 业务调用链与配置消费总纲

业务意图只通过 Boundary inbox 进入单 Kernel；Runtime 只消费 Session 内不可变 Catalog 和 generated pure glue。完整正文见 [03B 子目录](03B-业务调用链与配置消费/README.md)。

```text
Luban rows -> SourceGenerator/Bake -> Catalog Blob + contracts + validation
Shell intent -> CommandPort/SessionIngressGate -> BoundaryIngressJournal -> pre-Fixed GasCommandIngressSystem -> ECS BoundaryCommandInbox -> GasTickKernel
GasTickKernel -> catalog index/pure evaluator -> target-owned transaction
final facts -> cleanup outbox -> single Drain -> immutable consumers
```

禁止 Runtime 反查 Excel/JSON/managed row，禁止 generated system/lifecycle，禁止 Ability/GE definition 或 runtime instance 以 Entity backend 分叉。
