# STORE-03: Store 选型按数据性质分类：gameplay / transient / telemetry / presentation

**严重度**: P1
**Primary Owner**: Store选型-数据承载策略
**来源**: EX-GAS 数据承载框架设计决议

## 规则声明
所有数据承载方式选型必须按照 gameplay / transient / telemetry / presentation 四类数据性质进行，不得混淆类别。

## 为什么
不同性质的数据对确定性、持久性、实时性、丢失容忍度的要求完全不同。混淆类别导致：
- Gameplay 数据用 transient 承载 -> 帧丢失导致状态不一致
- Presentation 数据用 gameplay 承载 -> 不必要的确定性开销拖慢 hot path
- Telemetry 数据混入 gameplay 流程 -> 污染 battle hash 或不必要地增加 gameplay 数据量

## EX-GAS 诊断
- Owner-local active effects：`DynamicBuffer<BActiveEffectSlot>`，固定容量（如 64 slot），gameplay 分类
- EffectCommand fan-in：`NativeStream`（并行），per-frame scratch，transient 分类
- AttributeDelta：per-target `DynamicBuffer<BAttributeDelta>`，per-frame clear，transient 分类
- TypedSimulationFact：`DynamicBuffer<BTypedFact>` on singleton，per-frame clear，transient 分类
- Presentation outbox：transient `NativeStream` 或 boundary managed queue，presentation 分类
- Debug telemetry：sampled `NativeList`（Persistent）+ periodic export，telemetry 分类
- Static definition：`BlobAssetReference` 或 generated static array，不分类（只读共享）

## 检查方法
每个数据承载方式选型决策必须记录数据性质分类和选型理由。
