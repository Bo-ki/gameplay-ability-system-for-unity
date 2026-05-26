# AutoChessDemo 实现事实

> 上次更新：2026-05-26 | 状态：**已破坏性删除，待重构** | 基于 34→11 个 C# 源文件（保留 11，删除 24）

本文件回答"当前 AutoChessDemo 已经实现了什么、与 Spec 存在哪些差距"。2026-05-26 执行了破坏性删除：24 个文件被移除，5 个文件被重写为桩/类型定义占位，6 个文件完整保留。下方事实为删除前的完整审查结论，标注为"（已删除）"的事实不再适用于当前代码。

## 破坏性删除摘要（2026-05-26）

| 操作 | 文件数 | 说明 |
|------|--------|------|
| **已删除** | 24 | 全部 Simulation reaction/driver(13) + Observation(2) + Presentation marker projection(1) + Validation scenario runtime(8) |
| **重写为桩** | 5 | Bootstrap → 空注册框架、RuntimeRunner → CLI stub、DemoSceneRunner → MonoBehaviour stub、ScaleValidation/PerformanceProfile → 类型定义保留 + Run() 抛 NotImplementedException |
| **完整保留** | 6 | Components.cs（28 IComponentData + 2 IBufferElementData）、DefinitionSource.cs、GeneratedDefinitionRows.cs、ScenarioConstants.cs、ScenarioTypes.cs、NoopCue.cs |
| **当前合计** | **11** | ~8400 行（删除前 ~19000 行） |

### 保留原因

| 文件 | 评级 | 保留原因 |
|------|------|---------|
| `HeadlessAutoChessComponents.cs` | A | 28 个 pure unmanaged struct，DOTS 合规最佳，无需修改 |
| `HeadlessAutoChessScenarioConstants.cs` | A | 全部常量集中管理，纯声明式，无依赖 |
| `HeadlessAutoChessScenarioTypes.cs` | A | 全部类型定义（readonly struct），作为后续重构的契约基础 |
| `HeadlessAutoChessNoopCue.cs` | A | 正确的 Cue 占位实现 |
| `HeadlessAutoChessDefinitionSource.cs` | B+ | Registry 注入模式 + warmup 实践正确，后续仅需 Baker 化 |
| `HeadlessAutoChessGeneratedDefinitionRows.cs` | B+ | Luban 生成代码，不应手动修改 |

## 代码规模（删除前）

| 层级 | 文件数（删除前→后） | 总行数（删除前） |
|------|---------------------|-----------------|
| Simulation | 15→2 | ~6400 |
| Observation | 2→0 | ~560 |
| Presentation | 3→2 | ~1830 |
| Config | 2→2 | ~3100 |
| Validation | 12→5 | ~7200 |
| **合计** | **34→11** | **~19000** |

## 目录结构与 Spec 对照

| Spec 10 目标目录 | 当前实现 | 差距 |
|---|---|---|
| `Runtime/Bootstrap/` | `Simulation/HeadlessAutoChessRuntimeSystemBootstrap.cs` | 未细分 Bootstrap 子目录；Bootstrap 是 static class 而非 ECS bootstrap |
| `Runtime/Board/` | 不存在 | 棋盘逻辑嵌入 Driver |
| `Runtime/Units/` | 不存在独立目录 | 单位定义嵌入 Scenario partial class |
| `Runtime/AbilityLogic/` | 不存在 | 能力激活逻辑嵌入 Driver |
| `Runtime/Effects/` | 不存在 | GE 反应逻辑分散在 9 个 Reaction System |
| `Runtime/Combat/` | 不存在 | 战斗决策全部在 Driver 主线程 |
| `Runtime/Synergy/` | `Simulation/SHeadlessAutoChessSynergyProjection.cs` | 单文件，未拆分 |
| `Runtime/AI/` | 不存在 | AI 选敌逻辑嵌入 Driver（12+ helper 方法） |
| `Runtime/Observation/` | `Observation/`（2 文件） | 基本对应 |
| `Runtime/Presentation/` | `Presentation/`（3 文件） | 无 `Contracts/` `LogOutboxBridge/` `UnityOutboxBridge/` 子层 |
| `Runtime/Validation/` | `Validation/`（12 文件） | 基本对应但 Scenario.cs 2611 行过于庞大 |
| `Runtime/Debugging/` | 不存在独立目录 | 调试能力嵌入 Scenario runtime timing |
| `Authoring/` | 完全不存在 | 零 Baker、零 ScriptableObject、零 EditorBridge |
| `Config/LubanTables/` | `EX_GAS_Config/.../Datas/`（8 张 Excel） | 缺少 `AutoChessDemo/` 子目录层级 |
| `Config/GeneratedRuntime/` | `Config/Generated/`（单体 2500 行文件） | 未按 Spec 11 拆分为 6 个独立 .g.cs |
| `Tests/` | 不存在独立测试目录 | 验证逻辑全部在 Scenario 内 |
| `Scenes/` | 不存在 | 无 Demo 场景 |
| `Content/` | 不存在 | 无占位资源 |

**事实 1**: 当前目录结构与 Spec 10 目标目录结构存在全面差距。Simulation/Observation/Presentation/Validation 四层已建立但内部均未细分。Authoring/Board/Units/AbilityLogic/Effects/Combat/Synergy/AI/Debugging/Tests/Scenes/Content 全部缺失。

## Simulation 层事实

### 系统清单

| 文件 | 行数 | SystemGroup | 职责 |
|------|------|------------|------|
| `SHeadlessAutoChessDriver.cs` | 999 | `GASCommandGroup` | 回合制战斗驱动、选敌、能力激活 |
| `SHeadlessAutoChessRuntimeSystemBootstrap.cs` | 113 | — (static) | 注册 16 个 System 到旧 Group |
| `SHeadlessAutoChessPassiveReaction.cs` | 372 | `GASExecutionCalculationExtensionGroup` | 击杀回蓝 + 自我复活 |
| `SHeadlessAutoChessSynergyProjection.cs` | 454 | `GASExecutionCalculationExtensionGroup` | 羁绊激活/去激活/周期 tick |
| `SHeadlessAutoChessSummonLifecycle.cs` | 507 | `GASExecutionCalculationExtensionGroup` | 召唤物生命周期 |
| `SHeadlessAutoChessShieldDamageCalculation.cs` | 179 | `GASExecutionCalculationExtensionGroup` | 护盾吸收伤害计算 |
| `SHeadlessAutoChessCounterReaction.cs` | 442 | `GASExecutionCalculationExtensionGroup` | 反击触发与伤害 |
| `SHeadlessAutoChessCleanseReaction.cs` | 467 | `GASExecutionCalculationExtensionGroup` | 净化 + 集结 buff |
| `SHeadlessAutoChessEnrageReaction.cs` | 330 | `GASExecutionCalculationExtensionGroup` | 低血量狂暴 |
| `SHeadlessAutoChessExecuteReaction.cs` | 416 | `GASExecutionCalculationExtensionGroup` | 处决（低血量斩杀） |
| `SHeadlessAutoChessLifeStealReaction.cs` | 333 | `GASExecutionCalculationExtensionGroup` | 吸血 |
| `SHeadlessAutoChessPoisonReaction.cs` | 432 | `GASExecutionCalculationExtensionGroup` | 中毒叠加 |
| `SHeadlessAutoChessDeathBurstReaction.cs` | 407 | `GASExecutionCalculationExtensionGroup` | 死亡爆发 AOE |
| `SHeadlessAutoChessRallyComboReaction.cs` | 347 | `GASExecutionCalculationExtensionGroup` | 集结连击 |
| `HeadlessAutoChessComponents.cs` | 699 | — | 28 个 IComponentData + 2 个 IBufferElementData |

**事实 2**: 16 个 AutoChess ISystem 中，15 个注册在旧的 `GASExecutionCalculationExtensionGroup`，1 个（Driver）在 `GASCommandGroup`。全部未迁移到目标态 8-phase SystemGroup。

### DOTS 合规状态（删除前）

| 指标 | 数值 | Spec 要求 |
|------|------|----------|
| 使用 IJobEntity 的 System | **0** | 全部热路径 |
| 使用 IJobChunk 的 System | **0** | 全部批量遍历 |
| 使用 ToEntityArray 的 System | **16 (100%)** | 0 |
| 直接 EM 结构变化（SC-01 违规） | **16 (100%)** | 0 |
| ECB 使用 | **0** | 所有结构变化应通过 ECB |
| `[BurstCompile]` | **0** | 全部热路径 |
| 使用旧 SystemGroup 名称 | **16 (100%)** | 应使用 8-phase 命名 |

**事实 3**（已解决——全部 16 个 ISystem 已删除）: AutoChessDemo 的 16 个 ISystem **全部零 BurstCompile、零 IJobEntity、零 IJobChunk、零 ECB**。这是整个 EX-GAS 代码库中 DOTS 合规最薄弱的子系统——比 Runtime Core（2/39 使用 IJobEntity）的合规度更低。破坏性删除后，AutoChessDemo 当前零个 ISystem，DOTS 合规问题集中到后续重构实现中解决。

### Component 设计

**事实 4**: `HeadlessAutoChessComponents.cs` 中的 28 个 IComponentData 和 2 个 IBufferElementData 全部为 pure unmanaged struct——这是 AutoChessDemo 在 DOTS 合规方面唯一的正面事实。所有组件均使用 blittable 字段（float、int、Entity、bool、enum）。

关键类型：
- `CHeadlessAutoChessDriver`（17 字段）：回合驱动状态、统计计数器
- `CHeadlessAutoChessUnit`（40 字段）：单位完整状态（位置、属性、技能槽、目标策略、响应状态）
- 10 个 reaction 专用 `*Facts`/`*Rules`/`*State` 三元组

### 代码重复

**事实 5**: 9 个 Reaction System 各自独立实现了相同的 helper 模式，造成大量代码重复：

| 重复模式 | 出现次数 | 文件 |
|---|---|---|
| `IsAliveAutoChessUnit()` | 9+ | 所有 Reaction |
| `GetAttribute(Entity, attrCode)` | 12+ | 所有 Reaction + Driver + Scenario |
| `ResolveCurrentFrame()`（含临时 EntityQuery fallback） | 8+ | 所有 Reaction |
| `IsGameplayEffectWithCode()` | 9+ | 所有 Reaction |
| `CreateApplyRequest()` → `GameplayEffectLegacyBridge` | 9+ | 所有 Reaction |

**事实 6**: `ResolveCurrentFrame()` 在多个 Reaction System 中的 fallback 路径创建临时 `EntityQuery`（`em.CreateEntityQuery(ComponentType.ReadOnly<GlobalTimer>())`），这是 PRF-05 和 API 选型的双重违规——应使用 `SystemAPI.GetSingleton<GlobalTimer>()` 或 `SystemAPI.Time`。

### 事件驱动模式

**事实 7**: AutoChess Reaction System 全部通过读取全局 EventBus 的 5 种 DynamicBuffer（`BGameplayEvent`、`BAttributeChangeEvent`、`BDamageEvent`、`BTagChangeEvent`、`BCueRequest`）来触发——违反 Spec 10 的"不依赖全局 EventBus 扫描"约束。EventBus 的 `SnapshotBufferRange` 模式用于增量读取，但核心问题仍是：反应逻辑依赖全局事件扫描而非 typed fact 驱动。

### 与 Spec 10B 业务设计对照

| Spec 10B 定义 | 当前实现 | 差异 |
|---|---|---|
| 4 种棋子（Frost Swordsman, Frost Witch, Shadow Assassin, Light Priest） | 7 种玩家棋子（Guardian, Mage, Ranger, Cleric, Battlemage 等） | 棋子种类完全不同 |
| 6 种属性（HP/MaxHP/ATK/DEF/ASPD/MANA） | 8 种属性（Health/Mana/Shield/ArcaneResistance/CounterDamage/LifeStealRatio + Max 变体） | 属性集完全不同 |
| 冰系羁绊 + 牧师职业羁绊 | 奥术（Arcane）羁绊 | 羁绊主题不同 |
| 3 条业务走查（盾击眩晕、冰霜新星、毒刃） | 9 种反应系统（Counter/Cleanse/RallyCombo/LifeSteal/Poison/Execute/DeathBurst/Enrage/Passive） | 机制数量远超 Spec 但未全覆盖 |
| Frame tick 实时战斗 | 回合制战斗 | **核心设计差异** |
| 4×2 棋盘 | 6×3 棋盘 | 棋盘尺寸不同 |
| `BHealth`/`BAttack` 等每种属性一个 IComponentData | `BAttribute` DynamicBuffer（统一属性槽） | 属性布局不同——当前使用 Runtime Core 通用 Buffer 而非 type-per-attribute |

**事实 8**: 当前 AutoChessDemo 的**实际业务设计与 Spec 10B 完全无关**。Spec 10B 定义的冰系/暗影/圣光三阵营、4×2 棋盘、实时帧战斗、6 种属性独立 Component、3 条走查，在代码中均不存在。当前实现是一套独立的更复杂的自走棋规则（回合制、6×3 棋盘、9 种反应系统、奥术羁绊）。

## Observation 层事实

| 文件 | 行数 | SystemGroup |
|------|------|------------|
| `SHeadlessAutoChessBattleFactProjection.cs` | 454 | `GASAttributeGroup` |
| `SHeadlessAutoChessGameplayEffectFactProjection.cs` | 106 | `GASAttributeGroup` |

**事实 9**: Observation 层职责划分清晰：`BattleFactProjection` 投影战斗事实（护盾、死亡、战斗结算），`GameplayEffectFactProjection` 投影 GE 应用事实。两者均写入 Driver 实体的 `BHeadlessAutoChess*Fact` buffer。

**事实 10**: BattleFactProjection 的 `deferBattleResolution = true` 机制（当一个单位被击败时延迟结算到下一帧）是正确的设计模式——防止同一帧内多个单位同时死亡导致胜负结算错乱。

**DOTS 合规**: 两个 Observation System 均使用 `ToEntityArray` + 直接 EM——与 Simulation 层相同的合规问题。

## Presentation 层事实

| 文件 | 行数 | SystemGroup |
|------|------|------------|
| `SHeadlessAutoChessPresentationCueMarkerProjection.cs` | 1720 | `GASCueGroup` |
| `HeadlessAutoChessDemoSceneRunner.cs` | 105 | — (MonoBehaviour) |
| `HeadlessAutoChessNoopCue.cs` | 6 | — |

**事实 11**: PresentationCueMarkerProjection（1720 行）是一个巨型表现标记系统。核心特征：
1. 消费全部 5 种 EventBus buffer（BGameplayEvent、BAttributeChangeEvent、BDamageEvent、BTagChangeEvent、BCueRequest）
2. 1000+ 行 switch 语句将 GameplayEvent 映射到 6 种 Presentation marker（UI/VFX/SFX/FloatingText/Cue/Settlement）
3. 使用 `EventBusHelper.AppendPresentationEvent` 写入 `BPresentationEvent` buffer
4. 定义了 100+ 个 `HeadlessAutoChessPresentationMarkerCode` 枚举值

**事实 12**: Spec 10 要求默认无头模式下保留完整的 Cue/UI/VFX/SFX/FloatingText 链路。当前实现满足了此要求——所有 marker 类型均有占位输出，`HeadlessAutoChessNoopCue` 提供无操作 Cue 实现。

**DOTS 合规**: PresentationCueMarkerProjection 使用 `ToEntityArray` + 直接 EM——同样零 IJobEntity、零 ECB。

## Config 层事实

| 文件 | 行数 | 职责 |
|------|------|------|
| `HeadlessAutoChessGeneratedDefinitionRows.cs` | ~2500 | Luban 生成的 Row 类型 + Snapshot + lookup |
| `HeadlessAutoChessDefinitionSource.cs` | 583 | Snapshot→Registry 桥接 + 7 个内联 Config 类 |

**事实 13**: Config 层遵循与 Runtime Core 一致的 Registry 注入模式：
1. `HeadlessAutoChessDefinitionSource.RegisterRuntimeProviders()` 将 4 个 `Func<int, T>` 委托注入到 Ability/GE/Timeline/Cue ConfigRegistry
2. `HeadlessAutoChessGeneratedRegistrySnapshot` 使用 `IReadOnlyList<T>` 托管数组存储 8 种 Row（同 BAKE-01 违规）
3. 7 个内联 Config 子类（DirectMaskAbilityTags、DirectMaskGrantedTags、ShieldDamageExecution 等）是 Demo 专用胶水代码

**事实 14**: `GameplayEffectConfigRegistry.TryWarmupRuntimePrototype()` 在 `WarmupGameplayEffectPrototypes()` 中被调用——这是正确的 warmup 实践，避免首帧 BlobAsset 懒加载开销。

**对照 Spec 11 缺口**:
- 14 种目标态生成产物仅实现 Row/Snapshot 2 种基本类型
- 12 张 AutoChess 配置表当前实现 8 张（缺 autochess.unit、autochess.scale_profile、autochess.validation、autochess.battle_param）
- BlobAsset lookup 通过 O(n) 线性搜索 array 实现（同 GASDefinitionTable 的 ISSUE-005 模式）
- 零 `Baker<T>` 实现

## Validation 层事实

| 文件 | 行数 | 职责 |
|------|------|------|
| `HeadlessAutoChessScenario.cs` | 2611 | 主场景运行器、单元创建、验证报告、事件计数 |
| `HeadlessAutoChessScenarioTypes.cs` | 1311 | 全部类型定义（Options/Thresholds/Result/EventCounts/Timing） |
| `HeadlessAutoChessScaleValidation.cs` | 1010 | 规模验证（多 variant × 多 run） |
| `HeadlessAutoChessPerformanceProfile.cs` | 863 | 性能 profile（P50/P95/Max tick） |
| `HeadlessAutoChessScenarioConstants.cs` | 146 | 全部常量（Ability/GE/Tag/Timeline/Attribute codes + 业务参数） |
| `HeadlessAutoChessScenarioVariants.cs` | 138 | 4 种 variant 定义 |
| `HeadlessAutoChessScenarioUnitDefinitions.cs` | 372 | 单位定义（Default/PlayerAdvantage/EnemyPressure/LargeBoard） |
| `HeadlessAutoChessScenarioUnitResolution.cs` | 61 | 胜负判定 + 单位刷新 |
| `HeadlessAutoChessScenarioState.cs` | 320 | ScenarioState + UnitDefinition/UnitRuntime |
| `HeadlessAutoChessScenarioBootstrap.cs` | 31 | GAS 初始化 + 系统注册 |
| `HeadlessAutoChessScenarioRuntimeLifecycle.cs` | 163 | 观察状态重置 + 清理（全部直接 EM） |
| `HeadlessAutoChessScenarioRuntimeTiming.cs` | 161 | 逐 Group 手动计时 |
| `HeadlessAutoChessRuntimeRunner.cs` | 155 | CLI runner（`[RuntimeInitializeOnLoadMethod]`） |

**事实 15**: Validation 层覆盖面极广但存在于一个巨型 partial class 中：
- `HeadlessAutoChessScenario` 横跨 11 个 partial class 文件（~4600 行合计）
- `HeadlessAutoChessScenarioTypes.cs` 单文件 1311 行
- `BuildValidationReport()` 包含 ~90 个 AddMinFailure/AddMaxFailure 检查
- `BuildValidationSummary()` 生成 200+ 字段的文本文本报告
- `CountEvents()` 遍历整个 replay log 计算 60+ 事件类型计数
- 确定性签名通过 FNV-1a hash 覆盖 80+ 字段

**事实 16**: Validation 层的 DOTS 合规问题与 Simulation 层相同——全部直接 EM、零 ECB。但考虑到 Validation 是 bootstrap/teardown 代码（非 hot path），部分直接 EM 可接受。问题是 `CleanupUnits()`、`ResetObservationState()` 和 `DestroyAscRuntime()` 中大量使用 `em.DestroyEntity()`、`em.CreateEntityQuery()` 等操作，这些应在 ECB 中完成。

**事实 17**: Runtime Timing 通过手动 `Stopwatch.GetTimestamp()` 逐 Group 调用 `group.Update()` 实现——这是正确的 measurement 模式。但不使用 `ICustomBootstrap`（Spec 10 要求），而是通过 `GASManager.Initialize()` 获取 World。

## 跨层事实

### 事实 18: GameplayEffectLegacyBridge 依赖

全部 9 个 Reaction System 和 Driver 使用 `GameplayEffectLegacyBridge.ApplyLegacyInstantBypassOrCreateSingleTargetRequest()` 创建 GE apply 请求。这是 Runtime Core ISSUE-001（GE 生命周期管线过重）的直接后果——高层业务被迫走 legacy request entity 路径。

### 事实 19: 回合制 vs 实时帧战斗

Spec 10B 明确要求"Frame tick（实时战斗，非回合制）"，但当前实现是纯回合制：Driver 按 TurnOrder 逐一选取行动单位，执行技能后等待下一帧继续。这是与 Spec 10B 最根本的设计差异。

### 事实 20: CTagMask ulong 设计

Spec 10B Invariant 32 要求 CTagMask 使用 ulong bitmask（最大 64 tags）。当前实现遵循此设计，13 个 AutoChess tag bit 全部在位掩码范围内。`TagAbilityActing` 等 tag 通过 `CTagMask.AddTag()` 设置。

### 事实 21: 无 ICustomBootstrap

Spec 10 要求"无头 runner 必须使用 `FixedStepTime(1.0f / 60f)` 的独立 World，通过 `ICustomBootstrap` 创建"。当前实现中 `HeadlessAutoChessRuntimeRunner` 使用 `[RuntimeInitializeOnLoadMethod]` 直接调用 `GASManager.Initialize()`——不创建独立 World，不设置 FixedStepTime。

## DOTS 合规缺陷（AutoChessDemo 专属）

以下缺陷是 AutoChessDemo 独有或在 AutoChessDemo 中特别严重的：

| ID | 缺陷 | 严重度 | 影响范围 |
|----|------|--------|---------|
| AC-01 | 16 个 ISystem 全部零 IJobEntity/IJobChunk/ECB | P0 | 全部 Simulation/Observation/Presentation |
| AC-02 | 全部 System 注册到旧 SystemGroup 名称 | P1 | 全部 16 个 System |
| AC-03 | 9 个 Reaction System 存在大量代码重复（5+ 模式 × 9 文件） | P1 | 全部 Reaction |
| AC-04 | `ResolveCurrentFrame()` 创建临时 EntityQuery 作为 fallback | P1 | 8+ Reaction System |
| AC-05 | `HeadlessAutoChessScenario` partial class 跨越 11 个文件（~4600 行） | P1 | 全部 Validation |
| AC-06 | PresentationCueMarkerProjection 1720 行，含 1000+ 行 switch | P2 | Presentation |
| AC-07 | 目录结构与 Spec 10 目标结构完全不匹配 | P1 | 全部 |
| AC-08 | 业务设计与 Spec 10B 完全无关（不同的棋子、属性、战斗系统） | P1 | 全部 Simulation |
| AC-09 | `HeadlessAutoChessGeneratedDefinitionRows.cs` ~2500 行单体生成文件 | P2 | Config |
| AC-10 | 无独立 World / FixedStepTime / ICustomBootstrap | P1 | Validation |
| AC-11 | `GameplayEffectLegacyBridge` 是全 AutoChess 的唯一 GE apply 通道 | P1 | 全部 Simulation |
| AC-12 | SummonLifecycle 直接 `em.CreateEntity()` 创建请求实体（零 ECB） | P1 | Simulation |

## 完全合规的代码

| 文件/组件 | 评级 | 关键特征 |
|---|---|---|
| `HeadlessAutoChessComponents.cs` | **A** | 全部 28 个 IComponentData + 2 个 IBufferElementData 为 pure unmanaged struct，blittable 字段 |
| `HeadlessAutoChessNoopCue.cs` | **A** | 正确的 Cue 占位实现 |
| `HeadlessAutoChessScenarioConstants.cs` | **A** | 全部常量集中管理，纯声明式 |
| `HeadlessAutoChessDefinitionSource.cs` | **B+** | 正确遵循 Registry 注入模式，warmup 实践正确 |
| `HeadlessAutoChessScaleValidation.cs` | **B+** | 完整的规模验证框架，确定性 FNV-1a hash，正确使用 readonly struct |
| `HeadlessAutoChessPerformanceProfile.cs` | **B+** | P50/P95/Max 分位数计算，正确的 measurement 口径 |
| `SHeadlessAutoChessBattleFactProjection.cs` | **B** | 职责边界清晰，deferBattleResolution 设计正确 |

## Spec 对照总表

| Spec 要求 | 当前状态 | 评级 |
|---|---|---|
| **Spec 10: 目录结构** | Simulation/Observation/Presentation/Config/Validation 存在但未细分 | **不匹配** |
| **Spec 10: 四层 Demo 模型** | Config→Sim→Obs→Pres 数据流方向正确 | **符合** |
| **Spec 10: 默认无画面运行** | Headless runner 存在且可工作 | **符合** |
| **Spec 10: Presentation marker 占位** | UI/VFX/SFX/FloatingText/Cue/Settlement 6 种 marker 全部占位 | **符合** |
| **Spec 10: ICustomBootstrap** | 不存在 | **缺失** |
| **Spec 10: 规模验收 Gate** | Functional x1 存在 + ScaleValidation 框架 | **部分** |
| **Spec 10: 性能门槛 (0.X ms)** | 当前 `1.x ms` 级别（未 DOTS 优化） | **不达标** |
| **Spec 10: 业务链路精而不多** | 9 种反应系统（远超 Spec 10 建议的"精链路"） | **超出** |
| **Spec 10: 禁止全局 EventBus 扫描** | 全部 Reaction 依赖 EventBus buffer 读取 | **违反** |
| **Spec 10B: 棋子/属性/技能设计** | 完全不同的棋子/属性/技能体系 | **不匹配** |
| **Spec 10B: 实时帧战斗** | 回合制 | **不匹配** |
| **Spec 10B: 3 条业务走查** | 9 种反应系统，无对应走查 | **不匹配** |
| **DOTS: IJobEntity/IJobChunk** | 零 | **违反** |
| **DOTS: ECB 结构变化** | 零 | **违反** |
| **DOTS: BurstCompile** | 零 | **违反** |

## 汇总结论（删除后更新）

1. **破坏性删除已完成**：24 个需要大范围重构的文件已移除，5 个文件重写为桩/类型占位，6 个设计优秀的文件完整保留。当前 AutoChessDemo 共 11 个 .cs 文件，~8400 行。

2. **保留的核心资产**：
   - **数据契约层**：`HeadlessAutoChessComponents.cs`（28 IComponentData + 2 IBufferElementData，全部 pure unmanaged struct）——后续重构的数据基础
   - **配置链**：`DefinitionSource.cs` + `GeneratedDefinitionRows.cs`——Registry 注入模式 + Luban 生成链路，仅需 Baker 化
   - **类型契约**：`ScenarioTypes.cs` + `ScenarioConstants.cs`——1311+146 行 readonly struct 类型和常量，作为后续验收框架的契约基础
   - **验收框架骨架**：`ScaleValidation.cs` + `PerformanceProfile.cs`——6+6 个 readonly struct 类型定义完整保留，Run() 待实现

3. **删除的债务已清零**：16 个零 DOTS 合规 ISystem、9 个 Reaction System 代码重复、1720 行巨型 switch 语句、GameplayEffectLegacyBridge 全局依赖——全部随删除移除。后续重构从零开始，不受旧代码约束。

4. **待重构范围**：
   - Simulation：Driver + 9 reaction + synergy + summon lifecycle + shield damage（需按 Spec 10 重新设计为 Board/Units/AbilityLogic/Effects/Combat/Synergy/AI 子目录）
   - Observation：BattleFactProjection + GameplayEffectFactProjection
   - Presentation：PresentationCueMarkerProjection
   - Validation：Scenario.cs 主运行器 + 8 个 partial class 辅助文件

5. **业务设计方向待决策**：删除前代码为回合制自走棋（6×3 棋盘、奥术羁绊、9 种反应），Spec 10B 定义实时帧战斗（4×2 棋盘、冰系/暗影/圣光）。重构前需确认走哪个方向。

6. **Config 链是最稳固的起點**：Registry 注入模式正确，warmup 实践正确，Luban 生成链路可用。后续重构可从 Config 层向 Simulation/Observation/Presentation/Validation 逐层推进。
