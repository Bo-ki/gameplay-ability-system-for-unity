# Runtime V1.1 D1 SourceGenerator 生产迁移与可运行验收结果

> 日期：2026-08-31  
> 最终 RunId：`D1FINAL-20260830T221518Z-759ce097c2d8`  
> 结论：`Passed`

## 1. 结论

本轮承诺的 V1 可运行迭代已经完成并实际跑通：production 已从 legacy active `.gen.cs` 一次性迁移到唯一 `StableGraphSourceGenerator` selector；主工程编译、RuntimeV1Runnable EditMode 11/11、PlayMode 5/5，以及 Windows Development Player 的 Ability、production GE 9203、AutoChess 三向量全部通过。

```text
RuntimeV1-Runnable-ClosedWorld = Passed
AcceptedProductionRoute = StableGraphSourceGenerator
D1-SourceGeneratorProductionMigration = Passed
V1Iteration = Passed
ProductionInstallAdmission = NotEvaluated
DeclaredFullSemanticEligibility = false
FullSemanticEligibility = false
```

最后三项是明确的声明边界：本结果关闭本轮 D1 与“先跑起来”的 V1 可运行目标，不冒充 B/C/Z、13 个 Tier-B Pending、Release/IL2CPP、性能证据或完整 UE-GAS 语义已经完成。generation record 中 `FullSemanticEligibility=false` 是 fail-closed 的物理记录值，不表示完整语义准入已经执行；`SupportProfileAdmission=Passed` 也不等于 `ProductionInstallAdmission` 已通过。

最终机器终端为 [`D1FINAL.terminal.json`](../../TestResults/RuntimeV1V1Final/D1FINAL-20260830T221518Z-759ce097c2d8/D1FINAL.terminal.json)，SHA-256：

```text
130c83806721201de2143ad0655ea891c5bb1d1fee6c192633b4b49510f8e890
```

## 2. 冻结身份

| 链 | 冻结产物 | SHA-256 |
|---|---|---|
| D0-M2S/D0-M2R | SourceGenerator 路线接受 terminal | `0578fa2afee26b6c024f37607db4c6ff06302ff212dc72974d31ff6a97a7149b` |
| D1-A | production analyzer DLL | `b6cb1219f05c6874f0eed4bf10d4457a7debb44f28ee7013daa6d1a0a1e26934` |
| D1-B | production CLI/control adapter | `61b6127b10c427de4fb53940c800f053eb410c8eecadd6b994390126dd7f3183` |
| D1-B/J | canonical selector | `24dd8a907752df35edcbd75142226072b45b111c4eb68c572dd98db7cf1c575a` |
| D1-C/J | 14 项 route scaffold 聚合 | `f83b592cf943cbec6c6f4b72c0de0e4f93337826517d1972a64f7acec40b5af2` |
| D1-C | E1 harness | `d3054b040ee15afe2744aff8af4bb10945eca0c48ab0aaf2d43ff4c2dd4c0a69` |
| D1-C | E1 terminal verifier | `233004a65233913070b447b20eb077f0bda84a2eb6824d087b9307f5191bbc00` |
| D1-J | production generation | `30ba57f71c22191196b3119d471e4ca52c0c948da91f2b66aa76af22a0a50ee8` |
| D1-J | artifact manifest | `fbb8b511f4362ecd210cf9b3c9c46dcb3a0440c684d84c195280f8fbc1e5c060` |

最终 Player 身份为：

| 身份 | 值 |
|---|---|
| `ProducerFingerprint` | `5c837b21fe5ef9991a4f6de5141f04eca024987f844e65248c180545273e7518` |
| `GeneratedArtifactIdentity` | `9d36c00eeb923a4aa4c23e3a86929cc904478a1841f35f6d242223e41d05f690` |
| `FinalSourceFingerprint` | `053c70d3cf73e39eb68f566a8282c8ab731bc8fb7d4f3e641c93e3fe55f4aefc` |
| `BuildHash` | `c38282af8bf9f29398f0baa914095ddfe18ca885e9bd36938fb07a0150532c3b` |
| `RunManifestSha256` | `d613b1f2ab98a93f97b14de50a3ad8c7503db36386941d5f4f208773ae5c1316` |

## 3. E1 与修正披露

正式 E1 RunId 为 `D1E1-20260830T215508Z-b7a6552b29b3`。不可变底层结果为 transaction 21/21、direct Unity 10/10、evidence index 156/156，且 cleanup、protected/tool identity 全部通过。transaction B 使用 actual production selector `24dd8a…575a`（119482 bytes）；direct-Unity A/B 使用隔离的 synthetic fixture（分别为 `d0d621…fa9`、`ee1eac…b9a`），不能表述为全程使用 production selector。迁移后的主工程与 Player 另由 post-J actual-route 证据证明消费 production selector。

原始 [`D1E1.terminal.json`](../../TestResults/GasCodeGen/D1/D1E1-20260830T215508Z-b7a6552b29b3/evidence-root/D1E1.terminal.json) 保持不可变，SHA-256 为 `0d5273604908dae6f0fe86a9edfb4129db3d2b6a765f70a79c19c6c57b5db8b0`。其 case summary 是 transaction 21 + direct Unity 10 + cleanup 1 共 32 项通过、`EV-01` 派生闭包 1 项失败；唯一报错来自聚合函数未显式接收 `$ExpectedRunId`，因此把已完整闭合的 evidence index 错记为 `EvidenceIncomplete`。

没有覆盖原始 terminal，也没有重跑或替换任何 Unity/transaction 证据。修复仅增加显式参数和更严格 verifier；随后对原始不可变证据逐文件重算并生成 [`D1E1.corrected.terminal.json`](../../TestResults/GasCodeGen/D1/D1E1-20260830T215508Z-b7a6552b29b3/evidence-root/D1E1.corrected.terminal.json)，SHA-256 为 `baf6749fac9351fe729cfbbb3794175ed29739b09651863c16455197618d2fdf`。修正范围、源 terminal、matrix/index/harness/verifier SHA 均由 [`D1E1.correction.json`](../../TestResults/GasCodeGen/D1/D1E1-20260830T215508Z-b7a6552b29b3/evidence-root/D1E1.correction.json) 绑定；该 manifest 的 SHA-256 为 `605901d66199c7ebf98a3bdaac8949ccab12b00cfe4987c72c218b6a4650f7e4`、`Kind=DerivedStatusRepairOnly`。当前 verifier 独立复验 corrected terminal 为 `Passed / 33 cases`；D0-M2F/T/S 的历史 raw terminal、sidecar 与 harness 均未回写。

## 4. 生产迁移结果

迁移前先对 5 个 legacy source 及 `.meta`、派生 manifest/report、完整旧 `ProjectSettings/GasCodeGen` 做了 115 个受管条目的逐字节备份；备份目录物理共 116 个文件（另含 manifest 自身）。[`backup.manifest.json`](../../TestResults/GasCodeGen/D1/D1E1-20260830T215508Z-b7a6552b29b3/inputs/pre-migration-production-backup/backup.manifest.json) SHA-256 为 `199c8f80ced3c1b649a6432b428319205dd3dbb3e649332b8225cb46e864a64a`。其中历史 `.gen.cs` 位于 `TestResults` 取证目录，不违反只约束 Unity-consumed `Assets/**` 与 embedded `Packages/**` 的零 legacy 要求。

随后执行一次不可兼容迁移：

1. 安装冻结 analyzer、selector `.meta` 与三个 required anchor；原有三个 asmdef/meta 保持冻结字节。
2. 删除精确 5 个 legacy active `.gen.cs` 及其 `.meta`，不保留 fallback 或双事实源。
3. 运行 production `sourcegen-all`，一次提交 generation `30ba57f…a50ee8`。
4. `generation-verify` exit 0；post-J scaffold 14/14 exact。

迁移后硬事实：

| 项 | 结果 |
|---|---|
| canonical selector | 1 份，119482 bytes，SHA `24dd8a…575a` |
| analyzer | 1 份，SHA `b6cb12…6934` |
| required anchors | 3 份 |
| Unity-consumed `Assets/**` + embedded `Packages/**` active `.gen.cs` | 0 |
| `ActiveGenerationRef` | v2，`UnityConsumerAuthority=0` |
| active generation record | v3，六项 archive SHA 全闭合 |
| committed transaction | `CommittedAndAudited`，previous selector 为 `Missing` |
| fixed `PublishIntent` / canonical `.bak` / temp residue | 0 |

`Generations/**` 当前共 6 个目录：1 个 active v3、4 个完整的非 active v1，以及 1 个空旧残目录。旧 v1、空目录与 `Initialized.marker` 均为零 authority 的取证历史，不参与 Unity 选择，也不能冒充 strict v3 Store 的可直接 rollback 集。完整绑定见 [`ProductionMigration.evidence.json`](../../TestResults/RuntimeV1V1Final/D1FINAL-20260830T221518Z-759ce097c2d8/ProductionMigration.evidence.json)。

## 5. 主工程与可运行验收

| 验收 | 结果 | 证据 |
|---|---|---|
| Unity 6000.3.14f1 主工程 import/compile | exit 0，fatal/compiler error 0 | [`MainCompile.evidence.json`](../../TestResults/RuntimeV1V1Final/D1FINAL-20260830T221518Z-759ce097c2d8/MainCompile.evidence.json) |
| RuntimeV1Runnable EditMode | 11/11，0 failed/skipped/inconclusive | [`EditMode.evidence.json`](../../TestResults/RuntimeV1V1Final/D1FINAL-20260830T221518Z-759ce097c2d8/EditMode.evidence.json) |
| RuntimeV1Runnable PlayMode | 5/5，0 failed/skipped/inconclusive | [`PlayMode.evidence.json`](../../TestResults/RuntimeV1V1Final/D1FINAL-20260830T221518Z-759ce097c2d8/PlayMode.evidence.json) |
| Windows Development Player build | exit 0，410-file build inventory 闭合 | [`PlayerBuild.evidence.json`](../../TestResults/RuntimeV1V1Final/D1FINAL-20260830T221518Z-759ce097c2d8/PlayerBuild.evidence.json) |
| Player run | exit 0，`RuntimeV1RunnablePlayer: passed=True` | [`Player.evidence.json`](../../TestResults/RuntimeV1V1Final/D1FINAL-20260830T221518Z-759ce097c2d8/Player.evidence.json) |

主工程三个 Editor compilation 的 RSP 均恰好消费一次 canonical selector 和一次 analyzer；Runtime、Editor、AutoChess raw/loaded PE 中的 marker 均绑定同一 selector、artifact manifest 与 source inventory。Development Player 的 Runtime、AutoChess RSP 和最终 Managed PE 也保持同一 marker，详见 [`UnityRoute.evidence.json`](../../TestResults/RuntimeV1V1Final/D1FINAL-20260830T221518Z-759ce097c2d8/UnityRoute.evidence.json)。Editor AutoChess raw→loaded 与 Player AutoChess raw→built 的 PE bytes 都因 IL post-process 而不同；证据成立的是各自 MVID 与 marker 绑定一致，而不是泛化的 PE byte-exact，也不构成第二 generation。

Player 最终结果：

| 向量 | 结果 |
|---|---|
| Ability | Passed |
| GameplayEffect 9203 | Passed |
| AutoChess | Passed，winner=`Player`，4 ASC ready |
| SupportProfileAdmission | `Passed` |
| ProductionInstallAdmission | `NotEvaluated` |
| DeclaredFullSemanticEligibility | `false` |

## 6. 停顿审查结论

最终三条独立 Agent 审查线将 post-J scaffold、selector/analyzer、audit/archive/Tx、零 legacy、actual RSP/PE marker、E1 156 项闭包、四组 inventory 与 Player 分开复核，结果为 `P0=0 / P1=0`，未发现假绿。审查中发现的 E1 selector 范围、raw/corrected terminal、历史目录、备份计数与 PE byte-exact 等文档 P1 已在本结果中修正。保留的非阻断 P2 仅包括：final-level 专用 schema/index/verifier 与 PID/timeout、阶段前后 P/G/F 快照尚未自动落盘，`HistoricalGenerationCount` 字段名易误读，以及 Player 退出时一条 `ComputeBuffer` 释放警告；这些属于后续审计自动化或运行卫生，不扩入本轮。

当前没有需要继续扩展本轮的兼容补丁或无关测试；AIBridge 在无活动 Editor bridge 时只形成等待，已终止且未计为通过，随后同版本 Unity 的真实 compile/test/build/run 提供了完整验证。

至此 D1 与本轮 V1 可运行迭代关闭。后续若继续推进，应作为独立 release-hardening/语义完备迭代领取 B/C/Z、Tier-B、Release/IL2CPP 与性能证据，不再回滚为 legacy `.gen.cs` 双路线。
