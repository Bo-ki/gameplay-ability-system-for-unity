# M04：Storage / 非压缩 slab 与 payload range 审计

审计基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`（2026-10-01 静态审计）。只通过 GitHub 读取固定提交；没有运行 Unity、测试、Burst、Player 或 Profiler，没有修改产品代码。下文“符合”仅指已检查静态性质，不代表模块端到端验收。

规范层级：[DOTS 依据库 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)明确区分官方机制、EX-GAS 项目规则与实测阈值。本报告中的 SYS/NAT/BUF/STORE 及目标不变量均为项目规则；DynamicBuffer 失效、cleanup、生存期等官方机制依据各主题 API 解读，不把项目选型冒充 Unity 强制要求。[目标态 README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/README.md)和[Spec17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/17-GAS%E4%B8%9A%E5%8A%A1%E9%93%BE%E8%B7%AF%E7%A0%B4%E5%9D%8F%E6%80%A7%E9%87%8D%E5%88%92%E5%88%86Spec.md)用于理想目标；受限交付范围不删除目标差距。

## 结论

本模块实现了非压缩的稳定索引、tombstone→free-list、显式generation溢出拒绝及payload exact-size重用，没有发现RemoveAt/SwapBack/compact。主要待闭环点是每次操作的全表验证成本、payload高水位碎片预算，以及“terminal交接完毕才回收”在领域调用者处的证明。这里的allocator是ECS buffer索引分配算法，不是另一个Native内存arena。

## 范围与实际检查

- 全文检查：[Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs#L1-L256)
- 全文检查：[Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L1-L657)
- 关联全文：Identity全部文件、Layout全部文件
- 未逐文件读取：Ability/GasAbilitySlabStorages.cs、Effect/GasGameplayEffectSlabStorage.cs及其他调用者；NonCompactingStorageTests未逐行读、未运行
- 已读规范：NativeContainer-Allocator _index/API/NAT-01～05；DynamicBuffer主题_index/API/BUF-01～04；Store主题_index/API/STORE-01～03；Spec13-01/03、Spec17稳定slab章节、ID-01～06


规范正文定位：[13-01-Entity清单与运行时布局Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/13-EntityComponent%E7%89%A9%E7%90%86%E5%B8%83%E5%B1%80/13-01-Entity%E6%B8%85%E5%8D%95%E4%B8%8E%E8%BF%90%E8%A1%8C%E6%97%B6%E5%B8%83%E5%B1%80Spec.md)；[13-03-Buffer容量与Phase映射Spec](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/13-EntityComponent%E7%89%A9%E7%90%86%E5%B8%83%E5%B1%80/13-03-Buffer%E5%AE%B9%E9%87%8F%E4%B8%8EPhase%E6%98%A0%E5%B0%84Spec.md)。其中无独立规则编号的章节以Spec编号和节号作为审计标识，不另造官方规则。

## 规范逐项矩阵

| 规则（EX-GAS） | 判定 | 代码证据与理由 |
|---|---|---|
| [ID-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L55-L55) | 符合（算法） | [GasNonCompactingSlabAllocator.cs:90–113](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs#L90-L113)只复用free-head或尾部Append；[GasNonCompactingSlabAllocator.cs:119–168](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs#L119-L168)tombstone/free-list转换不搬动邻居 |
| [ID-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L56-L56) | 符合 | [GasNonCompactingSlabAllocator.cs:157–168](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs#L157-L168)uint.MaxValue写前拒绝；[GasPayloadRangeAllocator.cs:300–315](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L300-L315)同样拒绝回绕。代际在回收到free时递增，后续reuse使用新代际，不是每次读取递增 |
| [ID-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L53-L53) / [ID-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L54-L54) | 符合（payload路由） | [GasPayloadRangeAllocator.cs:232–257](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L232-L257)先校验身份再读取store；[GasPayloadRangeAllocator.cs:533–574](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L533-L574)Kind/Owner/range/live/generation逐层检查 |
| Spec13-03 §4 / [ID-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L55-L55) | 符合（live range不移动） | [GasPayloadRangeAllocator.cs:408–465](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L408-L465)仅同kind同长度free range重用，保留Offset；[GasPayloadRangeAllocator.cs:471–503](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L471-L503)否则高水位尾追加，容量不足显式返回 |
| [ID-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/90-%E7%9B%AE%E6%A0%87%E6%80%81%E4%B8%8D%E5%8F%98%E9%87%8F.md#L57-L57) | 调用者前置条件；未验证 | [GasNonCompactingSlabAllocator.cs:144–168](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs#L144-L168)只检查Tombstone与generation，并不接收outbox/子引用receipt；payload也如此。不能据此证明业务提前回收，也不能宣称交接门已由allocator保障 |
| [STORE-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Store%E9%80%89%E5%9E%8B-%E6%95%B0%E6%8D%AE%E6%89%BF%E8%BD%BD%E7%AD%96%E7%95%A5/STORE-01.md#L1) / [STORE-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Store%E9%80%89%E5%9E%8B-%E6%95%B0%E6%8D%AE%E6%89%BF%E8%BD%BD%E7%AD%96%E7%95%A5/STORE-03.md#L1) | 符合（局部owner契约） | [GasPayloadRangeAllocator.cs:9–52](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L9-L52)record/value同ASC owner、Epoch与kind，明确持久word store；[GasNonCompactingSlabAllocator.cs:46–65](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs#L46-L65)caller-owned storage仅允许稳定索引写和追加 |
| [NAT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-01.md#L1) / [NAT-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-04.md#L1) | 独立Native分配不适用 | 两文件无Allocator.Persistent/TempJob/new NativeContainer；底层DynamicBuffer由ECS管理。struct adapter内的DynamicBuffer仅当前mutation lane使用，禁止调用者跨tick保存 |
| [NAT-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-05.md#L1) / [BUF-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/DynamicBuffer-Chunk-Archetype/BUF-01.md#L1) | 可观测性及性能未验证 | 高水位/容量作为输入或元数据存在；没有本模块内分配bytes/扩容次数profile采集。是否由外部Diagnostics补足尚未检查 |

## 风险与偏离

### M04-P2-01：每次分配/回收全表审计，规模成本需验证（非已测性能缺陷）

TryAllocate、TryMarkTombstone、TryRecycleTombstone都先调用ValidateMetadata；slab验证扫描全high-water及free链（[GasNonCompactingSlabAllocator.cs:174–220](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasNonCompactingSlabAllocator.cs#L174-L220)）。payload验证扫描全record并走free链，再按Offset扫描或搜索exact free（[GasPayloadRangeAllocator.cs:348–392](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L348-L392)、[GasPayloadRangeAllocator.cs:509–527](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Storage/GasPayloadRangeAllocator.cs#L509-L527)）。若高水位N下执行K次操作，验证工作可能达到O(K×N)，即使live量已经很小。触发：长战局高水位和密集分配/回收。影响：可能抵消free-list常数时间收益。先采Profile，再考虑由admission一次验证后使用受约束token；不可为性能直接删除安全检查。本次没有断言超过任何毫秒门槛。

### M04-P2-02：exact-size重用的容量碎片合同需明确

已释放但长度/kind不同的range不会合并、拆分或复用，ValueHighWater只增加。触发：变动payload长度或kind组合的长生命周期负载，即使大量总free bytes存在仍可能PayloadValueCapacityExceeded。算法满足live range不移动要求，不能为了压缩随意搬动live range；但ScaleProfile/CapacityProof必须覆盖这一实际分配策略，而非只计算同时live总字数。该拒绝是显式fail-closed，非静默覆盖或已证实泄漏。

### 元数据注意点（不计已证实缺陷）

payload复用了GasSlabHead，但只使用Records.HighWater/FreeHeadIndex：其回收未维护Records.FreeCount，验证也不检查该字段。当前算法通过扫描统计freeCount所以并未据此错误分配。后续Diagnostics/Proof若开始读取该通用字段，必须先明确payload计数合同，不能默认等同普通slab allocator的FreeCount。

## API / ECS 适用性说明

| 维度 | 本模块答案 |
|---|---|
| Tick | 跨tick保存稳定值handle与record；adapter不得跨tick持有具体DynamicBuffer |
| owner | ASC-local caller拥有records/value buffer，算法不建立全局mirror |
| 数据/API | generic struct storage adapter；按稳定索引读写；非System、不调度job |
| allocator | 逻辑槽分配，底层ECS buffer lifetime；不自行Rewind/DisposeNativeContainer |
| 结构变化 | Append/写槽不改变组件集合；容量增长与句柄稳定分离；没有ECB职责 |
| drain | tombstone与最终free分步；terminal/outbox交接由业务caller证明 |
| ScaleProfile | hardRangeRecordCapacity/hardPayloadValueCapacity显式；需覆盖high-water、碎片及全表验证成本 |

## 历史状态与当前裁决

[D0-M2R 路线接受裁决](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D0-M2R-SourceGenerator%E8%B7%AF%E7%BA%BF%E6%8E%A5%E5%8F%97%E4%B8%8ED1%E6%8E%88%E6%9D%83%E8%A3%81%E5%86%B3.md#L11-L28)已明确取代“生产路线为空”的历史状态；[D1 最终验收结果](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1.1-D1-SourceGenerator%E7%94%9F%E4%BA%A7%E8%BF%81%E7%A7%BB%E4%B8%8E%E5%8F%AF%E8%BF%90%E8%A1%8C%E9%AA%8C%E6%94%B6%E7%BB%93%E6%9E%9C.md#L9-L21)记录 SourceGenerator 迁移及受限闭世界可运行基线通过，同时保留 ProductionInstallAdmission=NotEvaluated、FullSemanticEligibility=false。这里只引用仓库已有结果，没有重新复验其 raw evidence；不将 Pending 自动判为缺陷，也不将该 Passed 外推为全部目标不变量通过。

## 后续验证（本次未执行）

1. allocate→tombstone→recycle→reuse：旧handle拒绝、邻居稳定，uint.MaxValue失败时head/record字节不变
2. 构造断链、环、越界NextFreeIndex、零generation、错误state、重叠/缺口range，验证fail-closed且不部分写
3. 随机不同kind/length序列对照纯值参考模型；容量N成功、N+1无mutation，极端int边界无回绕
4. 追踪Ability/Effect/Continuation的terminal fact、child与pending引用到recycle，确认ID-05真实交接顺序
5. 长局不同high-water/live比值下记录操作次数、metadata扫描次数、实际bytes及耗时；按allocator真实exact-size策略建立容量证明
