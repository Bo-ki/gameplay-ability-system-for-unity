# Runtime Validation Demo - AutoChess 无头验收 - AutoChessDemo 目录迁移与分层重构

## 父节点

[AutoChess 无头验收](README.md)

## 任务ID

`T6-AutoChess-DemoRefactor`

## 状态

`暂停`

## 当前问题

1. AutoChess 根目录已从 `Assets/GAS/Runtime/Demo/AutoChess` 迁移到 `Assets/AutoChessDemo`，Runtime Core 对 AutoChess 系统类型的静态依赖已拆除。
2. 当前实现已完成文件级分层，并已开始拆 `HeadlessAutoChessScenario` 的 constants、state、bootstrap、variants、unit definitions、unit resolution、runtime timing、runtime lifecycle、types；但 unit bootstrap、validation summary、event/outbox counting、generated rows 和 presentation marker projection 等内部职责尚未完全拆开。该内部拆分在 GAS Runtime 重构完成前暂停。
3. 历史方案 12/13/14/15 中已有可吸收的 Demo 业务样板，但尚未折算成当前 Demo 目录与任务规则。

## 目标态参考

1. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
2. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
3. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
4. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
5. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`

## 历史方案参考

1. `../../历史方案参考/方案12.md`、`方案13.md` 的真实业务 Demo 和配置生成链可作为非自走棋对照。
2. `../../历史方案参考/方案14.md`、`方案15.md` 的自走棋业务闭环、Luban 配置、Debugger workflow、四层架构优先吸收。

## 目标 / 目的

1. 建立 `Assets/AutoChessDemo` 目标目录和分层规范。已建立根目录、asmdef、README 和最小 runtime system bootstrap。
2. 将 Config / Generated / Simulation / Observation / Presentation / Validation / Debugging 从根目录平铺结构拆出。已完成文件级拆分。
3. 将 Config / Generated / Simulation / Observation / Presentation / Validation / Debugging 从巨类内部进一步拆出。`HeadlessAutoChessScenario` 的 constants / state / bootstrap / variants / unit definitions / unit resolution / runtime timing / runtime lifecycle / types 已拆出；unit bootstrap / report builder / event-outbox counting / generated rows 后置到 Runtime Core 重构之后。
4. 保持无头自动验收和 SceneRuntime 验收链路可运行。
5. 保留真实 Demo 资源接入结构，默认只使用 log adapter。

## 非目标

1. 不在本任务接真实美术资源。
2. 不修改 GAS Runtime Core 概念语义。
3. 不新增业务机制掩盖 Runtime Core 性能问题。

## 执行范围

1. `Assets/AutoChessDemo`
2. `Assets/AutoChessDemo/Config`
3. `Assets/AutoChessDemo/Simulation`
4. `Assets/AutoChessDemo/Observation`
5. `Assets/AutoChessDemo/Presentation`
6. `Assets/AutoChessDemo/Validation`
7. `Assets/AutoChessDemo/Debugging`
8. 原迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
9. AutoChess asmdef、tests、validation runner、summary export。

## 执行细则

1. 先建目标目录、asmdef、README 和最小 bootstrap，再迁移业务。本轮已完成。
2. Demo 业务只能依赖 Runtime Core public contract，不允许 Runtime Core 反向引用 Demo。本轮已完成静态拆除，待 Unity 编译验证。
3. 表现 marker 迁移到 Presentation 层，Debugger / Validation 迁移到独立层。
4. Luban / SourceGenerator 相关内容迁到 Config 层，不再把大量 generated rows 留在场景巨类。
5. 默认业务链路保持精简，迁移时优先保证核心链路完整，不把所有历史机制一次性搬入默认场景。
6. 文件级归位已完成；巨类内部拆分已开始但当前暂停，不把 partial 化等同于架构完成。

## 验收标准

1. `Assets/AutoChessDemo` 成为 AutoChess 新增代码唯一入口。已完成根目录迁移。
2. `Assets/GAS/Runtime/Demo/AutoChess` 不再承接新增业务。已完成。
3. Runtime Core 不再静态引用 AutoChessDemo 系统。已完成静态扫描验证。
4. AutoChessDemo 目录形成 Config / Simulation / Observation / Presentation / Validation / Debugging 第一层边界。已完成。
5. 默认 headless validation 与 SceneRuntime runner 仍能输出 summary。待 Unity license 恢复后补验证。
6. `HeadlessAutoChessScenario` 内部至少拆出 constants / state / bootstrap / variants / unit definitions / unit resolution / runtime timing / runtime lifecycle / public DTO/timing types，且 `RunDefault/RunVariant` 外部 API 不变。已完成静态拆分，待 Unity 编译验证。
7. 文档和任务树同步记录迁移状态。已更新。

## 测试链路

1. AutoChess 默认 validation。
2. SceneRuntime runner。
3. 若改代码，运行受影响 runtime tests。

## 交还内容

1. 更新当前架构事实中的 Demo 目录事实。
2. 更新本任务状态和最近验证摘要。
3. 必要时新增迁移迭代记录。
