# ECB-04: ECB Allocator 在 Playback 后 Rewind

**严重度**: P1
**Primary Owner**: 结构变化-ECB
**来源**: `systems-entity-command-buffer.md`

## 规则声明

ECB 内部使用 `RewindableAllocator`，每次 playback 后自动 rewind 释放内部缓冲区。不能在 playback 后继续持有或访问 ECB 分配的内存（包括通过 ECB 创建的 entity、buffer 引用等）。

## 为什么

Rewind 后 allocator 释放了所有内部段，继续访问已释放内存导致未定义行为或崩溃。ECB 本质是单帧工具，不应跨帧使用。

## EX-GAS 诊断

Debugger 应监控 ECB 生命周期，确保没有任何 structure 在 playback 后被持有。每帧的 ECB 在帧末释放，新帧重新创建。

## 检查方法

搜索 ECB 类型的字段或跨帧缓存。ECB 必须在 `OnUpdate` 中通过 `CreateCommandBuffer` 获取，不能存储在 system 字段中跨帧复用。
