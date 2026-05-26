# BAKE-01: Baker 只添加不读取 — Baker 间无依赖

**严重度**: P0
**Primary Owner**: Baking-BlobAsset
**来源**: `baking-phases.md`

## 规则声明
Baker 内 `GetComponent<T>()`／`SetComponent<T>()` 不可用；只能 `AddComponent<T>()`。Baker 之间无依赖关系，Baker 只能向当前 entity 添加新 component，不能读取已有 component 的值或访问／修改其他 entity。

## 为什么
若在 Baker 中访问其他 entity 会导致未定义行为。Baker 之间的无依赖设计保证了 Baking 流程的并行安全性和增量 Baking 的正确性。

## EX-GAS 诊断
Ability／GE／Buff definition 的 Baker 只负责烘焙期数据搬运；GE 的 modifier 计算逻辑放在 Baking System。所有 Baker 必须审查，确认无 `GetComponent`／`SetComponent` 调用。

## 检查方法
审查所有 Baker 的 `Bake()` 方法，确认无 `GetComponent`／`SetComponent` 调用。需读取已有数据做复杂计算时，使用 Baking System（参考 BAKE-03 + CASE-32 TemporaryBakingType）。
