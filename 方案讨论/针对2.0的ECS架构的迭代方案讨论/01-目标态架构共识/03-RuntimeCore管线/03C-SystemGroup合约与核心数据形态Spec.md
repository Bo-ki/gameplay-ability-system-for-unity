# 03C：SystemGroup 合约与核心数据形态

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec | 拆分来源：`../03-RuntimeCore管线Spec.md` | 最近拆分：2026-06-07

本文件只描述理想 Runtime Core 目标态。禁止写入当前代码事实、迁移流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

定位：目标代码骨架中的 SystemGroup 合约、核心组件、buffer、frame context、command/fact/outbox 数据形态。

## 目标代码骨架 — Runtime Core DOTS Kernel

以下代码不是 Editor 逻辑，也不是任何过渡 Runtime 实现的镜像，而是目标态 Runtime Core 的最小可解释骨架。它展示每个 kernel 如何落到 Unity Entities 的真实机制：`SystemGroup`、`ISystem`、`IJobChunk`、`NativeStream`、owner-local `DynamicBuffer`、自定义 ECB 和只读 projection。默认同帧主链是无环的；`Gameplay Fact` 到新 GE command 的反馈写入下一帧 seed，除非显式声明 bounded reaction pass。

### SystemGroup 合约

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(GASCommandResolveSystemGroup))]
    public partial class GASFramePrepareSystemGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GASFramePrepareSystemGroup))]
    [UpdateBefore(typeof(GASCoreSimulationSystemGroup))]
    public partial class GASCommandResolveSystemGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GASCommandResolveSystemGroup))]
    [UpdateBefore(typeof(GASStructuralCommitSystemGroup))]
    public partial class GASCoreSimulationSystemGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GASCoreSimulationSystemGroup))]
    [UpdateBefore(typeof(GASBoundaryProjectionSystemGroup))]
    public partial class GASStructuralCommitSystemGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(GASStructuralCommitSystemGroup), OrderLast = true)]
    public partial class EndGASStructuralCommitECBSystem : EntityCommandBufferSystem { }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GASStructuralCommitSystemGroup))]
    public partial class GASBoundaryProjectionSystemGroup : ComponentSystemGroup { }
}
```

合理性：

1. `ComponentSystemGroup` 只表达物理执行域和 update order，符合 `SYS-02`；不手写 manager tick，不手动调用其他 system 的 `Update()`（`PRF-27`）。
2. 业务 lane 不再一一建立 SystemGroup，避免 `SYS-03` / `PRF-07` 的固定调度成本；lane 内用 system update order + job dependency 表达数据流。
3. 结构变化集中到 `GASStructuralCommitSystemGroup`，符合 `PRF-04` / `ECB-03`；默认只保留帧尾 ECB。
4. `GASFramePrepareSystemGroup` 不保存 query registry；各 `ISystem` 自己在 `OnCreate` 通过 `SystemState.GetEntityQuery` 建 query（`PRF-33`）。

### 核心数据形态

```csharp
using Unity.Entities;

namespace GAS.Runtime
{
    public struct ASCIdentityComponent : IComponentData
    {
        public int PlayerId;
        public int TeamId;
    }

    public struct TagMaskComponent : IComponentData
    {
        public ulong Word0;
        public ulong Word1;
        public ulong Word2;
    }

    public struct TagStatusFlagsComponent : IComponentData
    {
        public ulong Flags;
    }

    public static class AttributeCodes
    {
        public const int Health = 1;
        public const int Shield = 2;
        public const int Attack = 3;
        public const int Defense = 4;
        public const int MagicPower = 5;
        public const int Mana = 101;
        public const int Energy = 102;
    }

    public struct CombatAttributeCurrentSetComponent : IComponentData
    {
        public float Health;
        public float Shield;
        public float Attack;
        public float Defense;
        public float MagicPower;
    }

    public struct CombatAttributeBaseSetComponent : IComponentData
    {
        public float MaxHealth;
        public float MaxShield;
        public float BaseAttack;
        public float BaseDefense;
        public float BaseMagicPower;
    }

    public struct ResourceAttributeCurrentSetComponent : IComponentData
    {
        public float Mana;
        public float Energy;
    }

    public struct ResourceAttributeBaseSetComponent : IComponentData
    {
        public float MaxMana;
        public float MaxEnergy;
        public float ManaRegen;
    }

    public struct AttributeDirtyMaskComponent : IComponentData
    {
        public ulong CombatWord;
        public ulong ResourceWord;
    }

    public static class AttributeDirtyBits
    {
        public const ulong Health = 1ul << 0;
        public const ulong Shield = 1ul << 1;
        public const ulong Attack = 1ul << 2;
        public const ulong Defense = 1ul << 3;
        public const ulong MagicPower = 1ul << 4;
        public const ulong Mana = 1ul << 0;
        public const ulong Energy = 1ul << 1;
    }

    public struct GlobalTimer : IComponentData
    {
        public int Frame;
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

    public enum AbilityCommandStatus : byte
    {
        Pending = 0,
        Valid = 1,
        Rejected = 2,
        Consumed = 3
    }

    public struct AbilityStateComponent : IComponentData
    {
        public Entity OwnerAsc;
        public int AbilityCode;
        public int AbilityDefinitionIndex;
        public short Level;
        public AbilityRuntimeState State;
        public AbilityRuntimeFlags Flags;
        public int ActivationFrame;
        public int CooldownEndFrame;
    }

    public struct AbilityActivationRequestComponent : IComponentData
    {
        public Entity SourceAsc;
        public Entity AbilityEntity;
        public Entity ExplicitTargetAsc;
        public int InputSequence;
        public int RequestFrame;
        public int TargetGroupSortKey;
        public byte TargetMode;
    }

    public struct AbilityCommandComponent : IComponentData
    {
        public Entity SourceAsc;
        public Entity AbilityEntity;
        public Entity ExplicitTargetAsc;
        public int AbilityDefinitionIndex;
        public int PrimaryGameplayEffectCode;
        public int PrimaryGameplayEffectDefinitionIndex;
        public int CostGameplayEffectCode;
        public int CostGameplayEffectDefinitionIndex;
        public int CooldownGameplayEffectCode;
        public int CooldownGameplayEffectDefinitionIndex;
        public short Level;
        public int InputSequence;
        public int RequestFrame;
        public int TargetGroupSortKey;
        public byte TargetMode;
        public AbilityCommandStatus Status;
    }

    public struct AbilityActivationCommandRecord
    {
        public int Sequence;
        public int Frame;
        public Entity SourceAsc;
        public Entity AbilityEntity;
        public Entity ExplicitTargetAsc;
        public int AbilityDefinitionIndex;
        public int PrimaryGameplayEffectCode;
        public int PrimaryGameplayEffectDefinitionIndex;
        public int CostGameplayEffectCode;
        public int CostGameplayEffectDefinitionIndex;
        public int CooldownGameplayEffectCode;
        public int CooldownGameplayEffectDefinitionIndex;
        public short Level;
        public int TargetGroupSortKey;
        public byte TargetMode;
    }

    public struct AbilityTargetRecord
    {
        public int Sequence;
        public int TargetIndex;
        public int TargetSortKey;
        public Entity SourceAsc;
        public Entity SourceAbility;
        public Entity TargetAsc;
        public int GameplayEffectCode;
        public int GameplayEffectDefinitionIndex;
        public short Level;
    }

    public struct AbilityPendingDestroyComponent : IComponentData
    {
        public int ReasonCode;
    }

    public static class GameplayEventCodes
    {
        public const int AttributeChanged = 1;
        public const int DamageResolved = 2;
        public const int CueRequested = 3;
    }

    public enum ActiveEffectSlotState : byte
    {
        Empty = 0,
        PendingApply = 1,
        Active = 2,
        Inhibited = 3,
        PendingRemove = 4
    }

    [System.Flags]
    public enum ActiveEffectSlotFlags : ushort
    {
        None = 0,
        HasDuration = 1 << 0,
        HasPeriod = 1 << 1,
        HasStacking = 1 << 2,
        HasGrantedTags = 1 << 3,
        HasGrantedAbilities = 1 << 4,
        TicksWhenInhibited = 1 << 5
    }

    [InternalBufferCapacity(8)]
    public struct ActiveGameplayEffectBuffer : IBufferElementData
    {
        public int Sequence;
        public int GameplayEffectCode;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public ActiveEffectSlotState State;
        public ActiveEffectSlotState PreviousState;
        public ActiveEffectSlotFlags Flags;
        public short StackCount;
        public int StartFrame;
        public int RemainingFrame;
        public int PeriodFrame;
        public int LastPeriodFrame;
    }

    [InternalBufferCapacity(4)]
    public struct TargetDataBuffer : IBufferElementData
    {
        public Entity TargetAsc;
        public int TargetSortKey;
    }

    public struct GEEffectCommandRecord
    {
        public int Sequence;
        public int SortKey;
        public int Frame;
        public int GameplayEffectCode;
        public int GameplayEffectDefinitionIndex;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public short Level;
    }

    [InternalBufferCapacity(4)]
    public struct GEEffectCommandBuffer : IBufferElementData
    {
        public int Sequence;
        public int SortKey;
        public int Frame;
        public int GameplayEffectCode;
        public int GameplayEffectDefinitionIndex;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public short Level;
    }

    [InternalBufferCapacity(8)]
    public struct AttributeModifierBuffer : IBufferElementData
    {
        public int Sequence;
        public int AttributeCode;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public float Magnitude;
    }

    public readonly struct MagnitudeEvalContext
    {
        public readonly CombatAttributeCurrentSetComponent SourceCombat;
        public readonly CombatAttributeCurrentSetComponent TargetCombat;
        public readonly ResourceAttributeCurrentSetComponent SourceResource;
        public readonly ResourceAttributeCurrentSetComponent TargetResource;
        public readonly float BaseMagnitude;
        public readonly short Level;

        public MagnitudeEvalContext(
            in CombatAttributeCurrentSetComponent sourceCombat,
            in CombatAttributeCurrentSetComponent targetCombat,
            in ResourceAttributeCurrentSetComponent sourceResource,
            in ResourceAttributeCurrentSetComponent targetResource,
            float baseMagnitude,
            short level)
        {
            SourceCombat = sourceCombat;
            TargetCombat = targetCombat;
            SourceResource = sourceResource;
            TargetResource = targetResource;
            BaseMagnitude = baseMagnitude;
            Level = level;
        }
    }

    public static class MagnitudeEvaluatorCodes
    {
        public const int Flat = 0;
        public const int AttackScaleDamage = 1;
        public const int MagicPowerVsDefenseDamage = 2;
        public const int ManaCost = 101;
    }

    public static class GASMagnitudeEvaluator
    {
        public static float Evaluate(in MagnitudeEvalContext context, int evaluatorCode)
        {
            return evaluatorCode switch
            {
                MagnitudeEvaluatorCodes.Flat => context.BaseMagnitude,
                MagnitudeEvaluatorCodes.AttackScaleDamage => -(context.SourceCombat.Attack * context.BaseMagnitude),
                MagnitudeEvaluatorCodes.MagicPowerVsDefenseDamage => -math.max(1f, context.SourceCombat.MagicPower * context.BaseMagnitude - context.TargetCombat.Defense * 0.3f),
                MagnitudeEvaluatorCodes.ManaCost => -math.abs(context.BaseMagnitude),
                _ => context.BaseMagnitude
            };
        }
    }

    [InternalBufferCapacity(4)]
    public struct GameplayEventBuffer : IBufferElementData
    {
        public int Sequence;
        public int EventCode;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public float Value;
    }

    [InternalBufferCapacity(4)]
    public struct PresentationEventBuffer : IBufferElementData
    {
        public int Sequence;
        public int EventCode;
        public Entity TargetAsc;
        public float Value;
    }
}
```

合理性：

1. ASC 的跨帧权威状态是 `ASCIdentityComponent`、attribute component、`TagMaskComponent`、`ActiveGameplayEffectBuffer`；每帧瞬时数据默认小容量 owner-local buffer 或 `NativeStream`，不再默认大 singleton buffer。
2. `AbilityStateComponent` 保存 `AbilityCode` + `AbilityDefinitionIndex`，不是复制整份 Ability config；Ability grant 低频解析 index，activation hot path 通过 Definition Catalog 读 `ref readonly AbilityDefinitionBlob`。
3. `ActiveGameplayEffectBuffer` 使用 enum + bit flags，符合 `FSM-02` / `FSM-05`；不为每个状态或 buff/debuff 建独立 component。
4. `InternalBufferCapacity` 小而明确，符合 `BUF-01` / `PRF-10`；大规模 fan-in 用 `NativeStream`，不是把每个 ASC 都塞进大 inline buffer。
