# M09 Tag 模块 ECS 标准审查

## 结论与边界

审查日期：2026-10-01。

基线 `61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。TagCount 的 exact/inclusive 权威、祖先传播、数值边界与派生 presence 有局部闭环。完整的“按 contribution 身份恰好撤销一次”不是 count helper 能独立实现的；当前 Runnable 强制 TagCatalog 与 requirement 为空，不能把它当成 Tag 业务链已验证。

本次只读 GitHub、静态审查；无 Unity/Player/Burst/性能测试、无代码修复。规范采用当前 Runtime v1，未把旧 Ability Entity/五组路线当作目标。

## 文件覆盖

全部 scoped 第一方 C# 文件已阅读全文：
- [Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs)，1–248 行
- [Assets/GAS/Runtime/Tag/GameplayTag.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Tag/GameplayTag.cs)，1–114 行

交叉阅读：[Profile 零 Tag 门](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L83-L101)；[Effect requirement consumer](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Effect/GasGameplayEffectRequirements.cs#L30-L123)；[Ability cooldown tag writer](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Ability/GasAbilityOwnerTransaction.cs#L13-L74)；[State/Tag Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-02-StateEvaluateActiveEffectStoreSpec.md)；[Spec17 TagCount](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L319-L327)。

本模块未读 C#：无。生成期 TagCatalog 去重/闭包构造、Layout 定义、Kernel 事件传播与测试不属于本模块全覆盖结论。旧 GameplayTag 的全部引用点未在此逐一审查，不能声称它已完全退出所有程序集。

## 标准矩阵

| 标准及类型 | 审查点 | 结果 |
|---|---|---|
| [FSM-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E7%8A%B6%E6%80%81%E6%9C%BA%E7%AD%96%E7%95%A5/FSM-01.md)、[FSM-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E7%8A%B6%E6%80%81%E6%9C%BA%E7%AD%96%E7%95%A5/FSM-02.md) EX-GAS 项目规则 | Tag 业务状态不用高频 ECS component Add/Remove | 符合；count/presence 数值写入 |
| [BUF-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/BUF-01.md) EX-GAS 项目规则；[官方机制解读](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/API%E4%B8%8EEX-GAS%E8%A7%A3%E8%AF%BB.md) | 固定 buffer 形状、IBC与外部化实测 | 形状符合；性能未验证 |
| [BUF-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/BUF-02.md)、[PRF-19](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-19.md) EX-GAS 项目规则 | target 唯一 writer | helper 接收 owner-local buffers，无随机跨 owner 写；调度需相邻模块证明 |
| [PRF-13](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-13.md) EX-GAS 项目规则 | 稳定祖先传播与可重建 cache | 数值操作固定顺序；来源撤销身份仍有目标缺口 |
| [Spec17 TagCount](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L319-L327)、[03E-02 Tag authority](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-02-StateEvaluateActiveEffectStoreSpec.md#L121-L123) 项目目标契约 | 多来源、underflow/overflow、exactly-once 撤销 | helper 的计数算术符合，完整 provenance 未闭合 |

以上项目选型不等于 Unity 官方强制。依据分层遵循 [依据库 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md#L5-L13)。

## 逐项结果

### T09-01 fixed dense shape 与 ancestor range 校验符合

[ValidateShapeAndRange](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L137-L170) 同时校验 counts/presence 长度、dense TagIndex、合法半开 range；祖先索引严格递增、在界内且包含自己，避免重复 ancestor 被加两次。

符合 [祖先传播契约](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-02-StateEvaluateActiveEffectStoreSpec.md#L121-L123)。验证建议：重复祖先、缺 self、负 range、溢出式 Start/Count、最后 TagIndex、63/64/65 个 tag。失败时 count 与 presence 均不变。

### T09-02 全链 underflow/overflow 写前检查符合

[ValidateExistingCounts](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L175-L185) 拒绝负数及 Exact>Inclusive；[ValidateDelta](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L191-L217) 用 long 计算所有 ancestor 的 nextInclusive 及叶节点 nextExact，统一通过后 [TryApplyDelta](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L93-L111) 才写回。单次请求不会因中途祖先溢出留下半更新。

验证建议：两个不同子 tag 共享父 tag，依次 +1/+1/-1/-1；叶/父分别到 int.MaxValue、delta=int.MinValue、重复 -1。注意“单 delta 原子”不自动等于“多个 delta 的复合事务原子”，后者仍需外层 shadow/admission。

### T09-03 presence 是派生值而非第二权威符合

[RebuildPresence](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L219-L237) 清空固定 bitmap 后由 InclusiveCount>0 重建；[requirement consumer](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Effect/GasGameplayEffectRequirements.cs#L90-L110) 直接读 count，避免把 cache 当权威。这里没有 Entity tag AddComponent/RemoveComponent，符合 FSM-01。

验证建议：0→1、1→2、2→1、1→0、祖先 presence、多字边界及未用高位清零。该 buffer 没有在 helper 内维护独立 revision，不应凭此声称事件/Wait 的边沿传播已经实现；生产通知闭环需另验。

### T09-04 Exactly-once contribution ledger 是目标缺口

**等级：P1 目标缺口；现行零 Tag profile 外。**

[TryApplyDelta 参数](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L63-L70) 只有 TagIndex/delta/counts/presence，[mutation record](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L22-L31) 也没有来源 Effect/Activation/Contributor 身份。计数防下溢不能检测“来源 A 重复撤销、恰好抵掉来源 B”的情况。例如 A、B 各 grant 同 tag，count=2；错误地撤销 A 两次仍合法减到0。目标 [Spec17 exact-once](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L321-L327) 与 [GE owned tag](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/05-ActiveEffectStoreSpec.md#L167-L171) 要求按贡献精确撤销。

这不是 count primitive 本身必须承担全部业务，而是完整 Tag 模块验收仍需 producer-owned ledger 及幂等 removal。当前 [Profile 零 Tag](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L83-L96) 不可触发该业务。修复方向：每次 grant 绑定稳定 contributor/generation，remove 验证并消费一次该记录，再调用 delta primitive；失败必须让 target shadow 不发布。

验证门：A/B 同 tag，重复 revoke A 返回显式重复/过期结果且 B 的 count/presence 保留；inhibit/reactivate/expiry/owner death 并发语义序下只撤销一次。

### T09-05 全量扫描与 cache 重建需要真实规模证据

**等级：P2 动态未验证，非已证性能违规。**

每次 delta [全 count 校验](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L175-L185) 加 [全 presence 重建](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Tag/GasTagTransactionUtility.cs#L222-L237) 为 O(TagCount)，不仅是 O(ancestor depth)。对低 TagCount 最简单、可验证；大量 modifier/tag toggle 时可能把 work 放大。依据 [FSM-06](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E7%8A%B6%E6%80%81%E6%9C%BA%E7%AD%96%E7%95%A5/FSM-06.md) 与 BUF-01，没有实测不能宣判必须换 bitset 或额外 Entity。

建议测量 TagCount、每 Tick delta 数、祖先深度、全扫描/写带宽、chunk dirty 及 slab high-water；如确有瓶颈，只局部更新受影响 ancestor 的 0/1 crossing bit，并保留 full rebuild 作一致性验证路径。不要为了省遍历引入第二 authority。

### T09-06 legacy GameplayTag 默认值异常是独立兼容风险

**等级：P2 静态确认的 API 边界缺陷候选；现行调用可达性未证明。**

[legacy struct fields/constructor](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Tag/GameplayTag.cs#L5-L17) 的构造函数把 null 数组变为空；但 C# `default(GameplayTag)` 不调用该构造函数。[HasTag/HasChildTag/HasParentTag](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Tag/GameplayTag.cs#L19-L72) 枚举数组，[IsRoot/HasChild](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Tag/GameplayTag.cs#L100-L101) 直接读 Length，默认值会在相应调用上抛 NullReferenceException。等值/哈希只按 Code，是另一份明确的 identity 语义，不应与 runtime dense TagIndex 混用。

影响：旧 authoring/反序列化/默认字段路径可能异常；不能证明现行 Runnable 已触发。建议 null-safe 查询或把有效实例构造约束显式化，并增加 default、null 输入、数组为空测试。Readonly 数组引用并不让元素 immutable；若作为配置快照，应由生成期复制/封印，避免运行时调用者改祖先数组。

ECS 判定：此 legacy 类型含 managed int[]，不能因为仍叫 GameplayTag 就当作 Burst-native 权威数据；本次没发现 V1 Tag helper使用它。保留为 authoring/兼容值类型本身不是违反 ECS，但迁移删除门必须核查真实引用。

## 验收结论

Tag 算术基础可保留。完整业务合规还缺 contribution 幂等撤销与 Tag-driven stabilization/Wait/fact 端到端证据；运行性能、Burst safety、变更通知未动态验证。不要用零 Tag demo 成功代替上述验收，也不要把目标缺口写成当前 profile 必然损坏。
