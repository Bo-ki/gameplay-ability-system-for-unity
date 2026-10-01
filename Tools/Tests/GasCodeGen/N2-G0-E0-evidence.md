# N2-G0-E0 隔离黑盒基线证据

> 当前状态：D 已精确恢复到冻结的 pre-D 控制面，schema v4 harness 的正式双跑已完成。两轮均为 10/10 Passed、被测进程 exit 0、cleanup Passed、`PostFinalWriteConfirmed=true`，且真实项目协议范围在 cases 与 aggregate 写入前后保持同一字节指纹。

> 正式执行日期：2026-08-30
>
> 被测入口：`GasCodeGenCli.exe --mode generation-verify|generation-recover`
>
> 结论：E0 当前协议基线通过；该结论不授权 D1、E1、`FullSemanticEligibility=true` 或 N2-1B

## 当前正式矩阵

以下结果描述 D 精确恢复后的 generation/ref/intent/descriptor E0。生产 CLI 只消费系统 temp 下构造的 synthetic boundary state，不执行 Unity、SourceGen 或 promotion。

- CLI Release 构建：0 warning / 0 error。
- 正式串行双跑：两次均为 10 Passed / 0 Failed，expected case count 为 10；被测进程 exit 0，`OverallPassed=true`。
- 两次均为 `Cleanup=Passed`、`PostFinalWriteConfirmed=true`，临时 suite 无残留。
- CLI SHA-256：`ecb1be4e2d293ae69bdd4c3f298c4994d82f0bf56ff9997c9395ed1c5ed957f2`。
- harness SHA-256：`757c98e3c2653a1faefb1bea8ab45e8b509a4f03c6422127e0952e91dd6bb5ff`。
- GenerationStore 源 SHA-256：`52a290924ae5a633bfea9cfdfe8e80d6092bfcfd79d78e0c1d9443b2cb26a959`。
- CLI Program 源 SHA-256：`ba89be6c5c05747e30195ce68722389399631b865e4458dac76443c5cecc2dc9`。
- 真实项目协议范围 before / after cases / after aggregate write：`064da69a1228bbfe19b7bb25b00f275583a48511dd9a2d9d44f5f12a69489351`；两轮各阶段均一致，`Unchanged=true`。
- run1 aggregate：`TestResults/GasCodeGen/N2-G0-E0.post-D-recovery.run1.json`，文件 SHA-256 `3902904b28b9dc5118f9f42742689d9c6f3b6bc571f5cf870241dd801bfe43c1`。
- run2 aggregate：`TestResults/GasCodeGen/N2-G0-E0.post-D-recovery.run2.json`，文件 SHA-256 `50b8371fadd71303f9c2bd3dc15306040da53463e8e2fc21bc4c018be865c65c`。
- 两个 aggregate 的原始文件 hash 因 `Inputs.OutputPath` 不同而不同；将该字段归一化为 `<OUTPUT>` 后，结构完全相等，归一化结构 SHA-256 均为 `f1c70086c46f3b9d02ce78690330dc82eb5468db0a169b11ad8f16e5baf25563`。

hardening 前的历史双跑只用于追溯，不再作为当前裁决输入。旧版 CLI/harness SHA-256 分别为 `1fa604b7a407d141418771b3c29818c98bfdd114ec8303cddc18c41c73e12fe8` 与 `8e6aefa82023c20edb039c0edb848617e69a489a4d98b00f7931058c176fe32f`；旧 aggregate hash 为 `82348060647928655b6747b587cad14df87bf008b87d5bac30ad90ef6d71bfeb`。

## 故障矩阵

| Case | 普通 verify | 显式 recover | 最终身份/证据 |
|---|---:|---:|---|
| `E0-01` 首发 intent、无 ref | 1，拒绝推断 legacy active | 0，按原 PromotionId 前滚 target | ref/marker/target 双树落定，intent 清理；重复 recover 不变 |
| `E0-02` 非首发 ref=previous、active=target | 0，open recovery 回滚 | 0，回滚 previous | previous ref/双树，intent 清理；重复 recover 不变 |
| `E0-03` 非首发 ref=target | 0，open recovery 前滚 | 0，前滚 target | target ref/双树，intent 清理；previous-ref `.bak` 不消费、不改写；重复 recover 不变 |
| `E0-04` initialized 后 ref 丢失 | 1 | 1 | marker 与 previous active 保留，不猜 LKG |
| `E0-05` active drift | 1 | 0，仅按 ref 修复 | previous ref/双树；verify 探针保留 drift 证据，重复 recover 不变 |
| `E0-06` descriptor 后向 immutable tree 注入文件 | 1 | 1 | ref/active 不变，注入文件保留，generation tree mismatch |
| `E0-07` intent 单独复用 previous 旧 tree 字段 | 1 | 1 | previous ref/active 与 intent 全部保留；只证明 intent binding |
| `E0-08` 正常 active 重复 recover/no-op | 0 | 0 | 第一次和重复 recover 后完整协议状态均与 before 相同 |
| `E0-09` 首发 target ref 已提交、intent/active 存在、无 marker | 0 | 0 | 创建 marker、清 intent；ref bytes 与 PromotionId 不变 |
| `E0-10` descriptor/record 合法声明旧 tree、实际为新 bytes | 1 | 1 | generation tree mismatch；ref/intent/active/generations 完整状态不变 |

每个 fault point 的 verify 与 recover 使用两个独立夹具。aggregate 保存确定 exit code、归一化输出 SHA-256、ref/intent/ref backup 全字段、marker、Core/AutoChess active 的 `FileExists`/`DirectoryExists` 与双树身份，以及所有 generation 的 descriptor/record 声明、文件 hash、实际双树与聚合 hash。所有预期失败 probe 均强制 `After == Before`；`E0-08` 第一次 recover 也使用同一 no-op 断言。

## Aggregate hard-link 隔离探针

当前卫生版 harness SHA-256：`757c98e3c2653a1faefb1bea8ab45e8b509a4f03c6422127e0952e91dd6bb5ff`。

本轮只运行不调用生产 CLI 的 `-OutputAliasProbeOnly`：预先让允许路径叶成为外部 sacrificial target 的 NTFS hard link，再调用 aggregate writer。结果为 `Passed`；外部目标前后 SHA-256 均为 `efaa6150ae092541a592ab09103816ec78ad7e6266adbf55463982db33786a25`，新 aggregate SHA-256 为 `15dae2fa5bac34d6263c1e36e25d49b12a0e7f6f253691ef1e76f2a9a98691fd`，且输出为独立普通文件。失败路径 JSON 解析得到 `PostFinalWriteConfirmed=false`，四项确认位真值表通过。独立 owner sentinel 在正常 cleanup 前完成内容/边界/reparse 校验；`SentinelCreateFailure`、`SentinelMissing`、`SentinelWrongContent` 三项负例均证明根仍保留，测试随后恢复正确 sentinel 并经同一受控清理器删除。受保护真实项目 fingerprint 前后均为 `591789421688c028a99c3df1f905d12e6d3cfd15e9765f9e3f55bd2364d694e7`，probe 精确根已删除，无临时残留。

writer 对已有叶执行精确 unlink，再以 `FileMode.CreateNew + FileShare.None` 写 UTF-8 no-BOM；同名竞争创建会 fail closed。aggregate schema v4 仅在 `TestedProcessExitCode=0` 且未确认 candidate 的写入、读取解析均完成后允许发布 `PostFinalWriteConfirmed=true`，确认版还会再次读取。只有进入 confirmed-final finalization 后且 failure aggregate 重写成功的可恢复普通失败承诺稳定收口为 exit 1 / confirmation false；candidate 前置失败只保证非零退出与 confirmation 不为 true，不承诺重写文件内 exit 字段。

正常完成路径中，最终保留 true 只与 harness 实际 exit 0 配对；确认版先写 true 后复核窗口中的强杀、收口双故障和机器断电仍为 `SKIP/blocked`，可能留下未完成配对的磁盘状态。

parent directory handle/no-follow TOCTOU 仍明确保留为 P2：现有 PowerShell 范围不引入复杂 P/Invoke，因此不宣称防御路径复核前后或句柄关闭后的恶意父目录/叶替换。

## RefSha256 与 RefFileSha256

真实 `ActiveGenerationRef.json` 的独立 oracle 校准结果：

- `RefSha256`：`296c4a19e4713a6bd9c0f411fd1777af9897119b799607fed6703d663ee9e426`。
- `RefFileSha256`：`e5f06302c8ae7edf5229be7344f193476e9a5f2293f396616c7f209ec20cd9af`。

前者是协议身份字段的 canonical self-hash，不包含 `RefSha256` 自身；后者覆盖 JSON 文件的全部原始 bytes。E0 同时独立复算前者、读取后者，并在真实 ref 与所有成功收敛夹具中断言两者不可互换。

## 保证边界

本轮只证明生产 CLI 对构造出的合法 synthetic boundary state 的 open/recover 收敛与 fail-closed 行为。它没有实际在生产写入中途终止 CLI 进程；`ActualProcessTerminationInjection` 与 `MachinePowerLossDurability` 均在 aggregate 中明确记录为 `SKIP/blocked`。

`stale RSP`、candidate asmdef/source/reference graph 与 single-root/selector 物理 fault point 明确延期到 D1 后的 E1；Z 集成后再执行 E2 最终重跑。E0 通过不授权设置 `FullSemanticEligibility=true`，也不授权领取 N2-1B。
