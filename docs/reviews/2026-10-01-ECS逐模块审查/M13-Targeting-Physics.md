# M13 — Targeting / Physics 目标路径规范审查

基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。GitHub只读静态审查，无Unity运行、真实Physics query、碰撞场景或性能测量。PHY编号是EX-GAS项目规则；Physics broadphase/事件有效期为其依托的Unity Physics机制，二者分开陈述。

## 1. 职责与阅读范围

TargetCatcher目录现行职责是**authoring参数描述和类型映射**，不是运行时Physics World查询器。V1目标路径在Kernel TargetResolve中把Boundary稳定身份解析为planned target/application，然后在target writer校验当前context并应用。

已完整读5个TargetCatcher文件：CatchAreaBox3D、CatchSelf、CatchTarget、TargetCatcherBase、TargetCatcherHelper。已读GasBoundaryCommandProtocol spatial/target字段与校验、SessionIngressGate目标成员/支持面校验、GasRuntimeV1SupportProfile目标组合；GasTickJobs仅抽查TargetResolveExpand 1227–1770、GroupWorkByTarget 4592–4623、TargetPrepare application 4831–4902与context 5389–5448。整个9,123行文件未全审。TargetCatcherHelper的其他Editor调用者未穷尽；不能把本模块未实现物理adapter扩大为“全仓没有任何Physics集成”。

依据已读：官方参考README，UnityPhysics/NativeContainer/Query/数据流主题_index和API，PHY01–05、NAT01/02、QRY04、PRF13/19；目标README，Spec17 identity/目标writer，Spec18 Physics/事务，以及Spec03D目标/排序/失败模型。

## 2. 逐规则矩阵

| 规则 | 判定 | 源码证据 | 解释 |
|---|---|---|---|
| [PHY-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/UnityPhysics/PHY-01.md#L10-L24) | 受限符合 | [GasFixedTickSystems.cs:9–24](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasFixedTickSystems.cs#L9-L24)；[CatchAreaBox3D.cs:5–17](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Ability/TargetCatcher/CatchAreaBox3D.cs#L5-L17) | GAS组在Physics后；authoring类不修改physics实体布局。并未执行真实physics pipeline以证明完整集成安全 |
| [PHY-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/UnityPhysics/PHY-02.md#L10-L26) | 符合分层；**目标adapter差距 P1** | [TargetCatcherBase.cs:5–24](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Ability/TargetCatcher/TargetCatcherBase.cs#L5-L24)；[TargetCatcherHelper.cs:7–36](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Ability/TargetCatcher/TargetCatcherHelper.cs#L7-L36)；[GasTickJobs.cs:1230–1245](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L1230-L1245) | helper不解析World/Entity/命中，不直接写Attribute/Tag；但该目录没有将真实命中变成stable TargetData的执行体 |
| [PHY-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/UnityPhysics/PHY-03.md#L10-L22)；[NAT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-01.md#L10-L20) | 不适用（当前捕获文件）/受限符合（值载体） | [GasBoundaryCommandProtocol.cs:50–80](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryCommandProtocol.cs#L50-L80)；[CatchTarget.cs:3–8](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Ability/TargetCatcher/CatchTarget.cs#L3-L8) | 已读路径没有collision/trigger iterator或simulation stream；空间数据为有限值副本。不能据此声称未来事件adapter生命周期已验收 |
| [PHY-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/UnityPhysics/PHY-04.md#L10-L26) | 当前query不适用；目标差距 | [CatchAreaBox3D.cs:5–31](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Ability/TargetCatcher/CatchAreaBox3D.cs#L5-L31)；[GasRuntimeV1SupportProfile.cs:493–500](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Definition/GasRuntimeV1SupportProfile.cs#L493-L500) | Box3D只保存offset/size/rotation/layer；Runnable只允许Spatial.None。不存在实际query调用点可验证broadphase时效，不能以“不用Physics”豁免未来adapter规范 |
| [PHY-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/UnityPhysics/PHY-05.md#L10-L20) | 待实测/目标证据缺口 | [GasRuntimeDataOrientedScorecard.cs:60–74](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Debugger/GasRuntimeDataOrientedScorecard.cs#L60-L74)；[GasFixedTickSystems.cs:126–154](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasFixedTickSystems.cs#L126-L154) | 当前scorecard未独立提供physicsStepMs/stepCount策略；group order不能代替成本分组证据 |
| [Spec03D:121–137](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03D-CommandResolve%E4%B8%8ETargetResolveSpec.md#L121-L137) | 符合（四维表示） | [GasBoundaryCommandProtocol.cs:208–233](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/GasBoundaryCommandProtocol.cs#L208-L233)；[GasTickJobs.cs:1665–1751](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L1665-L1751) | ASC/Avatar generation/Spatial/Life分开，冻结Avatar精确匹配；空间variant与descriptor核验，无不合法空间自动转self |
| [Spec03D:132–137](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03D-CommandResolve%E4%B8%8ETargetResolveSpec.md#L132-L137) | 符合（显式Self分支） | [GasTickJobs.cs:1475–1535](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L1475-L1535) | 仅Definition Self进入self解析；显式远端目标不匹配则None/reject。缺目标不通用fallback self |
| [Spec03D:93–107](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03D-CommandResolve%E4%B8%8ETargetResolveSpec.md#L93-L107)；[Spec03D:171–175](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/03-RuntimeCore%E7%AE%A1%E7%BA%BF/03D-CommandResolve%E4%B8%8ETargetResolveSpec.md#L171-L175) | **部分符合/结果分类偏差 P2** | [GasTickJobs.cs:1592–1645](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L1592-L1645)；[GasTickJobs.cs:1300–1320](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L1300-L1320)；[GasTickJobs.cs:4841–4902](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L4841-L4902) | application阶段重读life是优点；tick-start dead却在resolve提前变成通用StaleBinding，丢失独立TargetLife typed reason |
| [NAT-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/NativeContainer-Allocator/NAT-02.md#L10-L20)；[PRF-13](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-13.md#L10-L24) | 受限符合；目标总序待补 | [GasTickJobs.cs:4592–4623](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L4592-L4623) | 以stable target/application/operation排序，不依赖worker；目前单目标node白名单，未覆盖Spec03D完整多目标SemanticPhase/WorkClass canonical tuple |
| [QRY-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Query-Job-%E9%81%8D%E5%8E%86/QRY-04.md#L10-L22)；[PRF-19](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/PRF-19.md#L10-L24) | 待实测；single-writer静态符合 | [GasTickJobs.cs:1620–1645](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L1620-L1645)；[GasTickJobs.cs:4735–4775](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs#L4735-L4775) | registry线性resolve和target分组处理避免source跨owner直接写；单IJob并非跨target并行目标的替代，详见M10 |

## 3. 六项实质检查

### M13-01：AreaBox已退为authoring，实际Physics adapter是目标缺口（P1能力差距）

CatchAreaBox3D没有运行时方法，保存isWorldSpace/offset/size/rotation/LayerMask供生成期使用。TargetCatcherHelper只注册/创建authoring对象。优点是清除了旧式静态World解析与managed catcher直接改GAS状态的入口。

不能因此说Box目标已经支持。当前生产Profile只接受FrozenAsc+RequireSameAvatar+Spatial.None+AliveOnly，ApplyEffect非ASC target在Gate被拒。[SessionIngressGate.cs:449–455](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/SessionIngressGate.cs#L449-L455)。

建议：建立独立Physics adapter，明确从PhysicsWorldSingleton/collision-world取得候选，复制stable ASC token与命中值后通过正式输入路径提交；Core仍拥有life/tag/requirement判定。必须随配置bake拒绝不支持area/spatial组合，不能仅凭Inspector有字段宣传可运行能力。

验收：box旋转/世界局部空间、layer filter、重复body命中同ASC、无效binding、多目标排序；候选作为权威输入记录或与确定性索引等价，headless回放不重新猜Physics。

### M13-02：broadphase与事件寿命不是“PostPhysics就天然最新”（P1未来开放门）

当前group在Physics之后，不自动意味着collision world已同步到模拟后位置；PHY04要求写明相对Initialize/Simulation排序、SynchronizeCollisionWorld选择和查询窗口。已读TargetCatcher没有Overlap/Cast调用，故**不认定现行query违反时效**，而是没有可验收adapter。

建议每个未来query调用点有BroadphasePolicy；事件仅在有效simulation窗口内复制值，不跨step持有iterator/stream。验收一个render frame多fixed steps、快速运动body、同步开关两种配置；记录候选对应哪个sample tick。

### M13-03：逻辑身份/Avatar/空间分离和显式Self值得保留

BoundaryTargetRef携带Epoch、Battle、ASC generation、Avatar generation和固定值spatial snapshot；finite check拒绝NaN/Infinity。Kernel校验目标同Battle、registry Ready、身份epoch与binding。FollowAsc规范要求不夹带冻结Avatar；RequireSameAvatar要求完整pair。

优点：不携带raw Entity给外部；目标失效不悄悄转self；显式self空间数据缺失时拒绝。建议新增或保留half binding、过期generation、epoch错、不同Battle、冻结shape variant错的负向向量；不要为了“更易用”增加fallback self。

### M13-04：tick-start死亡被误归因stale binding（P2源码结果分类缺陷）

触发条件：多单位Battle仍Running，目标ASC registry仍Ready且identity/binding有效，但AscLifecycle已Dead；下一条AliveOnly ApplyEffect指向该target。Gate的ValidateAscTarget只检查registry成员，不检查Life，因此可进入Kernel。[SessionIngressGate.cs:603–619](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/SessionIngressGate.cs#L603-L619)。

TryResolveTarget先算targetAlive，再以MatchesLifePolicy返回false；调用方统一记录RejectedStaleBinding/InvalidIdentity。真正的life拒绝没有到达target transaction的RejectedTargetLife路径。对同Tick先活后死的后续application，writer会重读shadow life，反而能给正确分类，导致两种死亡时点的reason不一致。

影响：诊断/replay/API消费者把业务生命策略拒绝误认为身份失效；不等于错误伤害已落地。建议resolve只验证identity/context并保留life snapshot，或返回分类型resolve outcome；最终life仍在逐application线性化点判断。验收tick-start dead与same-tick death crossing两组都保留ApplicationId，给typed TargetLife rejection，其他targets正常继续。

### M13-05：多目标与完整canonical tuple未被当前白名单证明（P1目标差距）

当前排序采用TargetAscStableId/Generation、ApplicationId、OperationOrdinal。它是显式稳定键，优于依赖Physics命中数组或worker完成次序；但Spec03D要求TargetIndex、ProgramNodeOrdinal、SemanticPhase/WorkClass、source sequence等全序且冲突fault。Profile每nodeMaximumTargetCount=1/MaximumOutputCount=1只证明受限场景，不能替代多目标隔离验收。

建议按目标Spec建立正式canonical work key并检测同键非等价work；source spec可共享但target capture/requirements/stack结果必须独立。验收同候选集合不同物理排列、同ASC多个collider、重复key、不同worker与0/1/N batch，得到相同outcome/hash且无跨目标capture串用。

### M13-06：registry scan成本与target并行度应分开判断（P2待测；并行差距见M10）

每目标resolve线性扫registry，Prepare又按target分组并查shadow。源码支持“潜在线性/重复扫描成本”判断，不支持“必定慢”或“必须换hashmap”的结论。QRY04要求先测lookup/locality和替代复制成本。

建议记录resolved candidates、registry scan steps、target bucket长度/偏斜、Physics query count、Core resolve/apply cost。不同target并行时必须独占shadow/output slice，不能为跨ASC写关闭安全限制。验收均匀target与hot-target分别采样，Physics和Core成本分开，明确disabled/not-collected状态。

## 4. 不适用与未验证边界

- 当前authoring类型里的UnityEngine.Vector3/LayerMask、Activator与Dictionary不在已证Core热路径，不能仅凭managed API认定DOTS违规
- 当前没有Physics event引用，PHY03在这些文件不适用；未来adapter仍必须满足
- Runtime spatial None只是当前支持面，不是完整目标态的永久非目标
- 未执行物理一致性、broadphase同步、多目标随机排列、Hash/Replay或性能测试；本报告不宣称Physics↔Headless确定性已通过

