# PRF-11：以 chunk 证据控制 Prefab；静态定义优先 BlobAsset

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6；EX-GAS 当前 Definition 架构
**严重度**：P1
**Primary Owner**：Prefab-Content管理
**来源**：CONTENT-01；`performance-chunk-allocations.md`

## 规则声明

Prefab 审计依据是 prefab 相关 archetype/chunk 数量和 unused memory，不设置脱离 payload、shared component 与加载集合的固定数量阈值。静态不可变定义默认使用 BlobAsset/generated table。

## 为什么

相同数量的 prefab 可能共享 archetype，也可能因组件集合或 shared-component value 不同而分裂成多个 chunk；仅按 prefab 数量告警会产生误判。

## 检查方法

使用 Archetypes window 和 Profiler 输出实际 allocated/unused memory；在压测场景记录同时加载的 prefab 集合，而不是使用“超过 100”一类无基准阈值。
