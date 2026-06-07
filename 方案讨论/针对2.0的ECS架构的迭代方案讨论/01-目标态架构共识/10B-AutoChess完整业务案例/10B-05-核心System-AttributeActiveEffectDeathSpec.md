# 10B-05：核心 System 的 Attribute、ActiveEffect 与死亡检测

> Owner：`01-目标态架构共识/10B-AutoChess完整业务案例` | 状态：目标态子 Spec | 拆分来源：`../10B-AutoChess完整业务案例设计Spec.md` | 最近拆分：2026-06-07

本文件只描述 AutoChess 完整业务案例的目标态设计。禁止写入当前代码事实、执行流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。
## 八、核心 System 实现（续）

### 8.4 Attribute Reduce/Apply System（GASCoreSimulationSystemGroup / Attribute lane）

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core — GASCoreSimulationSystemGroup]
// Resolved AttributeModifierBuffer → target grouped AttributeSet apply
// Magnitude Resolve 在 Fan-In/Spec lane 只读 source/target snapshot 完成；
// 本 lane 只写当前 chunk 内 target ASC 的 AttributeSet 和 dirty mask。
//
// 官方依据:
// - systems-data-granularity.md: Current 与 Base/Config 分离
// - systems-optimizing.md: 不为每个属性生成一个同形 system
// - systems-looking-up-data.md: Apply lane 禁止 ComponentLookup 随机写
// - iterating-data-ijobchunk-implement.md: 无 enableable mask 走普通 for；有 mask 才用 ChunkEntityEnumerator
// ============================================================

[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[BurstCompile]
public partial struct GASAutoChessAttributeSetReduceApplySystem : ISystem
{
    private EntityQuery _targetQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _targetQuery = state.GetEntityQuery(new EntityQueryDesc
        {
            All = new[]
            {
                ComponentType.ReadWrite<AutoChessCombatAttributeCurrentSetComponent>(),
                ComponentType.ReadOnly<AutoChessCombatAttributeBaseSetComponent>(),
                ComponentType.ReadWrite<AutoChessResourceAttributeCurrentSetComponent>(),
                ComponentType.ReadOnly<AutoChessResourceAttributeBaseSetComponent>(),
                ComponentType.ReadWrite<AutoChessAttributeDirtyMaskComponent>(),
                ComponentType.ReadWrite<AttributeModifierBuffer>(),
                ComponentType.ReadWrite<GameplayEventBuffer>()
            }
        });

        state.RequireForUpdate(_targetQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new ApplyAutoChessAttributeSetJob
        {
            CombatCurrentType = state.GetComponentTypeHandle<AutoChessCombatAttributeCurrentSetComponent>(false),
            CombatBaseType = state.GetComponentTypeHandle<AutoChessCombatAttributeBaseSetComponent>(true),
            ResourceCurrentType = state.GetComponentTypeHandle<AutoChessResourceAttributeCurrentSetComponent>(false),
            ResourceBaseType = state.GetComponentTypeHandle<AutoChessResourceAttributeBaseSetComponent>(true),
            DirtyMaskType = state.GetComponentTypeHandle<AutoChessAttributeDirtyMaskComponent>(false),
            ModifierBufferType = state.GetBufferTypeHandle<AttributeModifierBuffer>(false),
            FactBufferType = state.GetBufferTypeHandle<GameplayEventBuffer>(false)
        }.ScheduleParallel(_targetQuery, state.Dependency);
    }
}

[BurstCompile]
public struct ApplyAutoChessAttributeSetJob : IJobChunk
{
    public ComponentTypeHandle<AutoChessCombatAttributeCurrentSetComponent> CombatCurrentType;
    [ReadOnly] public ComponentTypeHandle<AutoChessCombatAttributeBaseSetComponent> CombatBaseType;
    public ComponentTypeHandle<AutoChessResourceAttributeCurrentSetComponent> ResourceCurrentType;
    [ReadOnly] public ComponentTypeHandle<AutoChessResourceAttributeBaseSetComponent> ResourceBaseType;
    public ComponentTypeHandle<AutoChessAttributeDirtyMaskComponent> DirtyMaskType;
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
        NativeArray<AutoChessCombatAttributeCurrentSetComponent> combatValues,
        NativeArray<AutoChessCombatAttributeBaseSetComponent> combatBaseValues,
        NativeArray<AutoChessResourceAttributeCurrentSetComponent> resourceValues,
        NativeArray<AutoChessResourceAttributeBaseSetComponent> resourceBaseValues,
        NativeArray<AutoChessAttributeDirtyMaskComponent> dirtyMasks,
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

        for (int i = 0; i < modifiers.Length; i++)
        {
            var modifier = modifiers[i];
            var beforeHealth = combat.Health;
            var beforeMana = resource.Mana;

            AttrAccessor.ApplyResolvedModifier(
                modifier.AttributeCode,
                modifier.Magnitude,
                ref combat,
                in combatBase,
                ref resource,
                in resourceBase,
                ref dirty);

            var delta = modifier.AttributeCode switch
            {
                XAttr.HP => combat.Health - beforeHealth,
                XAttr.MANA => resource.Mana - beforeMana,
                _ => modifier.Magnitude
            };

            facts.Add(new GameplayEventBuffer
            {
                Sequence = ((unfilteredChunkIndex & 0x7FFF) << 17) | (entityIndex << 8) | (i & 0xFF),
                EventCode = GameplayEventCodes.AttributeChanged,
                SourceAsc = modifier.SourceAsc,
                TargetAsc = modifier.TargetAsc,
                Value = delta
            });
        }

        combatValues[entityIndex] = combat;
        resourceValues[entityIndex] = resource;
        dirtyMasks[entityIndex] = dirty;
        modifiers.Clear();
    }
}
```

### 8.5 Active Effect Lifecycle System（GASCoreSimulationSystemGroup / State lane）

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core — GASCoreSimulationSystemGroup]
// Duration GE 生命周期管理拆成两个 State lane：
//   1. GASActiveEffectMutationApplySystem
//      消费 target-grouped ActiveEffectMutation range，写 owner-local active slot。
//   2. GASActiveEffectTickSystem
//      tick owner-local slot，输出 Period EffectCommand 与 GameplayFact。
//
// 目标态不通过 GEStreamOwnerComponent singleton 承载 mutation/fact。
// 目标态不通过 ECB.AppendToBuffer 生产 frame fact/command。
// 结构变化仅限“目标 ASC 首次安装 active slot buffer”等低频结构意图，
// 并进入 GASStructuralCommitSystemGroup 的 StructuralIntent lane。
// ============================================================

[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[BurstCompile]
public partial struct GASActiveEffectMutationApplySystem : ISystem
{
    private EntityQuery _targetQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _targetQuery = state.GetEntityQuery(
            ComponentType.ReadWrite<ActiveGameplayEffectBuffer>(),
            ComponentType.ReadWrite<TagMaskComponent>());
        state.RequireForUpdate(_targetQuery);
        state.RequireForUpdate<EffectFanInLaneStateComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var frame = GASActiveEffectMutationFrame.Resolve(ref state);
        state.Dependency = new ApplyActiveEffectMutationRangesJob
        {
            Entities = state.GetEntityTypeHandle(),
            SlotType = state.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(false),
            TagType = state.GetComponentTypeHandle<TagMaskComponent>(false),
            Mutations = frame.TargetGroupedMutationRanges,
            DefinitionLookup = frame.GameplayEffectLookup,
            Facts = frame.FactWriter,
            StructuralIntents = frame.StructuralIntentWriter,
        }.ScheduleParallel(_targetQuery, state.Dependency);
    }
}

[Flags]
public enum EffectSlotFlags : byte
{
    None          = 0,
    Active        = 1 << 0,
    Inhibited     = 1 << 1,
    PendingRemove = 1 << 2,
}

[BurstCompile]
public struct ApplyActiveEffectMutationRangesJob : IJobChunk
{
    [ReadOnly] public EntityTypeHandle Entities;
    public BufferTypeHandle<ActiveGameplayEffectBuffer> SlotType;
    public ComponentTypeHandle<TagMaskComponent> TagType;
    [ReadOnly] public GASTargetGroupedRangeReader<ActiveEffectMutationBuffer> Mutations;
    [ReadOnly] public GAStaticLookup DefinitionLookup;
    public GASNativeStreamWriter<GameplayEventBuffer> Facts;
    // 仅用于“目标 ASC 尚未拥有 active slot buffer”这类低频结构意图。
    // 常规 stack / refresh / tag grant 不写 structural ECB。
    public GASNativeStreamWriter<GASStructuralIntentRecord> StructuralIntents;

    public void Execute(
        in ArchetypeChunk chunk,
        int unfilteredChunkIndex,
        bool useEnabledMask,
        in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
    {
        var entities = chunk.GetNativeArray(Entities);
        var slotsByTarget = chunk.GetBufferAccessor(ref SlotType);
        var tags = chunk.GetNativeArray(ref TagType);
        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out var entityIndex))
        {
            var target = entities[entityIndex];
            if (!Mutations.TryGetRange(target, out var range))
                continue;

            var slots = slotsByTarget[entityIndex];
            var tagMask = tags[entityIndex];
            for (var i = range.Start; i < range.End; i++)
            {
                ApplyMutation(
                    unfilteredChunkIndex,
                    target,
                    Mutations[i],
                    ref slots,
                    ref tagMask);
            }

            tags[entityIndex] = tagMask;
        }
    }

    private void ApplyMutation(
        int chunkIndex,
        Entity target,
        in ActiveEffectMutationBuffer mutation,
        ref DynamicBuffer<ActiveGameplayEffectBuffer> slots,
        ref TagMaskComponent tagMask)
    {
        var existingIndex = FindSlot(slots, mutation.EffectCode);
        if (existingIndex >= 0)
        {
            ref var slot = ref slots.ElementAt(existingIndex);
            slot.StackCount = (byte)math.min(slot.StackCount + 1, mutation.StackLimit);
            slot.RemainingDuration = math.max(slot.RemainingDuration, mutation.DurationFrames);
            return;
        }

        slots.Add(new ActiveGameplayEffectBuffer
        {
            EffectCode = mutation.EffectCode,
            StackCount = 1,
            RemainingDuration = mutation.DurationFrames,
            PeriodAccumulator = 0f,
            ContextId = mutation.ContextId,
            SourceAsc = mutation.SourceAsc,
            TargetAsc = target,
            Flags = (byte)EffectSlotFlags.Active,
        });

        var geBlob = DefinitionLookup.Find(mutation.EffectCode);
        if (geBlob.IsCreated)
        {
            ref var ge = ref geBlob.Value;
            for (var t = 0; t < ge.GrantedTags.Length; t++)
                tagMask.Value |= ge.GrantedTags[t];
        }
    }

    private static int FindSlot(
        DynamicBuffer<ActiveGameplayEffectBuffer> slots,
        int effectCode)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i].EffectCode == effectCode)
                return i;
        }

        return -1;
    }
}

[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[BurstCompile]
public partial struct GASActiveEffectTickSystem : ISystem
{
    private EntityQuery _activeSlotQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _activeSlotQuery = state.GetEntityQuery(
            ComponentType.ReadWrite<ActiveGameplayEffectBuffer>(),
            ComponentType.ReadWrite<TagMaskComponent>());
        state.RequireForUpdate(_activeSlotQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var frame = GASActiveEffectTickFrame.Resolve(ref state);
        state.Dependency = new TickActiveSlotsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            SlotType = state.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(false),
            TagType = state.GetComponentTypeHandle<TagMaskComponent>(false),
            DefinitionLookup = frame.GameplayEffectLookup,
            PeriodCommands = frame.PeriodEffectCommandWriter,
            Facts = frame.FactWriter,
        }.ScheduleParallel(_activeSlotQuery, state.Dependency);
    }
}

[BurstCompile]
public struct TickActiveSlotsJob : IJobChunk
{
    public float DeltaTime;
    public BufferTypeHandle<ActiveGameplayEffectBuffer> SlotType;
    public ComponentTypeHandle<TagMaskComponent> TagType;
    [ReadOnly] public GAStaticLookup DefinitionLookup;
    public GASNativeStreamWriter<GEEffectCommandBuffer> PeriodCommands;
    public GASNativeStreamWriter<GameplayEventBuffer> Facts;

    public void Execute(
        in ArchetypeChunk chunk,
        int unfilteredChunkIndex,
        bool useEnabledMask,
        in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
    {
        var slotsByTarget = chunk.GetBufferAccessor(ref SlotType);
        var tags = chunk.GetNativeArray(ref TagType);
        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out var entityIndex))
        {
            var slots = slotsByTarget[entityIndex];
            var tagMask = tags[entityIndex];
            TickSlots(unfilteredChunkIndex, ref slots, ref tagMask);
            tags[entityIndex] = tagMask;
        }
    }

    private void TickSlots(
        int chunkIndex,
        ref DynamicBuffer<ActiveGameplayEffectBuffer> slots,
        ref TagMaskComponent tagMask)
    {
        for (var i = slots.Length - 1; i >= 0; i--)
        {
            ref var slot = ref slots.ElementAt(i);
            if ((slot.Flags & (byte)EffectSlotFlags.Active) == 0)
                continue;

            slot.RemainingDuration -= DeltaTime;
            if (slot.RemainingDuration <= 0f)
            {
                RemoveExpiredSlot(chunkIndex, ref slots, i, ref tagMask);
                continue;
            }

            TryEmitPeriodCommand(chunkIndex, ref slot);
        }
    }

    private void RemoveExpiredSlot(
        int chunkIndex,
        ref DynamicBuffer<ActiveGameplayEffectBuffer> slots,
        int slotIndex,
        ref TagMaskComponent tagMask)
    {
        var slot = slots[slotIndex];
        var geBlob = DefinitionLookup.Find(slot.EffectCode);
        if (geBlob.IsCreated)
        {
            ref var ge = ref geBlob.Value;
            for (var t = 0; t < ge.GrantedTags.Length; t++)
                tagMask.Value &= ~ge.GrantedTags[t];
        }

        Facts.Write(chunkIndex, new GameplayEventBuffer
        {
            EventCode = GameplayEventCodes.EffectExpired,
            SourceAsc = slot.SourceAsc,
            TargetAsc = slot.TargetAsc,
            SourceEffectCode = slot.EffectCode,
        });
        slots.RemoveAt(slotIndex);
    }

    private void TryEmitPeriodCommand(
        int chunkIndex,
        ref ActiveGameplayEffectBuffer slot)
    {
        var geBlob = DefinitionLookup.Find(slot.EffectCode);
        if (!geBlob.IsCreated || geBlob.Value.PeriodFrames <= 0)
            return;

        var periodSec = geBlob.Value.PeriodFrames / 60f;
        slot.PeriodAccumulator += DeltaTime;
        while (slot.PeriodAccumulator >= periodSec)
        {
            slot.PeriodAccumulator -= periodSec;
            PeriodCommands.Write(chunkIndex, new GEEffectCommandBuffer
            {
                EffectCode = geBlob.Value.PeriodGameplayEffectCode,
                SourceAsc = slot.SourceAsc,
                TargetAsc = slot.TargetAsc,
                ContextId = slot.ContextId,
            });
        }
    }
}
```

### 8.6 死亡检测 System（GASCoreSimulationSystemGroup / Gameplay Fact lane）

```csharp
// ============================================================
// [Layer 3: GAS Runtime Core — GASCoreSimulationSystemGroup]
// 死亡检测：HP <= 0 -> 写入 Death fact 与 presentation fact
//
// CASE-02 (IJobEntity) — 拒绝 CASE-01 (SystemAPI.Query foreach)
// 原因: 每帧遍历所有存活 ASC，hot path 主线程遍历不可接受
// 目标态: fact / presentation 输出走 frame-local NativeStream，
//       structural cleanup 只写 StructuralIntent，交给 StructuralCommit phase。
// ============================================================

[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
[BurstCompile]
public partial struct GameplayEventDeathCheckSystem : ISystem
{
    private EntityQuery _aliveQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _aliveQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<AutoChessCombatAttributeCurrentSetComponent>(),
            ComponentType.ReadOnly<AutoChessAttributeDirtyMaskComponent>(),
            ComponentType.ReadWrite<TagMaskComponent>(),
            ComponentType.ReadWrite<CChessUnit>());
        state.RequireForUpdate(_aliveQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var frame = GASGameplayFactFrame.Resolve(ref state);
        var currentFrame = (int)(SystemAPI.Time.ElapsedTime * 60);

        var deathJob = new DeathCheckJob
        {
            Facts = frame.FactWriter,
            PresentationFacts = frame.PresentationFactWriter,
            StructuralIntents = frame.StructuralIntentWriter,
            CurrentFrame = currentFrame,
        };
        state.Dependency = deathJob.ScheduleParallel(_aliveQuery, state.Dependency);
    }
}

[BurstCompile]
public partial struct DeathCheckJob : IJobEntity
{
    public GASNativeStreamWriter<GameplayEventBuffer> Facts;
    public GASNativeStreamWriter<PresentationEventBuffer> PresentationFacts;
    public GASNativeStreamWriter<GASStructuralIntentRecord> StructuralIntents;
    public int CurrentFrame;

    public void Execute(
        [EntityIndexInChunk] int chunkIndex,
        Entity asc,
        in AutoChessCombatAttributeCurrentSetComponent combat,
        in AutoChessAttributeDirtyMaskComponent dirty,
        ref TagMaskComponent tagMask,
        ref CChessUnit unit)
    {
        if (!unit.IsAlive) return;
        if ((dirty.CombatWord & (1ul << 0)) == 0) return;
        if (combat.Health > 0f) return;

        // 标记死亡
        unit.IsAlive = false;
        tagMask.Value |= XTagBit.Dead;

        // 产出 Death fact
        Facts.Write(chunkIndex, new GameplayEventBuffer
        {
            EventCode = GameplayEventCodes.DeathOccurred,
            Frame = CurrentFrame,
            TargetAsc = asc,
        });

        // 产出 Cue marker（表现层消费）
        PresentationFacts.Write(chunkIndex, new PresentationEventBuffer
        {
            CueCode = CueCode.DeathVFX,
            SourceAsc = asc,
            PositionCol = unit.BoardCol,
            PositionRow = unit.BoardRow,
        });

        StructuralIntents.Write(chunkIndex, new GASStructuralIntentRecord
        {
            Kind = GASStructuralIntentKind.RequestAscDestroy,
            TargetAsc = asc,
            ReasonCode = GameplayEventCodes.DeathOccurred,
        });
    }
}
```

---
