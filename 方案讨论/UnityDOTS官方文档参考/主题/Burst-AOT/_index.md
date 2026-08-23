# Burst-AOT

## 定位

本文件是“Burst-AOT”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [BUR-01：Runtime Core 热路径必须保持可 Burst 编译](<BUR-01.md>)
- [BUR-02：FunctionPointer 仅用于经验证的批量动态算法选择](<BUR-02.md>)
- [BUR-03：Player 性能报告必须包含 AOT、构建配置、架构和预热上下文](<BUR-03.md>)
- [BUR-04：默认不手写 `[NoAlias]`；仅在可证明契约下使用](<BUR-04.md>)
- [BUR-05：Editor Burst 结果不能替代目标 Player AOT 验证](<BUR-05.md>)

## 模式与案例

- [CASE-11：Burst FunctionPointer 用于动态算法选择与批处理](<CASE-11.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
