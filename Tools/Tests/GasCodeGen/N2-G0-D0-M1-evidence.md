# N2-G0-D0-M1 物理可见性实验双跑证据

> 当前状态：正式串行双跑完成；两轮均为 10/11，唯一失败 `U-08`。该失败稳定证明 Unity 会写入 local-directory UPM slot，因此此 selector 路线被否决。`D1Authorized=false`，不得把本轮解释为 D1 已授权。
>
> 正式执行日期：2026-08-30

## 正式双跑

- runner：[Run-N2-G0-D0-M1.ps1](D0M1/Run-N2-G0-D0-M1.ps1)
- harness SHA-256：`e7a0b6bef7d621981f12f39d1b5df05f412856761148694b2769d215c0f75938`
- deterministic evidence SHA-256：两轮均为 `d9d4321436a0513c10dc981b1e2aff412b0748e14932c5195446181040a0a6ff`
- [run1](../../../TestResults/GasCodeGen/N2-G0-D0-M1.run1.json)：文件 SHA-256 `d31854a9fe83fbde8dd186c31fc011b33a6fd36f17410b29631393e00d7b398c`
- [run2](../../../TestResults/GasCodeGen/N2-G0-D0-M1.run2.json)：文件 SHA-256 `ad5d36dba8941cc5fe9b471c2567f8f955acf2780b47538f17d23619a5853673`
- 两轮均为 `Expected=11`、`Total=11`、`Passed=10`、`Failed=1`、`ExactCaseIdSet=true`；唯一失败项均为 `U-08`。
- 两轮 `TestedProcessExitCode=1`、`OverallPassed=false`、`PostFinalWriteConfirmed=false` 是候选路线被否决后的预期结果，不是 harness 执行故障。

## 决定性观察

- `M-01/M-02`：materializer 在移走旧安装根后被强杀会留下 Missing；随后直接启动 Unity 能在任何项目 C# 恢复钩子前观察到该状态，因此 pre-start materializer 存在技术旁路。
- `U-04`：并发 selector reader 两轮均同时观察到完整 generation A/B，且无 invalid hash、无重试耗尽；单文件 manifest replace 本身没有暴露混合字节。
- `U-07`：同一 Unity 进程完成 B→A selector replace、UPM resolve、脚本域重载与三程序集同代验证，未观察到 marker/source 混代。
- `U-08`：两轮均捕获 80 条 generation slot 写事件（40 `Changed`、20 `Created`、20 `Deleted`），目标为 Unity 创建的 `~UnityDirMonSyncFile~...`；即使 generation A/B 的首尾树 SHA-256 分别保持一致，也不满足物理不可变要求。
- `U-09`：Slots 与 Packages 两个 watcher 的被保护路径 mutation event 均为 0、watcher error 均为 0，positive control 各 6。Slots watcher 的 4 条原始事件均只指向 `.watcher-control`，Packages watcher 原始事件为 0；缺槽来源 gate 按预期 fail closed。

## 卫生与裁决

- 两轮均为 `Cleanup=Passed`、`FixtureRoot=<deleted>`，无 cleanup failure，也没有遗留 `gas-codegen-d0-m1-*` 临时根。
- 真实项目协议范围 before/after SHA-256 均为 `2a8885230a1597a0c4217d81e64c31179f7fcb836aaa1683d32afedd087d57c2`，`Unchanged=true`。
- aggregate writer 使用精确 unlink、`CreateNew`、单链接句柄校验和逐次 SHA-256/长度读回；parent-directory handle/no-follow 的同主体并发路径替换仍保留为 P2，不扩大本轮保证。
- `MaterializerRoute=RejectedByDirectUnityBypass`。
- `ImmutableUpmSelectorRoute=RejectedByUnityDirectoryMonitorMutation`。
- `D1Authorized=false`；剩余门禁是 D0-M2 immutable tarball selector 或 SourceGenerator physical experiment，随后仍需 ADR 人工确认。

