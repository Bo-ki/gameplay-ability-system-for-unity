# Store 选型 / 数据承载策略

## 职责边界

本主题覆盖 EX-GAS 中不同性质数据的承载方式选型框架：gameplay / transient / telemetry / presentation 四类数据的承载策略、Store 命名规范、确定性要求。不覆盖底层 ECS 数据结构（DynamicBuffer / Archetype / Chunk 物理布局见 `DynamicBuffer-Chunk-Archetype/_index.md`）、不覆盖 BlobAsset 构建细节、不覆盖 NativeContainer allocator 管理。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解数据性质分类框架和选型决策流）
2. **核心规范**（按严重度）
   - `STORE-01: Store 命名表达 owner、生命周期、索引方式.md` — P1: Store 命名表达 owner、生命周期、索引方式
   - `STORE-02: 影响 battle hash 的输出 deterministic.md` — P1: 影响 battle hash 的输出 deterministic
   - `STORE-03: Store 选型按数据性质分类：gameplay _ transient _ telemetry _ presentation.md` — P1: Store 选型按数据性质分类：gameplay / transient / telemetry / presentation
3. **模式与案例**：本主题的 CASE 分布在其他主题中。
   - DynamicBuffer 作为 owner-local store → `DynamicBuffer-Chunk-Archetype/CASE-04.md`
   - NativeStream 并行 fan-in + deterministic merge → `NativeContainer-Allocator/CASE-12.md`
   - SystemGroup Allocator → `NativeContainer-Allocator/CASE-16.md`
   - Chunk Component 用法 → `DynamicBuffer-Chunk-Archetype/CASE-28.md`
   - ComponentTypeSet 批量结构变化 → `DynamicBuffer-Chunk-Archetype/CASE-33.md`
   - System-Associated Entity Data（CASE-45）→ `应用-高级模式/_index.md`
4. **拓展阅读**（按需）
   - DynamicBuffer 物理布局和容量管理 → `DynamicBuffer-Chunk-Archetype/_index.md`
   - NativeContainer allocator 生命周期 → `NativeContainer-Allocator/_index.md`
   - BlobAsset 只读共享数据 → `Baking-BlobAsset/_index.md`
   - ECB 延迟结构变化 → `结构变化-ECB/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | 数据性质分类框架、选型决策流、数据分类映射 |
| `STORE-01: Store 命名表达 owner、生命周期、索引方式.md` | 规范 P1 | Store 命名表达 owner、生命周期、索引方式 |
| `STORE-02: 影响 battle hash 的输出 deterministic.md` | 规范 P1 | 影响 battle hash 的输出 deterministic |
| `STORE-03: Store 选型按数据性质分类：gameplay _ transient _ telemetry _ presentation.md` | 规范 P1 | Store 选型按数据性质分类 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `STORE-01` | P1 | Store 命名表达 owner、生命周期、索引方式 | Code review 检查新 Store 类型是否包含 owner/lifetime/index 维度 |
| `STORE-02` | P1 | 影响 battle hash 的输出 deterministic | 检查 gameplay 数据通路的写入顺序是否可复现 |
| `STORE-03` | P1 | Store 选型按数据性质分类 | 每个数据承载方式选型需记录分类和选型理由 |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `STORE-01` | DynamicBuffer-Chunk-Archetype | ActiveEffectStore 名称规范 |
| `STORE-02` | Mathematics-确定性 | 确定性 fan-in 排序策略 |
| `STORE-03` | NativeContainer-Allocator | Allocator 选型与数据分类对应 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| DynamicBuffer 容量管理 | `DynamicBuffer-Chunk-Archetype/_index.md` |
| NativeContainer allocator | `NativeContainer-Allocator/_index.md` |
| BlobAsset 只读共享数据 | `Baking-BlobAsset/_index.md` |
| ECB 结构变化 | `结构变化-ECB/_index.md` |
| 确定性计算 | `Mathematics-确定性/_index.md` |

## 验收指标

1. 每个数据承载方式选型决策记录数据性质分类（gameplay/transient/telemetry/presentation）
2. 所有 gameplay 分类数据明确标注是否参与 battle hash，参与路径经过确定性验证
3. Transient 数据在帧末 clear 或 drain，无跨帧泄漏
4. Telemetry 数据不阻塞 gameplay hot path，通过独立采样窗口采集
5. Presentation 数据单向流入表现层，不反向影响 gameplay 状态
6. Store 名称符合 STORE-01 命名约束（owner/lifetime/index 维度）
7. 所有 NativeStream 的 gameplay 使用路径包含 deterministic merge 步骤
8. Debugger 输出 Store 分类统计和健康指标
