# QRY-03: Optional 分支用 chunk 级判断

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — QRY-03 节 + IJobChunk 节

## 规则声明
当一组 entity 中部分含有可选 component 时，使用 `IJobChunk` + `chunk.Has(ref TypeHandle)` 在同一 system 内判断。禁止为每种 component 组合创建一个独立 system/query。

## 为什么
为每种组合创建 system 导致 system 数量爆炸（SYS-03）。Chunk 级判断是 O(1) 操作，entity 级判断才需要逐 entity 检查。如果某个 component 出现在 50%+ chunk 中，用 `WithAll` + `IEnableableComponent` 更优。

## EX-GAS 诊断
Effect application 路径中可能存在为不同 effect type 创建不同 system 的模式，应合并为单个 IJobChunk 使用 optional component 判断。

## 检查方法
- 搜索 Runtime Core 中是否存在 `[WithAll(typeof(A))]` 和 `[WithAll(typeof(A), typeof(B))]` 两个独立 system 处理同一类逻辑
- 评估合并可能性
