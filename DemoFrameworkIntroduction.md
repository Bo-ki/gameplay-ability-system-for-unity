# EX-GAS 2.0 Demo 与项目接入说明

更新时间：2026-05-23

本文替代旧版 `UnitBase + AbilitySystemCell + GASEventCenter` Demo 框架说明。当前 EX-GAS 2.0 的 Demo 和项目接入口径以 ECS Runtime 为准：GameObject 可以作为输入、表现和生命周期绑定壳，但玩法权威必须留在 ECS Entity / Component / System。

## 接入目标

推荐把项目层拆成四类代码：

| 层 | 可以做什么 | 不应该做什么 |
| --- | --- | --- |
| GameObject Shell | 挂 `AbilitySystemBinding`，接收输入，绑定表现对象 | 保存 GAS 权威状态 |
| Facade / Adapter | 调用 `AbilitySystemFacade` 创建 request，读取 `AbilitySystemObservation` | 直接改 Attribute、Tag、GE、Ability runtime state |
| ECS Gameplay Core | 实现 Ability / GE / Attribute / Tag / reaction / driver system | 依赖 GameObject、Editor、Odin 或托管事件中心 |
| Presentation | 读取 `BPresentationEvent`、Cue request、replay / log snapshot | 反向驱动 simulation 判定 |

## 初始化顺序

典型项目启动顺序：

1. `GASManager.Initialize()` 创建 EX-GAS World、系统组、全局计时器和 event bus。
2. `XLuban.Init(loader)` 或等价配置入口加载表数据，并注册 Ability / GameplayEffect / Cue / Timeline registry provider。
3. 调用 `GameplayEffectConfigRegistry.ReloadDefinitionCaches(...)` 或由注册入口触发 registry warmup。
4. `GASManager.Run()` 允许 EX-GAS World 随 PlayerLoop 推进。
5. 场景对象通过 `AbilitySystemBinding.Init(config)` 创建 ASC 初始化 request。
6. 输入、AI 或业务 driver 创建 Ability / GE / ASC request。
7. UI / 表现层读取 observation / presentation outbox / replay snapshot。
8. 场景或游戏退出时通过 facade / binding 发起 ASC destroy；全局停服时再 `GASManager.Stop()`。

`GASManager.Run()` 只是运行开关，不代表外部可以绕过 request 直接改状态。

## ASC 接入

最小 GameObject 绑定：

```csharp
using GAS.Runtime;
using UnityEngine;

public sealed class PlayerGasBinding : MonoBehaviour
{
    [SerializeField] private AbilitySystemConfig config;
    private AbilitySystemBinding binding;

    private void Awake()
    {
        binding = GetComponent<AbilitySystemBinding>();
        binding.Init(config);
    }

    public void Activate(int abilityCode)
    {
        binding.Facade.TryActivateAbility(abilityCode);
    }
}
```

注意：

- `TryActivateAbility` 返回的是 request entity，不是“立即激活完成”。
- `SetAttrBaseValue`、`AddFixedTag`、`RequestGameplayEffectToSelf` 也是写 request。
- 读取当前值请走 `binding.Facade.Observation` 或 facade 的只读便捷方法。

## Ability 输入

玩家输入或 AI 决策只应产生命令：

```csharp
public void OnAttack(AbilitySystemFacade self, AbilitySystemFacade target, int attackAbility)
{
    self.TryActivateAbility(attackAbility, target);
}
```

复杂玩法不要写成 MonoBehaviour 回调链。推荐方式：

1. 配置层新增 AbilityExecution schema。
2. `CodeGeneratorLubanPart` 把表格执行配置映射为 `AbilityComponentConfig`。
3. Ability entity 上装载对应 ECS component / buffer。
4. 专用 `ISystem` 在明确 system group 中读取组件、状态和 facts，输出 request / fact。
5. 新 system 进入 `GASSystemScheduleContract`。

## GameplayEffect 输入

GE 施加使用 facade 或 system writer：

```csharp
self.RequestGameplayEffectTo(effectCode, target, level: 1);
```

带 SetByCaller：

```csharp
var values = new[]
{
    new BSetByCallerValue { Key = DamageKeys.Amount, Value = 25f },
};
self.RequestGameplayEffectTo(effectCode, target, values, level: 1);
```

在 ECS system 内推荐使用 `GameplayEffectRequestWriter`，并显式写 `CTargetDataHeader`、`BTargetEntity` 和 `BSetByCallerValue`。不要直接修改目标 Attribute，也不要手动伪造 Attribute change fact。

## UI 与表现

HUD / 表现层读取三类数据：

| 需求 | 当前入口 |
| --- | --- |
| 当前属性、Tag、Ability active 状态 | `AbilitySystemObservation` |
| 当帧 UI/VFX/SFX/FloatingText/Cue 事件 | `PeekPresentationEvents(...)` / `BPresentationEvent` |
| 调试、回放、日志、测试断言 | `BDebugReplayEvent` / `GasStructuredLogExport` |

旧的 `GASEventCenter.RegisterOnAttrCurrentValueChangeAfter(...)` 不再是当前推荐入口。属性变化会投影为 ECS fact，再进入 observation / replay / presentation。

## 当前 Demo 路线

### Headless AutoBattle

`Assets/GAS/Runtime/Demo/AutoBattle` 是较小的无头样板，用于验证 Ability / GE / Attribute / Cue 的基础链路。

### Headless AutoChess

`Assets/GAS/Runtime/Demo/AutoChess` 是当前主要验收 Demo。它覆盖：

- generated definition source / package / export / toolchain / real Luban process gate。
- Ability、GE、Attribute、Tag、Cue、Timeline definition 到 runtime provider。
- 多局 scale / determinism / performance validation。
- Shield、Summon、Damage Type、Counter、Cleanse、Rally、LifeSteal、Poison、Execute、Death Burst、Enrage 等业务链。
- typed facts、presentation outbox、structured replay / log。
- 真实 Editor Scene runtime 与 batchmode validation。

该 Demo 的重点不是“如何写一个 MonoBehaviour 战斗框架”，而是证明复杂玩法可以在 ECS request / fact / presentation outbox 单向链路中闭合。

## 验证建议

常规 C# 编译：

```powershell
dotnet build .\com.exhard.exgas.runtime.csproj --no-restore
dotnet build .\com.exhard.exgas.editor.csproj --no-restore
dotnet build .\com.exhard.exgas.runtime.tests.csproj --no-restore
```

AutoChess 命令行验收入口位于 `Assets/_Test/GAS/Runtime/AutoChess`，可按测试类中的静态入口在 Unity batchmode 中运行。

## 迁移检查清单

迁移旧 Demo 或项目代码时逐项检查：

1. 是否还在保存 `AbilitySystemCell`、`AbilitySpec`、`GameplayEffectSpec` 等旧对象引用。
2. 是否在 MonoBehaviour 中直接修改 Attribute / Tag / GE / Ability 状态。
3. 是否把 Cue / replay / log / UI 事件当成 gameplay 输入。
4. 是否在 ECS system 中边读 `DynamicBuffer<T>` 边做结构变化。
5. 新 system 是否已经加入 `GASSystemScheduleContract`。
6. 新配置是否通过 Bean / Luban / registry / definition table 进入 runtime。
7. 测试是否能证明 request、runtime state、fact、presentation outbox 全链路闭合。
