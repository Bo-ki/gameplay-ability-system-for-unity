# PRF-24: 禁止逐 Component 构建 Entity Archetype（P2 补充）

**严重度**: P2
**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: `optimize-structural-changes.md`

## 规则声明
同 PRF-14 原则。即使当前规模下逐 component 添加不明显影响性能，也应遵循一次性 Archetype 构建的习惯以避免 scale 时恶化。批量添加/移除多个 component 使用 `ComponentTypeSet` 一次完成。

## 为什么
在 scale 时（10K+ entity），逐 component 添加的代价从可忽略变为 P1 严重级。预先使用正确模式避免技术债务积累。

## EX-GAS 诊断
所有 entity 创建路径审计，确保使用 `CreateArchetype` + `CreateEntity(archetype, count)` 模式。AddComponent 操作优先使用 `ComponentTypeSet` 批量接口。

## 检查方法
Code review 检查新 entity 创建路径。对有 `foreach` 中 `CreateEntity` + 逐个 `AddComponent` 的遗留代码标记迁移。
