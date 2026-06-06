///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime.Generated
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectRemoveSystem))]
    [UpdateBefore(typeof(GEEffectSpecBuildSystem))]
    public partial struct GEEffectCommandCatalogNormalizeSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GEEffectCommandBuffer>(),
                },
            });
            state.RequireForUpdate(_query);
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            state.Dependency = new GEEffectCommandCatalogNormalizeJob
            {
                CommandTypeHandle = SystemAPI.GetBufferTypeHandle<GEEffectCommandBuffer>(),
                Catalog = catalogComponent.Catalog,
            }.Schedule(_query, state.Dependency);
        }

        private struct GEEffectCommandCatalogNormalizeJob : IJobChunk
        {
            public BufferTypeHandle<GEEffectCommandBuffer> CommandTypeHandle;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated)
                    return;

                ref var catalog = ref Catalog.Value;
                var commandBuffers = chunk.GetBufferAccessor(ref CommandTypeHandle);
                for (var bufferIndex = 0; bufferIndex < commandBuffers.Length; bufferIndex++)
                {
                    var commands = commandBuffers[bufferIndex];
                    for (var i = 0; i < commands.Length; i++)
                    {
                        var command = commands[i];
                        if (GASGeneratedActiveEffectRuntime.TryNormalizeCommand(ref catalog, ref command))
                            commands[i] = command;
                    }
                }
            }
        }
    }

    [DisableAutoCreation]
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

    [DisableAutoCreation]
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

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var tickJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectsTypeHandle = SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                CleanupRecordLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                SetByCallerSnapshotLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),
                AttributeDirtyLookup = SystemAPI.GetComponentLookup<AttributeDirtyComponent>(isReadOnly: false),
                ActiveModifierPresentLookup =
                    SystemAPI.GetComponentLookup<AttributeActiveModifierPresentComponent>(isReadOnly: false),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                AbilityCancelRequestLookup =
                    SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(isReadOnly: false),
                AbilityDestroyOnCleanupLookup =
                    SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(isReadOnly: false),
                RemoveCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<GERemoveCommandBuffer>(isReadOnly: false),
                RemovePendingLookup = SystemAPI.GetComponentLookup<GERemoveCommandPendingComponent>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                MutationLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationBuffer>(isReadOnly: false),
                GameplayEventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(isReadOnly: false),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(isReadOnly: false),
                TagChangeEventLookup = SystemAPI.GetBufferLookup<TagChangeEventBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                GrantedAbilityArchetype = GASRuntimeEntityArchetypes.GrantedAbility(em),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                ProcessTickRecords = true,
            };
            state.Dependency = tickJob.Schedule(_ownerQuery, state.Dependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectPreTickSystem))]
    public partial struct GASActiveEffectRemoveSystem : ISystem
    {
        private EntityQuery _removeCommandQuery;

        public void OnCreate(ref SystemState state)
        {
            _removeCommandQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    GERemoveCommandPendingComponent,
                    GERemoveCommandBuffer,
                    ASCActiveEffectsComponent,
                    ActiveGameplayEffectBuffer>()
                .Build();
            state.RequireForUpdate(_removeCommandQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectsTypeHandle = SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                RemoveCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<GERemoveCommandBuffer>(isReadOnly: false),
                CleanupRecordLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                SetByCallerSnapshotLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),
                AttributeDirtyLookup = SystemAPI.GetComponentLookup<AttributeDirtyComponent>(isReadOnly: false),
                ActiveModifierPresentLookup =
                    SystemAPI.GetComponentLookup<AttributeActiveModifierPresentComponent>(isReadOnly: false),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                AbilityCancelRequestLookup =
                    SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(isReadOnly: false),
                AbilityDestroyOnCleanupLookup =
                    SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(isReadOnly: false),
                RemovePendingLookup = SystemAPI.GetComponentLookup<GERemoveCommandPendingComponent>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                MutationLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationBuffer>(isReadOnly: false),
                GameplayEventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(isReadOnly: false),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(isReadOnly: false),
                TagChangeEventLookup = SystemAPI.GetBufferLookup<TagChangeEventBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                ProcessExplicitRemoveCommands = true,
            }.Schedule(_removeCommandQuery, state.Dependency);
        }
    }

    internal static class GASGeneratedActiveEffectRuntime
    {
        public struct GEActiveEffectPreTickJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<ASCActiveEffectsComponent> ActiveEffectsTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            public BufferTypeHandle<GERemoveCommandBuffer> RemoveCommandBufferTypeHandle;
            public BufferLookup<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecordLookup;
            public BufferLookup<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshotLookup;
            public BufferLookup<AttributeValueBuffer> AttributeLookup;
            public BufferLookup<AttributeActiveModifierBuffer> ActiveModifierLookup;
            public ComponentLookup<AttributeDirtyComponent> AttributeDirtyLookup;
            public ComponentLookup<AttributeActiveModifierPresentComponent> ActiveModifierPresentLookup;
            public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public ComponentLookup<TagFixedMaskComponent> TagFixedMaskLookup;
            public BufferLookup<TagTemporarySourceBuffer> TagSourceLookup;
            public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;
            [ReadOnly] public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> AbilityGrantedLookup;
            public ComponentLookup<AbilityCancelRequestComponent> AbilityCancelRequestLookup;
            public ComponentLookup<AbilityDestroyOnCleanupComponent> AbilityDestroyOnCleanupLookup;
            public ComponentLookup<GERemoveCommandPendingComponent> RemovePendingLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<GESetByCallerValueBuffer> CommandSetByCallerLookup;
            public BufferLookup<ActiveEffectMutationBuffer> MutationLookup;
            public ComponentLookup<GameplayEventBusComponent> GameplayEventBusLookup;
            public BufferLookup<GameplayEventBusEventBuffer> GameplayEventLookup;
            public BufferLookup<TagChangeEventBuffer> TagChangeEventLookup;
            public EntityCommandBuffer StructuralEcb;
            public EntityArchetype GrantedAbilityArchetype;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;
            public bool ProcessTickRecords;
            public bool ProcessExplicitRemoveCommands;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if ((!ProcessTickRecords && !ProcessExplicitRemoveCommands)
                    || StreamEntity == Entity.Null
                    || !MutationLookup.HasBuffer(StreamEntity)
                    || (ProcessTickRecords && !Catalog.IsCreated))
                {
                    return;
                }

                var owners = chunk.GetNativeArray(EntityTypeHandle);
                var stores = chunk.GetNativeArray(ref ActiveEffectsTypeHandle);
                var slotBuffers = chunk.GetBufferAccessor(ref ActiveEffectSlotBufferTypeHandle);
                var mutations = MutationLookup[StreamEntity];
                var removeCommandBuffers = ProcessExplicitRemoveCommands
                    ? chunk.GetBufferAccessor(ref RemoveCommandBufferTypeHandle)
                    : default;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = owners[entityIndex];
                    var store = stores[entityIndex];
                    var slots = slotBuffers[entityIndex];

                    if (ProcessTickRecords)
                    {
                        if (store.ChunkSkipMatchedSlotCount > 0
                            || store.ChunkSkipDuePeriodSlotCount > 0
                            || store.CleanupRecordCount > 0)
                        {
                            ref var catalog = ref Catalog.Value;
                            ProcessTickOwner(owner, slots, mutations, ref catalog, ref store);
                        }
                    }

                    if (ProcessExplicitRemoveCommands)
                    {
                        var removeCommands = removeCommandBuffers[entityIndex];
                        for (var commandIndex = 0; commandIndex < removeCommands.Length; commandIndex++)
                        {
                            RemoveMatchingOwnerLocalEffects(
                                owner,
                                slots,
                                removeCommands[commandIndex].GameplayEffectCode,
                                mutations,
                                ref store);
                        }

                        removeCommands.Clear();
                        if (RemovePendingLookup.HasComponent(owner))
                            RemovePendingLookup.SetComponentEnabled(owner, false);
                    }

                    ActiveEffectStore.RefreshChunkSkipIndexCounters(ref store, slots, Frame);
                    stores[entityIndex] = store;
                }
            }

            private void ProcessTickOwner(
                Entity owner,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref GASDefinitionCatalogBlob catalog,
                ref ASCActiveEffectsComponent store)
            {
                if (!StreamLookup.HasComponent(StreamEntity))
                    return;

                for (var slotIndex = slots.Length - 1; slotIndex >= 0; slotIndex--)
                {
                    var slot = slots[slotIndex];
                    var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, Frame);
                    if (actionFlags == (int)ActiveEffectTickActionFlags.None)
                        continue;

                    if ((actionFlags & (int)ActiveEffectTickActionFlags.Period) != 0)
                    {
                        EmitPeriodCommand(ref catalog, in slot, owner);
                        slot.LastPeriodFrame = Frame;
                        slots[slotIndex] = slot;
                        mutations.Add(new ActiveEffectMutationBuffer
                        {
                            Sequence = slot.Sequence,
                            Frame = Frame,
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
                        HandleDurationExpired(owner, slots, slotIndex, ref catalog, mutations, ref store);
                }
            }

            private void RemoveMatchingOwnerLocalEffects(
                Entity owner,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots,
                int gameplayEffectCode,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref ASCActiveEffectsComponent store)
            {
                if (owner == Entity.Null)
                    return;

                for (var i = slots.Length - 1; i >= 0; i--)
                {
                    var slot = slots[i];
                    if (gameplayEffectCode > 0 && slot.GameplayEffectCode != gameplayEffectCode)
                        continue;

                    RemoveSlotAt(owner, slots, i, mutations, ref store);
                }
            }

            private void HandleDurationExpired(
                Entity owner,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots,
                int slotIndex,
                ref GASDefinitionCatalogBlob catalog,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref ASCActiveEffectsComponent store)
            {
                if ((uint)slotIndex >= (uint)slots.Length)
                    return;

                var slot = slots[slotIndex];
                if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                {
                    RemoveSlotAt(owner, slots, slotIndex, mutations, ref store);
                    return;
                }

                ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                var expirationPolicy = (EffectExpirationPolicy)gameplayEffect.EffectExpirationPolicy;
                if (expirationPolicy == EffectExpirationPolicy.RefreshDuration)
                {
                    RefreshSlotDuration(ref slot, Frame, resetPeriod: true);
                    slots[slotIndex] = slot;
                    mutations.Add(CreateRefreshMutation(in slot, Frame));
                    return;
                }

                if (expirationPolicy == EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration
                    && slot.StackCount > 1)
                {
                    slot.StackCount--;
                    RefreshSlotDuration(ref slot, Frame, resetPeriod: true);
                    slots[slotIndex] = slot;
                    RebuildActiveModifiersForSlot(ref catalog, in gameplayEffect, in slot);
                    mutations.Add(CreateStackMutation(in slot, Frame));
                    EnqueueStackCountChangedEvent(in slot);
                    return;
                }

                RemoveSlotAt(owner, slots, slotIndex, mutations, ref store);
            }

            private void RemoveSlotAt(
                Entity owner,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots,
                int slotIndex,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref ASCActiveEffectsComponent store)
            {
                if ((uint)slotIndex >= (uint)slots.Length)
                    return;

                var slot = slots[slotIndex];
                var activeModifierCount = CountActiveModifiersForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RemoveActiveModifiersForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedTagsForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedAbilitiesForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RemoveSetByCallerSnapshotForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RecordCleanup(owner, in slot, activeModifierCount, ref store);
                mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    Frame = Frame,
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
                EnqueueRemovedEvent(in slot);
                slots.RemoveAt(slotIndex);
            }

            private int RebuildActiveModifiersForSlot(
                ref GASDefinitionCatalogBlob catalog,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                in ActiveGameplayEffectBuffer slot)
            {
                if (gameplayEffect.ModifierCount <= 0
                    || slot.TargetAsc == Entity.Null
                    || !AttributeLookup.HasBuffer(slot.TargetAsc)
                    || !ActiveModifierLookup.HasBuffer(slot.TargetAsc))
                {
                    return 0;
                }

                RemoveActiveModifiersForSlot(slot.TargetAsc, slot.Sequence, slot.GameplayEffectCode);
                var attributes = AttributeLookup[slot.TargetAsc];
                var activeModifiers = ActiveModifierLookup[slot.TargetAsc];
                var setByCallerSnapshot = SetByCallerSnapshotLookup.HasBuffer(slot.TargetAsc)
                    ? SetByCallerSnapshotLookup[slot.TargetAsc]
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

                    var context = BuildMagnitudeContextFromSlot(in slot, setByCallerSnapshot, in modifier);
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
                    MarkActiveModifierAdded(slot.TargetAsc);
                    MarkCurrentValueDirty(attributes, slot.TargetAsc, modifier.AttributeSetCode, modifier.AttributeCode);
                    added++;
                }

                return added;
            }

            private MagnitudeEvalContext BuildMagnitudeContextFromSlot(
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
                    && TryReadAttributeValue(slot.SourceAsc, in modifier, out var sourceValue))
                {
                    context.HasSourceAttributeValue = 1;
                    context.SourceAttributeValue = sourceValue;
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute
                    && TryReadAttributeValue(slot.TargetAsc, in modifier, out var targetValue))
                {
                    context.HasTargetAttributeValue = 1;
                    context.TargetAttributeValue = targetValue;
                }

                return context;
            }

            private bool TryReadAttributeValue(
                Entity owner,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                if (owner == Entity.Null || !AttributeLookup.HasBuffer(owner))
                    return false;

                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attributeCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attributes = AttributeLookup[owner];
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attributeCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private void RemoveActiveModifiersForSlot(
                Entity owner,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !ActiveModifierLookup.HasBuffer(owner))
                    return;

                var modifiers = ActiveModifierLookup[owner];
                var hasAttributes = AttributeLookup.HasBuffer(owner);
                var attributes = hasAttributes ? AttributeLookup[owner] : default;
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
                        MarkCurrentValueDirty(attributes, owner, modifier.AttrSetCode, modifier.AttributeCode);
                }

                RefreshActiveModifierPresence(owner, modifiers);
            }

            private int CountActiveModifiersForSlot(
                Entity owner,
                int sourceSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !ActiveModifierLookup.HasBuffer(owner))
                    return 0;

                var count = 0;
                var modifiers = ActiveModifierLookup[owner];
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

            private void RemoveGrantedTagsForSlot(
                Entity owner,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !TagSourceLookup.HasBuffer(owner))
                    return;

                var sources = TagSourceLookup[owner];
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    var source = sources[i];
                    if (source.SourceSequence != slotSequence
                        || source.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    sources.RemoveAt(i);
                    var removedFromMask = RemoveTagIndexFromEffectiveMaskIfUnreferenced(owner, source.TagIndex);
                    if (removedFromMask)
                    {
                        EnqueueTagChangeEvent(new TagChangeEventBuffer
                        {
                            ASC = owner,
                            TagIndex = source.TagIndex,
                            Added = false,
                        });
                    }
                }
            }

            private bool RemoveTagIndexFromEffectiveMaskIfUnreferenced(Entity owner, int tagIndex)
            {
                if (owner == Entity.Null || !TagMaskLookup.HasComponent(owner))
                    return false;
                if (TagFixedMaskLookup.HasComponent(owner)
                    && TagFixedMaskLookup[owner].Mask.HasTag(tagIndex))
                {
                    return false;
                }
                if (HasAnyTemporarySourceForTag(owner, tagIndex))
                    return false;

                var mask = TagMaskLookup[owner];
                if (!mask.HasTag(tagIndex))
                    return false;
                mask.RemoveTag(tagIndex);
                TagMaskLookup[owner] = mask;
                return true;
            }

            private bool HasAnyTemporarySourceForTag(Entity owner, int tagIndex)
            {
                if (owner == Entity.Null || !TagSourceLookup.HasBuffer(owner))
                    return false;

                var sources = TagSourceLookup[owner];
                for (var i = 0; i < sources.Length; i++)
                {
                    if (sources[i].TagIndex == tagIndex)
                        return true;
                }

                return false;
            }

            private void RemoveGrantedAbilitiesForSlot(
                Entity owner,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !AbilitySlotLookup.HasBuffer(owner))
                    return;

                var grantedAbilities = AbilitySlotLookup[owner];
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
                    RequestAbilityCancel(ability, runtime);
                    EnableDestroyOnCleanup(ability);
                    return;
                }

                StructuralEcb.DestroyEntity(ability);
            }

            private void RequestAbilityCancel(Entity ability, in AbilityStateComponent runtime)
            {
                if (!AbilityCancelRequestLookup.HasComponent(ability)
                    || AbilityCancelRequestLookup.IsComponentEnabled(ability))
                {
                    return;
                }

                AbilityCancelRequestLookup[ability] = new AbilityCancelRequestComponent
                {
                    Reason = EAbilityLifecycleReason.GrantedEffectRemoved,
                    SourceAbility = Entity.Null,
                    SourceEffect = Entity.Null,
                    SourceAbilityCode = 0,
                };
                AbilityCancelRequestLookup.SetComponentEnabled(ability, true);
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = EGameplayEventType.AbilityCancelRequested,
                    SourceAsc = runtime.Owner,
                    TargetAsc = runtime.Owner,
                    SourceAbility = ability,
                    GameplayEffect = Entity.Null,
                    RelatedAbility = Entity.Null,
                    EventCode = runtime.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.GrantedEffectRemoved,
                });
            }

            private void EnableDestroyOnCleanup(Entity ability)
            {
                if (AbilityDestroyOnCleanupLookup.HasComponent(ability)
                    && !AbilityDestroyOnCleanupLookup.IsComponentEnabled(ability))
                {
                    AbilityDestroyOnCleanupLookup.SetComponentEnabled(ability, true);
                }
            }

            private void RemoveSetByCallerSnapshotForSlot(
                Entity owner,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !SetByCallerSnapshotLookup.HasBuffer(owner))
                    return;

                GASGeneratedActiveEffectRuntime.RemoveSetByCallerSnapshotForSlot(
                    SetByCallerSnapshotLookup[owner],
                    slotSequence,
                    gameplayEffectCode);
            }

            private void RecordCleanup(
                Entity owner,
                in ActiveGameplayEffectBuffer slot,
                int activeModifierCount,
                ref ASCActiveEffectsComponent store)
            {
                if (owner == Entity.Null || !CleanupRecordLookup.HasBuffer(owner))
                    return;

                var records = CleanupRecordLookup[owner];
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

                store.LastCleanupFrame = Frame;
                store.CleanupRecordCount = records.Length;
            }

            private void EmitPeriodCommand(
                ref GASDefinitionCatalogBlob catalog,
                in ActiveGameplayEffectBuffer slot,
                Entity owner)
            {
                if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                    return;

                ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                if (gameplayEffect.PeriodGameplayEffectCode <= 0
                    || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.PeriodGameplayEffectCode, out var periodGameplayEffectIndex)
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !CommandSetByCallerLookup.HasBuffer(StreamEntity))
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

                var commandSetByCallerValues = CommandSetByCallerLookup[StreamEntity];
                var sourceSetByCallerValues = SetByCallerSnapshotLookup.HasBuffer(owner)
                    ? SetByCallerSnapshotLookup[owner]
                    : default;
                var setByCallerCount = CountSetByCallerValues(sourceSetByCallerValues, slot.Sequence, slot.GameplayEffectCode);
                var stream = StreamLookup[StreamEntity];
                var resolved = PrepareCommand(ref stream, commandSetByCallerValues, in command, setByCallerCount, Frame);
                CopySetByCallerValues(
                    commandSetByCallerValues,
                    sourceSetByCallerValues,
                    slot.Sequence,
                    slot.GameplayEffectCode,
                    resolved.Sequence);
                CommandLookup[StreamEntity].Add(resolved);
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
                    resolved.Sequence = Allocate(ref stream.NextCommandSequence);
                if (resolved.Frame <= 0)
                    resolved.Frame = currentFrame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = Allocate(ref stream.NextContextId);
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

            private void MarkActiveModifierAdded(Entity asc)
            {
                if (asc == Entity.Null
                    || !ActiveModifierPresentLookup.HasComponent(asc)
                    || ActiveModifierPresentLookup.IsComponentEnabled(asc))
                {
                    return;
                }

                ActiveModifierPresentLookup.SetComponentEnabled(asc, true);
            }

            private void RefreshActiveModifierPresence(
                Entity asc,
                DynamicBuffer<AttributeActiveModifierBuffer> modifiers)
            {
                if (asc == Entity.Null || !ActiveModifierPresentLookup.HasComponent(asc))
                    return;

                ActiveModifierPresentLookup.SetComponentEnabled(asc, modifiers.Length > 0);
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
                if (asc != Entity.Null
                    && AttributeDirtyLookup.HasComponent(asc)
                    && !AttributeDirtyLookup.IsComponentEnabled(asc))
                {
                    AttributeDirtyLookup.SetComponentEnabled(asc, true);
                }
            }

            private void EnqueueStackCountChangedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
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

            private void EnqueueRemovedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
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

            private void EnqueueGameplayEvent(GameplayEventBusEventBuffer evt)
            {
                if (EventBusEntity == Entity.Null || !GameplayEventLookup.HasBuffer(EventBusEntity))
                    return;

                evt.Frame = Frame;
                if (GameplayEventBusLookup.HasComponent(EventBusEntity))
                {
                    var eventBus = GameplayEventBusLookup[EventBusEntity];
                    evt.Sequence = eventBus.NextSequence;
                    eventBus.NextSequence++;
                    GameplayEventBusLookup[EventBusEntity] = eventBus;
                }
                else
                {
                    evt.Sequence = 0;
                }

                GameplayEventLookup[EventBusEntity].Add(evt);
            }

            private void EnqueueTagChangeEvent(TagChangeEventBuffer evt)
            {
                if (EventBusEntity != Entity.Null && TagChangeEventLookup.HasBuffer(EventBusEntity))
                    TagChangeEventLookup[EventBusEntity].Add(evt);
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
                AttributeHelper.MarkActiveModifierAdded(em, command.TargetAsc);
                AttributeHelper.MarkCurrentValueDirty(
                    em,
                    command.TargetAsc,
                    attributes,
                    modifier.AttributeSetCode,
                    modifier.AttributeCode);
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
                    AttributeHelper.MarkCurrentValueDirty(
                        em,
                        owner,
                        attributes,
                        modifier.AttrSetCode,
                        modifier.AttributeCode);
            }

            AttributeHelper.RefreshActiveModifierPresence(em, owner, modifiers);
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
                AttributeHelper.MarkActiveModifierAdded(em, slot.TargetAsc);
                AttributeHelper.MarkCurrentValueDirty(
                    em,
                    slot.TargetAsc,
                    attributes,
                    modifier.AttributeSetCode,
                    modifier.AttributeCode);
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
