# 10B-02：技能与 GameplayEffect 目标设计

> Owner：`01-目标态架构共识/10B-AutoChess完整业务案例` | 状态：目标态子 Spec | 拆分来源：`../10B-AutoChess完整业务案例设计Spec.md` | 最近拆分：2026-06-07

本文件只描述 AutoChess 完整业务案例的目标态设计。禁止写入当前代码事实、执行流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。
## 五、技能设计

### 5.1 技能 Luban 配置（`autochess.ability.xlsx`）

| AbilityId | Name | Type | ManaCost | CooldownFrames | TargetRule | MaxTargets | Range | EffectId_Primary | EffectId_Secondary | BlockTags |
|-----------|------|------|----------|----------------|------------|------------|-------|------------------|--------------------|------------|
| 3001 | 盾击 | Active | 40 | 180 (3s@60fps) | NearestEnemy | 1 | 1.5 | 5001 | 5002 | Stunned,Frozen |
| 3002 | 冰霜新星 | Active | 80 | 300 (5s@60fps) | NearestEnemies | 3 | 3.0 | 5003 | 5004 | Stunned,Frozen |
| 3003 | 毒刃 | Active | 60 | 120 (2s@60fps) | NearestEnemy | 1 | 1.5 | 5005 | — | Stunned,Frozen |
| 3004 | 圣光治疗 | Active | 40 | 240 (4s@60fps) | LowestHPAlly | 1 | 4.0 | 5006 | — | Stunned,Frozen |
| 0 | 普攻 | PassiveAuto | 0 | — | NearestEnemy | 1 | 按职业 | 4001 | — | Stunned,Frozen |

**普攻是 default ability**：每个棋子都有，不占 AbilityId 槽位。普攻通过 `AttackAbilityDataComponent` component 配置其伤害 GE 和攻击范围。

### 5.2 技能 BlobAsset 结构

```csharp
// ============================================================
// [Layer 4: Definition & Generation]
// Ability 静态定义 BlobAsset
// ============================================================

public struct AbilityDefBlob
{
    public int AbilityId;
    public FixedString32Bytes Name;
    public byte AbilityType;     // 0=PassiveAuto, 1=Active
    public float ManaCost;
    public float CooldownFrames;
    public byte TargetRule;      // 0=NearestEnemy, 1=LowestHPAlly, 2=NearestEnemies
    public byte MaxTargets;
    public float Range;
    public int EffectIdPrimary;  // 主 GE ID
    public int EffectIdSecondary;// 副 GE ID（如盾击：主=伤害GE，副=眩晕GE）
    public ulong BlockTagBits;   // 自身有此 tag 时禁止释放
}

public enum AbilityRuntimeState : byte
{
    Granted = 0,
    Ready = 1,
    Active = 2,
    Cooldown = 3,
    Ending = 4
}

[System.Flags]
public enum AbilityRuntimeFlags : ushort
{
    None = 0,
    Executable = 1 << 0,
    Activating = 1 << 1,
    Blocked = 1 << 2
}

// Ability Entity 上挂载的跨帧 granted ability 状态
public struct AbilityStateComponent : IComponentData
{
    public Entity OwnerAsc;
    public int AbilityId;
    public int PrimaryGameplayEffectCode;
    public int SecondaryGameplayEffectCode;
    public short Level;
    public AbilityRuntimeState State;
    public AbilityRuntimeFlags Flags;
    public int CooldownEndFrame;
}
```

---

## 六、GE 设计

### 6.1 GE Luban 配置（`autochess.gameplay_effect.xlsx`）

| GEId | Name | Type | DurationFrames | PeriodFrames | StackLimit | StackPolicy | ModifierAttr | ModifierOp | ModifierMmc | GrantedTags | RemoveTags | ApplicationTagReq | ImmunityTags |
|------|------|------|----------------|--------------|------------|-------------|-------------|------------|-------------|-------------|------------|-------------------|--------------|
| 4001 | 普攻伤害 | Instant | 0 | 0 | — | — | HP | Minus | ATK*1.0 | — | — | — | — |
| 4002 | 冰系减速 | Duration | 180 (3s) | 0 | — | — | ASPD | Multiply | 0.7 | Slowed | — | — | — |
| 5001 | 盾击伤害 | Instant | 0 | 0 | — | — | HP | Minus | ATK*0.8 | — | — | — | — |
| 5002 | 眩晕 | Duration | 120 (2s) | 0 | — | — | ASPD | Override | 0.0 | Stunned | — | — | — |
| 5003 | 冰霜新星伤害 | Instant | 0 | 0 | — | — | HP | Minus | MagicPower*1.5-DEF*0.3 | — | — | — | — |
| 5004 | 冰霜减速 | Duration | 180 (3s) | 0 | — | — | ASPD | Multiply | 0.7 | Slowed | — | — | Frozen |
| 5005 | 毒刃 | Duration | 240 (4s) | 60 (1s) | 3 | SourceAggregate | HP | Minus | ATK*0.3 | Poisoned | — | — | — |
| 5006 | 圣光治疗 | Instant | 0 | 0 | — | — | HP | Add | MagicPower*0.8 | — | — | — | — |

**MMC 说明：**
- `ATK*1.0`：取 Source ASC 的 ATK CurrentValue × 1.0
- `MagicPower*1.5-DEF*0.3`：取 Source MagicPower × 1.5 − Target DEF × 0.3
- `ATK*0.8`：取 Source ATK × 0.8
- `ATK*0.3`：取 Source ATK × 0.3
- `MagicPower*0.8`：取 Source MagicPower × 0.8
- `0.7`：固定倍率（Multiply 操作，ASPD × 0.7）
- `0.0`：固定值（Override 操作，ASPD = 0）

### 6.2 GE BlobAsset 构建（SourceGenerator 生成）

```csharp
// ============================================================
// [Layer 4: Definition & Generation]
// 目标输出路径: Assets/AutoChessDemo/Config/GeneratedRuntime/AutoChessBlobBuilders.g.cs
// 由 Luban 读取 GE 配置 → SourceGenerator 生成 BlobAsset 构建代码
// ============================================================

public enum GEType : byte { Instant = 0, Duration = 1 }
public enum GEOperation : byte { Add = 0, Minus = 1, Multiply = 2, Override = 3 }
public enum GEStackPolicy : byte { None = 0, SourceAggregate = 1, TargetAggregate = 2 }

public struct GEStaticBlob
{
    public int GeId;
    public GEType GeType;
    public float DurationFrames;       // 0 = Instant
    public float PeriodFrames;         // 0 = 无 Period
    public byte StackLimit;            // 0 = 不可叠加
    public GEStackPolicy StackPolicy;

    public BlobArray<GEModifierBlob> Modifiers;
    public BlobArray<ulong> GrantedTags;   // GE 激活时授予的 tag bits
    public BlobArray<ulong> RemoveTags;    // GE 激活时移除的 tag bits
    public ulong ApplicationTagReqBits;    // 目标必须有这些 tag 才能施加
    public ulong ImmunityTagBits;          // 目标有这些 tag 则免疫
    public FixedString32Bytes Name;
}

public struct GEModifierBlob
{
    public int AttrCode;         // XAttr.HP / XAttr.ATK / ...
    public GEOperation Operation;
    public byte MmcTypeId;       // generated static evaluator code
    public float BaseMagnitude;  // 固定值（如 0.7, 0.0）
}

// ============================================================
// GE_5003: 冰霜新星伤害（Instant，MMC = MagicPower*1.5 - DEF*0.3）
// ============================================================
public static class GEBlobBuilder
{
    public static BlobAssetReference<GEStaticBlob> Build_FrostNovaDamage()
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<GEStaticBlob>();

        root.GeId = 5003;
        root.GeType = GEType.Instant;
        root.DurationFrames = 0;
        root.PeriodFrames = 0;
        root.StackLimit = 0;
        root.StackPolicy = GEStackPolicy.None;
        root.ApplicationTagReqBits = 0;
        root.ImmunityTagBits = 0;
        root.Name = "冰霜新星伤害";

        var modifiers = builder.Allocate(ref root.Modifiers, 1);
        modifiers[0] = new GEModifierBlob
        {
            AttrCode = XAttr.HP,
            Operation = GEOperation.Minus,
            MmcTypeId = MmcTypeId.MagicPowerMulDefReduc, // static evaluator code
            BaseMagnitude = 0f,
        };

        builder.Allocate(ref root.GrantedTags, 0);
        builder.Allocate(ref root.RemoveTags, 0);

        var result = builder.CreateBlobAssetReference<GEStaticBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }

    // ============================================================
    // GE_5005: 毒刃（Duration 4s, Period 1s, StackLimit=3, SourceAggregate）
    // ============================================================
    public static BlobAssetReference<GEStaticBlob> Build_PoisonBlade()
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<GEStaticBlob>();

        root.GeId = 5005;
        root.GeType = GEType.Duration;
        root.DurationFrames = 240f;   // 4s @ 60fps
        root.PeriodFrames = 60f;      // 1s @ 60fps
        root.StackLimit = 3;
        root.StackPolicy = GEStackPolicy.SourceAggregate;
        root.ApplicationTagReqBits = 0;
        root.ImmunityTagBits = 0;
        root.Name = "毒刃";

        var modifiers = builder.Allocate(ref root.Modifiers, 1);
        modifiers[0] = new GEModifierBlob
        {
            AttrCode = XAttr.HP,
            Operation = GEOperation.Minus,
            MmcTypeId = MmcTypeId.AtkScale03, // ATK * 0.3
            BaseMagnitude = 0f,
        };

        var grantedTags = builder.Allocate(ref root.GrantedTags, 1);
        grantedTags[0] = XTagBit.Poisoned;

        builder.Allocate(ref root.RemoveTags, 0);

        var result = builder.CreateBlobAssetReference<GEStaticBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }

    // ============================================================
    // GE_5002: 眩晕（Duration 2s，Override ASPD=0，授予 Stunned tag）
    // ============================================================
    public static BlobAssetReference<GEStaticBlob> Build_Stun()
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<GEStaticBlob>();

        root.GeId = 5002;
        root.GeType = GEType.Duration;
        root.DurationFrames = 120f;   // 2s @ 60fps
        root.PeriodFrames = 0;
        root.StackLimit = 0;
        root.StackPolicy = GEStackPolicy.None;
        root.ApplicationTagReqBits = 0;
        root.ImmunityTagBits = XTagBit.Frozen; // 冰冻免疫眩晕
        root.Name = "眩晕";

        var modifiers = builder.Allocate(ref root.Modifiers, 1);
        modifiers[0] = new GEModifierBlob
        {
            AttrCode = XAttr.ASPD,
            Operation = GEOperation.Override,
            MmcTypeId = MmcTypeId.Flat,         // 固定值
            BaseMagnitude = 0f,                  // ASPD → 0
        };

        var grantedTags = builder.Allocate(ref root.GrantedTags, 1);
        grantedTags[0] = XTagBit.Stunned;

        builder.Allocate(ref root.RemoveTags, 0);

        var result = builder.CreateBlobAssetReference<GEStaticBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }

    // ============================================================
    // GE_5006: 圣光治疗（Instant，HP += MagicPower*0.8）
    // ============================================================
    public static BlobAssetReference<GEStaticBlob> Build_HolyLightHeal()
    {
        var builder = new BlobBuilder(Allocator.Temp);
        ref var root = ref builder.ConstructRoot<GEStaticBlob>();

        root.GeId = 5006;
        root.GeType = GEType.Instant;
        root.DurationFrames = 0;
        root.PeriodFrames = 0;
        root.StackLimit = 0;
        root.StackPolicy = GEStackPolicy.None;
        root.ApplicationTagReqBits = 0;
        root.ImmunityTagBits = 0;
        root.Name = "圣光治疗";

        var modifiers = builder.Allocate(ref root.Modifiers, 1);
        modifiers[0] = new GEModifierBlob
        {
            AttrCode = XAttr.HP,
            Operation = GEOperation.Add,
            MmcTypeId = MmcTypeId.MagicPowerScale08,
            BaseMagnitude = 0f,
        };

        builder.Allocate(ref root.GrantedTags, 0);
        builder.Allocate(ref root.RemoveTags, 0);

        var result = builder.CreateBlobAssetReference<GEStaticBlob>(Allocator.Persistent);
        builder.Dispose();
        return result;
    }
}
```

### 6.3 MMC 静态 Evaluator

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core]
// MMC 默认使用 generated static switch；FunctionPointer 只允许同类型大批量 batch
// 不变量 29: 不使用托管 delegate 或可变静态注册表
// ============================================================

public static class MmcTypeId
{
    public const byte Flat                = 0;
    public const byte AtkScale10          = 1;
    public const byte AtkScale08          = 2;
    public const byte AtkScale03          = 3;
    public const byte MagicPowerScale08   = 4;
    public const byte MagicPowerMulDefReduc = 5; // MagicPower*1.5 - TargetDEF*0.3
}

[BurstCompile]
public static class MmcEvaluator
{
    // GE 施加时调用：计算 modifier 的实际 magnitude
    // source/target AttributeSet snapshot 在 Magnitude Resolve lane 只读采集；
    // Attribute Apply lane 不再通过 ComponentLookup 随机写其他 ASC。
    [BurstCompile]
    public static float Evaluate(
        byte mmcTypeId,
        float baseMagnitude,
        in AutoChessCombatAttributeCurrentSetComponent sourceCombat,
        in AutoChessResourceAttributeCurrentSetComponent sourceResource,
        in AutoChessCombatAttributeCurrentSetComponent targetCombat,
        in AutoChessResourceAttributeCurrentSetComponent targetResource)
    {
        return mmcTypeId switch
        {
            MmcTypeId.Flat                => baseMagnitude,
            MmcTypeId.AtkScale10          => sourceCombat.Attack * 1.0f,
            MmcTypeId.AtkScale08          => sourceCombat.Attack * 0.8f,
            MmcTypeId.AtkScale03          => sourceCombat.Attack * 0.3f,
            MmcTypeId.MagicPowerScale08   => sourceCombat.Attack * 0.8f,  // demo 暂用 Attack 代表法强输入
            MmcTypeId.MagicPowerMulDefReduc => sourceCombat.Attack * 1.5f - targetCombat.Defense * 0.3f,
            _ => baseMagnitude,
        };
    }
}
```

---
