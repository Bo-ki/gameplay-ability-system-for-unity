# M20 Presentation / Cue / Resources

## 基线、职责与覆盖

固定提交 `61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。只读静态审查，未运行Unity/Player或资源加载。Cue是托管表现对象，GameObject/AudioSource/反射/字符串在这个边界允许使用；不能将Core的unmanaged要求一概套用到Cue。裁决使用符合、受限符合、目标gap、待实测、N/A五档；这些是项目规则，不是Unity全局要求。

重点已读：Runtime/Cue/Base/GameplayCueBase.cs、Common/CueMountPrefab.cs、CuePlaySound.cs、CuePlayAnimator.cs、CueLog.cs、CueLogging.cs、CueConfig.cs；Runtime/General/Helper/CueHelper.cs；General/GASResourceLoader.cs；AutoChess/Presentation/AutoChessPresentationOutboxBridge.cs。AutoChessDemoSceneRunner.cs仅抽样UI与结果展示入口，XParamMountPrefab/XParamPlaySound仅抽样配置；prefab、音频、粒子和第三方loader内部未审，不宣称全部资源路径通过。

## 规则适用矩阵

| 规则 | 实现证据 | 裁决 |
|---|---|---|
| [GFX-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/EntitiesGraphics/GFX-01.md#L1-L20) / [Spec06出站边界](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L1-L31) | [Cue只有GO与稳定ID](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Cue/Base/GameplayCueBase.cs#L5-L83) | 受限符合：基类无Entity/EntityManager，未发现此基类回写Core |
| [GFX-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/EntitiesGraphics/GFX-02.md#L1-L25)、[GFX-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/EntitiesGraphics/GFX-03.md#L1-L23) | [Object.Instantiate](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Cue/Common/CueMountPrefab.cs#L145-L175) | N/A：这里是GameObject实例，不是Entities Graphics渲染实体；不能以未用RenderMeshUtility判违规 |
| [CONTENT-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Prefab-Content%E7%AE%A1%E7%90%86/CONTENT-02.md#L1-L24)生命周期思想 / Spec06 managed resource owner | [可插拔loader](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/GASResourceLoader.cs#L15-L61)、[加载但未保留句柄](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Cue/Common/CueMountPrefab.cs#L145-L163) | 目标gap，P1条件触发：引用计数loader下未配对释放。WeakObjectReference API本身未使用，非强制替换 |
| [Cue键与去重](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/06-Observation-Presentation-ReplaySpec.md#L51-L70) | [实例字段](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Cue/Base/GameplayCueBase.cs#L8-L17)、[日志snapshot](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Presentation/AutoChessPresentationOutboxBridge.cs#L97-L134) | 目标gap/本模块未证明：未见session级Cue ledger、cycle/event复合键与retry配对 |
| [GFX-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/EntitiesGraphics/GFX-04.md#L1-L20) | [headless派生源](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Presentation/AutoChessPresentationOutboxBridge.cs#L71-L90) | 待实测：日志bridge是派生只读路径，但没有据此证明有头/无头paired hash |
| [BUR-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-01.md#L1-L20)、[BUR-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-05.md#L1-L20) | [反射创建](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/General/Helper/CueHelper.cs#L35-L60) | BUR-01对表现对象N/A；BUR-05受限：Player stripping/AOT仍需实际验证 |
| [DBG-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-04.md#L1-L20)、[GFX-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/EntitiesGraphics/GFX-05.md#L1-L20) | [格式化输出](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Presentation/AutoChessPresentationOutboxBridge.cs#L113-L134) | 字符串在managed展示层允许；render/资源耗时独立计量待实测 |

## 发现、触发与验收

### M20-01｜P1｜Prefab加载资源没有配对释放
CueMountPrefab把LoadSync返回值保存在局部prefab，Instantiate后遗失原资产引用；[DestroyInstance](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Cue/Common/CueMountPrefab.cs#L320-L353)只销毁实例、清字段，没有GASResourceLoader.Release。对Resources缓存未必每次泄漏物理内存，但注册YooAsset/Addressables式引用计数loader后，每次activate会增加未归还引用。

验收：保存明确asset lease，实例销毁与asset释放分离且恰一次；重复OnRemove/OnDestroy/Reset不得double release；1000次创建/删除、失败加载、宿主提前销毁后引用计数回baseline；headless不调用loader。

### M20-02｜P2｜Reset与参数校验没有完整复用契约
[Sound清理](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Cue/Common/CuePlaySound.cs#L94-L149)的OnRemove/OnDestroy执行CleanUp，但Reset只StopSound；若池直接Reset后重新OnAdd，会覆盖旧clip/owned AudioSource。[参数类型检查](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/Cue/Base/GameplayCueBase.cs#L191-L218)在类型错时仅Editor日志，旧Parameter可能仍保留。这里不是已证实生产池调用顺序，而是公开复用API允许的风险。

验收：定义Reset是否必须释放lease；wrong-param typed失败且清空旧状态；覆盖Reset→Init→Add、Add失败、Remove→Destroy等排列，确保资源余额与实例数稳定。

### M20-03｜P1/目标gap｜日志marker不是完整Cue lifecycle消费者
当前AutoChess bridge裁剪BattleLog行并显示RuntimeMarkerCount，这证明展示派生，不证明OnActive/WhileActive/Removed按同一cycle键且每event去重。基类也只有sourceStableId与play/stop flags，不能独自满足Spec06的EffectHandle+definitionOrdinal+cycleOrdinal+AvatarBindingGeneration与reconcile快照。

验收：明确session ledger owner；同batch retry不重复播放；inhibit/reactivate产生新cycle；丢增量后snapshot关闭旧cycle；两个Cue/两个Effect同source不互相吞事件；资源缺失只影响consumer结果、不改变marker/hash。不能仅增加count断言转Green。

### M20-04｜P2/待实测｜反射注册和裁剪
CueHelper通过字符串→Type→Activator创建；没有在本模块见到完整保留清单。不能断言IL2CPP必失败，但Editor能创建不是AOT证据。

验收：目标Release/IL2CPP启用实际stripping，遍历允许Cue类型完成创建/参数绑定/销毁；未知类型和抽象类失败有typed诊断；注册表freeze后不随assembly枚举顺序漂移。

## 正面项

- Cue基类明确无ECS句柄，Play/Stop只是表现请求
- Sound在OnRemove/OnDestroy有资源释放与owned AudioSource销毁
- Log bridge不反向修改Core，显示行数有上限且记录DroppedLineCount
- 不强迫GameObject表现改为Entities Graphics，也不把Resources.Load本身定性为ECS违规；优化须有目标平台数据
