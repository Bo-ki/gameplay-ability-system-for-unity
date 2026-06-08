# Performance Budget

| Metric | Value |
|---|---:|
| measured ticks | 9 |
| avg tick ms | 1.217 |
| GAS tick avg ms | 1.201 |
| CoreRuntimeOwner avg ms | 0.874 |
| CoreSimulation avg ms | 0.726 |
| Boundary avg ms | 0.273 |
| Runner avg ms | 0.053 |
| Debugger avg ms | 115.012 |
| performance observation pollution risks | 0 |
| profiler evidence passed | False |

## Cost Split

| Group | avg ms | share |
|---|---:|---:|
| FramePrepare | 0.040 | 3.3% |
| CommandResolve | 0.092 | 7.7% |
| CoreSimulation | 0.726 | 60.4% |
| StructuralCommit | 0.016 | 1.3% |
| BoundaryProjection | 0.273 | 22.7% |
| RunnerOwner | 0.053 | 4.4% |
| DebuggerOwner | 115.012 | outside tick |
