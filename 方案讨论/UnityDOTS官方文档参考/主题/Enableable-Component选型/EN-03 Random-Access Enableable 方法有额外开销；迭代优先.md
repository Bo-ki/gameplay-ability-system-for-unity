# EN-03: Random-Access Enableable 方法有额外开销；迭代优先

**严重度**: P1
**Primary Owner**: Enableable-Component选型
**来源**: `components-enableable-use.html`; `主题/13-DOTS编写规范与性能陷阱.md` P1-02

## 规则声明
通过 `ComponentLookup.SetComponentEnabled(entity, value)` 随机访问修改 enableable 状态有额外的 entity 定位开销（哈希查找 + 随机内存访问）。高频路径应优先使用迭代式方法（`EnabledRefRW<T>` 或 `EnabledMask[index]`）。

## 为什么
`SetComponentEnabled` 通过 ComponentLookup 查找到目标 entity 的 chunk 和 index，涉及哈希表查找和可能的 cache miss。在 IJobEntity 的紧密循环中，一次 random lookup 的成本 ≈ 10-20 次顺序 entity 处理。

## EX-GAS 诊断
Effect 生命周期管理中逐 entity 调用 `SetComponentEnabled` 切换状态——应评估是否可改为 IJobChunk 批量操作或 IJobEntity 中 `EnabledRefRW<T>` 的顺序遍历。

## 检查方法
Grep 搜索 `SetComponentEnabled` 在 job 中的调用。如果在 IJobEntity.Execute 内部出现按 entity 参数的随机访问 → 评估是否可改为 `EnabledRefRW<T>` 参数声明。
