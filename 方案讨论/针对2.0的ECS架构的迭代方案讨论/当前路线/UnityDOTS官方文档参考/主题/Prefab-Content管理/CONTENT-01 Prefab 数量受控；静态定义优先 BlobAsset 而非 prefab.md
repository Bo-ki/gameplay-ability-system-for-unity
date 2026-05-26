# CONTENT-01: Prefab 数量受控；静态定义优先 BlobAsset 而非 prefab

**严重度**: P1
**Primary Owner**: Prefab-Content管理
**来源**: `performance-chunk-allocations.html` (P1-07)

## 规则声明
每个 prefab 占用至少 16 KiB 的独立 chunk。大量不同的 prefab 导致显著内存浪费。静态定义数据应用 BlobAsset 或 generated static table。

## 为什么
Prefab component 使每个 prefab 成为独立 archetype。N 个不同 prefab = N 个独立 archetype = N × 16 KiB chunk 至少。对于配置定义类数据（技能、效果、Buff），使用 Entity Prefab 承载是严重的内存浪费 —— 一个仅有 `IComponentData` 配置数据的 prefab 浪费 16 KiB chunk 而实际 payload 可能仅数百字节。BlobAsset 允许多 entity 共享同一数据块，无 chunk 开销。

## EX-GAS 诊断
GE 定义、Ability 定义、Tag 配置不应是 prefab，而应是 BlobAsset 或 static data table。仅需要实体原型（如角色模型、特效 entity）才使用 prefab。Debugger 应输出 prefab archetype 数量和总 chunk 内存。

## 检查方法
审查所有使用 `EntityPrefabReference` 的场景。区分"实体原型"（允许 prefab）与"配置定义"（必须用 BlobAsset）。在 Debugger 中观察 `prefabArchetypeCount`。
