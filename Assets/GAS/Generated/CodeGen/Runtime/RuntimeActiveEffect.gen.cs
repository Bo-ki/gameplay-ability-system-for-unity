///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectRemoveSystem))]
    [UpdateBefore(typeof(GEEffectSpecBuildSystem))]
    public partial struct GEEffectCommandCatalogNormalizeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;
            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            ref var catalog = ref catalogComponent.Catalog.Value;

            for (var i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (GASGeneratedActiveEffectRuntime.TryNormalizeCommand(ref catalog, ref command))
                    commands[i] = command;
            }
        }
    }

    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEEffectSpecBuildSystem))]
    [UpdateBefore(typeof(GASAttributeSetReduceApplySystem))]
    [UpdateBefore(typeof(GEExecutionCalculationSystem))]
    public partial struct GASActiveEffectMutationApplySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;
            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            var mutations = em.GetBuffer<ActiveEffectMutationBuffer>(streamEntity);
            var setByCallerValues = em.GetBuffer<GESetByCallerValueBuffer>(streamEntity);
            ref var catalog = ref catalogComponent.Catalog.Value;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            try
            {
                var start = GASGeneratedActiveEffectRuntime.ClampCursor(stream.ActiveMutationCommandCursor, commands.Length);
                for (var i = start; i < commands.Length; i++)
                {
                    var command = commands[i];
                    if (command.Kind != GEEffectCommandKind.ActiveMutation)
                        continue;

                    GASGeneratedActiveEffectRuntime.TryApplyActiveMutation(
                        em,
                        ref stream,
                        ref catalog,
                        in command,
                        commands,
                        setByCallerValues,
                        mutations,
                        frame,
                        ref structuralEcb,
                        ref eventWriter);
                }

                stream.ActiveMutationCommandCursor = commands.Length;
                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                eventWriter.Dispose();
            }
        }
    }

    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup), OrderFirst = true)]
    public partial struct GASActiveEffectPreTickSystem : ISystem
    {
        private EntityQuery _ownerQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerQuery = SystemAPI.QueryBuilder()
                .WithAll<ASCActiveEffectsComponent, ActiveGameplayEffectBuffer>()
                .Build();
            state.RequireForUpdate(_ownerQuery);
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;
            var mutations = em.GetBuffer<ActiveEffectMutationBuffer>(streamEntity);
            var commandWriter = EffectCommandSpecStream.BeginCommandWriter(em, streamEntity, frame);
            ref var catalog = ref catalogComponent.Catalog.Value;
            var ownerChunkCount = _ownerQuery.CalculateChunkCount();
            if (ownerChunkCount <= 0)
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var tickRecordStream = new NativeStream(ownerChunkCount, Allocator.TempJob);

            try
            {
                var scanJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectTickScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: true),
                    Frame = frame,
                    TickRecordWriter = tickRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_ownerQuery, state.Dependency);
                state.Dependency.Complete();

                var tickRecordReader = tickRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < tickRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = tickRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var tickRecord = tickRecordReader.Read<GASGeneratedActiveEffectRuntime.GEActiveEffectTickRecord>();
                        GASGeneratedActiveEffectRuntime.ApplyActiveEffectTickRecord(
                            em,
                            ref catalog,
                            in tickRecord,
                            frame,
                            mutations,
                            ref commandWriter,
                            ref structuralEcb,
                            ref eventWriter);
                    }
                    tickRecordReader.EndForEachIndex();
                }

                commandWriter.Flush();
            }
            finally
            {
                eventWriter.Dispose();
                tickRecordStream.Dispose();
            }
        }
    }

    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectPreTickSystem))]
    public partial struct GASActiveEffectRemoveSystem : ISystem
    {
        private EntityQuery _removeRequestQuery;

        public void OnCreate(ref SystemState state)
        {
            _removeRequestQuery = SystemAPI.QueryBuilder()
                .WithAll<GERemoveRequestComponent>()
                .Build();
            state.RequireForUpdate(_removeRequestQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;
            var mutations = em.GetBuffer<ActiveEffectMutationBuffer>(streamEntity);
            var requestChunkCount = _removeRequestQuery.CalculateChunkCount();
            if (requestChunkCount <= 0)
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var requestRecordStream = new NativeStream(requestChunkCount, Allocator.TempJob);

            try
            {
                var scanJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectRemoveRequestScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    RemoveRequestTypeHandle = SystemAPI.GetComponentTypeHandle<GERemoveRequestComponent>(isReadOnly: true),
                    RequestRecordWriter = requestRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_removeRequestQuery, state.Dependency);
                state.Dependency.Complete();

                var requestRecordReader = requestRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < requestRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = requestRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var requestRecord = requestRecordReader.Read<GASGeneratedActiveEffectRuntime.GEActiveEffectRemoveRequestRecord>();
                        GASGeneratedActiveEffectRuntime.RemoveMatchingOwnerLocalEffects(
                            em,
                            requestRecord.TargetAsc,
                            requestRecord.GameplayEffectCode,
                            mutations,
                            frame,
                            ref structuralEcb,
                            ref eventWriter);
                        structuralEcb.DestroyEntity(requestRecord.RequestEntity);
                    }
                    requestRecordReader.EndForEachIndex();
                }
            }
            finally
            {
                eventWriter.Dispose();
                requestRecordStream.Dispose();
            }
        }
    }

    internal static class GASGeneratedActiveEffectRuntime
    {
        public struct GEActiveEffectTickRecord
        {
            public Entity Owner;
            public int SlotSequence;
            public int GameplayEffectCode;
            public int ActionFlags;
        }

        public struct GEActiveEffectRemoveRequestRecord
        {
            public Entity RequestEntity;
            public Entity TargetAsc;
            public int GameplayEffectCode;
        }

        public struct GEActiveEffectRemoveRequestScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<GERemoveRequestComponent> RemoveRequestTypeHandle;
            public NativeStream.Writer RequestRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                RequestRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var requestEntities = chunk.GetNativeArray(EntityTypeHandle);
                var requests = chunk.GetNativeArray(ref RemoveRequestTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var request = requests[entityIndex];
                    RequestRecordWriter.Write(new GEActiveEffectRemoveRequestRecord
                    {
                        RequestEntity = requestEntities[entityIndex],
                        TargetAsc = request.TargetAsc,
                        GameplayEffectCode = request.GameplayEffectCode,
                    });
                }
                RequestRecordWriter.EndForEachIndex();
            }
        }

        public struct GEActiveEffectTickScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            public int Frame;
            public NativeStream.Writer TickRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                TickRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var owners = chunk.GetNativeArray(EntityTypeHandle);
                var slotBuffers = chunk.GetBufferAccessorRO(ref ActiveEffectSlotBufferTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    AppendOwnerActiveEffectTickRecords(
                        owners[entityIndex],
                        slotBuffers[entityIndex],
                        Frame,
                        ref TickRecordWriter);
                }
                TickRecordWriter.EndForEachIndex();
            }
        }

        public static bool TryApplyActiveMutation(
            EntityManager em,
            ref GEEffectCommandStreamComponent stream,
            ref GASDefinitionCatalogBlob catalog,
            in GEEffectCommandBuffer command,
            DynamicBuffer<GEEffectCommandBuffer> commands,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (!IsAvailableAsc(em, command.TargetAsc)
                || !HasActiveEffectStorage(em, command.TargetAsc)
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
            {
                return false;
            }

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            var targetTags = em.GetComponentData<TagMaskComponent>(command.TargetAsc);
            if (!EvaluateGameplayEffectRequirements(ref catalog, in gameplayEffect, in targetTags))
                return false;

            var slots = em.GetBuffer<ActiveGameplayEffectBuffer>(command.TargetAsc);
            var setByCallerSnapshot = em.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(command.TargetAsc);
            RemoveGameplayEffectsWithTags(
                em,
                ref catalog,
                command.TargetAsc,
                in gameplayEffect,
                slots,
                mutations,
                frame,
                ref structuralEcb,
                ref eventWriter);

            var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
            if (!HasPersistentRuntimeState(in gameplayEffect, durationFrame))
            {
                mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = command.Sequence,
                    SourceCommandSequence = command.Sequence,
                    Frame = frame,
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
                EnqueueAppliedEvents(ref eventWriter, in command, in gameplayEffect, Entity.Null);
                return true;
            }

            var store = em.GetComponentData<ASCActiveEffectsComponent>(command.TargetAsc);
            var slotIndex = FindRefreshableSlot(slots, in command);
            if (slotIndex < 0 && slots.Length >= GASParameterSetting.ASC_MAX_GAMEPLAY_EFFECT_COUNT)
                return false;

            var isNewSlot = slotIndex < 0;
            var previous = isNewSlot ? default : slots[slotIndex];
            var previousStackCount = previous.StackCount <= 0 ? 1 : previous.StackCount;
            var isOverflow = !isNewSlot && IsStackOverflow(in gameplayEffect, previousStackCount);
            if (isOverflow)
            {
                EmitOverflowCommand(
                    ref stream,
                    ref catalog,
                    commands,
                    setByCallerValues,
                    in command,
                    in gameplayEffect,
                    frame);
                EnqueueStackOverflowEvent(ref eventWriter, in command, in gameplayEffect);
                if (gameplayEffect.ClearStackOnOverflow != 0)
                {
                    RemoveSlotAt(em, command.TargetAsc, slots, slotIndex, mutations, frame, ref structuralEcb, ref eventWriter);
                    if (em.HasComponent<ASCActiveEffectsComponent>(command.TargetAsc))
                        store = em.GetComponentData<ASCActiveEffectsComponent>(command.TargetAsc);
                    slotIndex = -1;
                    isNewSlot = true;
                    previous = default;
                    previousStackCount = 1;
                    EnqueueStackClearedByOverflowEvent(ref eventWriter, in command);
                }

                if (gameplayEffect.DenyOverflowApplication != 0)
                {
                    EnqueueStackOverflowDeniedEvent(ref eventWriter, in command);
                    return true;
                }
            }

            var slot = isNewSlot
                ? CreateNewSlot(ref store, in command, in gameplayEffect, durationFrame, frame)
                : RefreshExistingSlot(
                    previous,
                    in command,
                    in gameplayEffect,
                    durationFrame,
                    frame,
                    ShouldRefreshDuration(in gameplayEffect),
                    ShouldResetPeriod(in gameplayEffect));

            slot.StackCount = ResolveNextStackCount(in gameplayEffect, previousStackCount, isNewSlot);
            RemoveActiveModifiersForSlot(em, command.TargetAsc, slot.Sequence, slot.GameplayEffectCode);
            RemoveSetByCallerSnapshotForSlot(setByCallerSnapshot, slot.Sequence, slot.GameplayEffectCode);
            CopySetByCallerSnapshot(in command, setByCallerValues, setByCallerSnapshot, slot.Sequence, slot.GameplayEffectCode);

            var activeModifierCount = ApplyActiveModifiers(
                em,
                ref catalog,
                in gameplayEffect,
                in command,
                setByCallerValues,
                slot.Sequence,
                slot.StackCount);
            slot.ActiveGrantedTagCount = ApplyGrantedTags(
                em,
                ref catalog,
                command.TargetAsc,
                in gameplayEffect,
                slot.Sequence,
                ref eventWriter);
            slot.ActiveGrantedAbilityCount = ApplyGrantedAbilities(
                em,
                ref catalog,
                command.TargetAsc,
                in gameplayEffect,
                slot.Sequence,
                ref structuralEcb);
            slot.Flags = ResolveSlotFlags(
                in gameplayEffect,
                durationFrame,
                activeModifierCount,
                slot.ActiveGrantedTagCount,
                slot.ActiveGrantedAbilityCount);

            if (slotIndex >= 0)
                slots[slotIndex] = slot;
            else
                slots.Add(slot);

            ActiveEffectStore.RefreshChunkSkipIndexCounters(ref store, slots, frame);
            em.SetComponentData(command.TargetAsc, store);

            mutations.Add(new ActiveEffectMutationBuffer
            {
                Sequence = slot.Sequence,
                SourceCommandSequence = command.Sequence,
                Frame = frame,
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

            EnqueueAppliedEvents(ref eventWriter, in command, in gameplayEffect, Entity.Null);
            return true;
        }

        private static int AppendOwnerActiveEffectTickRecords(
            Entity owner,
            DynamicBuffer<ActiveGameplayEffectBuffer> slots,
            int frame,
            ref NativeStream.Writer tickRecordWriter)
        {
            if (owner == Entity.Null)
                return 0;

            var appended = 0;
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var slot = slots[i];
                var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, frame);
                if (actionFlags == (int)ActiveEffectTickActionFlags.None)
                    continue;

                tickRecordWriter.Write(new GEActiveEffectTickRecord
                {
                    Owner = owner,
                    SlotSequence = slot.Sequence,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    ActionFlags = actionFlags,
                });
                appended++;
            }

            return appended;
        }

        public static void ApplyActiveEffectTickRecord(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            in GEActiveEffectTickRecord tickRecord,
            int frame,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            ref EffectCommandSpecStream.CommandWriter commandWriter,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            var owner = tickRecord.Owner;
            if (!em.Exists(owner) || !em.HasBuffer<ActiveGameplayEffectBuffer>(owner))
                return;

            var slots = em.GetBuffer<ActiveGameplayEffectBuffer>(owner);
            var slotIndex = FindSlotBySequence(slots, tickRecord.SlotSequence, tickRecord.GameplayEffectCode);
            if (slotIndex < 0)
                return;

            var slot = slots[slotIndex];
            var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, frame) & tickRecord.ActionFlags;
            if (actionFlags == (int)ActiveEffectTickActionFlags.None)
                return;

            if ((actionFlags & (int)ActiveEffectTickActionFlags.Period) != 0)
            {
                EmitPeriodCommand(em, ref catalog, in slot, owner, ref commandWriter);
                slot.LastPeriodFrame = frame;
                slots[slotIndex] = slot;
                mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    Frame = frame,
                    Kind = ActiveEffectMutationKind.PeriodTick,
                    ActiveEffect = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = owner,
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
                HandleDurationExpired(em, owner, slots, slotIndex, ref catalog, mutations, frame, ref structuralEcb, ref eventWriter);

            var store = em.GetComponentData<ASCActiveEffectsComponent>(owner);
            ActiveEffectStore.RefreshChunkSkipIndexCounters(ref store, slots, frame);
            em.SetComponentData(owner, store);
        }

        private static int FindSlotBySequence(
            DynamicBuffer<ActiveGameplayEffectBuffer> slots,
            int slotSequence,
            int gameplayEffectCode)
        {
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (slot.Sequence == slotSequence && slot.GameplayEffectCode == gameplayEffectCode)
                    return i;
            }

            return -1;
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
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
            {
                return false;
            }

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
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

        public static void RemoveMatchingOwnerLocalEffects(
            EntityManager em,
            Entity owner,
            int gameplayEffectCode,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (owner == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<ActiveGameplayEffectBuffer>(owner))
            {
                return;
            }

            var slots = em.GetBuffer<ActiveGameplayEffectBuffer>(owner);
            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (gameplayEffectCode > 0 && slot.GameplayEffectCode != gameplayEffectCode)
                    continue;

                RemoveSlotAt(em, owner, slots, i, mutations, frame, ref structuralEcb, ref eventWriter);
            }

            if (em.HasComponent<ASCActiveEffectsComponent>(owner))
            {
                var store = em.GetComponentData<ASCActiveEffectsComponent>(owner);
                ActiveEffectStore.RefreshChunkSkipIndexCounters(ref store, slots, frame);
                em.SetComponentData(owner, store);
            }
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
                || gameplayEffect.RemoveGameplayEffectTagMaskIndex >= 0;
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

        private static int ApplyActiveModifiers(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            int slotSequence,
            int stackCount)
        {
            if (gameplayEffect.ModifierCount <= 0
                || command.TargetAsc == Entity.Null
                || !em.Exists(command.TargetAsc)
                || !em.HasBuffer<AttributeValueBuffer>(command.TargetAsc)
                || !em.HasBuffer<AttributeActiveModifierBuffer>(command.TargetAsc))
            {
                return 0;
            }

            var attributes = em.GetBuffer<AttributeValueBuffer>(command.TargetAsc);
            var activeModifiers = em.GetBuffer<AttributeActiveModifierBuffer>(command.TargetAsc);
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

                var context = BuildMagnitudeContext(em, in command, setByCallerValues, in modifier, stackCount);
                if (!GASGeneratedMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
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
                AttributeHelper.MarkCurrentValueDirty(attributes, modifier.AttributeSetCode, modifier.AttributeCode);
                added++;
            }

            return added;
        }

        private static MagnitudeEvalContext BuildMagnitudeContext(
            EntityManager em,
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            in GASCatalogModifierDefinitionBlob modifier,
            int stackCount)
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

            if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute
                && TryReadAttributeValue(em, command.SourceAsc, in modifier, out var sourceValue))
            {
                context.HasSourceAttributeValue = 1;
                context.SourceAttributeValue = sourceValue;
            }

            if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute
                && TryReadAttributeValue(em, command.TargetAsc, in modifier, out var targetValue))
            {
                context.HasTargetAttributeValue = 1;
                context.TargetAttributeValue = targetValue;
            }

            return context;
        }

        private static bool TryReadAttributeValue(
            EntityManager em,
            Entity owner,
            in GASCatalogModifierDefinitionBlob modifier,
            out float value)
        {
            value = 0f;
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<AttributeValueBuffer>(owner))
                return false;

            var attrSetCode = modifier.CaptureAttributeSetCode != 0
                ? modifier.CaptureAttributeSetCode
                : modifier.AttributeSetCode;
            var attributeCode = modifier.CaptureAttributeCode != 0
                ? modifier.CaptureAttributeCode
                : modifier.AttributeCode;
            var attributes = em.GetBuffer<AttributeValueBuffer>(owner);
            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attributeCode);
            if (attrIndex < 0)
                return false;

            value = attributes[attrIndex].CurrentValue;
            return true;
        }

        private static bool TryFindSetByCallerValue(
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
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

        private static void RemoveActiveModifiersForSlot(
            EntityManager em,
            Entity owner,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<AttributeActiveModifierBuffer>(owner))
                return;

            var modifiers = em.GetBuffer<AttributeActiveModifierBuffer>(owner);
            var attributes = em.HasBuffer<AttributeValueBuffer>(owner)
                ? em.GetBuffer<AttributeValueBuffer>(owner)
                : default;
            for (var i = modifiers.Length - 1; i >= 0; i--)
            {
                var modifier = modifiers[i];
                if (modifier.SourceSequence != slotSequence
                    || modifier.SourceGameplayEffectCode != gameplayEffectCode)
                {
                    continue;
                }

                modifiers.RemoveAt(i);
                if (attributes.IsCreated)
                    AttributeHelper.MarkCurrentValueDirty(attributes, modifier.AttrSetCode, modifier.AttributeCode);
            }
        }

        private static int ApplyGrantedTags(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            Entity owner,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int slotSequence,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (gameplayEffect.GrantedTagMaskIndex < 0
                || gameplayEffect.GrantedTagMaskIndex >= catalog.TagMasks.Length
                || owner == Entity.Null
                || !em.Exists(owner))
            {
                return 0;
            }

            if (!em.HasComponent<TagMaskComponent>(owner) || !em.HasBuffer<TagTemporarySourceBuffer>(owner))
                return 0;

            var grantedMask = catalog.TagMasks[gameplayEffect.GrantedTagMaskIndex].Mask;
            var ownerTags = em.GetComponentData<TagMaskComponent>(owner);
            var sources = em.GetBuffer<TagTemporarySourceBuffer>(owner);
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

                if (!wasActive && eventWriter.IsCreated)
                {
                    eventWriter.EnqueueTagChangeEvent(new TagChangeEventBuffer
                    {
                        ASC = owner,
                        TagIndex = tagIndex,
                        Added = true,
                    });
                }
            }

            em.SetComponentData(owner, ownerTags);
            return CountTempTagSourcesForSlot(sources, slotSequence, gameplayEffect.GameplayEffectCode);
        }

        private static void RemoveGrantedTagsForSlot(
            EntityManager em,
            Entity owner,
            int slotSequence,
            int gameplayEffectCode,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<TagTemporarySourceBuffer>(owner))
                return;

            var sources = em.GetBuffer<TagTemporarySourceBuffer>(owner);
            for (var i = sources.Length - 1; i >= 0; i--)
            {
                var source = sources[i];
                if (source.SourceSequence != slotSequence
                    || source.SourceGameplayEffectCode != gameplayEffectCode)
                {
                    continue;
                }

                sources.RemoveAt(i);
                var removedFromMask = RemoveTagIndexFromEffectiveMaskIfUnreferenced(em, owner, source.TagIndex);
                if (removedFromMask && eventWriter.IsCreated)
                {
                    eventWriter.EnqueueTagChangeEvent(new TagChangeEventBuffer
                    {
                        ASC = owner,
                        TagIndex = source.TagIndex,
                        Added = false,
                    });
                }
            }
        }

        private static int ApplyGrantedAbilities(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            Entity owner,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int slotSequence,
            ref EntityCommandBuffer structuralEcb)
        {
            if (gameplayEffect.GrantedAbilityCount <= 0
                || owner == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<AbilitySlotBuffer>(owner))
            {
                return 0;
            }

            var grantedAbilities = em.GetBuffer<AbilitySlotBuffer>(owner);
            var added = 0;
            for (var i = 0; i < gameplayEffect.GrantedAbilityCount; i++)
            {
                var grantedIndex = gameplayEffect.GrantedAbilityStart + i;
                if ((uint)grantedIndex >= (uint)catalog.GrantedAbilities.Length)
                    continue;

                var granted = catalog.GrantedAbilities[grantedIndex];
                if (granted.AbilityCode <= 0
                    || HasGrantedAbilityForSlot(em, grantedAbilities, granted.AbilityCode, slotSequence, gameplayEffect.GameplayEffectCode))
                {
                    continue;
                }

                var ability = structuralEcb.CreateEntity(GASRuntimeEntityArchetypes.GrantedAbility(em));
                GASRuntimeEntityArchetypes.InitializeAbilityEntity(structuralEcb, ability);
                structuralEcb.SetComponent(
                    ability,
                    AbilityStateComponent.Create(granted.AbilityCode, granted.Level, owner));
                structuralEcb.SetComponent(ability, new AbilityMainTargetComponent { TargetAsc = Entity.Null });
                structuralEcb.SetComponent(ability, new AbilityGrantedByEffectComponent
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

                structuralEcb.AppendToBuffer(owner, new AbilitySlotBuffer { AbilityEntity = ability });
                added++;

                var activationPolicy = (GrantedAbilityActivationPolicy)granted.ActivationPolicy;
                if ((activationPolicy == GrantedAbilityActivationPolicy.WhenAdded
                        || activationPolicy == GrantedAbilityActivationPolicy.SyncWithEffect))
                {
                    structuralEcb.SetComponentEnabled<AbilityActivationPendingComponent>(ability, true);
                }
                else
                {
                    DisableMarker<AbilityActivationPendingComponent>(ref structuralEcb, ability);
                }
            }

            return CountGrantedAbilitiesForSlot(em, grantedAbilities, slotSequence, gameplayEffect.GameplayEffectCode) + added;
        }

        private static void DisableMarker<T>(ref EntityCommandBuffer ecb, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            ecb.SetComponentEnabled<T>(entity, false);
        }

        private static void RemoveGrantedAbilitiesForSlot(
            EntityManager em,
            ref EntityCommandBuffer structuralEcb,
            Entity owner,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<AbilitySlotBuffer>(owner))
                return;

            var grantedAbilities = em.GetBuffer<AbilitySlotBuffer>(owner);
            for (var i = grantedAbilities.Length - 1; i >= 0; i--)
            {
                var ability = grantedAbilities[i].AbilityEntity;
                if (!IsGrantedBySlot(em, ability, slotSequence, gameplayEffectCode))
                    continue;

                grantedAbilities.RemoveAt(i);
                RemoveGrantedAbilityEntity(em, ref structuralEcb, ability);
            }
        }

        private static bool HasGrantedAbilityForSlot(
            EntityManager em,
            DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
            int abilityCode,
            int slotSequence,
            int gameplayEffectCode)
        {
            for (var i = 0; i < grantedAbilities.Length; i++)
            {
                var ability = grantedAbilities[i].AbilityEntity;
                if (!IsGrantedBySlot(em, ability, slotSequence, gameplayEffectCode)
                    || !em.HasComponent<AbilityStateComponent>(ability))
                {
                    continue;
                }

                if (em.GetComponentData<AbilityStateComponent>(ability).Code == abilityCode)
                    return true;
            }

            return false;
        }

        private static int CountGrantedAbilitiesForSlot(
            EntityManager em,
            DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
            int slotSequence,
            int gameplayEffectCode)
        {
            var count = 0;
            for (var i = 0; i < grantedAbilities.Length; i++)
            {
                if (IsGrantedBySlot(em, grantedAbilities[i].AbilityEntity, slotSequence, gameplayEffectCode))
                    count++;
            }

            return count;
        }

        private static bool IsGrantedBySlot(
            EntityManager em,
            Entity ability,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (ability == Entity.Null
                || !em.Exists(ability)
                || !em.HasComponent<AbilityGrantedByEffectComponent>(ability))
            {
                return false;
            }

            var granted = em.GetComponentData<AbilityGrantedByEffectComponent>(ability);
            return granted.SourceSequence == slotSequence
                && granted.SourceGameplayEffectCode == gameplayEffectCode;
        }

        private static void RemoveGrantedAbilityEntity(
            EntityManager em,
            ref EntityCommandBuffer structuralEcb,
            Entity ability)
        {
            if (ability == Entity.Null || !em.Exists(ability))
                return;

            var isRunning = false;
            if (em.HasComponent<AbilityStateComponent>(ability))
            {
                var runtime = em.GetComponentData<AbilityStateComponent>(ability);
                isRunning = runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
            }

            if (isRunning)
            {
                AbilityRuntimeActions.RequestAbilityCancel(
                    ability,
                    em,
                    ref structuralEcb,
                    EAbilityLifecycleReason.GrantedEffectRemoved,
                    sourceEffect: Entity.Null);
                AbilityRuntimeActions.EnableDestroyOnCleanup(ability, em, ref structuralEcb);
                return;
            }

            structuralEcb.DestroyEntity(ability);
        }

        private static void CopySetByCallerSnapshot(
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
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
            EntityManager em,
            Entity owner,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(owner))
                return;

            RemoveSetByCallerSnapshotForSlot(
                em.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(owner),
                slotSequence,
                gameplayEffectCode);
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

        private static bool RemoveTagIndexFromEffectiveMaskIfUnreferenced(EntityManager em, Entity owner, int tagIndex)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasComponent<TagMaskComponent>(owner))
                return false;
            if (em.HasComponent<TagFixedMaskComponent>(owner)
                && em.GetComponentData<TagFixedMaskComponent>(owner).Mask.HasTag(tagIndex))
            {
                return false;
            }
            if (HasAnyTemporarySourceForTag(em, owner, tagIndex))
                return false;

            var mask = em.GetComponentData<TagMaskComponent>(owner);
            if (!mask.HasTag(tagIndex))
                return false;
            mask.RemoveTag(tagIndex);
            em.SetComponentData(owner, mask);
            return true;
        }

        private static bool HasAnyTemporarySourceForTag(EntityManager em, Entity owner, int tagIndex)
        {
            if (!em.Exists(owner) || !em.HasBuffer<TagTemporarySourceBuffer>(owner))
                return false;

            var sources = em.GetBuffer<TagTemporarySourceBuffer>(owner);
            for (var i = 0; i < sources.Length; i++)
            {
                if (sources[i].TagIndex == tagIndex)
                    return true;
            }

            return false;
        }

        private static void HandleDurationExpired(
            EntityManager em,
            Entity owner,
            DynamicBuffer<ActiveGameplayEffectBuffer> slots,
            int slotIndex,
            ref GASDefinitionCatalogBlob catalog,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if ((uint)slotIndex >= (uint)slots.Length)
                return;

            var slot = slots[slotIndex];
            if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
            {
                RemoveSlotAt(em, owner, slots, slotIndex, mutations, frame, ref structuralEcb, ref eventWriter);
                return;
            }

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            var expirationPolicy = (EffectExpirationPolicy)gameplayEffect.EffectExpirationPolicy;
            if (expirationPolicy == EffectExpirationPolicy.RefreshDuration)
            {
                RefreshSlotDuration(ref slot, frame, resetPeriod: true);
                slots[slotIndex] = slot;
                mutations.Add(CreateRefreshMutation(in slot, frame));
                return;
            }

            if (expirationPolicy == EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration
                && slot.StackCount > 1)
            {
                slot.StackCount--;
                RefreshSlotDuration(ref slot, frame, resetPeriod: true);
                slots[slotIndex] = slot;
                RebuildActiveModifiersForSlot(em, ref catalog, in gameplayEffect, in slot);
                mutations.Add(CreateStackMutation(in slot, frame));
                EnqueueStackCountChangedEvent(ref eventWriter, in slot);
                return;
            }

            RemoveSlotAt(em, owner, slots, slotIndex, mutations, frame, ref structuralEcb, ref eventWriter);
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

        private static int RebuildActiveModifiersForSlot(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            in ActiveGameplayEffectBuffer slot)
        {
            if (gameplayEffect.ModifierCount <= 0
                || slot.TargetAsc == Entity.Null
                || !em.Exists(slot.TargetAsc)
                || !em.HasBuffer<AttributeValueBuffer>(slot.TargetAsc)
                || !em.HasBuffer<AttributeActiveModifierBuffer>(slot.TargetAsc))
            {
                return 0;
            }

            RemoveActiveModifiersForSlot(em, slot.TargetAsc, slot.Sequence, slot.GameplayEffectCode);
            var attributes = em.GetBuffer<AttributeValueBuffer>(slot.TargetAsc);
            var activeModifiers = em.GetBuffer<AttributeActiveModifierBuffer>(slot.TargetAsc);
            var setByCallerSnapshot = em.HasBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(slot.TargetAsc)
                ? em.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(slot.TargetAsc)
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

                var context = BuildMagnitudeContextFromSlot(em, in slot, setByCallerSnapshot, in modifier);
                if (!GASGeneratedMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
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
                AttributeHelper.MarkCurrentValueDirty(attributes, modifier.AttributeSetCode, modifier.AttributeCode);
                added++;
            }

            return added;
        }

        private static MagnitudeEvalContext BuildMagnitudeContextFromSlot(
            EntityManager em,
            in ActiveGameplayEffectBuffer slot,
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerSnapshot,
            in GASCatalogModifierDefinitionBlob modifier)
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

            if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute
                && TryReadAttributeValue(em, slot.SourceAsc, in modifier, out var sourceValue))
            {
                context.HasSourceAttributeValue = 1;
                context.SourceAttributeValue = sourceValue;
            }

            if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute
                && TryReadAttributeValue(em, slot.TargetAsc, in modifier, out var targetValue))
            {
                context.HasTargetAttributeValue = 1;
                context.TargetAttributeValue = targetValue;
            }

            return context;
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

        private static void RemoveGameplayEffectsWithTags(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            Entity owner,
            in GASCatalogGameplayEffectDefinitionBlob appliedGameplayEffect,
            DynamicBuffer<ActiveGameplayEffectBuffer> slots,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (appliedGameplayEffect.RemoveGameplayEffectTagMaskIndex < 0
                || appliedGameplayEffect.RemoveGameplayEffectTagMaskIndex >= catalog.TagMasks.Length)
            {
                return;
            }

            var removeMask = catalog.TagMasks[appliedGameplayEffect.RemoveGameplayEffectTagMaskIndex].Mask;
            if (removeMask.IsEmpty)
                return;

            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var slotGameplayEffectIndex))
                    continue;

                ref readonly var slotGameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, slotGameplayEffectIndex);
                if (slotGameplayEffect.GrantedTagMaskIndex < 0
                    || slotGameplayEffect.GrantedTagMaskIndex >= catalog.TagMasks.Length)
                {
                    continue;
                }

                var slotGrantedMask = catalog.TagMasks[slotGameplayEffect.GrantedTagMaskIndex].Mask;
                if (slotGrantedMask.HasAnyTag(removeMask))
                    RemoveSlotAt(em, owner, slots, i, mutations, frame, ref structuralEcb, ref eventWriter);
            }
        }

        public static void RemoveSlotAt(
            EntityManager em,
            Entity owner,
            DynamicBuffer<ActiveGameplayEffectBuffer> slots,
            int slotIndex,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if ((uint)slotIndex >= (uint)slots.Length)
                return;

            var slot = slots[slotIndex];
            var activeModifierCount = CountActiveModifiersForSlot(em, owner, slot.Sequence, slot.GameplayEffectCode);
            RemoveActiveModifiersForSlot(em, owner, slot.Sequence, slot.GameplayEffectCode);
            RemoveGrantedTagsForSlot(em, owner, slot.Sequence, slot.GameplayEffectCode, ref eventWriter);
            RemoveGrantedAbilitiesForSlot(em, ref structuralEcb, owner, slot.Sequence, slot.GameplayEffectCode);
            RemoveSetByCallerSnapshotForSlot(em, owner, slot.Sequence, slot.GameplayEffectCode);
            RecordCleanup(em, owner, in slot, activeModifierCount, frame);
            mutations.Add(new ActiveEffectMutationBuffer
            {
                Sequence = slot.Sequence,
                Frame = frame,
                Kind = ActiveEffectMutationKind.Remove,
                ActiveEffect = Entity.Null,
                SourceAsc = slot.SourceAsc,
                TargetAsc = owner,
                SourceAbility = slot.SourceAbility,
                SourceEffect = slot.SourceEffect,
                GameplayEffectCode = slot.GameplayEffectCode,
                ContextId = slot.ContextId,
                ParentContextId = slot.ParentContextId,
                StackCount = slot.StackCount,
                DurationFrameOverride = slot.DurationFrame,
                PeriodFrame = slot.PeriodFrame,
            });
            EnqueueRemovedEvent(ref eventWriter, in slot);
            slots.RemoveAt(slotIndex);
        }

        private static void RecordCleanup(
            EntityManager em,
            Entity owner,
            in ActiveGameplayEffectBuffer slot,
            int activeModifierCount,
            int frame)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<ActiveGameplayEffectCleanupRecordBuffer>(owner))
                return;

            var records = em.GetBuffer<ActiveGameplayEffectCleanupRecordBuffer>(owner);
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
                TargetAsc = owner,
                SourceAbility = slot.SourceAbility,
                SourceEffect = slot.SourceEffect,
                Instigator = slot.Instigator,
                Causer = slot.Causer,
                GameplayEffectCode = slot.GameplayEffectCode,
                Level = slot.Level,
                StackCount = slot.StackCount,
                ContextId = slot.ContextId,
                ParentContextId = slot.ParentContextId,
                CleanupFrame = frame,
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
                CleanupResolvedFrame = frame,
                Flags = slot.Flags,
            });

            if (em.HasComponent<ASCActiveEffectsComponent>(owner))
            {
                var store = em.GetComponentData<ASCActiveEffectsComponent>(owner);
                store.LastCleanupFrame = frame;
                store.CleanupRecordCount = records.Length;
                em.SetComponentData(owner, store);
            }
        }

        private static void EmitPeriodCommand(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            in ActiveGameplayEffectBuffer slot,
            Entity owner,
            ref EffectCommandSpecStream.CommandWriter commandWriter)
        {
            if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                return;

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            if (gameplayEffect.PeriodGameplayEffectCode <= 0
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.PeriodGameplayEffectCode, out var periodGameplayEffectIndex))
            {
                return;
            }

            ref readonly var periodGameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, periodGameplayEffectIndex);
            var durationFrame = periodGameplayEffect.DurationFrames;
            var kind = RequiresActiveMutationLane(in periodGameplayEffect, durationFrame)
                ? GEEffectCommandKind.ActiveMutation
                : GEEffectCommandKind.Instant;
            var command = new GEEffectCommandBuffer
            {
                Kind = kind,
                Source = GEEffectCommandSource.Period,
                SourceAsc = slot.SourceAsc,
                TargetAsc = owner,
                SourceAbility = slot.SourceAbility,
                SourceEffect = Entity.Null,
                Instigator = slot.Instigator != Entity.Null ? slot.Instigator : slot.SourceAsc,
                Causer = slot.Causer != Entity.Null ? slot.Causer : slot.SourceAbility,
                GameplayEffectCode = gameplayEffect.PeriodGameplayEffectCode,
                Level = slot.Level,
                DurationFrameOverride = durationFrame,
                ParentContextId = slot.ContextId,
                TargetDataKind = slot.SourceAsc == owner ? ETargetDataKind.Self : ETargetDataKind.Entity,
                Flags = kind == GEEffectCommandKind.ActiveMutation ? GASGECommandSeedFlags.ActiveMutation : GASGECommandSeedFlags.None,
            };

            if (owner != Entity.Null
                && em.Exists(owner)
                && em.HasBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(owner))
            {
                commandWriter.AppendCommand(
                    command,
                    em.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(owner),
                    slot.Sequence,
                    slot.GameplayEffectCode);
                return;
            }

            commandWriter.AppendCommand(command);
        }

        private static bool EvaluateGameplayEffectRequirements(
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            in TagMaskComponent targetTags)
        {
            for (var i = 0; i < gameplayEffect.RequirementCount; i++)
            {
                var index = gameplayEffect.RequirementStart + i;
                if ((uint)index >= (uint)catalog.Requirements.Length)
                    return false;

                var requirement = catalog.Requirements[index];
                if (requirement.RequirementKind == GASRequirementKind.None)
                    continue;
                if (requirement.TagMaskIndex < 0 || requirement.TagMaskIndex >= catalog.TagMasks.Length)
                    continue;

                var mask = catalog.TagMasks[requirement.TagMaskIndex].Mask;
                if (requirement.RequirementKind == GASRequirementKind.RequiredTags && !targetTags.HasAllTags(mask))
                    return false;
                if (requirement.RequirementKind == GASRequirementKind.BlockedTags && targetTags.HasAnyTag(mask))
                    return false;
            }

            return true;
        }

        private static bool HasActiveEffectStorage(EntityManager em, Entity owner)
        {
            return ASCEntityFactory.HasASCRuntimeCoreComponents(em, owner);
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

        private static int CountActiveModifiersForSlot(
            EntityManager em,
            Entity owner,
            int sourceSequence,
            int gameplayEffectCode)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<AttributeActiveModifierBuffer>(owner))
                return 0;

            var count = 0;
            var modifiers = em.GetBuffer<AttributeActiveModifierBuffer>(owner);
            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                if (modifier.SourceSequence == sourceSequence
                    && modifier.SourceGameplayEffectCode == gameplayEffectCode)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsAvailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                && em.Exists(asc)
                && !ASCEntityFactory.IsDestroying(em, asc);
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;
            return next++;
        }

        private static void EmitOverflowCommand(
            ref GEEffectCommandStreamComponent stream,
            ref GASDefinitionCatalogBlob catalog,
            DynamicBuffer<GEEffectCommandBuffer> commands,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            in GEEffectCommandBuffer sourceCommand,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int frame)
        {
            if (gameplayEffect.OverflowGameplayEffectCode <= 0
                || gameplayEffect.OverflowGameplayEffectCode == sourceCommand.GameplayEffectCode
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.OverflowGameplayEffectCode, out var overflowIndex))
            {
                return;
            }

            ref readonly var overflowEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, overflowIndex);
            var durationFrame = overflowEffect.DurationFrames;
            var kind = RequiresActiveMutationLane(in overflowEffect, durationFrame)
                ? GEEffectCommandKind.ActiveMutation
                : GEEffectCommandKind.Instant;
            EffectCommandSpecStream.AppendPreparedCommand(
                ref stream,
                commands,
                setByCallerValues,
                new GEEffectCommandBuffer
                {
                    Frame = frame,
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
                },
                frame);
        }

        private static void EnqueueStackOverflowEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.StackOverflow,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
                ReasonCode = gameplayEffect.OverflowGameplayEffectCode,
            });
        }

        private static void EnqueueStackOverflowDeniedEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in GEEffectCommandBuffer command)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.StackOverflowDenied,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
            });
        }

        private static void EnqueueStackClearedByOverflowEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in GEEffectCommandBuffer command)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.StackClearedByOverflow,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
            });
        }

        private static void EnqueueStackCountChangedEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in ActiveGameplayEffectBuffer slot)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.StackCountChanged,
                SourceAsc = slot.SourceAsc,
                TargetAsc = slot.TargetAsc,
                SourceAbility = slot.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = slot.ContextId,
                EventCode = slot.GameplayEffectCode,
                Value = slot.StackCount,
            });
        }

        private static void EnqueueAppliedEvents(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            Entity gameplayEffectEntity)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.GameplayEffectApplied,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = gameplayEffectEntity,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
            });

            if (gameplayEffect.GameplayCueCode <= 0)
                return;

            writer.EnqueueCueRequest(new CueRequestBuffer
            {
                TargetAsc = command.TargetAsc,
                SourceAsc = command.SourceAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = gameplayEffectEntity,
                SourceEntity = command.SourceAbility,
                SourceType = CueSourceType.GameplayEffect,
                CueEntity = Entity.Null,
                ContextId = command.ContextId,
                ReasonCode = gameplayEffect.GameplayCueCode,
                CueEvent = EGameplayCueEvent.OnApply,
            });
            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.CueRequested,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = gameplayEffectEntity,
                ContextId = command.ContextId,
                EventCode = (int)EGameplayCueEvent.OnApply,
                ReasonCode = gameplayEffect.GameplayCueCode,
            });
        }

        private static void EnqueueRemovedEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in ActiveGameplayEffectBuffer slot)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.GameplayEffectRemoved,
                SourceAsc = slot.SourceAsc,
                TargetAsc = slot.TargetAsc,
                SourceAbility = slot.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = slot.ContextId,
                EventCode = slot.GameplayEffectCode,
            });
        }
    }
}
