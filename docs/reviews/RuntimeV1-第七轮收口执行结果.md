# Runtime V1 第七轮收口执行结果

> 日期：2026-08-30  
> 状态：完成  
> RunId：`RV1-20260830T070718Z-ab754759ec68`  
> 交付：`RuntimeV1-Runnable-ClosedWorld` Development Player 可运行功能基线

## 结论

Runtime V1 本轮目标已经完成：受限闭世界功能基线通过最小静态门、EditMode 11/11、PlayMode 5/5，并在真实 StandaloneWindows64 Development Player 中依次跑通 Ability、production GameplayEffect 9203 与 AutoChess 三个向量。Player 进程 exit 0，结果、运行清单、源码、生成物与构建产物身份闭环一致。

本结论只声明 **Development Player 可运行的 Runtime V1 受限基线**。它不声明 Release/IL2CPP、production install admission、crash-atomic 发布链或完整 UE-GAS 语义已经完成；`DeclaredFullSemanticEligibility` 仍为 `false`。

## 本轮必要改动

1. `GasTickJobs.TryAllocateCaptureRange` 在 allocator 推进 `ValueHighWater` 后同步扩展 payload 的 logical Length，关闭首次非瞬时 Effect capture range 触发 `PostAdmissionInvariantViolation(1019)` 的 P0。
2. `RuntimeV1RunnableScenarioRunner` 不再二次 drain 唯一 Boundary ring，改为按 cursor 读取 ObservationGateway 已投影的结构化日志，恢复 production 9203 三阶段 period delta 的真实验证。
3. Standalone scripting define 补入 `URP_COMPATIBILITY_MODE`，使项目现有 URP Compatibility Mode 配置满足 Unity 6.3 Player 构建前置；未迁移渲染路径、未扩大 Runtime 语义。
4. Amendment3 launcher 在 dot-source 前固定 evidence helper SHA，并通过 `LauncherTrust.Amendment3.json` 将当前 launcher、A2/A3 helper 与四份既有绿色证据显式绑定。

本轮没有重跑 CodeGen，也没有手改 generated 文件。最终生成物身份在 Edit、Play、Build 和 Player 前后均保持不变。

## 身份与证据

| 身份 | SHA-256 / 值 |
|---|---|
| ProducerFingerprint `P` | `3cd42fde517c3d7f8b217846dd3798c980634438a149eedb9dc3782694ae79fb` |
| GeneratedArtifactIdentity `G` | `18fe1344cf0754728c55ced657919e939a6d0ea382bc0b78df74a4daa267d3af` |
| FinalSourceFingerprint `F` | `85078d0a12e7b54fecdd38b040c0538b55fe14a2422e645bd9411891eab685db` |
| BuildHash | `6d2164ad3024f034760532379fea8128b42b2203b45cd8fbba2259cfbb1fd89c` |
| 最终 RunManifest | `2844b1e040b2256f2b721414d3f0ee3e9297f96ac8445f698af141cdcd644aad` |
| PlayerResult | `880acdb8ae6da07768466a1051a294aa1e3cfacd72961d89419e26058ffa951f` |
| Player provenance | `53496b1900e872afe0816ca08c6de7c68b4ef86c111299c1d2325aed6f4b07fd` |
| LauncherTrust | `9780888793abaad61308006bedc48dcd98c1804312e7b93e423e88f625920a08` |

权威证据根为 [`TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68`](../../TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/)。最终入口文件为：

- [`RunManifest.json`](../../TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/RunManifest.json)
- [`EditMode.Amendment3.provenance.json`](../../TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/EditMode.Amendment3.provenance.json)
- [`PlayMode.Retry2.provenance.json`](../../TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/PlayMode.Retry2.provenance.json)
- [`Player.Amendment3.provenance.json`](../../TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/Player.Amendment3.provenance.json)
- [`PlayerResult.json`](../../TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/PlayerResult.json)

## 最小验证结果

| 门禁 | 结果 | 关键证据 |
|---|---|---|
| Amendment3 freeze | 通过 | `F=85078d0a…`，886 条最终源码记录 |
| 静态门 | exit 0 | 生成物未漂移，未再次生成 |
| EditMode `RuntimeV1Runnable` | 11/11，通过；0 failed、0 skipped | source 与 generated identity 前后一致 |
| PlayMode `RuntimeV1Runnable` | 5/5，通过；0 failed、0 skipped | Boundary、Ability、9203、AutoChess required TestId 全部命中 |
| Development Player build | exit 0 | Development、唯一 scene、410 条构建清单，`BuildHash=6d2164ad…` |
| Development Player run | exit 0 | `RuntimeV1RunnablePlayer: passed=True`，结果身份与 manifest/build/F/G 一致 |

Player 运行结果：

| 向量 | 结果 | 摘要 |
|---|---|---|
| Ability | 通过 | activate=1、commit=1、normal End、owner terminal |
| GameplayEffect9203 | 通过 | terminals=3、stack `3>2>1>0`、三个 period stage 数值闭合 |
| AutoChess | 通过 | winner=Player、Scale=1、AscCount=4、alive=2/0、ingress closed |

`SupportProfileAdmission=Passed`、`ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`，与本轮声明边界一致。

## 失败历史与修正链

所有失败产物均保留，未覆盖为绿色：

1. 原 PlayMode Attempt1 为 3/5，定位并修复 payload logical Length P0。
2. PlayMode Retry1 为 4/5，唯一失败是 9203 验证器在 ticker 后再次消费 Boundary ring；改为读取唯一结构化投影后，Retry2 为 5/5。
3. Player Build Attempt1 在 Runtime 编译完成后被 Unity 6.3 URP preprocess 拒绝，原因是 Compatibility Mode 已开启但 Standalone define 缺失。失败日志与哈希记录在 [`PlayerBuild.Amendment3.Attempt1.provenance.json`](../../TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/PlayerBuild.Amendment3.Attempt1.provenance.json)。补齐项目配置后没有改变冻结 Runtime F，因此未重复运行已经绿色的 Edit/Play；同一 launcher 与同一 Play 5/5 前置完成了 fresh Player build/run。

## 停顿审查与交付清单

1. 修改范围与两个 Runtime 阻断、一个 Player 构建配置阻断一一对应，没有顺手重构或双实现。
2. C# 9.0、程序集引用与 Unity API 由完整 Development Player 构建验证；Player 实际运行成功。
3. ProjectSettings YAML 只增加一个既有 Standalone define，无 fileID、GUID、引用或 `.meta` 变化；Unity 导入、编译和构建均通过。
4. 多 Agent 最终交叉快审未发现 P0/P1；launcher/helper/evidence supersession 绑定通过。
5. Player 与 Unity 进程均已退出。Player 日志无 error/exception；Build 日志只有既有 deprecated、package/caching/网络类非阻塞告警，最终结果明确为 Success。
6. AIBridge `compile unity` 因当时没有活动 Editor bridge 而等待无输出，约 90 秒后人工终止，未把它标记为通过；同一工作树随后完成的真实 Development Player 全量编译、构建和运行作为更强验证证据。

## 后续边界

本轮不继续追加测试或功能。完成后停顿审查已经把 V1.1 第一门收敛为纯 `D0-M2F`，详见[Runtime V1 完成后停顿审查与 V1.1 D0-M2F 单轮计划](RuntimeV1-完成后停顿审查与V1.1-D0-M2F单轮计划.md)。该门只裁决 sole Unity selector 与物理路线，不夹带 Runtime P1，也不等于 selector P0 已关闭。

production install admission、选中路线完整实验、D0-M2R、Release/IL2CPP、crash-atomic 发布与完整 UE-GAS 语义仍属于后续；这些项目不得反向否定本次 Development Player 可运行基线，也不得在当前 V1 收口中继续扩 scope。
