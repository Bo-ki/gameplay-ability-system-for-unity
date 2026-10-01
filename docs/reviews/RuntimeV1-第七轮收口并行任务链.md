# Runtime V1 第七轮收口并行任务链

> 状态：已完成（2026-08-30）  
> 权威结果：[Runtime V1 第七轮收口执行结果](RuntimeV1-第七轮收口执行结果.md)

## 结论

本轮已按“三条外部并行线 + 一条集成主线”完成收口：静态门通过、EditMode 11/11、PlayMode 5/5，唯一 Development Windows Player 已实际构建并启动，三个运行向量全部通过且进程 exit 0。最终身份为 `P=3cd42fde…`、`G=18fe1344…`、`F=85078d0a…`、`BuildHash=6d2164ad…`。

执行中保留了完整失败历史：PlayMode Attempt1 的 3/5 定位到 payload logical Length P0；Retry1 的 4/5 继续定位到 9203 验证器重复消费 Boundary ring；Retry2 达到 5/5。Player Build Attempt1 暴露 Unity 6.3 的 URP Compatibility Mode define 缺失，补齐唯一 Standalone 项目配置后构建与 Player 均通过。以下内容保留为当时的并行任务边界与可复用提示词，不再表示待执行状态。

所有并行线必须遵守以下共同约束：

- 不重新运行 CodeGen，不手改任何 generated 文件。
- 不覆盖或删除 `PlayMode.xml`、`PlayMode.log` 与 `PlayMode.Attempt1.provenance.json`。
- 不启动 Unity；唯一 Unity 进程、静态门、PlayMode Retry 与 Player 均由集成主线执行。
- 不修改未分配的文件；发现额外问题只报告，不顺手修复。
- 不提交 Git，不回滚现有 dirty worktree。

## P1：Payload 补丁交叉审查线

目标：在集成主线落下最小补丁后，独立证明补丁同时满足 append、exact-size reuse、失败回收和 shadow publish 不变量。

写集：无。只读审查并在会话中返回报告。

重点文件：

- `Assets/GAS/Runtime/V1/System/GasTickJobs.cs`
- `Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs`
- `Assets/GAS/Runtime/V1/System/GasStageBBootstrapRecorder.cs`
- `Assets/GAS/Runtime/V1/System/GasStageBSpawnFinalizeJob.cs`

验收：

- 新 append 只能在已预留 Capacity 内把 logical Length 扩到新的 `ValueHighWater`。
- exact-size reuse 不得误扩长度或破坏旧 capture。
- 所有拒绝路径保持显式 recycle，不留下 live range 或错误高水位。
- 不把 Stage-B buffer 预填到 profile 最大值。
- 报告按 P0/P1/P2 分级；没有问题时明确写“未发现阻塞项”。

可复制提示词：

> 你负责 Runtime V1 第七轮 P1-Payload 补丁交叉审查，只读，不修改文件、不运行 Unity、不提交 Git。当前已证明的 P0 是 `GasTickJobs.TryAllocateCaptureRange` 在 allocator 推进 `ValueHighWater` 后仍用旧 `PayloadValues.Length` 校验，导致 production 9203 与 AutoChess 锁存 1019。等待或检查集成主线对 `GasTickJobs.cs` 的最小修复，逐项审查新 append、exact-size reuse、Capacity/Length/ValueHighWater、一切失败回收、shadow copy/publish 与 Stage-B 空长度语义。仅返回证据化审查报告，按严重度列出问题；不要建议扩大功能或测试范围。

## P2：Amendment2 证据链准备线

目标：准备不覆盖 Attempt1 的 Amendment2/Retry1 脚本，让集成主线审核后执行。

唯一写集：

- `TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/Invoke-Amendment2.ps1`

禁止修改 `RunManifest.json`、既有 sidecar、项目源码或文档；禁止执行脚本与 Unity。

证据前提：

- RunId：`RV1-20260830T070718Z-ab754759ec68`
- P：`3cd42fde517c3d7f8b217846dd3798c980634438a149eedb9dc3782694ae79fb`
- G：`18fe1344cf0754728c55ced657919e939a6d0ea382bc0b78df74a4daa267d3af`
- 被取代 F：`90a91044839d93a9e98c0373b1c98b2982f9e63269078e0f6fb27f8ede90c6c5`
- Attempt1 sidecar SHA-256：`d5e3f4dc7771821f494d6970166f64cfc223142882aaad9922458f0f90953631`

脚本必须：

- 复用现有 `RuntimeV1RunnableEvidence.ps1` 的 canonical inventory/hash helper。
- 先校验 P/G、旧 F、Attempt1 sidecar 及原始 XML/log 哈希，再允许更新最终 F。
- 创建 distinct evidence：`J3Freeze.Amendment2.json`、`StaticGate.Amendment2.*`、`EditMode.Amendment2.*`、`PlayMode.Retry1.*` 及各自 provenance。
- EditMode 与 PlayMode 仍只运行 category `RuntimeV1Runnable`，均不得带 `-quit`。
- 最终 F 变化后不得复用旧 Edit sidecar；依赖顺序固定为 StaticGate.Amendment2 → EditMode.Amendment2 → PlayMode.Retry1。
- Build/Player 只能依赖绿色的 `PlayMode.Retry1.provenance.json`。
- 任一路径已存在时 fail closed；不得覆盖。

可复制提示词：

> 你负责 Runtime V1 第七轮 P2-Amendment2 证据链准备。只能新建 `TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/Invoke-Amendment2.ps1`，不得修改其他文件、不得运行脚本或 Unity、不得提交 Git。复用同目录现有 helper/launcher，严格保留旧 Edit 与 PlayMode Attempt1，校验任务卡列出的 P/G/旧 F/sidecar 哈希，然后提供 distinct Amendment2 freeze、static gate、EditMode Amendment2、PlayMode Retry1 和依赖 Retry1 的 BuildAndPlayer phases。所有路径 fresh-only、失败关闭；EditMode/PlayMode 均禁止 `-quit`。完成后报告脚本入口、输出路径与静态自检结果。

## P3：Development Player 前置审查线

目标：在 PlayMode 重试期间提前排除 Player 构建与身份绑定阻塞，不运行构建。

写集：无。只读审查并在会话中返回报告。

重点文件：

- `Assets/AutoChessDemo/Editor/RuntimeV1RunnablePlayerBuilder.cs`
- `Assets/AutoChessDemo/AutoRunner/RuntimeV1RunnablePlayerOrchestrator.cs`
- `Assets/AutoChessDemo/Presentation/Scenes/AutoChessLogDemo.unity`
- `TestResults/RuntimeV1Runnable/RV1-20260830T070718Z-ab754759ec68/Invoke-RuntimeV1RunnableJ3.ps1`

验收：

- BuildScenes 只有 `Assets/AutoChessDemo/Presentation/Scenes/AutoChessLogDemo.unity`。
- Development Build、启动参数、`PlayerResult.json`、RunManifest SHA 与 BuildHash 绑定闭环一致。
- 不存在 `-gasAutoChessDemo` legacy runner 旁路。
- Player 只声明 SupportProfileAdmission=Passed、ProductionInstallAdmission=NotEvaluated、Scale=1、AscCount=4 与三个绿色向量，不冒充完整生产安装验收。
- 只报告真正会阻塞本轮 Player 的 P0/P1；V1.1 hardening 单列但不要求本轮实现。

可复制提示词：

> 你负责 Runtime V1 第七轮 P3-Development Player 前置只读审查。不要修改文件、不要运行 Unity/构建、不要提交 Git。检查任务卡列出的 builder、orchestrator、唯一 scene 和 J3 launcher，验证 Development Build、RunManifest/BuildHash/PlayerResult 身份闭环、三个向量、限定声明以及 legacy runner 禁止项。按 P0/P1/P2 给出证据；只把会阻塞本轮真实 Player 的问题列为本轮修复，其他放入 V1.1。

## 集成主线：由根 Agent 独占

集成主线唯一负责：

1. 修改 `GasTickJobs.cs`，同步 capture payload logical Length；不改 allocator 与 Stage-B 模型。
2. 审核 P1/P2/P3 结果并拒绝越界改动。
3. 更新 Amendment2 F，运行一次静态编译门，并为新 F 重跑一次最小 EditMode category。
4. 运行 `RuntimeV1Runnable` PlayMode Retry1；若绿色，立即构建并运行唯一 Development Player。
5. 校验 `PlayerResult.json`、BuildHash、RunManifest SHA 与所有 sidecar，再更新最终收口结论。

本轮停止条件只有两个：Development Player 绿色完成，或出现有明确证据且无法在冻结写集内解决的新 P0。非阻塞警告和 V1.1 hardening 不得拖延 V1 交付。
