# Runtime V1.1 D0-M2R SourceGenerator 路线接受与 D1 授权裁决

> 日期：2026-08-31  
> 裁决状态：`StableGraphSourceGeneratorAccepted`  
> 已接受生产路线：`StableGraphSourceGenerator`  
> D1：`Authorized`  
> 下一门：`D1-SourceGeneratorProductionMigration`

## 1. 结论

正式 D0-M2S RunId `D0M2S-20260830T180951Z-fe75df12b9a2` 已在同一 fresh run 中通过 full-fault、SG-specific X 和 SG-01～SG-08 全部固定 case。该技术门成立，因此 D0-M2R 正式接受 `StableGraphSourceGenerator`，冻结唯一 production selector 与 analyzer authority，并授权 D1 实施一次性 production migration；本文件后续同时闭合完整 artifact manifest、source subset 和首次发布协议。

当前统一状态更新为：

```text
AcceptedProductionRoute = StableGraphSourceGenerator
AcceptedSoleUnityConsumedSelector = Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile
AcceptedAnalyzerAuthority = Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll
D1Authorized = true
ProductionMigrationAuthorized = true
ProductionInstallAdmission = NotEvaluated
DeclaredFullSemanticEligibility = false
NextGate = D1-SourceGeneratorProductionMigration
```

本裁决 supersede [immutable tarball 路线否决与 SourceGenerator 恢复裁决](RuntimeV1.1-D0-M2R-ImmutableTarball路线否决与SourceGenerator恢复裁决.md)中的“当前 production route 为空”状态，但不修改其历史内容或 SHA。D0-M2T 的 tarball 否决、D0-M2S terminal 中冻结的 `AcceptedProductionRoute=None / D1Authorized=false` 也继续作为当时实验事实保留；实验 terminal 不具有自我授权权限。

D1 授权仅覆盖 SourceGenerator 物理消费协议、单 selector promotion、legacy active source 迁移与 route-specific E1。它不授权把 `ProductionInstallAdmission` 标为通过，不授权把 `DeclaredFullSemanticEligibility` 改为 `true`，也不扩展 Semantics、Proofs 或 Runtime install consumer。

## 2. 正式证据与准入映射

权威实验结果见 [D0-M2S 完整故障实验结果](RuntimeV1.1-D0-M2S-SourceGenerator完整故障实验结果.md)和 [正式 terminal](../../TestResults/GasCodeGen/N2-G0-D0-M2S/D0M2S-20260830T180951Z-fe75df12b9a2/D0M2S.terminal.json)。

| 准入门 | 正式事实 | 裁决 |
|---|---|---|
| fresh identity | 独立 RunId、独立 evidence root、冻结 contract/runner/generator/Unity SHA | 通过 |
| full-fault | `FullFaultStatus=Passed` | 通过 |
| SG-specific X | `SourceGeneratorSpecificXStatus=Passed` | 通过 |
| fixed cases | SG-01～SG-08 全部 `Passed / None` | 通过 |
| terminal | `Passed / None`；SHA-256 `0578fa2afee26b6c024f37607db4c6ff06302ff212dc72974d31ff6a97a7149b` | 通过 |
| evidence closure | declared/physical `74/74`、`Verified=true`、`Partial=false` | 通过 |
| production protection | protected aggregate 前后同为 `556feff02c068a7265e32ab632da9654a1fb193ae689dab2b894440449774583` | 通过 |
| tool identity | aggregate 前后同为 `91b5eeaf06a24bdd349ad14b0ed039a25bf29957c5875e2986dff80a25388a4f` | 通过 |
| cleanup | 夹具删除、无 owned process/reparse/hardlink/temp residue | 通过 |

实测 fixture selector `Assets/Selector/Generation.D0M2SCanaryGenerator.additionalfile` 只证明 Unity 机制和故障边界，不能复制为 production 文件名。production 路径按 Unity 的 `Filename.[Analyzer Name].additionalfile` 规则投影为 `Generation.GasCodeGenSourceGenerator.additionalfile`；`Generation` 前缀不含句点，`GasCodeGenSourceGenerator` 与 analyzer assembly 名大小写精确一致。

## 3. 冻结的 production 物理布局

```text
Assets/GAS/CodeGen/Selector/
  Generation.GasCodeGenSourceGenerator.additionalfile       # mutable Authority / SoleUnitySelector
  Generation.GasCodeGenSourceGenerator.additionalfile.meta  # immutable route scaffold

Assets/GAS/CodeGen/Analyzers/
  GasCodeGenSourceGenerator.dll                              # immutable Authority / Unity-consumed analyzer
  GasCodeGenSourceGenerator.dll.meta                         # immutable；RoslynAnalyzer label 与 plugin flags

Assets/GAS/Generated/CodeGen/Runtime/
  com.exhard.exgas.generated.runtime.asmdef                   # stable assembly scaffold
  GasCodeGenSourceGeneratorRequired.cs                       # stable hard-guard anchor

Assets/GAS/Generated/CodeGen/Editor/
  com.exhard.exgas.generated.editor.asmdef                    # stable assembly scaffold
  GasCodeGenSourceGeneratorRequired.cs                       # stable hard-guard anchor

Assets/AutoChessDemo/Generated/
  GasCodeGenSourceGeneratorRequired.cs                       # 加入既有 com.exhard.exgas.autochessdemo

ProjectSettings/GasCodeGen/
  ActiveGenerationRef.json                                   # DerivedAudit；零 Unity 选择权
  Generations/<generation-id>/**                             # sealed audit/rollback archive；零 Unity 选择权
  PublishIntent.json                                         # Derived transaction evidence；零 Unity 选择权
```

Runtime 与 Editor 的 `.asmdef` 不再按 generation 生成或替换；AutoChess 不新增第四个 `.asmdef` 或 `.asmref`。三个 `GasCodeGenSourceGeneratorRequired.cs` anchor 必须各自引用本程序集由 generator 生成的 generation marker；analyzer 缺失、损坏、未运行或未为该程序集发射 marker 时，普通 C# 编译直接失败。该 hard guard 用来补足 Unity 可能只把 analyzer load failure 报为 `CS8034` warning 的边界，D1 不得只依赖 warning 文本决定失败。

所有当前 generation-dependent `.gen.cs` 在 migration 后都不得继续作为 Unity active source。候选 workspace 可以暂存现有 emitter 产生的 `.gen.cs` bytes，但这些文件只能用于封包和 isolated validation，不能被复制回 `Assets` active generated roots。

`RouteScaffoldSha256` 的输入集合固定为以下 14 个 project-relative 文件；路径大小写精确、分隔符统一为 `/`，directory `.meta` 不参与：

```text
Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll.meta
Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile.meta
Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef
Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef.meta
Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs
Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs.meta
Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef
Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef.meta
Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs
Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs.meta
Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef
Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef.meta
Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs
Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs.meta
```

算法固定为 `SHA256(ASCII("EX-GAS-RouteScaffold-v1\0") || U32LE(14) || entries)`；entries 按 project-relative path ordinal 升序，每项为 `LP_UTF8(path) || U64LE(fileByteLength) || Raw32(SHA256(fileBytes))`。缺项、多项、路径大小写漂移、reparse/hardlink 或 bytes 漂移均拒绝；analyzer DLL bytes 不重复进入该集合，由独立 `AnalyzerSha256` 绑定。

## 4. Authority / Derived / Cache 角色

| 对象 | 角色 | Unity 消费 | generation 选择权 | 规则 |
|---|---|---:|---:|---|
| canonical `.additionalfile` raw bytes | Authority / mutable `SoleUnitySelector` | 是 | 1 | ordinary promotion 唯一线性化输入；必须 self-contained |
| `GasCodeGenSourceGenerator.dll` bytes | Authority / immutable toolchain | 是 | 0 | 路线版本内固定；不得随 ordinary generation promotion 改写 |
| analyzer `.meta`、stable asmdef、hard-guard anchors | ImmutableRouteScaffold | 是 | 0 | 纳入 scaffold/compile-plan 身份，不携带 generation 数据 |
| compiler-generated C# / assembly output | Derived compilation projection | 是 | 0 | 只由 selector + analyzer + scaffold 导出，不落盘为 active `.gen.cs` |
| `ActiveGenerationRef.json` | DerivedAudit | 否 | 0 | selector commit 后写；缺失或漂移不得反向选择、回滚或修复 selector |
| sealed generation archive / descriptor / envelope / compile plan | Derived evidence/archive | 否 | 0 | 用于审计和显式 rollback candidate，不参与 Unity 当前代选择 |
| `Library/ScriptAssemblies/**` | Derived | 是 | 0 | 可删重建，永不授权 generation |
| `Library/Bee/**`、RSP、driver/cache | Cache | 是 | 0 | Authority 缺失/损坏时不得 fallback |
| 第二同名 `.additionalfile`、AnalyzerConfig generation authority、legacy active `.gen.cs` | ForbiddenCompetingAuthority | 可能 | 禁止 | 发现即 fail closed，不按路径、时间或扫描顺序选一个 |

## 5. 唯一 selector wire contract

为最快复用当前稳定 emitter，D1 不重写业务语义生成算法。现有 pipeline 继续在 suite-owned candidate workspace 产生精确 C# artifact bytes，随后把它们封装为单个 self-contained selector；SourceGenerator 只执行严格验证、按 assembly 分发和 `AddSource`。

selector 文件固定为 UTF-8 no-BOM、LF，只有两行并以 LF 结尾：

```text
EX-GAS-SourceSelector-v1
<GasSourceBundle-v1 binary 的 RFC 4648 Base64；无空白、无换行折叠>
```

selector 文本必须逐 byte 匹配 `ASCII("EX-GAS-SourceSelector-v1\n") + canonicalBase64 + LF`。第二行只允许 RFC 4648 standard alphabet 与必需的 `=` padding，不允许空白；解码后重新编码必须与原行 ordinal 相等。binary 解码器必须使用严格 UTF-8，拒绝非法序列。binary 中所有无符号整数均为 little-endian 固定宽度，字符串均为 `uint32 byte length + UTF-8 bytes`，SHA-256 均为 32 个 raw bytes。

header 字段顺序、类型和常量冻结为：

| 序号 | 字段 | wire type | 约束 |
|---:|---|---|---|
| 1 | `BundleVersion` | `uint32` | 必须为 `1` |
| 2 | `EncodingDomain` | length-prefixed UTF-8 | 必须为 `EX-GAS-SourceBundle-v1` |
| 3 | `SourceInputHash` | 32 raw bytes | 完整 digest |
| 4 | `SchemaHash` | 32 raw bytes | 四元 identity 第 1 项 |
| 5 | `ContentHash` | 32 raw bytes | 四元 identity 第 2 项 |
| 6 | `LayoutHash` | 32 raw bytes | 四元 identity 第 3 项 |
| 7 | `ArtifactManifestHash` | 32 raw bytes | 完整 candidate artifact manifest 的 hash，不得缩窄为 C# inventory |
| 8 | `SourceArtifactInventoryHash` | 32 raw bytes | bundle 内 C# source subset inventory 的 hash |
| 9 | `RequiredArtifactSetId` | length-prefixed UTF-8 | 必须为 `EX-GAS-RuntimeV1-RequiredArtifacts-v2` |
| 10 | `RequiredArtifactSetContractHash` | 32 raw bytes | 必须为下文冻结 contract hash |
| 11 | `AnalyzerSha256` | 32 raw bytes | production analyzer DLL hash |
| 12 | `RouteScaffoldSha256` | 32 raw bytes | immutable route scaffold hash |
| 13 | `FullSemanticEligibility` | `uint8` | D1 必须为 `0`；其他值拒绝 |
| 14 | `SourceArtifactCount` | `uint32` | v2 必须为 `5`；decoder 绝对上限 `4096` |

随后紧接 `SourceArtifactCount` 个 entry，每个 entry 的字段顺序、类型和约束冻结为：

| 序号 | 字段 | wire type | 约束 |
|---:|---|---|---|
| 1 | `TargetAssembly` | length-prefixed UTF-8 | `1..128` bytes，且必须属于三目标 allowlist |
| 2 | `HintName` | length-prefixed UTF-8 | `1..255` bytes；匹配 `^[A-Za-z0-9][A-Za-z0-9_.-]{0,254}$`，不得含路径分隔符 |
| 3 | `ArtifactCategory` | `uint8` | `1=RuntimeGenerated`、`2=EditorBakingGenerated`、`3=AutoChessGenerated`，且必须与 target assembly 对应 |
| 4 | `SourceByteLength` | `uint32` | `1..16777216`；所有 source bytes 合计不得超过 `67108864` |
| 5 | `SourceSha256` | 32 raw bytes | 后续 source bytes 的 SHA-256 |
| 6 | `SourceBytes` | 精确 `SourceByteLength` bytes | 严格 UTF-8、no-BOM、LF-only、无 NUL 的完整 C# source |

entries 必须按 `TargetAssembly`、`HintName` 的 ordinal 升序排列，tuple 不得重复；最后一个 entry 后必须立即 EOF，不允许 padding 或 trailing bytes。`SourceArtifactInventoryHash` 的算法固定为：

```text
SHA256(
  ASCII("EX-GAS-SourceArtifactInventory-v1\0")
  || U32LE(SourceArtifactCount)
  || foreach entry in wire order:
       LP_UTF8(TargetAssembly)
       || LP_UTF8(HintName)
       || U8(ArtifactCategory)
       || U32LE(SourceByteLength)
       || Raw32(SourceSha256)
)
```

`ArtifactManifestHash` 保持现行四元 install identity 的完整 candidate-manifest 语义：它覆盖按 canonical path/kind/owner 排序的全部受管 candidate artifact byte hash，包括 C#、Blob/catalog、validation/report、proof 与其他 required artifact；不能用 `SourceArtifactInventoryHash` 替代。selector、candidate manifest 文件自身、descriptor、envelope、compile plan 和 generation record 不进入 `ArtifactManifestHash`。任何进入 `ArtifactManifestHash` 的 artifact bytes 都禁止反向嵌入 `ArtifactManifestHash` 或 `SelectorSha256`，以消除自哈希闭环；其中 C# `SourceBytes` 还禁止嵌入自身 `SourceSha256`、`SourceArtifactInventoryHash` 的 raw/hex 表示。

`ArtifactManifestHashDomain` 保持 `EX-GAS-ArtifactManifest-v3`，算法不改：items 按 `CanonicalPath` ordinal 升序；先写 domain，再对每项依次写 `CanonicalPath / Kind / Owner / invariant-decimal ByteLength / lowercase ContentSha256 / invariant-decimal MetaByteLength / lowercase-or-empty MetaContentSha256`。每个文本字段编码为 `invariant-decimal UTF-16 code-unit count + ':' + value + '|'`，最终对整个文本的 UTF-8 no-BOM bytes 取 SHA-256。D1 只升级 exact required set，不另造第二套 artifact manifest domain。

`EX-GAS-RuntimeV1-RequiredArtifacts-v2` 的 exact contract 只包含下列 6 个 manifest item，不得缺项、多项或改写职责；每项的 artifact bytes 与 managed `.meta` bytes/length/SHA 都进入完整 manifest。两个 stable asmdef 已迁入 `ImmutableRouteScaffold`，不再属于 generation required set。

| CanonicalPath | Kind | Owner | RuntimeVisible | Component | TargetAssembly | HintName | Category |
|---|---|---|---:|---|---|---|---:|
| `Assets/AutoChessDemo/Generated/AutoChessGeneratedConfig.gen.cs` | `RuntimeDemoConfig` | `AutoChessDemo` | 1 | `AutoChess` | `com.exhard.exgas.autochessdemo` | `AutoChessGeneratedConfig.gen.cs` | 3 |
| `Assets/GAS/Generated/CodeGen/Editor/LubanNormalizedRows.gen.cs` | `NormalizedDefinitionRow` | `DefinitionCodeGen` | 0 | `Core` | `com.exhard.exgas.generated.editor` | `LubanNormalizedRows.gen.cs` | 2 |
| `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md` | `ValidationArtifact` | `EditorCi` | 0 | `Core` | 空 | 空 | 0 |
| `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` | `RuntimePureGlue` | `DefinitionCodeGen` | 1 | `Core` | `com.exhard.exgas.generated.runtime` | `RuntimeAbilityActivation.gen.cs` | 1 |
| `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` | `RuntimePureGlue` | `DefinitionCodeGen` | 1 | `Core` | `com.exhard.exgas.generated.runtime` | `RuntimeActiveEffect.gen.cs` | 1 |
| `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` | `RuntimePureGlue` | `DefinitionCodeGen` | 1 | `Core` | `com.exhard.exgas.generated.runtime` | `RuntimeEffectInstant.gen.cs` | 1 |

contract hash 算法固定为 `SHA256(ASCII("EX-GAS-RequiredArtifactSetContract-v1\0") || U32LE(6) || entries)`；entries 按 `CanonicalPath` ordinal 升序，每项依次编码 `LP_UTF8(path) || LP_UTF8(kind) || LP_UTF8(owner) || U8(runtimeVisible) || LP_UTF8(component) || LP_UTF8(targetAssemblyOrEmpty) || LP_UTF8(hintNameOrEmpty) || U8(categoryOrZero) || U8(managedMetaRequired=1)`。冻结结果为 `63f69551708194359d76c3772fb68983a3632d8994d818d7ec3e8c931ac74565`。

descriptor 必须封存完整 candidate manifest bytes、`ArtifactManifestHash`、`RequiredArtifactSetContractHash`、`SourceArtifactInventoryHash` 与 source-subset mapping。mapping 是双向 bijection：上述 5 个 C# manifest item 必须各自映射到且只映射到一个 exact target/hint/category bundle entry，每个 bundle entry 也必须反向映射到其中一个 C# item；entry 的 source length/SHA 必须与 manifest item 的 artifact length/content SHA 相等，validation report 不进入 bundle。任一方向缺失/重复、length/hash 不等、required set 缩窄/扩张或出现未分类 artifact，candidate 均失败。selector 文件的外部 `SelectorSha256` 只在封包完成后计算，并与 analyzer/scaffold/compile-plan hash、required-contract/manifest/inventory hash 一起写入 descriptor、generation record、intent 与 audit ref；codec/descriptor negative tests 必须覆盖 required-set 漂移、双向漏映射与上述 source 自哈希禁令。

D1 直接把 `GasPackageDescriptor.json` 升级为 descriptor v3，把 binary install envelope 升级为 `EX-GAS-InstallEnvelope-v2 / EnvelopeVersion=2`；旧 descriptor v2、required-set v1 与 envelope v1 一律拒绝，不保留双写/兼容分支。envelope v2 的字段顺序以 ADR-0001 为准；D1 仍不修改 Runtime admission consumer，且 `FullSemanticEligibility=0`。

Generator 仅对以下 assembly name 发射 entry：

```text
com.exhard.exgas.generated.runtime
com.exhard.exgas.generated.editor
com.exhard.exgas.autochessdemo
```

generator-owned hint `GasCodeGenSourceGenerator.Marker.g.cs` 为保留名，bundle entry 使用即拒绝。generator 必须在每个目标 compilation 额外发射 `GAS.Generated.CodeGen.GasCodeGenSourceGeneratorMarker`（`internal static class`），至少包含 `TargetAssembly`、`SelectorSha256`、`ArtifactManifestHash`、`SourceArtifactInventoryHash` 四个 `internal const string`；marker 属于 Derived compilation projection，不进入 required set、`ArtifactManifestHash` 或 `SourceArtifactInventoryHash`。三个 stable anchor 均定义本程序集内的 `GAS.Generated.CodeGen.GasCodeGenSourceGeneratorRequired`，并以常量引用 `GasCodeGenSourceGeneratorMarker.SelectorSha256`；缺 marker 必须产生普通 C# symbol error。生成类与 anchor 都必须满足项目 XML 类职责注释规则。

非目标程序集 no-op；目标程序集必须看到且只看到精确 production selector，并要求 raw bytes 与 `AdditionalText.GetText().ToString()` 是同一 canonical 文本。generator 必须从 `AdditionalText.Path` 的冻结 selector suffix 反算工程根，在任何 `AddSource` 前读取并重算 analyzer DLL、analyzer `.meta`、selector `.meta`、stable asmdef 与 required anchors 的冻结 `AnalyzerSha256 / RouteScaffoldSha256`；路径、文件集合或 bytes 不匹配即报 error diagnostic。任一目标程序集缺 selector、出现重复 selector、bundle 损坏、analyzer/scaffold 身份不匹配或缺本程序集 marker，均 fail closed；analyzer 完全未加载时由 stable hard-guard anchor 产生普通 C# 编译错误。

## 6. Candidate、promotion 与 recovery

### Candidate gate

1. 对 Luban/raw/settings 输入只读取一次 byte snapshot，运行现有 semantic/layout/artifact 前门与 emitter。
2. 在 candidate workspace 生成完整受管 artifact 集与全部 C# bytes，先关闭完整 candidate manifest，再形成 source-subset mapping 与 `SourceArtifactInventoryHash`，最后封装唯一 selector；之后任何 artifact、manifest、source、selector、analyzer 或 scaffold byte 漂移都使 candidate 失效。
3. 在独立临时 Unity project 中使用精确 production 相对路径、同一 analyzer/meta、stable asmdef/anchors 和自己的 `Library/Bee` 执行真实编译。
4. 从 `CompilationPipeline` 与 Bee/RSP 证明三目标程序集各自只含同一 selector path/hash、同一 analyzer hash和预期 source marker；禁止 active checkout Bee/RSP 获得 candidate 资格。
5. candidate gate 必须显式覆盖 selector missing/corrupt、analyzer missing/corrupt、duplicate selector、legacy active `.gen.cs` 和 stale A cache/Bee/RSP；负例必须在 Play/Build/Runtime admission 前失败。

### Promotion

1. fixed path `ProjectSettings/GasCodeGen/PublishIntent.json` 同时是零 Unity 选择权的 durable intent 与单 active mutation claim。每次 publish 必须先读取 canonical selector 的完整 snapshot，形成 `PreviousSelectorState = Missing | Present`（仅 `Present` 记录 previous 完整 bytes/SHA），再把该 previous snapshot 与 `CommitAttemptState=NotStarted` 一起写入以 `FileMode.CreateNew` no-overwrite 创建并 flush 的唯一 intent；已有 intent 时只能恢复或 fail closed，不得以时间/lease 过期夺取。intent 同时绑定 `ExclusiveClaimId`、owner sentinel、target selector 完整 bytes/SHA、generation identity、`DescriptorSha256`、`InstallEnvelopeSha256` 与 required-contract/manifest/inventory/analyzer/scaffold/compile-plan SHA。取得 claim 后必须重读 selector 并与 intent 中的 previous snapshot 做 CAS 前置复核；不相等时将该已完整绑定 previous 的 intent durable 记为 `Indeterminate` 并停止。不得先创建不含 previous snapshot 的 claim，再补写 previous。
2. intent 的 `CommitAttemptState` 固定为 `NotStarted | Armed | Committed | CompetitionFailed | Indeterminate | NoOp`。同 selector bytes 在独占 claim 内关闭为 `NoOp`，不产生新 `PromotionId`、不改 selector/audit。需要 mutation 时先 durable 写 `Armed` 与 `PromotionId`，再在 selector 同目录写唯一 temp 并 flush。
3. `PreviousSelectorState=Present` 时，提交前必须复核 canonical selector 仍等于 previous，再以 `File.Replace` 原子替换；`Missing` 时必须复核目标仍缺失，再以同目录 no-overwrite atomic move/create-new 提交。任一 API 竞争/失败必须先 durable 记为 `CompetitionFailed` 后停止；若结果无法 durable 分类则记为或按 `Indeterminate` 处理。失败 intent 即使后来观察到相同 target bytes 也不得冒领 `PromotionId`。
4. mutation API 成功后必须重读 target 完整 bytes/SHA，durable 写入绑定 `ExclusiveClaimId + PromotionId + target SHA` 的 `Committed` receipt，然后才允许写 DerivedAudit `ActiveGenerationRef`。audit 写失败保留 committed intent/receipt；不能撤回、替换或覆盖 selector。canonical selector 出现完整 target bytes 仍是唯一 generation 线性化点，receipt/intent 选择权为零。
5. analyzer DLL、`.meta`、asmdef、anchors 和任何目录都不参与 ordinary promotion；不得在 selector commit 前后顺带移动 active source root。rollback 是把精确历史 selector bytes 作为新 candidate 通过同一 gate 和新 PromotionId 再次提交；禁止 Runtime/Editor 自动 fallback。

### Recovery

- recovery 首先验证唯一 active intent 的 exact bytes、`ExclusiveClaimId`、owner sentinel，并重算 descriptor、envelope、compile plan、required-contract/manifest/inventory、selector/analyzer/scaffold 全部冻结 snapshot SHA；无法证明 snapshot 闭合或该 intent 独占 Store mutation 权时，selector 即使等于 target 也 fail closed。
- `CommitAttemptState=CompetitionFailed|Indeterminate`：无条件 fail closed；不得因 selector 后来等于相同 target 而改判成功。
- `CommitAttemptState=NotStarted|Armed` 且 `previous=Missing && selector missing`：视为未线性化，关闭为 aborted evidence；不自动前滚 target。
- `CommitAttemptState=NotStarted|Armed` 且 `previous=Present && selector完整 bytes/SHA 等于 previous`：视为未线性化，保留 previous generation，关闭为 aborted evidence；不自动前滚 target。
- 只有 `CommitAttemptState=Committed`、receipt 与 exclusive claim 全部有效且 selector 完整 bytes/SHA 等于 target，才视为已线性化，只补写/确认 DerivedAudit 后关闭 intent；该分支同时覆盖首次发布和已有 previous。
- `NotStarted|Armed` 下 selector 等于 target、`Committed` 下 selector 不等于 target、`previous=Present` 时 selector missing、任意 corrupt/unknown 或竞争创建结果：全部 fail closed 并保留证据；不得根据 audit、最近目录、时间戳、Bee/RSP 或 generation archive 自动猜测。
- `NoOp` 只允许 previous/target 完整 bytes 相等且 selector 再验证仍相等；关闭 intent 后不得新增 promotion/audit identity。
- generation archive 只允许显式 operator 选择后重新走 candidate/promotion；其存在不改变 Unity 当前 selector。

## 7. 一次性 migration

D1 migration 与 ordinary generation promotion 分开处理：

1. 关闭真实 Unity/Player/Bee，确认没有竞争编译进程。
2. 在 disposable project 先用 staged production 精确路径完成 candidate compile 与 route-specific E1，全部通过后才写 production Assets。事务矩阵必须覆盖：首次 `Missing` 成功/commit 前中断、`Present` replace 成功/commit 前中断、完整 NotStarted intent 已 CreateNew+flush 但尚未执行 claim 后 selector 重读时中断、claim 后 CAS 发现 previous 漂移、committed receipt 后 audit 失败、same-target `NoOp`、`Present` selector missing、corrupt/unknown、相同与不同 target 的 competing create、competition/indeterminate 后 target-equal 不得冒领，以及 repeated recovery；物理负例另覆盖 direct-Unity scaffold missing/byte drift，断言 invocation 非零、无 `AddSource`/promotion 且原 selector 不变。
3. 一次代码变更加入 analyzer/meta、canonical selector/meta、stable anchors 和 SourceGenerator 工具；同时删除或降权所有 generation-dependent active `.gen.cs`、双 active root rename、public path promotion 与旧 selector/fallback API。
4. Runtime/Editor asmdef 保持稳定；AutoChess 保持既有 assembly owner；不得用兼容字段、双生成器或 legacy `.gen.cs` 过渡两套 active 实现。
5. 启动主工程完成一次真实 import/compile，然后只执行受影响的 RuntimeV1Runnable EditMode、PlayMode 和 Development Player build/run。

migration 的代码提交/工作树切换由版本控制提供恢复，不宣称本地源码更新过程具有掉电原子性。D1 需要证明的是 migration 后 ordinary production generation 只通过单 selector 原子提交，不再存在双 active root 或第二 selector。

## 8. D1 精确授权边界

D1 可修改：

- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateWorkspace.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenCandidateCompileGate.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenGenerationStore.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenManifest.cs`
- `Assets/GAS/Editor/CodeGen/Core/GasCodeGenPackageDescriptor.cs`
- 新 `Assets/GAS/Editor/CodeGen/Core/GasCodeGenInstallEnvelope.cs` 与 `.meta`
- `Assets/GAS/Editor/CodeGen/GasCodeGenPipeline.cs`
- `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs`
- `Assets/GAS/Editor/CodeGen/Phases/AutoChessDemoCodeGenPhase.cs`
- 新 `Assets/GAS/CodeGen/Analyzers/**`、`Assets/GAS/CodeGen/Selector/**`
- Runtime/Editor stable asmdef/anchors 与 AutoChess hard-guard anchor
- 与 legacy active generated source 迁移直接相关的既有 generated roots
- `Tools/GasCodeGenSourceGenerator/**`
- `Tools/GasCodeGenCli/**`、`Tools/CodeGen/README.md`
- `Tools/Tests/GasCodeGen/D1/**`（route-specific E1、codec、精确 production selector/analyzer/scaffold 测试）
- `TestResults/GasCodeGen/D1/**`（正式 evidence/provenance 输出）

D1 不修改：

- `Assets/GAS/Editor/CodeGen/Semantics/**`
- `Assets/GAS/Editor/CodeGen/Proofs/**`
- `Assets/GAS/Runtime/V1/Install/**`
- Runtime admission consumer 和 production gameplay 接线
- `FullSemanticEligibility` 的计算或 true 值
- D0-M2F、D0-M2T、D0-M2S 历史 terminal、sidecar、raw evidence 与冻结 harness

## 9. 最小验收与停止条件

D1 只执行必要验证：

1. generator/tool 的 C# 9 编译与 selector codec 静态/negative tests；必须含 required-set contract drift、双向漏映射、6 个 required item 任一 managed `.meta` 缺失/byte-length/SHA 不一致和 source self-hash 负例；
2. 一次 disposable Unity production-path candidate compile + E1 fault slice；必须含完整 transaction matrix、scaffold missing/byte drift、competing create/unknown/repeated recovery；
3. 主工程 Unity compile；
4. 既有 `RuntimeV1Runnable` EditMode 11/11、PlayMode 5/5；
5. 一次 StandaloneWindows64 Development Player build/run，Ability、GE 9203、AutoChess 三向量仍通过；
6. selector/analyzer/scaffold/descriptor/compile-plan/evidence identity 闭包与零 Unity/Bee/temp residue。

不重跑 D0-M2S，不扩展全量 Tier-B、Release/IL2CPP、Profiler、Journaling 或完整 UE-GAS 语义测试。出现以下任一情况立即停止 D1 并回到本 ADR：production 路径无法形成三程序集共同 selector、generator load 失败仍能依赖 legacy source 编译、ordinary promotion 需要改写第二个 Unity-consumed generation 文件、audit/cache 可以改变选择结果，或主工程必须保留双 active 实现才能运行。

## 10. 确认

本文件完成 D0-M2R 所需的精确 production layout、selector wire、Authority/Derived/Cache、candidate、promotion、recovery、migration、D1 写集和最小验收冻结。ADR-0001 按本裁决再次确认后，`D1Authorized=true` 生效；后续无需追加新的 D0-M2S 前置运行。
