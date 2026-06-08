# OwnerLocal Pending Marker Rejected Run7

> 日期：2026-06-08
> 范围：GAS Runtime Core / owner-local command & mutation lane / AutoChess headless validation
> 状态：已撤回风险链路并归档

## 背景

Run5 / Run4 的 hotspot matrix 指向 `OwnerLocalInstantCommandFramePrepareSystem`、`ActiveEffectOwnerLocalMutationFramePrepareSystem` 和 `GASActiveEffectPreTickSystem` 的 `GetBufferRW` 热点。Run6 尝试用 `OwnerLocalInstantCommandPendingComponent` 与 `ActiveEffectMutationPendingComponent` 两个 enableable marker 收缩 FramePrepare 查询。

## Run6 反例

Run6 证明该方向不成立：

| 指标 | Run6 |
|---|---:|
| `passed` | false |
| `completed` | false |
| `avgTickMs` | 12.643ms |
| `GASTickTotal.avgMs` | 12.625ms |
| `CoreSimulation.avgMs` | 11.698ms |
| `journalingWorldRecords` | 505,543 |
| `EnableComponent` | 37,384 |
| `OwnerLocalInstantCommandFramePrepareSystem GetBufferRW` | 46,500 |

日志还出现多条 Unity Job safety / aliasing 异常，例如同一 job 同时持有 `ComponentTypeHandle<ActiveEffectMutationPendingComponent>` 与 `ComponentLookup<ActiveEffectMutationPendingComponent>`，以及多个系统对 `GEEffectCommandStreamComponent` 写依赖未正确串联。

结论：高频 enableable marker 把 buffer scan 成本转移成了 `SetComponentEnabled` / safety 依赖成本，不是 DOTS 数据形态优化。

## Run7 修正

Run7 直接移除两个 pending marker：

1. 删除 `OwnerLocalInstantCommandPendingComponent` / `ActiveEffectMutationPendingComponent` 类型。
2. ASC archetype 回到 38 个 core component。
3. `OwnerLocalInstantCommandFramePrepareSystem`、`ActiveEffectOwnerLocalMutationFramePrepareSystem`、`GEEffectCommandCatalogNormalizeSystem`、`GEEffectSpecBuildSystem` 不再依赖 marker 查询门控。
4. AbilityCommit、GameplayEffectRequestWriter、ActiveEffect pre-tick / mutation apply 不再高频 `SetComponentEnabled`。
5. `Verify-GAS-RuntimeCoreBoundary.ps1` 改为阻断这两个 marker 回流。

## Run7 验证

| 验证 | 结果 |
|---|---|
| `dotnet build .\com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` | 通过；仅既有 `MSB3277` warning |
| `dotnet build .\com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` | 通过；仅既有 `MSB3277` warning |
| `Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` | 通过 |
| Unity batch Run7b | 有效写出 summary；`passed=True`、`completed=True`、`winner=Player`、`blockingDebugErrors=0` |

Run7 关键指标：

| 指标 | Run7 |
|---|---:|
| `headlessLogicBudgetPassed` | true |
| `performanceExcellentPassed` | false |
| `avgTickMs` | 0.970ms |
| `GASTickTotal.avgMs` | 0.960ms |
| `CoreSimulation.avgMs` | 0.497ms |
| `BoundaryOwner.avgMs` | 0.293ms |
| `EnableComponent` | 650 |
| `journalingWorldRecords` | 87,426 |

证据路径：

1. Summary：`TestResults/AutoChess/Headless/AutoChessHeadlessValidation-DebuggerProbe-20260608-Run7-PendingMarkerRemoved.txt`
2. Brief：`TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run7-PendingMarkerRemoved/AutoChessProfileBrief.md`
3. 子报告：`TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run7-PendingMarkerRemoved/SubReports/*.md`
4. Unity log：`Temp/AutoChessBattleValidation-Run7b-PendingMarkerRemoved.log`

## 仍未解决

Run7 不是性能终局。hotspot matrix 仍指向：

1. `GASActiveEffectPreTickSystem=15800`
2. `ActiveEffectOwnerLocalMutationFramePrepareSystem=13000`
3. `OwnerLocalInstantCommandFramePrepareSystem=9000`
4. `OwnerLocalGameplayFactBuffer=11250`
5. `GEEffectCommandStreamComponent=9237`
6. `AttributeValueBuffer=8250`
7. `executionSpecScans=1350` / `executionMatchedEffectSpecs=350`
8. `Profiler disabled`

## 复发入口

如果后续重新引入 `OwnerLocalInstantCommandPendingComponent`、`ActiveEffectMutationPendingComponent` 或等价高频 enableable marker，必须重新打开 ISSUE-014 / ISSUE-003，并证明：

1. marker toggle 次数按 owner 去重；
2. `EnableComponent` 不进入 TopN；
3. 没有 Job safety / aliasing exception；
4. AutoChess x50 business chain、strict budget、repeat run 和 summary hash 均通过；
5. hotspot matrix 中对应 High row 下降，而不是转移到新的 DOTS API 风险。
