# PRF-11: 控制 Prefab 数量

**严重度**: P1
**Primary Owner**: Prefab-Content管理（本主题为跨主题引用）
**来源**: `Query-Job-遍历.md`；后续 Phase 3 整理跨主题归属

## 规则声明
Prefab 数量直接影响 EntityQuery 遍历的 entity 基数。每个 Prefab 实例化后的 entity 都会进入相关 query 的匹配集合。Prefab 数量不受控会导致任意遍历路径的 entity 规模不可预测地增长。

## 为什么
过多的 Prefab 实例使所有依赖 query 的遍历路径被动承载更大的 entity 基数——即使是高效 job 遍历也受限于总 entity 数。当 Prefab 数量膨胀时，原本设计为处理 1000 entity 的遍历路径可能面临 10000 entity 的压力。

## EX-GAS 诊断
（待补充 — Primary Owner 为 Prefab-Content管理）

## 检查方法
（待补充 — 详见 Prefab-Content管理 主题的规范）
