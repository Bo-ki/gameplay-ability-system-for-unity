# M15 CodeGen Semantics 与 Proof Builders 审查

## 结论

语义编译原型具备值得保留的确定性、不可变快照、固定RuleId、依赖环验证与资源预算防线；但它仍明确是全Red coverage，Layout/Capacity adapter和实际Runtime consumer尚未闭合。当前需要做单一graph到proof再到admission的最小垂直接线，不是继续凭测试数量或hash数量推断生产可发布。

> 审查基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`；审查日期：2026-10-01。只读 GitHub 源码和仓库规范，未执行 Unity、Luban、dotnet、测试或性能测量。本文区分已证实源码行为、阶段性目标缺口和待动态验证事项；“符合”仅限列明的静态检查。

规范层级：[DOTS依据库README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)区分官方机制、EX-GAS项目规则和带基准的项目阈值；下文 BLOB/BUR/CONTENT 等为项目规则，不冒充Unity官方强制要求。已读相关 Baking/Blob、Burst、Prefab/Content、数据流 API 解读，以及目标规范08/14/15/19/20/25与[ADR-0001](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/adr/0001-codegen-single-install-root-and-install-envelope.md)。当前物理路线按 SourceGenerator 单 additional-file selector 裁决；旧 tarball、双active目录结论已被替代。

## 范围与已读清单

负责 Assets/GAS/Editor/CodeGen/Semantics 与 Proofs。已读：CanonicalNormalizedSemanticGraph、CanonicalSemanticProjector、CanonicalSemanticCompiler、CanonicalSemanticGraphValidator、CanonicalSemanticIdentity、CanonicalSemanticDependencyCycleValidator、CanonicalSemanticResourceBudget、TypedContractBuilder、GasRuntimeV1LayoutProofBuilder、GasRuntimeV1CapacityProofBuilder。已读相关Tests~/Program.cs的测试登记、coverage/身份/预算/跨进程测试段，及EditMode Layout/Capacity测试。辅助读取Runtime proof contracts/inventory。

范围限制：上述大型文件均取得全文，精读聚焦公开入口、排序/冻结、coverage、proof连接及测试。TypedContractBinaryCodec与semantic codec的每一种wire分支、Layout/Capacity每一公式未逐行穷举；没有直接运行测试。Proof hasher和memory inventory的全量公式审查仍需后续专门检查，不以文件存在视为全覆盖。

## 逐规范检查矩阵

| 编号/规范 | 源码证据 | 判定 | 说明 |
|---|---|---|---|
| [25 §2.1 canonical graph](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [CanonicalSemanticProjector.cs L14–39](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/CanonicalSemanticProjector.cs#L14-L39)；[CanonicalNormalizedSemanticGraph.cs L748–790](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/CanonicalNormalizedSemanticGraph.cs#L748-L790) | 受限符合 | 原型从source document排序、编译、冻结；完整production same-parse adapter仍Red |
| [25 §2.2/§9 RuleId和provenance](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [Program.cs L90–119](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/Tests~/Program.cs#L90-L119) | 符合（所读向量） | 字段ordinal、RuleId、源row/path均有显式断言 |
| [25 §5 dependency/cleanup bound](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md)；[15 DirectEffectProgram边界](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/15-SourceGenerator%E8%81%8C%E8%B4%A3%E8%BE%B9%E7%95%8CSpec.md) | [CanonicalSemanticDependencyCycleValidator.cs L69–123](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/CanonicalSemanticDependencyCycleValidator.cs#L69-L123) | 受限符合 | proof相关环和无正上界拒绝；不等于所有runtime动态组合已闭合 |
| [25 §6 Layout/Capacity](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [GasRuntimeV1LayoutProofBuilder.cs L13–29](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Proofs/Layout/GasRuntimeV1LayoutProofBuilder.cs#L13-L29)；[GasRuntimeV1CapacityProofBuilder.cs L250–276](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Proofs/Capacity/GasRuntimeV1CapacityProofBuilder.cs#L250-L276) | 目标缺口 | canonical adapter、目标Player ABI、维度与consumer缺口显式保留 |
| [25 §8/§10 eligibility](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [CanonicalSemanticGraphValidator.cs L75–85](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/CanonicalSemanticGraphValidator.cs#L75-L85) | 受限符合 | 当前强制Red是防误装；不能据此宣称Green生产支持 |
| [BUR-01 热路径HPC#](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-01.md) | [TypedContractBuilder.cs L15–57](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Proofs/Typed/TypedContractBuilder.cs#L15-L57) | N-A | managed Editor编译/验证可用集合、异常、SHA；不要求全部Burst |
| [BUR-03 Player/AOT证据](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-03.md) | [GasRuntimeV1LayoutProofBuilder.cs L17–19](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Proofs/Layout/GasRuntimeV1LayoutProofBuilder.cs#L17-L19) | 待验证 | source中的TargetPlayerAbiIdentityMissing不能由当前Editor sizeof证据抹掉 |

## 实质检查

### M15-01 Canonical构建与TypedContract同源是正面基础

严重度：正面。Project先验证source预算和identity，按canonical key排序，经过compiler再构图；graph冻结规则和definition数组。TypedContractBuilder重新validate graph，从同一graph生成schema/content payload、matrix hash、typed hash；EnsureMatches同时核验外部expected hash和重建bytes。[TypedContractBuilder.cs L15–81](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Proofs/Typed/TypedContractBuilder.cs#L15-L81)

它没有在TypedContract创建过程中发布selector或修改Runtime状态，符合15的职责边界。最小改善是复用这一管线，不再为UI、CLI、测试新增另一套字段白名单。

验收：顺序扰动不改变set语义hash；有序数组变化必须改变identity；修改任意相关字段传播到typed bytes；外部expected hash不匹配必须拒绝。

### M15-02 资源预算在复制/遍历前检查，而非OOM后补救

严重度：正面。[CanonicalSemanticResourceBudget.cs L29–112](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/CanonicalSemanticResourceBudget.cs#L29-L112)定义collection/string/payload/wire限额和快照检查。Project入口先ValidateSource，SCC入口先ValidateArcBudget。对应测试登记包含SourceBudgetsFailBeforeCopyOrTraversal、GraphAndTypedAggregateBudgetsCoverNestedFields和CompilerProvenanceExpansionIsBounded。[Program.cs L51–72](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/Tests~/Program.cs#L51-L72)

验收：恶意count/getter、同count但内容变化、深嵌套、多字节UTF-8、超大provenance均产生稳定诊断，而非分配后失败。此处限额是项目策略，不称为Unity官方限制。

### M15-03 依赖验证采用迭代SCC并结合work上界

严重度：正面/受限符合。[CanonicalSemanticDependencyCycleValidator.cs L69–123](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/CanonicalSemanticDependencyCycleValidator.cs#L69-L123)使用迭代Kosaraju，proof-relevant边的MaxExpansion必须为正且不能落环；不能只把“DAG”当作完整容量证明。所读测试还登记长链深度安全与signed cycle策略。

验收：长链不会栈溢出，负边/SCC拒绝时返回原edge provenance；没有cleanup/work上界的环外边仍拒绝。动态runtime回边与live组合不因静态图通过而自动批准。

### M15-04 全Red不是完成状态，当前还没有生产single-graph闭环

严重度：P1目标缺口。[CanonicalNormalizedSemanticGraph.cs L647–688](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Semantics/CanonicalNormalizedSemanticGraph.cs#L647-L688)将schema、reference、program、same-parse与eligibility全部固定Red；validator拒绝非Red；TypedContract继续复制同一coverage。测试明确断言Red。它诚实暴露迁移状态，不是“已经实现但一个flag忘了打开”。

触发：希望把此原型作为完整业务发布资格，或仅因contract round-trip/hash一致而放行。影响：缺失production adapter、语义覆盖与consumer仍无法通过25最终验收。

最小改善：选一个现有可运行的direct-effect能力，把同一次Luban parse产生的graph作为该能力唯一输入，补adapter和consumer测试后只开放相应coverage；禁止直接放宽ValidateCoverage来制造Green。

验收：canonical graph与生产Catalog/manifest实际来自同一parse；连续生成、不同目录/culture的artifact一致；未知/未覆盖字段维持deny。

### M15-05 Layout/Capacity完整性与资格判断需要分别理解

严重度：P1目标缺口伴随正面防线。Layout builder明示CanonicalGraphAdapterMissing、TargetPlayerAbiIdentityMissing；Capacity builder明示六种coverage缺口。TryVerifyPayload证明当前payload的完整性，不证明Succeeded。[GasRuntimeV1CapacityProofBuilder.cs L282–334](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Proofs/Capacity/GasRuntimeV1CapacityProofBuilder.cs#L282-L334)；[GasRuntimeV1LayoutProofBuilder.cs L17–62](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Proofs/Layout/GasRuntimeV1LayoutProofBuilder.cs#L17-L62)

现有测试特意让合法编码的Red payload通过完整性验证，但Succeeded=false。[GasRuntimeV1CapacityProofTests.cs L19–50](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/_Test/RuntimeV1/EditMode/Proofs/CodeGen/Capacity/GasRuntimeV1CapacityProofTests.cs#L19-L50)。不要把这解读为门禁绕过。

验收：任何使用方分别记录IntegrityValid、Coverage、AdmissionEligibility；修改CoverageGaps、provenance或bound后完整性校验失败；真实admission逐项消费bound，而不是只查ProofHash非空。

### M15-06 测试向量很有价值，但运行证据需要独立登记

严重度：P2验证缺口。Semantics的Tests~是独立Program入口，包含跨进程/culture/workdir确定性向量；EditMode proof tests包含257 Tags、重复ID、范围溢出、预算等。它们覆盖算法性质，却不能替代目标Player ABI/AOT和production生成链的same-parse验收。

最小改善：报告同时列“源码中存在的测试”“最近一次执行结果”“目标构建验证”三列；将完整性测试、eligibility测试和runtime admission消费测试区分，不用总通过数掩盖缺口。

验收：每次release能定位准确commit、命令、结果文件及目标平台；本次审查不新增虚构PASS或性能基准。

## 推荐下一步

沿一条能力完成source document→canonical graph→typed contract→layout/capacity→实际consumer的垂直切片。保留现有Red状态、资源预算与mutation tests；先让一个consumer真正读proof，再扩大能力面。M14负责Runtime契约，M16负责selector物理发布，三者完成状态不能互相替代。

