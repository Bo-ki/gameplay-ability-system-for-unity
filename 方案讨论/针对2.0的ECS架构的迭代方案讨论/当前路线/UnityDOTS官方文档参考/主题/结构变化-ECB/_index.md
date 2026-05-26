# 结构变化-ECB

## 职责边界

覆盖 ECS 中所有改变 entity archetype 的操作机制与管理规范：Structural Change、Sync Point、EntityCommandBuffer（ECB）。阐述为什么结构变化是性能瓶颈、如何通过 ECB 延迟合并降低成本、以及在 EX-GAS Runtime Core 中必须遵守的结构变化集中化设计。

不覆盖 Enableable Component（参见 `Enableable-Component选型/_index.md`），不覆盖 NativeStream 等非实体创建的数据流模式。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解结构变化/ECB/Sync Point 机制和 GAS 集中化 phase 设计）
2. **核心规范**（按严重度）
   - `SC-01: Hot Path 禁止直接结构变化.md` — P0: Hot path 禁止直接结构变化
   - `SC-02: 结构变化后必须重取所有 Handle.md` — P0: 结构变化后重取所有 handle
   - `ECB-01: ECB 是延迟结构变化工具，不是 Gameplay Event Bus.md` — P0: ECB 是延迟结构变化工具，不是 gameplay event bus
   - `ECB-02: AppendToBuffer 前必须确保 Buffer 已存在.md` — P0: AppendToBuffer 前确保 buffer 已存在
   - `PRF-02: 禁止在 Hot Path 直接执行结构变化（P0 致命）.md` — P0: 禁止在 Hot Path 直接执行结构变化
   - `PRF-04: 结构变化必须集中到单一 ECB Playback Phase（P0 致命）.md` — P0: 结构变化必须集中到单一 ECB playback phase
   - `SC-03: 批量同类结构变化优先 EntityQuery Bulk.md` — P1: 批量同类变化优先 EntityQuery bulk
   - `ECB-03: ECB Playback 位置必须属于明确 SystemGroup Phase.md` — P1: ECB playback 位置属于明确 SystemGroup phase
   - `ECB-04: ECB Allocator 在 Playback 后 Rewind.md` — P1: ECB allocator 在 playback 后 rewind
3. **模式与案例**
   - `CASE-05: ECB 延迟结构变化.md` — ECB 延迟结构变化
   - `CASE-21: ECB PlaybackPolicy.MultiPlayback + Deferred Entity Remapping.md` — ECB PlaybackPolicy.MultiPlayback + Deferred Entity Remapping
   - `CASE-25: IECBSingleton 自定义 ECB System.md` — IECBSingleton 自定义 ECB System
   - `CASE-34: EntityQueryCaptureMode.AtPlayback.md` — EntityQueryCaptureMode.AtPlayback
   - `CASE-35: [ChunkIndexInQuery] sortKey 确定性 ECB 回放.md` — [ChunkIndexInQuery] sortKey 确定性 ECB 回放
   - `CASE-47: ECB AppendToBuffer + sortKey 多源并行 fan-in.md` — ECB AppendToBuffer + sortKey 多源并行 fan-in
4. **拓展阅读**（按需）
   - Enableable Component 替代方案 → `Enableable-Component选型/_index.md`
   - System 内部 job 调度 → `Query-Job-遍历/_index.md`
   - 数据流确定性与生命周期 → `数据流-系统生命周期/_index.md`
   - SystemGroup 层次结构与 phase 设计 → `System-World-SystemGroup/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | 结构变化/ECB/Sync Point 机制详解 + GAS 集中化 phase 设计 |
| `SC-01: Hot Path 禁止直接结构变化.md` | 规范 P0 | Hot path 禁止直接结构变化 |
| `SC-02: 结构变化后必须重取所有 Handle.md` | 规范 P0 | 结构变化后必须重取所有 handle |
| `SC-03: 批量同类结构变化优先 EntityQuery Bulk.md` | 规范 P1 | 批量同类结构变化优先 EntityQuery bulk |
| `ECB-01: ECB 是延迟结构变化工具，不是 Gameplay Event Bus.md` | 规范 P0 | ECB 是延迟结构变化工具，不是 gameplay event bus |
| `ECB-02: AppendToBuffer 前必须确保 Buffer 已存在.md` | 规范 P0 | AppendToBuffer 前确保 buffer 已存在 |
| `ECB-03: ECB Playback 位置必须属于明确 SystemGroup Phase.md` | 规范 P1 | ECB playback 位置属于明确 SystemGroup phase |
| `ECB-04: ECB Allocator 在 Playback 后 Rewind.md` | 规范 P1 | ECB allocator 在 playback 后 rewind |
| `PRF-02: 禁止在 Hot Path 直接执行结构变化（P0 致命）.md` | 规范 P0 | 禁止在 Hot Path 直接执行结构变化（致命） |
| `PRF-04: 结构变化必须集中到单一 ECB Playback Phase（P0 致命）.md` | 规范 P0 | 结构变化必须集中到单一 ECB playback phase（致命） |
| `CASE-05: ECB 延迟结构变化.md` | 模式 | ECB 延迟结构变化 |
| `CASE-21: ECB PlaybackPolicy.MultiPlayback + Deferred Entity Remapping.md` | 模式 | ECB PlaybackPolicy.MultiPlayback + Deferred Entity Remapping |
| `CASE-25: IECBSingleton 自定义 ECB System.md` | 模式 | IECBSingleton 自定义 ECB System |
| `CASE-34: EntityQueryCaptureMode.AtPlayback.md` | 模式 | EntityQueryCaptureMode.AtPlayback |
| `CASE-35: [ChunkIndexInQuery] sortKey 确定性 ECB 回放.md` | 模式 | [ChunkIndexInQuery] sortKey 确定性 ECB 回放 |
| `CASE-47: ECB AppendToBuffer + sortKey 多源并行 fan-in.md` | 模式 | ECB AppendToBuffer + sortKey 多源并行 fan-in |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `SC-01` | P0 | Hot path 禁止直接结构变化 | 搜索 EntityManager 结构变化 API 在 OnUpdate 或 job 中的出现 |
| `SC-02` | P0 | 结构变化后重取所有 handle | Code review 检查结构变化 API 后是否继续使用旧 buffer/handle |
| `SC-03` | P1 | 批量同类变化优先 EntityQuery bulk | 搜索 foreach + AddComponent/DestroyEntity 模式 |
| `ECB-01` | P0 | ECB 不是 gameplay event bus | ECB command 语义审查；非结构变化操作视为违规 |
| `ECB-02` | P0 | AppendToBuffer 前确保 buffer 存在 | 搜索 AppendToBuffer 并逐处确认上游 AddComponent |
| `ECB-03` | P1 | ECB playback 位置在明确 phase | 审计 CreateCommandBuffer 调用来源 |
| `ECB-04` | P1 | ECB allocator playback 后 rewind | 搜索 ECB 类型字段或跨帧缓存 |
| `PRF-02` | P0 | 禁止 Hot Path 直接结构变化 | Grep EntityManager 直接结构变化；Debugger 输出 structuralChangeCount |
| `PRF-04` | P0 | 结构变化集中单一 ECB playback phase | 统计每帧 sync point 数量 |

## 跨主题引用

本主题规则被以下主题引用：

| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `SC-01` | 技术领域/结构变化-ECB.md | 原始来源 |
| `PRF-02` | 技术领域/13-DOTS编写规范与性能陷阱.md | P0 致命级规范 |
| `PRF-04` | 技术领域/13-DOTS编写规范与性能陷阱.md | P0 致命级规范 |
| `ECB-03` | System-World-SystemGroup | ECB playback phase 归属 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Enableable Component 如何替代高频结构变化 | `Enableable-Component选型/_index.md` |
| ECB 与 Job 依赖的关系 | `Query-Job-遍历/_index.md` |
| 数据流确定性与生命周期 | `数据流-系统生命周期/_index.md` |
| SystemGroup 层次结构与 phase 设计 | `System-World-SystemGroup/_index.md` |

## 验收指标

1. Debugger 能报告每帧 `structuralChangeCount`、`ecbCommandCount`、`syncPointCount` 并按来源 system 分类
2. Hot path 零直接 `EntityManager.CreateEntity/DestroyEntity/AddComponent/RemoveComponent` 调用
3. 所有结构变化来源集中在 `GasStructuralPlaybackSystemGroup` 的 ECB playback phase
4. 不再出现 `BufferTypeHandle invalidated by structural change` 安全异常
5. AutoChess x50 压测中 `structuralChangeCount` 不随单位数线性增长
6. Instant GE 路径的 entity 创建/销毁随 instant 伤害数量呈零增长（迁移为 EffectCommand 直写）
7. 所有结构变化来源可通过 Debugger 追踪到具体 System 名称和所在 phase
8. Frame backbone 设计图中只标注一个结构变化 phase，sync point count 稳定在 1
