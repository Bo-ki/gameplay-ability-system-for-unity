# M01：Session / World 所有权审计

审计基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`（2026-10-01 静态审计）。只通过 GitHub 读取固定提交；没有运行 Unity、测试、Burst、Player 或 Profiler，没有修改产品代码。下文“符合”仅指已检查静态性质，不代表模块端到端验收。

规范层级：[DOTS 依据库 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)明确区分官方机制、EX-GAS 项目规则与实测阈值。本报告中的 SYS/NAT/BUF/STORE 及目标不变量均为项目规则；DynamicBuffer 失效、cleanup、生存期等官方机制依据各主题 API 解读，不把项目选型冒充 Unity 强制要求。[目标态 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/README.md)和[Spec17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md)用于理想目标；受限交付范围不删除目标差距。

## 结论

完整 Simulation→FixedStep→EndFixed→batch fence 拓扑、world-local 唯一注册和 FinalDrain 失败阻止 Dispose 的方向符合项目规则。仍有可由代码直接确认的多 Battle 隔离目标差距，以及 TickBatch/观察 API 的目标契约缺口；不能用“D1 已通过”抹去这些差距。

## 范围与实际检查

- 全文检查：[Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L1-L1240)
- 关联全文检查：Layout、Identity、Storage、Install 目录文件（详见 M02–M05）
- 未读取的调用实现：SessionIngressGate、GasBoundaryDrainCoordinator、Kernel 和 SpawnRecorder 全文；其内部协议由相应模块报告负责。本报告不声称完整调用链或测试覆盖率
- 关联补读：System/GasCommandIngressSystem.cs 第1–100行，重点核验Bind唯一gate；其余行未读
- 测试未执行，Session 专项测试未逐文件读取
- 已读规范：System-World-SystemGroup 的 _index、API 解读、SYS-01～05；NativeContainer-Allocator 的 _index、API 解读、NAT-01～05；Spec02、Spec13-01/02/03、Spec17 相关 Session/调度章节、90 不变量


规范正文定位：[02-四层架构Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/02-%E5%9B%9B%E5%B1%82%E6%9E%B6%E6%9E%84Spec.md)；[13-01-Entity清单与运行时布局Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/13-EntityComponent%E7%89%A9%E7%90%86%E5%B8%83%E5%B1%80/13-01-Entity%E6%B8%85%E5%8D%95%E4%B8%8E%E8%BF%90%E8%A1%8C%E6%97%B6%E5%B8%83%E5%B1%80Spec.md)；[13-03-Buffer容量与Phase映射Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/13-EntityComponent%E7%89%A9%E7%90%86%E5%B8%83%E5%B1%80/13-03-Buffer%E5%AE%B9%E9%87%8F%E4%B8%8EPhase%E6%98%A0%E5%B0%84Spec.md)。其中无独立规则编号的章节以Spec编号和节号作为审计标识，不另造官方规则。

## 规范逐项矩阵

| 规则（EX-GAS） | 判定 | 代码证据与理由 |
|---|---|---|
| [SYS-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/System-World-SystemGroup/SYS-02.md#L1) / [TIM-08](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L26-L26) | 符合（局部） | [GasRuntimeWorldOwner.cs:303–307](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L303-L307)只更新 Simulation 父组；[GasRuntimeWorldOwner.cs:569–608](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L569-L608)显式安装 Physics、GAS、标准 EndFixed 并排序，没有在此枚举旧业务 phase |
| [ARC-07](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L15-L15) / [SYS-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/System-World-SystemGroup/SYS-05.md#L1) | 唯一 owner 符合；多 Battle 隔离存在目标差距 | [GasRuntimeWorldOwner.cs:660–693](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L660-L693)以 world-local registration 检测重复 owner；[GasRuntimeWorldOwner.cs:867–902](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L867-L902)却在任一战局终局后关闭全体 ingress |
| [TIM-09](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L27-L27) / [TIM-12](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L30-L30) | 部分符合 / 目标差距 | [GasRuntimeWorldOwner.cs:614–643](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L614-L643)检查 batch timing 并投影 timestep/MaximumDeltaTime；[GasRuntimeWorldOwner.cs:303–307](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L303-L307)无 absolute elapsed 输入，仅返回 true，不返回 physical/gameplay/maintenance/debt |
| [TIM-13](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L31-L31) / [JOB-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L39-L39) | 静态拓扑符合；实际 0/N 次未验证 | [GasRuntimeWorldOwner.cs:1208–1236](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L1208-L1236)在 Simulation 尾部调用一次 fence；[GasRuntimeWorldOwner.cs:497–539](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L497-L539)于批次边界完成 jobs 后 drain。此处 Complete 是获准 outer fence，不能误报为 lane 间同步 |
| [BND-14](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L139-L139) | 部分符合 / 完整状态机未验证 | [GasRuntimeWorldOwner.cs:458–491](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L458-L491)先关 ingress，停止 PlayerLoop，完成 jobs，FinalDrain 失败抛出且不置 disposed；但没有在本类看到完整 CleanupAudited/DisposedReceipt/ValidationResultSeal 协议 |
| [BND-15](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L140-L140) / [ATT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L90-L90) | 目标差距（观察 API） | [GasRuntimeWorldOwner.cs:313–437](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L313-L437)直接读 live ECS；[GasRuntimeWorldOwner.cs:405–411](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L405-L411)把 attribute[0]/[1]解释为 Health/Energy，未从 layout 角色映射 |
| [NAT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-01.md#L1) | 符合（本文件分配） | [GasRuntimeWorldOwner.cs:995–1029](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L995-L1029)Temp entity array 用 using，使用在同步 fence 内；未发现本类 Persistent 容器。Blob 释放属于外部 owner，不能由本类证明 |

## 已确认偏离与触发条件

### M01-P1-01：多战局终局封闭范围过大（目标差距）

触发：同一 Session 中 A 已终局而 B 仍运行。CloseDetectedBattleTerminal 选最小 stable-id 终局 A，随后无条件调用 CloseBattleIngress(session) 与 CloseAscIngress(session)；后两者遍历所有 live 项并设置 IngressClosed，未按 Battle 过滤（[GasRuntimeWorldOwner.cs:1076–1107](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Session/GasRuntimeWorldOwner.cs#L1076-L1107)）。影响：B 的命令/ASC admission 可被 A 的终局提前封闭，违反 ARC-07/BND-12。代码反例是确定的；尚未声称当前单战局已验收向量发生失败。修复方向：以精确 terminal token/per-Battle membership 封闭；全 Session fault/显式 shutdown 才全关。

### M01-P2-01：TickBatch 时间与回执契约未达目标

触发：独立 runner 需要控制单调 elapsed、检查 debt 或核验 maintenance/gameplay 次数。当前无参 bool 方法依赖调用方配置 World 时间；TIM-12 规定的输入与回执不能由本 API 表达。影响：调用者无法只靠受限 Session capability 验证批次时间合同。不是宣称已出现重复 Tick。

### M01-P2-02：live 观察与角色硬编码

触发：调用 TryReadSessionObservation，或换用 AttributeLayout 中 Health/Energy 索引不是 0/1 的 Catalog。该方法绕过 immutable snapshot cut，且给位置字段赋予业务名称；可能读到语义错误的显示值。应迁移到带 cut 的 immutable cache，并通过生成 layout 角色索引投影。此方法是观察而非权威写，不把它升级为已发生 gameplay 双事实源。

### M01-P1-02：同一 World 释放 owner 后无法重新安装（已证实静态路径）

触发：Install(world) → owner.Dispose()成功 → World保持存活 → Install(world)。第一次Dispose只销毁registration，不销毁已有Ingress/System；第二次Install创建新SessionIngressGate，但GetOrCreateSystemManaged返回旧Ingress，随后Bind新gate。[Ingress的Bind:69–78](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasCommandIngressSystem.cs#L69-L78)明确拒绝不同引用，因而抛InvalidOperationException。这是可由分支直接推导的生命周期缺陷，不依赖多Battle语义。当前Demo若同时销毁World可避开，不能宣称所有现有运行均失败。建议明确每World只装一次的合同，或安全teardown后重建Ingress；禁止随意开放运行中gate重绑。

## API / ECS 适用性说明

| 维度 | 本模块答案 |
|---|---|
| Tick | Simulation父链统一驱动；TIM-12所需elapsed/详细batch回执尚缺 |
| owner | World-local registration唯一；World由调用方释放；同World重装有P1路径 |
| 数据/API | managed lifecycle shell使用EntityManager/query，观察返回纯值；BND-15 immutable cut仍未覆盖 |
| allocator | Temp array using释放；未分配Persistent；不拥有Kernel scratch；Blob teardown由实际catalog owner负责 |
| 结构变化 | 注册entity属setup，spawn录入标准EndFixed |
| drain | batch后fence、Dispose时FinalDrain；失败保持registration和未disposed状态 |
| ScaleProfile | 投影TickRate/MaximumDeltaTimeTicks；实际步数、debt与峰值未实测 |

## 历史状态与当前裁决

[D0-M2R 路线接受裁决](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D0-M2R-SourceGenerator%E8%B7%AF%E7%BA%BF%E6%8E%A5%E5%8F%97%E4%B8%8ED1%E6%8E%88%E6%9D%83%E8%A3%81%E5%86%B3.md#L11-L28)已明确取代“生产路线为空”的历史状态；[D1 最终验收结果](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D1-SourceGenerator%E7%94%9F%E4%BA%A7%E8%BF%81%E7%A7%BB%E4%B8%8E%E5%8F%AF%E8%BF%90%E8%A1%8C%E9%AA%8C%E6%94%B6%E7%BB%93%E6%9E%9C.md#L9-L21)记录 SourceGenerator 迁移及受限闭世界可运行基线通过，同时保留 ProductionInstallAdmission=NotEvaluated、FullSemanticEligibility=false。这里只引用仓库已有结果，没有重新复验其 raw evidence；不将 Pending 自动判为缺陷，也不将该 Passed 外推为全部目标不变量通过。

## 后续验证（本次未执行）

1. 两个 Battle：A 终局后 B 继续 accept/consume；分别核对 Gate、Battle slot、ASC IngressClosed 与请求终态
2. 0/1/N FixedStep 的 outer batch：Drain 恰一次；有 InFlight retry 时不增加 gameplay Tick
3. 非默认 TickRate、非法 timing、递减 elapsed、超 batch debt 的 typed failure 与写前检查
4. FinalDrain 失败→显式重试→成功：registration 不提前移除，facts 不丢，最终 receipt 与 Blob owner teardown 顺序可证明
5. 交换 Health/Energy layout index，检查观察值来源与 snapshot cut；对关闭后的 terminal tombstone 禁止 live fallback

6. 同World Install→Dispose→Install，以及Dispose失败→重试：核验gate/system/registration生命周期，区分销毁World重建与保留World重装路径
