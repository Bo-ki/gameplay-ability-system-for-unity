# M17 Luban Authoring 与配置导出审查

## 结论

当前配置链最直接的内容迭代阻塞是Runnable profile把实际战斗数值一起冻结；这属于明确的阶段性闭世界策略，不应伪装成通用配表支持。另有可明确定位的设置快照缓存缺陷和跨平台导出脚本不等价。先把每次操作的settings/input snapshot与统一导出契约做可靠，再逐项放开profile能力。

> 审查基线：`61daa507e52e823ff42a8cb8c8ec91716c7e80e7`；审查日期：2026-10-01。只读 GitHub 源码和仓库规范，未执行 Unity、Luban、dotnet、测试或性能测量。本文区分已证实源码行为、阶段性目标缺口和待动态验证事项；“符合”仅限列明的静态检查。

规范层级：[DOTS依据库README](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md)区分官方机制、EX-GAS项目规则和带基准的项目阈值；下文 BLOB/BUR/CONTENT 等为项目规则，不冒充Unity官方强制要求。已读相关 Baking/Blob、Burst、Prefab/Content、数据流 API 解读，以及目标规范08/14/15/19/20/25与[ADR-0001](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/docs/adr/0001-codegen-single-install-root-and-install-envelope.md)。当前物理路线按 SourceGenerator 单 additional-file selector 裁决；旧 tarball、双active目录结论已被替代。

## 范围与已读清单

负责 EX_GAS_Config、BeanUpdater、CodeGenerator与ProcessGate。已读：exgas_config/luban.conf、gen.bat、gen.sh，BeanUpdater、CodeGenerator、GasCodeGenProcessGate、GASSettingAsset/Context、GasCodeGenSettings；交叉已读RuntimeV1RawAuthoringSupportProfileAdapter、AutoChessDemoConfigModel、RuntimeV1RunnableSupportProfileTests、AutoChessLubanDamageRegressionTests、Tools/CodeGen驱动说明。

已核查Excel文件路径/树，但未解包读取二进制xlsx的每个单元格；未运行Luban。BeanUpdater完整源码已取得，精读settings、schema收集、写出和错误处理，未逐一验证所有自定义XParam反射结果。不同OS、Excel锁占用、异常中断后文件状态仍待动态验证。

## 规范矩阵

| 编号/规范 | 代码证据 | 判定 | 说明 |
|---|---|---|---|
| [08 核心契约15 Luban长期权威](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/08-Luban-SourceGenerator%E9%85%8D%E7%BD%AE%E7%94%9F%E6%88%90%E9%93%BE%E8%B7%AFSpec.md)；[19 Luban职责](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/19-GAS%E4%B8%9A%E5%8A%A1%E7%BC%96%E8%BE%91%E8%B7%AF%E5%BE%84%E4%B8%8E%E9%85%8D%E7%BD%AE%E9%93%BE%E8%81%8C%E8%B4%A3Spec.md) | [luban.conf L1–21](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/EX_GAS_Config/ProjectConfigTable/exgas_config/luban.conf#L1-L21)；[CodeGenerator.cs L26–45](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/CodeGenerator.cs#L26-L45) | 受限符合 | schema/rows明确；不同入口尚无统一settings/source快照保证 |
| [20 平衡调参与支持诊断](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/20-%E7%AD%96%E5%88%92%E9%85%8D%E7%BD%AE%E8%83%BD%E5%8A%9B%E4%BA%A4%E5%8F%89%E5%AE%A1%E6%9F%A5Spec.md) | [RuntimeV1RawAuthoringSupportProfileAdapter.cs L161–186](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/RuntimeV1RawAuthoringSupportProfileAdapter.cs#L161-L186) | 目标缺口 | 固定黄金伤害限制普通调参，不等于schema不支持数值 |
| [25 §2 single-run consistency](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/25-%E9%85%8D%E7%BD%AE%E8%AF%AD%E4%B9%89%E7%BC%96%E8%AF%91%E5%A5%91%E7%BA%A6%E4%B8%8ECapacityProof%E7%BB%9F%E4%B8%80%E8%A3%81%E5%86%B3Spec.md) | [BeanUpdater.cs L66–76](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/BeanUpdater.cs#L66-L76)；[CodeGenerator.cs L26–36](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/CodeGenerator.cs#L26-L36) | 确定缺陷 | ActiveSettings跨调用缓存可能与新保存的配置不一致 |
| [08 Luban Unity编译边界](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/08-Luban-SourceGenerator%E9%85%8D%E7%BD%AE%E7%94%9F%E6%88%90%E9%93%BE%E8%B7%AFSpec.md) | [gen.sh L1–11](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/EX_GAS_Config/ProjectConfigTable/exgas_config/gen.sh#L1-L11)；[gen.bat L1–14](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/EX_GAS_Config/ProjectConfigTable/exgas_config/gen.bat#L1-L14) | 受限符合 / 入口差异 | shell脚本不生成C#且输出output，与Editor client/C#路径不同 |
| [15 不生成runtime owner](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/15-SourceGenerator%E8%81%8C%E8%B4%A3%E8%BE%B9%E7%95%8CSpec.md)；[BUR-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-01.md) | [BeanUpdater.cs L66–104](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/BeanUpdater.cs#L66-L104) | N-A / 符合边界 | Bean反射、EPPlus、Process属于Editor authoring控制面，不要求Burst |
| [20 发布状态不能等同保存](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/20-%E7%AD%96%E5%88%92%E9%85%8D%E7%BD%AE%E8%83%BD%E5%8A%9B%E4%BA%A4%E5%8F%89%E5%AE%A1%E6%9F%A5Spec.md) | [CodeGenerator.cs L60–65](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/CodeGenerator.cs#L60-L65)；[GasCodeGenProcessGate.cs L5–21](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenProcessGate.cs#L5-L21) | 受限符合 | 顺序门存在；导表成功仅代表Luban退出0，不能替代完整candidate与runtime门 |

## 实质检查

### M17-01 普通数值调整会碰到精确样例profile

严重度：P1迭代限制；已证实设计限制，非无意写错分支。Raw gate固定PlayerAttackDamage=12、EnemyAttackDamage=8，finisher与poison也按精确参数检查；Editor模型生成前执行该gate。[RuntimeV1RawAuthoringSupportProfileAdapter.cs L161–234](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/RuntimeV1RawAuthoringSupportProfileAdapter.cs#L161-L234)；[AutoChessDemoConfigModel.cs L37–47](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/AutoChessDemoConfigModel.cs#L37-L47)。

触发：把基础伤害12改13，或修改既有poison公式系数，其他字段完全合法。影响：仍会被RawEffectUnsupported拒绝；Runtime Blob profile还独立限制闭世界形状，修掉一处raw常量不等于完成支持扩展。

现有测试有意把Luban玩家伤害12冻结为公开BattleReport期望，防止旧手工Catalog=44复活。[AutoChessLubanDamageRegressionTests.cs L10–56](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Tests/EditMode/AutoChessLubanDamageRegressionTests.cs#L10-L56)。这种黄金回归有用，应与“任意合法业务数值可编辑”分开。

最小改善：把可支持的结构/语义/范围规则留在capability profile；把具体12/8与样例公式放golden fixture。若当前阶段不能放开，就在Editor字段处显示锁定原因、RuleId和受影响测试，而不是等生成最后报错。

验收：合法数值变更沿raw→model→Blob→BattleReport同源生效；不支持的结构仍fail-closed；UI、raw和Blob对支持面一致。禁止直接关闭整个profile绕过保护。

### M17-02 Settings A导表后改B，下一次可能仍处理A

严重度：P1/P2确定缺陷，取决于用户是否切换工程/输出目录。BeanUpdater与CodeGenerator优先读取static ActiveSettings；GasCodeGenSettings.From复制字符串到不可变对象，SaveSettings仅保存ScriptableObject，没有失效该缓存。[GasCodeGenContext.cs L635–676](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenContext.cs#L635-L676)；[GASCenterContext.cs L44–49](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/GASCenterUIToolkit/GASCenterContext.cs#L44-L49)。

触发：同一Editor domain已执行一次导表→修改ConfigProjectPath或输出路径→保存→再次导表。单独导表可继续写旧路径；完整入口先用旧settings跑process gate，随后Context.Create又读新settings，形成同一次生成两个配置快照。[GasCodeGenContext.cs L304–320](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenContext.cs#L304-L320)。

最小改善：公开操作入口捕获一次新settings，显式传递给Bean/Luban/Pipeline；或保存设置立即invalidate，但仍建议事务内显式snapshot。不要依赖domain reload恰好清static。

验收：A→B切换后本次所有读取/写入均为B；错误路径不写A；失败后返回结果包含实际resolved paths。

### M17-03 仓库的gen.sh与gen.bat不是等价导出器

严重度：P2确定入口差异，是否是当前团队实际阻塞待验证。gen.sh执行target all、只-d json、输出相对output；gen.bat执行client、cs-simple-json、从参数取两个输出目录。Editor同样client+C#，但使用配置输出路径。两个脚本都按当前工作目录解析CONF_ROOT，未切换到脚本目录；bat无参数时输出变量为空。

触发：按仓库AGENTS中的跨平台命令从项目根直接运行，或在Mac/Linux把gen.sh当作Windows/Editor同义入口。结果可能是conf/dll路径不对，或只更新了另一个output目录，Unity-visible JSON/C#仍旧。

最小改善：一个共同参数契约，脚本首先定位自身目录/工程root；默认目标与JSON/C#输出跟设置一致；参数缺失明确报错，自动化入口移除pause。保留仅JSON/all作为显式模式，不能让名字暗示完整生成。

验收：Windows/Linux在任意cwd、有空格路径、无参数/显式参数下消费同一输入并输出同一目标集；验证错误退出码。此建议仅文档，未执行或改脚本。

### M17-04 Luban子进程无限同步等待会拖住Editor反馈

严重度：P2条件性可用性风险。输出异步读取避免简单stdout阻塞，但按钮回调调用Process.WaitForExit且没有timeout/cancel。[CodeGenerator.cs L124–151](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/CodeGenerator.cs#L124-L151)。

触发：dotnet/Luban卡住、超长运行或等待不可恢复资源。影响：Editor调用栈等待，缺少可取消进度和阶段诊断；这不是Runtime Burst/Job性能问题。

最小改善：保留同一事务门，改为可取消的异步控制与明确timeout策略；取消/失败不能把部分JSON视为成功generation。日志要标Bean、Luban、candidate、publish分别耗时，不凭猜测优化Runtime。

验收：受控挂起子进程时可取消；退出码、stderr、耗时与失败阶段可追溯；active selector不因失败变化。

### M17-05 顺序process gate存在，但原始输入变更仍需单独保护

严重度：正面与P2边界。ProcessGate先Bean成功，再Luban成功，最后进入Pipeline。任何一段返回false阻断下一段，这是正确的fail-closed控制流。[GasCodeGenProcessGate.cs L5–21](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/CodeGen/Core/GasCodeGenProcessGate.cs#L5-L21)。

但candidate selector原子提交保护的是生成产物；BeanUpdater此前已修改__beans__.xlsx，Luban此前已写JSON/C#。不能把candidate原子性宣传为原始Excel/所有中间产物一起回滚。

最小改善：在操作结果中列出source/schema变更diff及导出产物身份；异常时保留可恢复备份或临时输出，下一次必须重新核验输入。优先把变更范围说清，不建立第二套Runtime配置源。

验收：Bean成功而Luban失败时，报告准确标注哪些文件已改；candidate与active状态保持可区分；生成Report不因导表退出0自动标“全语义通过”。

## 建议顺序

先修settings快照与脚本入口；对当前closed-world受限能力做明确显示；随后把黄金样例值与合法数值域分离。Excel实际写回冲突与legacy Timeline行为见M18，旧factory单遍一致性见M16。

