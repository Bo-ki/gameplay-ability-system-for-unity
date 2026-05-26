using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE Tick。处理 Duration 计时、Period 子效果触发和自然过期。
    /// </summary>
    [UpdateInGroup(typeof(GASEffectGroup))]
    [UpdateAfter(typeof(SEffectRemove))]
    public partial struct SEffectTick : ISystem
    {
        private EntityQuery _activeDurationQuery;
        private EntityQuery _activeEffectStoreQuery;
        private ComponentTypeHandle<CActiveEffectStore> _activeEffectStoreHandle;
        private BufferTypeHandle<BActiveEffectSlot> _activeEffectSlotHandle;
        private ComponentTypeHandle<CEffectContext> _effectContextHandle;
        private ComponentTypeHandle<CDurationDefinition> _durationDefinitionHandle;
        private ComponentTypeHandle<CDurationRuntime> _durationRuntimeHandle;
        private ComponentTypeHandle<CPeriodDefinition> _periodDefinitionHandle;
        private ComponentTypeHandle<CPeriodRuntime> _periodRuntimeHandle;
        private ComponentTypeHandle<CEffectLifecycle> _effectLifecycleHandle;
        private EntityTypeHandle _entityHandle;

        public void OnCreate(ref SystemState state)
        {
            _activeDurationQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext, CDurationDefinition, CDurationRuntime>()
                .WithNone<CEffectDestroy>()
                .WithNone<CEffectCleanup>()
                .WithNone<CEffectFinalDestroy>()
                .Build();
            _activeEffectStoreQuery = SystemAPI.QueryBuilder()
                .WithAllRW<CActiveEffectStore>()
                .WithAll<BActiveEffectSlot>()
                .Build();
            _activeEffectStoreHandle = state.GetComponentTypeHandle<CActiveEffectStore>();
            _activeEffectSlotHandle = state.GetBufferTypeHandle<BActiveEffectSlot>(true);
            _effectContextHandle = state.GetComponentTypeHandle<CEffectContext>(true);
            _durationDefinitionHandle = state.GetComponentTypeHandle<CDurationDefinition>(true);
            _durationRuntimeHandle = state.GetComponentTypeHandle<CDurationRuntime>(true);
            _periodDefinitionHandle = state.GetComponentTypeHandle<CPeriodDefinition>(true);
            _periodRuntimeHandle = state.GetComponentTypeHandle<CPeriodRuntime>(true);
            _effectLifecycleHandle = state.GetComponentTypeHandle<CEffectLifecycle>(true);
            _entityHandle = state.GetEntityTypeHandle();
            state.RequireForUpdate<GlobalTimer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (_activeEffectStoreQuery.CalculateEntityCount() == 0
                && _activeDurationQuery.CalculateEntityCount() == 0)
            {
                return;
            }

            var currentFrame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var dueSlotCandidateQueue = new NativeQueue<OwnerLocalDueSlotCandidate>(Allocator.TempJob);
            var globalIndexCandidateQueue = new NativeQueue<BGlobalActiveEffectIndex>(Allocator.TempJob);
            var durationCandidateQueue = new NativeQueue<DurationEffectCandidate>(Allocator.TempJob);

            _activeEffectStoreHandle.Update(ref state);
            _activeEffectSlotHandle.Update(ref state);
            _effectContextHandle.Update(ref state);
            _durationDefinitionHandle.Update(ref state);
            _durationRuntimeHandle.Update(ref state);
            _periodDefinitionHandle.Update(ref state);
            _periodRuntimeHandle.Update(ref state);
            _effectLifecycleHandle.Update(ref state);
            _entityHandle.Update(ref state);
            var gameplayEffectLookup = SystemAPI.GetBufferLookup<BGameplayEffect>(true);
            var refreshHandle = new RefreshOwnerLocalChunkSkipIndexJob
            {
                CurrentFrame = currentFrame,
                ActiveEffectStoreHandle = _activeEffectStoreHandle,
                ActiveEffectSlotHandle = _activeEffectSlotHandle,
                EntityHandle = _entityHandle,
                DueSlotCandidates = dueSlotCandidateQueue.AsParallelWriter(),
                GlobalIndexCandidates = globalIndexCandidateQueue.AsParallelWriter(),
            }.ScheduleParallel(_activeEffectStoreQuery, state.Dependency);
            var collectDurationHandle = new CollectDurationEffectCandidatesJob
            {
                CurrentFrame = currentFrame,
                EntityHandle = _entityHandle,
                EffectContextHandle = _effectContextHandle,
                DurationDefinitionHandle = _durationDefinitionHandle,
                DurationRuntimeHandle = _durationRuntimeHandle,
                PeriodDefinitionHandle = _periodDefinitionHandle,
                PeriodRuntimeHandle = _periodRuntimeHandle,
                EffectLifecycleHandle = _effectLifecycleHandle,
                GameplayEffectLookup = gameplayEffectLookup,
                DurationCandidates = durationCandidateQueue.AsParallelWriter(),
            }.ScheduleParallel(_activeDurationQuery, refreshHandle);
            collectDurationHandle.Complete();

            var globalIndexCandidates = new NativeList<BGlobalActiveEffectIndex>(Allocator.Temp);
            while (globalIndexCandidateQueue.TryDequeue(out var globalIndexCandidate))
                globalIndexCandidates.Add(globalIndexCandidate);
            SortGlobalIndexCandidates(globalIndexCandidates);
            ActiveEffectStore.TryMergeGlobalIndexEntries(em, globalIndexCandidates, currentFrame);

            var dueSlotCandidates = new NativeList<OwnerLocalDueSlotCandidate>(Allocator.Temp);
            while (dueSlotCandidateQueue.TryDequeue(out var dueSlotCandidate))
                dueSlotCandidates.Add(dueSlotCandidate);
            SortDueSlotCandidates(dueSlotCandidates);
            for (var i = 0; i < dueSlotCandidates.Length; i++)
            {
                var candidate = dueSlotCandidates[i];
                TickStoreBackedDurationEffect(
                    em,
                    ref ecb,
                    candidate.Slot,
                    candidate.ActionFlags,
                    currentFrame);
            }

            var durationCandidates = new NativeList<DurationEffectCandidate>(Allocator.Temp);
            while (durationCandidateQueue.TryDequeue(out var durationCandidate))
                durationCandidates.Add(durationCandidate);
            SortDurationEffectCandidates(durationCandidates);

            if (globalIndexCandidates.Length == 0
                && dueSlotCandidates.Length == 0
                && durationCandidates.Length == 0)
            {
                ecb.Dispose();
                dueSlotCandidates.Dispose();
                globalIndexCandidates.Dispose();
                durationCandidates.Dispose();
                durationCandidateQueue.Dispose();
                globalIndexCandidateQueue.Dispose();
                dueSlotCandidateQueue.Dispose();
                return;
            }

            for (var i = 0; i < durationCandidates.Length; i++)
            {
                var candidate = durationCandidates[i];
                var ge = candidate.Effect;
                if (IsStoreBackedEffect(em, ge))
                    continue;

                TickLegacyDurationEffect(
                    em,
                    ref ecb,
                    ge,
                    candidate.ActionFlags,
                    currentFrame);
            }

            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                currentFrame,
                EGasRuntimeDiagnosticModule.Effect);
            ecb.Dispose();
            dueSlotCandidates.Dispose();
            globalIndexCandidates.Dispose();
            durationCandidates.Dispose();
            durationCandidateQueue.Dispose();
            globalIndexCandidateQueue.Dispose();
            dueSlotCandidateQueue.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool IsStoreBackedEffect(EntityManager em, Entity ge)
        {
            if (!em.Exists(ge) || !em.HasComponent<CEffectContext>(ge))
                return false;

            var context = em.GetComponentData<CEffectContext>(ge);
            return ActiveEffectStore.HasSlot(em, context.TargetAsc, ge);
        }

        private static void TickStoreBackedDurationEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            in BActiveEffectSlot slot,
            int actionFlags,
            int currentFrame)
        {
            var ge = slot.ActiveEffectEntity;
            if (!em.Exists(ge)
                || !em.HasComponent<CEffectContext>(ge)
                || em.HasComponent<CEffectCleanup>(ge)
                || em.HasComponent<CEffectDestroy>(ge)
                || em.HasComponent<CEffectFinalDestroy>(ge))
            {
                return;
            }

            var context = em.GetComponentData<CEffectContext>(ge);

            if (HasTickAction(actionFlags, EActiveEffectTickActionFlags.Period))
                TickPeriodEffects(em, ref ecb, ge, context, slot, currentFrame);

            if (!HasTickAction(actionFlags, EActiveEffectTickActionFlags.DurationExpire))
                return;

            if (em.HasComponent<CDurationRuntime>(ge))
            {
                var durationRuntime = em.GetComponentData<CDurationRuntime>(ge);
                EffectRuntimeUtility.HandleDurationExpired(em, ref ecb, ge, context, ref durationRuntime, currentFrame);
                return;
            }

            EffectRuntimeUtility.MarkEffectForRemoval(em, ref ecb, ge, currentFrame);
        }

        private static bool HasTickAction(int actionFlags, EActiveEffectTickActionFlags action)
        {
            return (actionFlags & (int)action) != 0;
        }

        private static void TickLegacyDurationEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int actionFlags,
            int currentFrame)
        {
            if (!em.Exists(ge)
                || !em.HasComponent<CEffectContext>(ge)
                || !em.HasComponent<CDurationDefinition>(ge)
                || !em.HasComponent<CDurationRuntime>(ge)
                || em.HasComponent<CEffectCleanup>(ge)
                || em.HasComponent<CEffectDestroy>(ge)
                || em.HasComponent<CEffectFinalDestroy>(ge))
            {
                return;
            }

            var durationRuntime = em.GetComponentData<CDurationRuntime>(ge);
            var context = em.GetComponentData<CEffectContext>(ge);

            if (HasTickAction(actionFlags, EActiveEffectTickActionFlags.Period))
                TickPeriodEffects(em, ref ecb, ge, context, currentFrame);

            if (HasTickAction(actionFlags, EActiveEffectTickActionFlags.DurationExpire))
                EffectRuntimeUtility.HandleDurationExpired(em, ref ecb, ge, context, ref durationRuntime, currentFrame);
        }

        private static void TickPeriodEffects(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            in BActiveEffectSlot slot,
            int currentFrame)
        {
            if (slot.State != EActiveEffectSlotState.Active)
                return;

            if (slot.PeriodFrame <= 0)
                return;

            if (currentFrame - slot.LastPeriodFrame < slot.PeriodFrame)
                return;

            if (!EffectRuntimeUtility.TryGetStaticDefinitionBlob(em, ge, out var blob))
                return;

            ref var staticDefinition = ref blob.Value;
            if (!staticDefinition.HasPeriod)
                return;

            for (var i = 0; i < staticDefinition.PeriodEffectCodes.Length; i++)
                EffectRuntimeUtility.CreateDerivedApplyRequest(
                    em,
                    ref ecb,
                    ge,
                    context,
                    staticDefinition.PeriodEffectCodes[i]);

            SetPeriodStartTime(em, ref ecb, ge, currentFrame);
        }

        private static void TickPeriodEffects(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            int currentFrame)
        {
            if (!TryGetPeriodDefinition(em, ge, out var periodDefinition))
                return;

            if (periodDefinition.Period <= 0)
                return;

            var runtime = GetPeriodRuntime(em, ge);
            if (currentFrame - runtime.StartTime < periodDefinition.Period)
                return;

            if (!EffectRuntimeUtility.TryGetStaticDefinitionBlob(em, ge, out var blob))
                return;

            ref var staticDefinition = ref blob.Value;
            if (!staticDefinition.HasPeriod)
                return;

            for (var i = 0; i < staticDefinition.PeriodEffectCodes.Length; i++)
                EffectRuntimeUtility.CreateDerivedApplyRequest(
                    em,
                    ref ecb,
                    ge,
                    context,
                    staticDefinition.PeriodEffectCodes[i]);

            SetPeriodStartTime(em, ref ecb, ge, currentFrame);
        }

        private static bool TryGetPeriodDefinition(
            EntityManager em,
            Entity ge,
            out CPeriodDefinition definition)
        {
            if (em.HasComponent<CPeriodDefinition>(ge))
            {
                definition = em.GetComponentData<CPeriodDefinition>(ge);
                return true;
            }

            definition = default;
            return false;
        }

        private static CPeriodRuntime GetPeriodRuntime(EntityManager em, Entity ge)
        {
            if (em.HasComponent<CPeriodRuntime>(ge))
                return em.GetComponentData<CPeriodRuntime>(ge);

            return default;
        }

        private static void SetPeriodStartTime(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int currentFrame)
        {
            if (em.HasComponent<CPeriodRuntime>(ge))
            {
                var runtime = em.GetComponentData<CPeriodRuntime>(ge);
                runtime.StartTime = currentFrame;
                ecb.SetComponent(ge, runtime);
            }
            else
            {
                ecb.AddComponent(ge, new CPeriodRuntime { StartTime = currentFrame });
            }

            if (em.HasComponent<CEffectContext>(ge))
                ActiveEffectStore.TryRefreshPeriodFrame(em, ge, em.GetComponentData<CEffectContext>(ge), currentFrame);
        }

        private static void SortDueSlotCandidates(NativeList<OwnerLocalDueSlotCandidate> candidates)
        {
            for (var i = 1; i < candidates.Length; i++)
            {
                var current = candidates[i];
                var j = i - 1;
                while (j >= 0 && CompareDueSlotCandidates(candidates[j], current) > 0)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }

                candidates[j + 1] = current;
            }
        }

        private static void SortGlobalIndexCandidates(NativeList<BGlobalActiveEffectIndex> candidates)
        {
            for (var i = 1; i < candidates.Length; i++)
            {
                var current = candidates[i];
                var j = i - 1;
                while (j >= 0 && CompareGlobalIndexCandidates(candidates[j], current) > 0)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }

                candidates[j + 1] = current;
            }
        }

        private static void SortDurationEffectCandidates(NativeList<DurationEffectCandidate> candidates)
        {
            for (var i = 1; i < candidates.Length; i++)
            {
                var current = candidates[i];
                var j = i - 1;
                while (j >= 0 && CompareDurationEffectCandidates(candidates[j], current) > 0)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }

                candidates[j + 1] = current;
            }
        }

        private static int CompareGlobalIndexCandidates(
            in BGlobalActiveEffectIndex left,
            in BGlobalActiveEffectIndex right)
        {
            var result = left.OwnerAsc.Index.CompareTo(right.OwnerAsc.Index);
            if (result != 0)
                return result;

            result = left.OwnerAsc.Version.CompareTo(right.OwnerAsc.Version);
            if (result != 0)
                return result;

            result = left.Sequence.CompareTo(right.Sequence);
            if (result != 0)
                return result;

            result = left.ActiveEffectEntity.Index.CompareTo(right.ActiveEffectEntity.Index);
            if (result != 0)
                return result;

            return left.ActiveEffectEntity.Version.CompareTo(right.ActiveEffectEntity.Version);
        }

        private static int CompareDueSlotCandidates(
            in OwnerLocalDueSlotCandidate left,
            in OwnerLocalDueSlotCandidate right)
        {
            var result = left.OwnerIndex.CompareTo(right.OwnerIndex);
            if (result != 0)
                return result;

            result = left.OwnerVersion.CompareTo(right.OwnerVersion);
            if (result != 0)
                return result;

            result = left.SlotSequence.CompareTo(right.SlotSequence);
            if (result != 0)
                return result;

            result = left.SlotIndex.CompareTo(right.SlotIndex);
            if (result != 0)
                return result;

            result = left.ActiveEffectIndex.CompareTo(right.ActiveEffectIndex);
            if (result != 0)
                return result;

            return left.ActiveEffectVersion.CompareTo(right.ActiveEffectVersion);
        }

        private static int CompareDurationEffectCandidates(
            in DurationEffectCandidate left,
            in DurationEffectCandidate right)
        {
            var result = left.EffectIndex.CompareTo(right.EffectIndex);
            if (result != 0)
                return result;

            return left.EffectVersion.CompareTo(right.EffectVersion);
        }

        private struct OwnerLocalDueSlotCandidate
        {
            public Entity Owner;
            public BActiveEffectSlot Slot;
            public int ActionFlags;
            public int OwnerIndex;
            public int OwnerVersion;
            public int SlotSequence;
            public int SlotIndex;
            public int ActiveEffectIndex;
            public int ActiveEffectVersion;
        }

        private struct DurationEffectCandidate
        {
            public Entity Effect;
            public int ActionFlags;
            public int EffectIndex;
            public int EffectVersion;
        }

        [BurstCompile]
        private struct CollectDurationEffectCandidatesJob : IJobChunk
        {
            public int CurrentFrame;

            [ReadOnly]
            public EntityTypeHandle EntityHandle;

            [ReadOnly]
            public ComponentTypeHandle<CEffectContext> EffectContextHandle;

            [ReadOnly]
            public ComponentTypeHandle<CDurationDefinition> DurationDefinitionHandle;

            [ReadOnly]
            public ComponentTypeHandle<CDurationRuntime> DurationRuntimeHandle;

            [ReadOnly]
            public ComponentTypeHandle<CPeriodDefinition> PeriodDefinitionHandle;

            [ReadOnly]
            public ComponentTypeHandle<CPeriodRuntime> PeriodRuntimeHandle;

            [ReadOnly]
            public ComponentTypeHandle<CEffectLifecycle> EffectLifecycleHandle;

            [ReadOnly]
            public BufferLookup<BGameplayEffect> GameplayEffectLookup;

            public NativeQueue<DurationEffectCandidate>.ParallelWriter DurationCandidates;

            [BurstCompile]
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var contexts = chunk.GetNativeArray(ref EffectContextHandle);
                var durationDefinitions = chunk.GetNativeArray(ref DurationDefinitionHandle);
                var durationRuntimes = chunk.GetNativeArray(ref DurationRuntimeHandle);
                var hasPeriodDefinition = chunk.Has(ref PeriodDefinitionHandle);
                var hasPeriodRuntime = chunk.Has(ref PeriodRuntimeHandle);
                var hasLifecycle = chunk.Has(ref EffectLifecycleHandle);
                var periodDefinitions = hasPeriodDefinition
                    ? chunk.GetNativeArray(ref PeriodDefinitionHandle)
                    : default;
                var periodRuntimes = hasPeriodRuntime
                    ? chunk.GetNativeArray(ref PeriodRuntimeHandle)
                    : default;
                var lifecycles = hasLifecycle
                    ? chunk.GetNativeArray(ref EffectLifecycleHandle)
                    : default;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var effect = entities[entityIndex];
                    var context = contexts[entityIndex];
                    var durationRuntime = durationRuntimes[entityIndex];
                    var appliedDurationEffect = IsAppliedDurationEffect(
                        effect,
                        context,
                        durationRuntime,
                        hasLifecycle,
                        lifecycles,
                        entityIndex,
                        GameplayEffectLookup);
                    var periodFrame = hasPeriodDefinition
                        ? periodDefinitions[entityIndex].Period
                        : 0;
                    var periodStartFrame = hasPeriodRuntime
                        ? periodRuntimes[entityIndex].StartTime
                        : 0;
                    var actionFlags = ActiveEffectStore.CreateLegacyDurationTickActionFlags(
                        durationDefinitions[entityIndex],
                        durationRuntime,
                        appliedDurationEffect,
                        hasPeriodDefinition,
                        periodFrame,
                        periodStartFrame,
                        CurrentFrame);
                    if (actionFlags == (int)EActiveEffectTickActionFlags.None)
                        continue;

                    DurationCandidates.Enqueue(new DurationEffectCandidate
                    {
                        Effect = effect,
                        ActionFlags = actionFlags,
                        EffectIndex = effect.Index,
                        EffectVersion = effect.Version,
                    });
                }
            }

            private static bool IsAppliedDurationEffect(
                Entity effect,
                in CEffectContext context,
                in CDurationRuntime durationRuntime,
                bool hasLifecycle,
                NativeArray<CEffectLifecycle> lifecycles,
                int entityIndex,
                BufferLookup<BGameplayEffect> gameplayEffectLookup)
            {
                if (durationRuntime.Active)
                    return true;

                if (hasLifecycle)
                {
                    var state = lifecycles[entityIndex].State;
                    if (state == EGameplayEffectLifecycleState.Active
                        || state == EGameplayEffectLifecycleState.Inhibited)
                    {
                        return true;
                    }
                }

                if (!gameplayEffectLookup.HasBuffer(context.TargetAsc))
                    return false;

                var activeEffects = gameplayEffectLookup[context.TargetAsc];
                for (var i = 0; i < activeEffects.Length; i++)
                {
                    if (activeEffects[i].GameplayEffect == effect)
                        return true;
                }

                return false;
            }
        }

        [BurstCompile]
        private struct RefreshOwnerLocalChunkSkipIndexJob : IJobChunk
        {
            public int CurrentFrame;
            public ComponentTypeHandle<CActiveEffectStore> ActiveEffectStoreHandle;

            [ReadOnly]
            public BufferTypeHandle<BActiveEffectSlot> ActiveEffectSlotHandle;

            [ReadOnly]
            public EntityTypeHandle EntityHandle;

            public NativeQueue<OwnerLocalDueSlotCandidate>.ParallelWriter DueSlotCandidates;
            public NativeQueue<BGlobalActiveEffectIndex>.ParallelWriter GlobalIndexCandidates;

            [BurstCompile]
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var stores = chunk.GetNativeArray(ref ActiveEffectStoreHandle);
                var slots = chunk.GetBufferAccessorRO(ref ActiveEffectSlotHandle);
                var owners = chunk.GetNativeArray(EntityHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var store = stores[entityIndex];
                    var ownerSlots = slots[entityIndex];
                    ActiveEffectStore.RefreshChunkSkipIndexCounters(
                        ref store,
                        ownerSlots,
                        CurrentFrame);
                    stores[entityIndex] = store;
                    for (var slotIndex = 0; slotIndex < ownerSlots.Length; slotIndex++)
                    {
                        var slot = ownerSlots[slotIndex];
                        if (slot.ActiveEffectEntity != Entity.Null && slot.TargetAsc != Entity.Null)
                            GlobalIndexCandidates.Enqueue(ActiveEffectStore.CreateGlobalIndexEntry(slot, CurrentFrame));

                        var actionFlags = ActiveEffectStore.CreateTickActionFlags(slot, CurrentFrame);
                        if (actionFlags == (int)EActiveEffectTickActionFlags.None)
                            continue;

                        DueSlotCandidates.Enqueue(new OwnerLocalDueSlotCandidate
                        {
                            Owner = owners[entityIndex],
                            Slot = slot,
                            ActionFlags = actionFlags,
                            OwnerIndex = owners[entityIndex].Index,
                            OwnerVersion = owners[entityIndex].Version,
                            SlotSequence = slot.Sequence,
                            SlotIndex = slotIndex,
                            ActiveEffectIndex = slot.ActiveEffectEntity.Index,
                            ActiveEffectVersion = slot.ActiveEffectEntity.Version,
                        });
                    }
                }
            }
        }
    }
}
