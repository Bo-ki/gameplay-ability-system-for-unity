# EX Gameplay Ability System For Unity 2.0

更新时间：2026-05-23

EX-GAS 2.0 是一个基于 Unity DOTS / ECS 的 Gameplay Ability System 实现。当前主线不再是 1.x 的托管 OOP 运行模型，也不再以 `AbilitySystemCell`、`AbilityLogicBase`、`AbilitySpec`、`GameplayEffectSpec` 作为 Runtime 主入口。旧文档、旧 Demo 说明或历史方案若与本文冲突，以当前代码和本文口径为准。

一句话概括当前架构：

> Ability 产生意图，GameplayEffect 改变状态，Attribute / Tag 承载判定，Cue / Presentation 观察事实；Simulation 权威只存在于 ECS Entity / Component / System 中。

## 当前状态

- Unity 版本：`2022.3.62f3`，以 `ProjectSettings/ProjectVersion.txt` 为准。
- Runtime 主路径：`Assets/GAS/Runtime`。
- Editor / Authoring 工具：`Assets/GAS/Editor`。
- Wiki 文字页：`Assets/GAS/Wiki`。
- 配置源：`EX_GAS_Config/ProjectConfigTable/exgas_config/Datas`。
- 迭代讨论目录：`方案讨论/` 是本地中间产物，已通过 `.gitignore` 排除。

## 架构分层

当前 EX-GAS 2.0 按五个平面理解和维护：

| 平面 | 职责 | 主要入口 |
| --- | --- | --- |
| Authoring | 编辑器、表格、Schema、诊断 UI | `Assets/GAS/Editor`、GAS Center、Timeline editor、web editors |
| Definition | 配置定义、Registry、Blob、Generated adapter | `AbilityConfigRegistry`、`GameplayEffectConfigRegistry`、`GASDefinitionTable` |
| Simulation | 真正的玩法权威和状态变更 | ECS component、request entity、`GASSystemScheduleContract` 中的 system |
| Observation | 事实流、表现 outbox、replay、结构化日志 | `CGameplayEventBus`、`BPresentationEvent`、`BDebugReplayEvent` |
| Extension | 业务扩展、ExecutionCalculation、Demo driver | `GASExecutionCalculationExtensionGroup`、AutoChess systems |

核心不变量：

1. 外部写入只创建 request entity 或等价 command data。
2. 外部观察只读 ECS mirror、presentation outbox、replay 或 read model。
3. Definition 不携带 spec、context、runtime state。
4. Runtime state 不反查 managed authoring config。
5. Cue / UI / VFX / SFX / log / replay 不决定 gameplay。
6. 新增 runtime system 必须进入 `GASSystemScheduleContract`，并由调度契约测试锁住顺序。

## Runtime 主链

### GameObject 接入

GameObject 层只作为绑定壳：

- `AbilitySystemBinding` 在 `Awake` 创建 ASC Entity 对应的 `AbilitySystemFacade`。
- `AbilitySystemBinding.Init(AbilitySystemConfig)` 只创建初始化请求，实际写入由 ECS system 消费。
- `AbilitySystemBinding.Facade` 提供有限外部 API：创建 request、读取 observation、peek presentation outbox。
- GameObject 不承载 GAS 运行时权威状态。

### Facade 写入

`AbilitySystemFacade` 是非 ECS 代码进入 ECS 主链的轻量门面。它只持有 `Entity` 引用：

| 操作 | 当前写入口 | 消费系统 |
| --- | --- | --- |
| ASC 初始化 | `CAscInitializeRequest` | `SAscInitializeRequest` |
| ASC 命令 | `CAscCommandRequest` | `SAscCommandRequest` |
| Ability 命令 | `CAbilityCommandRequest` | `SAbilityCommandRequest` |
| GE 施加 | `CApplyGameplayEffectRequest` + target / SetByCaller buffers | `SApplyGameplayEffectRequest` |
| GE 移除 | `CRemoveGameplayEffectRequest` | `SRemoveGameplayEffectRequest` |
| ASC 销毁 | `CAscDestroyRequest` | `SAscDestroyRequest` |

便捷方法如 `TryActivateAbility`、`RequestGameplayEffectToSelf`、`AddFixedTag`、`SetAttrBaseValue` 仍存在，但它们的语义是“写 request”，不是立即修改玩法状态。

### Ability

当前 Ability 激活链路：

1. 外部或业务 system 创建 `CAbilityCommandRequest`。
2. `SAbilityCommandRequest` 找到 Ability entity 并写入 try marker。
3. `STryActivateAbility` 将 try marker 归一化为 `CAbilityCommitRequest`。
4. `SAbilityCommit` 统一检查 phase、activation tag requirement、cost、cooldown、active ability block relation。
5. 成功时输出 `AbilityCommitSucceeded` fact，并创建 cost / cooldown / activation GE request，添加 activation owned tags 和必要的 cancel request。
6. 失败时输出 `AbilityCommitFailed` fact，不写入 cost / cooldown / activation GE，也不进入 active phase。
7. End / Cancel 统一通过 `AbilityRuntimeActions.RequestAbilityEnd/Cancel` 写 lifecycle request 和 request fact。
8. `SAbilityStateCleanup` 负责最终收尾：清理 activation owned tags、Ability 创建的 GE、granted ability runtime 和 try / commit / timeline 组件，并输出最终 fact。

不要为新 Ability 增加托管生命周期对象。新增行为应表现为配置 schema、Ability component config、request / fact 和对应 ECS system。

### GameplayEffect

当前 GE 施加链路：

1. `CApplyGameplayEffectRequest` 承载 `SourceAsc`、`SourceAbility`、`SourceEffect`、`Instigator`、`Causer`、`GameplayEffectCode`、`Level`、`ParentContextId`、`DurationFrameOverride`。
2. 目标集合走 `CTargetDataHeader` + `BTargetEntity`，不塞进 request component。
3. SetByCaller 走 `BSetByCallerValue`，可挂在 request 或 GE runtime instance 上。
4. `SApplyGameplayEffectRequest` 消费 request 后创建 runtime GE entity，并写入 `CEffectContext`、`CEffectSpecData`、`CEffectLifecycle`。
5. `SEffectApply`、`SOngoingTagRequirements`、`SEffectTick`、`SEffectRemove` 推进 apply、inhibit、period、duration、stacking、remove 和 fact 输出。
6. Attribute 改动由 GE modifier / execution calculation 进入 `SAttributeRecalculate`，再投影成 attribute / damage facts。

`GEStaticDefinitionBlob` 和 registry summary 是 GE definition 的读取边界。Prototype / static Blob / generated bake contract 不保存 runtime spec、context、stack count、剩余 duration 或 Attribute current value。

### Observation

Runtime facts 与表现分层如下：

- `CGameplayEventBus`：统一 observation fact stream，保留 damage、tag、gameplay、attribute、cue request 等事实。
- typed fact buffer：高频业务 reaction 可使用更窄的 typed fact / cursor，不应长期扫描全局 `BGameplayEvent`。
- `BPresentationEvent`：current-frame presentation outbox，供 UI / VFX / SFX / FloatingText / Cue / settlement 等表现层读取。
- `BDebugReplayEvent`：replay / structured log 的只读事实来源。
- `GasStructuredLogView` / export：从 replay snapshot 派生人读或断言格式，不写回 simulation。

重要约束：不要跨结构变化持有 `DynamicBuffer<T>` 或 query buffer 视图。只要处理中会 `CreateEntity`、`AddComponent`、`RemoveComponent` 或 `DestroyEntity`，先 snapshot 要读的范围，再写世界。

## Definition 与配置链

配置链仍以 Excel / Luban / JSON / 生成代码为主：

```text
Excel 配置
  -> Luban 导表
  -> XLuban 生成代码与 JSON
  -> ConfigRegistry warmup
  -> GASDefinitionTable / Registry summary
  -> ECS runtime request / definition / Blob
```

常用命令：

```powershell
EX_GAS_Config\ProjectConfigTable\exgas_config\gen.bat
```

```bash
bash EX_GAS_Config/ProjectConfigTable/exgas_config/gen.sh
```

`BeanUpdater` 当前收集这些 Bean：

- `XParam` 参数类：通过 `[BeanField]` 和 `[BeanPolymorphicField]` 标注字段。
- `GameplayCueBase<T>`：表现层 Cue 逻辑。
- `AbilityExecutionBase`：由 `EditorAbilityHelper.GetAbilityExecutionSchemas()` 暴露的数据化 Ability 执行配置。
- `TimelineActionParameterBase`：由 `EditorAbilityHelper.GetTimelineActionParameterSchemas()` 暴露的 Timeline action 参数。
- `TargetCatcherBase<T>`：目标捕获参数。

不要再把 `AbilityLogicBase` / `AbilityTaskBase` 当作当前 Ability runtime 扩展入口。

## Generated / Bake / Runtime Integration

当前 generated pipeline 的目标是强化 Definition Plane，不生成 runtime lifecycle：

- `GASDefinitionGeneratedAdapter`：把 generated / Luban source 映射到 unified definition table。
- `GASGeneratedDefinitionBakingPlan`：描述 carrier / Baker input / static Blob 候选与 diagnostics gate。
- `GASGeneratedDefinitionBakeContract`：拆分 generated carrier、Unity Entities Baker input、GE static Blob cache、runtime archetype template 和 deferred boundary。
- `GASGeneratedDefinitionBakePipeline`：materialize contract artifact，并通过 `GameplayEffectConfigRegistry` warmup GE static definition Blob cache。
- `GASGeneratedDefinitionRuntimeIntegrationPlan`：把 bake result、runtime query layout plan、structural change plan 合并为 integration contract。

这些 contract 不暴露 `Entity`、`EntityManager`、`EntityQuery`、`BlobAssetReference`、Editor 类型、`XLuban/cfg/SimpleJSON`、spec/context/runtime state。

## Editor 与 Authoring

当前仓库仍保留若干 legacy Editor 工具，包括 GAS Center、Timeline editor、GASWatcher 和 web editors。Runtime 主链不依赖 Editor、GameObject、Odin attribute 或托管事件中心。后续 Editor / Authoring UI 应消费 Definition / Authoring snapshot / diagnostics，只能输出 definition patch 或配置源修改，不能反向成为 runtime lifecycle owner。

主要入口：

- `EXTool/EX-GAS/生成脚本/更新Bean定义`
- `EXTool/EX-GAS/生成脚本/GAS表配置`
- GAS Center / GameplayTag / Attribute / AttributeSet / GameplayEffect / GameplayCue / Ability / ASC 编辑页
- Runtime log / replay / watcher 类工具只读 observation

## Demo 与验证

当前 Demo 验证重点是 headless ECS Runtime，而不是旧 MonoBehaviour 业务框架。

- `Assets/GAS/Runtime/Demo/AutoBattle`：较小的无头战斗样板。
- `Assets/GAS/Runtime/Demo/AutoChess`：自走棋 Runtime 验收链，覆盖 generated definition source、Luban / SourceGenerator row contract、scale / determinism / performance、presentation outbox、typed facts、复杂 GE / Attribute / Tag / Ability 链路。
- `Assets/_Test/GAS/Runtime/AutoChess`：命令行验收入口与契约测试。

常用构建验证：

```powershell
dotnet build .\com.exhard.exgas.runtime.csproj --no-restore
dotnet build .\com.exhard.exgas.editor.csproj --no-restore
dotnet build .\com.exhard.exgas.runtime.tests.csproj --no-restore
```

Unity Test Runner：

```powershell
Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml
```

## 迁移口径

旧口径中常见的这些说法已经不再作为当前架构主线：

| 旧口径 | 当前口径 |
| --- | --- |
| ASC 是托管 `AbilitySystemCell` | ASC 权威是 Entity；GameObject 只通过 `AbilitySystemBinding` 绑定 |
| 外部直接调用托管对象修改状态 | 外部创建 request entity，ECS system 消费 |
| Ability 通过 `AbilityLogicBase` 回调执行 | Ability 行为数据化，ECS system 推进 |
| GE 通过 `GameplayEffectSpec` OOP 包装施加 | GE apply request 生成 runtime GE entity 和 spec/context component |
| Cue / log / event center 可驱动 gameplay | Cue / presentation / replay / log 只读派生，不决定 gameplay |
| 新系统靠自动发现进入调度 | 新系统必须进入 `GASSystemScheduleContract` |

## 文档索引

- [Assets/GAS/Wiki/EX-GAS.md](Assets/GAS/Wiki/EX-GAS.md)：Wiki 总览。
- [Assets/GAS/Wiki/Ability.md](Assets/GAS/Wiki/Ability.md)：Ability 当前生命周期。
- [Assets/GAS/Wiki/GameplayEffect.md](Assets/GAS/Wiki/GameplayEffect.md)：GE definition / spec / context / runtime instance。
- [Assets/GAS/Wiki/GameplayCue.md](Assets/GAS/Wiki/GameplayCue.md)：Cue / Presentation 边界。
- [BeanMappingSpec.md](BeanMappingSpec.md)：Bean / Luban / XParam 映射规范。
- [DemoFrameworkIntroduction.md](DemoFrameworkIntroduction.md)：当前 ECS Demo / 项目接入说明。

