# EN-01: 高频开关优先 Enableable，低频生命周期再考虑 Add/Remove

**严重度**: P0
**Primary Owner**: Enableable-Component选型
**来源**: `components-enableable-use.html`; `主题/03-结构变化-ECB-Enableable.md`

## 规则声明
场景需要高频（每帧多次）切换 entity 状态时（active/inactive、alive/dead、tag on/off），必须使用 `IEnableableComponent` 而非 `AddComponent`/`RemoveComponent`。仅当操作是低频的实体生命周期变更（创建时一次性添加、销毁前一次性移除）才使用 Add/Remove。

## 为什么
Enableable toggle 只修改 enabled bit，无结构变化、无 archetype 迁移、无 sync point。Add/Remove 每次触发完整的 archetype 迁移与数据复制，高频执行导致 chunk 碎片化和 sync point 堆积。

## EX-GAS 诊断
ActiveEffect 的 duration 到期切换、ability cooldown 状态切换、attribute modifier 的 granted tag 开关——这些高频状态切换当前可能使用 Add/Remove，应统一改为 Enableable。

## 检查方法
搜索高频路径（每帧执行）中出现 `AddComponent` / `RemoveComponent` 的位置，评估是否可替换为 `SetComponentEnabled`。Debugger 输出 `enableableToggleCount` 和 `componentAddRemoveCount` 作为对比指标。
