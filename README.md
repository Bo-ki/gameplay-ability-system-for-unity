# EX Gameplay Ability System For Unity 2.0

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/Bo-ki/gameplay-ability-system-for-unity)

更新时间：2026-06-08

EX-GAS 2.0 是一个围绕 Unity DOTS / ECS 重构中的 Gameplay Ability System。当前仓库的主线目标不是把 UE GAS 的 OOP 对象层级搬到 Unity，也不是在 ECS 外再包一层厚 OOP runtime，而是把 Ability、GameplayEffect、Attribute、Tag、Cue、Fact 和 Debugger 逐步收敛到可验证、可归因的 ECS Runtime Core。

> 当前实现仍处于快速架构迭代阶段，不是稳定可直接商用的 Runtime。根 README 只做项目入口和当前事实索引；详细事实、目标态 Spec 和任务树分别维护在 `方案讨论/针对2.0的ECS架构的迭代方案讨论/00-当前架构事实`、`01-目标态架构共识` 和 `02-主线任务树`。

## 当前工程基线

| 项 | 当前值 |
|---|---|
| Unity Project Version | `6000.3.14f1`，以 `ProjectSettings/ProjectVersion.txt` 为准 |
| GAS package metadata | `Assets/GAS/package.json`：`com.exhard.exgas` / `2.0.0`，其中 `unity: 2022.3` 是包元数据字段，不代表当前项目版本 |
| DOTS 关键包 | `com.unity.entities 1.4.6`、`com.unity.entities.graphics 1.4.19`、`com.unity.physics 1.4.6`、`com.unity.mathematics 1.3.3` |
| 其他关键包 | URP `17.3.0`、Input System `1.19.0`、Netcode `1.10.0`、AIBridge `1.4.1`、UniTask |
| Runtime asmdef | `Assets/GAS/Runtime/com.exhard.exgas.runtime.asmdef` |
| Generated Runtime asmdef | 无；Runtime v1 的权威运行时由手写 owner 与 kernel 持有 |
| AutoChessDemo asmdef | `Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef` |

不要修改 `Library/PackageCache` 下的包缓存内容；Unity 会自动还原，这类改动不是有效工程修复。

## 代码目录

| 路径 | 当前职责 |
|---|---|
| `Assets/GAS/Runtime` | GAS Runtime v1 主体：V1 owner、command port、tick kernel、transaction、boundary 与纯表现/配置类型 |
| `Assets/GAS/Editor` | GAS Center、CodeGen、Luban / Bean / Authoring 工具 |
| `Assets/GAS/Generated/CodeGen` | CodeGen 输出：Luban normalized rows、Runtime v1 纯生命周期 marker、Editor asmdef 与 validation report |
| `Assets/GAS/General` | Runtime / Editor 共用基础工具 |
| `Assets/AutoChessDemo` | 当前可运行的 AutoChess 业务 Demo 和 GAS Runtime 验收链 |
| `EX_GAS_Config/ProjectConfigTable/exgas_config` | Luban Excel / JSON 配置源和导表脚本 |
| `方案讨论/UnityDOTS官方文档参考` | 本仓库使用的 DOTS 官方文档摘录和规则编号 |
| `方案讨论/针对2.0的ECS架构的迭代方案讨论` | 架构事实、目标态 Spec、任务树和当前进度 |

## 当前 Runtime 主链

当前代码已经不是 1.x 的托管 OOP 运行模型，也不应继续按旧 `AbilitySystemCell`、`AbilityLogicBase`、`AbilitySpec`、`GameplayEffectSpec` 作为 Runtime 主入口理解。

当前可从这些核心入口理解运行链；下表只列现行 Runtime v1 入口：

| 入口 | 当前职责 | 重要状态 |
|---|---|---|
| `GasRuntimeWorldOwner` | 持有 session、identity、definition catalog、slab 与 boundary 生命周期 | Runtime v1 的唯一 world owner |
| `GasCommandPort` | 接收外部 intent 并写入 owner-local command lane | 外部只能提交 command，不同步修改玩法状态 |
| `GasTickDag` / `GasTickKernelSystem` | 按固定阶段推进 command、ability、effect、attribute、tag 与 fact | 调度顺序是单一权威 |
| `GasBoundaryDrainCoordinator` | 将 scope 内 facts 投影到只读 boundary 快照 | Presentation、replay、日志只消费投影结果 |
| `GasRuntimeV1Diagnostics` | 导出机器可读的运行证据与 scorecard | 诊断不反向控制 simulation |

一句话概括当前规则：

> 外部只提交 intent / command；Runtime Core 只跑 ECS data / system / job；Definition 只提供不可变 catalog / lookup / pure glue；Debugger / Replay / Presentation 只消费 Boundary facts 和 evidence。

## 四层边界

| 层 | 目标职责 | 当前代码落点 |
|---|---|---|
| Application Shell | UI、Input、AI、Network、Demo runner，只表达业务 intent 和消费 snapshot/evidence | AutoChessDemo、Editor window、Presentation adapter |
| Runtime Boundary | command port、opaque handle resolve、read model、boundary drain、diagnostics snapshot | `Assets/GAS/Runtime/V1`、AutoChess `Integration/GasCore` |
| GAS Runtime Core | session、ability/effect/attribute/tag transaction 与 fact 的权威 Runtime v1 数据流 | `Assets/GAS/Runtime/V1` |
| Definition & Generation | Luban normalized rows、不可变 catalog/blob schema、纯 glue 与 validation report | `Assets/GAS/Generated/CodeGen`、`Assets/GAS/Editor/CodeGen`、`EX_GAS_Config` |

当前目标态原则：

1. OOP Shell 只能在边界层表达 intent 和消费 snapshot，不得成为 gameplay 中间层。
2. SourceGenerator 只生成 definition、Blob、static lookup、pure evaluator、Baker / Editor glue 和 validation metadata，不生成 Runtime lifecycle / query / ECB / NativeContainer owner。
3. Debugger 输出的是机器可读 evidence，不是 Runtime 控制层；字符串日志、图表和战报只是 derived export。
4. singleton DynamicBuffer 只能作为 proof / 迁移 carrier；scale-ready fan-in 必须有 deterministic merge、allocator owner、capacity / spill 和 battle hash evidence。

## Definition / Luban / CodeGen

配置链路当前由 Luban + SourceGenerator / CodeGen 共同承担：

```text
Excel / schema
  -> Luban 导表
  -> Luban C# / JSON
  -> GAS CodeGen row normalization
  -> normalized rows（Editor）
  -> 手写 Runtime v1 catalog / blob schema
  -> Runtime v1 owner consumes immutable catalog
```

常用命令：

```powershell
EX_GAS_Config\ProjectConfigTable\exgas_config\gen.bat
Tools\CodeGen\Generate-GAS-SourceGen.bat
```

```bash
bash EX_GAS_Config/ProjectConfigTable/exgas_config/gen.sh
```

当前生成链路的验证报告在：

- `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md`
- `Assets/GAS/Generated/CodeGen/GasCodeGen.manifest.json`

当前生成器不再生成旧 Runtime lifecycle、ECS query、ECB owner 或 Runtime asmdef；manifest 只登记 normalized rows、三个 Runtime v1 marker、Editor asmdef 与 validation report。release-ready 仍需要负例验证、catalog lifetime / dispose owner 和规模证据。

## AutoChessDemo

`Assets/AutoChessDemo` 现在是 EX-GAS 2.0 的业务验收 Demo，不再是“待重建的旧样例”。它提供：

- `GameRoom`：房间、玩家席位、阵容、单位展示名和规则码。
- `Battle` / `Battle/Flow`：对局入口、session、tick 推进、胜负收口。
- `Battle/Ecs`：AutoChess 专用 ECS 扩展，包括 command drive、definition catalog、execution calculation。
- `Integration/GasCore`：AutoChess 到 GAS Core 的唯一 adapter，负责 runtime host、catalog session、lifecycle、ticker、observation gateway 和 report fact projection。
- `Battle/Report` / `Battle/Validation`：structured log 到业务战报、headless / scene 共用验证、official diff 和 timing evidence。
- `Presentation`：日志场景和 presentation outbox bridge。

更细的 Demo 事实见 `Assets/AutoChessDemo/README.md` 与 `方案讨论/针对2.0的ECS架构的迭代方案讨论/00-当前架构事实/AutoChessDemo事实.md`。

## 当前风险

当前代码已经具备 DOTS Runtime backbone，但还不能宣称“纯 ECS GAS Core 完成”或“DOTS 性能优秀”。已知主要风险：

1. TargetCatcher 的 authoring 规则仍需完成 typed target-rule bake 与非法配置拒绝。
2. ActiveEffect、fact lane 与 diagnostics evidence 仍需要 capacity / spill / scale profile。
3. AutoChessDemo 验证能证明当前业务链可跑，但不能替代更大规模、Unity Test Runner、Profiler / Journaling 和 Player/AOT evidence。

这些问题的权威记录不写在根 README，统一维护在 `00-当前架构事实`。

## 构建与验证

窄构建：

```powershell
dotnet build .\com.exhard.exgas.runtime.csproj --no-restore
dotnet build .\com.exhard.exgas.editor.csproj --no-restore
dotnet build .\com.exhard.exgas.autochessdemo.csproj --no-restore
```

Unity Test Runner：

```powershell
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml
```

Unity Test Framework `1.6.0` 会在测试结束后自行退出；不要把 `-quit` 与 `-runTests` 同用，否则测试不会执行。

AutoChessDemo batch 验证入口：

```powershell
Unity.exe -batchmode -quit -projectPath . -executeMethod GAS.AutoChessDemo.Editor.AutoChessDemoBatchRunner.RunAutoChessBattleOnceAndExit -logFile Temp/AutoChessBattleValidation.log
```

说明：

- 若 Unity 项目文件未刷新，`*.csproj` 可能落后于 asmdef / 新文件；不要把这种项目文件生成问题误判为 Runtime 架构问题。
- 若全仓 build 被无关旧错误污染，优先用 runtime、editor、AutoChessDemo 等窄门禁定位。
- AIBridge / Unity Editor 运行验证以实际日志和 `statusConfirmed` 为准；超时或项目锁不能写成通过。

## 文档索引

根 README 只做导航。长期文档 owner 如下：

| 文档 | Owner |
|---|---|
| `方案讨论/针对2.0的ECS架构的迭代方案讨论/00-当前架构事实` | 当前代码事实、缺陷诊断、验证证据、过时口径降权 |
| `方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识` | 目标态 Spec、架构 contract、DOTS API 选型、禁止方向、验收门槛 |
| `方案讨论/针对2.0的ECS架构的迭代方案讨论/02-主线任务树` | 可领取任务、任务依赖、交还标准 |
| `方案讨论/针对2.0的ECS架构的迭代方案讨论/04-当前进度状态` | 当前窗口、短期 handoff、最近验证摘要 |
| `方案讨论/UnityDOTS官方文档参考` | Unity DOTS 官方文档摘录和本仓库规则编号 |

重要入口：

- `方案讨论/针对2.0的ECS架构的迭代方案讨论/README.md`
- `方案讨论/针对2.0的ECS架构的迭代方案讨论/00-当前架构事实/README.md`
- `方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/README.md`
- `方案讨论/针对2.0的ECS架构的迭代方案讨论/01-目标态架构共识/16-纯ECS内核与边界重划分/16-06-端到端消息流代码骨架/16-06A-完整端到端消息流代码骨架Spec.md`
- `方案讨论/针对2.0的ECS架构的迭代方案讨论/02-主线任务树/README.md`
- `Assets/AutoChessDemo/README.md`

## 迁移口径

旧说法与当前口径对照：

| 旧口径 | 当前口径 |
|---|---|
| ASC 是托管 `AbilitySystemCell` | ASC 权威由 `GasRuntimeWorldOwner` 持有，外部只使用稳定句柄和 Boundary snapshot |
| 外部调用托管对象立即改状态 | 外部通过 `GasCommandPort` 写入 owner-local command，Runtime v1 在固定 Tick 中消费 |
| Ability 通过 `AbilityLogicBase` 回调执行 | Ability 行为由 V1 definition、owner transaction 与 `GasTickDag` 推进 |
| GE 通过 OOP `GameplayEffectSpec` runtime wrapper 施加 | GE 由 V1 definition / transaction 产生 target-owned state 与 Boundary facts |
| Cue / log / event center 可驱动 gameplay | Cue / presentation / replay / log 只消费 Boundary facts，不决定 simulation |
| 新系统靠自动发现进入调度 | 新 Runtime 阶段必须显式登记到 `GasTickDag` 并补对应验证 |
