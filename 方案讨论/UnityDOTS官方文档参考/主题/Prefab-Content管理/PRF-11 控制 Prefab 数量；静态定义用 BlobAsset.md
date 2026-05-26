# PRF-11: 控制 Prefab 数量；静态定义用 BlobAsset

**严重度**: P1
**Primary Owner**: Prefab-Content管理
**来源**: `performance-chunk-allocations.html`

## 规则声明
Prefab 数量必须审计，静态定义数据禁止使用 Entity Prefab 承载。（CONTENT-01 别名）

## 为什么
Prefab 独立 archetype 的 16 KiB chunk 开销在大量定义场景下不可接受。静态数据 BlobAsset 共享无需额外 chunk。

## EX-GAS 诊断
Debugger 报告 prefab archetype 数量和占用 chunk 内存。Prefab 数量超出 100 时触发告警审查。

## 检查方法
运行时查询所有 `Prefab` 标记的 entity 数量。与 "应有 prefab" 列表交叉比对发现冗余。
