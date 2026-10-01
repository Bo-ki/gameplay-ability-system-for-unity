# M05：Install / Envelope Admission 审计

审计基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`（2026-10-01 静态审计）。只通过 GitHub 读取固定提交；没有运行 Unity、测试、Burst、Player 或 Profiler，没有修改产品代码。下文“符合”仅指已检查静态性质，不代表模块端到端验收。

规范层级：[DOTS 依据库 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)明确区分官方机制、EX-GAS 项目规则与实测阈值。本报告中的 SYS/NAT/BUF/STORE 及目标不变量均为项目规则；DynamicBuffer 失效、cleanup、生存期等官方机制依据各主题 API 解读，不把项目选型冒充 Unity 强制要求。[目标态 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/README.md)和[Spec17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md)用于理想目标；受限交付范围不删除目标差距。

## 结论

当前C0 consumer是一条有意封闭的negative path：完整匹配也返回AcceptPathUnavailable，CanInstall恒为false。四元identity、三类proof及purpose-domain/length-delimited SHA-256 binding的静态校验较完整；它尚不是完整生产安装准入，更不认证trusted expectation的来源。D1仅接受并迁移SourceGenerator物理消费路线，明确没有授权修改本consumer。因此“没有success”是目标缺口，不是D1回归。

## 范围与实际检查

- 全文检查：[Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionConsumer.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionConsumer.cs#L1-L634)
- 全文检查：[Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionContracts.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionContracts.cs#L1-L270)
- 全文检查：[Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeBindingCodec.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeBindingCodec.cs#L1-L278)
- 关联全文：Session/GasRuntimeWorldOwner.cs与Layout/GasSessionLayout.cs；可确认WorldOwner安装拓扑与C0 envelope consumer是不同职责
- 已读规范：DOTS总README、System/Store/Native主题门户与API解读（建立managed install边界）；90不变量GEN-01～08、Spec17 Definition/Session章节；Spec25的proof/install identity相关段落；D0-M2R接受裁决与D1最终验收的scope字段
- 未逐文件读取：C1投影/独立expectation生产路径、Editor InstallEnvelope、所有artifact bytes/hash、完整Proof实现及InstallEnvelopeAdmissionTests；没有复验生产二进制封包


规范正文定位：[25-配置语义编译契约与CapacityProof统一裁决Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md)。其中无独立规则编号的章节以Spec编号和节号作为审计标识，不另造官方规则。

## 规范逐项矩阵

| 规则 / 分类 | 判定 | 代码证据与理由 |
|---|---|---|
| [GEN-07](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L151-L151)（项目目标） | 符合（值比较范围） | [GasInstallEnvelopeAdmissionContracts.cs:61–81](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionContracts.cs#L61-L81)四元Schema/Content/Layout/ArtifactManifest hash互不替代；[GasInstallEnvelopeAdmissionConsumer.cs:242–290](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionConsumer.cs#L242-L290)每项独立missing/mismatch拒绝 |
| [GEN-08](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L152-L152) / Spec25 §6（项目目标） | 部分符合；proof真实性在上游 | [GasInstallEnvelopeAdmissionConsumer.cs:296–385](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionConsumer.cs#L296-L385)验证Typed/Layout/Capacity三个commitment的完整性、IsVerified、版本、输入identity、hash；这不是运行时重新证明生成期上界 |
| GAS.RUNTIME.INSTALL.EXPECTATION（代码诊断ID，非Unity规则） | 符合negative contract；独立信任源未验证 | [GasInstallEnvelopeAdmissionConsumer.cs:18–35](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionConsumer.cs#L18-L35)缺expectation先拒绝；[GasInstallEnvelopeAdmissionConsumer.cs:81–117](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionConsumer.cs#L81-L117)重算binding检查内部一致性，不认证来源 |
| GAS.RUNTIME.INSTALL.INTEGRITY / [GEN-07](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L151-L151) | 符合（编码及比较） | [GasInstallEnvelopeBindingCodec.cs:12–35](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeBindingCodec.cs#L12-L35)版本、purpose domain及字段ordinal；[GasInstallEnvelopeBindingCodec.cs:111–140](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeBindingCodec.cs#L111-L140)绑定所有identity/proof/eligibility，非法Unicode/crypto失败拒绝；[GasInstallEnvelopeBindingCodec.cs:208–259](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeBindingCodec.cs#L208-L259)严格UTF-8+长度+LE定界 |
| GAS.RUNTIME.INSTALL.ELIGIBILITY / GAS.RUNTIME.INSTALL.ACCEPT_UNAVAILABLE | 受限符合 / 完整安装目标差距 | [GasInstallEnvelopeAdmissionConsumer.cs:156–195](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionConsumer.cs#L156-L195)eligibility false拒绝、true也sealed；[GasInstallEnvelopeAdmissionContracts.cs:229–245](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionContracts.cs#L229-L245)所有结果CanInstall=false，default也fail-closed |
| [GEN-06](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L150-L150) / 最新D0-M2R scope（项目裁决） | 未制造runtime fallback；不等于promotion已由本模块证明 | consumer没有磁盘扫描、旧schema reader或fallback；[D0-M2R明确不扩Runtime consumer](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D0-M2R-SourceGenerator%E8%B7%AF%E7%BA%BF%E6%8E%A5%E5%8F%97%E4%B8%8ED1%E6%8E%88%E6%9D%83%E8%A3%81%E5%86%B3.md#L26-L28)解释C0与D1并存合法 |
| [GEN-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L146-L146) / [SYS-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/System-World-SystemGroup/SYS-01.md) | 符合（边界职责）；managed install本身不要求Burst | 本consumer无System/ECB/EntityManager/NativeContainer mutation；List/string/SHA256仅属于安装边界。不能因Runtime目录含managed API而直接判热路径Burst违规 |

> 上表GAS.RUNTIME.INSTALL.*的精确声明见[GasInstallEnvelopeAdmissionContracts.cs:45–55](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeAdmissionContracts.cs#L45-L55)；这些是实现诊断键，不冒充DOTS规则编号。SYS-01应引用[System-World-SystemGroup/SYS-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/System-World-SystemGroup/SYS-01.md)，与任何历史issue同名编号无关。

## 目标差距与风险

### M05-P1-01：完整production install admission尚未开放（已知目标差距）

触发：要求按GEN-07/08、Spec25把exact-install、proof consumer与gameplay authority创建连接成完整production准入。当前API没有success token，C1/Z独立expectation生产及安全接线不在本模块可验证范围。影响：不能宣称ProductionInstallAdmission=Passed或FullSemanticEligibility=true。这里不建议“把CanInstall改true”作为修复，那会绕过上游未完成门；应在独立授权迭代中补齐真实producer、proof与accept链。

### M05-P2-01：输入大小预算是明确的上游责任，尚未验证

codec使用List<byte>、UTF8.GetBytes和ToArray，未在本类限制字符串总字节数（[GasInstallEnvelopeBindingCodec.cs:108–131](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeBindingCodec.cs#L108-L131)、[GasInstallEnvelopeBindingCodec.cs:208–218](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Install/GasInstallEnvelopeBindingCodec.cs#L208-L218)），注释将解码前边界交给C1。触发：未来把candidate接到外部/大体积输入而C1没有落实D0 bounds。影响：无谓内存分配或进程级OOM；当前internal negative-only类不能单凭此推导现有远程攻击入口。后续验收应证明读取/解码前上限，而不是给OOM catch后继续安装。

### M05-P2-02：expectation类型分离不构成来源认证

Expectation与Candidate构造器允许装入调用者提供字符串，重算SHA256仅证明字段一致。若未来从candidate克隆expectation，两者自然可匹配；现有CanInstall=false仍封闭，所以本审计未发现成功安装绕过。触发/影响：未来开放accept前，必须证明expectation来自独立冻结Player build身份，不能用相同来源相等充当可信证据。

## 符合证据

- 候选binding是计算属性而非可独立注入的字符串，避免字段与binding分离
- binding包括eligibility和proof.IsVerified，不允许翻转eligibility后保持相同commitment
- hash小写hex规范、Ordinal比较与InvariantCulture版本诊断避免文化区改变判定
- 当前consumer不消费文件系统，也不执行Definition/runtime authority写；negative result保留expected/actual identity、字段、RuleId、provenance
- 三类proof哈希相等只证明commitment一致，不证明对应资源上界已经被Kernel消费；这一点仍由GEN-08/Spec25要求

## API / ECS 适用性说明

| 维度 | 本模块答案 |
|---|---|
| Tick | Session安装前验证，不推进SimulationTick、不应进入Kernel热路径 |
| owner | caller提供独立expectation和candidate；consumer只返回拒绝证据 |
| 数据/API | managed字符串/纯值projection/SHA256；不需要Query、IJobEntity或Lookup |
| allocator | managed临时分配，SHA256 using释放；无NativeContainer/WorldUpdateAllocator |
| 结构变化 | 无Entity/ECB API；authority接线尚未开放 |
| drain | 不拥有outbox/Drain；安装失败不能创建需清理的gameplayauthority |
| ScaleProfile | 仅比较CapacityProof承诺，不能代替实际profile资源消费者；大小上限须C1落实 |

## 历史状态与当前裁决

[D0-M2R 路线接受裁决](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D0-M2R-SourceGenerator%E8%B7%AF%E7%BA%BF%E6%8E%A5%E5%8F%97%E4%B8%8ED1%E6%8E%88%E6%9D%83%E8%A3%81%E5%86%B3.md#L11-L28)已明确取代“生产路线为空”的历史状态；[D1 最终验收结果](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D1-SourceGenerator%E7%94%9F%E4%BA%A7%E8%BF%81%E7%A7%BB%E4%B8%8E%E5%8F%AF%E8%BF%90%E8%A1%8C%E9%AA%8C%E6%94%B6%E7%BB%93%E6%9E%9C.md#L9-L21)记录 SourceGenerator 迁移及受限闭世界可运行基线通过，同时保留 ProductionInstallAdmission=NotEvaluated、FullSemanticEligibility=false。这里只引用仓库已有结果，没有重新复验其 raw evidence；不将 Pending 自动判为缺陷，也不将该 Passed 外推为全部目标不变量通过。

早期“SourceGenerator provisional / production route=None”不得用于否定当前D1。但D1裁决明确排除了Runtime Install目录与FullSemanticEligibility计算；二者互不矛盾。本报告用P1标记完整目标的交付阻塞，不标记当前D1受限验收失败。

## 后续验证（本次未执行）

1. 每个identity/proof/header字段的missing、错值、未知version、eligibility翻转、proof交换、非法Unicode、大小写hash负例，验证固定首因和CanInstall始终false
2. C1实际解码前长度/总量上限；极大字符串、trailing bytes、截断、重复字段必须在分配/authority创建前拒绝
3. 独立build expectation的provenance，candidate→expectation克隆反例，默认值和默认result fail-closed
4. 未来Z开放后exact-install四元identity+三类proof+consumer-map逐项闭包；任意失败无Session/ASC写入、无fallback、无旧generation替代
5. 安装managed allocation与Player平台crypto可用性单独验证；不能以Editor测试绿色替代Release/IL2CPP结果
