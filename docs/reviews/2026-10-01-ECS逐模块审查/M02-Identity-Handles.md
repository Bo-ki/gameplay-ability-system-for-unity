# M02：Identity / 强类型句柄审计

审计基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`（2026-10-01 静态审计）。只通过 GitHub 读取固定提交；没有运行 Unity、测试、Burst、Player 或 Profiler，没有修改产品代码。下文“符合”仅指已检查静态性质，不代表模块端到端验收。

规范层级：[DOTS 依据库 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)明确区分官方机制、EX-GAS 项目规则与实测阈值。本报告中的 SYS/NAT/BUF/STORE 及目标不变量均为项目规则；DynamicBuffer 失效、cleanup、生存期等官方机制依据各主题 API 解读，不把项目选型冒充 Unity 强制要求。[目标态 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/README.md)和[Spec17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md)用于理想目标；受限交付范围不删除目标差距。

## 结论

四个 Identity 文件在纯值身份层符合 epoch、owner generation、slot generation、kind 分离的目标；未发现可以在本模块直接证明的身份绕过。IsValid 仅表示形状有效，不能替代读取实际 slab 后的 liveness 校验；把“有字段”当作所有调用点都正确是错误结论。回收与消费链的完整正确性仍需跨模块验证。

## 范围与实际检查

- 全文检查：[Assets/GAS/Runtime/V1/Identity/HandleValidation.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/HandleValidation.cs#L1-L137)
- 全文检查：[Assets/GAS/Runtime/V1/Identity/OwnerIdentityHandles.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/OwnerIdentityHandles.cs#L1-L137)
- 全文检查：[Assets/GAS/Runtime/V1/Identity/PayloadRangeHandle.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/PayloadRangeHandle.cs#L1-L125)
- 全文检查：[Assets/GAS/Runtime/V1/Identity/StableSlotHandles.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/StableSlotHandles.cs#L1-L564)
- 关联全文检查：Storage/GasNonCompactingSlabAllocator.cs、Storage/GasPayloadRangeAllocator.cs、Layout/GasAscLayout.cs、Layout/GasSessionLayout.cs
- 未逐文件读取：所有 Ability/Effect/Boundary handle 消费者、IdentityTests；本报告不声称全仓库每次索引前均调用 validator
- 已读规范：Spec17 的 ASC identity 与稳定 slab；Spec13-01/03；90 不变量 ID-01～09；Store主题 _index/API/STORE-01～03；DOTS 总 README


规范正文定位：[13-01-Entity清单与运行时布局Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/13-EntityComponent%E7%89%A9%E7%90%86%E5%B8%83%E5%B1%80/13-01-Entity%E6%B8%85%E5%8D%95%E4%B8%8E%E8%BF%90%E8%A1%8C%E6%97%B6%E5%B8%83%E5%B1%80Spec.md)；[13-03-Buffer容量与Phase映射Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/13-EntityComponent%E7%89%A9%E7%90%86%E5%B8%83%E5%B1%80/13-03-Buffer%E5%AE%B9%E9%87%8F%E4%B8%8EPhase%E6%98%A0%E5%B0%84Spec.md)。其中无独立规则编号的章节以Spec编号和节号作为审计标识，不另造官方规则。

## 规范逐项矩阵

| 规则（EX-GAS） | 判定 | 代码证据与边界 |
|---|---|---|
| [ID-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L53-L53) | 符合（身份载体） | [OwnerIdentityHandles.cs:8–22](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/OwnerIdentityHandles.cs#L8-L22)ASC stable id + ASC generation；[StableSlotHandles.cs:23–48](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/StableSlotHandles.cs#L23-L48)长期 slot 再带 Epoch、Index、SlotGeneration |
| [ID-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L54-L54) | 符合 | [StableSlotHandles.cs:8–17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/StableSlotHandles.cs#L8-L17)闭集 HandleKind，六种不同 readonly struct；[HandleValidation.cs:54–84](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/HandleValidation.cs#L54-L84)Kind 先于访问相关条件验证 |
| [ID-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L53-L53) / [ID-08](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L60-L60) | 符合（值域及拒绝顺序） | [HandleValidation.cs:63–84](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/HandleValidation.cs#L63-L84)按 Epoch→Kind→Owner→Index→Live→Generation 返回首因；不查询 Entity/World。未知 enum 不通过 [HandleValidation.cs:90–94](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/HandleValidation.cs#L90-L94) |
| [ID-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L53-L53) / Spec13-03 range identity | 符合（字段） | [PayloadRangeHandle.cs:37–71](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/PayloadRangeHandle.cs#L37-L71)保存 Epoch/Owner/Offset/Length/Generation/Kind，IsValid 拒绝零代际、负 Offset、空 range 与未知 PayloadKind；范围实存校验在 M04 |
| [ARC-07](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L15-L15) / [ID-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L53-L53) | 符合（Battle 值身份） | [OwnerIdentityHandles.cs:76–93](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/OwnerIdentityHandles.cs#L76-L93)含 Epoch、BattleStableId、BattleGeneration；[HandleValidation.cs:116–134](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/HandleValidation.cs#L116-L134)逐项拒绝零值或不匹配 |
| [ID-08](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L60-L60) | 符合（本目录公开载体） | 四个文件公开 handle/diagnostic carrier 只有数值字段，不携 raw Entity；[HandleValidation.cs:20–43](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Identity/HandleValidation.cs#L20-L43)无 ECS 依赖。不能外推为整个 Session public API 已零 World |
| [ID-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L55-L55) / [ID-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L56-L56) / [ID-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L57-L57) | Identity 本体不适用；Storage 局部符合；交接未验证 | handle 不拥有分配/回收，[GasNonCompactingSlabAllocator.cs:144–168](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs#L144-L168)只从 tombstone 回收并在 uint.MaxValue 拒绝；是否等待 terminal fact/队列交接由领域 owner 负责 |

## 符合证据与注意点

- 六种 slot handle 的 Equals、GetHashCode、==/!=均覆盖本类型全部身份字段；跨 Kind 由 C# 类型及 diagnostic carrier kind 校验隔离
- OwnerAscHandle 不单独带 Epoch 是 Spec13 明确的嵌套设计，不能误报缺失 Session 隔离；跨 Session 的 slot/Battle/payload 已各自带 Epoch
- Battle validator 不查 Live 状态是函数职责限制：它验证生命周期身份，调用者仍必须验证 Battle slot 状态
- PayloadRangeHandle.IsValid 不证明 Offset+Length 落在实际 store 中；Storage 使用减法边界检查来避免加法溢出。不要把纯值检查当授权 token

## 偏离与优先级

本目录未发现已证实 P1/P2 身份算法缺陷。保留 P2 验证门：必须追踪所有消费者确认在索引、mutation、迟到 completion 前执行真实 generation/liveness 验证；这是未验证项，不是已经命中的违规。ID-05 的“交接后回收”不是 handle 类型能独立满足的约束。

## API / ECS 适用性说明

| 维度 | 本模块答案 |
|---|---|
| Tick | 不推进 Tick；Epoch/代际可跨 Tick 保存，slot liveness 需每次解析验证 |
| owner | 值类型不拥有资源；OwnerAscHandle/BattleInstanceHandle明确逻辑归属 |
| 数据/API | readonly struct + pure validator；不使用 SystemAPI.Query/Lookup/EntityManager，属有意不适用 |
| allocator | 无 NativeContainer/Blob 分配，无 Dispose 职责 |
| 结构变化 | 不创建/销毁 Entity，不负责 ECB；非压缩性见 M04 |
| drain | 不消费 outbox；交接前不得回收是领域 owner 义务 |
| ScaleProfile | 不规定容量；极限 uint generation 与非法 enum 可纯值测试，运行规模和 stale reject 计数由消费者采集 |

## 历史状态与当前裁决

[D0-M2R 路线接受裁决](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D0-M2R-SourceGenerator%E8%B7%AF%E7%BA%BF%E6%8E%A5%E5%8F%97%E4%B8%8ED1%E6%8E%88%E6%9D%83%E8%A3%81%E5%86%B3.md#L11-L28)已明确取代“生产路线为空”的历史状态；[D1 最终验收结果](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D1-SourceGenerator%E7%94%9F%E4%BA%A7%E8%BF%81%E7%A7%BB%E4%B8%8E%E5%8F%AF%E8%BF%90%E8%A1%8C%E9%AA%8C%E6%94%B6%E7%BB%93%E6%9E%9C.md#L9-L21)记录 SourceGenerator 迁移及受限闭世界可运行基线通过，同时保留 ProductionInstallAdmission=NotEvaluated、FullSemanticEligibility=false。这里只引用仓库已有结果，没有重新复验其 raw evidence；不将 Pending 自动判为缺陷，也不将该 Passed 外推为全部目标不变量通过。

## 后续验证（本次未执行）

1. 六类 handle 逐字段变异：零值、错 Epoch/Kind/Owner/Index/Live/Generation，验证首因顺序
2. default、越界 enum、int.MaxValue Offset/Length、uint.MaxValue generation；验证与实际 store 的边界一致
3. destroy/recreate ASC、slot recycle、payload exact-size reuse 后，旧 handle 全拒绝而邻居 live handle 不变化
4. 从 Boundary command、continuation wake、GE remove、Cue delayed callback 各选完整调用链，证明 validator 在实际读取前执行；回收必须有 terminal/queue handoff 证据
