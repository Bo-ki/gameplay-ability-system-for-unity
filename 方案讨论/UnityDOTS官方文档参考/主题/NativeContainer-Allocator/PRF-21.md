# PRF-21：ExclusiveEntityTransaction 仅限 Secondary/Streaming World

**严重度**：P1
**Primary Owner**：NativeContainer-Allocator
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `concepts-safety.md`、`systems-entity-command-buffer-use.md`

## 规则声明
Runtime Core job 内结构变化默认通过 ECB 录制并在明确 phase playback。只有目标为 secondary/streaming World，且单个 worker 需要批量结构变化时，才评估 `ExclusiveEntityTransaction`；它不是通用 worker-thread `EntityManager`。

## 为什么
官方将其主要用途定位为 secondary/streaming World，并只提供该 transaction 暴露的 API 子集。常规 job 结构变化路径仍是 ECB。

## EX-GAS 诊断
Battle init/scene loading 若发生在独立 streaming World 可评估；每帧 ability/effect 结构变化不使用它。主线程可立即批量变化时，还应比较 EntityManager query/bulk API，而不是机械改用 ECB。

## 检查方法
搜索 `ExclusiveEntityTransaction`；要求给出目标 World、单 writer 模型、支持 API 和 teardown/依赖证明。
