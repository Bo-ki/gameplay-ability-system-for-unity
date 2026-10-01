# M08 Attribute 模块 ECS 标准审查

## 结论与边界

审查日期：2026-10-01。

审查基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。静态阅读，无代码修改、无 Unity/Burst/Player 执行，不声称测试通过。

Attribute 的固定 dense 布局、有限数值拒绝、clamp、revision 与显式 dirty 位更新有完整局部实现。它目前是一个 delta mutation primitive，不能据此认定完整 Aggregator 已达目标态。现有 Runnable 仅接受 Health/Energy/Attack 三属性和 Health Add modifier；这能界定当前可达故障面，不能删掉持久 contribution、channel、qualifier、撤销与 Live 依赖的目标要求。

## 文件覆盖

本模块全部第一方 C# 文件已静态阅读全文：
- [Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs)，1–377 行

交叉证据已读：
- [Effect transaction 全文](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Effect/GasGameplayEffectTransaction.cs)
- [SupportProfile 支持面](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L83-L166)、[Health Add 限定](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L374-L386)
- [03E-03 Attribute 完整 Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md)
- [Spec17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L304-L327)

本模块未读第一方 C#：无。Layout、Kernel、Definition builder、测试程序集由相邻模块负责；本报告不以 helper 的局部正确性替代其调用图、writer 隔离或事实发布证明。PackageCache 原文件哈希、实机 profile 未在本次验证。

## 标准矩阵

依据库明确区分官方机制与项目规则，见 [依据库 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md#L5-L13)。下表规则均为 EX-GAS 项目规则/项目推导，并非 Unity 强制同一种架构。

| 标准及类型 | 本模块适用点 | 裁决 |
|---|---|---|
| [BUF-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/BUF-01.md) 项目规则；官方机制：[DynamicBuffer API 解读](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/API%E4%B8%8EEX-GAS%E8%A7%A3%E8%AF%BB.md) | 固定逻辑长度与物理 IBC/外部化是两件事 | 固定逻辑长度符合；物理性能动态未验证 |
| [BUF-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/BUF-02.md)、[PRF-19](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-19.md) 项目规则 | target single writer，不能共享无同步并行写 | helper 不做 random lookup，局部符合；调度由 Kernel 证明 |
| [PRF-13](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-13.md) 项目规则 | 浮点 reduce/order、mutation provenance | 局部顺序明确；完整 aggregator 有目标缺口 |
| [NAT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-01.md) 项目规则；[Allocator API](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/API%E4%B8%8EEX-GAS%E8%A7%A3%E8%AF%BB.md) | 不持有跨 Tick scratch | helper 无自有 native allocation，无跨 Tick 引用字段 |
| [03E-03 固定布局与 dirty](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md#L3-L19) 项目目标契约 | dense index、revision、dirty words | 符合 |
| [03E-03 Aggregator](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md#L21-L40)、[Spec17 Aggregator](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md#L304-L311) 项目目标契约 | contribution/channel/qualifier/hook | 目标缺口，不能用 Runnable 限制替代完成证明 |

## 逐项结果

### A08-01 固定布局和索引写入符合

代码在 [ValidateShapeAndEntry](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs#L316-L333) 要求 Attribute buffer 长度等于 Catalog layout，dirty buffer 等于 ceil(AttributeCount/64)，拒绝越界；[最终写回](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs#L202-L222) 仅 overwrite 当前 index，不 resize、RemoveAt 或创建 per-attribute Entity。符合 [固定布局契约](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md#L5-L19)。

验证建议：0/1/63/64/65 属性边界、错误 layout 长度、负 index、末尾 index、错误 dirty 长度；拒绝时两个 buffer 字节不变。

### A08-02 非有限数、clamp 和 revision 的失败原子性符合

[TryPrepareMutation](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs#L228-L289) 在写前验证输入与既有 Base/Current、加法结果有限，独立对 Base/Current clamp。实际值不变则不递增 revision、不置 dirty；变化且 revision 为 uint.MaxValue 时拒绝。[TrySimulateDelta](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs#L96-L138) 为复合事务提供同口径 shadow 预演。

触发/影响：NaN、Infinity、float 溢出、revision 回绕不会被本 helper 静默写进权威。动态验证仍应覆盖 clamp 全部挡住 delta、只改 Base、只改 Current、禁用某侧 clamp、min>max 与无效开关。

### A08-03 Mutation record 保留 requested 与 unclamped 证据符合

[record](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs#L22-L36) 与 [赋值](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs#L239-L288) 保留 requested delta、前值、unclamped、applied、前后 revision。它足以供调用方计算 effective delta，而不是由 clamped=0 反推 overkill。符合 [AttributeMutationFact 契约](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md#L55-L82) 的数值证据需求。

范围限制：record 本身没有 Application/Contributor/Causality/DeathTransition 身份；这些是外层 transaction/fact 责任。本报告没有把“record 已有”误写成“每次 mutation 的完整 fact 已验证”。

验证建议：100 生命受 -150 时 requested=-150、unclamped=-50、applied=0、effective=-100；检查外层事实准确关联唯一 killer，致死 application 后续节点不能覆盖首次 crossing。

### A08-04 持久 Aggregator 与可逆 contribution 尚未闭合

**等级：P1 目标缺口；现行 profile 外。**

[Effect modifier 写法](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Effect/GasGameplayEffectTransaction.cs#L558-L609) 直接逐 modifier 调用 delta helper；[Add/Multiply/Divide/Override](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Effect/GasGameplayEffectTransaction.cs#L821-L855) 同时修改 Base 与 Current。Attribute 模块没有 contributor ledger、channel/qualifier 归并、override tie-breaker、撤销重算或 Pre/Post/meta hook。不能把这些四个算术 opcode 等同于 [Aggregator 契约](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md#L21-L40)。

触发：开放非周期 Duration/Infinite buff，要求 remove/inhibit 后恢复；或开放多个 channel/Override/qualifier。影响：逐次修改最终值不能提供精确 contributor 撤销及稳定重算语义。当前 [Health Add profile](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L377-L386) 排除了此类输入，因此不是已经证明可在现有四 Ability 场景复现的故障。

修复方向：target-owned contribution ledger + 稳定 channel/order key dirty recompute；Instant/Periodic Base mutation 后统一 recompute Current；在能力闭合前维持 bake reject，不能只放宽 profile。

验证门：Add/Multiply/Divide/Override 混合、qualified override 选择、双来源 buff 移除一个、inhibit/reactivate、不同输入物理顺序同 hash、clamp/hook/meta conversion。

### A08-05 无 dirty overload 是受控调用契约风险

**等级：P2 待调用链验证，不列为已复现缺陷。**

[无 dirty 校验与写入 overload](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs#L143-L186) 可改变 revision 而不写 dirty。带 dirty 入口正常；内部测试/shadow 或明确由外层统一标脏时可以合法使用。目标 [精确 dirty](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md#L17-L19) 不允许权威更新后漏掉依赖通知。

触发：未来生产调用误选无 dirty overload。影响：数值已变，但 aggregator/Live/fact finalize 若依赖 dirty 会漏处理。建议把名称或可见性显式区分 authority 与 shadow，审查全部生产 callsite；验证所有 mutation → dirty/revision → finalize 的闭环。本模块单独不能证明漏脏已发生。

### A08-06 性能与调度只能动态验收

**等级：P2 动态未验证。**

helper 无 EntityManager/ECB/查询、无 native allocation；[dirty 位更新](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Attribute/GasAttributeTransactionUtility.cs#L350-L357) 为常数索引写入。这个局部设计支持 owner-local 和细粒度 dirty，但不能证明 IBC、chunk packing、写依赖或整个 Tick 零分配。官方 [Buffer 机制](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/API%E4%B8%8EEX-GAS%E8%A7%A3%E8%AF%BB.md) 没有要求所有 buffer 固定某一 IBC。

验证门：代表性 AttributeCount×ASCCount 的 Length/Capacity、chunk capacity、external bytes、dirty scan 成本，Safety Checks/Burst 下 target writers 非重叠、不同 worker/hash 一致。没有平台和数据之前不设毫秒/倍率门槛。

## 收口

当前局部符合项可保留；完整 Attribute 目标不能标记完成。优先实现并证明 A08-04，再开放相关 Definition。A08-05 是接口误用防线，A08-06 是未提供的运行证据，二者不得伪装成现行程序已失败。
