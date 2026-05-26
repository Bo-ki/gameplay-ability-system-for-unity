# Query-Job-遍历

## 职责边界

本主题覆盖 EntityQuery 构建与选项、三种遍历方式对比（SystemAPI.Query / IJobEntity / IJobChunk）、job 调度与依赖管理、ComponentLookup/BufferLookup 随机访问、query filter / ChangeFilter / chunk.DidChange 语义、以及并行安全规则（竞态条件、嵌套 job、参数匹配）。不覆盖 SystemGroup 层次结构（见 System-World-SystemGroup/_index.md）、不覆盖 ECB 使用细节（见 结构变化-ECB/_index.md）、不覆盖 enableable component 生命周期。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解三种遍历方式核心机制、job 调度原理、随机访问语义和 GAS 项目应用）
2. **核心规范**（按严重度）
   - `QRY-01: Hot path 优先 job 化；SystemAPI.Query 限于小规模_debug.md` — P0: Hot path 优先 job 化；SystemAPI.Query 限于小规模/debug
   - `QRY-02: Query contract 写清 All_Any_None_Disabled_ChangeFilter.md` — P0: Query contract 写清 All/Any/None/Disabled/ChangeFilter
   - `JOB-01: 并行批处理说明 IJobEntity_IJobChunk_主线程选择理由.md` — P0: 并行批处理说明 IJobEntity/IJobChunk/主线程选择理由
   - `PRF-05: Hot Path 禁止主线程遍历.md` — P0: Hot Path 禁止主线程遍历
   - `PRF-06: 禁止高频 Random Access Lookup.md` — P0: 禁止高频 Random Access Lookup
   - `QRY-03: Optional 分支用 chunk 级判断.md` — P1: Optional 分支用 chunk 级判断
   - `QRY-04: 高频 random lookup 重构为 owner-local 或 chunk-local.md` — P1: 高频 random lookup 重构为 owner-local 或 chunk-local
   - `JOB-02: 区分 job scheduling overhead、main-thread sync、Burst warmup.md` — P1: 区分 job scheduling overhead、main-thread sync、Burst warmup
   - `JOB-03: 不使用 EntityIndexInQuery 作为 hot path 高频操作.md` — P1: 不使用 EntityIndexInQuery 作为 hot path 高频操作
   - `JOB-04: IJobEntity 的 Execute 参数明确 ref_in 语义.md` — P1: IJobEntity 的 Execute 参数明确 ref/in 语义
   - `PRF-08: 禁止 EntityIndexInQuery 在 Hot Path 使用.md` — P1: 禁止 EntityIndexInQuery 在 Hot Path 使用
   - `PRF-09: 注意 Query 操作的 Sync 触发.md` — P1: 注意 Query 操作的 Sync 触发
   - `PRF-11: 控制 Prefab 数量.md` — P1: 控制 Prefab 数量（跨主题引用）
   - `PRF-12: 审核 SharedComponent 使用.md` — P1: 审核 SharedComponent 使用
   - `PRF-13: 确定性输出不得依赖无序写入.md` — P1: 确定性输出不得依赖无序写入（跨主题引用）
   - `PRF-17: 禁止使用 IAspect.md` — P1: 禁止使用 IAspect
   - `PRF-19: ComponentLookup_BufferLookup 随机访问竞态.md` — P1: ComponentLookup/BufferLookup 随机访问竞态（跨主题引用）
   - `PRF-20: IJobEntity Execute 参数不匹配的安全风险.md` — P1: IJobEntity Execute 参数不匹配的安全风险
   - `PRF-23: 禁止从 Job 内部启动新 Job.md` — P1: 禁止从 Job 内部启动新 Job
3. **模式与案例**
   - `CASE-01: SystemAPI.Query 遍历.md` — SystemAPI.Query 遍历：仅 Debugger/Editor/proof
   - `CASE-02: IJobEntity 遍历.md` — IJobEntity 遍历：Runtime Core hot path 主力
   - `CASE-03: IJobChunk 遍历.md` — IJobChunk 遍历：批量统计、enableable 过滤
   - `CASE-13: Aspects（已废弃）.md` — Aspects（已废弃）：禁止新代码使用
   - `CASE-18: chunk.DidChange 精细变更检测.md` — chunk.DidChange：精细变更检测
4. **拓展阅读**（按需）
   - 数据流确定性与生命周期 → `数据流-系统生命周期/_index.md`
   - ECB 使用细节 → `结构变化-ECB/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | 三种遍历方式核心机制详解 + job 调度 + EX-GAS 项目应用 + 常见陷阱 |
| `QRY-01: Hot path 优先 job 化；SystemAPI.Query 限于小规模_debug.md` | 规范 P0 | Hot path 优先 job 化；SystemAPI.Query 限于小规模/debug |
| `QRY-02: Query contract 写清 All_Any_None_Disabled_ChangeFilter.md` | 规范 P0 | Query contract 写清 All/Any/None/Disabled/ChangeFilter |
| `QRY-03: Optional 分支用 chunk 级判断.md` | 规范 P1 | Optional 分支用 chunk 级判断 |
| `QRY-04: 高频 random lookup 重构为 owner-local 或 chunk-local.md` | 规范 P1 | 高频 random lookup 重构为 owner-local 或 chunk-local |
| `JOB-01: 并行批处理说明 IJobEntity_IJobChunk_主线程选择理由.md` | 规范 P0 | 并行批处理说明 IJobEntity/IJobChunk/主线程选择理由 |
| `JOB-02: 区分 job scheduling overhead、main-thread sync、Burst warmup.md` | 规范 P1 | 区分 job scheduling overhead、main-thread sync、Burst warmup |
| `JOB-03: 不使用 EntityIndexInQuery 作为 hot path 高频操作.md` | 规范 P1 | 不使用 EntityIndexInQuery 作为 hot path 高频操作 |
| `JOB-04: IJobEntity 的 Execute 参数明确 ref_in 语义.md` | 规范 P1 | IJobEntity 的 Execute 参数明确 ref/in 语义 |
| `PRF-05: Hot Path 禁止主线程遍历.md` | 规范 P0 | Hot Path 禁止主线程遍历 |
| `PRF-06: 禁止高频 Random Access Lookup.md` | 规范 P0 | 禁止高频 Random Access Lookup |
| `PRF-08: 禁止 EntityIndexInQuery 在 Hot Path 使用.md` | 规范 P1 | 禁止 EntityIndexInQuery 在 Hot Path 使用 |
| `PRF-09: 注意 Query 操作的 Sync 触发.md` | 规范 P1 | 注意 Query 操作的 Sync 触发 |
| `PRF-11: 控制 Prefab 数量.md` | 规范 P1 | 控制 Prefab 数量（跨主题引用） |
| `PRF-12: 审核 SharedComponent 使用.md` | 规范 P1 | 审核 SharedComponent 使用 |
| `PRF-13: 确定性输出不得依赖无序写入.md` | 规范 P1 | 确定性输出不得依赖无序写入（跨主题引用） |
| `PRF-17: 禁止使用 IAspect.md` | 规范 P1 | 禁止使用 IAspect |
| `PRF-19: ComponentLookup_BufferLookup 随机访问竞态.md` | 规范 P1 | ComponentLookup/BufferLookup 随机访问竞态（跨主题引用） |
| `PRF-20: IJobEntity Execute 参数不匹配的安全风险.md` | 规范 P1 | IJobEntity Execute 参数不匹配的安全风险 |
| `PRF-23: 禁止从 Job 内部启动新 Job.md` | 规范 P1 | 禁止从 Job 内部启动新 Job |
| `CASE-01: SystemAPI.Query 遍历.md` | 模式 | SystemAPI.Query 遍历：仅 Debugger/Editor/proof |
| `CASE-02: IJobEntity 遍历.md` | 模式 | IJobEntity 遍历：Runtime Core hot path 主力 |
| `CASE-03: IJobChunk 遍历.md` | 模式 | IJobChunk 遍历：批量统计、enableable 过滤 |
| `CASE-13: Aspects（已废弃）.md` | 模式 | Aspects（已废弃）：禁止新代码使用 |
| `CASE-18: chunk.DidChange 精细变更检测.md` | 模式 | chunk.DidChange：精细变更检测 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `QRY-01` | P0 | Hot path 优先 job 化；SystemAPI.Query 限于小规模/debug | Grep 扫描 Runtime Core 中 SystemAPI.Query 和 .Run() ；Code review 确认新增遍历路径的规模 |
| `QRY-02` | P0 | Query contract 写清 All/Any/None/Disabled/ChangeFilter | Grep IJobEntity/IJobChunk 检查 query 属性是否完整 |
| `QRY-03` | P1 | Optional 分支用 chunk 级判断而非拆分 system | 搜索 Runtime Core 中是否为不同类型创建独立 system/query |
| `QRY-04` | P1 | 高频 random lookup 重构为 owner-local 或 chunk-local | Grep TryGetComponent/TryGetBuffer 在 job 中用法；评估 lookup 频率 |
| `JOB-01` | P0 | 并行批处理说明 IJobEntity/IJobChunk/主线程选择理由 | Code review 确认每个遍历选择有理由文档 |
| `JOB-02` | P1 | 区分 job scheduling overhead、main-thread sync、Burst warmup | 审查 OnUpdate 中 job 调度代码；统计调度开销 vs 执行开销 |
| `JOB-03` | P1 | 不使用 EntityIndexInQuery 作为 hot path 高频操作 | Grep EntityIndexInQuery 在 Runtime Core 中的使用 |
| `JOB-04` | P1 | IJobEntity Execute 参数明确 ref/in 语义 | Code review 检查 ref/in 使用是否正确 |
| `PRF-05` | P0 | Hot Path 禁止主线程遍历 | 搜索 Runtime Core 中 SystemAPI.Query 和 .Run( 的 entity 规模和频率 |
| `PRF-06` | P0 | 禁止高频 Random Access Lookup | Grep TryGetComponent/TryGetBuffer 在 IJobEntity/IJobChunk 中使用；评估规模 |
| `PRF-08` | P1 | 禁止 EntityIndexInQuery 在 Hot Path 使用 | Grep EntityIndexInQuery/EntityIndexInChunk/ChunkIndexInQuery |
| `PRF-09` | P1 | 注意 Query 操作的 Sync 触发 | 搜索 OnUpdate 中 CalculateEntityCount/ToEntityArray/ToComponentDataArray/GetSingleton |
| `PRF-11` | P1 | 控制 Prefab 数量（跨主题） | — |
| `PRF-12` | P1 | 审核 SharedComponent 使用 | 审查 SharedComponent 在 job 中的使用模式 |
| `PRF-13` | P1 | 确定性输出不得依赖无序写入（跨主题） | — |
| `PRF-17` | P1 | 禁止使用 IAspect | 全局搜索 IAspect 关键字；Code review 拦截 |
| `PRF-19` | P1 | ComponentLookup/BufferLookup 随机访问竞态（跨主题） | Grep ComponentLookup/BufferLookup 检查 entity 集合重叠 |
| `PRF-20` | P1 | IJobEntity Execute 参数不匹配安全风险 | Code review 交叉验证 query 属性和 Execute 参数 |
| `PRF-23` | P1 | 禁止从 Job 内部启动新 Job | Grep 搜索嵌套 .Schedule()/.Run() 模式 |

## 跨主题引用

其他主题引用了本主题的规则：

| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `PRF-05` | System-World-SystemGroup | System 内遍历方式选择 |
| `PRF-17` | 数据流-系统生命周期 | 已废弃 IAspect 替代方案 |

本主题跨主题引用的规则（Primary Owner 在其他主题）：

| 规则 | Primary Owner | 说明 |
|------|---------------|------|
| `PRF-11` | Prefab-Content管理 | 控制 Prefab 数量以减少 query 遍历开销 |
| `PRF-13` | 数据流-系统生命周期 | 确定性输出不依赖无序写入的全局约束 |
| `PRF-19` | 数据流-系统生命周期 | ComponentLookup/BufferLookup 随机访问竞态 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| System 如何组织成 SystemGroup | `System-World-SystemGroup/_index.md` |
| ECB 如何正确使用 | `结构变化-ECB/_index.md` |
| 数据流确定性保证 | `数据流-系统生命周期/_index.md` |
| Enableable component 生命周期 | `enableable-component/_index.md` |

## 验收指标

1. Runtime Core 任务交还包含 query contract（All/Any/None/ChangeFilter/IgnoreEnabledState）的显式声明。
2. 性能报告包含 filtered / unfiltered query count、lookup count、job count、active system count。
3. 百万实体目标下，主链 hot path 不依赖逐实体 managed callback 或主线程 foreach。
4. AutoChess Driver 不再每 tick `ToEntityArray` 全量扫描（改用异步 query 或 read model）。
5. 每个 IJobEntity 旁有 EntityQuery 条件注释，Code review 时交叉验证通过（PRF-20）。
6. 零 `IAspect` 出现在新增代码中；已有 Aspect 登记为待迁移（PRF-17）。
7. 每个 System 的 job 依赖链在 Debugger 中可审查（JOB-02）。
8. 无嵌套 job 调用（PRF-23 检查通过）。
9. 每个并行 job 使用独立 ECB。
10. 所有 Runtime Core 遍历使用 IJobEntity 或 IJobChunk + Burst（PRF-05 检查通过）。
