# ISSUE-006 AutoChessDemo 边界混入 Runtime Core

> 最近复核：2026-06-02 | 状态：Mitigated | 严重度：P1

## 当前结论

旧问题已明显缓解。AutoChessDemo 当前位于 `Assets/AutoChessDemo`，业务层、GAS adapter、Demo ECS 扩展、Presentation 已拆开；Runtime Core 不再承载旧 Headless 业务系统。

当前剩余风险不是“Demo 混入 Runtime Core”，而是 `AutoChessGasCoreBridge` 内部仍直接触碰 `GASManager.EntityManager`，需要继续收口为明确 adapter/command owner。

## 已缓解部分

1. `GameRoom` / `Battle` / `Integration/GasCore` / `AutoRunner` / `Presentation` 分层成立。
2. Demo ECS systems 通过 `AutoChessRuntimeSystemBootstrap` 插入当前 GAS groups。
3. Presentation 只消费 battle log，不回读 ECS World。
4. Runtime Core 未引用 AutoChess 业务类型。

## 仍成立风险

1. `AutoChessGasCoreBridge` 直接创建/销毁 battle driver、ASC、ability、active effect。
2. Demo catalog 由代码安装最小 blob，不是配置驱动链。
3. Demo ECS systems 仍是主线程 query/loop，不是高规模 DOTS 证明。

## 代码证据

| 事实 | 文件 |
|---|---|
| demo README 边界 | `Assets/AutoChessDemo/README.md` |
| bridge | `AutoChessGasCoreBridge.cs` |
| system bootstrap | `AutoChessRuntimeSystemBootstrap.cs` |
| command drive | `AutoChessBattleCommandDriveSystem.cs` |
| execution extension | `AutoChessExecuteDamageCalculationSystem.cs` |
| log-only presentation | `AutoChessDemoSceneRunner.cs` |

## 退出条件

1. bridge 内结构变化分类清晰：初始化、session lifecycle、hot path。
2. battle unit create/destroy 进入 request/command owner 或明确 session-owned lifecycle。
3. Demo 系统的 query/dependency/ordering 有独立审查。
