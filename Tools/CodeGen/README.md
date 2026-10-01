# GAS CodeGen Drivers

当前 production 协议是 `StableGraphSourceGenerator`：现有 emitter 只在隔离 workspace 生成六个 required artifact，再把五个 C# source 的 exact bytes 封装进唯一 selector。Unity 只消费：

```text
Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile
```

`ActiveGenerationRef.json`、generation archive、descriptor、envelope、compile plan、Bee/RSP 都是零选择权的审计或派生对象，不能恢复、替代或猜测 active generation。legacy 双 generated root、目录 rename、公开 path promotion 和 active RSP candidate token 已移除。

## 版本与身份

- descriptor：v3；旧 v2 直接拒绝。
- envelope：`EX-GAS-InstallEnvelope-v2 / EnvelopeVersion=2`；旧 v1 直接拒绝。
- required set：`EX-GAS-RuntimeV1-RequiredArtifacts-v2`。
- required contract SHA-256：`63f69551708194359d76c3772fb68983a3632d8994d818d7ec3e8c931ac74565`。
- `ArtifactManifestHash` 覆盖六个 artifact 及其 managed `.meta`；`SourceArtifactInventoryHash` 只覆盖五个 C# bundle entries，两者不可互换。
- 六个 artifact（含 validation report）都拒绝嵌入 `ArtifactManifestHash`/`SelectorSha256` 的 raw 或任意大小写 hex；五个 C# source 还拒绝自身 `SourceSha256` 与 inventory hash。
- `FullSemanticEligibility=false`；D1 不生成伪 proof，也不改变 Runtime admission。

## Selector transaction

fixed claim 位于 `ProjectSettings/GasCodeGen/PublishIntent.json`。普通发布顺序固定为：

1. 先读取 canonical selector 完整 previous snapshot。
2. 用 `FileMode.CreateNew` 一次性写入完整 `NotStarted` intent 并 `Flush(true)`。
3. claim 后重读 selector 做 previous CAS；漂移 durable 标记 `Indeterminate`。
4. mutation 先写 `Armed + PromotionId`；Present 使用 `File.Replace`，Missing 使用 no-overwrite `File.Move`。
5. selector target bytes 复核后写 `Committed` receipt，再写零选择权 audit。

状态仅允许 `NotStarted|Armed|Committed|CompetitionFailed|Indeterminate|NoOp`。`CompetitionFailed`、`Indeterminate`、以及 `NotStarted|Armed` 下 selector 已等于 target 都 fail closed，不能冒领 PromotionId。same-target `NoOp` 不创建 promotion/audit identity。

intent、generation record 与 audit 只接受 production writer 的 exact canonical JSON bytes：未知字段、字段重排、额外空白、非 canonical enum/数字表示都在 recovery/verify 前 fail closed，原始 evidence 不得被重写洗掉。

`Flush(true)` 只声明当前已验证的进程/文件 API 边界；不把它表述为机器断电、目录 fsync 或不合作主体并发写入保证。

## 命令

```bat
dotnet build Tools\GasCodeGenCli\GasCodeGenCli.csproj -c Release
dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- --projectRoot "%CD%" --mode sourcegen-all
dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- --projectRoot "%CD%" --mode generation-verify
dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- --projectRoot "%CD%" --mode generation-recover
dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- --mode d1b-contract-tests
```

`sourcegen-all` 要求 production analyzer 与 14 个 immutable scaffold 文件已存在且 identity 闭合；Windows route capture 还要求每项 link count 恰为一，非 Windows hardlink identity 仍记录为 P2。selector 只有通过完整 GasSourceBundle-v1 version/domain/required-set/source/inventory/hash/EOF strict reader 后才属于 `Present`；`Missing` 只表示 exact namespace path 确实不存在，目录或其他可探测非 regular 对象一律是 fail-closed unknown。它不会读取 legacy active `.gen.cs` 或 active Bee/RSP 取得资格。`generation-verify` 只验证 selector 与 DerivedAudit；`generation-recover` 只按 fixed intent 六态恢复，不按时间、audit、cache 或最近 generation 猜测。

E1 transaction adapter：

```bat
GasCodeGenCli.exe --mode d1b-export-selectors --projectRoot "<staged-fixture>" --selectorAOutput "<fresh-a>" --selectorBOutput "<fresh-b>"
dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- --mode d1b-self-test --projectRoot "<fresh-project>" --request "<request.json>" --output "<response.json>"
```

`d1b-export-selectors` 复用 staged fixture 已安装的 exact analyzer/scaffold，由 production package writer 生成完整 A/B wire，并在落盘前调用 D1-A production `DecodeSelector` 反向验证；它不接受仅有 magic/Base64 外壳的伪 selector。

request schema 固定为 `EX-GAS-D1-E1-TransactionRequest-v1`，exact 字段为：

```text
Schema / RunId / CaseId / InvocationId / Scenario / ProjectRoot
SelectorRelativePath / IntentRelativePath / AuditRelativePath
InitialSelector / Target
SelectorABytesBase64 / SelectorBBytesBase64 / CompetitionSelectorBytesBase64
FaultPoint / SeedState / CompetitionTarget / RecoveryCount
```

`CaseId` 只用于 identity 回绑；执行分支只消费 `Scenario/SeedState/CompetitionTarget`。adapter 复用 fixture 已安装的 analyzer 与 14 项 route scaffold，不覆盖其 bytes。response schema 固定为 `EX-GAS-D1-E1-TransactionResponse-v1`，除 identity、observed state、fault/recovery exit 外，必须保存 `IntentBeforeRecovery` 原始 length/SHA/Base64、每轮 `RecoverySnapshots` 的 selector/intent/audit/PromotionId typed snapshot，以及从 Store production durable writer 回执派生的 `ProductionClaimWrite`。负例在 durable response 写完后返回非零进程码；外层 E1 仍独立复核磁盘与进程结果，不信任 response 自报。

Unity production-path candidate compile、route-specific E1、主工程 compile 与 Player 验收由唯一 D1-J Unity lease 串行执行；不要在 A/B/C 并行链启动 Unity。
