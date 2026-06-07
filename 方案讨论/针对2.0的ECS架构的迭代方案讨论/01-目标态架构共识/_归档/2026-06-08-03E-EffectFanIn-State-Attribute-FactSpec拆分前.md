# 03E：Effect Fan-In / State / Attribute / Fact

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec | 拆分来源：`../03-RuntimeCore管线Spec.md` | 最近拆分：2026-06-07

本文件只描述理想 Runtime Core 目标态。禁止写入当前代码事实、迁移流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

定位：目标态 effect fan-in、active effect store、attribute reduce/apply 和 gameplay fact 数据流。

### Effect Fan-In：NativeStream + deterministic merge

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    public partial struct GASEffectFanInSystem : ISystem
    {
        private EntityQuery _producerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _producerQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AbilityCommandComponent>(),
                    ComponentType.ReadOnly<TargetDataBuffer>()
                }
            });

            state.RequireForUpdate<GlobalTimer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var chunkCount = _producerQuery.CalculateChunkCount();
            if (chunkCount == 0)
                return;

            var commandStream = new NativeStream(chunkCount, Allocator.TempJob);
            var sortedCommands = new NativeList<GEEffectCommandRecord>(Allocator.TempJob);
            var collectJob = new CollectEffectCommandsJob
            {
                CommandType = state.GetComponentTypeHandle<AbilityCommandComponent>(true),
                TargetBufferType = state.GetBufferTypeHandle<TargetDataBuffer>(true),
                CommandWriter = commandStream.AsWriter(),
                Frame = SystemAPI.GetSingleton<GlobalTimer>().Frame
            }.ScheduleParallel(_producerQuery, state.Dependency);

            var ownerCommands = state.GetBufferLookup<GEEffectCommandBuffer>(false);
            var mergeJob = new MergeEffectCommandsJob
            {
                CommandReader = commandStream.AsReader(),
                SortedCommands = sortedCommands,
                OwnerCommandBuffers = ownerCommands
            }.Schedule(collectJob);

            var disposeStreamJob = commandStream.Dispose(mergeJob);
            state.Dependency = sortedCommands.Dispose(disposeStreamJob);
        }

        [BurstCompile]
        private struct CollectEffectCommandsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<AbilityCommandComponent> CommandType;
            [ReadOnly] public BufferTypeHandle<TargetDataBuffer> TargetBufferType;
            public NativeStream.Writer CommandWriter;
            public int Frame;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var commands = chunk.GetNativeArray(ref CommandType);
                var targets = chunk.GetBufferAccessor(ref TargetBufferType);

                CommandWriter.BeginForEachIndex(unfilteredChunkIndex);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    var command = commands[entityIndex];
                    if (command.Status != AbilityCommandStatus.Valid)
                        continue;

                    WriteEffectCommand(
                        command,
                        (command.InputSequence << 16) ^ (unfilteredChunkIndex << 8) ^ 0x01,
                        command.TargetGroupSortKey,
                        command.CostGameplayEffectCode,
                        command.CostGameplayEffectDefinitionIndex,
                        command.SourceAsc);

                    WriteEffectCommand(
                        command,
                        (command.InputSequence << 16) ^ (unfilteredChunkIndex << 8) ^ 0x02,
                        command.TargetGroupSortKey,
                        command.CooldownGameplayEffectCode,
                        command.CooldownGameplayEffectDefinitionIndex,
                        command.SourceAsc);

                    var targetList = targets[entityIndex];

                    for (var targetIndex = 0; targetIndex < targetList.Length; targetIndex++)
                    {
                        var target = targetList[targetIndex];
                        var sequence = (command.InputSequence << 16) ^ (unfilteredChunkIndex << 8) ^ (targetIndex + 0x100);
                        WriteEffectCommand(
                            command,
                            sequence,
                            target.TargetSortKey,
                            command.PrimaryGameplayEffectCode,
                            command.PrimaryGameplayEffectDefinitionIndex,
                            target.TargetAsc);
                    }
                }

                CommandWriter.EndForEachIndex();
            }

            private void WriteEffectCommand(
                in AbilityCommandComponent command,
                int sequence,
                int sortKey,
                int gameplayEffectCode,
                int gameplayEffectDefinitionIndex,
                Entity targetAsc)
            {
                if (gameplayEffectCode == 0 || gameplayEffectDefinitionIndex < 0)
                    return;

                CommandWriter.Write(new GEEffectCommandRecord
                {
                    Sequence = sequence,
                    SortKey = sortKey,
                    Frame = Frame,
                    GameplayEffectCode = gameplayEffectCode,
                    GameplayEffectDefinitionIndex = gameplayEffectDefinitionIndex,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = targetAsc,
                    SourceAbility = command.AbilityEntity,
                    Level = command.Level
                });
            }
        }

        [BurstCompile]
        private struct MergeEffectCommandsJob : IJob
        {
            [ReadOnly] public NativeStream.Reader CommandReader;
            public NativeList<GEEffectCommandRecord> SortedCommands;
            public BufferLookup<GEEffectCommandBuffer> OwnerCommandBuffers;

            public void Execute()
            {
                for (var forEachIndex = 0; forEachIndex < CommandReader.ForEachCount; forEachIndex++)
                {
                    CommandReader.BeginForEachIndex(forEachIndex);
                    while (CommandReader.RemainingItemCount > 0)
                    {
                        SortedCommands.Add(CommandReader.Read<GEEffectCommandRecord>());
                    }
                    CommandReader.EndForEachIndex();
                }

                SortedCommands.Sort(new EffectCommandComparer());

                for (var i = 0; i < SortedCommands.Length; i++)
                {
                    var command = SortedCommands[i];
                    if (!OwnerCommandBuffers.HasBuffer(command.TargetAsc))
                        continue;

                    OwnerCommandBuffers[command.TargetAsc].Add(new GEEffectCommandBuffer
                    {
                        Sequence = command.Sequence,
                        SortKey = command.SortKey,
                        Frame = command.Frame,
                        GameplayEffectCode = command.GameplayEffectCode,
                        GameplayEffectDefinitionIndex = command.GameplayEffectDefinitionIndex,
                        SourceAsc = command.SourceAsc,
                        TargetAsc = command.TargetAsc,
                        SourceAbility = command.SourceAbility,
                        Level = command.Level
                    });
                }
            }
        }

        private struct EffectCommandComparer : System.Collections.Generic.IComparer<GEEffectCommandRecord>
        {
            public int Compare(GEEffectCommandRecord x, GEEffectCommandRecord y)
            {
                var sortCompare = x.SortKey.CompareTo(y.SortKey);
                return sortCompare != 0 ? sortCompare : x.Sequence.CompareTo(y.Sequence);
            }
        }
    }
}
```

合理性：

1. Producer 并行写 `NativeStream` 的 `GEEffectCommandRecord`，避免全局 DynamicBuffer 写竞争（`CASE-12`）。
2. Primary GE 按目标列表写入 target；cost/cooldown GE 是同一次 activation 的 source-side command seed，目标为 `SourceAsc`。它们共用 Definition Catalog 解析出的 GE index，不在 Fan-In 阶段再查 managed config。
3. Ability command、period/passive command、previous-frame reaction command 都应按同一 record 形态进入 Fan-In；不同 producer 可以使用独立 `NativeStream` 后统一 merge，或使用不重叠的 `forEachIndex` range，禁止用 ECB / singleton buffer 充当 gameplay command bus。
4. Merge 阶段按 deterministic `TargetSortKey / Sequence` 排序，避免依赖 worker thread、entity index 或 `ParallelWriter` 的不确定顺序（`MAT-05` `NAT-02`）。
5. Merge 阶段通过 `BufferLookup<GEEffectCommandBuffer>` 对 target ASC 做单线程确定性 append；这仍是随机访问，但被限制在 merge job 内。如果 merge 成本或随机写压力超过 budget，应按 `TargetAsc` 排序后切成 target range，再改为 chunk-local / range-local apply。

### State Evaluate：PostApply active effect store

```csharp
using Unity.Burst;
using Unity.Entities;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASEffectFanInSystem))]
    public partial struct GASActiveEffectPostApplySystem : ISystem
    {
        private EntityQuery _ascQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ascQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ActiveGameplayEffectBuffer>()
                }
            });

            state.RequireForUpdate(_ascQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new TickActiveEffectsJob
            {
                EffectBufferType = state.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(false),
                Frame = SystemAPI.GetSingleton<GlobalTimer>().Frame
            }.ScheduleParallel(_ascQuery, state.Dependency);
        }

        [BurstCompile]
        private struct TickActiveEffectsJob : IJobChunk
        {
            public BufferTypeHandle<ActiveGameplayEffectBuffer> EffectBufferType;
            public int Frame;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                Unity.Assertions.Assert.IsFalse(useEnabledMask);
                var effectsByAsc = chunk.GetBufferAccessor(ref EffectBufferType);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    var effects = effectsByAsc[entityIndex];

                    for (var slotIndex = 0; slotIndex < effects.Length; slotIndex++)
                    {
                        var slot = effects[slotIndex];

                        switch (slot.State)
                        {
                            case ActiveEffectSlotState.Active:
                                TickActiveSlot(ref slot, Frame);
                                break;

                            case ActiveEffectSlotState.Inhibited:
                                if ((slot.Flags & ActiveEffectSlotFlags.TicksWhenInhibited) != 0)
                                    TickActiveSlot(ref slot, Frame);
                                break;

                            case ActiveEffectSlotState.PendingRemove:
                                slot.RemainingFrame = 0;
                                break;
                        }

                        effects[slotIndex] = slot;
                    }
                }
            }

            private static void TickActiveSlot(ref ActiveGameplayEffectBuffer slot, int frame)
            {
                if ((slot.Flags & ActiveEffectSlotFlags.HasDuration) != 0)
                {
                    slot.RemainingFrame -= 1;
                    if (slot.RemainingFrame <= 0)
                    {
                        slot.PreviousState = slot.State;
                        slot.State = ActiveEffectSlotState.PendingRemove;
                    }
                }

                // Period due detection is owned by GASActiveEffectPreTickSystem so
                // period commands enter the same deterministic Fan-In merge as all
                // other GE commands. PostApply only mutates owner-local store state.
            }
        }
    }
}
```

合理性：

1. ActiveEffect 生命周期状态数少，单 job enum switch 是默认策略（`FSM-02` `FSM-06`）。
2. period due 的 `GEEffectCommandRecord` 由 Effect Fan-In lane 的 producer 写入 `NativeStream`；本系统只提交 owner-local slot 时间状态和 chunk skip 元数据，避免 State lane 在 Fan-In 之后绕过 deterministic merge。
3. expire / remove 不创建临时 entity；真正的 destroy / add / remove 进入 Structural Commit，避免 `PRF-01` / `SC-01`。
4. slot flags 表达 granted tags/abilities/period/stack 等状态，不按状态增删 component，避免 archetype 爆炸（`FSM-01` `FSM-05`）。

### Attribute Reduce / Apply：AttributeSet target grouped 写入

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

合理性：

1. 每个 job 只写自己 chunk 内 target ASC 的 AttributeSet 和 fact buffer，避免 `ComponentLookup` 随机写竞态。官方 `systems-looking-up-data.md` 明确说随机 lookup 低效且可能与直接读写数据重叠产生 race condition。
2. Attribute current 与 base/config 分离，符合官方 `systems-data-granularity.md` 的 read-only / read-write 分离要求；写 Current 不再把 Base 的 reactive consumer 误触发。
3. 不再按 `Health/Mana/Attack/...` 生成同形 apply system，避免 `systems-optimizing.md` 所说的 system 固定成本、重复 lookup/type handle 和更复杂 `JobHandle` 链。
4. `AttributeModifierBuffer` 是 Effect Fan-In / Magnitude Resolve 后的 target grouped 输入；MMC 读取 source/target snapshot 的随机访问发生在写属性之前，Apply lane 不再 random write 其他 ASC。
5. `if (!useEnabledMask) for ... else ChunkEntityEnumerator` 同时保留无 enableable query 的普通 for 快路径，以及未来加入 enableable filter 时的 disabled entity 正确性，符合官方 `iterating-data-ijobchunk-implement.md` 的 IJobChunk 迭代写法。

### Gameplay Fact：Core reaction facts

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [BurstCompile]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASAttributeSetReduceApplySystem))]
    public partial struct GASDeathFactProjectionSystem : ISystem
    {
        private EntityQuery _targetQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<CombatAttributeCurrentSetComponent>(),
                    ComponentType.ReadOnly<AttributeDirtyMaskComponent>(),
                    ComponentType.ReadWrite<GameplayEventBuffer>()
                }
            });

            state.RequireForUpdate(_targetQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ProjectDeathFactsJob
            {
                EntityType = state.GetEntityTypeHandle(),
                CombatCurrentType = state.GetComponentTypeHandle<CombatAttributeCurrentSetComponent>(true),
                DirtyMaskType = state.GetComponentTypeHandle<AttributeDirtyMaskComponent>(true),
                FactBufferType = state.GetBufferTypeHandle<GameplayEventBuffer>(false)
            }.ScheduleParallel(_targetQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ProjectDeathFactsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<CombatAttributeCurrentSetComponent> CombatCurrentType;
            [ReadOnly] public ComponentTypeHandle<AttributeDirtyMaskComponent> DirtyMaskType;
            public BufferTypeHandle<GameplayEventBuffer> FactBufferType;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var combatValues = chunk.GetNativeArray(ref CombatCurrentType);
                var dirtyMasks = chunk.GetNativeArray(ref DirtyMaskType);
                var factsByTarget = chunk.GetBufferAccessor(ref FactBufferType);

                if (!useEnabledMask)
                {
                    for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                    {
                        ProjectEntity(entityIndex, unfilteredChunkIndex, entities, combatValues, dirtyMasks, factsByTarget);
                    }

                    return;
                }

                var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    ProjectEntity(entityIndex, unfilteredChunkIndex, entities, combatValues, dirtyMasks, factsByTarget);
                }
            }

            private static void ProjectEntity(
                int entityIndex,
                int unfilteredChunkIndex,
                NativeArray<Entity> entities,
                NativeArray<CombatAttributeCurrentSetComponent> combatValues,
                NativeArray<AttributeDirtyMaskComponent> dirtyMasks,
                BufferAccessor<GameplayEventBuffer> factsByTarget)
            {
                var dirty = dirtyMasks[entityIndex];
                if ((dirty.CombatWord & AttributeDirtyBits.Health) == 0)
                    return;

                var combat = combatValues[entityIndex];
                if (combat.Health > 0f)
                    return;

                factsByTarget[entityIndex].Add(new GameplayEventBuffer
                {
                    Sequence = unfilteredChunkIndex << 16 | entityIndex,
                    EventCode = GameplayEventCodes.DamageResolved,
                    SourceAsc = Entity.Null,
                    TargetAsc = entities[entityIndex],
                    Value = 0f
                });
            }
        }
    }
}
```

合理性：

1. Gameplay Fact 是 Core reaction 输入，不是 Presentation event bus；Ability trigger / reactive GE 可以消费该 fact 并重新进入 Effect Fan-In。
2. 该 system 只读 committed AttributeSet 与 dirty mask、写本 target 的 fact buffer，不做结构变化；死亡销毁、grant/remove 等进入 Structural Commit。
3. `AttributeDirtyMaskComponent` 将 AttributeSet 打包后的业务变化重新收敛到具体 AttributeCode，避免 Boundary / Fact 因 set 打包而全量扫描所有属性字段。
4. Fact projection 与 Boundary Projection 分离，Presentation / Replay / Debugger 只能观察 fact，不反向驱动 simulation（`SYS-05`）。
