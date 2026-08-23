# ECB-04: 区分 ECB System 管理与手工 ECB 生命周期

**严重度**: P1
**Primary Owner**: 结构变化-ECB
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `allocators-entity-command-buffer.md`、`systems-entity-command-buffer-automatic-playback.md`、`systems-entity-command-buffer-playback.md`

## 规则声明

通过 `EntityCommandBufferSystem.CreateCommandBuffer()` 创建的 ECB 使用该系统的 rewindable allocator，并在 ECB System update 中自动 playback、dispose，随后释放录制内存；调用方不得手工 playback/dispose。手工 `new EntityCommandBuffer(allocator, policy)` 的实例由调用方管理，可按 `PlaybackPolicy.MultiPlayback` 多次播放，并在最后手工 `Dispose()`。

## 为什么

ECB 返回的 deferred entity 只是该 ECB 命令流内的 placeholder，不能传给 `EntityManager` 或其它 ECB。ECB System 管理的实例不能跨其 playback 生命周期缓存；手工 ECB 是否跨帧取决于 allocator、所有权和明确的 dispose 计划，不能一概称为单帧工具。

## EX-GAS 诊断

Debugger 应监控 ECB 生命周期，确保没有任何 structure 在 playback 后被持有。每帧的 ECB 在帧末释放，新帧重新创建。

## 检查方法

搜索 ECB 类型的字段或跨帧缓存。ECB 必须在 `OnUpdate` 中通过 `CreateCommandBuffer` 获取，不能存储在 system 字段中跨帧复用。
