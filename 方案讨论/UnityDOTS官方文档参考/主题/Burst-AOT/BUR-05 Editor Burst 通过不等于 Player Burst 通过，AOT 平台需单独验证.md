# BUR-05: Editor Burst 通过不等于 Player Burst 通过，AOT 平台需单独验证

**严重度**: P1
**Primary Owner**: Burst-AOT
**来源**: `09-Burst-编译-向量化-AOT.md` — 常见陷阱

## 规则声明
Editor 中 Burst 编译通过不代表 Player 中能正确运行。iOS、consoles 等 AOT 平台有更多限制（如泛型特化、指针操作、FunctionPointer 可用性差异），必须在目标平台进行验证。

## 为什么
Editor 使用 JIT 模式，Burst 可动态生成代码；AOT 平台在编译时就需要完成所有代码生成，限制更严格。泛型特化、FunctionPointer 引用等在 AOT 平台可能失败。

## EX-GAS 诊断
AutoChess 目标平台列表中必须包含至少一个 AOT 平台的构建验证步骤。iOS/Android Player build 加入 CI 流水线。

## 检查方法
CI 配置中包含 AOT 平台（如 iOS）的 Build + Burst 编译验证。Burst Inspector 的 AOT 编译日志无 Error/Warning。
