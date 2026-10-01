# M12 — Diagnostics / Structured Log / Replay 模块规范审查

审查基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。本次为静态规范映射，不运行项目，不生成虚构profile结论。DBG/NAT/SYS规则是EX-GAS项目规则，Profiler/Journaling工具行为来自仓库的版本化官方机制解读；尚未核验本地PackageCache hash。

## 1. 职责与阅读覆盖

范围：Runtime/Debugger、Runtime/Event、V1/Boundary/GasRuntimeV1Diagnostics，以及Kernel发布诊断的必要调用链。

已读：OfficialToolDiff全文（349行）；DiagnosticEvidenceSnapshot全文；Scorecard的输入/输出、构造、ComputeMetricFamilyMask、ComputeDominantRisk；MetricFamilySnapshot的workload/API health/structural/overhead/聚合判定，重复纯赋值构造部分抽样；ReplaySinkPolicy、StructuredLogExport、StructuredLogView全文；GameplayFactKinds枚举核对；V1Diagnostics全文；GasTickJobs 9013–9097诊断发布。

未全审：全部外部调用者是否处于Functional/Profiler pass；测试runner的Journaling开关门；所有原始日志→新Boundary投影；完整replay recorder/重演器、跨平台数值域。Event目录中的DTO不应被误当已实现的完整Replay引擎。

规范已读：Diagnostics主题_index/API/DBG01–05，System SYS04/05，Native NAT01/05，数据流API；目标README、Spec06 Replay/Diagnostics/overflow及Spec07全文、Spec18 Profile门。

## 2. 逐规则矩阵

| 规则 | 判定 | 源码 | 结论 |
|---|---|---|---|
| [DBG-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-01.md#L10-L20)；[Spec07:3–17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/07-RuntimeCoreDebuggerSpec.md#L3-L17) | 受限符合 | [GasRuntimeDiagnosticEvidenceSnapshot.cs:38–90](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeDiagnosticEvidenceSnapshot.cs#L38-L90)；[GasRuntimeOfficialToolDiff.cs:15–39](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeOfficialToolDiff.cs#L15-L39) | counters/read-model与官方工具snapshot分开；未把Journaling称simulation replay。实际同capture id对账仍待验证 |
| [DBG-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-02.md#L10-L20) | 受限符合/异常安全缺口 P2 | [GasRuntimeOfficialToolDiff.cs:128–175](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeOfficialToolDiff.cs#L128-L175) | Begin保存旧开关、End恢复；CaptureSnapshot在恢复之前调用且无finally，异常路径可能不恢复。不能断言当前benchmark必开启Journaling |
| [DBG-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-03.md#L10-L20) | 符合（disabled口径）；待实测 | [GasRuntimeOfficialToolDiff.cs:145–154](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeOfficialToolDiff.cs#L145-L154) | 明示Profiler disabled及module未导出，不伪造capture；但只有状态不能替代Profiler artifact或按phase结构计数验收 |
| [DBG-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-04.md#L10-L20)；[SYS-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/System-World-SystemGroup/SYS-05.md#L10-L22) | 受限符合 | [GasTickJobs.cs:9076–9096](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L9076-L9096)；[GasRuntimeV1Diagnostics.cs:6–34](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasRuntimeV1Diagnostics.cs#L6-L34) | Kernel发布结构值，文本StringBuilder位于managed export；不能仅因存在字符串就报hot-path违规。所有调用时点未全核 |
| [DBG-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-05.md#L10-L20)；[Spec18:192–208](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/18-DOTS%E5%AE%98%E6%96%B9%E8%A7%84%E8%8C%83%E5%A4%8D%E6%A0%B8%E4%B8%8E%E6%80%A7%E8%83%BD%E7%BA%A2%E7%BA%BFSpec.md#L192-L208) | **目标差距 P1（证据门）** | [GasRuntimeDataOrientedScorecard.cs:60–74](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeDataOrientedScorecard.cs#L60-L74)；[GasRuntimeDataOrientedScorecard.cs:199–219](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeDataOrientedScorecard.cs#L199-L219) | 有Core/Boundary/Runner分组，但该scorecard缺Physics/render单列、p95/p99/测量窗口与环境fingerprint，不能独立作为规范性能通过证据 |
| [NAT-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-05.md#L10-L20) | **目标差距 P2** | [GasRuntimeDataOrientedScorecard.cs:292–305](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeDataOrientedScorecard.cs#L292-L305) | 70%容量压力硬编码，无profile/基准输入；固定阈值不能装作官方或通用通过线 |
| [Spec07:19–40](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/07-RuntimeCoreDebuggerSpec.md#L19-L40)；[Spec07:73–84](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/07-RuntimeCoreDebuggerSpec.md#L73-L84) | **目标差距 P1** | [GasTickJobs.cs:9083–9096](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L9083-L9096)；[GasRuntimeMetricFamilySnapshot.cs:153–208](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeMetricFamilySnapshot.cs#L153-L208) | 当前Kernel仅输出少量lane/outcome counters；DTO存在不证明slot/tombstone/allocator/fence/capture/stabilization证据已贯通 |
| [Spec06:130–148](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L130-L148) | **只读契约偏差 P2** | [GasStructuredLogExport.cs:8–27](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Event/GasStructuredLogExport.cs#L8-L27) | readonly struct公开裸数组且构造不复制，调用者可改数组元素；不是不可变快照。未据此推断Core会被改写 |
| [Spec06:144–159](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L144-L159) | 受限符合/目标差距 | [GasReplaySinkPolicy.cs:6–51](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Event/GasReplaySinkPolicy.cs#L6-L51)；[GasStructuredLogView.cs:35–61](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Event/GasStructuredLogView.cs#L35-L61) | 有cursor/dropped统计与结构事件，但旧Frame/Sequence DTO不是完整command+content/tick/canonical identity replay协议 |
| [DBG-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-03.md#L10-L20)；[Spec07:105–110](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/07-RuntimeCoreDebuggerSpec.md#L105-L110) | **证据表达缺口 P2** | [GasRuntimeMetricFamilySnapshot.cs:28–34](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeMetricFamilySnapshot.cs#L28-L34)；[GasRuntimeMetricFamilySnapshot.cs:239–246](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeMetricFamilySnapshot.cs#L239-L246) | HasEvidence以大于0判断，会混同“已采集且值为0”和“未采集”，零结构变化不能用该mask单独证明 |

## 3. 实质检查与建议

### M12-01：采集层与格式化层分离做得对，但应追踪调用时机

Kernel诊断仅构造GasTickDiagnostics，不格式化文本。OfficialToolDiff和V1Diagnostics使用Dictionary/List/StringBuilder属于显式managed诊断/导出路径，**不能因源码中出现new就断言Runtime Core GC污染**。

建议：每个export/capture入口携带PassMode/capture window，禁止由每target/每modifier热点调用；记录observer own cost。验收比较Debugger开关、不同消费者组合的相同输入结果/hash，测量边界导出GC和耗时并单列。

### M12-02：70%压力线缺少版本化依据（P2确定代码与规范偏差）

ComputeDominantRisk以ActiveEffectSlotCount*100 >= Capacity*70产生BufferCapacityPressure，输入没有阈值profile。该分支是风险提示而非真正性能门，故不夸大为业务错误；但NAT-05明确不设无场景基准固定百分比。

建议把warning阈值放ScaleProfile并携带baseline/environment；或输出纯occupancy与unknown threshold，不作通用压力裁决。乘法使用long或安全比例，避免较大int乘法溢出。验收容量0、低/高水位、int边界；报告显示采用的profile版本和告警依据。

### M12-03：采集有效性不应靠“数值非零”推断（P2）

HasEvidence把全0structural counters视为无证据。对于“本Tick零结构变化/零spill/零unexpected fence”，0本身应是重要成功测量。当前mask无法区分未采集、不可用、采集成功零值，降低机器验收解释力。

建议每family有Collected/Unavailable/Disabled状态及capture/tick范围；payload保留真实0。验收同一全0值在“未开启”与“已采集”两种状态产生不同状态码，不能把default DTO当成功或当坏性能。

### M12-04：Journaling恢复缺少异常保证，global工具状态需谨慎（P2）

Begin会强制enable并Clear全局Journaling；End先CaptureSnapshot再恢复先前值。CaptureSnapshot包含官方API读取、集合构造与格式化；任一异常时恢复代码不会执行。Headless清理还通过反射调用非public Shutdown，版本变化或调用异常需可见。

建议用try/finally恢复开关，并明确capture独占/嵌套禁止契约；不要悄悄清掉另一个正在使用的捕获。用受支持版本策略替代未校验反射，或显式报告不支持。验收正常/导出失败/嵌套capture/原先已开启四种路径，检查原状态恢复与Native leak证据。这是异常安全风险，未声称已触发实际泄漏。

### M12-05：日志导出“不可变”承诺不成立（P2，范围限定）

GasStructuredLogExportSnapshot.Entries是public readonly数组引用；构造直接保留传入数组。readonly限制引用重赋值，不限制Entries[0]赋值。两个consumer共享该snapshot时可以影响彼此看到的日志；若producer仍持有输入数组，producer也能改变旧snapshot。**没有证据说明此数组回连ECS，不能宣称Core权威被修改。**

建议构造防御复制、对外IReadOnlyList/只读memory且明确所有权；与GasBoundaryDrainBatch的copy+Array.AsReadOnly一致。验收修改原数组不影响snapshot，consumer无法通过正常public API改元素。

### M12-06：scorecard与完整目标证据仍有距离（P1验证能力差距）

当前数值输入大多由调用方提供，ProfilerEvidencePassed也是外部bool，不能独自证明真实capture存在。Spec07要求tick/slot/capture/stabilization/Boundary/API health可机器读取；Spec18要求环境、版本、采样窗口、分位数、N-tick memory与各域cost。当前scorecard不是这些证据的完整容器。

建议建立版本化EvidenceEnvelope，区分measured/estimated/configured budget；关联Epoch/Tick/Build/CaptureId/Profiler artifact。日志应为可审计原始事实生成，不用“报告文件存在”充当验收。缺Physics/render时写disabled reason，不伪造0ms。

验收：Functional不能出性能pass；Profiler无artifact不能passed；benchmark关闭Journaling；0/1/N Tick+稳态/冷启动分离；每个scorecard数值能追到采样源和单位。

## 4. Replay目标边界

Event目录当前提供日志DTO、cursor和retention统计，优点是纯值、无ECS可写入口；尚不足以承担Spec06的Session/content hash、完整权威command输入、canonical身份、semantic/transport hash分层及incomplete policy。应把“结构化日志可导出”和“可确定性重演”分开验收。未对其它目录的完整Replay实现作全仓否定结论。

## 5. 不可声称的结论

本报告不声称0 GC、Journaling已关闭、Profiler已捕获、Debugger开关hash一致、跨平台重放通过或性能达标。以上都需要在真实目标Player、明确构建/场景/窗口下取得证据。

