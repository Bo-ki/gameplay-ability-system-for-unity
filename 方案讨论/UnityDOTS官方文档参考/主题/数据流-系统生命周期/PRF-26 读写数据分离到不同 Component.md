# PRF-26: 读写数据分离到不同 Component

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-data-granularity.md`

## 规则声明

遍历组件数据时若声明 read/write 访问（即使未实际修改），Unity 将整个 chunk 标记为已变更，触发所有监听该 component type 的响应式系统（使用 change filter 的系统）。应将只读字段和读写字段分离到不同 component 中。

## 为什么

静默性能退化——不需要运行的系统被触发，浪费 CPU。IJobEntity 的 `ref` 参数默认为 write access，即使只读取也标记 chunk 变更。CASE-38（`IJobEntityChunkBeginEnd`）可部分缓解，但分离 component 是根本方案。

## EX-GAS 诊断

GAS 中 `CAttribute` 可能有 field `CurrentValue`（每帧变化）+ `BaseValue`（极少变化）→ 应拆分为 `CAttributeCurrent` + `CAttributeConfig`。`CUnitState` 的 `Health` / `MaxHealth` / `Level` 同理。

## 检查方法

审计 IComponentData 中哪些字段运行时被修改、哪些不变。不变字段抽到独立 component；确保 IJobEntity 用 `in` 参数声明只读访问。
