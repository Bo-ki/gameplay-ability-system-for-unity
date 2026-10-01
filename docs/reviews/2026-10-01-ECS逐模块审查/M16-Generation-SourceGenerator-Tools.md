# M16 Generation SourceGenerator 与驱动工具审查

## 结论

当前production物理链已是单self-contained additional-file selector与固定analyzer/scaffold，不能继续按旧tarball或两个active目录评估。精确bytes、strict reader、selector事务值得保留。仍须优先处理的是Unity/预编译CLI使用旧row程序集快照的single-run风险，以及“封存compile plan”和“该candidate已真实编译/通过业务门”的状态区分。

> 审查基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`；审查日期：2026-10-01。只读 GitHub 源码和仓库规范，未执行 Unity、Luban、dotnet、测试或性能测量。本文区分已证实源码行为、阶段性目标缺口和待动态验证事项；“符合”仅限列明的静态检查。

规范层级：[DOTS依据库README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)区分官方机制、EX-GAS项目规则和带基准的项目阈值；下文 BLOB/BUR/CONTENT 等为项目规则，不冒充Unity官方强制要求。已读相关 Baking/Blob、Burst、Prefab/Content、数据流 API 解读，以及目标规范08/14/15/19/20/25与[ADR-0001](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/adr/0001-codegen-single-install-root-and-install-envelope.md)。当前物理路线按 SourceGenerator 单 additional-file selector 裁决；旧 tarball、双active目录结论已被替代。

## 范围与阅读清单

负责 GasCodeGenPipeline、GenerationStore、CandidateCompileGate、PackageDescriptor、InstallEnvelope、GasCodeGenSemanticIdentity、GasCodeGenContext、GasRowScanner、RowMetadataFactory、LubanNormalizedRowBootstrap；Tools/GasCodeGenCli、Tools/GasCodeGenSourceGenerator、Tools/CodeGen。已读上述文件与SourceGenerator/GasSourceBundleValidator、CLI Program/csproj、CodeGen README及批处理驱动。交叉已读ADR-0001、08/14/15/25、phase入口与AutoChess model。

Store/descriptor大型文件精读selector commit、route捕获、envelope字段与eligibility；全部故障恢复分支、D1BSelfTest与独立E1矩阵没有逐个动态执行或穷举。未运行工具，未验证本次平台文件系统原子性、hardlink、断电语义或analyzer实际加载。提交的是文档，不是生产迁移操作。

## 规范矩阵

| 编号/规范 | 源码 | 判定 | 含义 |
|---|---|---|---|
| [ADR-0001 当前D1](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/adr/0001-codegen-single-install-root-and-install-envelope.md)；[08 selector线性化点](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/08-Luban-SourceGenerator%E9%85%8D%E7%BD%AE%E7%94%9F%E6%88%90%E9%93%BE%E8%B7%AFSpec.md) | [GasCodeGenCandidateCompileGate.cs L16–44](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateCompileGate.cs#L16-L44)；[GasCodeGenGenerationStore.cs L596–652](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenGenerationStore.cs#L596-L652) | 受限符合 | 单selector、固定三assembly；进程/文件API保证不能扩大为断电保证 |
| [14 artifact责任](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/14-DefinitionCodeGen%E7%9B%AE%E6%A0%87%E9%93%BE%E8%B7%AFSpec.md)；[15 不生成lifecycle](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/15-SourceGenerator%E8%81%8C%E8%B4%A3%E8%BE%B9%E7%95%8CSpec.md) | [GasCodeGenSourceGenerator.cs L39–95](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/GasCodeGenSourceGenerator/GasCodeGenSourceGenerator.cs#L39-L95) | 符合（路由层） | 验证并按目标assembly发射已有bytes；不借此授予runtime owner |
| [25 §2.1 single-parse](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md)；[20 验收13旧factory负例](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/20-%E7%AD%96%E5%88%92%E9%85%8D%E7%BD%AE%E8%83%BD%E5%8A%9B%E4%BA%A4%E5%8F%89%E5%AE%A1%E6%9F%A5Spec.md) | [GasCodeGenContext.cs L304–320](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenContext.cs#L304-L320)；[RowMetadataFactory.cs L208–263](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/RowMetadataFactory.cs#L208-L263) | 目标缺口 / 确定风险路径 | 当前production身份仍来自已编译row factory；新JSON同时另行发射new rows |
| [08 candidate compile/AOT gate](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/08-Luban-SourceGenerator%E9%85%8D%E7%BD%AE%E7%94%9F%E6%88%90%E9%93%BE%E8%B7%AFSpec.md) | [GasCodeGenCandidateCompileGate.cs L76–112](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateCompileGate.cs#L76-L112) | 受限符合 | 只封存compile plan供独立E1执行；非此方法已执行编译 |
| [25 §8完整资格](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [GasCodeGenPackageDescriptor.cs L949–980](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenPackageDescriptor.cs#L949-L980) | 目标缺口 | Typed/Layout/Capacity hash空、FullSemanticEligibility=false明确保留 |
| [BUR-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-01.md)；[BAKE-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Baking-BlobAsset/BAKE-01.md) | [GasCodeGenSourceGenerator.cs L49–95](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/GasCodeGenSourceGenerator/GasCodeGenSourceGenerator.cs#L49-L95) | N-A | Roslyn/CLI/Editor控制面不要求Burst；非Baker也不套用Baker API限制 |

## 实质检查

### M16-01 单selector提交已经落入当前路线

严重度：正面/受限符合。CompileGate冻结唯一selector和analyzer路径，14项scaffold与三个目标assembly；Store提交前复核previous snapshot，Present用File.Replace，Missing用no-overwrite File.Move，随后重读target；不从audit记录猜active。[GasCodeGenGenerationStore.cs L596–652](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenGenerationStore.cs#L596-L652)。

这与旧tarball安装策略不同。candidate workspace仍有Core/AutoChess临时目录，不等于两个Unity-consumed active根；不要仅凭字段名误报倒退。

验收：保留first publish、replace、竞争、commit前后强杀、no-op和恢复的production-path矩阵；断电、目录fsync及不合作主体并发替换仍单独标待验证。不要从Flush(true)推导超出证据的保证。

### M16-02 SourceGenerator对缺失或篡改输入fail-closed

严重度：正面。[GasCodeGenSourceGenerator.cs L16–37](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/GasCodeGenSourceGenerator/GasCodeGenSourceGenerator.cs#L16-L37)定义缺失、重复、wire、analyzer、scaffold、legacy active等独立诊断；Execute完成selector decode、route验证、legacy source拒绝与snapshot复核后才AddValidatedSources。[GasCodeGenSourceGenerator.cs L49–85](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/GasCodeGenSourceGenerator/GasCodeGenSourceGenerator.cs#L49-L85)。

验收：缺selector、大小写碰撞、重复、raw bytes与Roslyn snapshot漂移、错误analyzer/scaffold均不发射成功marker；不能回退最近generation或缓存。严格bytes/inventory审查并不证明发射的所有业务代码语义已通过。

### M16-03 已编译row factory与新JSON能形成单次调用的双快照

严重度：P1，源码可确认的风险路径；具体受影响artifact和首次/二次hash差异尚待最小动态复现。

调用链：Pipeline先Context.Create取rows；Context扫描当前AppDomain并用RowMetadataFactory调用已编译零参数factory。随后GenerateCoreCandidate才从当前JSON生成新的LubanNormalizedRows.gen.cs。factory生成器写的是字面量数组，不是每次调用读JSON。证据：[GasCodeGenPipeline.cs L51–67](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs#L51-L67)、[GasCodeGenContext.cs L304–320](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenContext.cs#L304-L320)、[RowMetadataFactory.cs L208–263](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/RowMetadataFactory.cs#L208-L263)、[LubanNormalizedRowBootstrap.cs L216–258](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/LubanNormalizedRowBootstrap.cs#L216-L258)。

触发：Unity保持旧normalized row程序集时修改Luban数据，或直接执行以前编译的CLI exe去处理新JSON。semantic identity使用context.Rows，而当前candidate的新row源码来自新JSON；EnsureInputsUnchanged只能检测生成期间输入变化，不能让旧factory变成新parse。[GasCodeGenSemanticIdentity.cs L11–44](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenSemanticIdentity.cs#L11-L44)明确标记其算法是过渡期TypedRowSnapshotSemanticIdentity-v1。

重要限定：CLI csproj有BeforeBuild bootstrap，重新build会先生成normalized rows，降低常规dotnet run路径风险；不能把预编译exe/Unity路径问题扩大成“所有CLI都必然陈旧”。[GasCodeGenCli.csproj L1–39](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/GasCodeGenCli/GasCodeGenCli.csproj#L1-L39)。

最小改善：将本次JSON解析出的typed source model直接传入metadata/identity构造，normalized C#仅作派生产物；过渡期至少比较factory快照身份与当前parse，失配停在未发布candidate并给稳定RuleId。不要要求用户“再生成一次”当正确性方案。

验收：故意保留旧factory、改一个合法输入后，首次生成与编译后第二次生成得到相同语义/工件身份，或第一次明确拒绝且active bytes不变；同时验证fresh CLI build与prebuilt CLI两种入口。

### M16-04 CompileGate当前是编译计划封存，不是编译执行

严重度：P1目标资格缺口；不把D1职责分工误称为缺失异常处理。[GasCodeGenCandidateCompileGate.cs L76–112](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateCompileGate.cs#L76-L112)只核验route/candidate并写CandidateCompilePlan.bin；Pipeline之后直接stage/publish。[GasCodeGenPipeline.cs L82–103](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs#L82-L103)。Tools README明确production Unity/E1/Player由独立验证流程串行完成。

影响：调用名ValidateFullPackage或普通生成true不能解读为“此candidate已通过Unity/AOT/scenario”。如果业务发布UI未来将它直接当发布成功，就越过08/20目标门。

最小改善：结果类型拆分CandidateBuilt、SelectorCommitted、CompilePlanSealed、CompileEvidenceVerified、SemanticEligible；完整业务发布只接受与exact selector/route/plan hash绑定的验证回执。保留当前单selector事务，不重造目录发布器。

验收：给candidate注入编译错误时，完整业务发布不得产生成功admission结论；回执必须绑定本candidate，不能借旧E1结果。当前D1物理迁移和runnable能力状态须分别报告。

### M16-05 缺proof保持空与false是正确防误声明

严重度：正面加P1目标缺口。Descriptor明确三proof hash空且FullSemanticEligibility=false，不把生成report冒充proof。[GasCodeGenPackageDescriptor.cs L949–980](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenPackageDescriptor.cs#L949-L980)。这与M15 graph全Red、M14 consumer未闭合一致。

验收：D1完整性验证不应自行把这些字段改真；后续接C1/Z或相应install owner时，从真实同代artifact和consumer证据取得资格。不得用SourceArtifactInventoryHash替代覆盖全部受管artifact的ArtifactManifestHash。

### M16-06 驱动入口的输入契约要对使用者可见

严重度：P2操作/验证风险。CLI明确打印Input: Luban JSON tables并直接调用Pipeline；它不等同Unity入口的Bean→Luban→Pipeline。[Program.cs L23–54](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Tools/GasCodeGenCli/Program.cs#L23-L54)。用户修改Excel后直接执行sourcegen可能仍消费旧JSON；这属于入口差异，应显式标注而非默默推断已导表。

最小改善：统一驱动输出source workbook/JSON fingerprint、最近导出状态及“此命令是否执行Luban”；校验输入manifest一致性。不要让CLI为了便利在后台修改未经说明的Excel或静默选旧生成物。

验收：改Excel不导表时显示明确stale input；显式完整入口按同一snapshot执行；partial core/autochess仍只诊断不发布。

## 建议顺序

先补旧factory单遍负例与事务settings/source snapshot；其次将compile evidence和selector commit状态拆开；再接graph/proof/admission。保留现有exact-byte校验和selector故障矩阵，不能用“生成更快”名义放松输入、身份和唯一选择权。

