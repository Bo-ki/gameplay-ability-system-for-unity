# EX-GAS Wiki

更新时间：2026-05-23

EX-GAS 2.0 是 Unity DOTS / ECS 版 Gameplay Ability System。它借鉴 UE GAS 的问题域和术语，但当前实现不是 UE GAS 的逐项移植，也不是 1.x OOP API 外面套一层 ECS。

当前一句话：

> Ability 产生意图，GameplayEffect 改变状态，Attribute / Tag 承载判定，Cue / Presentation 观察事实；Simulation 权威只在 ECS 中。

## 核心概念

| 概念 | 当前口径 |
| --- | --- |
| ASC | Ability System Component 的运行时实体。权威状态在 ASC Entity 上，GameObject 只通过 `AbilitySystemBinding` 绑定 |
| Ability | 可触发行为。配置被转换为 Ability Entity 的组件、Blob 和 runtime state，由 ECS system 推进 |
| GameplayEffect | 属性、Tag、Granted Ability、Cue、duration、period、stacking 的状态变更载体 |
| Attribute | 数值状态。初始化可由 ASC 初始化请求写入，运行时变化应由 GE / ExecutionCalculation 产生 |
| GameplayTag | 判定与状态标记。通过 TagMask / TagRequirement 参与 Ability、GE、Cue 条件 |
| GameplayCue | 表现层逻辑。可以保留托管表现对象，但不能写 gameplay state |
| Observation | facts、presentation outbox、replay、structured log，只读派生 |

## 当前工程分层

```text
Authoring
  Excel / editor / schema / diagnostics

Definition
  Registry / DefinitionTable / static Blob / generated adapter

Simulation
  ECS component / request entity / system / runtime state

Observation
  CGameplayEventBus / BPresentationEvent / BDebugReplayEvent / log export

Extension
  ExecutionCalculation / gameplay driver / demo-specific reaction
```

## 运行边界

外部代码进入 GAS 的方式：

1. GameObject 挂 `AbilitySystemBinding`。
2. 通过 `AbilitySystemFacade` 创建 request entity。
3. ECS system 消费 request 并修改 ECS runtime state。
4. 表现层读取 observation / presentation outbox。

不要绕过 request 直接修改 Attribute、Tag、GE 或 Ability runtime state。

## 配置边界

配置源仍是 Excel / Luban / JSON / generated code，但 generated pipeline 只生产 Definition Plane artifact：

- `BeanUpdater` 更新 `__beans__.xlsx`。
- `CodeGeneratorLubanPart` 生成 `XLuban` 扩展。
- `ConfigRegistryGraphValidator` warmup 配置图。
- `GASDefinitionTable` 汇总 Ability、GameplayEffect、AttributeSet、Attribute、GameplayTag、GameplayCue。
- Bake / runtime integration plan 只描述 carrier、Baker input、static Blob、runtime archetype template 和 deferred boundary，不生成 runtime lifecycle。

## 系统调度

`GASSystemScheduleContract` 是 Runtime 系统注册和顺序权威。新增 system 只写 `UpdateInGroup` 不够，必须进入 contract 数组，并补调度契约测试。

当前主要组：

1. `GASCommandGroup`
2. `GASResetDirtyGroup`
3. `GASTagGroup`
4. `GASEffectGroup`
5. `GASAttributeGroup`
6. `GASAbilityGroup`
7. `GASCueGroup`

## 不再推荐的旧口径

| 旧说法 | 当前替代 |
| --- | --- |
| `AbilitySystemCell` 是运行时主对象 | ASC Entity + `AbilitySystemFacade` |
| `AbilityLogicBase` 承载 Ability 生命周期 | AbilityExecution schema + ECS system |
| `GameplayEffectSpec` 是 GE 施加入口 | `CApplyGameplayEffectRequest` + runtime GE entity |
| `GASEventCenter` 推送 UI / gameplay | `CGameplayEventBus` + observation / outbox |
| Cue / log 可以驱动玩法 | Cue / log / replay 只读派生 |

## Wiki 页面

- [Ability](Ability.md)
- [GameplayEffect](GameplayEffect.md)
- [GameplayCue](GameplayCue.md)
