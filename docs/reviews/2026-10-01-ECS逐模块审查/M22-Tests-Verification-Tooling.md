# M22 Tests / Verification / Diagnostics Tooling

## 基线与覆盖方法

固定提交 `61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。GitHub递归树完整3130条，未截断。只读源码和清单；未运行测试、Unity、PowerShell或Profiler。38个RuntimeV1 *Tests.cs是**源码文件数量**，不是test case数量或通过率。Tier-B清单静态解析为17向量、40唯一TestId、4 Green/13 Pending；这是登记状态，不是本轮测试结果。

重点已读：Tools/Tests/Run-RuntimeV1ConformanceTests.ps1、TestRunProvenance.ps1、RuntimeV1TierBManifest.json；RuntimeV1RunnablePlayModeTests、RuntimeV1RunnableSupportProfileTests、TierBVectorConformanceEditModeTests；GAS/Tests/TierBAvatarRebindFrozenSpatialTests；AutoChessLubanDamageRegressionTests；Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1、Verify-GAS-EditorOdinExit.ps1、Analyze-AutoChessProfile.ps1（输入、计时和报告分支）。

未逐行：RuntimeV1其余约35个测试文件、CodeGen D0/D1全部故障矩阵与Proofs exhaustive cases、Unity官方样例测试、binary test artifacts。它们存在，不据本次抽样断言不存在覆盖。历史TestResults报告不等于当前源码已实测。

## 规则矩阵

| 规则 | 源码/清单证据 | 裁决 |
|---|---|---|
| [Spec10版本化向量门](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10-AutoChess%E6%97%A0%E5%A4%B4%E9%AA%8C%E6%94%B6Spec.md#L70-L100) | [执行子清单](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/Tests/RuntimeV1TierBManifest.json#L1-L23)、[scope/hash门](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/Tests/Run-RuntimeV1ConformanceTests.ps1#L157-L217) | 受限符合：明确不能单独授权V0；13 Pending不能被看作release通过 |
| [Spec21独立样例oracle](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/21-AutoChessDemo%E7%AD%96%E5%88%92%E9%85%8D%E7%BD%AE%E9%AA%8C%E6%94%B6%E6%A0%B7%E4%BE%8BSpec.md#L29-L51) | [独立冻结12伤害](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Tests/EditMode/AutoChessLubanDamageRegressionTests.cs#L12-L63) | 符合所查断言：经公开报告读取、不是Catalog自证期望值 |
| [Tier A/B/C分层](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10B-AutoChess%E5%AE%8C%E6%95%B4%E4%B8%9A%E5%8A%A1%E6%A1%88%E4%BE%8B/10B-08-%E7%9C%9F%E5%AE%9E%E4%B8%9A%E5%8A%A1%E9%93%BE%E4%BA%8C%E8%BD%AE%E5%AE%A1%E6%9F%A5%E4%B8%8E%E7%96%91%E7%82%B9%E8%A3%81%E5%86%B3Spec.md#L23-L35) | [值对象测试](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Tests/TierBAvatarRebindFrozenSpatialTests.cs#L6-L68) | 受限符合：源码主动说明不替代完整rebind运行链 |
| [DBG-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-03.md#L1-L20) | [过时requiredPaths](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1#L66-L81) | 目标gap，P1：D1正确迁移后旧路径门必失败 |
| [DBG-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-05.md#L1-L20) / [分层性能证据](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10-AutoChess%E6%97%A0%E5%A4%B4%E9%AA%8C%E6%94%B6Spec.md#L141-L152) | [timing与evidence字段](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/Diagnostics/Analyze-AutoChessProfile.ps1#L383-L451)、[缺Profiler提示](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/Diagnostics/Analyze-AutoChessProfile.ps1#L657-L684) | 受限符合：识别缺Profiler；只是派生分析，不能代替采集/身份闭包 |
| [BUR-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-05.md#L1-L20) | [精确period断言](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/_Test/RuntimeV1/PlayMode/RuntimeV1RunnablePlayModeTests.cs#L83-L102) | 功能静态符合；Editor test存在不证明目标Player AOT通过 |
| [GFX-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/EntitiesGraphics/GFX-04.md#L1-L20) / [paired运行与最终Seal](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10-AutoChess%E6%97%A0%E5%A4%B4%E9%AA%8C%E6%94%B6Spec.md#L154-L165) | [scale1终局](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/_Test/RuntimeV1/PlayMode/RuntimeV1RunnablePlayModeTests.cs#L117-L136) | 待实测/目标gap：该case只断言单一scale1，不能当有头无头/规模门 |

## 实质发现

### M22-01｜P1｜边界检查脚本仍要求已删除的生成源
Verify-GAS-RuntimeCoreBoundary.ps1 L74-76强制存在RuntimeAbilityActivation.gen.cs、RuntimeEffectInstant.gen.cs、RuntimeActiveEffect.gen.cs。当前完整Assets/Packages树的active .gen.cs数量是0，D1已改为analyzer+selector+required anchors。现行正确安装会在Assert-Exists阶段失败。

触发：开发者按工具名执行Runtime边界验证；可能误导其恢复legacy文件，违反唯一生成路线。验收：required path gate改为现行selector/analyzer/anchor与三assembly marker身份；缺任一项失败，正常D1树通过；人为放回legacy active source必须拒绝。此项是高置信静态路径矛盾，不声称本轮实际运行报错。

### M22-02｜P1｜测试进程退出没有加入判定
[启动到分类](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/Tests/Run-RuntimeV1ConformanceTests.ps1#L394-L431)记录UnityExitCode后只验fresh XML/log/exact cases/fingerprint，没有将退出码与预期状态关联。Start-Process -Wait也无timeout。合法ApprovedRed可能需要特定非零码，不能简单无条件要求0，但异常退出必须区分。

验收：Passed XML+异常非零退出、挂起、过期XML、缺case、log泄漏均不通过；ApprovedRed检查明确允许码及失败签名；超时输出独立终态证据并清理子进程。

### M22-03｜P1/证据gap｜历史Green与provenance没有完整可解析闭包
LastRunEvidenceId仅要求非空；aggregate保存ProvenancePath但不hash sidecar。该runner的source hash、exact discovered cases、工作区前后fingerprint、log健康门是实质优点，仍不足以独立验证历史Green的原始artifact与对应build。

验收：EvidenceId解析到不可变manifest/XML/log/provenance/build；相对路径可移植；任何缺失或hash不符拒绝；区分test expected matched、conformance gate passed、release eligible三个状态。

### M22-04｜P2｜clean-clone测试需显式bootstrap
[raw输入路径](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/_Test/RuntimeV1/EditMode/RuntimeV1RunnableSupportProfileTests.cs#L240-L249)要求被.gitignore忽略的Luban JSON。新clone直接-runTests无法依赖开发者缓存。生成脚本存在，不能说没有导表能力。

验收：统一入口检查Unity/SDK、导表、构建generator、安装验证，再运行测试；在没有Library/generated JSON/bin/obj的工作区成功或给出明确前置失败。构建文件缺失问题详M23。

### M22-05｜状态治理｜不能恢复旧“normalEnd/9203完全缺失”结论
当前Runnable tests已有fault terminal/gate close（L18-26）、公开Activate/Commit/normalEnd（L58-69）、9203三个精确period区间及final removal（L84-102）。这些是存在的测试，不是本轮通过证明；同时也没有覆盖完整inhibit/reactivate/Cue/Wait支持面。4Green13Pending有合理范围差异，不应仅为数字好看改绿。

验收：每个历史问题映射“closed-world已覆盖/通用语义仍Pending/证据待发布”；每轮一个production catalog→public command→精确Boundary结果纵向用例，再扩大支持面。

## 正面检查

- runner对17向量编号、schema、source hash、真实Assembly/TestId/case count有显式拒绝门
- Pending始终阻断conformance；不是NUnit metadata wrapper就自动成功
- poison case精确断言11次delta，damage regression独立冻结策划值12
- Analyze脚本把Profiler缺失显式化；但其生成文字不能替代真实Profiler和Journaling独立pass

## 最小下一轮

先修M22-01/02并发布clean-clone基线证据；随后按Spec10分别建立Functional、Determinism、Scale、Profiler、Journaling、Deep Trace，而不是合并为一个“测试通过”。测量报告须包含版本/平台/build/profile/样本窗，未采集填NotCaptured。

