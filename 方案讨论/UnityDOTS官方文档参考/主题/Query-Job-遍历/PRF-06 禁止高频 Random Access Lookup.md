# PRF-06: 禁止高频 Random Access Lookup

**严重度**: P0
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-06 节；`components-enableable-use.html`

## 规则声明
高频路径（每帧执行、遍历 entity 数 > 100）中使用 `ComponentLookup.TryGetComponent` 或 `BufferLookup.TryGetBuffer` 做跨 entity 随机访问，其成本在规模下显著，必须禁止。重构为 owner-local data 或顺序遍历。

## 为什么
每次 lookup 是哈希查找 + 内存随机访问。在 IJobEntity 的紧密循环中，一个 random lookup 的 cache miss 成本 ≈ 10-20 个顺序 entity 处理成本。百万 entity 遍历中 10% 做 random lookup ≈ 10 万次 cache miss。官方文档明确指出："Random-access methods have some additional overhead because they need to look up the target entity's data. When performance is a priority, use the iteration-based methods where possible."

## EX-GAS 诊断
Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source ASC 属性、ActiveEffect MMC 计算中 lookup source/target 属性。应优先使用 owner-local buffer。本规则为 P0 — 高频 Lookup 违规必须修复后方可合并。

## 检查方法
- Grep `TryGetComponent` 和 `TryGetBuffer` 在 IJobEntity/IJobChunk 中的使用
- 评估 lookup 频率和 entity 规模
- 标记高频路径中的 Random Access Lookup 为 P0 违规
