# Performance Budget

| Metric | Value |
|---|---:|
| measured ticks | 9 |
| avg tick ms | 1.057 |
| GAS tick avg ms | 1.041 |
| CoreRuntimeOwner avg ms | 0.709 |
| CoreSimulation avg ms | 0.571 |
| Boundary avg ms | 0.268 |
| Runner avg ms | 0.065 |
| Debugger avg ms | 179.795 |
| performance observation pollution risks | 0 |
| profiler evidence passed | False |

## Cost Split

| Group | avg ms | share |
|---|---:|---:|
| FramePrepare | 0.040 | 3.8% |
| CommandResolve | 0.083 | 8.0% |
| CoreSimulation | 0.571 | 54.9% |
| StructuralCommit | 0.014 | 1.3% |
| BoundaryProjection | 0.268 | 25.7% |
| RunnerOwner | 0.065 | 6.2% |
| DebuggerOwner | 179.795 | outside tick |
