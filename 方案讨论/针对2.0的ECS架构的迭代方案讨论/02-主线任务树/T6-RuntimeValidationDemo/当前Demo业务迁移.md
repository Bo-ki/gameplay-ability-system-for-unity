# Runtime Validation Demo - 当前 Demo 业务迁移

## 父节点

[T6 Runtime Validation Demo](README.md)

## 节点定位

本支线用于迁移当前 Demo 中移动、闪避、死亡、引导、UI 二次读取等真实业务病灶。它是 AutoChess 主线之后的业务对照支线。

## 当前问题

1. 当前 Demo 覆盖的是已有项目真实业务问题，但不适合作为当前 Runtime rebuild 的首要验收载体。
2. MonoBehaviour 输入、presentation output、UI 二次读取等边界需要在 Runtime Core 稳定后统一迁移。

## 目标态参考

1. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
2. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
3. `01-目标态架构共识/03-RuntimeCore管线Spec.md`

## 历史方案参考

1. `历史方案参考/方案12.md`、`方案13.md` 的当前 demo 真实业务预演可参考。
2. `方案14.md`、`方案15.md` 的 AutoChess 验收优先级高于本支线。

## 支线目标

在 AutoChess 无头验收稳定后，把当前 Demo 的输入、表现、死亡、引导、UI 读取等业务边界迁移到目标态 Runtime / Observation / Presentation 分层。

## 当前状态

后置。该支线用于迁移当前 Demo 中移动、闪避、死亡、引导、UI 二次读取等真实业务病灶，但不抢占 AutoChess 无头验收主线。

## 非目标

1. 不抢占 AutoChess 无头验收。
2. 不让 `MonoBehaviour` 输入或 presentation output 成为 simulation 权威。
3. 不让 UI 二次读取反向修正 gameplay state。

## 前置依赖

1. T1 Runtime Core 主链稳定。
2. T4 Observation / Presentation / Debugger 边界稳定。
3. T6 AutoChess 默认链路和 diagnostics gate 通过。

## 执行范围

1. `Assets/DemoForESC`
2. 当前迁移来源：`Assets/GAS/Runtime/Demo`
3. 目标 AutoChess 目录：`Assets/AutoChessDemo`
3. Runtime input bridge / presentation outbox / UI read model

## 执行细则

1. 输入只转换为 command / request，不直接改 simulation state。
2. 表现只消费 facts / presentation outbox。
3. UI 读取使用只读 projection，不反向写回 runtime。

## 验收门槛

1. 当前 Demo 关键业务链路完成目标态分层迁移。
2. AutoChess 验收不因当前 Demo 迁移退化。
3. 输入、表现、UI 读取边界有测试或日志证据。

## 测试链路

1. 当前 Demo PlayMode / Scene 验证。
2. AutoChess regression validation。
3. presentation marker / UI read projection 检查。

## 候选任务

| 任务ID | 任务名 | 状态 |
|---|---|---|
| T6-CurrentDemo-MoveDodge | Runtime Validation Demo - 当前 Demo 业务迁移 - 移动闪避输入与表现输出边界 | 后置 |

## 禁止

1. 不让 `MonoBehaviour` 输入或 presentation output 成为 simulation 权威。
2. 不让 UI 二次读取反向修正 gameplay state。
