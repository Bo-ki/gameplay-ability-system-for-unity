# M11 — Boundary Command / Drain 模块规范审查

基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。静态只读；未执行Unity、并发测试、故障注入或性能测量。规则编号是EX-GAS项目规则；cleanup/ECB/Job依赖是其依托的Unity机制，不能混称官方强制。符合仅针对列示证据，范围限制不能抵消目标Spec差距。

## 1. 职责与已读范围

本目录把外部请求转换为稳定身份命令，管理accept/duplicate/close/terminal，冻结ECS outbox的watermark并交给唯一managed staging。它不应成为第二个gameplay裁决器。

已读/抽样：
- GasCommandPort 全文；GasBoundaryCommandProtocol 的稳定target/spatial/payload与journal duplicate章节
- SessionIngressGate 的TryAccept、freeze、authority/target校验、关闭与支持面限制；terminal ledger细节只作抽样，未穷尽竞态排列
- GasIngressAuthoritySnapshot 的clone/形状；GasRequestTerminalProtocol 的结果结构/终态契约抽样
- GasBoundaryDrainProtocol 的route、freeze、accept、no-fact、reuse watermark；GasBoundaryDrainCoordinator 的batch/ring、drain、final drain、shell判定
- GasRuntimeWorldOwner 的fence、fault/terminal close、dispose；GasTickJobs 请求terminal和Cue projection章节
- GasRuntimeV1Diagnostics 的文本export另见M12

未全审：全部public protocol字段组合、所有malformed receipt分支、cross-thread race、managed多消费者实现与每个Session API调用者。没有把“接口存在”当成全链验证通过。

依据：官方参考README；System/ECB/NativeContainer/数据流主题_index、API与原子规则；目标README、Spec06全文、Spec17边界/销毁、Spec18事务/红线；API选型基线§6。

## 2. 规则矩阵

| 规则 | 判定 | 精确实现证据 | 结论 |
|---|---|---|---|
| [SYS-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/System-World-SystemGroup/SYS-05.md#L10-L22)；[Spec17:108–128](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L108-L128) | 符合（命令载体） | [GasBoundaryCommandProtocol.cs:208–238](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryCommandProtocol.cs#L208-L238) | public target用Epoch/Battle/ASC/Avatar generation和空间值，无raw Entity/DynamicBuffer |
| [Spec17:51–58](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L51-L58)；[PRF-13](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-13.md#L10-L24) | 受限符合 | [SessionIngressGate.cs:160–239](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/SessionIngressGate.cs#L160-L239)；[GasBoundaryCommandProtocol.cs:689–709](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryCommandProtocol.cs#L689-L709) | accept与cutoff同锁、exact duplicate逐字段+逐字节确认，尾部进入新window。物理接受顺序不应直接代替gameplay stable order |
| [Spec06:72–92](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L72-L92)；[SC-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E7%BB%93%E6%9E%84%E5%8F%98%E5%8C%96-ECB/SC-02.md#L10-L24) | 符合（抽查） | [GasBoundaryDrainCoordinator.cs:262–337](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L262-L337)；[GasBoundaryDrainProtocol.cs:528–586](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainProtocol.cs#L528-L586) | 先freeze、copy、TryStage，再TryAccept清prefix；late tail保留。结构变化发生前后不跨持有旧outbox用于写入 |
| [Spec06:82–92](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L82-L92) | 符合（静态协议） | [GasBoundaryDrainProtocol.cs:429–493](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainProtocol.cs#L429-L493)；[GasBoundaryDrainCoordinator.cs:285–303](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L285-L303) | InFlight复用BatchId和watermark；当前没有将retry本身伪装为新batch |
| [ECB-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E7%BB%93%E6%9E%84%E5%8F%98%E5%8C%96-ECB/ECB-03.md#L10-L24)；[Spec18:99–103](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/18-DOTS%E5%AE%98%E6%96%B9%E8%A7%84%E8%8C%83%E5%A4%8D%E6%A0%B8%E4%B8%8E%E6%80%A7%E8%83%BD%E7%BA%A2%E7%BA%BFSpec.md#L99-L103) | 受限符合 | [GasBoundaryDrainCoordinator.cs:343–365](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L343-L365)；[GasBoundaryDrainCoordinator.cs:560–570](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L560-L570)；[GasTickJobs.cs:9–45](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L9-L45) | shell同时无ASC/Session identity；正常prepass走标准EndFixed，shutdown可direct cleanup。不能仅凭Exists判断业务存活 |
| [Spec06:72–80](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L72-L80) | 符合（批次不变性） | [GasBoundaryDrainCoordinator.cs:11–32](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L11-L32) | batch复制数组并Array.AsReadOnly，消费者不取得ECS buffer；不同于M12日志export的裸数组 |
| [Spec06:51–70](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L51-L70)；[Spec17:403–423](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L403-L423) | **目标差距 P1** | [GasTickJobs.cs:7293–7377](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L7293-L7377) | Cue事实是application marker，缺正式lifecycle/executed key与四阶段。profile限OnActive/Executed不能代替完整目标contract |
| [Spec06:152–159](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L152-L159) | **受限符合/目标差距 P1** | [GasBoundaryDrainCoordinator.cs:63–99](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L63-L99)；[GasRuntimeWorldOwner.cs:908–950](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L908-L950) | ring满统一拒绝staging并可关闭Session；验证模式可合理，尚无Presentation丢弃范围+reconcile与Replay显式不完整策略 |
| [NAT-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-05.md#L10-L20)；[DBG-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-05.md#L10-L20) | 待实测 | [GasBoundaryDrainCoordinator.cs:83–115](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L83-L115)；[GasBoundaryDrainCoordinator.cs:485–515](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L485-L515) | ring线性去重、RemoveAt(0)、batch多次复制属于managed boundary成本，不应归因Core worker或宣称allocation-free |
| [Spec17:64–77](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L64-L77)；[Spec18:159–169](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/18-DOTS%E5%AE%98%E6%96%B9%E8%A7%84%E8%8C%83%E5%A4%8D%E6%A0%B8%E4%B8%8E%E6%80%A7%E8%83%BD%E7%BA%A2%E7%BA%BFSpec.md#L159-L169) | **目标差距 P1** | [GasRuntimeWorldOwner.cs:458–492](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L458-L492)；[GasCommandIngressSystem.cs:69–78](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasCommandIngressSystem.cs#L69-L78) | Dispose能阻止新请求但不完整卸载执行域；同World重装新gate冲突，详见M10 |
| [Spec06:51–54](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L51-L54) | 受限符合 | [GasTickJobs.cs:6191–6308](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L6191-L6308)；[GasRuntimeWorldOwner.cs:502–508](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L502-L508) | 当前确有RequestTerminal projection+bridge，不再是旧“无终态”。未穷尽Core/envelope/gate tail的每种fault membership |

## 3. 六项实质审查

### M11-01：accept/freeze边界明确，验证要覆盖重复与关闭竞态

优点：gate持有自己的不可变权限快照，入口先phase/support/basic校验，再duplicate、authority、capacity、append；freeze在同一锁切走journal，显式返回cutoff sequence。payload duplicate并不只比hash，避免碰撞被误认完全一致。

局限：静态单锁不等于已经证明所有accept-vs-fault-close-vs-consumed竞态终态唯一。普通Apply/Activate成功也不自动证明“0..N child全集结束”符合Spec06 envelope语义。

建议/验收：同RequestId相同/不同字节、close与accept交错、fault前后tail、重试Terminal读取，逐RequestKey验证exactly-one terminal、预算只推进一次、来源不跨Battle。

### M11-02：两阶段drain与shell交接是当前明确优点

[GasBoundaryDrainProtocol.cs:545–585](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainProtocol.cs#L545-L585)确保Accepted重试幂等，新的tail回Pending；空shell另有NoFactReceipt，避免“空即丢弃”。[GasBoundaryDrainCoordinator.cs:232–256](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryDrainCoordinator.cs#L232-L256) FinalDrain遇blocked返回失败，调用方未直接伪造Dispose成功。

验收：stager拒绝/抛错、copy失败、旧batch+晚到tail、empty shell、同Tick destroy、无下Tick shutdown；source在Accepted前保持完整，之后仅清watermark。FinalDrain固定最多4遍是重试guard，不应被描述成稳定化算法或吞掉剩余数据，因为耗尽返回显式失败。

### M11-03：完整Cue lifecycle尚未落地（P1目标差距）

现行AppendDefinitionFacts只对AppliedInstant/CreatedActive/MergedStack产Cue，不消费period outcome；payload只含kind/ApplicationId/DefinitionId/CausalityId。没有正式CueEventKind、ActiveCycleOrdinal、CueDefinitionOrdinal及完整ActiveEffect lifecycle key。

触发：产品需要Duration OnActive→WhileActive→Removed、inhibit/reactivate、period Executed幂等，或异步资源回调。当前只能证明受限marker，不足以证明四阶段配对和snapshot reconcile。

建议：按Spec06独立生成lifecycle/executed键、dedup键包含事件种类；stable态才出事件。周期递增ActiveCycleOrdinal的既有helper债务须与此一起修复。**未发现当前marker消费者使用该ordinal，因此不声称现行已发生实例泄漏。**

验收：重复delivery不重复播放；inhibit一次Removed；reactivate新cycle；stack不换cycle；period换execution ordinal不换active cycle；destroy后仍可配对关闭。

### M11-04：overflow策略缺少按消费目的分层（P1目标差距，不泛判fail-closed错误）

ring默认上限1024批，满时TryStage=false；WorldOwner将drain失败映射为SessionFault。强一致Core/Headless/Validation“不允许丢失”的模式允许显式失败，因此本路径本身不等于规范违规。

偏差在于当前通用ring/host暴露单一失败策略，无法表达Spec06的Presentation明确丢弃范围+snapshot reconcile、Replay stop-recording/incomplete。把它直接用作普通慢UI队列时，会把消费背压提升为gameplay域fault。

建议：区别必达staging与下游retention；把容量放版本化Boundary ScaleProfile，定义byte预算/first-lost range，而非只batch数。验收分别以验证必达、Presentation慢消费者、Replay断流运行；确认普通表现失败不改Core结果，验证失败准确封印且不伪成功。

### M11-05：队列吞吐需在Boundary域单独测量（P2待实测）

TryStage遍历已retained batches去重，TryDequeue移除List首元素；TryCreateBatch复制并排序，batch构造与ring接管再复制。触发为大量owner burst或慢consumer backlog。源码能证明复制与线性操作，不能证明实际超预算。

建议测ring高水位、batch count/bytes、copy/sort/dequeue cost、GC与重试次数；若成立再用head-index队列/稳定receipt索引，避免把优化换成不受限长期dedup缓存。

### M11-06：public协议与可执行支持面需分开展示

GasCommandPort有Cancel/RemoveEffect入口，但[SessionIngressGate.cs:443–463](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/SessionIngressGate.cs#L443-L463)在接受前typed拒绝。优点是没有“接受后静默忽略”；目标gap是完整Cancel/RemoveEffect生命周期仍待实现。文档/API health应明确返回UnsupportedByRuntimeV1Profile，不能仅列public方法数宣称完整GAS支持。

验收：unsupported命令不分配RequestSequence、不增加journal/payload预算；未来开放时必须增加对应Kernel consumer、终态和准确移除事实。

## 4. 未通过的验证门

0/1/N outer batch、InFlight纯drain retry不推进SimulationTick、跨World隔离、各scope唯一outbox、consumer组合不影响semantic hash、per-Battle双cut与SnapshotCut、teardown最终seal、retention/overflow三种模式均需真实测试。Spec06包含比当前Runnable更广的要求，本报告将其保留为目标门，不因白名单而写“不适用”。

