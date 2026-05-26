# UnityPhysics

## 职责边界

本领域维护 Unity Physics 在 EX-GAS 中的边界：Physics pipeline 时序约束、CollisionWorld 查询规范、Collision/Trigger 事件处理、Physics 与 GAS 规则裁决的分离。Physics 只能作为 GAS 的输入数据源，不能替代 GAS 规则裁决。不覆盖 Physics authoring workflow、joint 和约束系统、character controller 等 GAS 不使用的子领域。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Physics pipeline 时序、CollisionWorld 查询、CollisionFilter 配置）
2. **核心规范**（按严重度）
   - `PHY-01: Physics pipeline 内禁止结构变化.md` — P0: Physics pipeline 内禁止结构变化
   - `PHY-02: Physics 只能作为 GAS 输入数据源，不能替代 GAS 规则裁决.md` — P0: Physics 只能作为 GAS 输入数据源，不能替代 GAS 规则裁决
   - `PHY-03: Collision_Trigger Events 不能跨 frame 保存.md` — P1: Collision/Trigger Events 不能跨 frame 保存
   - `PHY-04: Physics 查询必须声明 broadphase 时效性.md` — P1: Physics 查询必须声明 broadphase 时效性
   - `PHY-05: Physics 与 Core 成本必须分离统计.md` — P1: Physics 与 Core 成本必须分离统计
3. **模式与案例**
   - `CASE-09: Physics query 用于目标获取.md` — Physics query 用于目标获取
4. **拓展阅读**（按需）
   - Entities Graphics 渲染 → `EntitiesGraphics/_index.md`
   - 数据流确定性 → `数据流-系统生命周期/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | Physics pipeline、CollisionWorld 查询、CollisionFilter 配置详解 |
| `PHY-01: Physics pipeline 内禁止结构变化.md` | 规范 P0 | Physics pipeline 内禁止结构变化 |
| `PHY-02: Physics 只能作为 GAS 输入数据源，不能替代 GAS 规则裁决.md` | 规范 P0 | Physics 只能作为 GAS 输入数据源 |
| `PHY-03: Collision_Trigger Events 不能跨 frame 保存.md` | 规范 P1 | Collision/Trigger Events 不能跨 frame 保存 |
| `PHY-04: Physics 查询必须声明 broadphase 时效性.md` | 规范 P1 | Physics 查询必须声明 broadphase 时效性 |
| `PHY-05: Physics 与 Core 成本必须分离统计.md` | 规范 P1 | Physics 与 Core 成本必须分离统计 |
| `CASE-09: Physics query 用于目标获取.md` | 模式 | Physics query 用于目标获取 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `PHY-01` | P0 | Physics pipeline 内禁止结构变化 | 搜索 `CreateEntity`/`DestroyEntity` 在 `PhysicsSystemGroup` 标记的 system 中 |
| `PHY-02` | P0 | Physics 只能作为 GAS 输入数据源 | Code Review 确认 Physics system 不直接读写 GAS Core component |
| `PHY-03` | P1 | Collision/Trigger Events 不能跨 frame 保存 | 搜索 `SimulationSingleton` 存储为 system 字段或 component 字段 |
| `PHY-04` | P1 | Physics 查询必须声明 broadphase 时效性 | 搜索 `OverlapAabb`/`Raycast`/`ColliderCast` 调用点的 broadphase 注释 |
| `PHY-05` | P1 | Physics 与 Core 成本必须分离统计 | 确认 physicsTime 与 coreTime 使用独立计时器 |

## 跨主题引用

| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `PHY-02` | Diagnostics-Debugger | 性能报告 cost 拆分 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| 渲染层与 Physics 表现 | `EntitiesGraphics/_index.md` |
| 数据流确定性与生命周期 | `数据流-系统生命周期/_index.md` |

## 验收指标

1. Debugger 输出 physics step count、query count、event count、broadphase policy。
2. Range/hit 任务能复现 deterministic target result（相同输入 -> 相同目标列表）。
3. 压测报告拆出 Core 与 Physics 两类成本（`coreTickMs` / `physicsStepMs` 独立）。
4. Headless profile 输出 Physics disabled reason。
5. 零 Physics system 直接修改 GAS Core component。
