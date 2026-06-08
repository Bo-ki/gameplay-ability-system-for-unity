# AutoChess Profile Brief

Source: `E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\TestResults\AutoChess\Headless\AutoChessHeadlessValidation-DebuggerProbe-20260608-Run8-FrameLaneCounters.txt`

## Decision

- passed: True
- headless logic budget: True
- performance excellent: False
- dominant risk: DependencyWait
- profiler state: profiler disabled; Entities profiler modules collect no data

## Budget Snapshot

- avg tick: 1.217 ms
- GAS tick avg: 1.201 ms
- CoreSimulation avg: 0.726 ms
- Boundary avg: 0.273 ms
- Debugger avg: 115.012 ms diagnostic-only

## R3 Owner-Local Fact Lane

- changed chunks / tick: 266.7
- scanned owners / tick: 266.7
- dirty owners / tick: 155.6
- skipped owners: 1,000 (41.7%)
- facts / dirty owner: 3.71

## R7 Frame Lane Counters

- instant prepare scanned/skipped/dirty owners: 2,400/1,450/950
- instant prepare promoted commands: 0
- active mutation prepare scanned/skipped/dirty owners: 2,400/1,750/650
- active mutation prepare promoted commands: 0
- active effect pre-tick owners scanned/processed/skipped: 2,400/950/1,450
- active effect pre-tick slots scanned/due/noop: 1,200/550/650 (45.8% due)

## Top Next Owners

| Next owner | Evidence |
|---|---|
| R4 | R4-DBG-MATRIX / runtimeDataOrientedScorecard+JournalingTopN / 88,744 / ManualInference |
| R7/R3 | GAS-ARCH-AE-PRETICK / ActiveGameplayEffectBuffer+ActiveEffectMutationBuffer / 15,800 / PerFrameSlotScan |
| R7/R3 | GAS-ARCH-AE-MUTATION-PREPARE / ActiveEffectMutationBuffer / 13,000 / PerFrameBufferClearCopy |
| R3 | GAS-ARCH-07 / OwnerLocalGameplayFactBuffer / 11,250 / BroadBufferRW+OwnerLocality |
| R4/R3 | GAS-ARCH-STREAM-RW / GEEffectCommandStreamComponent / 10,555 / SingletonStreamRW |
| R7/R3 | GAS-ARCH-CMD-PREPARE / GEEffectCommandBuffer+GESetByCallerValueBuffer / 9,000 / PerFrameBufferClearCopy |
| R3 | GAS-ARCH-ATTR-RW / AttributeValueBuffer / 8,250 / BroadBufferRW |
| R3 | GAS-ARCH-07 / OwnerLocalGameplayFactBuffer / 5,200 / OwnerLocality |

## Sub Reports

- PerformanceBudget: `E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\TestResults\AutoChess\Analysis\DebuggerProbe-20260608-Run8-FrameLaneCounters\SubReports\PerformanceBudget.md`
- HotspotAttribution: `E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\TestResults\AutoChess\Analysis\DebuggerProbe-20260608-Run8-FrameLaneCounters\SubReports\HotspotAttribution.md`
- JournalingTopN: `E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\TestResults\AutoChess\Analysis\DebuggerProbe-20260608-Run8-FrameLaneCounters\SubReports\JournalingTopN.md`
- DebuggerEvidence: `E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\TestResults\AutoChess\Analysis\DebuggerProbe-20260608-Run8-FrameLaneCounters\SubReports\DebuggerEvidence.md`
- RuntimeBattleLog: `E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\TestResults\AutoChess\Analysis\DebuggerProbe-20260608-Run8-FrameLaneCounters\SubReports\RuntimeBattleLog.md`
