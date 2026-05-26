# System-World-SystemGroup

## 职责边界

本主题覆盖 ECS 中 World 生命周期管理、System 类型选择（ISystem vs SystemBase）、SystemGroup 层次结构与更新排序、以及系统数量成本管控。不覆盖 job 调度细节（见 Query-Job-遍历/_index.md）、不覆盖单个 System 内部数据流（见 数据流-系统生命周期/_index.md）、不覆盖 Baking 期系统。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 World/System/SystemGroup 机制和 GAS 8-phase 设计）
2. **核心规范**（按严重度）
   - `SYS-01: 权威计算落在 ECS System_Job 数据流，禁止托管 manager 驱动.md` — P0: 权威计算落在 ECS System/Job 数据流，禁止托管 manager 驱动
   - `SYS-02: SystemGroup 是 phase owner，禁止手写 Tick 顺序.md` — P0: SystemGroup 是 phase owner，禁止手写 Tick 顺序
   - `PRF-07: 避免不必要的 System 拆分.md` — P1: 避免不必要的 System 拆分
   - `SYS-03: 系统数量是成本源，避免不必要的 system 拆分.md` — P1: 系统数量是成本源
   - `SYS-04: Core_Physics_Presentation 成本分组统计.md` — P1: Core/Physics/Presentation 成本分组统计
   - `SYS-05: World 边界：Debugger_Demo_Presentation 只能通过 Boundary 观察 Core.md` — P1: World 边界：Debugger/Demo/Presentation 只能通过 Boundary 观察 Core
   - `PRF-16: System 创建依赖用 CreateAfter；ISystem 优于 SystemBase.md` — P2: System 创建依赖用 CreateAfter；ISystem 优于 SystemBase
3. **模式与案例**
   - `CASE-16: SystemGroup Allocator.md` — SystemGroup Allocator：per-group scratch allocator
   - `CASE-17: ICustomBootstrap 多世界.md` — ICustomBootstrap 多世界：AutoChess 无头验收
4. **拓展阅读**（按需）
   - System 内部 job 调度 → `Query-Job-遍历/_index.md`
   - 数据流确定性与生命周期 → `数据流-系统生命周期/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | World/System/SystemGroup 机制详解 + GAS 8-phase 设计 |
| `SYS-01: 权威计算落在 ECS System_Job 数据流，禁止托管 manager 驱动.md` | 规范 P0 | 权威计算落在 ECS System/Job，禁止托管 manager 驱动 |
| `SYS-02: SystemGroup 是 phase owner，禁止手写 Tick 顺序.md` | 规范 P0 | SystemGroup 是 phase owner，禁止手写 Tick 顺序 |
| `SYS-03: 系统数量是成本源，避免不必要的 system 拆分.md` | 规范 P1 | 系统数量是成本源，避免不必要的 system 拆分 |
| `SYS-04: Core_Physics_Presentation 成本分组统计.md` | 规范 P1 | Core/Physics/Presentation 成本分组统计 |
| `SYS-05: World 边界：Debugger_Demo_Presentation 只能通过 Boundary 观察 Core.md` | 规范 P1 | World 边界只读观察 |
| `PRF-07: 避免不必要的 System 拆分.md` | 规范 P1 | 避免不必要的 System 拆分 |
| `PRF-16: System 创建依赖用 CreateAfter；ISystem 优于 SystemBase.md` | 规范 P2 | CreateAfter + ISystem 优先 |
| `CASE-16: SystemGroup Allocator.md` | 模式 | SystemGroup Allocator |
| `CASE-17: ICustomBootstrap 多世界.md` | 模式 | ICustomBootstrap 多世界 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `SYS-01` | P0 | 权威计算落在 ECS System/Job 数据流 | 搜索 OnUpdate 中托管 manager 调用；统计 System/Job vs 托管执行占比 |
| `SYS-02` | P0 | SystemGroup 是 phase owner | 审计 OnUpdate 中的 .Update() 调用；确认 phase 首尾 SystemGroup 边界 |
| `SYS-03` | P1 | 系统数量是成本源 | Debugger 查看 ActiveSystemCount 分组统计 |
| `SYS-04` | P1 | 分组统计 Core/Physics/Presentation/Runner | 性能报告需包含四组开销分解 |
| `SYS-05` | P1 | Debugger/Demo/Presentation 只能只读观察 Core | 搜索跨 World 写入操作 |
| `PRF-07` | P1 | 避免不必要的 System 拆分 | 审计同 query 条件是否分布在多个 system |
| `PRF-16` | P2 | CreateAfter + ISystem 优先 | Code review 检查新 system 类型选择 |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `PRF-07` | Query-Job-遍历 | 同 query 多 system 拆分警告 |
| `PRF-16` | 数据流-系统生命周期 | System 创建依赖与数据流顺序 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| System 内部如何调度 job | `Query-Job-遍历/_index.md` |
| 数据流如何保证确定性 | `数据流-系统生命周期/_index.md` |
| ECB 在哪个 phase playback | `结构变化-ECB/_index.md` |
| Debugger 如何观察 Core | `Diagnostics-Debugger/_index.md` |

## 验收指标

1. Runtime Core 行动报告能说明新增/修改的 system 属于哪个 SystemGroup/phase
2. 性能报告至少拆分 Core/Physics/Presentation/Runner 四组成本
3. Debugger 能报告 active system count（按分组）、query count、lookup pressure
4. AutoChess 无头验收不隐式依赖 Editor frame delta
5. Runtime Core 所有新 system 为 ISystem 结构
6. 无手动调用其他 System 的 Update() 模式
7. Debugger/Demo/Presentation 层不对 Core component 写入
