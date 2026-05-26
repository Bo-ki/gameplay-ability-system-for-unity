# QRY-04: 高频 random lookup 重构为 owner-local 或 chunk-local

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — ComponentLookup/BufferLookup 节 + PRF-06 节

## 规则声明
高频路径（每帧执行、遍历 entity 数 > 100）中使用 `ComponentLookup.TryGetComponent` 或 `BufferLookup.TryGetBuffer` 做跨 entity 随机访问，其成本在规模下显著，必须重构为 owner-local data 或 chunk-local buffer 以实现顺序遍历。

## 为什么
每次 lookup 是哈希查找 + 内存随机访问。在 IJobEntity 的紧密循环中，一个 random lookup 的 cache miss 成本 ≈ 10-20 个顺序 entity 处理成本。百万 entity 遍历中 10% 做 random lookup ≈ 10 万次 cache miss。Random-access 方法的额外开销是官方文档明确警告的性能陷阱。

## EX-GAS 诊断
Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source ASC 属性、ActiveEffect MMC 计算中 lookup source/target 属性。应优先将 target 属性写入 source 的 owner-local buffer，避免跨 entity 随机访问。

## 检查方法
- Grep `TryGetComponent` 和 `TryGetBuffer` 在 IJobEntity/IJobChunk 中的使用
- 评估 lookup 频率和 entity 规模
- 确认是否有 owner-local 替代方案可行
