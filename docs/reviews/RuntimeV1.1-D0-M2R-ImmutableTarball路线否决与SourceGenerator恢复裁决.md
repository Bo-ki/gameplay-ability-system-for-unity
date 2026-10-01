# Runtime V1.1 D0-M2R ImmutableTarball 路线否决与 SourceGenerator 恢复裁决

> 日期：2026-08-31  
> 裁决状态：`ImmutableTarballRouteRejected`  
> 已接受生产路线：`None`  
> 恢复候选：`StableGraphSourceGenerator / ProvisionalCandidate`  
> 下一门：一次 fresh `SourceGeneratorFullFaultExperiment` + `SourceGeneratorSpecificX`  
> D1：`NotAuthorized`

## 1. 结论

正式 D0-M2T 已给出可判定的技术否决，而不是 harness 不确定：`ImmutableTarball` 在 warm `PackageCache` 存在时，权威 `.tgz` 缺失后仍被 Unity 接受并成功编译三程序集同代 B。这违反“Cache / compiled projection 永不成为 fallback selector”的冻结硬门，因此整条 immutable tarball 生产路线被否决，不能通过预先清空 `Library/PackageCache` 重新包装成绿色。

D0-M2R 的唯一当前裁决为：

```text
RouteUnderTest = ImmutableTarball
AcceptedProductionRoute = None
RejectedBoundary = WarmPackageCacheFallback
SourceGeneratorRouteStatus = ProvisionalCandidate
AcceptedSoleUnityConsumedSelector = NotFrozen
D1Authorized = false
ProductionInstallAdmission = NotEvaluated
DeclaredFullSemanticEligibility = false
NextGate = FreshSourceGeneratorFullFaultExperiment + SourceGeneratorSpecificX
```

`SourceGenerator` 的恢复只表示它重新成为唯一可继续验证的 provisional candidate。D0-M2F Probe S 的 feasibility 绿色不足以接受生产路线，也没有冻结 production selector；在一次 fresh full-fault 与 SG-specific X 都通过前，不得写成 `AcceptedProductionRoute=StableGraphSourceGenerator`，也不得把 fixture 使用过的 additional-file 路径提前写成 sole Unity-consumed selector。

## 2. 正式证据与裁决映射

权威运行是 [`D0M2T-20260830T163229Z-3191c7b97729`](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729)。[正式 terminal](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/D0M2T.terminal.json) 的原始 SHA-256 为 `14c200f23a134767abd7e720b94bcdd3f4273f418de04c38cd56fc10dde6347e`，其 sidecar 精确绑定同一值；terminal 为 `Status=RouteRejected`、`Reason=MissingOrCorruptAuthorityAccepted`。完整 [TarballFault aggregate](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/TarballFault.json) SHA-256 为 `d099529e88f2715904637d6ccb831c3ae9beb90452665f57255497f0d428b71f`。

八个固定 case 中，TT-01、TT-02、TT-03、TT-05、TT-06、TT-07、TT-08 均为 `Passed`；唯一决定性失败是 TT-04 `MissingCorruptAuthority`：

| 观察 | 正式结果 | 裁决含义 |
|---|---|---|
| warm B 基线 | `WarmBaselineB=true` | 先真实建立可复用的 B PackageCache，不是冷启动假设 |
| 删除 B authority archive 后启动 Unity | `ExitCode=0`、`SuccessfulObservation=true`、三程序集均消费 B | 缺失权威 payload 时，warm cache 实际接管了消费路径 |
| missing 观测的实际 resolved path | `Library/PackageCache/com.exhard.exgas.d0m2f-tarball@...` | 失败边界精确归一化为 `WarmPackageCacheFallback` |
| 损坏 B authority archive | `ExitCode=1`、`SuccessfulObservation=false` | corrupt 分支拒绝不抵消 missing 分支已成立的 fallback 反例 |
| TT-04 汇总 | `CacheFallbackAccepted=true`、`AuthorityRestored=true` | 反例成立且实验完成恢复，不是清理残留造成的误判 |

[TT-04 故障记录](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-04/missing-corrupt-authority.json) SHA-256 为 `04073abf156bff51219104e72f9e3aa817622b0c98112eec5a78aa9243dbddfd`；[missing Unity 观测](../../TestResults/GasCodeGen/N2-G0-D0-M2T/D0M2T-20260830T163229Z-3191c7b97729/Raw/tt-04/missing-b-unity.json) SHA-256 为 `2d81414df126260bca2f6a77dd44830c2e6c663e6d187bd04cad07dfddd382e7`。terminal 的 33 个 raw evidence 叶完整闭合，`EvidenceClosure.Verified=true`、`Partial=false`。

terminal 中历史字段 `SelectedRoute=ImmutableTarball` 与 `SoleUnityConsumedSelector=Packages/manifest.json` 只描述本轮冻结输入和被测路线，不能在 `Status=RouteRejected` 后继续解释为生产接受结果。本裁决不改写历史 terminal，而是把其技术终态投影为 `AcceptedProductionRoute=None`、`AcceptedSoleUnityConsumedSelector=NotFrozen`。

## 3. 为什么清空 PackageCache 只能制造假绿

本次必须保留 warm cache 前提，因为生产故障恰好发生在“曾成功运行过，然后 authority 丢失或不可读”的状态。预先删除 `Library/PackageCache` 再重跑，只能证明 cold 环境在 archive 缺失时会失败，不能反证已经实际观察到的 warm fallback。

更具体地说：

1. 冻结角色要求 PackageCache、解包目录与 Bee/RSP 始终是零选择权的 Cache / compiled projection；它们是否存在不能改变 authority 缺失时的接受结论。
2. “每次启动前先清缓存”把正确性依赖转移到另一个可中断、可绕过的清理步骤，并没有证明 Unity 不会消费缓存；直接 Unity 启动、清理失败或清理前强杀仍会恢复同一反例。
3. 将 TT-04 改成只测冷缓存会删除已命中的生产状态空间，属于改变验收条件，不是修复路线。

因此后续不得以清空全局或 fixture PackageCache、改用全新机器、禁用 warm restart，或把 cache purge 写成测试前置条件来宣称 immutable tarball 通过。已有正向结果仍可作为机制研究资料，但不能再获得 production route admission。

## 4. 否决边界与未被否定的内容

本次否决的是“immutable tarball + manifest 作为可满足当前 fail-closed 合同的完整生产路线”，不是否定内容寻址 archive、原子文件替换或 UPM tarball 的一般工程价值。TT-01/02/03/05/06/07 的绿色仍分别证明 baseline、selector 强杀、resolve 恢复、manifest/lock drift、stale cache/Bee/RSP 对撞和边界清理的已测行为；但任一 hard reject case 成立即足以否决整条路线，七个绿色不能投票覆盖 TT-04。

`Packages/manifest.json` 也不再具有“已接受 production sole selector”的身份。它仅是已否决 tarball 路线中的被测 selector；SourceGenerator 路线的真实 selector、跨 Runtime/Editor/AutoChess 三程序集的共同输入身份及其 Unity-consumed 边界，必须由下一门重新测量和冻结。

## 5. SourceGenerator 恢复后的最短执行门

为避免再展开一轮长期 feasibility，下一轮只执行一个 fresh RunId、一个新 evidence root 和一套冻结工具身份，串行完成以下两门；D0-M2F Probe S 只能复用为 harness/fixture 起点，不能复用为本轮通过证据。

### 5.1 Fresh SourceGenerator full-fault

在隔离 Unity project 与独立 Bee 图中，最少覆盖 cold A、A→B、生成/编译强杀与 restart、输入缺失/损坏 fail-closed、generator/semantic input 身份漂移、stale generated output/Bee/RSP/cache、三程序集同代、边界清理和 raw evidence closure。任何依赖旧 active `.gen.cs`、旧 Bee/RSP 或每程序集不同 generation token 才能通过的结果都直接 `RouteRejected`；timeout、证据缺失或 harness residue 只能 `Inconclusive`，不得降格为通过。

### 5.2 SourceGenerator-specific X

使用互异 A/B token 主动制造权威候选输入与所有潜在旁路的冲突：

- canonical candidate input 为 B 时，audit/cache/stale generator output/Bee/RSP 中保留 A，验证这些对象没有选择权且 Runtime、Editor、AutoChess 只消费同一 B；
- 若 legacy active generated source 或第二 additional-file/AnalyzerConfig 注册会形成另一 active authority，必须 fail closed，禁止用优先级或扫描顺序静默选一个；
- 从 Unity 实际 CompilationPipeline / Roslyn additional-file 图反算三程序集共同输入身份；任一程序集缺失该输入、读取不同 selector 或仍消费 A，立即否决。

只有 full-fault 与 SG-specific X 在同一 fresh run 中均技术通过，才允许后续最小规范冻结把 `AcceptedProductionRoute` 和 `AcceptedSoleUnityConsumedSelector` 从 `None/NotFrozen` 改为实测值，并再次确认 ADR；完成该确认后才可重新评估 D1。不得在两门之间先迁移 production。

## 6. 对 Runtime V1 可运行基线的影响

本裁决不反向否定 [`RuntimeV1-Runnable-ClosedWorld`](RuntimeV1-第七轮收口执行结果.md)。二者的声明域互不重叠：Runtime V1 已交付的是 Development Player 受限闭世界功能基线，并从未声明 production install admission、release selector 或完整语义已经通过；其既有字段本来就是 `ProductionInstallAdmission=NotEvaluated`、`DeclaredFullSemanticEligibility=false`。

D0-M2T 全程运行于 suite-owned 隔离 fixture。正式 terminal 证明 production protected snapshot `Unchanged=true`，前后 aggregate 均为 `1899f47e07cc4e9bd0415964aa00507108bb3979ecd4939b8690ca1d091d999f`；工具树也为 `Unchanged=true`。本轮没有修改或重新生成 Runtime V1 production source、现有 generated output、Player 构建或其权威证据。因此结果是：V1 继续可运行，V1.1 production 安装路线仍未获准。

## 7. 明确未授权事项

在 fresh SourceGenerator full-fault + SG-specific X 通过并完成后续 ADR 确认前，以下事项全部未授权：

- 不得领取或实施 D1，不得修改 production CodeGen promotion/install 协议、active 路径或唯一 mutation owner。
- 不得把 SourceGenerator feasibility、旧 Probe S 或本恢复裁决表述为 production route admission。
- 不得冻结某个 additional-file、AnalyzerConfig、descriptor/blob 路径为 sole selector；当前值必须保持 `NotFrozen`。
- 不得修改 `Packages/**`、`ProjectSettings/GasCodeGen/**`、现有 production generated roots、CLI production entry、Semantics、Proofs 或 Runtime install consumer 来预埋 SG 路线。
- 不得以 PackageCache/Bee/RSP 清理、兼容双实现、stable alias、materializer 或旧 `.gen.cs` fallback 绕过下一门。
- 不得设置 `ProductionInstallAdmission=Passed` 或 `DeclaredFullSemanticEligibility=true`，不得伪造未闭合的 proof/identity。
- 不得为本路线恢复重复运行 Runtime V1 EditMode、PlayMode 或 Player；除非后续真实 production diff 进入其声明域，否则既有可运行证据保持原样。
- 不得删除、覆盖或回写 D0-M2F/D0-M2T 历史 terminal、失败 raw evidence 与 sidecar。

本轮停在明确而可执行的状态：immutable tarball 已技术否决，production route 为空，SourceGenerator 仅恢复为唯一 provisional candidate；下一动作只有一次 fresh full-fault + SG-specific X。
