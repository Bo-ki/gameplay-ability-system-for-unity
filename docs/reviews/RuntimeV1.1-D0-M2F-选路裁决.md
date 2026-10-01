# Runtime V1.1 D0-M2F 选路裁决

> 日期：2026-08-30  
> 当前裁决：`CandidateSelected`  
> Runtime V1：保持 `Runnable-ClosedWorld`；Run5 的 protected production paths 未漂移  
> 外部人工门：`HumanConfirmed`（2026-08-30；机器 aggregate 的 `HumanConfirmationRequired=true` 保持历史原值）  
> 下一门：`ImmutableTarballFullFaultExperiment`  
> D1：`NotAuthorized`

## 1. 结论

用户显式授权的 fresh Run5 已完整执行 J0→J1→T→S→X→J2，并以退出码 0 发布 `CandidateSelected`。本轮冻结的合法终态为：

```text
Status = CandidateSelected
SelectedRoute = ImmutableTarball
SoleUnityConsumedSelector = Packages/manifest.json
HumanConfirmationRequired = true
D1Authorized = false
ProductionInstallAdmission = NotEvaluated
DeclaredFullSemanticEligibility = false
```

Probe T 与 Probe S 均为 4/4 `Passed`；按照冻结裁决规则，在两条路线均形成可判定结果且 T 通过时，provisional candidate 固定为 `ImmutableTarball`。随后 Tarball Probe X 以 `ActiveGenerationRef=A`、`manifest=B` 制造双 selector 冲突，cold、计划 kill 前观测与 restart 后 Runtime/Editor/AutoChess 三程序集均消费 B，证明 Unity 的唯一选择权来自 `Packages/manifest.json`；`ActiveGenerationRef` 只能是 audit/promotion record。

该机器结果完成的是 D0-M2F 选路合同，不关闭 selector P0，也不自动授权 D1 或 production migration。Run5 发布时必须停在人工确认门；用户随后明确要求以完全权限继续执行，已满足 tuple 与 delta 的外部人工确认。机器 aggregate 的 `HumanConfirmationRequired=true` 作为当时终态保持不变，不得被回写为新的机器结果。

## 2. Run5 权威证据

Run5 根为 [`D0M2F-20260830T124005Z-c661f64e4c81`](../../TestResults/GasCodeGen/N2-G0-D0-M2F/D0M2F-20260830T124005Z-c661f64e4c81)。最终 aggregate 原始 SHA-256 为 `cbe4415b6ae62208d9b026a368e2f2cc90dda5a3f0ceb00caf323dbae9d45a05`，sidecar 原始 SHA-256 为 `b525e4009201f80a3e21c1561a6d31a7f1d9d4c9f2a14a97fcf2149a1a0dd0ce`，其 payload 精确绑定同一 aggregate。

| 证据 | 结果 | 原始 SHA-256 |
|---|---|---|
| [Probe T](../../TestResults/GasCodeGen/N2-G0-D0-M2F/D0M2F-20260830T124005Z-c661f64e4c81/ProbeT.json) | 4/4 `Passed`；immutable tarball cold A、A→B、restart B、role closure | `72e47ea65c286bf50a4694af18ed777c12d5d6740b08e6464f5ac3ba1cdd1c1e` |
| [Probe S](../../TestResults/GasCodeGen/N2-G0-D0-M2F/D0M2F-20260830T124005Z-c661f64e4c81/ProbeS.json) | 4/4 `Passed`；stable graph + SourceGenerator 路线可行 | `c8c87419459162e3f6a24b9e3b1a38ca2c3337c21fd4ecd34dbd9aba0e6ad99c` |
| [Probe X](../../TestResults/GasCodeGen/N2-G0-D0-M2F/D0M2F-20260830T124005Z-c661f64e4c81/ProbeX.json) | 4/4 `Passed`；ref=A 与 manifest=B 冲突时三程序集只消费 B | `1cb2dfaf4f656c024c25e17c5e89c7414dcdbeadfcfdd6b06b306949443b4c3f` |

三份 probe 均再次通过公共合同 checker，并绑定合同 SHA-256 `4f6b8a1c237e5fa2fa7316484962c0153c05d01bfda81bc66d399b05e77db1e3`。X 的实际冲突结果不是“两个 selector 恰好同值”：`ActiveGenerationRef` 明确保持 A，manifest 明确指向 B，cold、pre-kill 与 restart 三次观测的 Runtime/Editor/AutoChess token 均为 B。

选中路线的角色闭包如下。下表仅按 Path/Role/Generation/ConsumerAuthority 做展示层去重；原始 Probe X 与 aggregate 保持不变，其中同一 B PackageCache compiled-projection 路径按 cold、pre-kill、restart 三次观测保留三行：

| 对象 | 角色 | Unity 消费权限 |
|---|---|---|
| content-addressed A/B `.tgz` | Authority / immutable payload | payload |
| `Packages/manifest.json` | Authority / mutable sole selector | `SoleUnitySelector` |
| `Packages/packages-lock.json` | Derived | none |
| `ProjectSettings/GasCodeGen/ActiveGenerationRef.json` | Derived | `AuditOnly` |
| `Library/PackageCache/**` 与解包目录 | Cache | none / compiled projection only；never selector |

J0 protected raw SHA-256 为 `83b1c2264f2c3a7b966d9732221b2dd1af2468cd827aa9eb1ae84d85122d5de7`；J0→J2 的 protected aggregate 均为 `65ea858df53ef8796f729c68813254ba5d813be09346eb8ed2c8f0a89b01c2d4` 且 `Unchanged=true`。J1→J2 的工具树均为 82 文件、aggregate `7f48c08d029469c4643800ead4a07e4d4ea60b8c74b66c275c6a0fe21c802dc8` 且 `Unchanged=true`。三个 probe 的 cleanup 叶与中央 residue gate 证明 suite-owned 收口；Run5 结束后的两次终局人工复核观察到 Unity、PackageManager、Bee、ShaderCompiler、CrashHandler 进程数为 0，三个 fixture 根及 `gas-codegen-d0-m2f-*` 临时残留均为 0。终局人工观察未另立持久化证据叶。

## 3. 五次 fresh 运行盘点

| Run | 机器终态 | 关键事实 | Aggregate SHA-256 |
|---|---|---|---|
| `D0M2F-20260830T093828Z-b2bb1730b701` | `Inconclusive/HarnessFailure` | T 在 Unity 前因空集合参数绑定失败；已补六处 `AllowEmptyCollection` | `6eff45ca42549b839e58fce0cfc631990368cef03f6516923fea8effdc882f29` |
| `D0M2F-20260830T103202Z-06344c5ef2c2` | `Inconclusive/HarnessFailure` | T 4/4 Passed；S 在 Unity 前因 `File.Copy` 括号错误把目标拼成 `Analyzers.meta/False` | `3471147f189403ccc81991d229e0b9ae0dd124a0dffb40bf331311abb1ad3c81` |
| `D0M2F-20260830T104445Z-3807c3dc2f18` | `Inconclusive/UnityProcessResidue` | 启动静态门与 Probe 前检测到 `Unity:18848`、`UnityCrashHandler64:25288`、`UnityPackageManager:32196`，按独占门 fail closed | `2ca6f7e0862e4a09cd85131b5da04ca9ac96ec730baabd23ee43dd188236c79d` |
| `D0M2F-20260830T112954Z-79b45a3c802a` | `Inconclusive/HarnessFailure` | T 4/4 Passed；S 的 A Unity 已生成 raw/log，但在首个 invocation 返回前读取 `unity-feasibility-a.log` SHA 遇到独占锁；`Invocations=[]`，X 未执行 | `77d9f0e8da3a344137c1af004934be2a142c99b517c9200423fc0d9e24d50afd` |
| `D0M2F-20260830T124005Z-c661f64e4c81` | `CandidateSelected/None` | T/S/X 均 4/4 Passed；选中 immutable tarball，manifest 是 sole selector；protected/tool 均 unchanged | `cbe4415b6ae62208d9b026a368e2f2cc90dda5a3f0ceb00caf323dbae9d45a05` |

另有 `D0M2F-20260830T104316Z-552686cd8d46` 在 J0 冻结过程中检测到 `UnityLaunchedByThisRun=false` 的 PackageManager，已记录为 `AbortedBeforeJ0`，不可复用。

五次运行及 abort 根均永久保留，未删除、覆盖或拼接证据。所有已完成 J2 的 protected comparison 均为 `Unchanged=true`，保护聚合始终是 `65ea858df53ef8796f729c68813254ba5d813be09346eb8ed2c8f0a89b01c2d4`。Run5 的 `CandidateSelected` 只由本 RunId 内 fresh T/S/X 与 J2 身份闭包支撑，不复用 Run2/Run4 的诊断叶。

## 4. Harness 修复与静态证据边界

只修复了四类已观测、确定性的 harness/证据门缺陷：

- Tarball runner：允许 watcher/role collector 合法接收空集合。
- SourceGenerator runner：恢复 `File.Copy(source, destination, false)` 三参数；静态门现从主脚本 AST 提取并真实执行 `Copy-OrdinaryTree`，按相对路径与 raw SHA 比较完整 `Fixture~`，同时拒绝 `False/True` 路径段。
- SourceGenerator Unity 日志与进程 settlement：normal/kill 路径收口 owned 根进程与 stdout/stderr/句柄；kill/异常清理使用 `Kill(true)` 加 30 秒 bounded wait，中央 residue gate 继续证明全局无 Unity 残留。Unity log SHA 仅对锁相关异常执行最多 `50×100ms` 的有限重试；缺失日志及持续锁映射 `HarnessFailure`；普通 invocation cleanup 失败映射 typed `CleanupFailure`，planned kill/kill-probe cleanup 失败映射 `UnityProcessResidue`。静态门从主脚本 AST 执行真实 helper，已通过 500ms 短锁精确 SHA 与 3000ms 持续锁有限失败回归。
- 中央 runner：在 U0 前强制 Dispatch tool identity 与 J1 精确一致，漂移映射 `InputIdentityDrift`；任何含 `J0.Aborted.json` 的 run root 永久拒绝复用。

修复后关键身份：

- Central runner：`6eb30b7298030965c5122de490f7b791fe59554a3ea7e842189979d26c16d727`
- SourceGenerator main：`2b30d34f70f0139ac341b4d8769d187f4a123dcffb4b9e75d77725b81ee3034f`
- SourceGenerator static：`de31021deecc98ae6df9b138afbad353fa0d57e9813b88e82b953a8c9fe5d3f4`
- 完整 82 文件 tool aggregate：`7f48c08d029469c4643800ead4a07e4d4ea60b8c74b66c275c6a0fe21c802dc8`

Run5 中 Contract、Tarball、SourceGenerator 三条中央静态门均为 `Passed=true`；其日志 SHA-256 分别为 `f8b33b63666d252d33fd89b66b331636bbb3edec682407662bc27cde62d1463d`、`a840fa8bec2d9b7469e703fabcd6c624147c6b3889a159a6f462810f2324ad44`、`42119fa9e4b2521618ece5213c725960b18c9b2806b0fb4b3a6a3f6ec0da1bc2`。本轮没有增加或执行 official CodeGen、Runtime 回归、Player、双跑或长跑。

非阻断审计边界：当前 `D0M2F.aggregate.schema.json` 实际验证 probe result，terminal aggregate 由中央程序化 invariant 与 sidecar 保护，并无另一份独立 aggregate JSON Schema；fixture 按合同清理后，probe 内引用的临时 raw log/path 不再可打开，只保留冻结的 typed payload 与哈希。二者不改变本轮合同结论，但应在后续完整实验增强事后取证深度。

## 5. 人工确认门与已批准 delta

Run5 已达到计划定义的停止点，以下 tuple 与边界已于 2026-08-30 获得外部人工确认；不再启动 Run6 或额外 feasibility probe：

```text
(ImmutableTarball, Packages/manifest.json, Authority/Derived/Cache table)
```

已批准写入 Spec08/ADR 的 delta 为：

1. generation A/B 以内容寻址、不可变 `.tgz` 作为 Authority payload。
2. `Packages/manifest.json` 是 Unity 唯一 active selector；不得再有 stable alias、materializer 或第二 active authority。
3. `Packages/packages-lock.json` 是 Derived；`PackageCache`、解包目录与 Bee/RSP 是 Cache/compiled projection，均不得成为 fallback selector。
4. `ActiveGenerationRef` 降为 audit/promotion record，不参与 Unity active generation 选择。
5. SourceGenerator 路线虽在 Probe S 中通过，但按冻结优先级未被选中，不授权并行进入 production。

人工确认已满足；独立规范变更负责把已批准 delta 写入并冻结 Spec08/ADR，同时同步直接复述旧 selector 事实的 20-Spec 与 91-术语表。规范冻结后的下一执行任务只能是 tarball 路线完整故障实验；该实验技术通过后，才另立唯一 `D0-M2R` owner 任务。`D0-M2R` 完成并再次确认 ADR 后，方可重新评估 D1。当前继续保持 `D1Authorized=false`、`ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`。
