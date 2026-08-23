# AutoChess 真实业务链二轮审查事实

> Owner：`00-当前架构事实` | 审查日期：2026-08-24 | 范围：`Assets/AutoChessDemo` + 实际 Generated Catalog + 相关 GAS Runtime | 状态：当前代码事实

## 结论

当前 AutoChessDemo 能跑通“创建单位、授予 Ability、选目标、攻击/斩杀/中毒、投影部分 fact、产生胜负与日志”的 legacy 链。它还没有证明 Runtime v1 所需的 Ready、请求终态、cost/cooldown、wait/cancel、immunity/inhibition、Tag count、Death authority、Cue 四阶段、FinalDrain 和真实 Unity Test 闭环。

本页只记录现实代码和生成物；目标业务语义由 `01` 目录维护，实施顺序由 `02` 目录维护。

## 实际端到端链

| 阶段 | 当前实现事实 | 当前失败/可见性事实 |
|---|---|---|
| Session / Spawn | `AutoChessBattleFlow` 打开 Runtime/Session，`AutoChessBattleSession` 逐单位创建 ASC，最后创建 Driver | 没有 Pending -> Ready 状态，也没有整批 all-or-nothing admission |
| Initialize / Grant | lifecycle adapter 只初始化 Health/Energy，再逐个 grant `CreateAbilityCodes()` | `RequestInitialize(...)` 返回值被忽略；grant/catalog/容量失败可静默留下部分初始化单位 |
| Target Select | Driver 只在同 `BattleGroup` 内选存活敌人，按 ActiveDue > Finisher > Primary 选 Ability | 目标破平先比 Slot，再比 raw `Entity.Index/Version` |
| Activate / Commit | Driver 写 `AbilityCommandBuffer`，Runtime resolve Ability Entity 并进入 Commit | command 无 RequestId/CommandId；无每请求恰好一个 success/typed reject 终态；失效 target 会 fallback 到 owner/self |
| Effect / Execution | 9101/9102 通过 9201/9202 固定扣血；9103 通过 9207 + Demo 自定义 execution system 斩杀 | 9101-9104 的 cost/cooldown/requirement 均为空或 0，不覆盖真实 Commit 资源语义 |
| Period / Stack | 9104 施加 9203，period child 9204 扣血 | 9203 配置与生成 overlay 含多个异常；9204 固定 `-1`，不乘 StackCount |
| Death / Winner | Driver 和 Snapshot 以 `Health > 0` 推导 Alive，Report 以 Health 跨 0 推导 Death | 没有 Runtime Dead authority；Driver 存活统计早于 Core damage，致死通常到下一 tick 才影响胜负 |
| Cue / Report / Replay | GE apply 会产生部分 fact/CueRequested，Report projector 派生斩杀和 Health 减少 | 只证明 OnApply/Cue count，未证明 OnActive/WhileActive/Executed/Removed 或真实 managed Cue |
| Terminal / Teardown | 胜负后固定再 tick，`Complete()` 生成 Result，后续 `Dispose()` 才 Close Session 并跑 cleanup ticks | Result 不包含 Close/物理删除/cleanup audit；post-victory 仍是完整 gameplay tick |

## Spawn、Initialize 与 Grant

1. `AutoChessBattleSession` 遍历场景单位并调用 `CreateBattleUnit()`，然后创建 Driver：`Assets/AutoChessDemo/Battle/AutoChessBattleSession.cs:22-32`。
2. `AutoChessGasBattleEntityLifecycle.CreateBattleUnit()` 创建 command port 失败时返回 default；调用 `RequestInitialize(...)` 后不检查 bool：`Assets/AutoChessDemo/Integration/GasCore/AutoChessGasBattleEntityLifecycle.cs:14-46`。
3. 初始化只写 Health/Energy，无 base tags；后续 grant `definition.CreateAbilityCodes()` 返回的 Ability codes。
4. granted Ability 当前是 ECB 创建的 Ability Entity，创建和首次 Ability query 之间依赖后续 tick：`Assets/GAS/Runtime/System/ASCCommandBufferResolveSystem.cs:505-569`。
5. `_nextBattleUnitKey` 是进程级静态自增值，不是配置中的稳定业务身份：`Assets/AutoChessDemo/Integration/GasCore/AutoChessGasBattleEntityLifecycle.cs:10,115-124`。
6. OpenSession 后仅真实执行一次 Runtime tick；`WarmupRuntimeTicks=3` 的其余部分是后续 gameplay tick 不计时，不是初始化 Ready barrier：`Assets/AutoChessDemo/Battle/Flow/AutoChessBattleFlow.cs:8,69-82,151-158`。

## AI、目标与请求结果

1. AI 当前优先级是 ActiveDue -> Finisher -> Primary：`Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleCommandDriveSystem.cs:336-382`。
2. 选目标限制在同 `BattleGroup`，破平使用 Slot -> raw `Asc.Index` -> raw `Asc.Version`：`Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleCommandDriveSystem.cs:385-423,466-489`。
3. `AbilityCommandBuffer` 只携带 Owner/AbilityEntity/TargetAsc/AbilityCode/CommandType，无 RequestId：`Assets/GAS/Runtime/Ability/Component/Dynamic/AbilityCommandBuffer.cs:14-21`。
4. Driver 的 issued counters 在意图入队时增加，不代表 Activate/Commit 成功：`Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleCommandDriveSystem.cs:325-332`。
5. `AbilityCommitSystem.ResolveMainTarget()` 对不可用 requested target 返回 fallback owner：`Assets/GAS/Runtime/System/Ability/AbilityCommitSystem.cs:521-527`。因此 hostile intent 可在 target 失效时变成 self-target。
6. Runtime 枚举虽含 Commit success/failure kind，当前主链没有为每条 AutoChess command 输出唯一终态 outcome；无效命令还可在 resolve 中静默清除。

## 实际 9101-9104 / 9201-9207 配置

### Ability 覆盖面

- 9101：己方 primary attack -> 9201。
- 9102：敌方 primary attack -> 9202。
- 9103：finisher -> 9207。
- 9104：active poison -> 9203。
- 这些 Ability 的 `CostGameplayEffectCode=0`、`CooldownGameplayEffectCode=0`、`CooldownFrames=0`，requirement count 为 0：`Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs:145-208`。

所以当前真实主战不能证明 affordability、cooldown、cancel/wait、immunity/inhibition、grant removal、multi-target、Exact/Inclusive Tag 或 cleanup 语义。

### 9203 多源语义异常

1. Luban JSON 实际输出 `Duration=8`、`Period=2`、`FirstTrigger=false`、`LimitCount=3`，但同时输出 `StackingType=9203`、`StackCode=0`：`Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplayeffect.json:403-440`。
2. 合法 `EffectStackType` 只有 `AggregateBySource=0` 与 `AggregateByTarget=1`：`Assets/GAS/Runtime/Effect/Component/Static/GEStackingComponents.cs:5-10`。`9203` 是非法 enum 值。
3. Generated Catalog 原样携带 `StackType=9203`：`Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs:684-711`。
4. 当前 ActiveEffect refresh matcher 并不消费 `StackType`，实际匹配键是 GE code + target + source ASC + source Ability Entity：`Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs:3408-3424`。因此非法配置没有在当前验证中暴露。
5. `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/exgas.sourcegen.json` 又对 9203：
   - 追加一个 target Energy / `SourceAttribute` modifier；
   - 覆盖 duration refresh 为 `NeverRefresh`；
   - 覆盖 expiration 为 `RemoveSingleStackAndRefreshDuration`。
6. Generated Catalog 消费了 overlay 后的值；这说明当前 Luban row 与 sourcegen sidecar 都在改变 gameplay 语义。

### 9204 与 9207

- 9204 是 Instant `Health -1`：`Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs:712-741`。当前 period command 不把 StackCount 投影为倍率，所以 1/2/3 层不会自动变成 1/2/3 倍跳伤。
- 9207 当前是 `DurationFrames=-1`、无 modifier 的 ActiveEffect marker：`Assets/GAS/Generated/CodeGen/Runtime/DefinitionCatalog.gen.cs:742-770`。`AutoChessExecuteDamageCalculationSystem` 扫描 9207 spec，以目标 Health BaseValue 计算处决伤害：`Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs:125-250,325-360`。

## Period / Overflow 当前 tick 事实

`GASActiveEffectPreTickSystem` 在当 tick 较早阶段为 due period 写入 GE command，同 tick 后续还会执行 Normalize -> SpecBuild -> DeltaApply。因此：

- owner-local period due 伤害在 `DueTick` 同 tick 生效：`Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs:2131-2188,2767-2864`、`Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs:233-252`。
- ActiveMutation apply/overflow 阶段动态生成的 child 已错过本 tick Normalize/Spec，进入 next-frame owner-local buffer，下一 tick 处理。
- 当前 tick 先处理 period，再处理 duration expiration：`Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs:2131-2257`。

## Death、胜负与终局当前事实

1. 当前无 `State.Dead` 或独立 Death authority。Driver 在下一次 command pass 用 `Health <= 0` 跳过单位：`Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleCommandDriveSystem.cs:236-241,410-414`。
2. Driver 的 alive stats 在 CommandResolve 阶段计算，当 tick 伤害在后续 Core 发生；`AutoChessBattleFlow.UpdateVictoryState()` 因此通常下一 tick 才看到致死结果：`Assets/AutoChessDemo/Battle/Flow/AutoChessBattleFlow.cs:194-207`。
3. `AutoChessBattleReportBuilder` 通过 Health 跨 0 推导 UnitDied，不是消费 Runtime Death transition：`Assets/AutoChessDemo/Battle/Report/AutoChessBattleReportBuilder.cs:40-52,75-106`。
4. Generated validation scenario 的 `PostVictoryFlushTicks=4`；它们是四个完整 gameplay tick，不是只排空 Boundary/cleanup：`Assets/AutoChessDemo/Generated/AutoChessGeneratedConfig.gen.cs:231-248`。
5. `AutoChessBattleFlow.Complete()` 先构建 Result，`AutoChessBattleManager` 的 `finally` 才调用 Dispose；Dispose 中 Session.Close 后还执行 cleanup ticks：`AutoChessBattleFlow.cs:97-149,225-229`、`Assets/AutoChessDemo/Battle/AutoChessBattleManager.cs:8-26`。
6. 因此当前 Result 在 Session Close、ASC physical destroy 和 cleanup audit 之前已冻结。

## Cue、Report、Scale 与验证当前事实

1. Active/Instant GE 当前主要投影单个 `GameplayCueCode` 的 OnApply fact；Boundary 请求中 `CueEntity=Entity.Null`，且 Cue bridge/lifecycle 没有进入实际 schedule。详见 [Runtime v1 不可兼容迁移基线事实](RuntimeV1不可兼容迁移基线事实.md#cue-当前是断链)。
2. `AutoChessGasBattleReportFactProjector` 只识别处决与 Health reduction：`Assets/AutoChessDemo/Integration/GasCore/AutoChessGasBattleReportFactProjector.cs:29-79`。
3. x50 当前是 50 个互相隔离的 4 单位 BattleGroup，合计 200 单位：`Assets/AutoChessDemo/GameRoom/AutoChessGameRoomDefinition.cs:328-344`。它不是单 hot target fan-in。
4. `RunRepeatRunCleanupProbe()` 连续跑两次，但比较的主要是 issued command、attribute/execution/period/cue counts：`Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationRun.cs:558-620`。
5. `HasRequiredRuntimeChain()` 以若干 count > 0 和 boundary key 作为通过条件，没有证明 request outcome、commit、stack 精确伤害、Death、Cue lifecycle 或 teardown：`Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationRun.cs:800-823`。
6. 当前 `FactsHash` 混入 frame/sequence/report key/raw `SourceAbility.Index`/`GameplayEffect.Index`；`SummaryHash` 又混入 Journaling 记录数和 presentation marker count：`Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs:1066-1094,1149-1169`。
7. Presentation 日志有行数截断/折叠，不是完整语义事实源：`Assets/AutoChessDemo/Battle/AutoChessBattleLog.cs:62-103,142,233,265`。

## 真实测试源缺口

- 当前 `Assets/_Test` 不存在。
- tracked `Assets` 下没有 GAS/AutoChess `NUnit` / `[TestFixture]` 测试源。
- `com.exhard.exgas.runtime.tests.csproj` 中的测试投影不能替代 Unity Test Runner 真实发现与运行。
- 历史 AutoChess log、count threshold、Profiler/Journaling summary 只能作当前特征化输入，不是 Runtime v1 语义通过证明。

## 本页不能推导的结论

1. 当前 count > 0 不能推导 Activate/Commit/Effect/Cue 终态恰好一次。
2. x50 隔离分组不能推导 hot-target、period burst、mass death、Boundary burst 或 wait fanout 已通过。
3. 9203 当前能跑不能推导 StackType/overlay 合法；恰恰相反，非法值因 Runtime 未消费而被隐藏。
4. Health 跨 0 的 report 推导不能推导同 tick Death/Cancel/Cleanup authority 已实现。
5. Result 成功返回不能推导 teardown 已被审计；当前顺序正好是 Result 先于 Close/cleanup。
