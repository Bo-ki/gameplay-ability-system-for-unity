# M23 Packaging / Dependencies / Repository Delivery

## 基线与范围

固定提交 `61daa507e52e823ff42a8cb8c8ec91716c7e80e7`，完整递归树3130条、truncated=false。只读审查，没有执行build、包安装或项目脚本；本文仅报告，不修改源码/依赖/工程配置。

已读：Packages/manifest.json、packages-lock.json；ProjectSettings/ProjectVersion.txt；Assets/GAS/package.json；Runtime/General/Editor与AutoChess asmdef及关键meta；analyzer DLL.meta与selector.meta；.gitignore；官方依据README、相关主题API与原子规则；Tools/Diagnostics两个边界检查脚本。ProjectSettings/GasCodeGen生成archive与binary只清点路径，不声称验证DLL反编译、签名、每个hash或完整RSP消费链。

第三方范围：Assets/Plugins可见LubanRuntime与Sirenix，以及manifest接入的AIBridge/UniTask/Unity packages。只审依赖边界、平台/asmdef/meta声明和可重复交付；**没有全面审计第三方源码、DLL内部、原生库或许可合规**。Unity官方文档快照只对所引用文件读取，未与本机PackageCache逐文件hash比对。ProjectSettings其余渲染/质量/平台设置及全部meta未逐项审计，不能据本报告宣称跨平台可发布。

## 逐规则矩阵

| 标准 | 仓库证据 | 裁决 |
|---|---|---|
| [精确版本依据](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/README.md#L101-L120) | [Unity版本](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/ProjectSettings/ProjectVersion.txt#L1-L2)、[直接依赖](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Packages/manifest.json#L3-L20)、[Burst/Collections锁](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Packages/packages-lock.json#L138-L175) | 符合版本声明：6000.3.14f1、Entities1.4.6、Graphics1.4.19、Burst1.8.29、Collections2.6.6；缓存实际身份待实测 |
| [BUR-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-01.md#L1-L20) / [分层](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/02-%E5%9B%9B%E5%B1%82%E6%9E%B6%E6%9E%84Spec.md#L1-L42) | [Runtime assembly](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Runtime/com.exhard.exgas.runtime.asmdef#L1-L21)、[Editor平台](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/Editor/com.exhard.exgas.editor.asmdef#L1-L17) | 受限符合：Editor限制存在；Runtime/Cue/General与V1未物理分assembly，不能仅凭Runtime名称宣称无managed依赖 |
| [BUR-05](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Burst-AOT/BUR-05.md#L1-L20) | [Windows Development builder](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/Editor/RuntimeV1RunnablePlayerBuilder.cs#L20-L43) | 待实测：目标固定Windows Development，非完整Release/IL2CPP平台矩阵 |
| [GFX-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/EntitiesGraphics/GFX-01.md#L1-L20) | [demo依赖](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef#L1-L21) | 待实测/受限：GUID引用需经Unity解析；未把未解析外部GUID猜成Unity.Rendering违规 |
| [build/evidence身份](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10-AutoChess%E6%97%A0%E5%A4%B4%E9%AA%8C%E6%94%B6Spec.md#L109-L152) | [csproj忽略](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/.gitignore#L42-L47)、完整树仅两个工具csproj | 目标gap，P1：generator/codec项目源码构建定义缺失 |
| [真实证据门](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/%E9%92%88%E5%AF%B92.0%E7%9A%84ECS%E6%9E%B6%E6%9E%84%E7%9A%84%E8%BF%AD%E4%BB%A3%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/01-%E7%9B%AE%E6%A0%87%E6%80%81%E6%9E%B6%E6%9E%84%E5%85%B1%E8%AF%86/10-AutoChess%E6%97%A0%E5%A4%B4%E9%AA%8C%E6%94%B6Spec.md#L175-L182) | [TestResults忽略](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/.gitignore#L5-L11)；当前树无D1原始结果目录 | 目标gap：文档Passed不能独立重放验证 |
| [CONTENT-01](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/Prefab-Content%E7%AE%A1%E7%90%86/CONTENT-01.md#L1-L22) / 规范层级 | [旧cleanup案例](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E4%B8%BB%E9%A2%98/%E6%95%B0%E6%8D%AE%E6%B5%81-%E7%B3%BB%E7%BB%9F%E7%94%9F%E5%91%BD%E5%91%A8%E6%9C%9F/CASE-15.md#L15-L46)与[官方cleanup机制](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/%E6%96%B9%E6%A1%88%E8%AE%A8%E8%AE%BA/UnityDOTS%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8F%82%E8%80%83/%E5%AE%98%E6%96%B9%E6%96%87%E6%A1%A3%E5%8E%9F%E4%BB%B6/com.unity.entities/Documentation~/components-cleanup-introducing.md#L3-L24) | 文档治理gap：案例错误，必须官方原件优先；不是要求修改正确runtime来适配错误案例 |

## 发现与验收

### M23-01｜P1｜提交bin/obj却遗漏可构建项目定义
完整树只有Tools/GasCodeGenCli/GasCodeGenCli.csproj、Tools/LubanNormalizedRowBootstrap/LubanNormalizedRowBootstrap.csproj；Tools/GasCodeGenSourceGenerator和Tools/Tests/GasCodeGen/D1/Codec存在源码、bin/obj DLL/cache，但没有对应csproj。.gitignore全局忽略*.csproj，仅白名单前述两个。生成器DLL已经作为RoslynAnalyzer接入，不等于新机器能从源码重建它。

验收：提交generator/codec必要项目定义、SDK/依赖约束与构建命令；清空工具bin/obj后能还原/构建/测试；记录源码提交、toolchain与产物hash；工程缓存不作为唯一build provenance。

### M23-02｜P1/交付gap｜包元数据不能支持独立UPM消费声明
[package metadata](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/package.json#L1-L18)标unity2022.3、dependencies为空，实际工程使用Unity6000.3和Entities/Collections/Burst等版本。README已说明unity字段不代表当前工程版本，因此不是发现版本运行失败；问题是如果把Assets/GAS子目录当独立UPM包发布，依赖声明与兼容范围不足。

验收：明确“完整工程示例”或“可独立安装包”二选一的交付契约；若独立包，在空项目安装全部required packages并测试Editor/Player；更新最小Unity支持矩阵与第三方可选边界，不能仅提升version数字。

### M23-03｜P2/边界gap｜程序集分层依赖软约定
Editor asmdef限制Editor是正面项；Runtime包含Cue与V1、General包含managed反射/资源loader，noEngineReferences=false。Unity允许这种混合，不能据此直接判非ECS；但编译层无法阻止后续Core误调用managed helper。

验收：至少静态依赖门按Core命名空间/目录禁止UnityEngine.Object/Reflection/Graphics读取；如拆assembly，保持Cue/Editor合法托管边界与GUID稳定；所有引用通过Unity真实import，不根据GUID外观猜包名。

### 规范治理交叉引用（主问题见 M24 / G24-04）

已独立复核CASE-15与官方cleanup原件相冲突；本模块仅提醒依赖官方原件与现行Spec，不重复计为独立finding。修订owner、严重度和验收由M24 / G24-04维护。本次未修改规范源。

### M23-04｜P2/待实测｜meta正确接入不等于AOT与生成器执行已验证
[RoslynAnalyzer标签](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll.meta#L1-L15)存在，selector.meta与Runtime/General/AutoChess GUID稳定可见，是积极接入证据。仍需compile RSP和生成marker证明三目标assembly消费同一selector/analyzer；不应把DLL二进制存在或metadata标签当执行成功。

验收：干净import、缺失/损坏analyzer、重复selector、stale Bee都fail-closed；目标Player记录marker一致性、AOT backend与stripping设置；全meta依赖/重复GUID扫描由Unity或工具输出证据。

### M23-05｜证据边界｜历史结果与版本锁
当前提交树未包含D1FINAL/Codec/Unity测试原始完整证据，不能验证历史11/5报告；也不能据缺证据宣称测试失败。UniTask manifest无ref，但[lock中git hash](https://github.com/Bo-ki/gameplay-ability-system-for-unity/blob/61daa507e52e823ff42a8cb8c8ec91716c7e80e7/Packages/packages-lock.json#L12-L18)已有固定hash，不应误报“完全未锁版本”。clean restore应保持lock不漂移，升级时更新规范版本表与受影响门。

## 建议下一轮最小交付

1. 补源码工程与clean-clone bootstrap，冻结SDK/Unity/package-lock
2. 修过时Diagnostics旧gen路径门（详M22），不恢复legacy双路线
3. 发布绑定提交的immutable证据包：XML/log/provenance/selector/build，独立机器可复算
4. 再补发布目标Player/AOT与独立UPM安装矩阵；第三方内部安全和性能另立明确审计范围

本模块不把所有metadata、生成物、文档快照或第三方二进制清点称作全面源码审查，也不把未执行的编译与测试写为通过。

