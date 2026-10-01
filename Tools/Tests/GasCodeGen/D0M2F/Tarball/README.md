# D0-M2F R1 immutable tarball probe

## 结论与边界

本目录只提供 `R1-Tarball` disposable harness，不选择路线、不修改 production 文件，也不拥有 Unity lease。`Run-D0M2F-TarballProbe.ps1` 只有在 root 完成 J0/J1 且取得唯一 Unity lease 后才可执行；R1 准备阶段只运行静态 checker。

`Archives~` 内 A/B `.tgz` 以自身 SHA-256 命名。运行时先深拷贝到 OS 临时目录，再将 archive 标为只读；fixture 和 archive copy 都拒绝 reparse point，普通文件要求单 hardlink。清理只接受精确临时根、固定名称和 owner sentinel，任一条件不满足即保留现场并输出 typed failure。

## Root 调用接口

```powershell
pwsh -NoProfile -File Tools/Tests/GasCodeGen/D0M2F/Tarball/Run-D0M2F-TarballProbe.ps1 `
  -Mode Feasibility `
  -UnityPath '<absolute Unity.exe>' `
  -OutputPath 'TestResults/GasCodeGen/N2-G0-D0-M2F/<RunId>/tarball.json' `
  -RunId '<RunId>'
```

`Feasibility` 固定执行并输出：

- `T-01`：manifest=A 的 direct cold Unity；
- `T-02`：manifest 原子切到 B 后，强杀前已观察 Runtime/Editor/AutoChess 同代 B；
- `T-03`：强杀该 Unity 进程树并 direct cold restart，仍消费 B；
- `T-04`：manifest/archive/lock/PackageCache/unpack 的 Authority/Derived/Cache 角色闭合。

只有 root 将 tarball 选为 provisional candidate 后，才允许调用：

```powershell
pwsh -NoProfile -File Tools/Tests/GasCodeGen/D0M2F/Tarball/Run-D0M2F-TarballProbe.ps1 `
  -Mode SelectorConflict `
  -UnityPath '<absolute Unity.exe>' `
  -OutputPath 'TestResults/GasCodeGen/N2-G0-D0-M2F/<RunId>/selector-conflict.json' `
  -RunId '<RunId>'
```

此模式在 fixture 内构造 `ActiveGenerationRef=A`、`manifest=B`，固定输出 `X-01..X-04`；它不会写 production `ProjectSettings`。`-KeepFixture` 仅用于人工取证，默认安全清理。

## 输出与退出码

每次调用只在 `OutputPath` 写一个 `D0M2F-ProbeResult-v1` JSON，且拒绝覆盖旧文件。顶层 `Status/Reason` 与 R3 合同一致：

| Exit | Status | 含义 |
|---:|---|---|
| `0` | `Passed` | 本 probe 合同通过；不等于 D1 获授权 |
| `20` | `HardRejected` | T 路线语义被明确否决；root 仍应继续 S |
| `21` | `TimedOut` | time-box 超时 |
| `22` | `Inconclusive` | 证据或 selector 冲突结论不充分 |
| `23` | `Inconclusive` | harness/watcher failure |
| `24` | `Inconclusive` | cleanup failure，fixture 被保留 |
| `90` | 无法写 JSON | 输出路径本身不安全或不可写 |

脚本不使用 `-quit`；Unity Test Framework/executeMethod 由 fixture 入口显式退出。强杀窗口只终止本脚本刚启动的 Unity process tree。

## R1 静态验收

```powershell
pwsh -NoProfile -File Tools/Tests/GasCodeGen/D0M2F/Tarball/Test-D0M2F-TarballStatic.ps1
```

该命令不启动 Unity，只检查 PowerShell AST、archive content address/内部 token、必要 `.meta`，并在 R3 checker 存在时校验 T/X 最小合同样本。
