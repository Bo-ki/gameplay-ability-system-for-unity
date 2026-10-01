# M21 General / XParam / Utilities

## 基线、责任域与覆盖

固定提交 `61daa507e52e823ff42a8cb8c8ec91716c7e80e7`。静态阅读，无运行。General同时服务Editor、authoring与managed presentation；Runtime/General/XParam是配置/表现参数，不因路径叫Runtime就必须成为IComponentData。审查重点是明确与Burst Core的边界，避免旧时间/反射工具重新变成玩法权威。

已阅读：GASTimer、GASResourceLoader、DataClass/ObservableValue与JsonData、Util/Pool、TypeUtil、Validations、GeneralGasChoiceHelper（缓存/反射入口）、ReflectionHelper（API/缓存/解析分支）；Runtime/General/TimeUnit、XParam/XParam、XParamFloat、XParamApplyEffects及基础scalar/vector/array参数的接口和Editor编解码。Cue资源问题归M20。DebugDrawTool、DebugExtension、PriorityValue、GASAnimatorUtil、ReflectionPathHelper、大量XParam Cue字段仅抽样/未逐行核对；不宣称这些工具线程安全或全部AOT可用。

判定：符合/受限符合/目标gap/待实测/N/A。项目规则与官方API机制分开；本文没有证明旧工具已被现行V1 Kernel调用。

## 逐规则矩阵

| 标准 | 源码 | 裁决 |
|---|---|---|
| [BUR-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-01.md#L1-L20) / [02分层](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/02-%E5%9B%9B%E5%B1%82%E6%9E%B6%E6%9E%84Spec.md#L1-L42) | [XParam接口](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/General/XParam/XParam.cs#L1-L32)、[反射入口](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/Util/ReflectionHelper.cs#L12-L41) | 受限符合：managed配置/工具可用；不能进入Burst hot path，程序集目前未强隔离 |
| [整数tick和时间域](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10-AutoChess%E6%97%A0%E5%A4%B4%E9%AA%8C%E6%94%B6Spec.md#L31-L41) | [wall-clock全局timer](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/GASTimer.cs#L6-L44) | 目标gap/条件风险：不得用作Core时间权威；本模块未证明V1引用它 |
| [MAT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Mathematics-%E7%A1%AE%E5%AE%9A%E6%80%A7/MAT-01.md#L1-L20)、[MAT-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Mathematics-%E7%A1%AE%E5%AE%9A%E6%80%A7/MAT-02.md#L1-L20) | [静态全局状态](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/GASTimer.cs#L8-L26) | Random规则N/A（不是Random）；类比为时间owner风险，不能误称MAT-01直接违规 |
| [MAT-03](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Mathematics-%E7%A1%AE%E5%AE%9A%E6%80%A7/MAT-03.md#L1-L20) / 配置确定性 | [当前culture解析](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/General/XParam/XParamFloat.cs#L24-L34)、[Frame/Turn枚举](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/General/TimeUnit.cs#L1-L8) | 受限符合/目标gap：显式enum存在，但Frame是否simulation tick无类型约束；解析culture需冻结 |
| [BUR-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-05.md#L1-L20) | [assembly扫描](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/Util/TypeUtil.cs#L9-L51)、[Activator](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/Util/Pool.cs#L24-L35) | 待实测：只作Editor不要求Player；若运行时调用则需stripping/AOT注册证据 |
| [DBG-04](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Diagnostics-Debugger/DBG-04.md#L1-L20) | [Regex/格式化](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/Util/Validations.cs#L20-L43) | N/A于authoring校验；若接入Kernel hot path则不符合，未见调用证据不能指控已发生 |
| [CONTENT-02](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Prefab-Content%E7%AE%A1%E7%90%86/CONTENT-02.md#L1-L24) | [全局资源适配器](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/GASResourceLoader.cs#L15-L61) | 受限符合managed边界；载入/释放owner与重置契约目标gap，详M20 |

## 实质检查

### M21-01｜P2/边界风险｜GASTimer仍是可公开调用的全局墙钟
DateTimeOffset.UtcNow+static offset用于时间，FrameRate硬编码60，UpdateCurrentFrameCount由浮点秒推Frame。Pause仅记时间，Unpause修改全局offset。若引入V1 ability duration/period/cooldown，将把墙钟/暂停/运行顺序引入simulation；目前不能从此文件存在推断已经污染V1。

验收：静态边界门禁止Runtime/V1引用GASTimer与DateTime/Time.frameCount作玩法时间；将工具命名/文档限定presentation/editor；暂停、倒退墙钟和0/1/N render frames不影响Core tick/hash。

### M21-02｜P2｜authoring浮点编解码没有固定culture
XParamFloat.DecodeExcelData用ToString→float.Parse，未指定InvariantCulture。相同文本“1.5”在不同区域设置可能失败或含义不同；此处属于生成输入一致性，不是Burst数学API问题。不要用Editor限定掩盖跨机器生成差异。

验收：en-US/de-DE/zh-CN读取同规范化输入得到相同数值/语义hash；非法NaN/Infinity/locale格式有明确拒绝；vector/float array同步测试，不能只改一个scalar。

### M21-03｜P2｜General managed API缺乏物理隔离
[General asmdef](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/General/com.exhard.exgas.general.asmdef#L1-L14)没有Editor平台限制，包含reflection、Regex、Object loader；这是合法共享工具程序集，但不能声称Core assembly天然不含managed依赖。GeneralGasChoiceHelper的LoadCache有UNITY_EDITOR防护，部分选项查询/反射API仍对运行时公开。

验收：明确允许runtime调用表；把Editor-only reflection/preview隔离到Editor asmdef或编译guard；Core依赖检测按符号/调用域而非文件夹名称；Editor与目标Player构建均通过。避免为追求纯ECS而移除合法managed presentation。

### M21-04｜P2/待实测｜对象池容量与复用安全契约不明
Pool有一个FastItem加MaxCapacity个queue项，故总保留量最多MaxCapacity+1；Return不检类型/重复归还，也不调用Reset。它自称线程安全无锁，但调用者仍必须保证每对象仅一个lease与state清理。不能把ConcurrentQueue当作允许Core job持有object的依据。

验收：明确MaxCapacity是queue还是总量；并发get/return保持唯一lease、错误type/double return可检测；池化Cue从旧Parameter/resource状态完全清理；Burst Core只用自己的unmanaged slab，不复用此池。

### M21-05｜正面｜配置与表现元数据可保留托管模型
XParam接口中的Excel encode/decode只在UNITY_EDITOR存在；Simple JsonData/ObservableValue是托管桥接工具，未声明IComponentData。无需机械改成NativeContainer。ObservableValue setter同步回调不能被重用为同tick Core reaction bus，应用侧消费快照是合理位置。

## 建议落地顺序

先补依赖/调用边界检测与culture测试，再明确Timer/Pool契约。API存在不等于hot path执行；性能、AOT、线程安全结论均需对应实际调用链和Player证据。本模块不提出重写整个General层的建议。
