# EntitiesGraphics

## 定位

本文件是“EntitiesGraphics”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [GFX-01：Core simulation 不得直接访问 Graphics component](<GFX-01.md>)
- [GFX-02：从零创建渲染实体使用 AddComponents；批量创建使用 Instantiate](<GFX-02.md>)
- [GFX-03：Presentation 数据修改只允许在两个例外组中](<GFX-03.md>)
- [GFX-04: 无头模式与有头模式的 Core simulation hash 必须相同](<GFX-04.md>)
- [GFX-05: Rendered profile 必须与 Core 成本独立计时](<GFX-05.md>)

## 模式与案例

- [CASE-10：Graphics runtime create 限于 Presentation 层](<CASE-10.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
