# Debugger Evidence

diagnostics source: GasRuntimeDiagnosticEvidenceSnapshot
metric family source: DiagnosticPassGasData+PerformancePassOverhead
scorecard metric mask: 0x7F
dominant risk: DependencyWait

| Metric | Value |
|---|---:|
| events | 138 |
| warnings | 58 |
| errors | 0 |
| blocking errors | 0 |
| observation materialized queries | 12 |
| observation materialized entities | 2,400 |
| observation materialization us | 121 |
| performance observation pollution risks | 0 |
| owner local fact flushes | 5,200 |
| owner local fact max range | 9 |
| owner local fact changed chunks | 2,400 |
| owner local fact scanned owners | 2,400 |
| owner local fact dirty owners | 1,400 |
| owner local fact skipped owners | 1,000 |
| owner local fact cleared owners | 1,400 |
| owner local fact skip rate | 41.7% |
| instant prepare scanned owners | 2,400 |
| instant prepare skipped owners | 1,450 |
| instant prepare dirty owners | 950 |
| instant prepare promoted commands | 0 |
| active mutation prepare scanned owners | 2,400 |
| active mutation prepare skipped owners | 1,750 |
| active mutation prepare dirty owners | 650 |
| active mutation prepare promoted commands | 0 |
| active effect pre-tick scanned owners | 2,400 |
| active effect pre-tick processed owners | 600 |
| active effect pre-tick skipped owners | 1,800 |
| active effect pre-tick scanned slots | 750 |
| active effect pre-tick due slots | 550 |
| active effect pre-tick mutation writes | 600 |

## Findings

### GAS-ARCH-01 (High)

- evidence: Journaling RW reads are 8,988.2/tick: GetComponentDataRW=2,245.6/tick, GetBufferRW=6,742.7/tick.
- next probe: Split TopN by system and add per-system chunk/entity match counters; prove which paths can move to IJobChunk or owner-local dirty sets.

### GAS-ARCH-02 (High)

- evidence: Enableable toggles are 72.2/tick: Enable=72.2/tick, Disable=0.0/tick.
- next probe: Measure toggle count by component and phase; replace repeated commit/end toggles with a compact command queue state where semantics allow it.

### GAS-ARCH-04 (High)

- evidence: GAS.Runtime.OwnerLocalGameplayFactBuffer is a hot component RW target: 11,250 records, 1,250.0/tick.
- next probe: Add per-owner dirty counters and prove which TopN component paths can become chunk-local or fixed-width metric streams.

### GAS-ARCH-07 (High)

- evidence: OwnerLocalFact max owner range is 9; owner-local facts flush 577.8/tick.
- next probe: Add dirty owner/fact span counters and move OwnerLocalGameplayFactBuffer writes behind a generated fact reduce/apply lane.

### R7-INSTANT-PREPARE-SPARSE-SCAN (Medium)

- evidence: OwnerLocalInstant prepare scans 2,400 owners but skips 1,450 (60.4%); promoted commands=0.
- next probe: Target generated dirty-owner lanes or owner-local command spans; do not reintroduce enableable marker toggles.

### R7-ACTIVE-MUTATION-PREPARE-SPARSE-SCAN (Medium)

- evidence: ActiveMutation prepare scans 2,400 owners but skips 1,750 (72.9%); promoted commands=0.
- next probe: Use owner-local dirty spans or generated active-effect mutation fan-in before changing query gates.

### GAS-ARCH-06 (High)

- evidence: Execution scans 1,350 specs for 350 matched effects, ratio=3.86:1.
- next probe: Generate a calculation-code to effect-spec index and compare spec scan count against matched effect count in the next run.

### GAS-DBG-01 (High)

- evidence: DebuggerOwner avg is 179.795 ms for 2,400 materialized entities and 12 queries; GASTick avg is 1.041 ms.
- next probe: Keep performance pass counter-only, and split DiagnosticMaterializationPass/export into an explicitly budgeted post-run owner.

### GAS-MEASURE-02 (Medium)

- evidence: Profiler evidence is missing: performanceExcellentPassed=False, dominantRisk=DependencyWait.
- next probe: Add an explicit profiler-enabled validation mode or record a hard reason why batchmode Profiler is unavailable on this machine.
