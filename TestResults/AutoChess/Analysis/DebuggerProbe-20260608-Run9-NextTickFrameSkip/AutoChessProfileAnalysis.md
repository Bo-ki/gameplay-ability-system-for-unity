# AutoChess x50 Runtime Profile Analysis

Source: `E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\TestResults\AutoChess\Headless\AutoChessHeadlessValidation-DebuggerProbe-20260608-Run9-NextTickFrameSkip.txt`

## Summary

- elapsed: 0.049s
- measured ticks: 9
- avg tick: 3.650 ms
- commands/tick: 116.7
- attributes/tick: 100.0
- cue requests/tick: 172.2
- performance excellent: False
- dominant risk: DependencyWait

## Cost Split

| Group | avg ms | share |
|---|---:|---:|
| FramePrepare | 0.073 | 2.0% |
| CommandResolve | 0.126 | 3.5% |
| CoreSimulation | 2.311 | 63.7% |
| StructuralCommit | 0.023 | 0.6% |
| BoundaryProjection | 0.953 | 26.3% |
| RunnerOwner | 0.141 | 3.9% |
| DebuggerOwner | 62.466 | outside tick |

## Journaling Rates

- GetComponentDataRW/tick: 2,245.6
- GetBufferRW/tick: 6,742.7
- Enableable toggles/tick: 72.2
- Journaling cap reached: False

## Debugger Evidence

- diagnostics source: GasRuntimeDiagnosticEvidenceSnapshot
- metric family source: DiagnosticPassGasData+PerformancePassOverhead
- scorecard metric mask: 0x7F
- scorecard core facts / active mutations: 5,200 / 200
- scorecard owner-local fact max range: 9
- owner-local fact dirty owners/tick: 155.6
- owner-local fact scanned/skipped owners: 2,400/1,000 (41.7%)
- instant prepare scanned/skipped owners: 2,400/1,450 (60.4%)
- active mutation prepare scanned/skipped owners: 2,400/1,750 (72.9%)
- active effect pre-tick processed owners/slots due: 600/550 (73.3%)
- events/warnings/errors: 138/79/0
- observation materialization: queries=12, entities=2,400, us=182
- execution spec scan ratio: 3.86:1
- owner local fact flushes/tick: 577.8

## Top Systems

| Operation | System | Count | Per measured tick |
|---|---|---:|---:|
| GetBufferRW | GAS.Runtime.OwnerLocalInstantCommandFramePrepareSystem | 9,000 | 1,000.0 |
| GetBufferRW | GAS.Runtime.ActiveEffectOwnerLocalMutationFramePrepareSystem | 9,000 | 1,000.0 |
| GetBufferRW | GAS.Runtime.GASActiveEffectPreTickSystem | 8,600 | 955.6 |
| GetBufferRW | GAS.Runtime.GASAttributeModifierDeltaApplySystem | 5,400 | 600.0 |
| GetBufferRW | GAS.Runtime.ASCCommandBufferResolveSystem | 4,550 | 505.6 |
| GetComponentDataRW | GAS.Runtime.GASActiveEffectPreTickSystem | 4,209 | 467.7 |
| GetBufferRW | GAS.Runtime.GASActiveEffectMutationApplySystem | 3,750 | 416.7 |
| GetComponentDataRW | GAS.AutoChessDemo.AutoChessExecuteDamageCalculationSystem | 3,609 | 401.0 |

## Top Components

| Operation | Component | Count | Per measured tick |
|---|---|---:|---:|
| GetBufferRW | GAS.Runtime.OwnerLocalGameplayFactBuffer | 11,250 | 1,250.0 |
| GetComponentDataRW | GAS.Runtime.GEEffectCommandStreamComponent | 10,555 | 1,172.8 |
| GetBufferRW | GAS.Runtime.AttributeValueBuffer | 7,050 | 783.3 |
| GetBufferRW | GAS.Runtime.GEEffectCommandBuffer | 4,650 | 516.7 |
| GetBufferRW | GAS.Runtime.ActiveEffectMutationBuffer | 4,000 | 444.4 |
| GetComponentDataRW | GAS.Runtime.PendingAttributeModifierComponent | 3,600 | 400.0 |
| GetBufferRW | GAS.Runtime.AttributeModifierBuffer | 3,600 | 400.0 |
| GetBufferRW | GAS.Runtime.GESetByCallerValueBuffer | 3,350 | 372.2 |

## Hotspot Attribution Matrix

| ID | Severity | GAS concept | Phase | Lane | System | Buffer | Operation | Count | DOTS risk | Next owner |
|---|---|---|---|---|---|---|---|---:|---|---|
| R4-DBG-MATRIX | High | DebuggerEvidence | DebuggerOwner | HotspotAttribution | GasRuntimeDerivedExportSink | runtimeDataOrientedScorecard+JournalingTopN | DerivedExport | 81,544 | ManualInference | R4 |
| GAS-ARCH-AE-MUTATION-PREPARE | High | ActiveEffectMutation | GASFramePrepareSystemGroup | ActiveEffectMutationPrepare | ActiveEffectOwnerLocalMutationFramePrepareSystem | ActiveEffectMutationBuffer | GetBufferRW | 13,000 | PerFrameBufferClearCopy | R7/R3 |
| GAS-ARCH-07 | High | GameplayFact | GASBoundaryProjectionSystemGroup | OwnerLocalFactDirtySpan | GameplayBoundaryFactExportSystem | OwnerLocalGameplayFactBuffer | GetBufferRW | 11,250 | BroadBufferRW+OwnerLocality | R3 |
| GAS-ARCH-STREAM-RW | High | EffectCommandStream | GASCoreSimulationSystemGroup | StreamSequenceAllocation | GEEffectCommandSpecStream | GEEffectCommandStreamComponent | GetComponentDataRW | 10,555 | SingletonStreamRW | R4/R3 |
| GAS-ARCH-CMD-PREPARE | High | CommandFanIn | GASFramePrepareSystemGroup | OwnerLocalInstantCommandPrepare | OwnerLocalInstantCommandFramePrepareSystem | GEEffectCommandBuffer+GESetByCallerValueBuffer | GetBufferRW | 9,000 | PerFrameBufferClearCopy | R7/R3 |
| GAS-ARCH-AE-PRETICK | High | ActiveEffectLifecycle | GASCoreSimulationSystemGroup | ActiveEffectPreTick | GASActiveEffectPreTickSystem | ActiveGameplayEffectBuffer+ActiveEffectMutationBuffer | GetBufferRW | 8,600 | PerFrameSlotScan | R7/R3 |
| GAS-ARCH-ATTR-RW | High | AttributeState | GASCoreSimulationSystemGroup | AttributeDeltaApply | GASAttributeModifierDeltaApplySystem | AttributeValueBuffer | GetBufferRW | 7,050 | BroadBufferRW | R3 |
| GAS-ARCH-07 | High | GameplayFact | GASBoundaryProjectionSystemGroup | OwnerLocalFactDirtySpan | GameplayBoundaryFactExportSystem | OwnerLocalGameplayFactBuffer | OwnerLocalFactFlush | 5,200 | OwnerLocality | R3 |
| GAS-DBG-01 | High | DebuggerObservation | DebuggerOwner | DiagnosticMaterialization | DiagnosticsSnapshotSystem | ToEntityArray | Materialization | 2,412 | ObservationMaterialization | R4 |
| GAS-ARCH-06 | High | ExecutionCalculation | GASCoreSimulationSystemGroup | ExecutionSpecSelection | AutoChessExecuteDamageCalculationSystem | GEEffectSpecBuffer | SpecScan | 1,350 | FanOutScan | R5/R3 |
| GAS-ARCH-04 | High | AttributeFactFanIn | GASCoreSimulationSystemGroup | PendingAttributeDeltaApply | GASAttributeModifierDeltaApplySystem | OwnerLocalGameplayFactBuffer | OwnerLocalFactAppend | 350 | BroadBufferRW | R3 |
| GAS-ARCH-ActiveEffect | High | ActiveEffectLifecycle | GASCoreSimulationSystemGroup | ActiveEffectPreTick | GASActiveEffectPreTickSystem | ActiveGameplayEffectBuffer | ActiveSlotTick | 100 | PerFrameSlotScan | R7/R3 |
| GAS-ARCH-01 | High | RuntimeCoreApiHealth | AllRuntimeGroups | DependencyAndSync | GASDependencyDrain | ComponentLookup+BufferLookup | DependencyWait | 17 | DependencyWait | R4 |
| GAS-MEASURE-02 | Medium | OfficialToolEvidence | Validation | ProfilerCapture | GasRuntimeOfficialToolDiff | Profiler | ProfilerEvidence | 1 | MissingProfilerEvidence | R8 |
| GAS-MEASURE-02 | Medium | OfficialToolEvidence | Validation | ProfilerCapture | GasRuntimeOfficialToolDiff | Profiler | ProfilerEvidence | 1 | MissingProfilerEvidence | R8 |

## Reverse-Inferred Architecture Mistakes

### GAS-ARCH-01 (High)

- evidence: Journaling RW reads are 8,988.2/tick: GetComponentDataRW=2,245.6/tick, GetBufferRW=6,742.7/tick.
- inference: The hot path is still shaped around random entity/buffer lookup instead of chunk-local or owner-local batches.
- design mistake: Runtime Core modules are too shallow around ability commit, cleanup, and attribute recalculation; callers still pay ECS lookup details every tick.
- next probe: Split TopN by system and add per-system chunk/entity match counters; prove which paths can move to IJobChunk or owner-local dirty sets.

### GAS-ARCH-02 (High)

- evidence: Enableable toggles are 72.2/tick: Enable=72.2/tick, Disable=0.0/tick.
- inference: The data is not suffering from structural changes; it is suffering from a per-command enableable-state protocol.
- design mistake: Ability lifecycle is encoded as too many request/commit/end marker flips instead of a compact per-owner command state or chunk batch.
- next probe: Measure toggle count by component and phase; replace repeated commit/end toggles with a compact command queue state where semantics allow it.

### GAS-ARCH-04 (High)

- evidence: GAS.Runtime.OwnerLocalGameplayFactBuffer is a hot component RW target: 11,250 records, 1,250.0/tick.
- inference: The runtime still pays high buffer RW volume even when structural changes are near zero.
- design mistake: Owner-local stores exist, but the execution/attribute/fact path still exposes broad buffer surfaces to too many systems.
- next probe: Add per-owner dirty counters and prove which TopN component paths can become chunk-local or fixed-width metric streams.

### GAS-ARCH-07 (High)

- evidence: OwnerLocalFact max owner range is 9; owner-local facts flush 577.8/tick.
- inference: The fact path has owner-local storage, but the hot range is still too broad for a strict chunk-local DOTS shape.
- design mistake: Fact fan-in is represented as mutable buffers with repeated owner flushes instead of a compact dirty-owner reduction lane.
- next probe: Add dirty owner/fact span counters and move OwnerLocalGameplayFactBuffer writes behind a generated fact reduce/apply lane.

### R7-INSTANT-PREPARE-SPARSE-SCAN (Medium)

- evidence: OwnerLocalInstant prepare scans 2,400 owners but skips 1,450 (60.4%); promoted commands=0.
- inference: The instant command frame lane is sparse at owner granularity.
- design mistake: The current prepare system has owner-local buffers but no compact dirty owner read model for frame-local promotion work.
- next probe: Target generated dirty-owner lanes or owner-local command spans; do not reintroduce enableable marker toggles.

### R7-ACTIVE-MUTATION-PREPARE-SPARSE-SCAN (Medium)

- evidence: ActiveMutation prepare scans 2,400 owners but skips 1,750 (72.9%); promoted commands=0.
- inference: The active-effect mutation frame lane is sparse at owner granularity.
- design mistake: The prepare system still pays broad owner scans for clear/copy even when mutation promotion work is zero.
- next probe: Use owner-local dirty spans or generated active-effect mutation fan-in before changing query gates.

### GAS-ARCH-06 (High)

- evidence: Execution scans 1,350 specs for 350 matched effects, ratio=3.86:1.
- inference: The execution-calculation path still has fan-out scan cost that grows faster than matched work.
- design mistake: Execution definitions are not yet indexed as a compact generated lookup matched to the active gameplay effect command stream.
- next probe: Generate a calculation-code to effect-spec index and compare spec scan count against matched effect count in the next run.

### GAS-DBG-01 (High)

- evidence: DebuggerOwner avg is 62.466 ms for 2,400 materialized entities and 12 queries; GASTick avg is 3.627 ms.
- inference: The debugger can locate gameplay hot spots, but its diagnostic materialization/export pass is itself a large one-shot cost.
- design mistake: Runtime diagnostics still mix validation evidence, materialization, and text-export obligations too closely.
- next probe: Keep performance pass counter-only, and split DiagnosticMaterializationPass/export into an explicitly budgeted post-run owner.

### GAS-MEASURE-02 (Medium)

- evidence: Profiler evidence is missing: performanceExcellentPassed=False, dominantRisk=DependencyWait.
- inference: Runtime self-diagnostics and Journaling are enough to find current hot spots, but the official Profiler leg is not enabled.
- design mistake: The validation contract now distinguishes headless budget pass from DOTS-profiler-backed performance excellence, but the runner still defaults to disabled Profiler.
- next probe: Add an explicit profiler-enabled validation mode or record a hard reason why batchmode Profiler is unavailable on this machine.
