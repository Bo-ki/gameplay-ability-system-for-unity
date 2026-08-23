# BUF-03：结构变化后必须重新获取 DynamicBuffer 引用

**严重度**：P0
**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `components-buffer-introducing.md`、`components-buffer-jobs.md`

## 规则声明
任何结构变化后，先前获取的 `DynamicBuffer<T>` 及由它取得的 ref/pointer 都视为失效，使用前重新获取。`BufferLookup<T>`/`BufferTypeHandle<T>` 可作为 system 字段缓存，但必须在调度/访问前按官方模式 `.Update(ref state)`；不要把它们与 DynamicBuffer 引用混为一谈。

## 为什么
结构变化可能移动或销毁 DynamicBuffer 引用的数组。Lookup/TypeHandle 是用于定位数据的缓存结构；从它们取得的具体 buffer 引用仍受失效规则约束。

## EX-GAS 诊断
同一系统若先取得 buffer、随后直接 EntityManager 结构变化，必须在后续访问前重新 GetBuffer；若结构变化延迟到 ECB playback 且本系统不再访问，则无需无意义重取。

## 检查方法
搜索 `GetBuffer`/lookup 索引后到结构变化之间的引用存活范围，并检查每帧 handle/lookup Update。
