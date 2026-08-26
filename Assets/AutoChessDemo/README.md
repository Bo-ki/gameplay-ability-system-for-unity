# AutoChess Demo

`Assets/AutoChessDemo` 是 EX-GAS 2.0 Runtime v1 的业务验收向量。它只通过唯一的 `GasCommandPort` 送入冻结命令，并通过 `GasBoundaryDrainCoordinator` 读取不可变 Boundary facts；Demo 不再拥有独立的战斗 System、Entity 权威或第二条事实流。

## 模块职责

- `GameRoom`：房间、席位、阵容和业务展示信息。
- `Battle`：输入/结果契约、会话、战斗流程和报告投影。
- `Battle/Ecs`：AutoChess 的定义 Catalog 装配与纯业务规则，不创建生命周期 System。
- `Integration/GasCore`：Runtime v1 的唯一接入层，负责 World/Session/Catalog、单位句柄、CommandPort、Boundary drain 和只读观察模型。
- `Battle/Validation`：headless、PlayMode、重复运行、Boundary hash 与 official diff 的验证编排。
- `Presentation`：只消费验证结果和 Boundary 派生战报，不读取 ECS World。
- `Generated`：Luban/source generator 生成的不可变配置、静态 evaluator 和 glue，不生成生命周期 System。

## Runtime v1 业务链

1. `AutoChessBattleSession` 从房间定义创建 `BattleInstanceId`、`OwnerAscHandle` 和 SpawnBatch；EndFixed 后由 `GasStageBSpawnFinalize` 整批发布 Ready。
2. 会话通过唯一 `GasCommandPort` 提交带 Battle/Epoch/RequestId/AvailableTick 的冻结命令；Ingress 只接受合法命令并在 Kernel 内 seal。
3. `GasTickKernelSystem` 按固定 DAG 执行 Gather、OwnerPlan、TargetResolve、WholeTickAdmission、OwnerWave、SourceSpecProjection、TargetWave、Stabilize/Death、StableFactMerge、BoundaryProject。
4. Attribute、EffectLifecycle、PeriodTick、ExecutionCalculation、Cue、Death 和 Fault 都写入唯一 Boundary outbox；Admission 失败时 gameplay lanes 零写，只锁存 Session fault。
5. `AutoChessGasObservationGateway` 通过唯一 managed drain 消费 ring，构造 `AutoChessGasV1ObservationSnapshot`、结构化日志和稳定 semantic hash。
6. `AutoChessBattleReportBuilder` 与 Presentation 只消费该 read model；旁路消息管线和 raw ASC Entity 都不参与业务判定。

## 冻结向量

当前 Catalog 覆盖普攻 `9201/9202`、毒伤持续效果 `9203` 和斩杀 `9207`，能力码为 `9101/9102/9103/9104`。Execution 公式由生成配置提供，Runtime 只执行静态 evaluator。

## 验收标准

- Runtime、AutoChess、Generated 项目编译无 Error。
- Runtime v1 EditMode/PlayMode 覆盖 SpawnBatch、whole-tick admission、target-owned mutation、death fan-in、period、wait、Boundary retry/late-tail/NoFactReceipt 和 shutdown drain。
- AutoChess 重复运行的 accepted command count、Attribute/Effect/Period/Cue/Death facts 与 Boundary sequence hash 一致。
- 业务验证只接受 `RuntimeV1Observation`；`RuntimeDiagnostics` 和 Unity Journaling 仅作为独立性能/官方差异 pass。
- 不存在 runtime selector、fallback adapter、第二个 managed drain 或第二个事实入口。

## 常用命令

```powershell
dotnet build com.exhard.exgas.runtime.csproj --no-restore -p:UseSharedCompilation=false
dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -p:UseSharedCompilation=false
E:\Unity\UnityEditor\6000.3.14f1\Editor\Unity.exe -batchmode -nographics -projectPath E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity-dots-gas-v1 -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml -logFile Logs/EditMode.log
E:\Unity\UnityEditor\6000.3.14f1\Editor\Unity.exe -batchmode -nographics -projectPath E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity-dots-gas-v1 -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml -logFile Logs/PlayMode.log
```

Unity 缓存、测试日志和构建产物不纳入提交；未经明确要求不 push。
