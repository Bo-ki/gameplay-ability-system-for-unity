# M06 Ability 与 Wait 模块 ECS 标准审查

## 结论与审查口径

审查日期：2026-10-01。

基线 `61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。Ability/Activation/Continuation/Subscription 已采用 ASC-local 非压缩 slab 与数值状态机；Wait 具备 observed-owner 注册、代际校验、取消 fence、T+1 completion 等实质实现。当前 one-shot 的正常 End 与 owner-terminal cleanup 已存在，不能继续报告为“没有生命周期收口”。

完整目标仍未完成：Commit requirement 二次检查、非 one-shot 的 contribution/cleanup 闭环、dead-owner cooldown/tag 收尾，以及开放全部 Wait 后的运行证明必须保留。当前闭世界限制只能界定可达性，不能改变 Spec17 的目标要求。

审查方式：只读 GitHub + 静态逐文件阅读（长文件重复 XML 注释不作为动态证明）。无代码修改，无 Unity/Burst/Player/压力测试执行。

## 覆盖清单

本模块全部 9 个第一方 C# 已读：
- [Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs)，610 行
- [Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerPlanUtility.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerPlanUtility.cs)，700 行
- [Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerTransaction.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerTransaction.cs)，462 行
- [Assets/GAS/Runtime/V1/Ability/GasAbilityPendingCommandOrder.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityPendingCommandOrder.cs)，94 行
- [Assets/GAS/Runtime/V1/Ability/GasAbilityRuntimeTypes.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityRuntimeTypes.cs)，255 行
- [Assets/GAS/Runtime/V1/Ability/GasAbilitySlabStorages.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilitySlabStorages.cs)，229 行
- [Assets/GAS/Runtime/V1/Ability/GasAbilityWaitObservationUtility.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitObservationUtility.cs)，201 行
- [Assets/GAS/Runtime/V1/Ability/GasAbilityWaitProtocol.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitProtocol.cs)，805 行
- [Assets/GAS/Runtime/V1/Ability/GasAbilityWaitSlabTransaction.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitSlabTransaction.cs)，1799 行

交叉证据：
- [SupportProfile](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L83-L166)：四个 one-shot、0 requirements、cost/cooldown disabled
- [公开命令预拒绝](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/SessionIngressGate.cs#L443-L463)：Cancel/RemoveEffect 在 gate 返回 UnsupportedByRuntimeV1Profile
- [dead owner cooldown caller](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L4258-L4312)：仅 Ready/Alive owner 执行 due release
- [03D 全文](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03D-CommandResolve%E4%B8%8ETargetResolveSpec.md) 与 [Spec17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md)

本模块未读 C#：无。上述 Kernel/Gate 是定点交叉阅读，不代表其整文件已由本模块审完；共享 allocator/layout、Kernel job 依赖、Definition producer 与测试由对应模块审查。

## 标准矩阵

| 规则或契约 | 分类 | 实现判断 |
|---|---|---|
| [FSM-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E7%8A%B6%E6%80%81%E6%9C%BA%E7%AD%96%E7%95%A5/FSM-01.md)、[FSM-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E7%8A%B6%E6%80%81%E6%9C%BA%E7%AD%96%E7%95%A5/FSM-02.md) | EX-GAS 项目规则；[官方 FSM 机制解读](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E7%8A%B6%E6%80%81%E6%9C%BA%E7%AD%96%E7%95%A5/API%E4%B8%8EEX-GAS%E8%A7%A3%E8%AF%BB.md) | enum/state 字段，不靠高频普通 tag Add/Remove，符合 |
| [BUF-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/BUF-01.md) | EX-GAS 项目规则 | slab append/overwrite 不 compact；IBC、high-water profile 未动态验证 |
| [BUF-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/BUF-02.md)、[PRF-19](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-19.md) | EX-GAS 项目规则 | utility 只写传入 owner buffers；跨 owner Wait 用命令，局部符合 |
| [PRF-13](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-13.md)、[NAT-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-02.md) | EX-GAS 项目规则 | pending 稳定 key与明确 semantic priority；需全调度不同 worker 验证 |
| [NAT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-01.md) | EX-GAS 项目规则 | utilities 无自有 allocator；持久 Wait 为 slab，previousPlans 为借用 scratch，未保存到 static |
| [Commit](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L242-L249)、[Continuation](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L259-L268) | 当前项目目标契约 | 幂等与 T+1 局部符合；requirement 二检及完整 cleanup 尚有缺口 |
| [03D owner shadow](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03D-CommandResolve%E4%B8%8ETargetResolveSpec.md#L48-L74) | 当前项目目标契约 | 前序 owner plans RYW 已有，不从 incoming target effect 读取 |

项目规则不是 Unity 官方强制，证据分层见 [README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md#L5-L13)。没有按 state 数、代码行数或固定 Entity 数直接判违规。

## 逐项结果

### A06-01 稳定 slab 与强类型 handle 符合

[slab adapters](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilitySlabStorages.cs#L8-L227) 仅读写 header 和尾部 Add，不 RemoveAt/SwapBack；[ObservedHandle](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityRuntimeTypes.cs#L123-L251) 携带 epoch/owner/index/generation/kind。[owner plan 解析](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerPlanUtility.cs#L314-L360) 调用 StableHandleValidator，[Wait 精确解析](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitSlabTransaction.cs#L1739-L1781) 额外比较完整 typed handle。

符合 [Spec17 slabs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L130-L152)，但代际递增、free-list 容量和所有引用释放仍要共享 allocator 与调用方共同证明。

验证门：slot 复用后旧 grant/activation/continuation/subscription handle 被拒；不同 owner 同 index/generation、跨 epoch、wrong-kind；churn 不改变其他 live index。

### A06-02 当前 one-shot normal End 与 owner terminal 已收口

[TryBeginCommittedOneShotNormalEnd](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs#L104-L130) 仅对成功 Committed 且有 frozen work 的 plan 标记 Ending/Completed；[RunPostCommand](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs#L82-L99) 等 live continuation 消失后 finalize；[FinalizeActivation](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs#L359-L379) tombstone 并减少 grant child count。[TryFinalizeOneShotOwnerTerminal](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs#L136-L167) 在无 live wait、形状合法前提下将 owner 的 activation 终止。

这是符合项，不再沿用“正常结束不存在”的旧结论。其契约明确是受限 one-shot，不能推断完整 wait/activation-owned contribution cleanup 全部实现。

验证门：Activate→Commit→正常 End→下一次可激活；Committed work 在源随后结束时保留；owner terminal 时准确回收 child count；重复 finalize/stale 不重复 decrement。

### A06-03 Commit 幂等已有，requirement 二次检查缺失

**等级：P1 目标偏离，现行 0-requirement profile 不可触发。**

[PlanCommit](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerPlanUtility.cs#L162-L219) 重读 activation 的 shadow phase；Committed 返回 AlreadyCommitted，Ending/Ended 返回 OwnerEnding；随后校验 target/cost/cooldown。它没有 TagCountSlot 参数，也未调用 requirement evaluator。requirement 检查只在 [PlanActivate](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerPlanUtility.cs#L132-L147)。

[Spec17 Commit1](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L242-L249) 明确要求 Commit 时重新检查 requirement。触发：Activate 后到 Commit 前 required tag 被移除或 blocked tag 出现。影响：若仅放宽 Profile，会产生本应拒绝的 Commit。当前 [Profile 0 requirements](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L147-L154) 排除此输入。

修复：在 owner shadow 的相同线性化口径上二次检查 requirement，失败 typed reason、cost/cooldown 零写。验证：跨 Tick tag 变化、同 Tick 前序 cooldown owned tag、重复 Commit、Cancel-before-Commit/Commit-before-Cancel；不要由 incoming target effect 污染 owner plan snapshot。

### A06-04 Wait 的分布式注册和取消协议有实现，但不是已验收业务面

[protocol register/ack/wake](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitProtocol.cs#L324-L455) 分开 PendingRegistration、Registered、Completed、Ending、Ended；[T+1 与 overflow](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitProtocol.cs#L564-L634)、[resume tick](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitProtocol.cs#L766-L775) 不让 completion 在 emit Tick 直接 resume。[注册时采样](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitObservationUtility.cs#L13-L49) 区分 Level/Edge/Event/HandleLifecycle；[owner cancel generation](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitSlabTransaction.cs#L312-L396) 立即抬 registration generation，迟到 response 不匹配。[observed unsubscribe/fence](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitSlabTransaction.cs#L462-L524)、[fence record](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitSlabTransaction.cs#L1264-L1372)、[owner ack cleanup](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityWaitSlabTransaction.cs#L645-L691) 是实质实现。

**分类：静态局部符合 + 动态未验证。** 不能写成“Wait 只有空壳”，也不能写成“完整 Wait 已在 Runnable 验证”。[03D continuation](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03D-CommandResolve%E4%B8%8ETargetResolveSpec.md#L65-L76) 要求多 child 和双端 generation；本次未执行相应集成矩阵。

验证门：register-before-cancel/cancel-before-register、completion-before-ack、迟到 signal、subscription reuse、observed owner gone、owner dying、persistent 多 wake 同 tick、ulong.MaxValue；确认 input gate/profile 与 Definition 能力声明同口径。

### A06-05 扩展 cooldown 前必须闭合 dead owner cleanup

**等级：P1 目标缺口，现行 cost/cooldown disabled profile 外。**

[ReleaseDueCooldowns](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerTransaction.cs#L13-L74) 正常释放 due gate 并减 owned tag；但是 [Kernel 调用门](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L4258-L4312) 只对 Ready/Alive owner 调用。[one-shot owner-terminal helper](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs#L136-L167) 无 cooldown/tag 参数。

触发：未来允许持久 cooldown，ASC 死亡但仍保留到后续 Tick。影响：不能只靠正常 due lane 保证 gate/tag 及时释放；若扩展 revive/terminal targeting，残留状态可能被再次观察。当前 [Profile 禁用](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L147-L154) 防止此输入，不是现行 demo 已复现 bug。

修复：明确 owner terminal 对 cooldown 和 owned contribution 的单一 cleanup owner、时点与幂等；不要通过删除生命 gate 让 dead ASC 重新接收普通 ability work。验证：cooldown 未到期死亡、到期同 tick 死亡、terminal保留、复活（若支持）、shutdown、重复 cleanup。

补充风险：[cost shadow](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerPlanUtility.cs#L381-L406) 直接叠加前序 plan delta，未复用 clamp-aware shadow primitive。开放非默认 clamp/cost contract 前应验证与真实 [commit mutation](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerTransaction.cs#L290-L333) 相同，否则 affordability 的 shadow 与 publish 值可能不一致。这是扩展面验证项，不据此宣称当前 cost 错扣。

### A06-06 完整 activation contribution 与公开 Cancel 仍是目标缺口

**等级：P1 目标缺口，不是当前请求被 accept 后失效。**

[contribution enum](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityRuntimeTypes.cs#L87-L103) 和 [contribution slab adapter](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilitySlabStorages.cs#L168-L195) 定义了 OwnedTag/Block/Cancel/Cue/RemoveOnActivationEnd。现有 [normal/terminal finalize](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs#L82-L167) 与 [FinalizeActivation](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs#L359-L379) 没有 contribution buffer、emitted-application cleanup ledger 或精确 remove-ack 接口。Wait cleanup 已有，不等于其余 owned contributions 也已整合。

目标 [Cancel/End ownership](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L251-L268)、[GE grant cleanup 图](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/05-ActiveEffectStoreSpec.md#L173-L196) 要求完整收口。当前 [Gate Cancel pre-reject](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/SessionIngressGate.cs#L443-L463) 是诚实拒绝，不应报告为“公开 Cancel 已受理却不起作用”。

修复：在开放 Cancel、多 task、GE grant、RemoveOnActivationEnd 前，把 contribution 精确撤销、远端 subscription Ack、grant child 保留与 tombstone reuse 纳入同一 admission/cleanup图。验证 LeaveGranted、RemoveWhenAllActivationsEnd、CancelImmediately、多 child、多源 grant、普通 AuditOnly emitted work 不被 End 撤回。

### A06-07 确定序和性能证据仍需动态补齐

**等级：P2 动态未验证。**

[PendingCommandOrder](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityPendingCommandOrder.cs#L11-L90) 显式比较 AvailableTick/CommandSequence/semantic priority/recipient/tag depth/owner/registration/continuation/wake/generation，不依赖 worker append；这是 PRF-13 的正向实现。[removal selection](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityLifecycleMaintenance.cs#L241-L296) 多次扫描；[前序 plan shadow](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerPlanUtility.cs#L381-L438)、concurrency/requirement 扫描可随 commands×slots 增长。未经 profile 不宣称必须 job 拆分或固定倍率性能损失。

验证门：重复 canonical key 是否在外层 fail-closed、改变物理 command 顺序/worker数/0-1-N TickBatch 后 hash 一致；hot owner、wait fanout、late cancel、slab high-water、native allocation/IBC、扫描耗时。utility 无自有 NativeContainer 分配不等于整 Kernel allocation-free。

## 最终判定

当前 one-shot lifecycle 与 Wait 协议存在，不需以旧缺陷重复否定。A06-03/05/06 是开放完整目标前的必要缺口；A06-04/07 保留动态验证门。没有本模块证据证明现行受限输入存在可复现的 P0；也没有证据支持宣称完整 Ability ECS 目标通过。
