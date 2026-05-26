# BAKE-02: Baker 必须无状态 — 单例实例、Bake() 多次调用且无序

**严重度**: P0
**Primary Owner**: Baking-BlobAsset
**来源**: `baking-baker-overview.md`

## 规则声明
Baker 只实例化一次，`Bake()` 被多次调用且顺序不确定；禁止在 Baker 实例字段中缓存数据。所有数据访问必须通过 Baker 方法，自动记录依赖和产出以支持增量烘焙的 undo/redo。

## 为什么
Baker 是单例实例（每个 Baker 类型一个实例），其 `Bake()` 方法在非确定性顺序下被多次调用（增量烘焙跨长时间运行）。在 Baker 字段中缓存任何值违反不变式，导致烘焙行为异常。

## EX-GAS 诊断
GE／Ability definition Baker 的 `Bake()` 方法必须无状态，定义数据通过 `BlobBuilder` 或 component 产出。static 字段同样禁止，会跨 Baking 会话残留。

## 检查方法
审查所有 Baker 类，确保无实例字段（除 `readonly` 常量外）。static 字段同样禁止，会跨 Baking 会话残留。
