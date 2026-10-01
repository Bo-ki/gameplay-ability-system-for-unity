# Runtime V1.1 D0-M2T ImmutableTarball 完整故障实验结果

> 日期：2026-08-31  
> 权威 Run：`D0M2T-20260830T163229Z-3191c7b97729`  
> 终态：`RouteRejected / MissingOrCorruptAuthorityAccepted`  
> 选中路线：`ImmutableTarball`  
> Unity 唯一消费 selector：`Packages/manifest.json`  
> D1：`NotAuthorized`  
> 下一门：`D0-M2R`

## 1. 结论

ImmutableTarball 路线未通过 D0-M2T 完整故障门，不能进入 production install，也不能据此授权 D1。权威 Run 的八个 case 中，TT-01、TT-02、TT-03、TT-05、TT-06、TT-07、TT-08 通过；TT-04 以 `MissingOrCorruptAuthorityAccepted` 失败，因此机器终态按合同收口为 `RouteRejected`。

TT-04 的实际阻断事实必须精确表述为：B tarball 已完成一次 warm resolve 后，删除 authority tarball 再启动 Unity，进程仍以退出码 0 完成并产生成功的 B 观测；该观测与 warm B 观测字节一致，三个程序集的 source 均来自既有 `Library/PackageCache/com.exhard.exgas.d0m2f-tarball@da176c9b532c`。这证明 warm PackageCache 在 authority 缺失时形成了 fallback。

本轮**没有证明 corrupt tarball 被接受**。corrupt 注入场景实际退出码为 1，未产生成功观测或结果文件。`MissingOrCorruptAuthorityAccepted` 是 TT-04 的冻结合同原因码；本 Run 触发它的已观测正谓词是 missing authority 被 warm cache 接受。

因此，本轮冻结以下机器结论：

```text
Status = RouteRejected
Reason = MissingOrCorruptAuthorityAccepted
SelectedRoute = ImmutableTarball
SoleUnityConsumedSelector = Packages/manifest.json
D1Authorized = false
ProductionInstallAdmission = NotEvaluated
DeclaredFullSemanticEligibility = false
NextGate = D0-M2R
```

## 2. 权威运行与适用边界

唯一权威证据根是 [`D0M2T-20260830T163229Z-3191c7b97729`](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729)。该 Run 使用 fresh RunId、独立 J0、最终 20 文件工具快照和独立 Unity/fixture 运行，不从诊断运行拼接证据。

首跑 `D0M2T-20260830T161353Z-f04d5bf42b65` 的终态是 `Inconclusive / SchemaViolation`；其 Tarball 结果为 `Inconclusive / EvidenceIncomplete`。该首跑及隔离的 `TT06-CleanedReplay-20260830T162813Z-e620d898` 只用于定位 TT-06 disposition 建模问题，不参与本文件的路线裁决，不向权威 Run 借入任何 evidence leaf。

权威 Run 的 terminal 原始 SHA-256 为 `14c200f23a134767abd7e720b94bcdd3f4273f418de04c38cd56fc10dde6347e`。其 [sidecar](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/D0M2T.terminal.sha256) payload 精确绑定该值；sidecar 文件自身原始 SHA-256 为 `d2fb23a7b0947b0cd9fb77255953188743be45d5992ba725fbdfa5fc00619e91`。

## 3. 八个故障 case 结果

| Case | 职责 | 机器结果 | 关键事实 |
|---|---|---|---|
| TT-01 `BaselineAuthority` | 基线 authority 与只读性 | `Passed / None` | A、B content-addressed tarball 的 SHA/长度匹配且只读；authority watcher 观测到 canary，`AuthorityWrites=0`、`WatcherErrors=0`。 |
| TT-02 `SelectorAtomicKill` | selector 原子替换与双 kill | `Passed / None` | A/B manifest 仅出现完整字节，replace 前后两处计划 kill 均被观测，`CompleteBytesOnly=true`。 |
| TT-03 `ResolveKillRecovery` | resolve 中断后的恢复 | `Passed / None` | B resolve 被计划 kill，kill 时结果尚不存在；fresh restart 退出码 0、未超时、三程序集均为 B。 |
| TT-04 `MissingCorruptAuthority` | authority 缺失/损坏必须 fail closed | `Failed / MissingOrCorruptAuthorityAccepted` | missing B authority 后仍退出码 0 并复用 warm B PackageCache；corrupt B 实际退出码 1、无成功观测。阻断来自 missing fallback。 |
| TT-05 `ManifestLockDrift` | manifest、lock 与 audit 权限边界 | `Passed / None` | invalid JSON、alias、escape 均被拒绝；stale A lock 未取得选择权；audit 保持 A/`AuditOnly`，Unity 最终只消费 B。 |
| TT-06 `StaleCacheBeeRsp` | stale PackageCache 与 Bee/RSP 不得选中 | `Passed / None` | stale A cache 被预置后由 Unity 在 final 前清理，final 使用不同的 B cache；stale A RSP canary 保留但最终三程序集全 B，编译图 A→B 已变化。 |
| TT-07 `BoundaryCleanup` | 文件系统边界与清理 | `Passed / None` | hardlink、reparse、sentinel 三类越界均被拒绝，external canary 未变化，suite-owned cleanup 通过。 |
| TT-08 `EvidenceClosure` | 证据可打开性、身份与残留闭包 | `Passed / None` | 32 份 prior evidence 全部相对寻址、可打开、长度/SHA 匹配，无重复或孤儿 ID；加上 closure leaf 共 33 份。 |

## 4. TT-04 阻断事实

[TT-04 fault record](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-04/missing-corrupt-authority.json) 固定了三阶段观测：

1. warm B 基线成功，`WarmBaselineB=true`。
2. B authority tarball 缺失时，Unity `ExitCode=0`、`SuccessfulObservation=true`、`ResultPresent=true`，因此 `CacheFallbackAccepted=true`。
3. B authority tarball 被替换为 SHA-256 `0f7dd6110bb0ddd241636b5d9e27051e3f3bf872709e30f35942a14d387dd1bf` 的 corrupt 内容时，Unity `ExitCode=1`、`SuccessfulObservation=false`、`ResultPresent=false`；实验结束后 authority 已恢复。

[warm B Unity raw](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-04/warm-b-unity.json) 与 [missing B Unity raw](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-04/missing-b-unity.json) 的长度均为 118653 字节，原始 SHA-256 均为 `2d81414df126260bca2f6a77dd44830c2e6c663e6d187bd04cad07dfddd382e7`。两份 raw 的 Runtime、Editor、AutoChess 三个程序集均为 generation B、token `d0m2f-tarball-generation-b`，source 路径均落在：

```text
Library/PackageCache/com.exhard.exgas.d0m2f-tarball@da176c9b532c/**/GenerationMarker.cs
```

这不是 selector 漂移：`Packages/manifest.json` 仍是唯一 Unity-consumed selector。失败点在于 manifest 指向的 authority payload 缺失后，Unity/UPM 仍可从 warm PackageCache 满足该选择，违反完整故障门要求的 authority fail-closed 语义。

## 5. TT-06 disposition 修正

诊断首跑将“预置 stale A cache 在 final 后必须仍存在”当作唯一通过形态；实际 Unity resolve 会清理该未选中、suite-owned 的 stale cache，因此首跑只能形成 `EvidenceIncomplete`，不能据此判定缓存被消费或路线失败。

权威 Run 前，harness 与合同 checker 已把 disposition 修正为从 before/after snapshot 和现场 presence 共同重算的两种合法终态：`RemainedUnselected` 或 `CleanedBeforeFinal`。两者都必须同时证明 final 使用不同 cache，且 stale A 未取得选择权；不接受手工声明替代 raw 事实。

权威 Run 的 [stale cache raw](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-06/stale-cache.json) 重算结果为：

```text
StaleAPreseeded = true
StaleAStillExists = false
StaleAInAfterSnapshot = false
StaleAPresenceConsistent = true
StaleADisposition = CleanedBeforeFinal
StaleAResolvedWithoutSelection = true
FinalUsesDifferentCache = true
```

同时，[stale Bee/RSP raw](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-06/stale-bee-rsp.json) 证明 A canary 在 before/after 均存在且未进入 final 结果；`FinalAllB=true`，编译图从 `d4743a57bb2f65d1e9ca40fd730f6ddf2dd3d54b61986a066f29d4eab8f60336` 变为 `a665954ab2b60f687bc1bd3eb2f76d665bff30326e6e268f2793bc76497ee151`。因此 TT-06 在正式合同下是 `Passed`，且该修正不改变 TT-04 的独立路线拒绝结论。

## 6. 证据闭包与关键身份

[TarballFault.json](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/TarballFault.json) 的原始 SHA-256 为 `d099529e88f2715904637d6ccb831c3ae9beb90452665f57255497f0d428b71f`，其状态/原因与 terminal 一致；故障进程退出码为 20，运行时长 78712ms。terminal 中的 evidence closure 为 `Verified=true`、`Partial=false`、`FileCount=33`，33 文件聚合 SHA-256 为 `cf6c38580d98641f7f31dad008e45b01986574b960e2f608bf9c2ec194ac626d`。

TT-08 [closure leaf](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-08/evidence-closure.json) 自身原始 SHA-256 为 `994b11bc03248a9ce709781993141d255521d039f11fbdce5df72a8329191e14`。其 32 项 prior evidence 检查结果均为：`AllPathsRelative=true`、`AllFilesOpenable=true`、`LengthsMatch=true`、`Sha256Match=true`、`DuplicateEvidenceIds=false`、`OrphanEvidenceIds=false`。

关键 raw 身份如下：

| 证据 | 原始 SHA-256 | 用途 |
|---|---|---|
| [terminal](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/D0M2T.terminal.json) | `14c200f23a134767abd7e720b94bcdd3f4273f418de04c38cd56fc10dde6347e` | 唯一机器终态 |
| [TarballFault](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/TarballFault.json) | `d099529e88f2715904637d6ccb831c3ae9beb90452665f57255497f0d428b71f` | 八 case 与 33 evidence inventory |
| [TT-04 fault record](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-04/missing-corrupt-authority.json) | `04073abf156bff51219104e72f9e3aa817622b0c98112eec5a78aa9243dbddfd` | missing/corrupt 三阶段结果 |
| [TT-04 warm B](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-04/warm-b-unity.json) | `2d81414df126260bca2f6a77dd44830c2e6c663e6d187bd04cad07dfddd382e7` | warm B Unity 观测 |
| [TT-04 missing B](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-04/missing-b-unity.json) | `2d81414df126260bca2f6a77dd44830c2e6c663e6d187bd04cad07dfddd382e7` | warm PackageCache fallback 观测 |
| [TT-06 stale cache](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-06/stale-cache.json) | `2e9738ad26d249d651bd1fcd10c9672adbef0009f4643c936f74519b42ac6a71` | `CleanedBeforeFinal` disposition |
| [TT-06 stale Bee/RSP](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-06/stale-bee-rsp.json) | `c0fa176a04537f4bbbca3e3158cac1d80f4fa0aae55a5215c14d6749238fb46d` | stale canary 与 A→B 编译图 |
| [TT-06 final B](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-06/final-b-unity.json) | `4ae30f70fbc9f1f9a4c0f22fe3984a6b512ff3c96ecbe7a77f973e3ba7f515cb` | final 三程序集全 B |
| [TT-08 closure](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-08/evidence-closure.json) | `994b11bc03248a9ce709781993141d255521d039f11fbdce5df72a8329191e14` | 32 prior + closure leaf 的完整性 |

J0 与 J2 身份闭包如下：

- [J0 Dispatch](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/J0.Dispatch.json) 原始 SHA-256 为 `3ac35aa1c484f3e0bc988c47c4c074f16124592bca87ce07cda1083f31b78c11`；六项冻结输入与 D0-M2F 冻结记录 SHA 均精确匹配。
- [J0 Protected Snapshot](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/J0.ProtectedSnapshot.json) 原始 SHA-256 为 `0535622e1259f8097e105b3f99a3757018221845f2e7df01521c05eb0330408b`。J0→J1→J2 protected aggregate 均为 `1899f47e07cc4e9bd0415964aa00507108bb3979ecd4939b8690ca1d091d999f`；[J2 comparison](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/J2.ProtectedComparison.json) 为 `Unchanged=true`、无 added/removed，其文件原始 SHA-256 为 `3e79d5447fc58b2608af63482de82f40f34210930179074429801d34f2b9844a`。
- 工具树在 J0/J1/J2 均为 20 文件，aggregate `1863194328a25a8ca6b96da5c3c21127d9306521117203b0c6fd42735e7cad83`；[J2 tool comparison](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/J2.ToolComparison.json) 为 `Unchanged=true`，其文件原始 SHA-256 为 `a46cf1920ab290663f36f97c343bff34e31dcb1a8eb861107d408fd9cb034aa7`。
- [Unity identity](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/J0.UnityIdentity.json) 绑定 Unity `6000.3.14f1`、product version `6000.3.14f1_d68c3f99a318`、`Unity.exe` SHA-256 `611b5785ece71684351029e7f64857f49949f87543a8de1ea45490eff8535262`；该身份文件原始 SHA-256 为 `8e5e948bd1b411603bf8133a9dbb6df492dee5e78203398633f459fdae6d4bf8`。
- Contract/Tarball 两条静态门均为 `Passed=true`；日志 SHA-256 分别为 `fdee7553e491f6c38be23af6bb6d6f9dfb99fc42e3f4d0578d987d1ea89ac250`、`76983e6729e2fbad1918bce91b87a5d109cf5a344f89af410460fd6031c84a86`。

## 7. 清理与边界闭包

TT-07 拒绝 hardlink、reparse、sentinel 三类边界攻击，external canary 保持不变。TT-08 记录 6 个 suite-owned fixture root 均进入 `RemovedRoots`，`ResidualRoots=[]`；本 Run 共登记 8 个 owned PID，闭包时 `LiveOwnedProcessIds=[]`。本轮没有删除、覆盖或回写历史 evidence root。

上述清理闭包只证明实验资源和进程已收口，不会把 `RouteRejected` 翻转为通过，也不授权清除 warm PackageCache fallback 风险。

## 8. 下一门

D0-M2T 已达到可裁决终态，不再追加同一路线的无边界试跑。下一任务必须是唯一 owner 的 `D0-M2R`：以本权威 terminal、TarballFault 和 TT-04 raw 为输入，决定 production 恢复协议与路线处置；在该门完成并重新确认 ADR 前，保持：

```text
D1Authorized = false
ProductionInstallAdmission = NotEvaluated
DeclaredFullSemanticEligibility = false
```

本文件只冻结 D0-M2T 的实验事实和 gate disposition，不修改 Spec/ADR，不宣称 ImmutableTarball 已具备 production 资格，也不把诊断运行升级为正式证据。
