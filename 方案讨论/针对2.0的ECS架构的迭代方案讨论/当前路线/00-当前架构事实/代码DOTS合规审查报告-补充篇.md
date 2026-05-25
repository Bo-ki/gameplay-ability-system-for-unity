# 代码DOTS合规审查报告 — 补充篇

## 审查范围

本轮补充审查覆盖上一轮未涉及的 ~50 个 GAS Runtime 文件（~15,000 行 C#），按模块分为六层：

| 层级 | 文件数 | 覆盖模块 |
|---|---|---|
| Tier 1: Effect 系统 | 8 | SEffectRemove, SExecutionCalculation, SExecutionCalculationOutputModifier, SRemoveGameplayEffectRequest, SOngoingTagRequirements, EffectMagnitudeResolver, GameplayEffectLegacyBridge, ExecutionCalculationRuntimeActions |
| Tier 2: Ability 系统 | 7 | SAbilityCommandRequest, SAbilityLifecycleRequest, SAbilityStateCleanup, SAbilityTick, SAbilityTimelineAction, SAbilityTimelineLifecycleRequest, SAttributeThresholdAbilityLifecycleRequest |
| Tier 3: ASC/Attribute/Tag 系统 | 7 | SAttributeRecalculate, STagQuery, SASCCreate, SAscCommandRequest, SAscDestroyFinalize, SAscDestroyRequest, SAscInitializeRequest |
| Tier 4: Helper/General 层 | 9 | GASManager, AttributeHelper, TagHelper, EntityHelper, CueHelper, TagRuntimeUtility, TagRequirementEvaluator, GameplayCueUnit, AbilityEntityFactory |
| Tier 5: Definition/Baking 层 | 8 | GASDefinitionGeneratedAdapter, GASDefinitionTable, GASGeneratedDefinitionBakeContract/Pipeline/Plan, GASGeneratedDefinitionRuntimeIntegrationPlan, AbilitySystemEntityFactory, AbilitySystemFacade |
| Cue 系统 | 5 | SCueDestroy, SCueEnd, SCueRequestBridge, SCueStart, SCueTick |
| Component 定义 | 15 | MCCue, ECCuePlayable/Playing, ECKillCue, CPlayImmunitedTags/RequiredTags, CCueOnApply/Add/Activate/Deactivate/Remove/Tick, BActiveModifier, CAscBasicData, CGASRunningTag |

---

## 新增 P0 致命缺陷

### 缺陷 M: `MCCue` 为 managed class IComponentData — 全部 Cue System 无法 Burst

**严重程度**: P0 × 5 个 System 全部受影响

**代码证据**: `Assets/GAS/Runtime/Cue/Component/MCCue.cs:8-10`
```csharp
public class MCCue : IComponentData  // class 不是 struct!
{
    public GameplayCueBase cue;  // 托管 abstract class 引用
}
```

**违反规则**:
- `SYS-01`: managed IComponentData 无法在 Job 中使用
- `BUR-01`: 托管引用阻止 Burst 编译
- `PRF-05`: 强制所有 Cue System 回退到主线程 foreach

**影响链**:
1. `SCueDestroy.OnUpdate` — `[BurstCompile]` 被注释关闭（line 19）
2. `SCueEnd.OnUpdate` — `[BurstCompile]` 被注释关闭（line 19）
3. `SCueRequestBridge.OnUpdate` — 未标注 BurstCompile
4. `SCueStart.OnUpdate` — `[BurstCompile]` 被注释关闭（line 18）
5. `SCueTick.OnUpdate` — `[BurstCompile]` 被注释关闭（line 18）

全部 5 个 System 通过 `SystemAPI.Query<..., MCCue>()` 做主线程 managed 遍历，每帧调用 `mcCue.cue.OnActivate(Time.time)` 等 managed 虚方法。

---

### 缺陷 N: `SCueRequestBridge` 零 ECB + 直接 EntityManager 结构变化

**严重程度**: P0

**代码证据**: `Assets/GAS/Runtime/System/Cue/SCueRequestBridge.cs`
- Line 22: `state.EntityManager.HasBuffer<BCueRequest>(eventBus)` — 直接 EM 访问
- Line 28: `var requests = em.GetBuffer<BCueRequest>(eventBus)` — 直接 EM 读 buffer
- Line 107: `em.SetComponentEnabled<ECKillCue>(cueEntity, true)` — 直接结构变化
- Line 109: `em.DestroyEntity(cueEntity)` — 直接销毁 entity

**全文无任何 ECB 使用**。所有结构变更通过 `EntityManager` 即时执行。

---

### 缺陷 O: `EffectMagnitudeResolver.PlaybackAndReset` — ECB 碎片化播放

**严重程度**: P0

**代码证据**: `Assets/GAS/Runtime/System/Effect/EffectMagnitudeResolver.cs:542-551`
```csharp
private static void PlaybackAndReset(ref EntityCommandBuffer ecb, EntityManager em)
{
    ecb.Playback(em);   // 在 helper 方法内部 playback!
    ecb.Dispose();
    ecb = new EntityCommandBuffer(Allocator.Temp);  // 立即重建
}
```

被 `GetOrCreateResolvedModifiers`（line 500-503）、`GetOrCreateAttributeCaptures`（line 523-525）、`EnsureResolverBuffers` 三个路径调用。**每次调用都是一次独立的 sync point + ECB 重建，破坏事务性。**

同模式也出现在 `SExecutionCalculationOutputModifier.cs:198-217`：
```csharp
ecb.AddBuffer<BResolvedModifier>(ge);
PlaybackAndReset(ref ecb, em);  // 在系统 Update 中间 playback
```

---

## 新增 P1 严重缺陷

### 缺陷 P: 三个 ASC 管理系统零 ECB — `SAscCommandRequest` / `SAscDestroyFinalize` / `SAscDestroyRequest`

**严重程度**: P1

**代码证据**:

| 文件 | 行数 | 直接 EM 操作数 | ECB 使用 |
|---|---|---|---|
| `SAscCommandRequest.cs` | 268 | 8+ 处（DestroyEntity, AddComponentData, SetComponentData, AddBuffer, GetBuffer.Add/RemoveAt） | **零** |
| `SAscDestroyFinalize.cs` | 136 | 4+ 处（DestroyEntity, GetBuffer, HasComponent, GetComponentData） | **零** |
| `SAscDestroyRequest.cs` | 217 | 10+ 处（DestroyEntity, AddComponent, AddBuffer, GetBuffer.RemoveAt, HasComponent, AddMarker<T>） | **零** |

**特别严重**: `SAscDestroyRequest.DestroyOwnedAbilities`（line 133-167）在主线程嵌套调用 `AbilityRuntimeActions.RequestAbilityCancel()`，在销毁 entity 前手动释放 `CAbilityConfig` 的托管资源，深度违反 DOTS 的确定性执行和结构变更隔离原则。

---

### 缺陷 Q: `TagHelper` 四个静态托管 Dictionary — 无法 Burst

**严重程度**: P1

**代码证据**: `Assets/GAS/Runtime/General/Helper/TagHelper.cs:9-12`
```csharp
private static Dictionary<int, GameplayTag> _tagMap;
private static Dictionary<int, string> _tagCode2TagName;
private static Dictionary<int, int> _tagCodeToDenseIndex;
private static Dictionary<int, int> _tagDenseIndexToCode;
```

所有 Tag 查找操作回退到托管 Dictionary（`_tagMap.ContainsKey`、`_tagMap.Keys` foreach），且多处使用 `new List<int>()` 做临时收集。整个 Tag 运行时查找基础设施无法进入 Burst/Job。

---

### 缺陷 R: `GameplayCueUnit` — Hybrid ECS 遗留 class 包装 Entity

**严重程度**: P1

**代码证据**: `Assets/GAS/Runtime/Cue/GameplayCueUnit.cs`
- Line 11: `public class GameplayCueUnit` — 托管 class 包装 Entity
- Line 13-21: 7 个托管字段（`Type _cueType`, `XParam _xParam`, 6 个 `int[]` 数组）
- Line 94: `_cueEntity = EntityManager.CreateEntity()` — 直接创建 entity
- Line 97-109: 6 次 `EntityManager.AddComponentData/AddComponent/SetComponentEnabled` — 批量直接结构变化
- Line 144-145: `EntityManager.GetComponentData<MCCue>(_cueEntity).cue.AddToTargetAsc(asc)` — 读取 IComponentData 后访问其托管字段

**全文零 ECB 使用**。创建/销毁/启停全部直接 EntityManager。

---

### 缺陷 S: `SAbilityTimelineAction` 360 行零 ECB + 直接 Entity 创建

**严重程度**: P1

**代码证据**: `Assets/GAS/Runtime/System/Ability/SAbilityTimelineAction.cs`
- Line 267: `em.CreateEntity()` — 在 hot path 中直接创建 entity
- Line 273-279: 4 次 `em.AddComponent<...>(cueEntity)` + `em.AddComponentData` — 批量直接结构变化
- Line 108-110: `em.SetComponentData` / `em.AddComponentData` — 直接写组件

**全文零 ECB 使用**。String 比较做 dispatch（`clip.ActionType == "ApplyEffects"`），应改为枚举/整数 ID。

---

### 缺陷 T: `CueHelper` 运行时反射创建 Cue 实例

**严重程度**: P1

**代码证据**: `Assets/GAS/Runtime/General/Helper/CueHelper.cs`
- Line 32: `if (Activator.CreateInstance(type) is GameplayCueBase cue)` — 运行时反射
- Line 52: `var cueParamEditor = (XParam)Activator.CreateInstance(cueParamConfigType)` — 运行时反射
- Line 72-74: 三个静态 `Dictionary<string, Type>` / `Dictionary<string, string>` — 托管反射类型映射

---

### 缺陷 U: Definition 层 6 个 DTO struct 全部含托管数组

**严重程度**: P1

**代码证据**:
- `GASDefinitionTable` 含 6 个托管数组字段
- `GASGeneratedDefinitionBakingPlan` 含 `GASGeneratedDefinitionBakingEntry[]`
- `GASGeneratedDefinitionBakeContract` 含 `GASGeneratedDefinitionBakeWrite[]` 和 `ArchetypeTemplate[]`
- `GASGeneratedDefinitionRuntimeIntegrationPlan` 含 `GASGeneratedDefinitionRuntimeIntegrationEntry[]`
- `GameplayTagDefinitionSummary` 含 `int[] ParentCodes` 和 `int[] ChildCodes`

这些值类型中的托管引用使它们无法作为 BlobAsset 内容或 ECS Component Data 使用。

---

### 缺陷 V: BlobAsset 通过静态 `Dictionary` 管理生命周期

**严重程度**: P1

**代码证据**: `GameplayEffectConfigRegistry` 中 `StaticDefinitionBlobByCode` 静态 Dictionary 缓存 BlobAsset，通过 `DisposeCachedStaticDefinitionBlobs()` 手动释放。绕过 Baker 的 `BlobAssetStore`。

---

### 缺陷 W: 代码库中零 `Baker<T>` 实现

**严重程度**: P1

**代码证据**: 全量搜索 `Assets/GAS/Runtime` 未发现任何 `Baker<T>` 类。BakeContract / BakePipeline / BakingPlan 是自定义预处理层，但与 Unity `Baker<T>` 的实际集成点缺失。CASE-39/40/41（Baker 三原则）无法在当前代码库中评估。

---

## P2 改进项

### 缺陷 X: 5 个 System 的 `OnUpdate` 的 `[BurstCompile]` 被注释关闭

`SCueDestroy`、`SCueEnd`、`SCueStart`、`SCueTick` 的 `OnUpdate` 上 `[BurstCompile]` 被 `//` 注释，根本原因是 `MCCue` 的 managed 引用。这是 `MCCue` 违规的直接后果，修复 `MCCue` 后可一并解决。

### 缺陷 Y: 4 个空 struct Tag Component 违反 PRF-03

`ECCuePlayable`、`ECCuePlaying`、`ECKillCue`、`CGASRunningTag` 是零字段的 `IComponentData`，作为 enableable tag 使用时功能上合理，但严格来说每个 tag component type 都会使 archetype 排列数翻倍。建议合并为 bitmask 或在确认低频使用后保留。

### 缺陷 Z: `CCueOn*` 六个文件 `NativeArray<Entity>` 挂在 `IComponentData` 上 — PRF-34

上一轮已报告的缺陷 L 得到本轮的补充证据：`CCueOnApply.cues`、`CCueOnAdd.cues`、`CCueOnActivate.cues`、`CCueOnDeactivate.cues`、`CCueOnRemove.cues`、`CCueOnTick.cues` 六个文件全部含有 `NativeArray<Entity>` 字段。修复方案：替换为 `DynamicBuffer<Entity>`（`IBufferElementData`）。

---

## 完全合规的代码

本轮审查中发现的**符合 DOTS 目标态**的文件：

| 文件 | 评级 | 关键特征 |
|---|---|---|
| `SAbilityTick.cs` | **A+** | `[BurstCompile]` + `IJobEntity` + `ScheduleParallel`，零 EM 操作 |
| `TagRequirementEvaluator.cs` | **A** | 纯函数式位掩码评估，无结构变化，可直接加 `[BurstCompile]` |
| `AbilitySystemEntityFactory.cs` | **A** | 正确重载 EM/ECB 路径，使用 `EnsureCapacity`，无状态 |
| `AbilitySystemFacade.cs` | **A** | 清晰的 OOP/ECS 桥接层，读写分离，通过 request entity 异步写入 |
| `BActiveModifier.cs` | **A** | 正确的 `IBufferElementData`，全部 blittable 字段 |
| `CAscBasicData.cs` | **A** | 正确的 `IComponentData`，纯 unmanaged 字段 |
| `CPlayImmunitedTags.cs` / `CPlayRequiredTags.cs` | **A** | 纯 unmanaged struct，`TagRequirementMask` 由 `ulong` 构成 |
| `GASManager.cs` | **B+** | 一次性初始化代码，使用直接 EM 可接受 |
| `AbilityEntityFactory.cs` | **B+** | 标准 `BlobBuilder` 模式，应迁移到 Baking 阶段 |
| `SAscInitializeRequest.cs` | **B** | 已使用 ECB 写入 + EM 读取，混合中间态 |

---

## 违规密度热力图

| 文件 | 行数 | PRF-05 | PRF-02/SC-01 | PRF-09/33 | ECB缺失 | 托管 | 综合评级 |
|---|---|---|---|---|---|---|---|
| SAscCommandRequest.cs | 268 | 1 | **8+** | 1 | **全零** | — | **F** |
| SAscDestroyRequest.cs | 217 | 1 | **10+** | 2 | **全零** | — | **F** |
| SAbilityTimelineAction.cs | 360 | 1 | **10+** | 0 | **全零** | string | **F** |
| GameplayCueUnit.cs | 217 | — | 8+ | 0 | **全零** | class | **F** |
| TagHelper.cs | 182 | 6 | — | — | — | 4 Dict | **F** |
| SCueRequestBridge.cs | 116 | 1 | 6+ | 1 | **全零** | — | **F** |
| SAscDestroyFinalize.cs | 136 | 1 | 4+ | 3 | **全零** | — | **F** |
| EffectMagnitudeResolver.cs | 575 | — | 10+ | — | ECB-03 | — | **D** |
| SExecutionCalculation.cs | 427 | 1 | 10+ | 1 | ECB-01 | — | **D** |
| CueHelper.cs | 178 | 1 | — | — | — | 反射+Dict | **D** |
| SExecutionCalculationOutputModifier.cs | 220 | 1 | 8+ | 2 | **ECB-03** | — | **D** |
| SAbilityCommandRequest.cs | 230 | 2 | **10+** | 0 | **全零** | — | **D** |
| SAscInitializeRequest.cs | 134 | 1 | 3(读) | 1 | ECB-03 | — | **C** |
| SAbilityStateCleanup.cs | 265 | 1 | 1+混合 | 0 | ECB-01 | List | **C** |
| AttributeHelper.cs | 106 | 1 | 4+ | — | 全零 | — | **C** |

---

## 汇总结论

本轮补充审查 + 上一轮初篇审查，合计覆盖了 GAS Runtime 几乎所有关键热路径文件（~65 文件，~23,000 行）。核心结论与初篇一致但进一步强化：

1. **ECB 缺失是最普遍的 P0 问题**：除 `SASCCreate`、`SAscInitializeRequest`、`SAbilityStateCleanup`（部分）和 `AbilitySystemEntityFactory` 外，几乎所有 runtime system 和 helper 都直接使用 `EntityManager` 做结构变化。本轮新增识别出 `SAscCommandRequest`、`SAscDestroyFinalize`、`SAscDestroyRequest`、`SCueRequestBridge`、`SAbilityTimelineAction` 五个 **零 ECB** 文件。

2. **`MCCue` managed class 是 Cue 系统的根因缺陷**：一个文件的类型声明错误（`class : IComponentData`）导致全部 5 个 Cue System 的 `[BurstCompile]` 被关闭，所有 Cue 逻辑锁死在主线程。

3. **唯一的合规范例是 `SAbilityTick.cs`**：在 ~65 个审查文件中，仅此一个文件完全符合 DOTS 目标态（`[BurstCompile]` + `IJobEntity` + `ScheduleParallel` + 零 EM 操作）。其他文件应以此为目标模板迁移。

4. **Definition/Baking 层完全缺失 `Baker<T>` 实现**：自定义的 BakeContract/BakePipeline/BakingPlan 是规划层，但没有对接 Unity 的 `Baker<T>`。BlobAsset 生命周期通过静态 Dictionary 管理而非 `BlobAssetStore`。

5. **`TagRequirementEvaluator.cs` 和 `STagQuery.cs` 是隐藏的合规亮点**：纯位掩码操作，零结构变化，可直接标记 `[BurstCompile]`。

| 严重度 | 新增缺陷数 | 关键违规规则 |
|---|---|---|
| P0 | 3 (M, N, O) | `SYS-01`, `BUR-01`, `ECB-01`, `ECB-03`, `SC-01` |
| P1 | 9 (P, Q, R, S, T, U, V, W + 延续) | `SC-01`, `ECB-01`, `BLOB-02`, `BAKE-01`, `CASE-39/40/41` |
| P2 | 3 (X, Y, Z) | `PRF-03`, `PRF-34`, `BUR-01` |
