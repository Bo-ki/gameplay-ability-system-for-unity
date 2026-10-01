# M14 Definition Catalog 与 Proof 契约审查

## 结论

Catalog 的不可变 Blob 形状、二分 ID lookup、结构验证与内容重算已形成有效基础。当前最大边界是“通用 schema 能表达什么”与“Runnable 闭世界允许什么”并不相同；Proof 仍诚实保留 Red 和缺失 consumer，不能因类名、hash 或生成成功就宣称完整语义 admission 已闭合。优先补 consumer 接线与能力状态说明，不需要把 Catalog 推倒重写。

> 审查基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`；审查日期：2026-10-01。只读 GitHub 源码和仓库规范，未执行 Unity、Luban、dotnet、测试或性能测量。本文区分已证实源码行为、阶段性目标缺口和待动态验证事项；“符合”仅限列明的静态检查。

规范层级：[DOTS依据库README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)区分官方机制、EX-GAS项目规则和带基准的项目阈值；下文 BLOB/BUR/CONTENT 等为项目规则，不冒充Unity官方强制要求。已读相关 Baking/Blob、Burst、Prefab/Content、数据流 API 解读，以及目标规范08/14/15/19/20/25与[ADR-0001](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/adr/0001-codegen-single-install-root-and-install-envelope.md)。当前物理路线按 SourceGenerator 单 additional-file selector 裁决；旧 tarball、双active目录结论已被替代。

## 范围与实际阅读

本模块：Assets/GAS/Runtime/V1/Definition 及其 Proofs。完整读取并重点审查：GasDefinitionCatalogLookup、GasDefinitionCatalogSchema、GasDefinitionCatalogValidator、GasDefinitionCatalogContentHasher、GasRuntimeV1SupportProfile、GasCheckedProofMath、GasProofContracts、GasRuntimeV1CapacityProofContracts、GasRuntimeV1ProofInventory。交叉抽样：Editor 的 Layout/Capacity builder、CatalogValidatorHardeningTests、RunnableSupportProfileTests、Layout/CapacityProofTests。

大型 validator/hasher 已取得全文，精读范围是入口顺序、header/identity、dense layout、索引与公开 lookup；没有对全部 evaluator/temporal组合做路径穷举。未独立审计所有 Runtime owner 对 Blob 的释放及每一写路径，生命周期交叉模块不能仅凭本报告判全绿。未执行现有测试。

## 逐规范检查矩阵

| 编号与规则 | 源码证据 | 判定 | 边界说明 |
|---|---|---|---|
| [BLOB-01 静态定义只读Blob](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Baking-BlobAsset/BLOB-01.md) | [GasDefinitionCatalogSchema.cs L648–672](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasDefinitionCatalogSchema.cs#L648-L672) | 符合 | root由值类型与BlobArray构成；不把运行时实例状态放入此root |
| [Baking/Blob官方API解读：内部引用访问](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Baking-BlobAsset/API%E4%B8%8EEX-GAS%E8%A7%A3%E8%AF%BB.md)；[08 核心契约13](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/08-Luban-SourceGenerator%E9%85%8D%E7%BD%AE%E7%94%9F%E6%88%90%E9%93%BE%E8%B7%AFSpec.md) | [GasDefinitionCatalogLookup.cs L128–145](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasDefinitionCatalogLookup.cs#L128-L145) | 符合 | definition按ref readonly返回；不是含内部offset对象的值复制 |
| [08 核心契约11 lookup复杂度](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/08-Luban-SourceGenerator%E9%85%8D%E7%BD%AE%E7%94%9F%E6%88%90%E9%93%BE%E8%B7%AFSpec.md) | [GasDefinitionCatalogLookup.cs L11–125](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasDefinitionCatalogLookup.cs#L11-L125) | 符合 | Ability/GE/Attribute/Tag均二分查找，未引入managed registry |
| [25 §6.1 dense布局与引用校验](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [GasDefinitionCatalogValidator.cs L130–230](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasDefinitionCatalogValidator.cs#L130-L230) | 受限符合 | 结构与范围检查可见；完整组合仍需现有测试与目标构建 |
| [25 §6.3 proof逐字段消费](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [GasRuntimeV1ProofInventory.cs L39–148](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/Proofs/GasRuntimeV1ProofInventory.cs#L39-L148) | 目标缺口 | Missing/DeclaredOnly/PreallocatedOnly/EquivalentCheck不能等同proof真实消费 |
| [BUR-01 runtime热路径](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-01.md) | [GasRuntimeV1CapacityProofContracts.cs L194–238](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/Proofs/GasRuntimeV1CapacityProofContracts.cs#L194-L238) | N-A / 待验证 | managed proof报告对象不因位于Runtime文件夹就必须Burst；实际热路径入口AOT另验 |
| [08 四hash/install identity](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/08-Luban-SourceGenerator%E9%85%8D%E7%BD%AE%E7%94%9F%E6%88%90%E9%93%BE%E8%B7%AFSpec.md) | [GasDefinitionCatalogSchema.cs L651–657](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasDefinitionCatalogSchema.cs#L651-L657)、[GasDefinitionCatalogContentHasher.cs L7–63](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasDefinitionCatalogContentHasher.cs#L7-L63) | 受限符合 | 本地ulong/FNV内容封印不是descriptor四元SHA-256安装身份，不应互换 |

## 实质检查

### M14-01 只读lookup与dense索引是应保留的正面设计

严重度：正面检查。稳定ID先二分查找，再以已验证index取得ref readonly definition。这里没有每definition Entity query、托管Dictionary或运行时JSON查找，符合BLOB-01、Blob官方API解读与08的消费边界。入口返回-1而非默默选第一条，未知ID可由调用者显式拒绝。

验收：保留已排序/未排序、重复ID、缺失ID与边界index测试；对新BlobArray字段继续使用ref访问。实际Burst兼容性只能由使用它的Job/AOT验证，静态方法没有Burst属性本身不是违规。

### M14-02 Catalog验证不是只信header自报

严重度：正面检查。[GasDefinitionCatalogValidator.cs L130–202](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasDefinitionCatalogValidator.cs#L130-L202)先检查schema、layout、indices、Ability和GE，再重算内容与子layout hash并同时对照候选值和外部expectation。能避免改payload后只保留旧header而“自证合法”。

现有测试：[RuntimeV1CatalogValidatorHardeningTests.cs L45–171](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/_Test/RuntimeV1/EditMode/RuntimeV1CatalogValidatorHardeningTests.cs#L45-L171)覆盖非有限attribute、缺自身/重复/未排序ancestor、闭包与环、modifier子range越出definition等。这里只确认测试存在及断言内容，未确认本次运行结果。

验收：新增schema字段时同时补validator和content-hash变更测试，防止“结构承载已加但身份未覆盖”。

### M14-03 Runnable profile仍是精确样例闭世界

严重度：P1迭代限制，属于明确设计状态而非意外逻辑bug。[GasRuntimeV1SupportProfile.cs L50–100](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L50-L100)要求恰好3属性、4 Ability、4 GE、0 Tags及固定descriptor数量。通用schema可容纳更广能力，并不代表当前安装profile已支持这些能力。

触发：新增第五个Ability或Tag，即使通用CatalogValidator结构上合法，仍可被profile拒绝。影响是普通内容扩展必须触及profile/验收样例边界。原始数据数值冻结另见M17，不在这里重复当两项故障。

最小改善：将profile名称/版本和拒绝原因暴露给authoring；为每一新增能力组合先新增受支持向量和负向向量，再扩展profile，不删除fail-closed防线。

验收：结构合法但profile不支持的样例返回稳定规则和DefinitionId；支持状态在UI、raw gate、Blob gate一致。

### M14-04 CapacityProof已有完整性契约，但真实消费者尚未闭合

严重度：P1目标缺口。inventory明确把TargetOverlay、TargetPublishDelta、Stabilization、capture/cleanup等标为Missing；部分物理容量仅PreallocatedOnly，部分只有等价运行时检查。[GasRuntimeV1CapacityProofContracts.cs L213–238](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/Proofs/GasRuntimeV1CapacityProofContracts.cs#L213-L238)要求CoverageGaps=None且没有failure才Succeeded。

这不是“有proof对象但偷偷放行”：[GasRuntimeV1CapacityProofTests.cs L15–57](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/_Test/RuntimeV1/EditMode/Proofs/CodeGen/Capacity/GasRuntimeV1CapacityProofTests.cs#L15-L57)专门断言当前Runtime消费者不完整时稳定Red。风险在未来把验证hash通过、预分配足够或运行未溢出误当成全语义通过。

最小改善：按consumer dimension分批接到authority mutation前的admission；每接一项附“读取proof字段→拒绝或准入→fault时durable state零写”的测试，不人工把status改成Green。

验收：每个标Green字段有真实第一消费owner和调用点；少任意必需bound即失败，成功admission后不再靠扩容/丢弃掩盖不足。

### M14-05 Proof算术和大Tag布局避免静默截断

严重度：正面检查。[GasCheckedProofMath.cs L13–77](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/Proofs/GasCheckedProofMath.cs#L13-L77)对负数、long溢出、int收窄、word count都显式返回失败；不是溢出后归零。[GasRuntimeV1LayoutProofTests.cs L281–297](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/_Test/RuntimeV1/EditMode/Proofs/CodeGen/Layout/GasRuntimeV1LayoutProofTests.cs#L281-L297)断言257 Tags映射5个64-bit words，同时仍因整体coverage缺口保持Red。

验收：继续保留边界值、0、最大值和非法值测试；不能把“257可布局”改写成“当前Runnable已支持257 Tags”，后者仍受M14-03约束。

### M14-06 Runtime内容封印与安装身份要保持不同用途

严重度：P2误用风险，未发现本范围内把两者直接等同的确定缺陷。Catalog header是ulong/FNV；descriptor/selector是SHA-256四元identity与附加路由证据。前者用于Runtime可重算的payload契约，不能当cryptographic install admission替代品。

验收：安装边界同时验证正确的envelope/descriptor identity；文档和API命名区分RuntimeContentFingerprint与InstallContentHash；本报告不建议在tick内加入SHA或managed证明对象。

## 建议顺序

先完成proof消费者地图与阶段状态对外呈现；保持现有Blob/lookup/validator结构。后续扩展profile以语义向量为单位推进，且单独执行目标Player/AOT验证。生命周期与多World释放由Catalog owner模块补交证据，本模块不作无依据的性能或泄漏结论。
