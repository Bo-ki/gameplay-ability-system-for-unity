# Runtime V1.1 D0-M2S SourceGenerator 完整故障实验结果

> 日期：2026-08-31  
> 正式 RunId：`D0M2S-20260830T180951Z-fe75df12b9a2`  
> 技术终局：`Passed / None`  
> 路线：`StableGraphSourceGenerator`  
> 下一门：`D0-M2R-SourceGeneratorConfirmation`

## 1. 结论

一次 fresh SourceGenerator full-fault 与 SG-specific X 已在同一 RunId、同一证据根、同一冻结工具身份下全部通过。SG-01～SG-08 均为 `Passed`，`FullFaultStatus=Passed`、`SourceGeneratorSpecificXStatus=Passed`；8 次独立 Unity 调用中的所有成功观测都证明三目标程序集绑定同一 generation 与同一 additional-file。每次负例调用都写入唯一受跟踪 phase stamp；generator 实际执行的负例由 diagnostic 绑定该身份，generator corrupt 则由 `CS8034`、缺失 marker probe 与外层 Unity invocation 非零共同证明 fail closed。缺失、损坏、重复 selector、legacy active generated source、generator 身份漂移和 stale cache/Bee/RSP 均未获得静默选择权。

该结果关闭了 D0-M2R 恢复裁决要求的唯一技术门，允许唯一 D0-M2R owner 重新裁决 production 路线并冻结精确协议。正式 terminal 仍按冻结实验合同写入 `AcceptedProductionRoute=None`、`AcceptedSoleUnityConsumedSelector=NotFrozen`、`D1Authorized=false`；这是“实验不得自我授权”的职责隔离，不是失败，也不能在历史 terminal 中回写。

本轮只修改隔离实验工具并写入独立证据根，没有修改 production CodeGen、generated roots、`ProjectSettings/GasCodeGen`、`Packages` 或 Runtime V1。`RuntimeV1-Runnable-ClosedWorld` 的既有 Development Player 可运行结论保持不变。

## 2. 权威证据

权威证据根为 [`D0M2S-20260830T180951Z-fe75df12b9a2`](../../TestResults/GasCodeGen/N2-G0-D0-M2S/D0M2S-20260830T180951Z-fe75df12b9a2)。

| 证据 | 值 |
|---|---|
| [terminal](../../TestResults/GasCodeGen/N2-G0-D0-M2S/D0M2S-20260830T180951Z-fe75df12b9a2/D0M2S.terminal.json) | SHA-256 `0578fa2afee26b6c024f37607db4c6ff06302ff212dc72974d31ff6a97a7149b` |
| [terminal sidecar](../../TestResults/GasCodeGen/N2-G0-D0-M2S/D0M2S-20260830T180951Z-fe75df12b9a2/D0M2S.terminal.sha256) | 精确记录同一 terminal SHA-256；sidecar 文件 SHA-256 `69beb2eb6b45a6ed1fd46032726e333a5f2bda65bca682eab3100bd5fdabcdfd` |
| [SourceGeneratorFault aggregate](../../TestResults/GasCodeGen/N2-G0-D0-M2S/D0M2S-20260830T180951Z-fe75df12b9a2/SourceGeneratorFault.json) | SHA-256 `eb781bcb17ad75e2694801409ba50dc80d9f97e1f00ab44cde2d925b48e8ee27` |
| evidence closure | `Verified=true`、`Partial=false`、declared/physical `74/74`、aggregate SHA-256 `80b60c0b34e0f26e0828100e3592987447a7f2e08e26cdf3da3617249354d178` |
| contract checker | `Passed=true`、`Status=Passed`、`Reason=None`、exit `0` |
| Unity | `6000.3.14f1_d68c3f99a318`；可执行文件 SHA-256 `611b5785ece71684351029e7f64857f49949f87543a8de1ea45490eff8535262` |

冻结输入身份为：

| 输入 | SHA-256 |
|---|---|
| D0-M2S contract | `bae3c480335679835a29457713bdd8550208163e07c81fbd94eba9397d1869e4` |
| D0-M2R 恢复裁决 | `7ee474685006c8904d199619339e8e2bfce2c6a95d6a8c9ae6c7129449bcd903` |
| central runner | `3a3145669227da3f9bc19cf0b6ae2ab87c93f2fa2b2e2621e4f06d7542c807d3` |
| SourceGenerator child runner | `370c3935c84533ec8e3581e1fd036f55b350b3bae200c63b08f9533a73443f73` |
| canary generator source | `4d339792ea0aad525ab4596e7c5de4143d2d6787a9a9b6b4798507d853918b67` |
| SourceGenerator static gate | `ed338b2f363f87328e0f6cc3f5aa2f461a10411fc3680dc2508f44087e5214d6` |

## 3. 固定 case 结果

| Case | 结果 | 已闭合事实 |
|---|---|---|
| SG-01 `ColdAIdentity` | Passed | cold A 真实编译；Runtime、Editor、AutoChess 三程序集 generation、selector、generator 与 phase-stamp 身份一致 |
| SG-02 `AtomicSelectorSwitch` | Passed | A→B selector 使用单普通文件 replace；replace 前后强杀只留下完整 A 或完整 B，无 transaction residue |
| SG-03 `GeneratorKillRestart` | Passed | generator 在 `BeforeAddSource` 检查点被强杀；成功 raw 缺失；restart 后三程序集只消费 B 且输出全量改变 |
| SG-04 `MissingCorruptInputs` | Passed | selector missing/invalid 与 generator corrupt 均 fail closed；原 selector/generator bytes 与 SHA 已恢复 |
| SG-05 `SourceGeneratorSpecificXPositive` | Passed | canonical B 对撞 audit/cache/stale A 后三程序集仍只消费共同 selector B |
| SG-06 `CompetingAuthorityFailClosed` | Passed | 第二同名 additional-file 与 legacy active `.gen.cs` 均被明确拒绝，未按顺序或优先级静默选取 |
| SG-07 `BoundaryCleanup` | Passed | reparse/junction、hardlink、越界 canary 与 owned cleanup 边界通过；外部 canary 未改变 |
| SG-08 `EvidenceClosure` | Passed | 74 个声明叶与 74 个物理叶逐一闭合；无缺失、额外或部分证据 |

## 4. 实测选择权与身份边界

实验中三程序集共同、唯一且由 Unity/Roslyn 实际消费的 selector 为：

```text
Assets/Selector/Generation.D0M2SCanaryGenerator.additionalfile
```

这是 disposable fixture 的实测路径，证明的 production 机制是“稳定 assembly 图 + 唯一 canonical additional-file + 固定 analyzer DLL”。D0-M2R 必须把该机制投影为精确 production 路径，并要求 D1 的 isolated candidate compile gate 对 production basename、路径、原始 bytes、Roslyn `AdditionalText` snapshot、analyzer bytes 和三程序集实际 `RoslynAdditionalFilePaths` 做同等级验证；不得把 canary 文件名直接安装进 production，也不得把未实测的第二 selector、audit record 或 active `.gen.cs` 解释为授权。

实验角色表确认：

- canonical `.additionalfile`：Authority、mutable、Unity-consumed；唯一 generation 线性化输入。
- analyzer DLL：Authority、immutable、Unity-consumed；必须与 generation 身份共同绑定。
- audit record：DerivedAudit、Unity 不消费、选择权为零。
- `Library/ScriptAssemblies/**`：Derived；`Library/Bee/**`：Cache；二者都不能在 Authority 缺失或损坏时授权成功。
- phase-stamp：HarnessControl，仅用于把每次编译身份和故障控制放进真实 compilation；不属于 production 协议。
- duplicate selector 与 legacy generated source：FaultOnlyCompetingAuthority；production 必须保证不可达并 fail closed。

selector 语义读取同时绑定 canonical raw bytes 与 Roslyn `AdditionalText.GetText()` snapshot；phase-stamp 则按 canonical selector 反推精确路径，并由每个程序集的 `CompilationPipeline.SourceFiles` 和当前文件 SHA 交叉验证。运行身份不依赖未跟踪进程环境变量，也不能由旧 generator driver/cache 复用伪造。

## 5. 保护、清理与残留

- production protected snapshot 前后 aggregate 均为 `556feff02c068a7265e32ab632da9654a1fb193ae689dab2b894440449774583`，`Unchanged=true`。
- D0-M2S 工具身份前后 aggregate 均为 `91b5eeaf06a24bdd349ad14b0ed039a25bf29957c5875e2986dff80a25388a4f`，`Unchanged=true`。
- disposable fixture 为 `<deleted>`，`CleanupStatus=Passed`、`OwnedProcessResidue=false`、`OwnerSentinelValidated=true`、`NoReparsePoints=true`、`NoHardlinks=true`。
- 正式运行后不存在本轮 owned Unity、UnityCrashHandler 或 Bee process residue；正式 run root 对应的 `gas-codegen-d0-m2s-sourcegen-*` 临时根不存在。

## 6. 诊断运行与 fresh 资格

正式通过前的诊断运行全部保留，未覆盖或伪装为绿色。它们依次暴露并修复了 PowerShell `$PID` 保留变量、byte span 兼容、evidence 参数拆分、日志共享、未跟踪环境身份、compiler 退出等待和 Windows symlink 权限等 harness 问题。`D0M2S-20260830T180022Z-bb009fe56970` 已证明 SG-01～SG-06、full-fault 与 X 语义通过，但因普通账户无法创建 symlink 而在 SG-07 正确给出 `Inconclusive / HarnessFailure`；最终改用同等 reparse 边界的 suite-owned Junction 探针后，另起全新 RunId 完成正式运行。

正式 Run5 没有复用任何诊断 terminal、raw leaf、临时项目或进程状态。每个失败或不确定运行均保留为历史，只有 `D0M2S-20260830T180951Z-fe75df12b9a2` 可用于 D0-M2R 准入。

## 7. 裁决边界

本结果允许 D0-M2R 接受 `StableGraphSourceGenerator` 并冻结 D1 实施合同，但不直接声明：

- `ProductionInstallAdmission=Passed`；
- `DeclaredFullSemanticEligibility=true`；
- Runtime admission consumer、Release/IL2CPP、断电/fsync 或同主体恶意并发已经验证；
- production CodeGen 已迁移到 SourceGenerator；
- 13 个历史 Tier-B release backlog 已因此自动关闭。

D0-M2R 必须继续保持 `ProductionInstallAdmission=NotEvaluated` 与 `DeclaredFullSemanticEligibility=false`。若授权 D1，D1 只实现冻结的 SourceGenerator 物理消费协议并以最小 isolated compile/fault 验证收口；Semantics、Proofs、Runtime install consumer 与最终 eligibility 仍由各自后续 owner 处理。
