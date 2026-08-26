# EX-GAS Wiki

更新时间：2026-05-23

> 迁移提示：本文早期章节保留了旧 Runtime 的术语和链路，仅用于历史参考；旧 facade、request entity、system group、EventBus 与生成 catalog 已删除。当前唯一运行时入口是 `Assets/GAS/Runtime/V1`：`GasRuntimeWorldOwner`、`GasCommandPort`、`GasTickDag`、`GasBoundaryDrainCoordinator`。新代码不得按下文旧类型接入。

EX-GAS 2.0 是 Unity DOTS / ECS 版 Gameplay Ability System。它借鉴 UE GAS 的问题域和术语，但当前实现不是 UE GAS 的逐项移植，也不是 1.x OOP API 外面套一层 ECS。

当前一句话：

> Ability 产生意图，GameplayEffect 改变状态，Attribute / Tag 承载判定，Cue / Presentation 观察事实；Simulation 权威只在 ECS 中。

## 核心概念

| 概念 | 当前口径 |
| --- | --- |
| ASC | `GasRuntimeWorldOwner` 管理的稳定身份与 owner-local slot；外部不持有 Entity 或全局 facade |
| Ability | V1 immutable definition、owner-local activation/continuation slab 与 command/fact 事务，由 `GasTickDag` 推进 |
| GameplayEffect | 由 V1 target transaction 产生的 Attribute、Tag、Granted Ability、Cue、duration、period、stacking 状态变更 |
| Attribute | 由 target-owned transaction 维护的数值状态；运行时变化只能由 GE / ExecutionCalculation 链路产生 |
| GameplayTag | 由 target-owned tag transaction 维护的判定与状态标记，按 catalog requirement program 参与条件判断 |
| GameplayCue | 表现层逻辑。可以保留托管表现对象，但不能写 gameplay state |
| Observation | Boundary facts、read model、diagnostics evidence、replay、structured log 的只读派生 |

## 当前工程分层（Runtime v1）

```text
Authoring
  Excel / editor / schema / diagnostics

Definition
  Luban normalized rows / immutable catalog / blob schema

Simulation
  Runtime v1 owner / command lane / tick kernel / transactions

Observation
  Boundary facts / read model / diagnostics evidence / log export

Extension
  ExecutionCalculation / gameplay driver / demo-specific reaction
```

## 运行边界

外部代码进入 GAS 的方式：

1. Application / Demo 持有 `GasRuntimeWorldOwner` 暴露的稳定 session/ASC handle。
2. 通过 `GasCommandPort` 提交带 tick、epoch 和 source identity 的 intent。
3. `GasTickDag` 在固定阶段消费 command，并写入 owner-local Runtime v1 state 与 facts。
4. `GasBoundaryDrainCoordinator` 投影只读 snapshot，表现层只消费 snapshot / evidence。

不要绕过 `GasCommandPort` 直接修改 Attribute、Tag、GE 或 Ability runtime state。

## 配置边界

配置源仍是 Excel / Luban / JSON / generated code，但 generated pipeline 只生产 Definition Plane artifact：

- `BeanUpdater` 更新 `__beans__.xlsx`。
- CodeGen 生成 Luban normalized rows、Editor manifest 和验证报告，不生成 Runtime lifecycle / query / ECB owner。
- `GasDefinitionCatalogBlob` 与 `GasDefinitionCatalogSchema` 提供 Runtime v1 的 immutable definition 边界。
- `GasRuntimeWorldOwner` 在安装阶段校验并持有 catalog；运行时只通过 owner-local lookup 读取定义。

## 系统调度（旧流程，仅作迁移对照）

旧版由 `GASSystemScheduleContract` 维护系统注册和顺序；Runtime v1 改由 `GasTickDag` 维护，新增阶段必须更新 DAG 并补验证。

旧版主要组：

1. `GASCommandGroup`
2. `GASResetDirtyGroup`
3. `GASTagGroup`
4. `GASEffectGroup`
5. `GASAttributeGroup`
6. `GASAbilityGroup`
7. `GASCueGroup`

## 不再推荐的旧口径（历史附录）

以下表格只用于迁移旧项目，不代表当前 API 或运行时入口。

| 旧说法 | 当前替代 |
| --- | --- |
| `AbilitySystemCell` 是运行时主对象 | `GasRuntimeWorldOwner` + stable ASC handle |
| `AbilityLogicBase` 承载 Ability 生命周期 | V1 ability definition + owner transaction + `GasTickDag` |
| `GameplayEffectSpec` 是 GE 施加入口 | `GasCommandPort` + target-owned effect transaction |
| `GASEventCenter` 推送 UI / gameplay | Boundary facts + `GasBoundaryDrainCoordinator` read model |
| Cue / log 可以驱动玩法 | Cue / log / replay 只读派生 |

## Wiki 页面

- [Ability](Ability.md)
- [GameplayEffect](GameplayEffect.md)
- [GameplayCue](GameplayCue.md)
