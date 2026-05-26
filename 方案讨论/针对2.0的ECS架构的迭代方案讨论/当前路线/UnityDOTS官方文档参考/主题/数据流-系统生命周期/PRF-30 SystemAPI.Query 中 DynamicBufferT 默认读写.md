# PRF-30: SystemAPI.Query 中 DynamicBuffer<T> 默认读写

**严重度**: P2
**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-systemapi-query.md`

## 规则声明

`SystemAPI.Query<DynamicBuffer<T>>` 中的 `DynamicBuffer<T>` 参数默认声明为 write access（等同于 `ref`）。在只读场景中（仅读取不修改 buffer 内容），这会导致不必要的 sync point 和 chunk 变更标记。只读场景需自定义 `EntityQuery` 并声明为 read-only，或分离为独立 component。

## 为什么

`DynamicBuffer<T>` 在 SystemAPI.Query 中被视为 mutable 类型，即使只使用 `in DynamicBuffer<T>` 参数声明。每个 type handle 建立时分配 read/write 权限。默认 write access 导致：1) 触发不必要的 sync point（等待所有对该 buffer 的写 job 完成）；2) 标记 chunk 为已变更，触发不相关的响应式系统。

## EX-GAS 诊断

使用 `SystemAPI.Query<DynamicBuffer<BDamageHistory>>` 只读遍历历史数据时 → 应自定义 EntityQuery + `BufferTypeHandle<T>` 声明为只读，或改用 IJobChunk 的 `GetBufferAccessorRO`。

## 检查方法

搜索 `SystemAPI.Query<DynamicBuffer<` 出现，判断 buffer 是否只读。若只读 → 建议改用 `EntityQuery` + `BufferAccessor<T>` 或 IJobChunk + `GetBufferAccessorRO`。
