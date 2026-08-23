# QRY-04: 高频 random lookup 重构为 owner-local 或 chunk-local

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `iterating-data-ijobchunk-implement.md`、`components-enableable-use.md`

## 规则声明
热点路径中的 `ComponentLookup` / `BufferLookup` 随机访问应纳入 Profiler 与 cache-locality 审核。只有确认其是瓶颈，且 owner-local、分组或 chunk 顺序布局不会制造更高同步/复制成本时，才要求重构。

## 为什么
Lookup 通过 `Entity` 定位数据，通常比 query/chunk 顺序访问更不利于缓存。Unity 没有承诺其内部为哈希查找，也没有给出固定成本倍率；不得把未经本项目基准验证的数字写成审查阈值。

## EX-GAS 诊断
Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source/target ASC 属性时，先测量访问局部性与等待成本。只有快照时点、复制一致性和容量边界明确时，才评估把必要输入捕获到 owner-local command/buffer；否则保留只读 lookup 可能更简单且更正确。

## 检查方法
- Grep `TryGetComponent` 和 `TryGetBuffer` 在 IJobEntity/IJobChunk 中的使用
- 评估 lookup 频率和 entity 规模
- 确认是否有 owner-local 替代方案可行
