# DynamicBuffer-Chunk-Archetype

## 职责边界

本主题覆盖 DynamicBuffer 容量管理与溢出监控、Archetype 布局与碎片化诊断、Chunk 物理布局（16 KiB 固定块）、Chunk Component / SharedComponent 选型。不覆盖 Store 选型框架（见 `Store选型-数据承载策略/_index.md`）、不覆盖 BlobAsset / NativeContainer / ECB 等其他数据承载方式。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Chunk 物理布局、Archetype 本质、DynamicBuffer 机制）
2. **核心规范**（按严重度）
   - `PRF-01: 禁止用 Entity 表示临时_瞬时状态.md` — P0: 禁止用 Entity 表示临时/瞬时状态
   - `BUF-01: DynamicBuffer 声明容量策略和 externalized 监控.md` — P0: DynamicBuffer 声明容量策略和 externalized 监控
   - `BUF-03: Buffer handle 结构变化后必须重取.md` — P0: Buffer handle 结构变化后必须重取
   - `BUF-02: 单一全局 buffer 限于 proof_低量；不做百万实体 fan-in.md` — P1: 单一全局 buffer 限于 proof/低量；不做百万实体 fan-in
   - `BUF-04: InternalBufferCapacity 按实际容量需求设定.md` — P1: InternalBufferCapacity 按实际容量需求设定
   - `PRF-10: 监控 DynamicBuffer 溢出.md` — P1: 监控 DynamicBuffer 溢出
   - `PRF-24: 禁止逐 Component 构建 Entity Archetype（P2 补充）.md` — P2: 禁止逐 Component 构建 Entity Archetype（批量创建）
3. **模式与案例**
   - `CASE-04: Owner-local DynamicBuffer.md` — Owner-local DynamicBuffer 用法
   - `CASE-23: BufferAccessor in IJobChunk.md` — BufferAccessor in IJobChunk
   - `CASE-28: Chunk Component.md` — Chunk Component 用法
   - `CASE-33: ComponentTypeSet 批量结构变化.md` — ComponentTypeSet 批量结构变化
   - `CASE-36: DynamicBuffer.Reinterpret.md` — DynamicBuffer.Reinterpret
4. **拓展阅读**（按需）
   - Store 选型框架 → `Store选型-数据承载策略/_index.md`
   - Enableable 替代 tag component → `Enableable-Component选型/_index.md`
   - ECB 结构变化 → `结构变化-ECB/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | DynamicBuffer/Chunk/Archetype 核心概念 |
| `BUF-01: DynamicBuffer 声明容量策略和 externalized 监控.md` | 规范 P0 | DynamicBuffer 容量策略和 externalized 监控 |
| `BUF-02: 单一全局 buffer 限于 proof_低量；不做百万实体 fan-in.md` | 规范 P1 | 单一全局 buffer 限于低量 |
| `BUF-03: Buffer handle 结构变化后必须重取.md` | 规范 P0 | Buffer handle 结构变化后必须重取 |
| `BUF-04: InternalBufferCapacity 按实际容量需求设定.md` | 规范 P1 | InternalBufferCapacity 按实际需求设定 |
| `PRF-01: 禁止用 Entity 表示临时_瞬时状态.md` | 规范 P0 | 禁止用 Entity 表示临时/瞬时状态 |
| `PRF-10: 监控 DynamicBuffer 溢出.md` | 规范 P1 | 监控 DynamicBuffer 溢出 |
| `PRF-24: 禁止逐 Component 构建 Entity Archetype（P2 补充）.md` | 规范 P2 | 禁止逐 Component 构建 Entity Archetype |
| `CASE-04: Owner-local DynamicBuffer.md` | 模式 | Owner-local DynamicBuffer 用法 |
| `CASE-23: BufferAccessor in IJobChunk.md` | 模式 | BufferAccessor in IJobChunk |
| `CASE-28: Chunk Component.md` | 模式 | Chunk Component 用法 |
| `CASE-33: ComponentTypeSet 批量结构变化.md` | 模式 | ComponentTypeSet 批量结构变化 |
| `CASE-36: DynamicBuffer.Reinterpret.md` | 模式 | DynamicBuffer.Reinterpret |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `PRF-01` | P0 | 禁止用 Entity 表示临时/瞬时状态 | Debugger 输出 entityCreated - entityDestroyed 是否约等于 0 |
| `BUF-01` | P0 | DynamicBuffer 声明容量策略和 externalized 监控 | 搜索 IBufferElementData 声明，确认有 InternalBufferCapacity |
| `BUF-03` | P0 | Buffer handle 结构变化后必须重取 | 搜索 GetBuffer 后出现 CreateEntity/DestroyEntity/AddComponent/RemoveComponent |
| `BUF-02` | P1 | 单一全局 buffer 限于 proof/低量 | 审查 GetSingletonBuffer 写入路径，超过 100 并行 writer 则违规 |
| `BUF-04` | P1 | InternalBufferCapacity 按实际容量需求设定 | 审查每个 InternalBufferCapacity 值是否匹配运行时典型长度 |
| `PRF-10` | P1 | 监控 DynamicBuffer 溢出 | externalizedCount / totalBufferCount > 30% 时告警 |
| `PRF-24` | P2 | 禁止逐 Component 构建 Entity Archetype | Code review 检查新 entity 创建路径 |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `BUF-01` | Store选型-数据承载策略 | DynamicBuffer 作为 Gameplay store 的容量策略 |
| `BUF-04` | Store选型-数据承载策略 | ActiveEffectSlot 容量规划 |
| `PRF-01` | Store选型-数据承载策略 | Instant GE 无 entity 路径 |
| `PRF-14` | NativeContainer-Allocator | 预创建 Archetype 批量创建（本主题 PRF-14 引用相关） |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Store 选型框架与数据分类 | `Store选型-数据承载策略/_index.md` |
| Enableable 替代 tag component | `Enableable-Component选型/_index.md` |
| ECB 结构变化 | `结构变化-ECB/_index.md` |
| NativeContainer allocator 管理 | `NativeContainer-Allocator/_index.md` |
| BlobAsset 静态数据 | `Baking-BlobAsset/_index.md` |

## 验收指标

1. Debugger 报告 buffer length、capacity、externalized、spill rate，每个关键 buffer 类型独立统计
2. 每个 `IBufferElementData` 声明具有明确的 `[InternalBufferCapacity(N)]` 值，且该值匹配运行时典型长度
3. 百万实体压测中 DynamicBuffer 访问呈线性 chunk 扫描或可解释的 O(n) 模式
4. ActiveEffectStore 不出现 per-effect-application entity churn（entityCreated ~= entityDestroyed ~= 0）
5. Debugger 输出 per-archetype 统计，能识别单 entity archetype 和 prefab chunk 浪费
6. 所有 entity 创建路径使用 `CreateArchetype` + `CreateEntity(archetype, count)` 模式，无 `CreateEntity()` 后逐个 `AddComponent` 的模式
7. Debugger 输出 archetype 总数、每个 archetype 的 chunk count/entity count、孤立的单 entity archetype 列表
8. `externalizedCount / totalBufferCount > 30%` 触发告警
