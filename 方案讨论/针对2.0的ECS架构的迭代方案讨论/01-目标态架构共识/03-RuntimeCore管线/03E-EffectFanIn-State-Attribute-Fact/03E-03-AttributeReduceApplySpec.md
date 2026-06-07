# 03E-03：Attribute Reduce / Apply

> Owner：`01-目标态架构共识/03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-Fact` | 状态：目标态 Spec 子页 | 最近拆分：2026-06-08

本文件只描述目标态 Attribute Reduce / Apply。当前代码事实、验证数字、执行流水和迁移 proof 不写入本文件。

## 定位

Attribute Reduce / Apply 负责消费 Effect Fan-In / Magnitude Resolve 后的 target grouped modifier 输入，只写本 chunk 内 target ASC 的 AttributeSet、dirty mask 和 fact buffer。

## 目标代码骨架

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectPostApplySystem))]
    public partial struct GASAttributeSetReduceApplySystem : ISystem
    {
        private EntityQuery _targetQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CombatAttributeCurrentSetComponent>(),
                    ComponentType.ReadOnly<CombatAttributeBaseSetComponent>(),
                    ComponentType.ReadWrite<ResourceAttributeCurrentSetComponent>(),
                    ComponentType.ReadOnly<ResourceAttributeBaseSetComponent>(),
                    ComponentType.ReadWrite<AttributeDirtyMaskComponent>(),
                    ComponentType.ReadWrite<AttributeModifierBuffer>(),
                    ComponentType.ReadWrite<GameplayEventBuffer>()
                }
            });

            state.RequireForUpdate(_targetQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ApplyAttributeSetModifiersJob
            {
                CombatCurrentType = state.GetComponentTypeHandle<CombatAttributeCurrentSetComponent>(false),
                CombatBaseType = state.GetComponentTypeHandle<CombatAttributeBaseSetComponent>(true),
                ResourceCurrentType = state.GetComponentTypeHandle<ResourceAttributeCurrentSetComponent>(false),
                ResourceBaseType = state.GetComponentTypeHandle<ResourceAttributeBaseSetComponent>(true),
                DirtyMaskType = state.GetComponentTypeHandle<AttributeDirtyMaskComponent>(false),
                ModifierBufferType = state.GetBufferTypeHandle<AttributeModifierBuffer>(false),
                FactBufferType = state.GetBufferTypeHandle<GameplayEventBuffer>(false)
            }.ScheduleParallel(_targetQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyAttributeSetModifiersJob : IJobChunk
        {
            public ComponentTypeHandle<CombatAttributeCurrentSetComponent> CombatCurrentType;
            [ReadOnly] public ComponentTypeHandle<CombatAttributeBaseSetComponent> CombatBaseType;
            public ComponentTypeHandle<ResourceAttributeCurrentSetComponent> ResourceCurrentType;
            [ReadOnly] public ComponentTypeHandle<ResourceAttributeBaseSetComponent> ResourceBaseType;
            public ComponentTypeHandle<AttributeDirtyMaskComponent> DirtyMaskType;
            public BufferTypeHandle<AttributeModifierBuffer> ModifierBufferType;
            public BufferTypeHandle<GameplayEventBuffer> FactBufferType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var combatValues = chunk.GetNativeArray(ref CombatCurrentType);
                var combatBaseValues = chunk.GetNativeArray(ref CombatBaseType);
                var resourceValues = chunk.GetNativeArray(ref ResourceCurrentType);
                var resourceBaseValues = chunk.GetNativeArray(ref ResourceBaseType);
                var dirtyMasks = chunk.GetNativeArray(ref DirtyMaskType);
                var modifiersByTarget = chunk.GetBufferAccessor(ref ModifierBufferType);
                var factsByTarget = chunk.GetBufferAccessor(ref FactBufferType);

                if (!useEnabledMask)
                {
                    for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                    {
                        ApplyEntity(
                            entityIndex,
                            unfilteredChunkIndex,
                            combatValues,
                            combatBaseValues,
                            resourceValues,
                            resourceBaseValues,
                            dirtyMasks,
                            modifiersByTarget,
                            factsByTarget);
                    }

                    return;
                }

                var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    ApplyEntity(
                        entityIndex,
                        unfilteredChunkIndex,
                        combatValues,
                        combatBaseValues,
                        resourceValues,
                        resourceBaseValues,
                        dirtyMasks,
                        modifiersByTarget,
                        factsByTarget);
                }
            }

            private static void ApplyEntity(
                int entityIndex,
                int unfilteredChunkIndex,
                NativeArray<CombatAttributeCurrentSetComponent> combatValues,
                NativeArray<CombatAttributeBaseSetComponent> combatBaseValues,
                NativeArray<ResourceAttributeCurrentSetComponent> resourceValues,
                NativeArray<ResourceAttributeBaseSetComponent> resourceBaseValues,
                NativeArray<AttributeDirtyMaskComponent> dirtyMasks,
                BufferAccessor<AttributeModifierBuffer> modifiersByTarget,
                BufferAccessor<GameplayEventBuffer> factsByTarget)
            {
                var combat = combatValues[entityIndex];
                var combatBase = combatBaseValues[entityIndex];
                var resource = resourceValues[entityIndex];
                var resourceBase = resourceBaseValues[entityIndex];
                var dirty = dirtyMasks[entityIndex];
                var modifiers = modifiersByTarget[entityIndex];
                var facts = factsByTarget[entityIndex];

                if (modifiers.Length == 0)
                    return;

                for (var i = 0; i < modifiers.Length; i++)
                {
                    if (!ApplyModifier(ref combat, in combatBase, ref resource, in resourceBase, ref dirty, modifiers[i], out var appliedDelta))
                        continue;

                    facts.Add(new GameplayEventBuffer
                    {
                        Sequence = ((unfilteredChunkIndex & 0x7FFF) << 17) | (entityIndex << 8) | (i & 0xFF),
                        EventCode = GameplayEventCodes.AttributeChanged,
                        SourceAsc = modifiers[i].SourceAsc,
                        TargetAsc = modifiers[i].TargetAsc,
                        Value = appliedDelta
                    });
                }

                combatValues[entityIndex] = combat;
                resourceValues[entityIndex] = resource;
                dirtyMasks[entityIndex] = dirty;
                modifiers.Clear();
            }

            private static bool ApplyModifier(
                ref CombatAttributeCurrentSetComponent combat,
                in CombatAttributeBaseSetComponent combatBase,
                ref ResourceAttributeCurrentSetComponent resource,
                in ResourceAttributeBaseSetComponent resourceBase,
                ref AttributeDirtyMaskComponent dirty,
                in AttributeModifierBuffer modifier,
                out float appliedDelta)
            {
                appliedDelta = 0f;

                switch (modifier.AttributeCode)
                {
                    case AttributeCodes.Health:
                    {
                        var oldValue = combat.Health;
                        combat.Health = math.clamp(oldValue + modifier.Magnitude, 0f, combatBase.MaxHealth);
                        appliedDelta = combat.Health - oldValue;
                        if (appliedDelta != 0f)
                            dirty.CombatWord |= AttributeDirtyBits.Health;
                        return appliedDelta != 0f;
                    }
                    case AttributeCodes.Shield:
                    {
                        var oldValue = combat.Shield;
                        combat.Shield = math.clamp(oldValue + modifier.Magnitude, 0f, combatBase.MaxShield);
                        appliedDelta = combat.Shield - oldValue;
                        if (appliedDelta != 0f)
                            dirty.CombatWord |= AttributeDirtyBits.Shield;
                        return appliedDelta != 0f;
                    }
                    case AttributeCodes.Attack:
                    {
                        var oldValue = combat.Attack;
                        combat.Attack = math.max(0f, oldValue + modifier.Magnitude);
                        appliedDelta = combat.Attack - oldValue;
                        if (appliedDelta != 0f)
                            dirty.CombatWord |= AttributeDirtyBits.Attack;
                        return appliedDelta != 0f;
                    }
                    case AttributeCodes.Defense:
                    {
                        var oldValue = combat.Defense;
                        combat.Defense = math.max(0f, oldValue + modifier.Magnitude);
                        appliedDelta = combat.Defense - oldValue;
                        if (appliedDelta != 0f)
                            dirty.CombatWord |= AttributeDirtyBits.Defense;
                        return appliedDelta != 0f;
                    }
                    case AttributeCodes.MagicPower:
                    {
                        var oldValue = combat.MagicPower;
                        combat.MagicPower = math.max(0f, oldValue + modifier.Magnitude);
                        appliedDelta = combat.MagicPower - oldValue;
                        if (appliedDelta != 0f)
                            dirty.CombatWord |= AttributeDirtyBits.MagicPower;
                        return appliedDelta != 0f;
                    }
                    case AttributeCodes.Mana:
                    {
                        var oldValue = resource.Mana;
                        resource.Mana = math.clamp(oldValue + modifier.Magnitude, 0f, resourceBase.MaxMana);
                        appliedDelta = resource.Mana - oldValue;
                        if (appliedDelta != 0f)
                            dirty.ResourceWord |= AttributeDirtyBits.Mana;
                        return appliedDelta != 0f;
                    }
                    case AttributeCodes.Energy:
                    {
                        var oldValue = resource.Energy;
                        resource.Energy = math.clamp(oldValue + modifier.Magnitude, 0f, resourceBase.MaxEnergy);
                        appliedDelta = resource.Energy - oldValue;
                        if (appliedDelta != 0f)
                            dirty.ResourceWord |= AttributeDirtyBits.Energy;
                        return appliedDelta != 0f;
                    }
                    default:
                        return false;
                }
            }
        }
    }
}
```

## 合理性

1. 每个 job 只写自己 chunk 内 target ASC 的 AttributeSet 和 fact buffer，避免 `ComponentLookup` 随机写竞态。
2. Attribute current 与 base / config 分离，保持 read-only / read-write 数据粒度清晰；写 Current 不应把 Base 的 reactive consumer 误触发。
3. 不再按 `Health/Mana/Attack/...` 生成同形 apply system，避免 system 固定成本、重复 lookup/type handle 和更复杂 `JobHandle` 链。
4. `AttributeModifierBuffer` 是 Effect Fan-In / Magnitude Resolve 后的 target grouped 输入；MMC 读取 source / target snapshot 的随机访问发生在写属性之前，Apply lane 不再 random write 其他 ASC。
5. `if (!useEnabledMask) for ... else ChunkEntityEnumerator` 同时保留无 enableable query 的普通 for 快路径，以及未来加入 enableable filter 时的 disabled entity 正确性。

## 与 EntityComponent 物理布局的关系

1. [13 EntityComponent 物理布局](../../13-EntityComponent物理布局Spec.md) 维护 AttributeSet、buffer、dirty mask 的全局物理布局和命名约束。
2. 本文件只维护 Attribute Apply lane 对这些 component / buffer 的读写顺序和 owner-local 写入规则。
3. 如果 AttributeSet 打包策略变化，先更新 `13`，再由本文件调整目标代码骨架。

## 反向入口

- 03E 子页索引：[README.md](README.md)
- 03E 根索引：[../03E-EffectFanIn-State-Attribute-FactSpec.md](../03E-EffectFanIn-State-Attribute-FactSpec.md)
