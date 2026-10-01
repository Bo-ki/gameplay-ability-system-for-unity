# N2-G0 CodeGen 控制面与物理路线实验

## 证据索引

- [N2-G0-E0 隔离黑盒基线证据](N2-G0-E0-evidence.md)
- [N2-G0-D0-M1 物理可见性实验双跑证据](N2-G0-D0-M1-evidence.md)
- [D0-M2F 历史候选选路裁决（已被正式 D0-M2T supersede）](../../../docs/reviews/RuntimeV1.1-D0-M2F-选路裁决.md)
- [D0-M2T 正式 terminal（RouteRejected）](../../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/D0M2T.terminal.json)
- [D0-M2S 正式 terminal（Passed）](../../../TestResults/GasCodeGen/N2-G0-D0-M2S/D0M2S-20260830T180951Z-fe75df12b9a2/D0M2S.terminal.json)
- [D0-M2S 完整故障实验结果](../../../docs/reviews/RuntimeV1.1-D0-M2S-SourceGenerator完整故障实验结果.md)
- [D0-M2R SourceGenerator 路线接受与 D1 授权](../../../docs/reviews/RuntimeV1.1-D0-M2R-SourceGenerator路线接受与D1授权裁决.md)

## 结论

当前 D0-M2R 路线裁决：immutable tarball 继续保持 D0-M2T 的 `RouteRejected / MissingOrCorruptAuthorityAccepted`。随后正式 D0-M2S RunId `D0M2S-20260830T180951Z-fe75df12b9a2` 的 terminal 为 `Passed / None`（SHA-256 `0578fa2afee26b6c024f37607db4c6ff06302ff212dc72974d31ff6a97a7149b`）；full-fault、SG-specific X、SG-01～SG-08、74/74 closure、protected/tool identity 与 cleanup 全部通过。当前 `AcceptedProductionRoute=StableGraphSourceGenerator`，唯一 selector 为 `Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile`，analyzer 为 `Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll`；`D1Authorized=true`、`ProductionMigrationAuthorized=true`、`ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`。D1 required set 固定升级为 `EX-GAS-RuntimeV1-RequiredArtifacts-v2`，保留完整 `ArtifactManifestHash` 语义，另用 `SourceArtifactInventoryHash` 与 required C# items 双向闭合；fixed intent 必须先以 `FileMode.CreateNew` 获取 exclusive claim，首次发布以 no-overwrite atomic create-new/move 提交，已有 selector 才使用 `File.Replace`，只有 durable committed receipt 可补 audit。下一门是 D1 production migration + route-specific E1，不再重复 D0-M2S；`RuntimeV1-Runnable-ClosedWorld` 状态不变。

D0-M2S 历史 terminal 仍按实验合同记录 `AcceptedProductionRoute=None / D1Authorized=false / NextGate=D0-M2R-SourceGeneratorConfirmation`，不得回写。这些字段表示实验不得自我授权；当前 production 状态只由后续 D0-M2R 裁决和 ADR-0001 提供。

`Run-N2-G0-E0.ps1` 只调用已构建的生产 `GasCodeGenCli.exe`，并把所有 ref、intent、active、generation 与 descriptor 故障注入限制在操作系统临时目录。它不运行 SourceGen、不构建 CLI，也不写真实项目的 active/ref/generation。

D0-M2T 合同 checker 的三个入口语义已隔离：正式 `-Path` 历史 aggregate 复核校验证据内冻结声明、合同常量、冻结记录与 J0 语义，不要求已被后续规范 supersede 的六个工作区文件继续保持旧 SHA；普通 `-StaticOnly` 仍现场复核六文件，任一漂移即 `InputIdentityDrift`，因此不得在当前规范上误跑 fresh D0-M2T；仅历史工具自检可显式使用 `-StaticOnly -HistoricalReplay`，它跳过六文件现场 raw 检查，但仍校验合同常量、冻结记录、PowerShell AST 与 synthetic 反例。`-HistoricalReplay` 不能与 `-Path` 组合。

当前 E0 覆盖 10 个场景：

1. 首次发布存在 intent、无 ref：普通 verify 拒绝，显式 recover 按原 PromotionId 前滚。
2. 非首次发布 ref 仍指向 previous：普通 verify 与显式 recover 都回滚 active 到 previous。
3. 非首次发布 ref 已指向 target：普通 verify 与显式 recover 都前滚 active 到 target，并保留生产 `File.Replace` 遗留的 previous-ref `.bak` 原始身份。
4. initialized 后 ref 丢失：verify/recover 都 fail closed，保留 marker 与 active 证据。
5. active 未登记漂移：verify 拒绝，recover 只按已验证 ref 修复。
6. descriptor 密封后向 immutable generation 注入文件：verify/recover 都拒绝并保留注入证据。
7. intent 单独复用 previous 的旧 tree hash 指向 target：即使重算合法 IntentSha256，仍因 intent/record mismatch 被拒绝；该 case 只证明 intent binding，不冒充 descriptor/tree binding。
8. 正常 active 上重复 recover/no-op：第一次及重复 recover 后完整协议状态完全不变。
9. 首发 target ref 已提交、intent 与 target active 存在、marker 缺失：verify/recover 创建 marker、清理 intent，但 ref bytes 与 PromotionId 不变。
10. generation 的 descriptor/record 自校验合法且共同声明 previous 旧 tree hash、实际保存新 tree bytes：verify/recover 都因 generation tree mismatch 拒绝。

## 运行

先确保当前生产 CLI 已构建。E0 故意不代替调用方执行构建，因为生产工程内的 `bin/obj` 写入会破坏“测试运行不改变真实项目字节”的边界。

```powershell
pwsh -NoProfile -File Tools/Tests/GasCodeGen/Run-N2-G0-E0.ps1
```

也可显式指定 CLI 和 aggregate：

```powershell
pwsh -NoProfile -File Tools/Tests/GasCodeGen/Run-N2-G0-E0.ps1 `
  -CliPath Tools/GasCodeGenCli/bin/Debug/net472/GasCodeGenCli.exe `
  -OutputPath TestResults/GasCodeGen/N2-G0-E0.aggregate.json
```

aggregate writer 的 hard-link 隔离探针不调用 CLI，也不运行正式矩阵：

```powershell
pwsh -NoProfile -File Tools/Tests/GasCodeGen/Run-N2-G0-E0.ps1 `
  -OutputAliasProbeOnly
```

相对 `-OutputPath` 始终以 `ProjectRoot` 为基准解析，并且只允许严格位于 `<ProjectRoot>/TestResults/GasCodeGen/` 下。创建目录和每次写入前后都会检查从 ProjectRoot 到输出文件的全部现存路径段；ADS、非法/保留文件名、reparse point、越界路径与目录型叶都会 fail closed。

若 aggregate 叶已存在，writer 只 unlink 该精确目录项，再用 `FileMode.CreateNew + FileShare.None` 的 UTF-8 no-BOM 句柄写入。这样预置 NTFS hard link 只会被解除，不会沿 alias 截断外部目标；unlink 后的竞争者若先创建同名叶，`CreateNew` 会失败而不是覆盖。句柄打开期间写入绑定到已打开文件且拒绝共享；边界外的恶意进程仍可能在“路径复核—CreateNew”之间替换父目录，或在句柄关闭与最终复核之后再次替换叶。完全防御这种并发对手需要基于 parent directory handle 的逐段 no-follow open；该问题明确保留为 P2，本 E0 不引入复杂 P/Invoke，只固定当前 handle-based 边界并在写后立即复核普通文件、reparse 与预期 bytes hash。

默认机器可读证据写入 `TestResults/GasCodeGen/N2-G0-E0.aggregate.json`。脚本对以下真实工程范围分别计算 tree/file hash，再形成总 SHA-256；case 结束后写入 draft aggregate，再取得保护快照，最终 aggregate 写入后再次确认同一 fingerprint。任一阶段不一致都会令 suite 失败：

- `Assets/GAS/Editor/CodeGen/Core`
- `Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs`
- `Assets/GAS/Editor/CodeGen/Phases/AutoChessDemoCodeGenPhase.cs`
- `Tools/GasCodeGenCli`
- `ProjectSettings/GasCodeGen`
- `Assets/GAS/Generated/CodeGen`
- `Assets/AutoChessDemo/Generated`

该范围故意不包含 A/B 链独占的 `Semantics/**` 与 `Proofs/**`，因此交接单允许的并行工作不会被误判成 E0 写入；任何 generation/ref/intent/descriptor 控制面或真实 active 字节变化仍会 fail closed。

临时目录名必须精确匹配 `gas-codegen-e0-<32 lowercase hex>`，并携带绑定本轮目录名的 sentinel。递归删除前会复核严格 temp 边界、sentinel 原始内容和全部后代无 reparse point；任一条件不满足都拒绝清理并令 suite 失败。hardlink probe 另用 `gas-codegen-e0-hardlink-probe-<32 lowercase hex>` 与独立 owner sentinel，清理前同样验证 GUID 子路径、根/后代非 reparse 和 sentinel 原始内容。owner sentinel 创建失败时只报告精确根并保留 cleanup debt，不在未验证 owner 的情况下递归删除；缺失或错误内容也拒绝删除。只有 cleanup 为 `Passed` 时 aggregate 才把 `FixtureRoot` 记为 `<deleted>`；`Failed` 与 `SkippedByRequest` 都保留真实 suite 根以便处置，其中 `-KeepFixtures` 对应 `SkippedByRequest`。

每个 probe 都采集 ref/intent/ref backup 全字段、marker、Core/AutoChess active 路径各自的 `FileExists` 与 `DirectoryExists`、active 双树，以及全部 generation 的 descriptor/record 文件 hash、声明字段、实际双树 hash、单代与 store 聚合 hash。因此“路径不存在”和“被普通文件占位”不会共享同一个空 tree hash 状态。`E0-03` 明确把 previous ref `.bak` 纳入 oracle，并固定当前协议不消费、不改写 backup。所有预期非零 exit probe 都强制 `After == Before`；`E0-08` 第一次 recover 同样强制 no-op。

aggregate 还固定记录 expected case count、harness SHA-256、GenerationStore 源码 SHA-256、CLI Program 源码 SHA-256 与 CLI 二进制 SHA-256。

aggregate schema v4 对 `PostFinalWriteConfirmed` 使用两阶段发布：本次运行实际写出的 draft、显式 failure aggregate 与未确认 final candidate 的字段值均为 `false`；candidate 经 `CreateNew` 写入、bytes 校验、JSON 解析及真实项目保护快照复核后，再由 `TestedProcessExitCode == 0 && FinalWriteCompleted && FinalReadValidated` 真值门决定是否允许写 `true`。确认版自身还会再次读取解析。只有已进入 confirmed-final finalization 且 failure aggregate 重写也成功的可恢复普通失败，才保证收口为 `TestedProcessExitCode=1`、`PostFinalWriteConfirmed=false`；位于该 try 之前的 candidate 写入/读取/快照失败会直接非零退出并保持 confirmation 不为 true，但不承诺已把文件内 exit 字段重写为 1。

在纳入保证的正常完成路径中，最终保留的 `PostFinalWriteConfirmed=true` 只与 harness 实际 exit code 0 配对。确认版“先写 true、后复核”窗口中的强杀、收口重写再次失败以及机器断电都可能留下未经最终配对的磁盘状态，明确属于 `SKIP/blocked`；E0 不用措辞掩盖该窗口，也不借该字段扩大保证。

正式机器双跑应使用两个独立 `OutputPath` 保留 run1/run2 aggregate，再分别计算文件 hash；单次 harness 不伪造跨运行证明。

## Hash 术语

- `RefSha256`：协议字段。它是 `ActiveGenerationRef` 除 `RefSha256` 自身外的固定身份字段按 canonical length-prefix codec 计算出的 self-hash。
- `RefFileSha256`：整个 `ActiveGenerationRef.json` 原始文件 bytes 的 SHA-256，包含 JSON 排版与末尾 LF。

两者不是同一命名空间、不能互换。suite 会同时记录两者、独立复算真实 ref 的 `RefSha256`，并断言 `RefSha256 != RefFileSha256`。

## 保证边界与后续阶段

E0 只验证生产 CLI 对构造出的合法 synthetic boundary state 执行 open/recover 时的收敛与 fail-closed 行为。它没有在生产写入中途实际终止 CLI 进程，也不证明机器断电级目录持久化、目录 flush 或 fsync 语义；这两项在 aggregate 中明确记为 `SKIP/blocked`。

以下 fault point 明确不属于 E0：

- stale Bee RSP；
- candidate asmdef/source/reference graph；
- 当前 SourceGenerator production selector/analyzer/scaffold 与单文件线性化；它们属于 D1/E1，不属于历史 E0。

历史门序是：D0-M2F tuple → D0-M2T tarball 否决 → D0-M2S full-fault + SG-specific X → D0-M2R/ADR 再确认。该门序现已完成，当前进入 D1 + route-specific E1；Z 集成后才执行 E2 最终全量重跑。在 E2 之前，D0-M2S 或 E0 通过都不代表 N2-G0、production install admission 或 `FullSemanticEligibility` 已完成。
