# Runtime V1.1 D0-M2F Spec/ADR 规范冻结结果

> 日期：2026-08-30  
> 状态：`SpecAdrDeltaFrozen`  
> 人工门：`HumanConfirmed`  
> 下一门：fresh `D0-M2T` / `ImmutableTarballFullFaultExperiment`  
> D1：`NotAuthorized`

## 结论

D0-M2F Run5 选出的 tuple 已写入目标规范并形成单一事实：

```text
(ImmutableTarball, Packages/manifest.json, Authority/Derived/Cache table)
```

content-addressed immutable `.tgz` 是 Authority payload；`Packages/manifest.json` 是唯一 Unity-consumed selector；`packages-lock.json` 是 Derived；`ActiveGenerationRef` 是 Derived `AuditOnly`；PackageCache、解包目录与 Bee/RSP 是 Cache / compiled projection，均不得成为 fallback selector。

本次只冻结物理路线、角色和后续门。manifest/audit 的精确写序、production archive/layout、恢复状态机、缓存回收与迁移步骤仍由完整故障实验和唯一 D0-M2R owner 决定。

## 冻结输入 SHA-256

| 输入 | SHA-256 |
|---|---|
| `08-Luban-SourceGenerator配置生成链路Spec.md` | `184df26da7a9080dcafa9af3b50c42655e8a69f41115617337c09621b3bfc6b0` |
| `docs/adr/0001-codegen-single-install-root-and-install-envelope.md` | `38be527ddf587c66655685153ada138efb6a1e92d0815b02378b0bb40b373f47` |
| `20-策划配置能力交叉审查Spec.md` | `e3141bdfb299f742b523a5e01166a3326aa01fed7d337a1881cecf48a89e1012` |
| `91-术语表.md` | `8f6440d32dfe8a8a84c0f5bafe54af15c04b6ddf2ad382c69a76f7ed2860a7a8` |
| `docs/reviews/RuntimeV1.1-D0-M2F-选路裁决.md` | `a2d568f60a834ba8a18ed750948b10e1894d1c3e85a98dfd4744356105326629` |
| `Tools/Tests/GasCodeGen/README.md` | `e074d6d6290a244ade97547b8d0fa722d24bc6da953e46d6821c34e671e01586` |

fresh D0-M2T 的 J0 必须现场复算并精确匹配这些 raw SHA；任何漂移都应映射为输入身份失败，不得静默采用新规范。

## 验证与边界

- 三条独立终局复审均为 `P0=0 / P1=0 / P2=0`。
- 已清除目标规范中 `ActiveGenerationRef=唯一 selector` 的旧事实与 manifest→audit 预设写序。
- exact archive 的门序固定为 pre-seal validation → deterministic seal → exact tarball isolated Unity/compile/fault gate → manifest promotion eligibility。
- 当前 Assets generated roots 明确降为 legacy Development implementation，不具有 release selector 或目标输出 authority。
- Run5 机器字段 `HumanConfirmationRequired=true` 保持历史原值；外部人工门已满足。
- 当前保持 `D1Authorized=false`、`ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`。
- 本次未修改 production CodeGen、`Packages/**`、`ProjectSettings/GasCodeGen/**`、CLI、Runtime、Semantics、Proofs 或 install consumer，未启动 Unity。

下一任务不得复用 D0-M2F 前规划的旧 M2T harness、假设或证据；只允许新建 fresh immutable tarball 完整故障实验。
