# M19 AutoChess Application / Integration

## 基线与判断口径

审查提交：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`（EX-GAS-2.0）。只读 GitHub 源码/规范；没有执行 Unity、项目代码、测试、Profiler 或修改项目。本文只新增报告。**符合**仅指已核对静态路径；**受限符合**限于当前支持面；**目标 gap**是相对目标规范的差距；**待实测**不能由静态阅读证明；**N/A**表示规则的 API/执行域不适用。项目规则不是 Unity 通用强制规定。

## 职责与覆盖

Application Shell 负责房间/单位描述、确定性 intent、启动和业务报告；Integration 是到 Runtime owner、CommandPort、Boundary ring 的适配器。这里的 class、Dictionary、Stopwatch、字符串并不天然违反 ECS；重点是是否越界持有权威状态、推进错误时间链或污染证据。

重点逐段阅读：
- AutoRunner/AutoChessRuntimeRunner.cs、AutoChessRuntimeSystemBootstrap.cs；Editor/RuntimeV1RunnablePlayerBuilder.cs
- Battle/AutoChessBattleSession.cs、Battle/Flow/AutoChessBattleFlow.cs、Battle/AutoChessBattleContracts.cs
- Integration/GasCore/AutoChessGasRuntimeHost.cs、AutoChessGasRuntimeTicker.cs、AutoChessGasRuntimeAccess.cs、AutoChessGasBattleEntityLifecycle.cs
- Battle/Ecs/AutoChessBattleDefinitionCatalogBuilder.cs；Integration/GasCore/AutoChessGasObservationGateway.cs（drain、hash 与 observation 部分）

抽样/未逐行：GameRoom/AutoChessGameRoomDefinition.cs、Battle/Validation 的全部历史统计与报告分支、RuntimeV1RunnableScenarioRunner 的全部微场景、RuntimeV1RunnablePlayerOrchestrator 的 CLI 分支；prefab/scene 二进制及生成 selector 未作全量审计。相邻 Kernel/Boundary 的内存正确性由对应模块审查，本文不据调用名推断已通过。

## 逐规则适用矩阵

| 规则/标准 | 源码证据 | 裁决 |
|---|---|---|
| [02 四层](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/02-%E5%9B%9B%E5%B1%82%E6%9E%B6%E6%9E%84Spec.md#L1-L42)：Shell只表达intent/消费结果 | [投递与推进](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Battle/Flow/AutoChessBattleFlow.cs#L69-L83)、[managed ring](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeAccess.cs#L64-L81) | 受限符合：走CommandPort/managed ring；内部adapter仍可拿EntityManager，仅限bootstrap |
| [PRF-27](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-27.md#L1-L25) / Spec10完整父链 | [owner.TickBatch](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeTicker.cs#L11-L31) | 符合所查调用：没有手动Update子System；0/1/N调度等价待实测 |
| [Spec10时间](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10-AutoChess%E6%97%A0%E5%A4%B4%E9%AA%8C%E6%94%B6Spec.md#L31-L41) | [20Hz与固定profile](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Integration/GasCore/AutoChessGasBattleEntityLifecycle.cs#L234-L275) | 目标gap：TickRate=20、ProfileHash=1、MaxTicksPerBatch=1为手写常量，不是生成profile语义身份 |
| [Spec10最终Seal](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10-AutoChess%E6%97%A0%E5%A4%B4%E9%AA%8C%E6%94%B6Spec.md#L13-L27) | [Complete先于Dispose](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Battle/Flow/AutoChessBattleFlow.cs#L93-L148)、[两次cleanup tick](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Battle/Flow/AutoChessBattleFlow.cs#L242-L246) | 目标gap，P1：结果返回前未包含teardown完成证明 |
| [DBG-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-05.md#L1-L20) | [整batch计时](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeTicker.cs#L23-L38)、[第三槽名为CoreSimulation](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Battle/AutoChessBattleContracts.cs#L396-L423) | 目标gap，P1：整owner batch时间被记作CoreSimulation |
| [CONTENT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Prefab-Content%E7%AE%A1%E7%90%86/CONTENT-01.md#L1-L22) | [persistent catalog](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Battle/Ecs/AutoChessBattleDefinitionCatalogBuilder.cs#L148-L185) | 受限符合：定义使用immutable Blob、明确dispose；shutdown时序需实测 |
| [MAT-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Mathematics-%E7%A1%AE%E5%AE%9A%E6%80%A7/MAT-05.md#L1-L20) / [hash分层](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L104-L128) | [Boundary摘要编码](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Integration/GasCore/AutoChessGasObservationGateway.cs#L766-L831) | 受限符合：显式字段编码；含OwnerSequence与Cue epoch，不可直接冒充canonical gameplay hash |

## 实质发现与验收

### M19-01｜P1｜结果生命周期未达到最终验收封印
Complete先ExportDiagnostics/Build result；Dispose随后Close、两次完整AdvanceFixedTick、Shutdown。触发：caller使用返回结果作为“最终验收通过”，但后续cleanup仍可能产生新事实或失败。即使Kernel已对terminal gameplay封闭，这也不等于DisposedReceipt/ValidationResultSeal已完成。**不能把此发现表述为已证实多执行伤害**。

验收：BattleOutcome与最终ValidationResult分离；FinalDrain Accepted→资源/Blob/World释放→DisposedReceipt→结果seal；无下一tick shutdown直接drain，不用固定两tick猜测完成；在cleanup故障、零ASC、terminal后尾请求下，最终结果fail-closed且seal后零新fact。

### M19-02｜P1｜计时域错误归因
Ticker Stopwatch覆盖owner.TickBatch，随后把elapsed放入CoreSimulation槽；实际owner batch包含完整调度/同步/Boundary，而adapter自己的DrainRuntimeV1BoundaryBatches又在计时外。会把batch开销归因Core，并低报观测成本。

验收：用独立marker分别记录Core每tick、Physics、EndFixed、Drain每outer、adapter observation、bootstrap/teardown；未采集项写NotCaptured，不能填0后当成零成本；与目标Player Profiler逐组校准。

### M19-03｜P2/目标gap｜硬编码profile限制规模证据
Stage-B固定20Hz、一Battle、单tick batch与ProfileHash=1，unitCount仅乘出容量。可用于封闭示例，不证明通用ScaleProfile与50个隔离Battle。

验收：profile内容变动改变hash；20/30/60Hz、0/1/N batch和多Battle使用同command输入获得相同规范化语义结果；逐项capacity由有效支持面推导，溢出typed reject。

### M19-04｜P2/待实测｜资源释放顺序与异常安全
[Host初始化/清理](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeHost.cs#L14-L44)先Uninstall catalog再owner.Dispose；Install抛异常时已创建World没有finally统一回收。当前同步TickBatch可能已完成producer，不能单凭顺序宣称UAF，但不满足显式producer完成→最后drain→Blob释放的可审计路径。

验收：安装异常、mid-batch fault、连续启停100次均无World/Blob泄漏；释放前记录producer fence及final drain receipt。

## 正面项与下一步

- 独立World隔离默认世界拓扑，且不自动附加PlayerLoop；业务通过owner推进
- managed ring消费替代Demo直接查询ECS buffer；catalog不再作为每Ability实体
- Windows Player builder拒绝复用已有输出并检查BuildResult，但只覆盖Development Windows，不能当Release/IL2CPP证明
- 先修结果封印和计时归因，再扩规模；不要因本模块使用托管Shell而机械改写成IJobEntity

