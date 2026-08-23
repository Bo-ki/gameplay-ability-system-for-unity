# 10B-01 场景、单位、属性与 Tag

## 单位

每个棋子拥有一个稳定 ASC；表现/物理 Avatar 可重绑。单位 Definition 至少配置阵营、职业/种族、初始 Attribute、初始 Tag、初始 GrantedAbility 和初始 ActiveEffect。Spawn 时根据 Session Layout 一次初始化所有 buffers/slabs。

## AttributeLayout

案例最小属性：`Health`、`MaxHealth`、`Attack`、`Defense`、`AttackSpeed`、`Mana`、`MaxMana`、`MoveSpeed`。每个 ASC 的 `AttributeValueSlot[]` 长度等于 Session AttributeLayout.Count；Definition/Effect 中已预解析 index。

Health 遵守 `0 <= Current <= MaxHealth`；MaxHealth 变化的 Health 调整 policy 必须配置化并进入 hash。Mana 与攻速同样通过 Aggregator contribution 变化，不生成 per-attribute component。

## TagCatalog

案例至少包含：

```text
State.Alive
State.Dead
State.Stunned
State.Poisoned
Unit.Faction.Ally / Enemy
Unit.Class.Warrior / Mage / Assassin
Ability.Blocked.Action
Cue.Hit / Stun / FrostNova / Poison / Death
```

`TagCountSlot` 维护 Exact/Inclusive count；`State.Stunned` 的多个来源各自记账，移除一个来源不应清除其他来源。父 Tag 查询使用 InclusiveCount，Exact 查询使用 ExactCount。

## 物理与身份

空间查询可以命中 Avatar Entity，但立即解析为 StableAvatarId/BindingGeneration/AscInstanceId。Boundary fact 只携带稳定身份和需要的空间快照。Avatar Destroy/替换不销毁 ASC 状态。

## 验收

- 初始化后每个 buffer 长度与 Layout/Catalog 一致，tick 内零 resize。
- 多来源 stun/poison grant/remove 的 Exact/Inclusive count 正确。
- Avatar 重绑后 Ability/Effect/Tag/Attribute 保留，旧 binding 的表现事件可识别 stale。
- x1000 内存由生成报告与 Memory Profiler 对账。
