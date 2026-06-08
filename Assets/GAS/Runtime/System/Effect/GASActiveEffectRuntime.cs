// Active-effect lifecycle runtime jobs owned by hand-written Runtime Core.
using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    public static class GASActiveEffectRuntime
    {
        public struct ActiveMutationCommandRange
        {
            public int Start;
            public int End;

            public ActiveMutationCommandRange(int start, int end)
            {
                Start = start;
                End = end;
            }
        }

        private static long MakeActiveMutationSourceAttributeSnapshotKey(int commandSequence, int modifierIndex)
        {
            return ((long)commandSequence << 32) ^ (uint)modifierIndex;
        }

        public readonly struct ActiveEffectSlotSourceAttributeSnapshotKey : IEquatable<ActiveEffectSlotSourceAttributeSnapshotKey>
        {
            public readonly Entity Owner;
            public readonly int SlotSequence;
            public readonly int ModifierIndex;

            public ActiveEffectSlotSourceAttributeSnapshotKey(Entity owner, int slotSequence, int modifierIndex)
            {
                Owner = owner;
                SlotSequence = slotSequence;
                ModifierIndex = modifierIndex;
            }

            public bool Equals(ActiveEffectSlotSourceAttributeSnapshotKey other)
            {
                return Owner.Equals(other.Owner)
                       && SlotSequence == other.SlotSequence
                       && ModifierIndex == other.ModifierIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is ActiveEffectSlotSourceAttributeSnapshotKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = Owner.GetHashCode();
                    hash = (hash * 397) ^ SlotSequence;
                    hash = (hash * 397) ^ ModifierIndex;
                    return hash;
                }
            }
        }

        public static int EstimateActiveEffectSlotSourceAttributeSnapshotCapacity(
            ref GASDefinitionCatalogBlob catalog,
            int ownerCapacity)
        {
            var maxSourceAttributeModifierCount = 1;
            for (var gameplayEffectIndex = 0; gameplayEffectIndex < catalog.GameplayEffects.Length; gameplayEffectIndex++)
            {
                ref readonly var gameplayEffect = ref catalog.GameplayEffects[gameplayEffectIndex];
                var sourceAttributeModifierCount = 0;
                for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                {
                    var modifierIndex = gameplayEffect.ModifierStart + i;
                    if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                        continue;

                    if (catalog.Modifiers[modifierIndex].MagnitudeSource == EMagnitudeSource.SourceAttribute)
                        sourceAttributeModifierCount++;
                }

                if (sourceAttributeModifierCount > maxSourceAttributeModifierCount)
                    maxSourceAttributeModifierCount = sourceAttributeModifierCount;
            }

            var capacity = (long)ownerCapacity * ActiveEffectStore.InlineSlotCapacity * maxSourceAttributeModifierCount;
            if (capacity < 256)
                return 256;
            return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
        }

        private static ActiveEffectSlotSourceAttributeSnapshotKey MakeActiveEffectSlotSourceAttributeSnapshotKey(
            Entity owner,
            int slotSequence,
            int modifierIndex)
        {
            return new ActiveEffectSlotSourceAttributeSnapshotKey(owner, slotSequence, modifierIndex);
        }

        public struct ActiveEffectSlotSourceSnapshotLaneCounters
        {
            public int GatherAttemptCount;
            public int SnapshotWriteCount;
            public int SnapshotWriteFailureCount;
            public int AttributeMissCount;
            public int ApplyHitCount;
            public int ApplyMissCount;
            public int FallbackValueCount;
            public int CapacityPressureCount;
            public int SpillCount;

            public bool HasEvidence =>
                GatherAttemptCount > 0
                || SnapshotWriteCount > 0
                || SnapshotWriteFailureCount > 0
                || AttributeMissCount > 0
                || ApplyHitCount > 0
                || ApplyMissCount > 0
                || FallbackValueCount > 0
                || CapacityPressureCount > 0
                || SpillCount > 0;

            public void RecordSnapshotWrite(bool success)
            {
                GatherAttemptCount++;
                if (success)
                {
                    SnapshotWriteCount++;
                    return;
                }

                SnapshotWriteFailureCount++;
                CapacityPressureCount++;
                SpillCount++;
            }

            public void AddToStream(ref GEEffectCommandStreamComponent stream)
            {
                if (!HasEvidence)
                    return;

                EffectCommandSpecStream.AddActiveEffectSlotSourceSnapshotCounters(
                    ref stream,
                    GatherAttemptCount,
                    SnapshotWriteCount,
                    SnapshotWriteFailureCount,
                    AttributeMissCount,
                    ApplyHitCount,
                    ApplyMissCount,
                    FallbackValueCount,
                    CapacityPressureCount,
                    SpillCount);
            }
        }

        public struct ActiveEffectPreTickLaneCounters
        {
            public int ChunkCount;
            public int ScannedOwnerCount;
            public int SkippedOwnerCount;
            public int ProcessedOwnerCount;
            public int ScannedSlotCount;
            public int DueSlotCount;
            public int NoopSlotCount;
            public int MutationWriteCount;

            public bool HasEvidence =>
                ChunkCount > 0
                || ScannedOwnerCount > 0
                || SkippedOwnerCount > 0
                || ProcessedOwnerCount > 0
                || ScannedSlotCount > 0
                || DueSlotCount > 0
                || NoopSlotCount > 0
                || MutationWriteCount > 0;

            public void AddToStream(ref GEEffectCommandStreamComponent stream)
            {
                if (!HasEvidence)
                    return;

                EffectCommandSpecStream.AddActiveEffectPreTickCounters(
                    ref stream,
                    ChunkCount,
                    ScannedOwnerCount,
                    SkippedOwnerCount,
                    ProcessedOwnerCount,
                    ScannedSlotCount,
                    DueSlotCount,
                    NoopSlotCount,
                    MutationWriteCount);
            }
        }

        private struct ActiveEffectMagnitudeSourceCounters
        {
            public int CurrentValueLookupCount;
            public int CapturedValueHitCount;
            public int CaptureMissCount;
            public int FallbackValueCount;
            public int SourceAttributeLookupCount;
            public int TargetAttributeLookupCount;

            public bool HasEvidence =>
                CurrentValueLookupCount > 0
                || CapturedValueHitCount > 0
                || CaptureMissCount > 0
                || FallbackValueCount > 0
                || SourceAttributeLookupCount > 0
                || TargetAttributeLookupCount > 0;

            public void AddToStream(ref GEEffectCommandStreamComponent stream)
            {
                if (!HasEvidence)
                    return;

                EffectCommandSpecStream.AddMagnitudeSourceCounters(
                    ref stream,
                    CurrentValueLookupCount,
                    CapturedValueHitCount,
                    CaptureMissCount,
                    captureMissLiveLookups: 0,
                    FallbackValueCount,
                    fallbackFacts: 0,
                    SourceAttributeLookupCount,
                    TargetAttributeLookupCount,
                    executionInputLookups: 0);
            }
        }

        [BurstCompile]
        public struct GEActiveEffectMutationOwnerCommandCollectJob : IJobChunk
        {
            [ReadOnly] public BufferTypeHandle<ActiveEffectMutationCommandBuffer> CommandBufferTypeHandle;
            [ReadOnly] public BufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer> SetByCallerBufferTypeHandle;
            public NativeList<GEEffectCommandBuffer> ActiveMutationCommands;
            public NativeList<GESetByCallerValueBuffer> ActiveMutationSetByCallerValues;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var commandBuffers = chunk.GetBufferAccessor(ref CommandBufferTypeHandle);
                var setByCallerBuffers = chunk.GetBufferAccessor(ref SetByCallerBufferTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var commands = commandBuffers[entityIndex];
                    var setByCallerValues = setByCallerBuffers[entityIndex];
                    for (var i = 0; i < commands.Length; i++)
                    {
                        var command = commands[i].Command;
                        if (command.Kind != GEEffectCommandKind.ActiveMutation)
                            continue;

                        ActiveMutationCommands.Add(CopySetByCallerValues(in command, setByCallerValues));
                    }
                }
            }

            private GEEffectCommandBuffer CopySetByCallerValues(
                in GEEffectCommandBuffer command,
                DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> source)
            {
                if (command.SetByCallerCount <= 0)
                {
                    var emptyCommand = command;
                    emptyCommand.SetByCallerStart = 0;
                    emptyCommand.SetByCallerCount = 0;
                    return emptyCommand;
                }

                var collectedCommand = command;
                var collectedStart = ActiveMutationSetByCallerValues.Length;
                var copied = 0;
                var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
                var end = start + command.SetByCallerCount;
                if (end > source.Length)
                    end = source.Length;

                for (var i = start; i < end; i++)
                {
                    var value = source[i].Value;
                    if (value.CommandSequence != command.Sequence)
                        continue;

                    ActiveMutationSetByCallerValues.Add(new GESetByCallerValueBuffer
                    {
                        CommandSequence = command.Sequence,
                        SpecSequence = value.SpecSequence,
                        Key = value.Key,
                        Value = value.Value,
                    });
                    copied++;
                }

                collectedCommand.SetByCallerStart = copied > 0 ? collectedStart : 0;
                collectedCommand.SetByCallerCount = copied;
                return collectedCommand;
            }
        }

        [BurstCompile]
        public struct GEActiveEffectMutationOwnerCommandFinalizeJob : IJob
        {
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public BufferLookup<AttributeValueBuffer> AttributeLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public NativeList<GEEffectCommandBuffer> ActiveMutationCommands;
            public NativeParallelHashMap<Entity, ActiveMutationCommandRange> ActiveMutationOwnerRanges;
            public NativeParallelHashMap<long, float> ActiveMutationSourceAttributeSnapshots;
            public Entity StreamEntity;

            public void Execute()
            {
                ActiveMutationOwnerRanges.Clear();
                ActiveMutationSourceAttributeSnapshots.Clear();
                if (ActiveMutationCommands.Length == 0)
                    return;

                var sortMoveCount = SortActiveMutationCommandsByOwner();
                var ownerGroupCount = BuildActiveMutationOwnerRanges(out var maxOwnerRange);
                BuildActiveMutationSourceAttributeSnapshots();

                if (StreamEntity != Entity.Null && StreamLookup.HasComponent(StreamEntity))
                {
                    var stream = StreamLookup[StreamEntity];
                    WriteActiveMutationStats(ref stream, sortMoveCount, ownerGroupCount, maxOwnerRange);
                    StreamLookup[StreamEntity] = stream;
                }
            }

            private void BuildActiveMutationSourceAttributeSnapshots()
            {
                if (!Catalog.IsCreated || ActiveMutationCommands.Length == 0)
                    return;

                ref var catalog = ref Catalog.Value;
                for (var commandIndex = 0; commandIndex < ActiveMutationCommands.Length; commandIndex++)
                {
                    var command = ActiveMutationCommands[commandIndex];
                    if (command.SourceAsc == Entity.Null
                        || CompareEntity(command.SourceAsc, command.TargetAsc) == 0
                        || !AttributeLookup.HasBuffer(command.SourceAsc)
                        || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
                    {
                        continue;
                    }

                    ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                    var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
                    if (!HasPersistentRuntimeState(in gameplayEffect, durationFrame) || gameplayEffect.ModifierCount <= 0)
                        continue;

                    var sourceAttributes = AttributeLookup[command.SourceAsc];
                    for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                    {
                        var modifierIndex = gameplayEffect.ModifierStart + i;
                        if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                            continue;

                        var modifier = catalog.Modifiers[modifierIndex];
                        if (modifier.MagnitudeSource != EMagnitudeSource.SourceAttribute
                            || !TryReadSnapshotAttributeValue(sourceAttributes, in modifier, out var sourceValue))
                        {
                            continue;
                        }

                        var snapshotKey = MakeActiveMutationSourceAttributeSnapshotKey(command.Sequence, modifierIndex);
                        ActiveMutationSourceAttributeSnapshots.TryAdd(snapshotKey, sourceValue);
                    }
                }
            }

            private static bool TryReadSnapshotAttributeValue(
                DynamicBuffer<AttributeValueBuffer> attributes,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attrCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private int BuildActiveMutationOwnerRanges(out int maxOwnerRange)
            {
                ActiveMutationOwnerRanges.Clear();
                maxOwnerRange = 0;
                if (ActiveMutationCommands.Length == 0)
                    return 0;

                var ownerGroupCount = 0;
                var rangeStart = 0;
                while (rangeStart < ActiveMutationCommands.Length)
                {
                    var owner = ActiveMutationCommands[rangeStart].TargetAsc;
                    var rangeEnd = rangeStart + 1;
                    while (rangeEnd < ActiveMutationCommands.Length
                           && CompareEntity(owner, ActiveMutationCommands[rangeEnd].TargetAsc) == 0)
                    {
                        rangeEnd++;
                    }

                    var rangeLength = rangeEnd - rangeStart;
                    if (rangeLength > maxOwnerRange)
                        maxOwnerRange = rangeLength;

                    ActiveMutationOwnerRanges.Add(owner, new ActiveMutationCommandRange(rangeStart, rangeEnd));
                    ownerGroupCount++;
                    rangeStart = rangeEnd;
                }

                return ownerGroupCount;
            }

            private int SortActiveMutationCommandsByOwner()
            {
                var moveCount = 0;
                for (var i = 1; i < ActiveMutationCommands.Length; i++)
                {
                    var value = ActiveMutationCommands[i];
                    var j = i - 1;
                    while (j >= 0 && CompareActiveMutationCommand(ActiveMutationCommands[j], value) > 0)
                    {
                        ActiveMutationCommands[j + 1] = ActiveMutationCommands[j];
                        moveCount++;
                        j--;
                    }

                    ActiveMutationCommands[j + 1] = value;
                }

                return moveCount;
            }

            private void WriteActiveMutationStats(
                ref GEEffectCommandStreamComponent stream,
                int sortMoveCount,
                int ownerGroupCount,
                int maxOwnerRange)
            {
                var commandCount = ActiveMutationCommands.Length;
                if (commandCount == 0)
                    return;

                stream.ActiveMutationCommandCount += commandCount;
                stream.ActiveMutationOwnerGroupCount += ownerGroupCount;
                if (maxOwnerRange > stream.ActiveMutationMaxOwnerRange)
                    stream.ActiveMutationMaxOwnerRange = maxOwnerRange;
                stream.ActiveMutationSortMoveCount += sortMoveCount;
            }

            private static int CompareActiveMutationCommand(
                in GEEffectCommandBuffer left,
                in GEEffectCommandBuffer right)
            {
                var result = CompareEntity(left.TargetAsc, right.TargetAsc);
                if (result != 0)
                    return result;

                result = left.Sequence.CompareTo(right.Sequence);
                if (result != 0)
                    return result;

                return left.ParentContextId.CompareTo(right.ParentContextId);
            }

            private static int CompareEntity(Entity left, Entity right)
            {
                var result = left.Index.CompareTo(right.Index);
                if (result != 0)
                    return result;

                return left.Version.CompareTo(right.Version);
            }
        }

        [BurstCompile]
        public struct GEActiveEffectMutationChunkApplyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ASCDestroyingComponent> DestroyingTypeHandle;
            public ComponentTypeHandle<ASCActiveEffectsComponent> ActiveEffectsTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshotBufferTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecordBufferTypeHandle;
            public BufferTypeHandle<ActiveEffectMutationBuffer> MutationBufferTypeHandle;
            public ComponentTypeHandle<TagMaskComponent> TagMaskTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TagFixedMaskComponent> TagFixedMaskTypeHandle;
            public BufferTypeHandle<AttributeValueBuffer> AttributeBufferTypeHandle;
            public BufferTypeHandle<AttributeActiveModifierBuffer> ActiveModifierBufferTypeHandle;
            public BufferTypeHandle<TagTemporarySourceBuffer> TagSourceBufferTypeHandle;
            public BufferTypeHandle<AbilitySlotBuffer> AbilitySlotBufferTypeHandle;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<OwnerLocalInstantNextFrameCommandBuffer> NextFrameInstantCommandLookup;
            public BufferLookup<OwnerLocalInstantNextFrameSetByCallerValueBuffer> NextFrameInstantSetByCallerLookup;
            public BufferLookup<ActiveEffectNextFrameMutationCommandBuffer> NextFrameActiveMutationCommandLookup;
            public BufferLookup<ActiveEffectNextFrameMutationSetByCallerValueBuffer> NextFrameActiveMutationSetByCallerLookup;
            public BufferLookup<AttributeOwnerMarkerRequestBuffer> AttributeOwnerMarkerRequestLookup;
            public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> AbilityGrantedLookup;
            public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;
            public BufferLookup<OwnerLocalGameplayFactBuffer> OwnerFactLookup;
            public EntityCommandBuffer StructuralEcb;
            public EntityArchetype GrantedAbilityArchetype;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            [ReadOnly] public NativeList<GEEffectCommandBuffer> ActiveMutationCommands;
            [ReadOnly] public NativeList<GESetByCallerValueBuffer> ActiveMutationSetByCallerValues;
            [ReadOnly] public NativeParallelHashMap<Entity, ActiveMutationCommandRange> ActiveMutationOwnerRanges;
            [ReadOnly] public NativeParallelHashMap<long, float> ActiveMutationSourceAttributeSnapshots;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;

            private struct ActiveMutationOwnerResources
            {
                public Entity Owner;
                public ASCActiveEffectsComponent Store;
                public DynamicBuffer<ActiveGameplayEffectBuffer> Slots;
                public DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshot;
                public TagMaskComponent TargetTags;
                public bool TargetTagsDirty;
                public bool HasFixedTags;
                public TagFixedMaskComponent FixedTags;
                public bool HasAttributes;
                public DynamicBuffer<AttributeValueBuffer> Attributes;
                public bool HasActiveModifiers;
                public DynamicBuffer<AttributeActiveModifierBuffer> ActiveModifiers;
                public bool HasTagSources;
                public DynamicBuffer<TagTemporarySourceBuffer> TagSources;
                public bool HasAbilitySlots;
                public DynamicBuffer<AbilitySlotBuffer> AbilitySlots;
                public bool HasCleanupRecords;
                public DynamicBuffer<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecords;
                public DynamicBuffer<ActiveEffectMutationBuffer> Mutations;
            }

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated
                    || ActiveMutationCommands.Length == 0
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var destroyingMask = chunk.GetEnabledMask(ref DestroyingTypeHandle);
                var stores = chunk.GetNativeArray(ref ActiveEffectsTypeHandle);
                var tagMasks = chunk.GetNativeArray(ref TagMaskTypeHandle);
                var fixedTagMasks = chunk.GetNativeArray(ref TagFixedMaskTypeHandle);
                var slots = chunk.GetBufferAccessor(ref ActiveEffectSlotBufferTypeHandle);
                var setByCallerSnapshots = chunk.GetBufferAccessor(ref SetByCallerSnapshotBufferTypeHandle);
                var cleanupRecords = chunk.GetBufferAccessor(ref CleanupRecordBufferTypeHandle);
                var mutations = chunk.GetBufferAccessor(ref MutationBufferTypeHandle);
                var attributes = chunk.GetBufferAccessor(ref AttributeBufferTypeHandle);
                var activeModifiers = chunk.GetBufferAccessor(ref ActiveModifierBufferTypeHandle);
                var tagSources = chunk.GetBufferAccessor(ref TagSourceBufferTypeHandle);
                var abilitySlots = chunk.GetBufferAccessor(ref AbilitySlotBufferTypeHandle);
                var stream = StreamLookup[StreamEntity];
                ref var catalog = ref Catalog.Value;
                var magnitudeSourceCounters = default(ActiveEffectMagnitudeSourceCounters);
                var enumerator = new ChunkEntityEnumerator(false, default, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (destroyingMask[entityIndex])
                        continue;

                    var owner = entities[entityIndex];
                    if (!ActiveMutationOwnerRanges.TryGetValue(owner, out var range))
                        continue;

                    var ownerMutations = mutations[entityIndex];
                    ownerMutations.Clear();
                    var ownerResources = new ActiveMutationOwnerResources
                    {
                        Owner = owner,
                        Store = stores[entityIndex],
                        Slots = slots[entityIndex],
                        SetByCallerSnapshot = setByCallerSnapshots[entityIndex],
                        TargetTags = tagMasks[entityIndex],
                        HasFixedTags = true,
                        FixedTags = fixedTagMasks[entityIndex],
                        HasAttributes = true,
                        Attributes = attributes[entityIndex],
                        HasActiveModifiers = true,
                        ActiveModifiers = activeModifiers[entityIndex],
                        HasTagSources = true,
                        TagSources = tagSources[entityIndex],
                        HasAbilitySlots = true,
                        AbilitySlots = abilitySlots[entityIndex],
                        HasCleanupRecords = true,
                        CleanupRecords = cleanupRecords[entityIndex],
                        Mutations = ownerMutations,
                    };

                    for (var commandIndex = range.Start; commandIndex < range.End; commandIndex++)
                    {
                        var command = ActiveMutationCommands[commandIndex];
                        TryApplyActiveMutationToOwner(
                            ref stream,
                            ref catalog,
                            ref ownerResources,
                            in command,
                            ActiveMutationSetByCallerValues,
                            ref magnitudeSourceCounters);
                    }

                    stores[entityIndex] = ownerResources.Store;
                    if (ownerResources.TargetTagsDirty)
                        tagMasks[entityIndex] = ownerResources.TargetTags;
                }

                magnitudeSourceCounters.AddToStream(ref stream);
                StreamLookup[StreamEntity] = stream;
            }

            private static int CompareEntity(Entity left, Entity right)
            {
                var result = left.Index.CompareTo(right.Index);
                if (result != 0)
                    return result;

                return left.Version.CompareTo(right.Version);
            }

            private bool TryApplyActiveMutationToOwner(
                ref GEEffectCommandStreamComponent stream,
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GEEffectCommandBuffer command,
                NativeList<GESetByCallerValueBuffer> setByCallerValues,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters)
            {
                if (CompareEntity(ownerResources.Owner, command.TargetAsc) != 0
                    || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
                {
                    return false;
                }

                ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                if (!GASRuntimeRequirementEvaluator.EvaluateGameplayEffectRequirements(
                        ref catalog,
                        in gameplayEffect,
                        in ownerResources.TargetTags,
                        out _))
                    return false;

                RemoveGameplayEffectsWithTags(
                    ref catalog,
                    ref ownerResources,
                    in gameplayEffect);

                var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
                if (!HasPersistentRuntimeState(in gameplayEffect, durationFrame))
                {
                    ownerResources.Mutations.Add(new ActiveEffectMutationBuffer
                    {
                        Sequence = command.Sequence,
                        SourceCommandSequence = command.Sequence,
                        Frame = Frame,
                        Kind = ActiveEffectMutationKind.Apply,
                        ActiveEffect = Entity.Null,
                        SourceAsc = command.SourceAsc,
                        TargetAsc = command.TargetAsc,
                        SourceAbility = command.SourceAbility,
                        SourceEffect = command.SourceEffect,
                        GameplayEffectCode = command.GameplayEffectCode,
                        ContextId = command.ContextId,
                        ParentContextId = command.ParentContextId,
                        StackCount = 1,
                        DurationFrameOverride = durationFrame,
                        PeriodFrame = gameplayEffect.PeriodFrames,
                    });
                    EnqueueAppliedEvents(in command, in gameplayEffect, Entity.Null);
                    ActiveEffectStore.RefreshChunkSkipIndexCounters(ref ownerResources.Store, ownerResources.Slots, Frame);
                    return true;
                }

                var slotIndex = FindRefreshableSlot(ownerResources.Slots, in command);
                if (slotIndex < 0 && ownerResources.Slots.Length >= GASParameterSetting.ASC_MAX_GAMEPLAY_EFFECT_COUNT)
                    return false;

                var isNewSlot = slotIndex < 0;
                var previous = isNewSlot ? default : ownerResources.Slots[slotIndex];
                var previousStackCount = previous.StackCount <= 0 ? 1 : previous.StackCount;
                var isOverflow = !isNewSlot && IsStackOverflow(in gameplayEffect, previousStackCount);
                if (isOverflow)
                {
                    EmitOverflowCommand(
                        ref stream,
                        ref catalog,
                        in command,
                        in gameplayEffect);
                    EnqueueStackOverflowEvent(in command, in gameplayEffect);
                    if (gameplayEffect.ClearStackOnOverflow != 0)
                    {
                        RemoveSlotAt(ref ownerResources, slotIndex);
                        slotIndex = -1;
                        isNewSlot = true;
                        previous = default;
                        previousStackCount = 1;
                        EnqueueStackClearedByOverflowEvent(in command);
                    }

                    if (gameplayEffect.DenyOverflowApplication != 0)
                    {
                        EnqueueStackOverflowDeniedEvent(in command);
                        ActiveEffectStore.RefreshChunkSkipIndexCounters(ref ownerResources.Store, ownerResources.Slots, Frame);
                        return true;
                    }
                }

                var slot = isNewSlot
                    ? CreateNewSlot(ref ownerResources.Store, in command, in gameplayEffect, durationFrame, Frame)
                    : RefreshExistingSlot(
                        previous,
                        in command,
                        in gameplayEffect,
                        durationFrame,
                        Frame,
                        ShouldRefreshDuration(in gameplayEffect),
                        ShouldResetPeriod(in gameplayEffect));

                slot.StackCount = ResolveNextStackCount(in gameplayEffect, previousStackCount, isNewSlot);
                RemoveActiveModifiersForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                GASActiveEffectRuntime.RemoveSetByCallerSnapshotForSlot(
                    ownerResources.SetByCallerSnapshot,
                    slot.Sequence,
                    slot.GameplayEffectCode);
                CopySetByCallerSnapshot(in command, setByCallerValues, ownerResources.SetByCallerSnapshot, slot.Sequence, slot.GameplayEffectCode);

                var activeModifierCount = ApplyActiveModifiers(
                    ref catalog,
                    ref ownerResources,
                    in gameplayEffect,
                    in command,
                    setByCallerValues,
                    slot.Sequence,
                    slot.StackCount,
                    ref magnitudeSourceCounters);
                slot.ActiveGrantedTagCount = ApplyGrantedTags(
                    ref catalog,
                    ref ownerResources,
                    in gameplayEffect,
                    slot.Sequence);
                slot.ActiveGrantedAbilityCount = ApplyGrantedAbilities(
                    ref catalog,
                    ref ownerResources,
                    in gameplayEffect,
                    slot.Sequence);
                slot.Flags = ResolveSlotFlags(
                    in gameplayEffect,
                    durationFrame,
                    activeModifierCount,
                    slot.ActiveGrantedTagCount,
                    slot.ActiveGrantedAbilityCount);

                if (slotIndex >= 0)
                    ownerResources.Slots[slotIndex] = slot;
                else
                    ownerResources.Slots.Add(slot);

                ActiveEffectStore.RefreshChunkSkipIndexCounters(ref ownerResources.Store, ownerResources.Slots, Frame);

                ownerResources.Mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    SourceCommandSequence = command.Sequence,
                    Frame = Frame,
                    Kind = isNewSlot
                        ? ActiveEffectMutationKind.Apply
                        : slot.StackCount > previousStackCount
                            ? ActiveEffectMutationKind.Stack
                            : ActiveEffectMutationKind.Refresh,
                    ActiveEffect = Entity.Null,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = command.SourceEffect,
                    GameplayEffectCode = command.GameplayEffectCode,
                    ContextId = command.ContextId,
                    ParentContextId = command.ParentContextId,
                    StackCount = slot.StackCount,
                    DurationFrameOverride = durationFrame,
                    PeriodFrame = slot.PeriodFrame,
                });

                EnqueueAppliedEvents(in command, in gameplayEffect, Entity.Null);
                return true;
            }

            private int ApplyActiveModifiers(
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                in GEEffectCommandBuffer command,
                NativeList<GESetByCallerValueBuffer> setByCallerValues,
                int slotSequence,
                int stackCount,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters)
            {
                if (gameplayEffect.ModifierCount <= 0
                    || !ownerResources.HasAttributes
                    || !ownerResources.HasActiveModifiers)
                {
                    return 0;
                }

                var attributes = ownerResources.Attributes;
                var activeModifiers = ownerResources.ActiveModifiers;
                var added = 0;
                for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                {
                    var modifierIndex = gameplayEffect.ModifierStart + i;
                    if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                        continue;

                    var modifier = catalog.Modifiers[modifierIndex];
                    var attrIndex = attributes.IndexOfAttribute(modifier.AttributeSetCode, modifier.AttributeCode);
                    if (attrIndex < 0)
                        continue;

                    var context = BuildMagnitudeContext(
                        ref ownerResources,
                        in command,
                        setByCallerValues,
                        in modifier,
                        modifierIndex,
                        stackCount,
                        ref magnitudeSourceCounters);
                    if (!GASRuntimeMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
                        continue;

                    if (HasActiveModifier(activeModifiers, slotSequence, command.GameplayEffectCode, modifier.AttributeSetCode, modifier.AttributeCode, modifier.Operation))
                        continue;

                    activeModifiers.Add(new AttributeActiveModifierBuffer
                    {
                        AttrSetCode = modifier.AttributeSetCode,
                        AttributeCode = modifier.AttributeCode,
                        SourceEntity = Entity.Null,
                        SourceSequence = slotSequence,
                        SourceGameplayEffectCode = command.GameplayEffectCode,
                        Magnitude = magnitude,
                        Op = modifier.Operation,
                    });
                    MarkActiveModifierAdded(ownerResources.Owner);
                    MarkCurrentValueDirty(attributes, ownerResources.Owner, modifier.AttributeSetCode, modifier.AttributeCode);
                    added++;
                }

                return added;
            }

            private MagnitudeEvalContext BuildMagnitudeContext(
                ref ActiveMutationOwnerResources ownerResources,
                in GEEffectCommandBuffer command,
                NativeList<GESetByCallerValueBuffer> setByCallerValues,
                in GASCatalogModifierDefinitionBlob modifier,
                int modifierIndex,
                int stackCount,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters)
            {
                var context = new MagnitudeEvalContext
                {
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    GameplayEffectCode = command.GameplayEffectCode,
                    Level = command.Level,
                    StackCount = stackCount <= 0 ? 1 : stackCount,
                    SetByCallerKey = modifier.MagnitudeKey,
                };

                if (modifier.MagnitudeSource == EMagnitudeSource.SetByCaller
                    && TryFindSetByCallerValue(in command, setByCallerValues, modifier.MagnitudeKey, out var setByCallerValue))
                {
                    context.HasSetByCallerValue = 1;
                    context.SetByCallerValue = setByCallerValue;
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute)
                {
                    magnitudeSourceCounters.SourceAttributeLookupCount++;
                    if (TryReadSourceAttributeValue(
                            ref ownerResources,
                            in command,
                            modifierIndex,
                            in modifier,
                            ref magnitudeSourceCounters,
                            out var sourceValue))
                    {
                        context.HasSourceAttributeValue = 1;
                        context.SourceAttributeValue = sourceValue;
                    }
                    else
                    {
                        magnitudeSourceCounters.FallbackValueCount++;
                    }
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute)
                {
                    magnitudeSourceCounters.TargetAttributeLookupCount++;
                    magnitudeSourceCounters.CurrentValueLookupCount++;
                    if (TryReadOwnerAttributeValue(ref ownerResources, in modifier, out var targetValue))
                    {
                        context.HasTargetAttributeValue = 1;
                        context.TargetAttributeValue = targetValue;
                    }
                    else
                    {
                        magnitudeSourceCounters.FallbackValueCount++;
                    }
                }

                return context;
            }

            private bool TryReadSourceAttributeValue(
                ref ActiveMutationOwnerResources ownerResources,
                in GEEffectCommandBuffer command,
                int modifierIndex,
                in GASCatalogModifierDefinitionBlob modifier,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                out float value)
            {
                if (CompareEntity(ownerResources.Owner, command.SourceAsc) == 0)
                {
                    magnitudeSourceCounters.CurrentValueLookupCount++;
                    return TryReadOwnerAttributeValue(ref ownerResources, in modifier, out value);
                }

                if (command.SourceAsc == Entity.Null || !ActiveMutationSourceAttributeSnapshots.IsCreated)
                {
                    magnitudeSourceCounters.CaptureMissCount++;
                    value = 0f;
                    return false;
                }

                var snapshotKey = MakeActiveMutationSourceAttributeSnapshotKey(command.Sequence, modifierIndex);
                if (ActiveMutationSourceAttributeSnapshots.TryGetValue(snapshotKey, out value))
                {
                    magnitudeSourceCounters.CapturedValueHitCount++;
                    return true;
                }

                magnitudeSourceCounters.CaptureMissCount++;
                return false;
            }

            private bool TryReadOwnerAttributeValue(
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                if (!ownerResources.HasAttributes)
                    return false;

                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attributeCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attributes = ownerResources.Attributes;
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attributeCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private int RemoveActiveModifiersForSlot(
                ref ActiveMutationOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasActiveModifiers)
                    return 0;

                var modifiers = ownerResources.ActiveModifiers;
                var hasAttributes = ownerResources.HasAttributes;
                var attributes = hasAttributes ? ownerResources.Attributes : default;
                var removed = 0;
                for (var i = modifiers.Length - 1; i >= 0; i--)
                {
                    var modifier = modifiers[i];
                    if (modifier.SourceSequence != slotSequence
                        || modifier.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    modifiers.RemoveAt(i);
                    if (hasAttributes)
                        MarkCurrentValueDirty(attributes, ownerResources.Owner, modifier.AttrSetCode, modifier.AttributeCode);
                    removed++;
                }

                RefreshActiveModifierPresence(ownerResources.Owner, modifiers);
                return removed;
            }

            private int ApplyGrantedTags(
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                int slotSequence)
            {
                if (gameplayEffect.GrantedTagMaskIndex < 0
                    || gameplayEffect.GrantedTagMaskIndex >= catalog.TagMasks.Length
                    || !ownerResources.HasTagSources)
                {
                    return 0;
                }

                var grantedMask = catalog.TagMasks[gameplayEffect.GrantedTagMaskIndex].Mask;
                var ownerTags = ownerResources.TargetTags;
                var sources = ownerResources.TagSources;
                for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
                {
                    if (!grantedMask.HasTag(tagIndex)
                        || HasTempTagSource(sources, tagIndex, slotSequence, gameplayEffect.GameplayEffectCode))
                    {
                        continue;
                    }

                    var wasActive = ownerTags.HasTag(tagIndex);
                    ownerTags.AddTag(tagIndex);
                    sources.Add(new TagTemporarySourceBuffer
                    {
                        TagIndex = tagIndex,
                        Source = Entity.Null,
                        SourceSequence = slotSequence,
                        SourceGameplayEffectCode = gameplayEffect.GameplayEffectCode,
                    });

                    if (!wasActive)
                    {
                        EnqueueTagChangedEvent(ownerResources.Owner, tagIndex, true);
                    }
                }

                ownerResources.TargetTags = ownerTags;
                ownerResources.TargetTagsDirty = true;
                return CountTempTagSourcesForSlot(sources, slotSequence, gameplayEffect.GameplayEffectCode);
            }

            private void RemoveGrantedTagsForSlot(
                ref ActiveMutationOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasTagSources)
                    return;

                var sources = ownerResources.TagSources;
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    var source = sources[i];
                    if (source.SourceSequence != slotSequence
                        || source.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    sources.RemoveAt(i);
                    var removedFromMask = RemoveTagIndexFromEffectiveMaskIfUnreferenced(ref ownerResources, source.TagIndex);
                    if (removedFromMask)
                    {
                        EnqueueTagChangedEvent(ownerResources.Owner, source.TagIndex, false);
                    }
                }
            }

            private bool RemoveTagIndexFromEffectiveMaskIfUnreferenced(ref ActiveMutationOwnerResources ownerResources, int tagIndex)
            {
                if (ownerResources.HasFixedTags
                    && ownerResources.FixedTags.Mask.HasTag(tagIndex))
                {
                    return false;
                }
                if (HasAnyTemporarySourceForTag(ownerResources.TagSources, tagIndex))
                    return false;

                var mask = ownerResources.TargetTags;
                if (!mask.HasTag(tagIndex))
                    return false;
                mask.RemoveTag(tagIndex);
                ownerResources.TargetTags = mask;
                ownerResources.TargetTagsDirty = true;
                return true;
            }

            private static bool HasAnyTemporarySourceForTag(
                DynamicBuffer<TagTemporarySourceBuffer> sources,
                int tagIndex)
            {
                for (var i = 0; i < sources.Length; i++)
                {
                    if (sources[i].TagIndex == tagIndex)
                        return true;
                }

                return false;
            }

            private int ApplyGrantedAbilities(
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                int slotSequence)
            {
                if (gameplayEffect.GrantedAbilityCount <= 0
                    || !ownerResources.HasAbilitySlots)
                {
                    return 0;
                }

                var grantedAbilities = ownerResources.AbilitySlots;
                var added = 0;
                for (var i = 0; i < gameplayEffect.GrantedAbilityCount; i++)
                {
                    var grantedIndex = gameplayEffect.GrantedAbilityStart + i;
                    if ((uint)grantedIndex >= (uint)catalog.GrantedAbilities.Length)
                        continue;

                    var granted = catalog.GrantedAbilities[grantedIndex];
                    if (granted.AbilityCode <= 0
                        || HasGrantedAbilityForSlot(grantedAbilities, granted.AbilityCode, slotSequence, gameplayEffect.GameplayEffectCode))
                    {
                        continue;
                    }

                    var ability = StructuralEcb.CreateEntity(GrantedAbilityArchetype);
                    GASRuntimeEntityArchetypes.InitializeAbilityEntity(StructuralEcb, ability);
                    StructuralEcb.SetComponent(
                        ability,
                        AbilityStateComponent.Create(granted.AbilityCode, granted.Level, ownerResources.Owner));
                    StructuralEcb.SetComponent(ability, new AbilityMainTargetComponent { TargetAsc = Entity.Null });
                    StructuralEcb.SetComponent(ability, new AbilityGrantedByEffectComponent
                    {
                        SourceEffect = Entity.Null,
                        SourceSequence = slotSequence,
                        SourceGameplayEffectCode = gameplayEffect.GameplayEffectCode,
                        ActivationPolicy = (GrantedAbilityActivationPolicy)granted.ActivationPolicy,
                        DeactivationPolicy = GrantedAbilityDeactivationPolicy.SyncWithEffect,
                        RemovePolicy = granted.RemovePolicy == 0
                            ? GrantedAbilityRemovePolicy.SyncWithEffect
                            : (GrantedAbilityRemovePolicy)granted.RemovePolicy,
                    });

                    StructuralEcb.AppendToBuffer(ownerResources.Owner, new AbilitySlotBuffer { AbilityEntity = ability });
                    added++;

                    var activationPolicy = (GrantedAbilityActivationPolicy)granted.ActivationPolicy;
                    StructuralEcb.SetComponentEnabled<AbilityActivationPendingComponent>(
                        ability,
                        activationPolicy == GrantedAbilityActivationPolicy.WhenAdded
                        || activationPolicy == GrantedAbilityActivationPolicy.SyncWithEffect);
                }

                return CountGrantedAbilitiesForSlot(grantedAbilities, slotSequence, gameplayEffect.GameplayEffectCode) + added;
            }

            private void RemoveGrantedAbilitiesForSlot(
                ref ActiveMutationOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasAbilitySlots)
                    return;

                var grantedAbilities = ownerResources.AbilitySlots;
                for (var i = grantedAbilities.Length - 1; i >= 0; i--)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (!IsGrantedBySlot(ability, slotSequence, gameplayEffectCode))
                        continue;

                    grantedAbilities.RemoveAt(i);
                    RemoveGrantedAbilityEntity(ability);
                }
            }

            private bool HasGrantedAbilityForSlot(
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                int abilityCode,
                int slotSequence,
                int gameplayEffectCode)
            {
                for (var i = 0; i < grantedAbilities.Length; i++)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (!IsGrantedBySlot(ability, slotSequence, gameplayEffectCode)
                        || !AbilityStateLookup.HasComponent(ability))
                    {
                        continue;
                    }

                    if (AbilityStateLookup[ability].Code == abilityCode)
                        return true;
                }

                return false;
            }

            private int CountGrantedAbilitiesForSlot(
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                int slotSequence,
                int gameplayEffectCode)
            {
                var count = 0;
                for (var i = 0; i < grantedAbilities.Length; i++)
                {
                    if (IsGrantedBySlot(grantedAbilities[i].AbilityEntity, slotSequence, gameplayEffectCode))
                        count++;
                }

                return count;
            }

            private bool IsGrantedBySlot(
                Entity ability,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (ability == Entity.Null
                    || !AbilityGrantedLookup.HasComponent(ability))
                {
                    return false;
                }

                var granted = AbilityGrantedLookup[ability];
                return granted.SourceSequence == slotSequence
                    && granted.SourceGameplayEffectCode == gameplayEffectCode;
            }

            private void RemoveGrantedAbilityEntity(Entity ability)
            {
                if (ability == Entity.Null || !AbilityStateLookup.HasComponent(ability))
                    return;

                var runtime = AbilityStateLookup[ability];
                var isRunning = runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
                if (isRunning)
                {
                    EnqueueAbilityLifecycleRequest(ability, runtime);
                    return;
                }

                StructuralEcb.DestroyEntity(ability);
            }

            private void EnqueueAbilityLifecycleRequest(Entity ability, in AbilityStateComponent runtime)
            {
                if (EventBusEntity == Entity.Null
                    || !AbilityLifecycleRequestLookup.HasBuffer(EventBusEntity))
                    return;

                var requests = AbilityLifecycleRequestLookup[EventBusEntity];
                requests.Add(new AbilityLifecycleRequestBuffer
                {
                    Sequence = requests.Length,
                    RequestKind = EAbilityLifecycleRequestKind.Cancel,
                    Reason = EAbilityLifecycleReason.GrantedEffectRemoved,
                    Ability = ability,
                    SourceAbility = Entity.Null,
                    SourceEffect = Entity.Null,
                    SourceAbilityCode = 0,
                    DestroyOnCleanup = 1,
                });
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.AbilityCancelRequested,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = runtime.Owner,
                    TargetAsc = runtime.Owner,
                    SourceAbility = ability,
                    EventCode = runtime.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.GrantedEffectRemoved,
                });
            }

            private void RemoveGameplayEffectsWithTags(
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogGameplayEffectDefinitionBlob appliedGameplayEffect)
            {
                var removeQuery = appliedGameplayEffect.RemoveGameplayEffectTagQuery;
                if (removeQuery.IsEmpty)
                    return;

                for (var i = ownerResources.Slots.Length - 1; i >= 0; i--)
                {
                    var slot = ownerResources.Slots[i];
                    if (!GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var slotGameplayEffectIndex))
                        continue;

                    ref readonly var slotGameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, slotGameplayEffectIndex);
                    if (slotGameplayEffect.GrantedTagMaskIndex < 0
                        || slotGameplayEffect.GrantedTagMaskIndex >= catalog.TagMasks.Length)
                    {
                        continue;
                    }

                    var slotGrantedMask = catalog.TagMasks[slotGameplayEffect.GrantedTagMaskIndex].Mask;
                    if (removeQuery.Evaluate(slotGrantedMask))
                        RemoveSlotAt(ref ownerResources, i);
                }
            }

            private void RemoveSlotAt(
                ref ActiveMutationOwnerResources ownerResources,
                int slotIndex)
            {
                if ((uint)slotIndex >= (uint)ownerResources.Slots.Length)
                    return;

                var slot = ownerResources.Slots[slotIndex];
                var activeModifierCount = RemoveActiveModifiersForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedTagsForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedAbilitiesForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                GASActiveEffectRuntime.RemoveSetByCallerSnapshotForSlot(
                    ownerResources.SetByCallerSnapshot,
                    slot.Sequence,
                    slot.GameplayEffectCode);
                RecordCleanup(ref ownerResources, in slot, activeModifierCount);
                ownerResources.Mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    Frame = Frame,
                    Kind = ActiveEffectMutationKind.Remove,
                    ActiveEffect = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    ContextId = slot.ContextId,
                    ParentContextId = slot.ParentContextId,
                    StackCount = slot.StackCount,
                    DurationFrameOverride = slot.DurationFrame,
                    PeriodFrame = slot.PeriodFrame,
                });
                EnqueueRemovedEvent(in slot);
                ownerResources.Slots.RemoveAt(slotIndex);
            }

            private void RecordCleanup(
                ref ActiveMutationOwnerResources ownerResources,
                in ActiveGameplayEffectBuffer slot,
                int activeModifierCount)
            {
                if (!ownerResources.HasCleanupRecords)
                    return;

                var records = ownerResources.CleanupRecords;
                while (records.Length >= ActiveEffectStore.MaxCleanupRecordCount)
                    records.RemoveAt(0);

                var cleanupFlags = ActiveEffectCleanupWorkFlags.OwnerLocalSlot;
                if (activeModifierCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.RuntimeModifiers;
                if (slot.ActiveGrantedTagCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.GrantedTags;
                if (slot.ActiveGrantedAbilityCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.GrantedAbilities;

                records.Add(new ActiveGameplayEffectCleanupRecordBuffer
                {
                    Sequence = slot.Sequence,
                    ActiveEffectEntity = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    Instigator = slot.Instigator,
                    Causer = slot.Causer,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    Level = slot.Level,
                    StackCount = slot.StackCount,
                    ContextId = slot.ContextId,
                    ParentContextId = slot.ParentContextId,
                    CleanupFrame = Frame,
                    CleanupState = EGameplayEffectLifecycleState.PendingRemove,
                    SlotState = ActiveEffectSlotState.PendingRemove,
                    PreviousSlotState = slot.State,
                    DurationFrame = slot.DurationFrame,
                    RemainingFrame = slot.RemainingFrame,
                    PeriodFrame = slot.PeriodFrame,
                    LastPeriodFrame = slot.LastPeriodFrame,
                    ActiveGrantedTagCount = slot.ActiveGrantedTagCount,
                    ActiveGrantedAbilityCount = slot.ActiveGrantedAbilityCount,
                    ActiveModifierCount = activeModifierCount,
                    RequestedCleanupWorkFlags = (int)cleanupFlags,
                    ResolvedCleanupWorkFlags = (int)cleanupFlags,
                    CleanupResolvedFrame = Frame,
                    Flags = slot.Flags,
                });

                ownerResources.Store.LastCleanupFrame = Frame;
                ownerResources.Store.CleanupRecordCount = records.Length;
            }

            private void EmitOverflowCommand(
                ref GEEffectCommandStreamComponent stream,
                ref GASDefinitionCatalogBlob catalog,
                in GEEffectCommandBuffer sourceCommand,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
            {
                if (gameplayEffect.OverflowGameplayEffectCode <= 0
                    || gameplayEffect.OverflowGameplayEffectCode == sourceCommand.GameplayEffectCode
                    || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.OverflowGameplayEffectCode, out var overflowIndex))
                {
                    return;
                }

                ref readonly var overflowEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, overflowIndex);
                var durationFrame = overflowEffect.DurationFrames;
                var kind = RequiresActiveMutationLane(in overflowEffect, durationFrame)
                    ? GEEffectCommandKind.ActiveMutation
                    : GEEffectCommandKind.Instant;
                var command = new GEEffectCommandBuffer
                {
                    Frame = Frame,
                    Kind = kind,
                    Source = GEEffectCommandSource.Overflow,
                    SourceAsc = sourceCommand.SourceAsc,
                    TargetAsc = sourceCommand.TargetAsc,
                    SourceAbility = sourceCommand.SourceAbility,
                    SourceEffect = sourceCommand.SourceEffect,
                    Instigator = sourceCommand.Instigator,
                    Causer = sourceCommand.Causer,
                    GameplayEffectCode = gameplayEffect.OverflowGameplayEffectCode,
                    Level = sourceCommand.Level,
                    DurationFrameOverride = durationFrame,
                    ParentContextId = sourceCommand.ContextId,
                    TargetDataKind = sourceCommand.TargetDataKind,
                    Flags = kind == GEEffectCommandKind.ActiveMutation ? GASGECommandSeedFlags.ActiveMutation : GASGECommandSeedFlags.None,
                };

                if (kind == GEEffectCommandKind.ActiveMutation)
                {
                    var targetAsc = command.TargetAsc != Entity.Null ? command.TargetAsc : command.SourceAsc;
                    if (targetAsc == Entity.Null
                        || !NextFrameActiveMutationCommandLookup.HasBuffer(targetAsc)
                        || !NextFrameActiveMutationSetByCallerLookup.HasBuffer(targetAsc))
                    {
                        return;
                    }

                    var ownerSetByCallerValues = NextFrameActiveMutationSetByCallerLookup[targetAsc];
                    var ownerCommand = PrepareCommand(
                        ref stream,
                        ownerSetByCallerValues.Length,
                        in command,
                        0,
                        Frame);
                    NextFrameActiveMutationCommandLookup[targetAsc].Add(new ActiveEffectNextFrameMutationCommandBuffer
                    {
                        Command = ownerCommand,
                    });
                    return;
                }

                var instantTargetAsc = command.TargetAsc != Entity.Null ? command.TargetAsc : command.SourceAsc;
                if (instantTargetAsc == Entity.Null
                    || !NextFrameInstantCommandLookup.HasBuffer(instantTargetAsc)
                    || !NextFrameInstantSetByCallerLookup.HasBuffer(instantTargetAsc))
                {
                    return;
                }

                var instantSetByCallerValues = NextFrameInstantSetByCallerLookup[instantTargetAsc];
                var instantCommand = PrepareCommand(
                    ref stream,
                    instantSetByCallerValues.Length,
                    in command,
                    0,
                    Frame);
                NextFrameInstantCommandLookup[instantTargetAsc].Add(new OwnerLocalInstantNextFrameCommandBuffer
                {
                    Command = instantCommand,
                });
            }

            private GEEffectCommandBuffer PrepareCommand(
                ref GEEffectCommandStreamComponent stream,
                int setByCallerStart,
                in GEEffectCommandBuffer command,
                int setByCallerCount,
                int currentFrame)
            {
                var resolved = command;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = GASRuntimeSequenceAllocator.AllocateCommandSequence(ref stream);
                if (resolved.Frame <= 0)
                    resolved.Frame = currentFrame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = GASRuntimeSequenceAllocator.AllocateContextId(ref stream);
                if (resolved.TargetAsc == Entity.Null)
                    resolved.TargetAsc = resolved.SourceAsc;
                if (resolved.Instigator == Entity.Null)
                    resolved.Instigator = resolved.SourceAsc;
                if (resolved.Causer == Entity.Null)
                    resolved.Causer = resolved.SourceAbility;

                resolved.SetByCallerStart = setByCallerStart;
                resolved.SetByCallerCount = setByCallerCount;
                return resolved;
            }

            private void MarkActiveModifierAdded(Entity asc)
            {
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent);
            }

            private void RefreshActiveModifierPresence(
                Entity asc,
                DynamicBuffer<AttributeActiveModifierBuffer> modifiers)
            {
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent);
            }

            private void MarkCurrentValueDirty(
                DynamicBuffer<AttributeValueBuffer> attributes,
                Entity asc,
                int attrSetCode,
                int attrCode)
            {
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
                if (attrIndex == -1)
                    return;

                var attr = attributes[attrIndex];
                attr.Dirty = true;
                attributes[attrIndex] = attr;
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.MarkDirty);
            }

            private void EnqueueAttributeOwnerMarkerRequest(Entity asc, EAttributeOwnerMarkerRequestKind requestKind)
            {
                if (asc == Entity.Null
                    || EventBusEntity == Entity.Null
                    || !AttributeOwnerMarkerRequestLookup.HasBuffer(EventBusEntity))
                {
                    return;
                }

                var requests = AttributeOwnerMarkerRequestLookup[EventBusEntity];
                requests.Add(new AttributeOwnerMarkerRequestBuffer
                {
                    Sequence = requests.Length,
                    ASC = asc,
                    RequestKind = requestKind,
                    Value = 1,
                });
            }

            private void EnqueueStackOverflowEvent(
                in GEEffectCommandBuffer command,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.StackOverflow,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    ContextId = command.ContextId,
                    EventCode = command.GameplayEffectCode,
                    ReasonCode = gameplayEffect.OverflowGameplayEffectCode,
                });
            }

            private void EnqueueStackOverflowDeniedEvent(in GEEffectCommandBuffer command)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.StackOverflowDenied,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.Failure,
                    Severity = EGameplayFactSeverity.Warning,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    ContextId = command.ContextId,
                    EventCode = command.GameplayEffectCode,
                });
            }

            private void EnqueueStackClearedByOverflowEvent(in GEEffectCommandBuffer command)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.StackClearedByOverflow,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    ContextId = command.ContextId,
                    EventCode = command.GameplayEffectCode,
                });
            }

            private void EnqueueAppliedEvents(
                in GEEffectCommandBuffer command,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                Entity gameplayEffectEntity)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.GameplayEffectApplied,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = gameplayEffectEntity,
                    ContextId = command.ContextId,
                    EventCode = command.GameplayEffectCode,
                });

                if (gameplayEffect.GameplayCueCode <= 0)
                    return;

                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.CueRequested,
                    Domain = EGameplayFactDomain.Cue,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = gameplayEffectEntity,
                    ContextId = command.ContextId,
                    EventCode = (int)EGameplayCueEvent.OnApply,
                    ReasonCode = gameplayEffect.GameplayCueCode,
                });
            }

            private void EnqueueRemovedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.GameplayEffectRemoved,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    ContextId = slot.ContextId,
                    EventCode = slot.GameplayEffectCode,
                });
            }

            private void EnqueueGameplayEvent(GameplayEventBuffer evt)
            {
                var owner = evt.TargetAsc != Entity.Null ? evt.TargetAsc : evt.SourceAsc;
                if (owner == Entity.Null || !OwnerFactLookup.HasBuffer(owner))
                    return;

                evt.Frame = Frame;
                if (StreamLookup.HasComponent(StreamEntity))
                {
                    var stream = StreamLookup[StreamEntity];
                    evt.Sequence = GASRuntimeSequenceAllocator.AllocateFactSequence(ref stream);
                    StreamLookup[StreamEntity] = stream;
                }
                else
                {
                    evt.Sequence = 0;
                }

                OwnerFactLookup[owner].Add(new OwnerLocalGameplayFactBuffer
                {
                    Fact = evt,
                });
            }

            private void EnqueueTagChangedEvent(Entity owner, int tagIndex, bool added)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.TagChanged,
                    Domain = EGameplayFactDomain.Tag,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    TargetAsc = owner,
                    EventCode = tagIndex,
                    ReasonCode = added ? 1 : 0,
                });
            }
        }

        [BurstCompile]
        public struct GEActiveEffectPreTickSourceAttributeSnapshotGatherJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            [ReadOnly] public BufferLookup<AttributeValueBuffer> AttributeLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public NativeParallelHashMap<ActiveEffectSlotSourceAttributeSnapshotKey, float>.ParallelWriter ActiveEffectSlotSourceAttributeSnapshots;
            [NativeDisableParallelForRestriction] public NativeArray<ActiveEffectSlotSourceSnapshotLaneCounters> SnapshotLaneCounters;
            public int Frame;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated)
                    return;

                ref var catalog = ref Catalog.Value;
                var owners = chunk.GetNativeArray(EntityTypeHandle);
                var slotBuffers = chunk.GetBufferAccessorRO(ref ActiveEffectSlotBufferTypeHandle);
                var snapshotLaneCounters = default(ActiveEffectSlotSourceSnapshotLaneCounters);
                var hasSnapshotLaneCounters = SnapshotLaneCounters.IsCreated
                    && (uint)unfilteredChunkIndex < (uint)SnapshotLaneCounters.Length;
                if (hasSnapshotLaneCounters)
                    snapshotLaneCounters = SnapshotLaneCounters[unfilteredChunkIndex];

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = owners[entityIndex];
                    var slots = slotBuffers[entityIndex];
                    for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                    {
                        var slot = slots[slotIndex];
                        var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, Frame);
                        if (actionFlags == (int)ActiveEffectTickActionFlags.None
                            || slot.SourceAsc == Entity.Null
                            || CompareEntity(slot.SourceAsc, owner) == 0
                            || !AttributeLookup.HasBuffer(slot.SourceAsc)
                            || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                        {
                            continue;
                        }

                        ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                        if (gameplayEffect.ModifierCount <= 0)
                            continue;

                        var sourceAttributes = AttributeLookup[slot.SourceAsc];
                        for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                        {
                            var modifierIndex = gameplayEffect.ModifierStart + i;
                            if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                                continue;

                            var modifier = catalog.Modifiers[modifierIndex];
                            if (modifier.MagnitudeSource != EMagnitudeSource.SourceAttribute)
                            {
                                continue;
                            }

                            if (!TryReadSnapshotAttributeValue(sourceAttributes, in modifier, out var sourceValue))
                            {
                                snapshotLaneCounters.AttributeMissCount++;
                                continue;
                            }

                            var snapshotKey = MakeActiveEffectSlotSourceAttributeSnapshotKey(owner, slot.Sequence, modifierIndex);
                            snapshotLaneCounters.RecordSnapshotWrite(
                                ActiveEffectSlotSourceAttributeSnapshots.TryAdd(snapshotKey, sourceValue));
                        }
                    }
                }

                if (hasSnapshotLaneCounters)
                    SnapshotLaneCounters[unfilteredChunkIndex] = snapshotLaneCounters;
            }

            private static bool TryReadSnapshotAttributeValue(
                DynamicBuffer<AttributeValueBuffer> attributes,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attrCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private static int CompareEntity(Entity left, Entity right)
            {
                var result = left.Index.CompareTo(right.Index);
                if (result != 0)
                    return result;

                return left.Version.CompareTo(right.Version);
            }
        }

        [BurstCompile]
        public struct GEActiveEffectPreTickJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<ASCActiveEffectsComponent> ActiveEffectsTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            public BufferTypeHandle<ActiveEffectMutationBuffer> MutationBufferTypeHandle;
            public BufferTypeHandle<GERemoveCommandBuffer> RemoveCommandBufferTypeHandle;
            public BufferLookup<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecordLookup;
            public BufferLookup<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshotLookup;
            public BufferLookup<AttributeValueBuffer> AttributeLookup;
            public BufferLookup<AttributeActiveModifierBuffer> ActiveModifierLookup;
            public BufferLookup<AttributeOwnerMarkerRequestBuffer> AttributeOwnerMarkerRequestLookup;
            public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public ComponentLookup<TagFixedMaskComponent> TagFixedMaskLookup;
            public BufferLookup<TagTemporarySourceBuffer> TagSourceLookup;
            public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;
            [ReadOnly] public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> AbilityGrantedLookup;
            public ComponentTypeHandle<GERemoveCommandPendingComponent> RemovePendingTypeHandle;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<GESetByCallerValueBuffer> CommandSetByCallerLookup;
            public BufferLookup<ActiveEffectMutationCommandBuffer> ActiveMutationCommandLookup;
            public BufferLookup<ActiveEffectMutationSetByCallerValueBuffer> ActiveMutationSetByCallerLookup;
            public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;
            public BufferLookup<OwnerLocalGameplayFactBuffer> OwnerFactLookup;
            public EntityCommandBuffer StructuralEcb;
            public EntityArchetype GrantedAbilityArchetype;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            [ReadOnly] public NativeParallelHashMap<ActiveEffectSlotSourceAttributeSnapshotKey, float> ActiveEffectSlotSourceAttributeSnapshots;
            [NativeDisableParallelForRestriction] public NativeArray<ActiveEffectSlotSourceSnapshotLaneCounters> SnapshotLaneCounters;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;
            public bool ProcessTickRecords;
            public bool ProcessExplicitRemoveCommands;

            private struct ActiveEffectOwnerResources
            {
                public Entity Owner;
                public ASCActiveEffectsComponent Store;
                public DynamicBuffer<ActiveGameplayEffectBuffer> Slots;
                public bool HasSetByCallerSnapshot;
                public DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshot;
                public bool HasTargetTags;
                public TagMaskComponent TargetTags;
                public bool TargetTagsDirty;
                public bool HasFixedTags;
                public TagFixedMaskComponent FixedTags;
                public bool HasAttributes;
                public DynamicBuffer<AttributeValueBuffer> Attributes;
                public bool HasActiveModifiers;
                public DynamicBuffer<AttributeActiveModifierBuffer> ActiveModifiers;
                public bool HasTagSources;
                public DynamicBuffer<TagTemporarySourceBuffer> TagSources;
                public bool HasAbilitySlots;
                public DynamicBuffer<AbilitySlotBuffer> AbilitySlots;
                public bool HasCleanupRecords;
                public DynamicBuffer<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecords;
            }

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if ((!ProcessTickRecords && !ProcessExplicitRemoveCommands)
                    || StreamEntity == Entity.Null
                    || (ProcessTickRecords && !Catalog.IsCreated))
                {
                    return;
                }

                var owners = chunk.GetNativeArray(EntityTypeHandle);
                var stores = chunk.GetNativeArray(ref ActiveEffectsTypeHandle);
                var slotBuffers = chunk.GetBufferAccessor(ref ActiveEffectSlotBufferTypeHandle);
                var mutationBuffers = chunk.GetBufferAccessor(ref MutationBufferTypeHandle);
                var magnitudeSourceCounters = default(ActiveEffectMagnitudeSourceCounters);
                var snapshotLaneCounters = default(ActiveEffectSlotSourceSnapshotLaneCounters);
                var preTickLaneCounters = default(ActiveEffectPreTickLaneCounters);
                preTickLaneCounters.ChunkCount++;
                var hasSnapshotLaneCounters = SnapshotLaneCounters.IsCreated
                    && (uint)unfilteredChunkIndex < (uint)SnapshotLaneCounters.Length;
                if (hasSnapshotLaneCounters)
                    snapshotLaneCounters = SnapshotLaneCounters[unfilteredChunkIndex];

                var removeCommandBuffers = ProcessExplicitRemoveCommands
                    ? chunk.GetBufferAccessor(ref RemoveCommandBufferTypeHandle)
                    : default;
                EnabledMask removePendingMask = default;
                if (ProcessExplicitRemoveCommands)
                    removePendingMask = chunk.GetEnabledMask(ref RemovePendingTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    preTickLaneCounters.ScannedOwnerCount++;
                    var owner = owners[entityIndex];
                    var mutations = mutationBuffers[entityIndex];
                    var mutationLengthBefore = mutations.Length;
                    var processedOwner = false;
                    var ownerResources = CaptureActiveEffectOwnerResources(
                        owner,
                        stores[entityIndex],
                        slotBuffers[entityIndex]);

                    if (ProcessTickRecords)
                    {
                        if (ownerResources.Store.ChunkSkipMatchedSlotCount > 0
                            || ownerResources.Store.ChunkSkipDuePeriodSlotCount > 0
                            || ownerResources.Store.CleanupRecordCount > 0)
                        {
                            processedOwner = true;
                            ref var catalog = ref Catalog.Value;
                            ProcessTickOwner(
                                ref ownerResources,
                                mutations,
                                ref catalog,
                                ref magnitudeSourceCounters,
                                ref snapshotLaneCounters,
                                ref preTickLaneCounters);
                        }
                    }

                    if (ProcessExplicitRemoveCommands)
                    {
                        var removeCommands = removeCommandBuffers[entityIndex];
                        if (removeCommands.Length > 0)
                            processedOwner = true;
                        for (var commandIndex = 0; commandIndex < removeCommands.Length; commandIndex++)
                        {
                            RemoveMatchingOwnerLocalEffects(
                                ref ownerResources,
                                removeCommands[commandIndex].GameplayEffectCode,
                                mutations);
                        }

                        removeCommands.Clear();
                        removePendingMask[entityIndex] = false;
                    }

                    if (processedOwner)
                    {
                        preTickLaneCounters.ProcessedOwnerCount++;
                    }
                    else
                    {
                        preTickLaneCounters.SkippedOwnerCount++;
                    }

                    var mutationWriteCount = mutations.Length - mutationLengthBefore;
                    if (mutationWriteCount > 0)
                        preTickLaneCounters.MutationWriteCount += mutationWriteCount;

                    ActiveEffectStore.RefreshChunkSkipIndexCounters(ref ownerResources.Store, ownerResources.Slots, Frame);
                    FlushActiveEffectOwnerResources(ref ownerResources);
                    stores[entityIndex] = ownerResources.Store;
                }

                if (hasSnapshotLaneCounters)
                    SnapshotLaneCounters[unfilteredChunkIndex] = snapshotLaneCounters;

                FlushMagnitudeSourceCounters(ref magnitudeSourceCounters, ref snapshotLaneCounters, ref preTickLaneCounters);
            }

            private ActiveEffectOwnerResources CaptureActiveEffectOwnerResources(
                Entity owner,
                in ASCActiveEffectsComponent store,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots)
            {
                var ownerResources = new ActiveEffectOwnerResources
                {
                    Owner = owner,
                    Store = store,
                    Slots = slots,
                    HasSetByCallerSnapshot = SetByCallerSnapshotLookup.HasBuffer(owner),
                    HasTargetTags = TagMaskLookup.HasComponent(owner),
                    HasFixedTags = TagFixedMaskLookup.HasComponent(owner),
                    HasAttributes = AttributeLookup.HasBuffer(owner),
                    HasActiveModifiers = ActiveModifierLookup.HasBuffer(owner),
                    HasTagSources = TagSourceLookup.HasBuffer(owner),
                    HasAbilitySlots = AbilitySlotLookup.HasBuffer(owner),
                    HasCleanupRecords = CleanupRecordLookup.HasBuffer(owner),
                };

                ownerResources.SetByCallerSnapshot = ownerResources.HasSetByCallerSnapshot
                    ? SetByCallerSnapshotLookup[owner]
                    : default;
                ownerResources.TargetTags = ownerResources.HasTargetTags
                    ? TagMaskLookup[owner]
                    : default;
                ownerResources.FixedTags = ownerResources.HasFixedTags
                    ? TagFixedMaskLookup[owner]
                    : default;
                ownerResources.Attributes = ownerResources.HasAttributes
                    ? AttributeLookup[owner]
                    : default;
                ownerResources.ActiveModifiers = ownerResources.HasActiveModifiers
                    ? ActiveModifierLookup[owner]
                    : default;
                ownerResources.TagSources = ownerResources.HasTagSources
                    ? TagSourceLookup[owner]
                    : default;
                ownerResources.AbilitySlots = ownerResources.HasAbilitySlots
                    ? AbilitySlotLookup[owner]
                    : default;
                ownerResources.CleanupRecords = ownerResources.HasCleanupRecords
                    ? CleanupRecordLookup[owner]
                    : default;

                return ownerResources;
            }

            private void FlushActiveEffectOwnerResources(ref ActiveEffectOwnerResources ownerResources)
            {
                if (ownerResources.Owner == Entity.Null)
                    return;

                if (ownerResources.TargetTagsDirty && ownerResources.HasTargetTags)
                    TagMaskLookup[ownerResources.Owner] = ownerResources.TargetTags;
            }

            private void FlushMagnitudeSourceCounters(
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters,
                ref ActiveEffectPreTickLaneCounters preTickLaneCounters)
            {
                if (!magnitudeSourceCounters.HasEvidence
                    && !snapshotLaneCounters.HasEvidence
                    && !preTickLaneCounters.HasEvidence)
                {
                    return;
                }

                if (StreamEntity == Entity.Null || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                magnitudeSourceCounters.AddToStream(ref stream);
                snapshotLaneCounters.AddToStream(ref stream);
                preTickLaneCounters.AddToStream(ref stream);
                StreamLookup[StreamEntity] = stream;
            }

            private static int CompareEntity(Entity left, Entity right)
            {
                var result = left.Index.CompareTo(right.Index);
                if (result != 0)
                    return result;

                return left.Version.CompareTo(right.Version);
            }

            private void ProcessTickOwner(
                ref ActiveEffectOwnerResources ownerResources,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters,
                ref ActiveEffectPreTickLaneCounters preTickLaneCounters)
            {
                if (!StreamLookup.HasComponent(StreamEntity))
                    return;

                for (var slotIndex = ownerResources.Slots.Length - 1; slotIndex >= 0; slotIndex--)
                {
                    var slot = ownerResources.Slots[slotIndex];
                    var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, Frame);
                    preTickLaneCounters.ScannedSlotCount++;
                    if (actionFlags == (int)ActiveEffectTickActionFlags.None)
                    {
                        preTickLaneCounters.NoopSlotCount++;
                        continue;
                    }

                    preTickLaneCounters.DueSlotCount++;

                    if ((actionFlags & (int)ActiveEffectTickActionFlags.Period) != 0)
                    {
                        EmitPeriodCommand(ref catalog, in slot, ref ownerResources);
                        slot.LastPeriodFrame = Frame;
                        ownerResources.Slots[slotIndex] = slot;
                        mutations.Add(new ActiveEffectMutationBuffer
                        {
                            Sequence = slot.Sequence,
                            Frame = Frame,
                            Kind = ActiveEffectMutationKind.PeriodTick,
                            ActiveEffect = Entity.Null,
                            SourceAsc = slot.SourceAsc,
                            TargetAsc = ownerResources.Owner,
                            SourceAbility = slot.SourceAbility,
                            SourceEffect = slot.SourceEffect,
                            GameplayEffectCode = slot.GameplayEffectCode,
                            ContextId = slot.ContextId,
                            ParentContextId = slot.ParentContextId,
                            StackCount = slot.StackCount,
                            DurationFrameOverride = slot.DurationFrame,
                            PeriodFrame = slot.PeriodFrame,
                        });
                    }

                    if ((actionFlags & (int)ActiveEffectTickActionFlags.DurationExpire) != 0)
                    {
                        HandleDurationExpired(
                            ref ownerResources,
                                slotIndex,
                                ref catalog,
                                mutations,
                                ref magnitudeSourceCounters,
                                ref snapshotLaneCounters);
                    }
                }
            }

            private void RemoveMatchingOwnerLocalEffects(
                ref ActiveEffectOwnerResources ownerResources,
                int gameplayEffectCode,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations)
            {
                if (ownerResources.Owner == Entity.Null)
                    return;

                for (var i = ownerResources.Slots.Length - 1; i >= 0; i--)
                {
                    var slot = ownerResources.Slots[i];
                    if (gameplayEffectCode > 0 && slot.GameplayEffectCode != gameplayEffectCode)
                        continue;

                    RemoveSlotAt(ref ownerResources, i, mutations);
                }
            }

            private void HandleDurationExpired(
                ref ActiveEffectOwnerResources ownerResources,
                int slotIndex,
                ref GASDefinitionCatalogBlob catalog,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters)
            {
                if ((uint)slotIndex >= (uint)ownerResources.Slots.Length)
                    return;

                var slot = ownerResources.Slots[slotIndex];
                if (!GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                {
                    RemoveSlotAt(ref ownerResources, slotIndex, mutations);
                    return;
                }

                ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                var expirationPolicy = (EffectExpirationPolicy)gameplayEffect.EffectExpirationPolicy;
                if (expirationPolicy == EffectExpirationPolicy.RefreshDuration)
                {
                    RefreshSlotDuration(ref slot, Frame, resetPeriod: true);
                    ownerResources.Slots[slotIndex] = slot;
                    mutations.Add(CreateRefreshMutation(in slot, Frame));
                    return;
                }

                if (expirationPolicy == EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration
                    && slot.StackCount > 1)
                {
                    slot.StackCount--;
                    RefreshSlotDuration(ref slot, Frame, resetPeriod: true);
                    ownerResources.Slots[slotIndex] = slot;
                    RebuildActiveModifiersForSlot(
                        ref ownerResources,
                        ref catalog,
                        in gameplayEffect,
                        in slot,
                        ref magnitudeSourceCounters,
                        ref snapshotLaneCounters);
                    mutations.Add(CreateStackMutation(in slot, Frame));
                    EnqueueStackCountChangedEvent(in slot);
                    return;
                }

                RemoveSlotAt(ref ownerResources, slotIndex, mutations);
            }

            private void RemoveSlotAt(
                ref ActiveEffectOwnerResources ownerResources,
                int slotIndex,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations)
            {
                if ((uint)slotIndex >= (uint)ownerResources.Slots.Length)
                    return;

                var slot = ownerResources.Slots[slotIndex];
                var activeModifierCount = RemoveActiveModifiersForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedTagsForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedAbilitiesForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                if (ownerResources.HasSetByCallerSnapshot)
                {
                    GASActiveEffectRuntime.RemoveSetByCallerSnapshotForSlot(
                        ownerResources.SetByCallerSnapshot,
                        slot.Sequence,
                        slot.GameplayEffectCode);
                }
                RecordCleanup(ref ownerResources, in slot, activeModifierCount);
                mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    Frame = Frame,
                    Kind = ActiveEffectMutationKind.Remove,
                    ActiveEffect = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    ContextId = slot.ContextId,
                    ParentContextId = slot.ParentContextId,
                    StackCount = slot.StackCount,
                    DurationFrameOverride = slot.DurationFrame,
                    PeriodFrame = slot.PeriodFrame,
                });
                EnqueueRemovedEvent(in slot);
                ownerResources.Slots.RemoveAt(slotIndex);
            }

            private int RebuildActiveModifiersForSlot(
                ref ActiveEffectOwnerResources ownerResources,
                ref GASDefinitionCatalogBlob catalog,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                in ActiveGameplayEffectBuffer slot,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters)
            {
                if (gameplayEffect.ModifierCount <= 0
                    || !ownerResources.HasAttributes
                    || !ownerResources.HasActiveModifiers)
                {
                    return 0;
                }

                RemoveActiveModifiersForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                var attributes = ownerResources.Attributes;
                var activeModifiers = ownerResources.ActiveModifiers;
                var setByCallerSnapshot = ownerResources.HasSetByCallerSnapshot
                    ? ownerResources.SetByCallerSnapshot
                    : default;
                var added = 0;
                for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                {
                    var modifierIndex = gameplayEffect.ModifierStart + i;
                    if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                        continue;

                    var modifier = catalog.Modifiers[modifierIndex];
                    var attrIndex = attributes.IndexOfAttribute(modifier.AttributeSetCode, modifier.AttributeCode);
                    if (attrIndex < 0)
                        continue;

                    var context = BuildMagnitudeContextFromSlot(
                        ref ownerResources,
                        in slot,
                        setByCallerSnapshot,
                        modifierIndex,
                        in modifier,
                        ref magnitudeSourceCounters,
                        ref snapshotLaneCounters);
                    if (!GASRuntimeMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
                        continue;

                    activeModifiers.Add(new AttributeActiveModifierBuffer
                    {
                        AttrSetCode = modifier.AttributeSetCode,
                        AttributeCode = modifier.AttributeCode,
                        SourceEntity = Entity.Null,
                        SourceSequence = slot.Sequence,
                        SourceGameplayEffectCode = slot.GameplayEffectCode,
                        Magnitude = magnitude,
                        Op = modifier.Operation,
                    });
                    MarkActiveModifierAdded(ownerResources.Owner);
                    MarkCurrentValueDirty(attributes, ownerResources.Owner, modifier.AttributeSetCode, modifier.AttributeCode);
                    added++;
                }

                return added;
            }

            private MagnitudeEvalContext BuildMagnitudeContextFromSlot(
                ref ActiveEffectOwnerResources ownerResources,
                in ActiveGameplayEffectBuffer slot,
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerSnapshot,
                int modifierIndex,
                in GASCatalogModifierDefinitionBlob modifier,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters)
            {
                var context = new MagnitudeEvalContext
                {
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    Level = slot.Level,
                    StackCount = slot.StackCount <= 0 ? 1 : slot.StackCount,
                    SetByCallerKey = modifier.MagnitudeKey,
                };

                if (modifier.MagnitudeSource == EMagnitudeSource.SetByCaller
                    && TryFindSetByCallerSnapshotValue(setByCallerSnapshot, slot.Sequence, slot.GameplayEffectCode, modifier.MagnitudeKey, out var setByCallerValue))
                {
                    context.HasSetByCallerValue = 1;
                    context.SetByCallerValue = setByCallerValue;
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute)
                {
                    magnitudeSourceCounters.SourceAttributeLookupCount++;
                    if (TryReadSourceAttributeValueFromSlot(
                            ref ownerResources,
                            in slot,
                            modifierIndex,
                            in modifier,
                            ref magnitudeSourceCounters,
                            ref snapshotLaneCounters,
                            out var sourceValue))
                    {
                        context.HasSourceAttributeValue = 1;
                        context.SourceAttributeValue = sourceValue;
                    }
                    else
                    {
                        magnitudeSourceCounters.FallbackValueCount++;
                        snapshotLaneCounters.FallbackValueCount++;
                    }
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute)
                {
                    magnitudeSourceCounters.TargetAttributeLookupCount++;
                    magnitudeSourceCounters.CurrentValueLookupCount++;
                    if (TryReadOwnerAttributeValue(ref ownerResources, in modifier, out var targetValue))
                    {
                        context.HasTargetAttributeValue = 1;
                        context.TargetAttributeValue = targetValue;
                    }
                    else
                    {
                        magnitudeSourceCounters.FallbackValueCount++;
                    }
                }

                return context;
            }

            private bool TryReadSourceAttributeValueFromSlot(
                ref ActiveEffectOwnerResources ownerResources,
                in ActiveGameplayEffectBuffer slot,
                int modifierIndex,
                in GASCatalogModifierDefinitionBlob modifier,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters,
                out float value)
            {
                if (CompareEntity(ownerResources.Owner, slot.SourceAsc) == 0)
                {
                    magnitudeSourceCounters.CurrentValueLookupCount++;
                    return TryReadOwnerAttributeValue(ref ownerResources, in modifier, out value);
                }

                value = 0f;
                if (slot.SourceAsc == Entity.Null || !ActiveEffectSlotSourceAttributeSnapshots.IsCreated)
                {
                    magnitudeSourceCounters.CaptureMissCount++;
                    snapshotLaneCounters.ApplyMissCount++;
                    return false;
                }

                var snapshotKey = MakeActiveEffectSlotSourceAttributeSnapshotKey(ownerResources.Owner, slot.Sequence, modifierIndex);
                if (ActiveEffectSlotSourceAttributeSnapshots.TryGetValue(snapshotKey, out value))
                {
                    magnitudeSourceCounters.CapturedValueHitCount++;
                    snapshotLaneCounters.ApplyHitCount++;
                    return true;
                }

                magnitudeSourceCounters.CaptureMissCount++;
                snapshotLaneCounters.ApplyMissCount++;
                return false;
            }

            private bool TryReadOwnerAttributeValue(
                ref ActiveEffectOwnerResources ownerResources,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                if (!ownerResources.HasAttributes)
                    return false;

                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attributeCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attributes = ownerResources.Attributes;
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attributeCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private int RemoveActiveModifiersForSlot(
                ref ActiveEffectOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasActiveModifiers)
                    return 0;

                var modifiers = ownerResources.ActiveModifiers;
                var hasAttributes = ownerResources.HasAttributes;
                var attributes = hasAttributes ? ownerResources.Attributes : default;
                var removed = 0;
                for (var i = modifiers.Length - 1; i >= 0; i--)
                {
                    var modifier = modifiers[i];
                    if (modifier.SourceSequence != slotSequence
                        || modifier.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    modifiers.RemoveAt(i);
                    if (hasAttributes)
                        MarkCurrentValueDirty(attributes, ownerResources.Owner, modifier.AttrSetCode, modifier.AttributeCode);
                    removed++;
                }

                RefreshActiveModifierPresence(ownerResources.Owner, modifiers);
                return removed;
            }

            private void MarkActiveModifierAdded(Entity asc)
            {
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent);
            }

            private void RefreshActiveModifierPresence(
                Entity asc,
                DynamicBuffer<AttributeActiveModifierBuffer> modifiers)
            {
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent);
            }

            private void MarkCurrentValueDirty(
                DynamicBuffer<AttributeValueBuffer> attributes,
                Entity asc,
                int attrSetCode,
                int attrCode)
            {
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
                if (attrIndex == -1)
                    return;

                var attr = attributes[attrIndex];
                attr.Dirty = true;
                attributes[attrIndex] = attr;
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.MarkDirty);
            }

            private void EnqueueAttributeOwnerMarkerRequest(Entity asc, EAttributeOwnerMarkerRequestKind requestKind)
            {
                if (asc == Entity.Null
                    || EventBusEntity == Entity.Null
                    || !AttributeOwnerMarkerRequestLookup.HasBuffer(EventBusEntity))
                {
                    return;
                }

                var requests = AttributeOwnerMarkerRequestLookup[EventBusEntity];
                requests.Add(new AttributeOwnerMarkerRequestBuffer
                {
                    Sequence = requests.Length,
                    ASC = asc,
                    RequestKind = requestKind,
                    Value = 1,
                });
            }

            private void RemoveGrantedTagsForSlot(
                ref ActiveEffectOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasTagSources)
                    return;

                var sources = ownerResources.TagSources;
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    var source = sources[i];
                    if (source.SourceSequence != slotSequence
                        || source.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    sources.RemoveAt(i);
                    var removedFromMask = RemoveTagIndexFromEffectiveMaskIfUnreferenced(ref ownerResources, source.TagIndex);
                    if (removedFromMask)
                    {
                        EnqueueTagChangedEvent(ownerResources.Owner, source.TagIndex, false);
                    }
                }
            }

            private bool RemoveTagIndexFromEffectiveMaskIfUnreferenced(ref ActiveEffectOwnerResources ownerResources, int tagIndex)
            {
                if (!ownerResources.HasTargetTags)
                    return false;
                if (ownerResources.HasFixedTags
                    && ownerResources.FixedTags.Mask.HasTag(tagIndex))
                {
                    return false;
                }
                if (HasAnyTemporarySourceForTag(ownerResources.TagSources, tagIndex))
                    return false;

                var mask = ownerResources.TargetTags;
                if (!mask.HasTag(tagIndex))
                    return false;
                mask.RemoveTag(tagIndex);
                ownerResources.TargetTags = mask;
                ownerResources.TargetTagsDirty = true;
                return true;
            }

            private static bool HasAnyTemporarySourceForTag(
                DynamicBuffer<TagTemporarySourceBuffer> sources,
                int tagIndex)
            {
                for (var i = 0; i < sources.Length; i++)
                {
                    if (sources[i].TagIndex == tagIndex)
                        return true;
                }

                return false;
            }

            private void RemoveGrantedAbilitiesForSlot(
                ref ActiveEffectOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasAbilitySlots)
                    return;

                var grantedAbilities = ownerResources.AbilitySlots;
                for (var i = grantedAbilities.Length - 1; i >= 0; i--)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (!IsGrantedBySlot(ability, slotSequence, gameplayEffectCode))
                        continue;

                    grantedAbilities.RemoveAt(i);
                    RemoveGrantedAbilityEntity(ability);
                }
            }

            private bool IsGrantedBySlot(
                Entity ability,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (ability == Entity.Null
                    || !AbilityGrantedLookup.HasComponent(ability))
                {
                    return false;
                }

                var granted = AbilityGrantedLookup[ability];
                return granted.SourceSequence == slotSequence
                    && granted.SourceGameplayEffectCode == gameplayEffectCode;
            }

            private void RemoveGrantedAbilityEntity(Entity ability)
            {
                if (ability == Entity.Null || !AbilityStateLookup.HasComponent(ability))
                    return;

                var runtime = AbilityStateLookup[ability];
                var isRunning = runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
                if (isRunning)
                {
                    EnqueueAbilityLifecycleRequest(ability, runtime);
                    return;
                }

                StructuralEcb.DestroyEntity(ability);
            }

            private void EnqueueAbilityLifecycleRequest(Entity ability, in AbilityStateComponent runtime)
            {
                if (EventBusEntity == Entity.Null
                    || !AbilityLifecycleRequestLookup.HasBuffer(EventBusEntity))
                    return;

                var requests = AbilityLifecycleRequestLookup[EventBusEntity];
                requests.Add(new AbilityLifecycleRequestBuffer
                {
                    Sequence = requests.Length,
                    RequestKind = EAbilityLifecycleRequestKind.Cancel,
                    Reason = EAbilityLifecycleReason.GrantedEffectRemoved,
                    Ability = ability,
                    SourceAbility = Entity.Null,
                    SourceEffect = Entity.Null,
                    SourceAbilityCode = 0,
                    DestroyOnCleanup = 1,
                });
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.AbilityCancelRequested,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = runtime.Owner,
                    TargetAsc = runtime.Owner,
                    SourceAbility = ability,
                    EventCode = runtime.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.GrantedEffectRemoved,
                });
            }

            private void RecordCleanup(
                ref ActiveEffectOwnerResources ownerResources,
                in ActiveGameplayEffectBuffer slot,
                int activeModifierCount)
            {
                if (!ownerResources.HasCleanupRecords)
                    return;

                var records = ownerResources.CleanupRecords;
                while (records.Length >= ActiveEffectStore.MaxCleanupRecordCount)
                    records.RemoveAt(0);

                var cleanupFlags = ActiveEffectCleanupWorkFlags.OwnerLocalSlot;
                if (activeModifierCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.RuntimeModifiers;
                if (slot.ActiveGrantedTagCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.GrantedTags;
                if (slot.ActiveGrantedAbilityCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.GrantedAbilities;

                records.Add(new ActiveGameplayEffectCleanupRecordBuffer
                {
                    Sequence = slot.Sequence,
                    ActiveEffectEntity = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    Instigator = slot.Instigator,
                    Causer = slot.Causer,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    Level = slot.Level,
                    StackCount = slot.StackCount,
                    ContextId = slot.ContextId,
                    ParentContextId = slot.ParentContextId,
                    CleanupFrame = Frame,
                    CleanupState = EGameplayEffectLifecycleState.PendingRemove,
                    SlotState = ActiveEffectSlotState.PendingRemove,
                    PreviousSlotState = slot.State,
                    DurationFrame = slot.DurationFrame,
                    RemainingFrame = slot.RemainingFrame,
                    PeriodFrame = slot.PeriodFrame,
                    LastPeriodFrame = slot.LastPeriodFrame,
                    ActiveGrantedTagCount = slot.ActiveGrantedTagCount,
                    ActiveGrantedAbilityCount = slot.ActiveGrantedAbilityCount,
                    ActiveModifierCount = activeModifierCount,
                    RequestedCleanupWorkFlags = (int)cleanupFlags,
                    ResolvedCleanupWorkFlags = (int)cleanupFlags,
                    CleanupResolvedFrame = Frame,
                    Flags = slot.Flags,
                });

                ownerResources.Store.LastCleanupFrame = Frame;
                ownerResources.Store.CleanupRecordCount = records.Length;
            }

            private void EmitPeriodCommand(
                ref GASDefinitionCatalogBlob catalog,
                in ActiveGameplayEffectBuffer slot,
                ref ActiveEffectOwnerResources ownerResources)
            {
                if (!GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                    return;

                ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                if (gameplayEffect.PeriodGameplayEffectCode <= 0
                    || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.PeriodGameplayEffectCode, out var periodGameplayEffectIndex)
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                ref readonly var periodGameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, periodGameplayEffectIndex);
                var durationFrame = periodGameplayEffect.DurationFrames;
                var kind = RequiresActiveMutationLane(in periodGameplayEffect, durationFrame)
                    ? GEEffectCommandKind.ActiveMutation
                    : GEEffectCommandKind.Instant;
                var command = new GEEffectCommandBuffer
                {
                    Kind = kind,
                    Source = GEEffectCommandSource.Period,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = Entity.Null,
                    Instigator = slot.Instigator != Entity.Null ? slot.Instigator : slot.SourceAsc,
                    Causer = slot.Causer != Entity.Null ? slot.Causer : slot.SourceAbility,
                    GameplayEffectCode = gameplayEffect.PeriodGameplayEffectCode,
                    Level = slot.Level,
                    DurationFrameOverride = durationFrame,
                    ParentContextId = slot.ContextId,
                    TargetDataKind = CompareEntity(slot.SourceAsc, ownerResources.Owner) == 0 ? ETargetDataKind.Self : ETargetDataKind.Entity,
                    Flags = kind == GEEffectCommandKind.ActiveMutation ? GASGECommandSeedFlags.ActiveMutation : GASGECommandSeedFlags.None,
                };

                var sourceSetByCallerValues = ownerResources.HasSetByCallerSnapshot
                    ? ownerResources.SetByCallerSnapshot
                    : default;
                var setByCallerCount = CountSetByCallerValues(sourceSetByCallerValues, slot.Sequence, slot.GameplayEffectCode);
                var stream = StreamLookup[StreamEntity];
                if (kind == GEEffectCommandKind.ActiveMutation)
                {
                    if (!ActiveMutationCommandLookup.HasBuffer(ownerResources.Owner)
                        || !ActiveMutationSetByCallerLookup.HasBuffer(ownerResources.Owner))
                    {
                        return;
                    }

                    var ownerSetByCallerValues = ActiveMutationSetByCallerLookup[ownerResources.Owner];
                    var ownerCommand = PrepareCommand(
                        ref stream,
                        ownerSetByCallerValues.Length,
                        in command,
                        setByCallerCount,
                        Frame);
                    CopySetByCallerValues(
                        ownerSetByCallerValues,
                        sourceSetByCallerValues,
                        slot.Sequence,
                        slot.GameplayEffectCode,
                        ownerCommand.Sequence);
                    ActiveMutationCommandLookup[ownerResources.Owner].Add(new ActiveEffectMutationCommandBuffer
                    {
                        Command = ownerCommand,
                    });
                    StreamLookup[StreamEntity] = stream;
                    return;
                }

                if (!CommandLookup.HasBuffer(ownerResources.Owner)
                    || !CommandSetByCallerLookup.HasBuffer(ownerResources.Owner))
                {
                    return;
                }

                var ownerInstantSetByCallerValues = CommandSetByCallerLookup[ownerResources.Owner];
                var resolved = PrepareCommand(ref stream, ownerInstantSetByCallerValues, in command, setByCallerCount, Frame);
                CopySetByCallerValues(
                    ownerInstantSetByCallerValues,
                    sourceSetByCallerValues,
                    slot.Sequence,
                    slot.GameplayEffectCode,
                    resolved.Sequence);
                CommandLookup[ownerResources.Owner].Add(resolved);
                StreamLookup[StreamEntity] = stream;
            }

            private GEEffectCommandBuffer PrepareCommand(
                ref GEEffectCommandStreamComponent stream,
                DynamicBuffer<GESetByCallerValueBuffer> setByCallerBuffer,
                in GEEffectCommandBuffer command,
                int setByCallerCount,
                int currentFrame)
            {
                var resolved = command;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = GASRuntimeSequenceAllocator.AllocateCommandSequence(ref stream);
                if (resolved.Frame <= 0)
                    resolved.Frame = currentFrame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = GASRuntimeSequenceAllocator.AllocateContextId(ref stream);
                if (resolved.TargetAsc == Entity.Null)
                    resolved.TargetAsc = resolved.SourceAsc;
                if (resolved.Instigator == Entity.Null)
                    resolved.Instigator = resolved.SourceAsc;
                if (resolved.Causer == Entity.Null)
                    resolved.Causer = resolved.SourceAbility;

                resolved.SetByCallerStart = setByCallerBuffer.Length;
                resolved.SetByCallerCount = setByCallerCount;
                return resolved;
            }

            private GEEffectCommandBuffer PrepareCommand(
                ref GEEffectCommandStreamComponent stream,
                int setByCallerStart,
                in GEEffectCommandBuffer command,
                int setByCallerCount,
                int currentFrame)
            {
                var resolved = command;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = GASRuntimeSequenceAllocator.AllocateCommandSequence(ref stream);
                if (resolved.Frame <= 0)
                    resolved.Frame = currentFrame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = GASRuntimeSequenceAllocator.AllocateContextId(ref stream);
                if (resolved.TargetAsc == Entity.Null)
                    resolved.TargetAsc = resolved.SourceAsc;
                if (resolved.Instigator == Entity.Null)
                    resolved.Instigator = resolved.SourceAsc;
                if (resolved.Causer == Entity.Null)
                    resolved.Causer = resolved.SourceAbility;

                resolved.SetByCallerStart = setByCallerStart;
                resolved.SetByCallerCount = setByCallerCount;
                return resolved;
            }

            private int CountSetByCallerValues(
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerValues,
                int sourceSequence,
                int sourceGameplayEffectCode)
            {
                if (!setByCallerValues.IsCreated)
                    return 0;

                var count = 0;
                for (var i = 0; i < setByCallerValues.Length; i++)
                {
                    var value = setByCallerValues[i];
                    if (value.SourceSequence == sourceSequence
                        && value.SourceGameplayEffectCode == sourceGameplayEffectCode)
                    {
                        count++;
                    }
                }

                return count;
            }

            private void CopySetByCallerValues(
                DynamicBuffer<GESetByCallerValueBuffer> target,
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> source,
                int sourceSequence,
                int sourceGameplayEffectCode,
                int commandSequence)
            {
                if (!source.IsCreated)
                    return;

                for (var i = 0; i < source.Length; i++)
                {
                    var value = source[i];
                    if (value.SourceSequence != sourceSequence
                        || value.SourceGameplayEffectCode != sourceGameplayEffectCode)
                    {
                        continue;
                    }

                    target.Add(new GESetByCallerValueBuffer
                    {
                        CommandSequence = commandSequence,
                        SpecSequence = 0,
                        Key = value.Key,
                        Value = value.Value,
                    });
                }
            }

            private void CopySetByCallerValues(
                DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> target,
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> source,
                int sourceSequence,
                int sourceGameplayEffectCode,
                int commandSequence)
            {
                if (!source.IsCreated)
                    return;

                for (var i = 0; i < source.Length; i++)
                {
                    var value = source[i];
                    if (value.SourceSequence != sourceSequence
                        || value.SourceGameplayEffectCode != sourceGameplayEffectCode)
                    {
                        continue;
                    }

                    target.Add(new ActiveEffectMutationSetByCallerValueBuffer
                    {
                        Value = new GESetByCallerValueBuffer
                        {
                            CommandSequence = commandSequence,
                            SpecSequence = 0,
                            Key = value.Key,
                            Value = value.Value,
                        },
                    });
                }
            }

            private void EnqueueStackCountChangedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.StackCountChanged,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    ContextId = slot.ContextId,
                    EventCode = slot.GameplayEffectCode,
                    Value = slot.StackCount,
                });
            }

            private void EnqueueRemovedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.GameplayEffectRemoved,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    ContextId = slot.ContextId,
                    EventCode = slot.GameplayEffectCode,
                });
            }

            private void EnqueueGameplayEvent(GameplayEventBuffer evt)
            {
                var owner = evt.TargetAsc != Entity.Null ? evt.TargetAsc : evt.SourceAsc;
                if (owner == Entity.Null || !OwnerFactLookup.HasBuffer(owner))
                    return;

                evt.Frame = Frame;
                if (StreamLookup.HasComponent(StreamEntity))
                {
                    var stream = StreamLookup[StreamEntity];
                    evt.Sequence = GASRuntimeSequenceAllocator.AllocateFactSequence(ref stream);
                    StreamLookup[StreamEntity] = stream;
                }
                else
                {
                    evt.Sequence = 0;
                }

                OwnerFactLookup[owner].Add(new OwnerLocalGameplayFactBuffer
                {
                    Fact = evt,
                });
            }

            private void EnqueueTagChangedEvent(Entity owner, int tagIndex, bool added)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.TagChanged,
                    Domain = EGameplayFactDomain.Tag,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    TargetAsc = owner,
                    EventCode = tagIndex,
                    ReasonCode = added ? 1 : 0,
                });
            }
        }

        public static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0) return 0;
            if (cursor > length) return length;
            return cursor;
        }

        public static bool TryNormalizeCommand(ref GASDefinitionCatalogBlob catalog, ref GEEffectCommandBuffer command)
        {
            if (command.GameplayEffectCode <= 0
                || !GASDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
            {
                return false;
            }

            ref readonly var gameplayEffect = ref GASDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
            var isActiveMutation = RequiresActiveMutationLane(in gameplayEffect, durationFrame);
            command.Kind = isActiveMutation ? GEEffectCommandKind.ActiveMutation : GEEffectCommandKind.Instant;
            command.DurationFrameOverride = durationFrame;
            if (isActiveMutation)
                command.Flags |= GASGECommandSeedFlags.ActiveMutation;
            else
                command.Flags &= ~GASGECommandSeedFlags.ActiveMutation;
            return true;
        }

        private static ActiveGameplayEffectBuffer CreateNewSlot(
            ref ASCActiveEffectsComponent store,
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame,
            int frame)
        {
            return new ActiveGameplayEffectBuffer
            {
                Sequence = Allocate(ref store.NextSequence),
                State = ActiveEffectSlotState.Active,
                PreviousState = ActiveEffectSlotState.Active,
                ActiveEffectEntity = Entity.Null,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                SourceEffect = command.SourceEffect,
                Instigator = command.Instigator,
                Causer = command.Causer,
                GameplayEffectCode = command.GameplayEffectCode,
                Level = command.Level,
                StackCount = 1,
                ContextId = command.ContextId,
                ParentContextId = command.ParentContextId,
                StartFrame = frame,
                StateStartFrame = frame,
                DurationFrame = durationFrame,
                RemainingFrame = durationFrame,
                PeriodFrame = gameplayEffect.PeriodFrames,
                LastPeriodFrame = frame,
            };
        }

        private static ActiveGameplayEffectBuffer RefreshExistingSlot(
            in ActiveGameplayEffectBuffer previous,
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame,
            int frame,
            bool refreshDuration,
            bool resetPeriod)
        {
            var slot = previous;
            slot.PreviousState = slot.State;
            slot.State = ActiveEffectSlotState.Active;
            slot.SourceAsc = command.SourceAsc;
            slot.TargetAsc = command.TargetAsc;
            slot.SourceAbility = command.SourceAbility;
            slot.SourceEffect = command.SourceEffect;
            slot.Instigator = command.Instigator;
            slot.Causer = command.Causer;
            slot.Level = command.Level;
            slot.ContextId = command.ContextId;
            slot.ParentContextId = command.ParentContextId;
            slot.StateStartFrame = frame;
            if (refreshDuration)
            {
                slot.StartFrame = frame;
                slot.DurationFrame = durationFrame;
                slot.RemainingFrame = durationFrame;
            }
            slot.PeriodFrame = gameplayEffect.PeriodFrames;
            if (resetPeriod || slot.LastPeriodFrame <= 0)
                slot.LastPeriodFrame = frame;
            return slot;
        }

        private static bool IsStackOverflow(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int previousStackCount)
        {
            return gameplayEffect.StackLimitCount > 0
                && previousStackCount >= gameplayEffect.StackLimitCount;
        }

        private static bool ShouldRefreshDuration(in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            return gameplayEffect.EffectDurationRefreshPolicy == (int)EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication;
        }

        private static bool ShouldResetPeriod(in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            return gameplayEffect.EffectPeriodResetPolicy == (int)EffectPeriodResetPolicy.ResetOnSuccessfulApplication;
        }

        private static int ResolveNextStackCount(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int previousStackCount,
            bool isNewSlot)
        {
            if (isNewSlot)
                return 1;
            if (gameplayEffect.StackLimitCount <= 0)
                return previousStackCount <= 0 ? 1 : previousStackCount;
            var next = previousStackCount <= 0 ? 1 : previousStackCount + 1;
            return next > gameplayEffect.StackLimitCount ? gameplayEffect.StackLimitCount : next;
        }

        private static int ResolveDurationFrame(
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            return command.DurationFrameOverride > 0
                ? command.DurationFrameOverride
                : gameplayEffect.DurationFrames;
        }

        private static bool HasPersistentRuntimeState(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame)
        {
            return durationFrame > 0
                || gameplayEffect.PeriodFrames > 0
                || gameplayEffect.StackLimitCount > 0
                || gameplayEffect.GrantedTagMaskIndex >= 0
                || gameplayEffect.GrantedAbilityCount > 0;
        }

        private static bool RequiresActiveMutationLane(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame)
        {
            return HasPersistentRuntimeState(in gameplayEffect, durationFrame)
                || !gameplayEffect.RemoveGameplayEffectTagQuery.IsEmpty;
        }

        private static int ResolveSlotFlags(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame,
            int activeModifierCount,
            int activeGrantedTagCount,
            int activeGrantedAbilityCount)
        {
            var flags = ActiveEffectSlotFlags.None;
            if (durationFrame > 0)
                flags |= ActiveEffectSlotFlags.HasDuration;
            if (gameplayEffect.PeriodFrames > 0)
                flags |= ActiveEffectSlotFlags.HasPeriod;
            if (gameplayEffect.StackLimitCount > 0)
                flags |= ActiveEffectSlotFlags.HasStacking;
            if (activeGrantedTagCount > 0)
                flags |= ActiveEffectSlotFlags.HasGrantedTags;
            if (activeGrantedAbilityCount > 0)
                flags |= ActiveEffectSlotFlags.HasGrantedAbilities;
            return (int)flags;
        }




        private static bool TryFindSetByCallerValue(
            in GEEffectCommandBuffer command,
            NativeList<GESetByCallerValueBuffer> setByCallerValues,
            int key,
            out float value)
        {
            var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
            var end = start + command.SetByCallerCount;
            if (end > setByCallerValues.Length)
                end = setByCallerValues.Length;
            for (var i = start; i < end; i++)
            {
                var setByCaller = setByCallerValues[i];
                if (setByCaller.Key != key || setByCaller.CommandSequence != command.Sequence)
                    continue;
                value = setByCaller.Value;
                return true;
            }

            value = 0f;
            return false;
        }











        private static void CopySetByCallerSnapshot(
            in GEEffectCommandBuffer command,
            NativeList<GESetByCallerValueBuffer> setByCallerValues,
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> snapshot,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (command.SetByCallerCount <= 0
                || slotSequence <= 0
                || gameplayEffectCode <= 0)
            {
                return;
            }

            var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
            var end = start + command.SetByCallerCount;
            if (end > setByCallerValues.Length)
                end = setByCallerValues.Length;
            for (var i = start; i < end; i++)
            {
                var value = setByCallerValues[i];
                if (value.CommandSequence != command.Sequence)
                    continue;

                snapshot.Add(new ActiveGameplayEffectSetByCallerValueBuffer
                {
                    SourceSequence = slotSequence,
                    SourceGameplayEffectCode = gameplayEffectCode,
                    Key = value.Key,
                    Value = value.Value,
                });
            }
        }


        private static void RemoveSetByCallerSnapshotForSlot(
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> snapshot,
            int slotSequence,
            int gameplayEffectCode)
        {
            for (var i = snapshot.Length - 1; i >= 0; i--)
            {
                var value = snapshot[i];
                if (value.SourceSequence == slotSequence
                    && value.SourceGameplayEffectCode == gameplayEffectCode)
                {
                    snapshot.RemoveAt(i);
                }
            }
        }



        private static void RefreshSlotDuration(ref ActiveGameplayEffectBuffer slot, int frame, bool resetPeriod)
        {
            slot.StartFrame = frame;
            slot.RemainingFrame = slot.DurationFrame;
            slot.StateStartFrame = frame;
            if (resetPeriod && slot.PeriodFrame > 0)
                slot.LastPeriodFrame = frame;
        }

        private static ActiveEffectMutationBuffer CreateRefreshMutation(in ActiveGameplayEffectBuffer slot, int frame)
        {
            return new ActiveEffectMutationBuffer
            {
                Sequence = slot.Sequence,
                Frame = frame,
                Kind = ActiveEffectMutationKind.Refresh,
                ActiveEffect = Entity.Null,
                SourceAsc = slot.SourceAsc,
                TargetAsc = slot.TargetAsc,
                SourceAbility = slot.SourceAbility,
                SourceEffect = slot.SourceEffect,
                GameplayEffectCode = slot.GameplayEffectCode,
                ContextId = slot.ContextId,
                ParentContextId = slot.ParentContextId,
                StackCount = slot.StackCount,
                DurationFrameOverride = slot.DurationFrame,
                PeriodFrame = slot.PeriodFrame,
            };
        }

        private static ActiveEffectMutationBuffer CreateStackMutation(in ActiveGameplayEffectBuffer slot, int frame)
        {
            var mutation = CreateRefreshMutation(in slot, frame);
            mutation.Kind = ActiveEffectMutationKind.Stack;
            return mutation;
        }

        private static bool TryFindSetByCallerSnapshotValue(
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> snapshot,
            int slotSequence,
            int gameplayEffectCode,
            int key,
            out float value)
        {
            if (snapshot.IsCreated)
            {
                for (var i = 0; i < snapshot.Length; i++)
                {
                    var item = snapshot[i];
                    if (item.SourceSequence == slotSequence
                        && item.SourceGameplayEffectCode == gameplayEffectCode
                        && item.Key == key)
                    {
                        value = item.Value;
                        return true;
                    }
                }
            }

            value = 0f;
            return false;
        }




        private static int FindRefreshableSlot(DynamicBuffer<ActiveGameplayEffectBuffer> slots, in GEEffectCommandBuffer command)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.State == ActiveEffectSlotState.PendingRemove)
                    continue;
                if (slot.GameplayEffectCode == command.GameplayEffectCode
                    && slot.TargetAsc == command.TargetAsc
                    && slot.SourceAsc == command.SourceAsc
                    && slot.SourceAbility == command.SourceAbility)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasActiveModifier(
            DynamicBuffer<AttributeActiveModifierBuffer> modifiers,
            int sourceSequence,
            int gameplayEffectCode,
            int attrSetCode,
            int attributeCode,
            EModifierOp op)
        {
            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                if (modifier.SourceSequence == sourceSequence
                    && modifier.SourceGameplayEffectCode == gameplayEffectCode
                    && modifier.AttrSetCode == attrSetCode
                    && modifier.AttributeCode == attributeCode
                    && modifier.Op == op)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTempTagSource(
            DynamicBuffer<TagTemporarySourceBuffer> sources,
            int tagIndex,
            int sourceSequence,
            int gameplayEffectCode)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.TagIndex == tagIndex
                    && source.SourceSequence == sourceSequence
                    && source.SourceGameplayEffectCode == gameplayEffectCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountTempTagSourcesForSlot(
            DynamicBuffer<TagTemporarySourceBuffer> sources,
            int sourceSequence,
            int gameplayEffectCode)
        {
            var count = 0;
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.SourceSequence == sourceSequence
                    && source.SourceGameplayEffectCode == gameplayEffectCode)
                {
                    count++;
                }
            }

            return count;
        }



        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;
            return next++;
        }




    }
}
