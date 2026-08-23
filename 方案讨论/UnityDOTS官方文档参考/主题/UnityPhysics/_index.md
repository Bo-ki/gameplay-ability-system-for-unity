# UnityPhysics

## 定位

本文件是“UnityPhysics”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [PHY-01：Physics pipeline 内保持物理实体布局稳定](<PHY-01.md>)
- [PHY-02：Physics 适配器只产出输入，GAS Core 拥有规则裁决](<PHY-02.md>)
- [PHY-03：Collision/Trigger event 引用不能跨下一次 simulation 更新](<PHY-03.md>)
- [PHY-04：Physics 查询必须声明 broadphase 构建时点](<PHY-04.md>)
- [PHY-05：Physics 与 Core 成本分离统计](<PHY-05.md>)

## 模式与案例

- [CASE-09：Physics query 产出 GAS 候选目标](<CASE-09.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
