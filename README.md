# EX Gameplay Ability System For Unity 2.0

更新时间：2026-05-26

EX-GAS 2.0 是一个基于 Unity DOTS / ECS 的 Gameplay Ability System 实现。当前主线不再是 1.x 的托管 OOP 运行模型，也不再以 `AbilitySystemCell`、`AbilityLogicBase`、`AbilitySpec`、`GameplayEffectSpec` 作为 Runtime 主入口。旧文档、旧 Demo 说明或历史方案若与本文冲突，以当前代码和本文口径为准。

重要警告：

> 当前仓库仍处于快速迭代和架构重构阶段，不是稳定可直接商用的 GAS Runtime。现有实现已经暴露出明显的 Runtime pipeline、性能曲线、结构变化、Observation 分层和 Debugger 能力问题。本文主要用于同步当前设计方向和工程思路，代码仅供参考，不建议把当前实现当成完成态框架直接接入生产项目。

一句话概括当前架构：

> Ability 产生意图，GameplayEffect 改变状态，Attribute / Tag 承载判定，Cue / Presentation 观察事实；Simulation 权威只存在于 ECS Entity / Component / System 中。

## 当前状态

- Unity 版本：`2022.3.62f3`，以 `ProjectSettings/ProjectVersion.txt` 为准。
- Runtime 主路径：`Assets/GAS/Runtime`。
- Editor / Authoring 工具：`Assets/GAS/Editor`。
- Wiki 文字页：`Assets/GAS/Wiki`。
- 配置源：`EX_GAS_Config/ProjectConfigTable/exgas_config/Datas`。
- 迭代讨论：`方案讨论/针对2.0的ECS架构的迭代方案讨论/`，详见下方文档索引。
- 不要修改 `Library/PackageCache` 下的包缓存内容；Unity 会自动还原，这类改动不是有效工程修复。

## 架构分层

当前 EX-GAS 2.0 按四层工程架构理解：

| 层 | 职责 | 边界 |
| --- | --- | --- |
| Application Shell | UI / Input / AI / Network / Demo runner | 不持有 runtime 权威 |
| Runtime Boundary | command gateway、read model、presentation outbox、diagnostics | 只读派生，不反向喂给 Core |
| GAS Runtime Core | ASC / Ability / GE / Attribute / Tag entities | 零 GameObject / managed callback |
| Definition & Generation | Luban / Excel / Bean schema / generated ids / bake plan | 不生成 gameplay lifecycle |

核心不变量：

1. 外部写入只创建 request entity 或等价 command data。
2. 外部观察只读 ECS mirror、presentation outbox、replay 或 read model。
3. Definition 不携带 spec、context、runtime state。
4. Runtime state 不反查 managed authoring config。
5. Cue / UI / VFX / SFX / log / replay 不决定 gameplay。
6. 新增 runtime system 必须进入 `GASSystemScheduleContract`。

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
| GE 施加 | `BEffectCommand` (via `GameplayEffectRequestWriter.TryAppendSimpleInstantCommand`) | `SEffectCommandIngest` → Spec → Delta → Fact |
| GE 移除 | 当前 no-op（旧管线已删除，新管线 ActiveEffectStore 待 AM-5 实现） | — |
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

当前 GE 施加链路（EffectCommandSpecStream pipeline，AM-2/AM-3 阶段完成）：

1. `GameplayEffectRequestWriter.TryAppendSimpleInstantCommand` 将 GE 请求转为 `BEffectCommand`，写入 singleton stream entity 的 `DynamicBuffer<BEffectCommand>`。
2. `SEffectCommandIngest` 消费 `BEffectCommand`，展开为 `BInstantEffectSpec`（instant modifier）或 `BActiveEffectMutation`（duration/stack/period，待 AM-5 实现）。
3. `SEffectSpecEvaluation` 对 `BInstantEffectSpec` 执行 modifier magnitude 计算，输出 `BAttributeDelta` 到 `SAttributeRecalculate`。
4. `SEffectTypedFactProjection` 从 `BAttributeDelta` 投影为 `BTypedSimulationFact`（attribute changed / damage resolved / unit defeated 等 typed fact）。
5. 结构变化（entity create/destroy/add/remove component）统一在 `SEffectStructuralPlayback` 中通过 ECB playback 执行。

关键架构决策：

- Instant GE 不再创建 runtime GE entity，全程走 command → spec → delta → fact 流。
- 零 `ToEntityArray`，零碎片化 ECB，全部使用 cursor 驱动的索引 for 循环。
- `GameplayEffectRuntimePipelineContract` 定义 pipeline kind/status/restrictions，合约层保证调度顺序。
- 旧 `CApplyGameplayEffectRequest` IComponentData 已降级为数据载体 struct，不再直接产生 entity。
- `SApplyGameplayEffectRequest` / `SEffectApply` / `SOngoingTagRequirements` / `SRemoveGameplayEffectRequest` 已删除。
- `SEffectTick` / `SEffectRemove` / `SEffectFinalDestroy` / `SExecutionCalculation` 已桩化为空 ISystem，待 AM-5 重新实现。
- Duration / Period / Stacking 效果暂不可用，待 AM-5 ActiveEffectStore 重建。

`GEStaticDefinitionBlob` 和 registry summary 是 GE definition 的读取边界。Prototype / static Blob / generated bake contract 不保存 runtime spec、context、stack count、剩余 duration 或 Attribute current value。

### Observation

Runtime facts 与表现分层如下：

- `CGameplayEventBus`：统一 observation fact stream，保留 damage、tag、gameplay、attribute、cue request 等事实。
- typed fact buffer：高频业务 reaction 可使用更窄的 typed fact / cursor，不应长期扫描全局 `BGameplayEvent`。
- `BPresentationEvent`：current-frame presentation outbox，供 UI / VFX / SFX / FloatingText / Cue / settlement 等表现层读取。
- `BDebugReplayEvent`：replay / structured log 的只读事实来源。
- `GasStructuredLogView` / export：从 replay snapshot 派生人读或断言格式，不写回 simulation。

重要约束：不要跨结构变化持有 `DynamicBuffer<T>` 或 query buffer 视图。只要处理中会 `CreateEntity`、`AddComponent`、`RemoveComponent` 或 `DestroyEntity`，先 snapshot 要读的范围，再写世界。

## 当前已知问题与重构路线

最新 x50 profile 事实和架构诊断见 [00-当前架构事实](方案讨论/针对2.0的ECS架构的迭代方案讨论/00-当前架构事实/README.md)。关键数据点：

- 仅 2/39 System 使用 IJobEntity，22 文件使用 ToEntityArray，24 文件直接 EM 结构变化
- 15+ ECB PlaybackAndReset 反模式
- 8-phase 管线契约已定义但 System 仍按旧 Group 名称注册

已修复的核心问题：

1. ~~Instant GE 走 request entity → runtime GE entity → lifecycle → destroy 链路~~ → 已修复：全程 command → spec → delta → fact 流
2. ~~旧 GE lifecycle pipeline（~7000 行）~~ → 已删除
3. ~~AutoChessDemo 零 DOTS 合规~~ → 已破坏性删除 24 个文件，待按 Spec 10/10B 重构

待解决的核心问题（详见 [ISSUE 看板](方案讨论/针对2.0的ECS架构的迭代方案讨论/00-当前架构事实/README.md)）：

- P0：代码执行范式未切换到 DOTS（全主线程 foreach + EntityManager）
- P0：临时 EntityQuery 泛滥与 API 承载选型错误
- P1：Runtime Core Frame Backbone 缺失
- P1：Observation 与 Runtime Core 热路径耦合

当前主线任务树进度（详见 [主线任务树](方案讨论/针对2.0的ECS架构的迭代方案讨论/02-主线任务树/README.md)）：

| 阶段 | 状态 |
| --- | --- |
| AM-0 Freeze / Safety Gate — 冻结并删除旧 GE lifecycle pipeline | 已完成 |
| AM-1 Runtime Core Debugger Baseline | 契约已确立 |
| AM-2 EffectCommand / SpecStream 契约 | 已完成 |
| AM-2B Frame Backbone (A~F) — phase / arena / stream / playback / debugger | 契约已确立 |
| AM-3 Instant Spec Evaluation — simple instant GE 不创建 runtime GE entity | 已完成 |
| AM-5 Active Effect Store — duration/stack/period/granted state 存储重建 | 进行中 |
| T0 文档治理与目标态共识 — Unity DOTS 官方文档全覆盖 | 已完成 |
| T6 AutoChess 无头验收 — 旧 Demo 已破坏性删除，待按 Spec 10/10B 重构 | 暂停 |

当前代码基线：AM-0~3 已完成，AM-5 为当前推进目标。

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

自动生成输出只作为 Definition Plane artifact。`Assets/DataGenerated/Luban/` 等生成目录不应作为手写 Runtime 源码维护，也不应为了临时编译问题把生成物或 PackageCache 内容纳入主线修复。

## Editor 与 Authoring

当前仓库仍保留若干 legacy Editor 工具，包括 GAS Center、Timeline editor、GASWatcher 和 web editors。Runtime 主链不依赖 Editor、GameObject、Odin attribute 或托管事件中心。后续 Editor / Authoring UI 应消费 Definition / Authoring snapshot / diagnostics，只能输出 definition patch 或配置源修改，不能反向成为 runtime lifecycle owner。

主要入口：

- `EXTool/EX-GAS/生成脚本/更新Bean定义`
- `EXTool/EX-GAS/生成脚本/GAS表配置`
- GAS Center / GameplayTag / Attribute / AttributeSet / GameplayEffect / GameplayCue / Ability / ASC 编辑页
- Runtime log / replay / watcher 类工具只读 observation

## Demo 与验证

当前 Demo 验证重点是 headless ECS Runtime，而不是旧 MonoBehaviour 业务框架。

- `Assets/GAS/Runtime/Demo/AutoBattle`：较小的无头战斗样板。当前计算系统（`SExecutionCalculation`、`SHeadlessAutoBattleExecuteCalculation`）已桩化，自动战斗伤害暂不可用，待 AM-5 重新实现。
- `Assets/GAS/Runtime/Demo/AutoChess`：自走棋 Runtime 验收链。AutoChess 业务 reaction / scenario 系统文件已在 `4193d414` 清理中删除（保留 11 个设计良好的模块待重建），对应测试文件已在 AM-0 阶段清理。
- `Assets/_Test/GAS/Runtime/AutoChess`：已删除，AutoChess 测试待 Demo 重建后恢复。

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
| GE 通过 `GameplayEffectSpec` OOP 包装施加 | Instant GE 走 BEffectCommand → Spec → Delta → Fact 流，不创建 runtime GE entity |
| Cue / log / event center 可驱动 gameplay | Cue / presentation / replay / log 只读派生，不决定 gameplay |
| 新系统靠自动发现进入调度 | 新系统必须进入 `GASSystemScheduleContract` |

## 文档索引

**项目文档**：
- [Assets/GAS/Wiki/EX-GAS.md](Assets/GAS/Wiki/EX-GAS.md)：Wiki 总览
- [Assets/GAS/Wiki/Ability.md](Assets/GAS/Wiki/Ability.md)：Ability 当前生命周期
- [Assets/GAS/Wiki/GameplayEffect.md](Assets/GAS/Wiki/GameplayEffect.md)：GE definition / spec / context / runtime instance
- [Assets/GAS/Wiki/GameplayCue.md](Assets/GAS/Wiki/GameplayCue.md)：Cue / Presentation 边界
- [BeanMappingSpec.md](BeanMappingSpec.md)：Bean / Luban / XParam 映射规范
- [DemoFrameworkIntroduction.md](DemoFrameworkIntroduction.md)：ECS Demo / 项目接入说明

**2.0 ECS 架构迭代讨论**：
- [方案讨论/针对2.0的ECS架构的迭代方案讨论/README.md](方案讨论/针对2.0的ECS架构的迭代方案讨论/README.md)：迭代讨论总入口
- [00-当前架构事实](方案讨论/针对2.0的ECS架构的迭代方案讨论/00-当前架构事实/README.md)：当前代码事实、核心问题诊断、合规缺陷
- [01-目标态架构共识](方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/README.md)：目标态 Spec、架构契约、术语表
- [02-主线任务树](方案讨论/针对2.0的ECS架构的迭代方案讨论/02-主线任务树/README.md)：任务看板、可领取叶子任务
- [04-当前进度状态](方案讨论/针对2.0的ECS架构的迭代方案讨论/04-当前进度状态/README.md)：跨轮接力快照、当前窗口
- [规范手册](方案讨论/针对2.0的ECS架构的迭代方案讨论/规范手册.md)：文档治理规范统一入口
- [Unity DOTS 官方文档参考](方案讨论/UnityDOTS官方文档参考/README.md)：PackageCache 版本、DOTS API 规则、官方案例
