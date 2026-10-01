# M24 文档、ECS 规范与状态治理审查

- 基线：61daa507e52e823ff42a8cb8c8ec91716c7e80e7
- 模块范围：方案讨论/UnityDOTS官方文档参考、目标态/事实/任务/当前窗口、docs/adr、docs/reviews
- 方法：静态文档交叉检查；没有重新执行 PackageCache 快照认证或测试
- 总结：标准体系的分类清晰，但状态与索引存在漂移，不能用某个“当前窗口”摘要替代逐条源码复核

## 已读取及覆盖限制

本轮完整读取 DOTS README、元信息行动报告模板、主题20 API基线、主题21 ODF流程、目标态 README；检查全局规则索引与 ADR0001 当前裁决、RuntimeV1迁移任务入口、当前窗口、D1结果及前轮相关审查。主题原子规则由对应模块读取。未逐字复核全部历史归档、官方原件或 817 份 Markdown；不声明全库链接完整。

## 标准对照

| 标准 | 实际证据 | 判定 |
|---|---|---|
| ODF-01/08：主题门户是正文入口，旧数字文件仅跳转 | README 明确主题/_index/API/原子规则分工，全局索引列 Primary Owner | 结构静态符合；所有旧文件是否纯跳转未全量认证 |
| ODF-02/11：精确版本与原件 hash | README 明列精确包版本及 PackageCache 路径，并要求逐文件比对 | 版本记载符合；本轮没有本机 cache，hash认证待验证 |
| ODF-03/04/05：目标、事实、任务分离 | 目标态 README 明确这些 owner；docs/reviews 另存历史交付裁决 | 职责定义符合；当前状态同步仍有偏差 |
| ODF-09/12/18：性能分组、JIT/AOT、render 分离 | API基线与行动模板要求独立证据 | 标准明确；没有本轮性能证据，不判性能合格 |
| 唯一规则编号/可达索引 | 全局索引声明 5 条 SEL，指向主题20的 #sel-规则；当前主题20没有该标题或 SEL-01…05 正文 | 确认文档索引与正文不一致，P2 |
| 当前范围与目标差距不得混淆 | 当前窗口实施盘点仍写 ABL-08无normal End、SYS-01无Request outcome；较新 scoped裁决与现行源码已有对应实现 | 确认状态粒度漂移，P2；不能因此认定全部通用语义已完成 |
| 显式 superseded 裁决 | ADR0001首段接受 StableGraphSourceGenerator，下文明确保留tarball历史合同 | 当前路线可识别；读者必须保留历史/现行区别 |

标准链接：[ODF规则](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/21-%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E8%A6%86%E7%9B%96%E4%B8%8E%E6%B5%81%E7%A8%8B%E9%97%AD%E7%8E%AF.md)；[索引](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E5%85%83%E4%BF%A1%E6%81%AF/90-%E8%A7%84%E5%88%99%E7%BC%96%E5%8F%B7%E7%B4%A2%E5%BC%95.md)；[主题20正文](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/20-GASRuntimeCore-API%E9%80%89%E5%9E%8B%E5%9F%BA%E7%BA%BF.md)；[报告模板](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E5%85%83%E4%BF%A1%E6%81%AF/%E8%A1%8C%E5%8A%A8%E6%8A%A5%E5%91%8A%E6%A8%A1%E6%9D%BF.md)。

## G24-01：SEL 索引与正文脱节

全局索引列出“5条SEL治理规则”，并链接主题20的 #sel-规则；当前文件正文是第1至9节，没有该规则标题和编号。风险是报告引用SEL编号时无法定位唯一规范正文。

建议：由标准 owner 决定恢复明确编号或更新索引，不根据索引自行发明五条规则。本次各报告应引用主题20实际章节或已存在原子规则。

验收：规则索引中每个编号有唯一可达正文，锚点存在；旧兼容入口不复制第二份内容。

## G24-02：“当前”状态包含不同历史阶段

[当前窗口](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/04-%E5%BD%93%E5%89%8D%E8%BF%9B%E5%BA%A6%E7%8A%B6%E6%80%81/%E5%BD%93%E5%89%8D%E7%AA%97%E5%8F%A3.md)的交付摘要与实施盘点混用不同代际；[迁移任务入口](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/02-%E4%B8%BB%E7%BA%BF%E4%BB%BB%E5%8A%A1%E6%A0%91/RuntimeV1%E4%B8%8D%E5%8F%AF%E5%85%BC%E5%AE%B9%E8%BF%81%E7%A7%BB/README.md)仍保留较早N2描述。[完成后scoped复核](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/reviews/RuntimeV1-%E5%AE%8C%E6%88%90%E5%90%8E%E5%81%9C%E9%A1%BF%E5%AE%A1%E6%9F%A5%E4%B8%8EV1.1-D0-M2F%E5%8D%95%E8%BD%AE%E8%AE%A1%E5%88%92.md)已经将多个旧P0限定为Closed-In-Runnable-Profile或Closed-By-Profile-Rejection。

当前实现证据包括 [one-shot lifecycle](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/System/GasTickJobs.cs)、[命令拒绝与准入](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/V1/Boundary/SessionIngressGate.cs)、[Runnable测试](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/_Test/RuntimeV1/PlayMode/RuntimeV1RunnablePlayModeTests.cs)。源码证明对应入口已存在，不能由此自动将全部Tier-B转Green。

建议：状态表按“规则/向量→当前支持范围→实现→测试→证据→尚缺语义”逐项维护；历史记录保留但显式标为历史。避免再次修复已经闭合的窄场景，也避免把窄场景完成误当全语义完成。

验收：同一能力在入口摘要、任务树和事实表具有一致范围；每条当前结论绑定源码SHA与真实测试ID；无证据项明确待验证。

## G24-03：审查记录不能冒充已执行验收

D1文档里的原始结果链接在本次仓库树缺少相应文件，详见M22；属于证据交付问题，不直接否认历史成功。建议按ODF-10提供可移植结果包与hash。报告不得将测试源代码存在写成运行通过。

## 不适用与反哺

文档治理不承担Tick、allocator、结构变化或simulation writer；这些代码规范对本模块为N-A。治理的职责是使适用规则可查、效力可辨、结果可复核。本轮不改原标准或任务状态；后续由规则索引owner、当前事实owner及交付证据owner分别收口。


## G24-04 / P1：Cleanup 案例与同版本官方原件机制矛盾

[CASE-15](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/CASE-15.md)描述 Destroy 将数据复制到临时 cleanup entity，并在处理后再次 DestroyEntity；注意事项也要求显式销毁 cleanup entity。

同提交的[官方原件 components-cleanup-introducing.md](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8E%9F%E4%BB%B6/com.unity.entities/Documentation~/components-cleanup-introducing.md)第3行及14–22行则明确：Destroy移除非cleanup组件，原Entity继续存在；移除最后cleanup组件才实际销毁。二者为可直接核对的机制冲突，非单纯不同架构风格。

本轮判定采用官方原件与正确API解读的机制，不要求实现复制新Entity，也不因正确移除cleanup buffer而判违背CASE-15。该案例还把处理写成经验分配等行为，不应被直接拿来绕过Boundary-only的目标合同。

建议由数据流主题owner修正文案和示例，区分“保留原Entity shell”和“创建另一Entity”，示例最终移除cleanup组件并符合项目EndFixed结构变化时序。验收：Destroy后Entity身份不变、非cleanup消失、最后cleanup移除后不存在，且标准链接与示例一致。本轮仅记录报告，不修改规范原件或CASE文件。
