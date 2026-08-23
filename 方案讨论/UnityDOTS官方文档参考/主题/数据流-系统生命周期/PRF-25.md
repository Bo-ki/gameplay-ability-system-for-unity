# PRF-25: 每个并行 Job 使用独立 ECB

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `systems-entity-command-buffers.md`

## 规则声明

默认让每个 distinct job 使用独立的 `EntityCommandBuffer`。同一 ECB 只有在后一个 job 明确依赖前一个 job 时才能再次被调度访问；即使依赖安全，若两个 job 的 sort key 域重叠（例如都使用 `[ChunkIndexInQuery]`），其命令仍可能按 sort key 交错，因此需要分 ECB 或明确接受该顺序语义。

## 为什么

若两个访问同一 ECB 的 job 没有依赖关系，Jobs Debugger 的 ECB safety handle 会报错；这不是静默安全。若 job 依次执行，安全检查可以通过，但重叠 sort key 会让命令相互穿插。命令仍按 ECB 的 sort key 规则排序，问题在于其顺序可能不符合“Job A 全部先于 Job B”的业务假设，而不是 ECB 随机回放。

## EX-GAS 诊断

GAS frame backbone 中，Attribute delta 与 GE application 若要求明确阶段边界，应分别从 ECB System 调用 `CreateCommandBuffer`，并由 system/job 依赖表达先后。若确实共享 ECB，必须证明依赖安全及交错 sort key 符合业务语义。

## 检查方法

审计 `ISystem.OnUpdate` 中 `CreateCommandBuffer` 调用次数与并行 job 数量的对应关系。
