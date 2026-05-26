# NativeContainer-Allocator

## 职责边界

本主题维护 NativeContainer（NativeList、NativeArray、NativeStream 等）在 EX-GAS 中的 allocator 生命周期管理、确定性写入规则和 dispose 规范。覆盖 Allocator.Temp/TempJob/Persistent 三类型使用边界、RewindableAllocator 帧级一次性分配、NativeStream 并行 fan-in 与 deterministic merge、以及容器归属的监控要求。不覆盖托管内存分配或 UnityEngine.Object 的资源管理。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Allocator 三种生命周期、NativeStream 机制）
2. **核心规范**（按严重度）
   - `NAT-01: 每个 NativeContainer 必须说明 allocator 类型、owner 和 dispose_rewind 位置.md` — P0: 每个 NativeContainer 必须说明 allocator 类型、owner 和 dispose/rewind 位置
   - `NAT-03: NativeStream 并行 fan-in 必须定义 merge 顺序和内存预算.md` — P0: NativeStream 并行 fan-in 必须定义 merge 顺序和内存预算
   - `NAT-04: Persistent 容器必须有明确的 owner entity_system 和 teardown 规则.md` — P0: Persistent 容器必须有明确的 owner entity/system 和 teardown 规则
   - `NAT-02: 影响 battle hash 的结果禁止使用无序 ParallelWriter.md` — P1: 影响 battle hash 的结果禁止使用无序 ParallelWriter
   - `NAT-05: Allocator 指标必须进入 Debugger 监控；GC-free 不等于 allocation-free.md` — P1: Allocator 指标必须进入 Debugger 监控
   - `PRF-14: NativeContainer 必须明确 Allocator 归属和生命周期.md` — P1: NativeContainer 必须明确 Allocator 归属和生命周期
   - `PRF-21: 工作线程结构变化用 ExclusiveEntityTransaction.md` — P1: 工作线程结构变化用 ExclusiveEntityTransaction
   - `PRF-34: NativeContainer 放在 IComponentData 上时禁止调度 IJobChunk_IJobEntity.md` — P1: NativeContainer 放在 IComponentData 上时禁止调度 IJobChunk/IJobEntity
3. **模式与案例**
   - `CASE-12: NativeStream 并行 Fan-In + Deterministic Merge.md` — NativeStream 并行 fan-in + deterministic merge
   - `CASE-16: SystemGroup Allocator.md` — SystemGroup Allocator：per-group scratch allocator
4. **拓展阅读**（按需）
   - Store 数据分类框架 → `Store选型-数据承载策略/_index.md`
   - DynamicBuffer 替代 NativeContainer 的场景 → `DynamicBuffer-Chunk-Archetype/_index.md`
   - Burst 编译约束 → `Burst-AOT/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | Allocator 生命周期、NativeStream 机制、Deterministic Merge |
| `NAT-01: 每个 NativeContainer 必须说明 allocator 类型、owner 和 dispose_rewind 位置.md` | 规范 P0 | 每个 NativeContainer 标注 allocator 类型、owner 和 dispose 位置 |
| `NAT-02: 影响 battle hash 的结果禁止使用无序 ParallelWriter.md` | 规范 P1 | 影响 battle hash 的结果禁止使用无序 ParallelWriter |
| `NAT-03: NativeStream 并行 fan-in 必须定义 merge 顺序和内存预算.md` | 规范 P0 | NativeStream 并行 fan-in 必须定义 merge 顺序和内存预算 |
| `NAT-04: Persistent 容器必须有明确的 owner entity_system 和 teardown 规则.md` | 规范 P0 | Persistent 容器必须有明确 owner 和 teardown 规则 |
| `NAT-05: Allocator 指标必须进入 Debugger 监控；GC-free 不等于 allocation-free.md` | 规范 P1 | Allocator 指标必须进入 Debugger 监控 |
| `PRF-14: NativeContainer 必须明确 Allocator 归属和生命周期.md` | 规范 P1 | NativeContainer 必须明确 Allocator 归属和生命周期 |
| `PRF-21: 工作线程结构变化用 ExclusiveEntityTransaction.md` | 规范 P1 | 工作线程结构变化用 ExclusiveEntityTransaction |
| `PRF-34: NativeContainer 放在 IComponentData 上时禁止调度 IJobChunk_IJobEntity.md` | 规范 P1 | NativeContainer 在 IComponentData 上禁止调度并行 job |
| `CASE-12: NativeStream 并行 Fan-In + Deterministic Merge.md` | 模式 | NativeStream 并行 fan-in + deterministic merge |
| `CASE-16: SystemGroup Allocator.md` | 模式 | SystemGroup Allocator：per-group scratch allocator |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `NAT-01` | P0 | 每个 NativeContainer 必须说明 allocator 类型、owner 和 dispose 位置 | 代码审查每个分配点 |
| `NAT-03` | P0 | NativeStream 并行 fan-in 必须定义 merge 顺序和内存预算 | 检查 merge 阶段是否有排序步骤 |
| `NAT-04` | P0 | Persistent 容器必须有明确 owner 和 teardown 规则 | 审查所有 Allocator.Persistent 使用点 |
| `NAT-02` | P1 | 影响 battle hash 的结果禁止使用无序 ParallelWriter | Grep AsParallelWriter 在 battle-deterministic 路径的使用 |
| `NAT-05` | P1 | Allocator 指标必须进入 Debugger 监控 | Debugger validation 包含 allocator 指标表格 |
| `PRF-14` | P1 | NativeContainer 必须明确 Allocator 归属和生命周期 | Code review checklist 包含确认项 |
| `PRF-21` | P1 | 工作线程结构变化用 ExclusiveEntityTransaction | 搜索批量 entity 创建场景 |
| `PRF-34` | P1 | NativeContainer 在 IComponentData 上禁止调度并行 job | Grep IComponentData 中的 NativeContainer 字段 |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `NAT-04` | System-World-SystemGroup | SystemGroup Allocator 生命周期 |
| `PRF-14` | DynamicBuffer-Chunk-Archetype | 预创建 Archetype 与 allocator 关联 |
| `PRF-34` | Store选型-数据承载策略 | Store 中 NativeContainer 使用约束 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Store 数据分类框架 | `Store选型-数据承载策略/_index.md` |
| DynamicBuffer 替代 NativeContainer | `DynamicBuffer-Chunk-Archetype/_index.md` |
| Burst 编译约束 | `Burst-AOT/_index.md` |
| 确定性计算规范 | `Mathematics-确定性/_index.md` |

## 验收指标

1. API 选型表包含 allocator 和 deterministic output policy
2. Debugger 报告 NativeContainer 分配、dispose、stream segment、merge cost
3. 十万/百万实体压测下 fan-in 不因单一写入点串行化
4. Battle hash 稳定（确定性 merge）
5. 所有 Persistent 分配有明确 owner 和 teardown 路径
6. 零 NativeContainer 作为 IComponentData 字段直接参与并行 job 调度
