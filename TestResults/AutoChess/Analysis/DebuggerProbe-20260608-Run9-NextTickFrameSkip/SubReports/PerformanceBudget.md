# Performance Budget

| Metric | Value |
|---|---:|
| measured ticks | 9 |
| avg tick ms | 3.650 |
| GAS tick avg ms | 3.627 |
| CoreRuntimeOwner avg ms | 2.532 |
| CoreSimulation avg ms | 2.311 |
| Boundary avg ms | 0.953 |
| Runner avg ms | 0.141 |
| Debugger avg ms | 62.466 |
| performance observation pollution risks | 0 |
| profiler evidence passed | False |

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
