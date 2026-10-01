# M18 GAS Center Timeline 与Editor维护工具审查

## 结论

GAS Center仍是以表行为中心的维护工具，距离19/20规定的业务能力包、引用图、变更集和发布诊断有明显目标缺口。优先修复通用Excel写回层的外部冲突和ID完整性；这比重画窗口更直接减少返工。Timeline是保留的数据编辑/legacy预览路径，不是当前AutoChess Runnable主链，不能用它的缺口代表整个Runtime不可运行。

> 审查基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`；审查日期：2026-10-01。只读 GitHub 源码和仓库规范，未执行 Unity、Luban、dotnet、测试或性能测量。本文区分已证实源码行为、阶段性目标缺口和待动态验证事项；“符合”仅限列明的静态检查。

规范层级：[DOTS依据库README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)区分官方机制、EX-GAS项目规则和带基准的项目阈值；下文 BLOB/BUR/CONTENT 等为项目规则，不冒充Unity官方强制要求。已读相关 Baking/Blob、Burst、Prefab/Content、数据流 API 解读，以及目标规范08/14/15/19/20/25与[ADR-0001](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/adr/0001-codegen-single-install-root-and-install-envelope.md)。当前物理路线按 SourceGenerator 单 additional-file selector 裁决；旧 tarball、双active目录结论已被替代。

## 范围与已读清单

本模块覆盖Editor中GASCenterUIToolkit、GASCenterEditor辅助、AbilityTimelineEditor、GameplayAbilitySystem设置、Helper及Runtime Timeline数据/XParam接口边界。

已读：GASCenterUIToolkitWindow、GASCenterContext、GASCenterSettingPage、GASCenterAbilityPage、GASCenterEffectPage、GASCenterJsonTablePage、GASCenterExcelTable、GasXlsxChoice；GASSettingAsset、GASSettingStatusWatcher、EditorAbilityHelper；GasAbilityTimelineXlsxReadWrite、AbilityTimelineEditorWindow、AbilityTimelineTrack、TrackBase、TimelineActionClip；XParam、XParamCue、XParamApplyEffects。调用链精读保存/删除/切页/导出/播放。

未逐一审查所有Cue/ASC/Tag页面、旧Odin视图、USS/UXML布局和全部General小工具；未读取所有Timeline数据模型实现，也未执行Unity UI。Excel实际内容未解包。源码树/测试文件名中未发现直接命名为ExcelTable/Timeline round-trip的专项测试，但未穷举全部测试正文，不能断言绝无间接覆盖。

## 规范矩阵

| 编号/规则 | 源码 | 判定 | 说明 |
|---|---|---|---|
| [19 Editor row projection/change diff](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/19-GAS%E4%B8%9A%E5%8A%A1%E7%BC%96%E8%BE%91%E8%B7%AF%E5%BE%84%E4%B8%8E%E9%85%8D%E7%BD%AE%E9%93%BE%E8%81%8C%E8%B4%A3Spec.md)；[20 协作变更/stable ID](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/20-%E7%AD%96%E5%88%92%E9%85%8D%E7%BD%AE%E8%83%BD%E5%8A%9B%E4%BA%A4%E5%8F%89%E5%AE%A1%E6%9F%A5Spec.md) | [GASCenterExcelTable.cs L75–148](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterExcelTable.cs#L75-L148) | 确定缺陷 | 磁盘重新打开却沿用旧物理行映射，无冲突检测 |
| [19 保存前引用/ID验证](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/19-GAS%E4%B8%9A%E5%8A%A1%E7%BC%96%E8%BE%91%E8%B7%AF%E5%BE%84%E4%B8%8E%E9%85%8D%E7%BD%AE%E9%93%BE%E8%81%8C%E8%B4%A3Spec.md) | [GASCenterExcelTable.cs L47–71](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterExcelTable.cs#L47-L71) | 确定缺陷 | 空ID终止读取；重复ID静默覆盖 |
| [20 Generated Editor Binding](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/20-%E7%AD%96%E5%88%92%E9%85%8D%E7%BD%AE%E8%83%BD%E5%8A%9B%E4%BA%A4%E5%8F%89%E5%AE%A1%E6%9F%A5Spec.md)；[25 §3 Editor共用allow/deny矩阵](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [GASCenterEffectPage.cs L124–131](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs#L124-L131)、[GASCenterEffectPage.cs L560–582](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs#L560-L582) | 目标缺口 | UI手工协议/offset与支持信息未统一生成 |
| [19 Runtime trace preview](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/19-GAS%E4%B8%9A%E5%8A%A1%E7%BC%96%E8%BE%91%E8%B7%AF%E5%BE%84%E4%B8%8E%E9%85%8D%E7%BD%AE%E9%93%BE%E8%81%8C%E8%B4%A3Spec.md) | [TimelineActionClip.cs L82–86](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/Ability/AbilityTimelineEditor/Track/TimelineActionClipTrack/TimelineActionClip.cs#L82-L86) | 目标缺口（legacy） | 仅帧范围检查，无实际action执行；不代表当前Runnable主链 |
| [19 Luban唯一长期权威](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/19-GAS%E4%B8%9A%E5%8A%A1%E7%BC%96%E8%BE%91%E8%B7%AF%E5%BE%84%E4%B8%8E%E9%85%8D%E7%BD%AE%E9%93%BE%E8%81%8C%E8%B4%A3Spec.md) | [GasAbilityTimelineXlsxReadWrite.cs L152–199](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/Ability/AbilityTimelineEditor/DataClass/GasAbilityTimelineXlsxReadWrite.cs#L152-L199) | 确定缺陷（legacy） | 清空clip后全表重写会丢技能基础记录 |
| [BUR-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-01.md)；[BAKE-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Baking-BlobAsset/BAKE-01.md) | [GASCenterContext.cs L9–49](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterContext.cs#L9-L49) | N-A | Editor UI、EPPlus、JSON和反射不是runtime热路径或Baker API |
| [CONTENT-01 定义与实体原型区分](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Prefab-Content%E7%AE%A1%E7%90%86/CONTENT-01.md) | [GASCenterAbilityPage.cs L339–353](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterAbilityPage.cs#L339-L353) | 符合（所读写回链） | UI改row，不以per-definition Entity/Prefab替代静态配置 |

## 实质检查

### M18-01 外部排序后保存/删除可能命中错误ID

严重度：P1确定缺陷。Load把ID缓存为物理行号；SaveRow/SaveRowWithRawColumns重新打开当前磁盘文件但AllocateRow返回旧行号；DeleteRow同样按旧行号删除。[GASCenterExcelTable.cs L47–148](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterExcelTable.cs#L47-L148)。

触发：加载A→外部Excel插行/排序/删除前面的行→回到窗口不刷新→保存/删除A。Ability页同时提供“打开Excel”“刷新”“保存”，所以不是只有非公开API才能触发。[GASCenterAbilityPage.cs L64–73](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterAbilityPage.cs#L64-L73)。外部变化后N行可能已变成B，写回可覆盖B并制造重复A，删除可删B。

最小改善：加载时保存文件hash与schema/header快照；写前检测变化，冲突时拒绝并展示diff/重载。当前workbook内按唯一ID重新解析目标，不能只信缓存行号。先统一修复ExcelTable让所有页面受益。

验收：加载后外部插行、排序、删除、同一记录改值时，保存/删除要么准确作用到唯一ID，要么明确拒绝；不得改动无关记录。文件locked/保存异常时没有成功提示。

### M18-02 空行与重复ID隐藏了真实数据边界

严重度：P2确定缺陷，可能升级为覆盖数据。读取遇第一条空ID行就停止，重复ID直接Rows[id]=rowData覆盖；新增以已读映射的最大行+1分配。[GASCenterExcelTable.cs L47–84](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterExcelTable.cs#L47-L84)。GasXlsxChoice也用遇空即停，故引用下拉会同步漏记录。

触发：外部编辑留分隔空行、暂时清空中间ID或多人合并生成重复ID。影响：后续记录不可见，重复ID仅最后一行可操作；连续新增最终可能写入未读到的有效行。

最小改善：扫描实际数据边界，空行skip；重复/无效ID和缺表头统一生成文件/sheet/row诊断，并阻止任何写入。新增位置按实际内容边界分配而非可见行边界。

验收：空行前后都有记录、重复ID、非整数ID、首行空、末行稀疏等向量；不能把错误数据默默转换为“空表”。

### M18-03 Timeline删除最后clip会丢整个技能记录

严重度：P1数据完整性缺陷，但作用于legacy Timeline authoring，不列为当前Runnable主线第一阻塞。

触发：删除某Timeline技能最后一个Action或最后一个轨道，再保存。clip删除只移除Action；Write先清除第6行以后全部旧数据，再跳过无轨道/无Action项，ID和基础信息只在第一条Action写出。[TimelineActionClip.cs L51–56](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/Ability/AbilityTimelineEditor/Track/TimelineActionClipTrack/TimelineActionClip.cs#L51-L56)；[GasAbilityTimelineXlsxReadWrite.cs L152–199](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/Ability/AbilityTimelineEditor/DataClass/GasAbilityTimelineXlsxReadWrite.cs#L152-L199)。因此空技能在下次reload消失，引用它的TimelineRef可能悬空；窗口仍报告保存成功。[AbilityTimelineEditorWindow.cs L89–93](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/Ability/AbilityTimelineEditor/EditorWindow/AbilityTimelineEditorWindow.cs#L89-L93)。

最小改善：保留空技能的基础行，或若schema不支持空技能，保存前明确阻止并要求用户显式删除。写回前检测外部文件变化，用临时输出+round-trip校验后替换；Write返回可显示的结果，不无条件成功。

验收：空技能、空轨道、最后clip删除、文件不存在、保存中断、外部修改冲突。当前Runtime支持Timeline与否不影响源数据不能静默丢失的要求。

未列入确定缺陷：读参数时跳过null可能压缩索引，但XParam接口明确要求默认占位、流式配置不支持空占位；没有验证具体合法配置会被破坏，故不能直接宣称当前数据普遍错位。

### M18-04 默认UI仍复制raw协议，缺共享支持矩阵

严重度：P1目标缺口，不是所有UI字段都存在运行错误。Effect页直接提示Modifiers和GrantedAbility分隔符协议，Duration/Period/Stacking以offset写列；Ability页有手工组件列表和整数引用。[GASCenterEffectPage.cs L124–131](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs#L124-L131)、[GASCenterEffectPage.cs L560–582](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterEffectPage.cs#L560-L582)。默认Window按Setting/Tag/Attribute/Cue/Effect/Ability/ASC单表注册，并非19目标中的业务能力包。[GASCenterUIToolkitWindow.cs L43–63](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterUIToolkitWindow.cs#L43-L63)。

影响：业务作者先理解多表ID和协议，再到导出/生成阶段发现当前profile拒绝；字段、UI与规则之间容易漂移。M17中的固定数值限制会放大这种反馈延迟。

最小改善：不重写整个Editor。先在现有Ability/Effect页加入共享schema/RuleId binding和“当前profile可用/不可用”提示；一个单目标伤害模板聚合相关row，保存前展示变更diff和引用影响。raw维护页保留为高级入口。

验收：未知引用、unsupported字段在写回前可定位；UI的allow/deny来自同一规则源；编辑器不直接生成/注入Runtime Catalog作为捷径。

### M18-05 Timeline播放目前不是行为验证

严重度：P2功能缺口（legacy）。OnTickView通过帧区间检查后没有执行内容，注释明示托管时间轴执行链已移除。[TimelineActionClip.cs L82–86](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/Ability/AbilityTimelineEditor/Track/TimelineActionClipTrack/TimelineActionClip.cs#L82-L86)。移动时间指针不能证明Cue/Effect等实际消费链正确。

最小改善：现在就明确“仅数据时间轴预览”，防用户把可播放按钮当业务验证；以后若接Runtime trace，应只读generated metadata/pure glue并展现已执行/拒绝原因，不读取live权威状态反推配置，更不能preview回写gameplay。

验收：UI清楚标注预览边界；若声明行为预览完成，需有action→record→fact/cue的固定向量。无需为legacy UI增加Runtime lifecycle生成器。

### M18-06 已有维护入口与分层可保留

严重度：正面。Ability保存调用共享ExcelTable，导出JSON显式调用CodeGenerator，设置另有“生成Runtime v1”按钮；这些操作至少在代码上分开，没有通过UI直接写ECS世界完成配表。Editor使用managed对象、反射、JSON是合理边界，不能以BUR-01要求全部改成NativeArray或Job。

验收：在状态文案中继续区分“已保存源表”“已导出JSON”“selector已提交”“语义/场景验证通过”；窗口切页或刷新会重新加载数据，新增dirty-state提示应基于真实未保存变更，不把保存当发布。

## 建议顺序

第一步修共享ExcelTable的文件冲突/ID验证；第二步将profile限制和RuleId显示前移；第三步做一个能力包变更集垂直切片。Timeline按legacy维护优先级修数据损失和预览提示，勿拿它的功能缺口替代当前AutoChess Runtime验收结论。

