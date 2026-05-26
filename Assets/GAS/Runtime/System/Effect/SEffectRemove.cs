using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE Remove 入口。优先消费 CEffectCleanup，并兼容 legacy CEffectDestroy / owner-local pending slot。
    /// </summary>
    [UpdateInGroup(typeof(GASEffectGroup))]
    [UpdateAfter(typeof(SEffectApply))]
    [UpdateBefore(typeof(SEffectFinalDestroy))]
    public partial struct SEffectRemove : ISystem
    {
        private EntityQuery _cleanupQuery;
        private EntityQuery _removeQuery;
        private EntityQuery _activeEffectStoreQuery;
        private BufferTypeHandle<BActiveEffectSlot> _activeEffectSlotHandle;
        private ComponentTypeHandle<CEffectContext> _effectContextHandle;
        private ComponentLookup<CEffectContext> _effectContextLookup;
        private ComponentLookup<CEffectFinalDestroy> _effectFinalDestroyLookup;
        private EntityTypeHandle _entityHandle;

        private const int CleanupShellSourceOrder = 0;
        private const int OwnerLocalPendingRemoveSourceOrder = 1;
        private const int LegacyDestroySourceOrder = 2;

        public void OnCreate(ref SystemState state)
        {
            _cleanupQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectCleanup>()
                .WithNone<CEffectFinalDestroy>()
                .Build();
            _removeQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectDestroy>()
                .WithNone<CEffectCleanup>()
                .WithNone<CEffectFinalDestroy>()
                .Build();
            _activeEffectStoreQuery = SystemAPI.QueryBuilder()
                .WithAll<CActiveEffectStore, BActiveEffectSlot>()
                .Build();
            _activeEffectSlotHandle = state.GetBufferTypeHandle<BActiveEffectSlot>(true);
            _effectContextHandle = state.GetComponentTypeHandle<CEffectContext>(true);
            _effectContextLookup = state.GetComponentLookup<CEffectContext>(true);
            _effectFinalDestroyLookup = state.GetComponentLookup<CEffectFinalDestroy>(true);
            _entityHandle = state.GetEntityTypeHandle();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var currentFrame = SystemAPI.TryGetSingleton<GlobalTimer>(out var timer)
                ? timer.Frame
                : GASRuntimeFrameContext.ResolveCurrentFrame(em);

            var candidateQueue = new NativeQueue<EffectCleanupCandidate>(Allocator.TempJob);
            _entityHandle.Update(ref state);
            _activeEffectSlotHandle.Update(ref state);
            _effectContextHandle.Update(ref state);
            _effectContextLookup.Update(ref state);
            _effectFinalDestroyLookup.Update(ref state);
            var cleanupHandle = new CollectCleanupEffectCandidatesJob
            {
                EntityHandle = _entityHandle,
                EffectContextHandle = _effectContextHandle,
                Candidates = candidateQueue.AsParallelWriter(),
            }.ScheduleParallel(_cleanupQuery, state.Dependency);
            var pendingHandle = new CollectPendingRemoveSlotCandidatesJob
            {
                EntityHandle = _entityHandle,
                ActiveEffectSlotHandle = _activeEffectSlotHandle,
                EffectContextLookup = _effectContextLookup,
                EffectFinalDestroyLookup = _effectFinalDestroyLookup,
                Candidates = candidateQueue.AsParallelWriter(),
            }.ScheduleParallel(_activeEffectStoreQuery, cleanupHandle);
            var legacyHandle = new CollectLegacyDestroyEffectCandidatesJob
            {
                EntityHandle = _entityHandle,
                EffectContextHandle = _effectContextHandle,
                Candidates = candidateQueue.AsParallelWriter(),
            }.ScheduleParallel(_removeQuery, pendingHandle);
            legacyHandle.Complete();

            var cleanupCandidates = new NativeList<EffectCleanupCandidate>(Allocator.Temp);
            while (candidateQueue.TryDequeue(out var candidate))
                cleanupCandidates.Add(candidate);
            candidateQueue.Dispose();

            if (cleanupCandidates.Length == 0)
            {
                cleanupCandidates.Dispose();
                return;
            }

            SortCleanupCandidates(cleanupCandidates);
            var handledEffects = new NativeList<Entity>(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (var i = 0; i < cleanupCandidates.Length; i++)
            {
                var candidate = cleanupCandidates[i];
                var ge = candidate.Effect;
                if (Contains(handledEffects, ge))
                    continue;

                ExecuteCleanupCandidate(em, ref ecb, candidate, currentFrame, ref handledEffects);
            }

            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                currentFrame,
                EGasRuntimeDiagnosticModule.Effect);
            ecb.Dispose();
            handledEffects.Dispose();
            cleanupCandidates.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void SortCleanupCandidates(NativeList<EffectCleanupCandidate> candidates)
        {
            for (var i = 1; i < candidates.Length; i++)
            {
                var current = candidates[i];
                var j = i - 1;
                while (j >= 0 && CompareCleanupCandidates(candidates[j], current) > 0)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }

                candidates[j + 1] = current;
            }
        }

        private static int CompareCleanupCandidates(
            in EffectCleanupCandidate left,
            in EffectCleanupCandidate right)
        {
            var result = left.SourceOrder.CompareTo(right.SourceOrder);
            if (result != 0)
                return result;

            result = left.OwnerIndex.CompareTo(right.OwnerIndex);
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

            result = left.EffectIndex.CompareTo(right.EffectIndex);
            if (result != 0)
                return result;

            return left.EffectVersion.CompareTo(right.EffectVersion);
        }

        private static void ExecuteCleanupCandidate(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            in EffectCleanupCandidate candidate,
            int currentFrame,
            ref NativeList<Entity> handledEffects)
        {
            var ge = candidate.Effect;
            if (!em.Exists(ge))
                return;

            if (HasCleanupAction(candidate.ActionFlags, EEffectCleanupCandidateActionFlags.SkipFinalDestroy))
                return;

            if (HasCleanupAction(candidate.ActionFlags, EEffectCleanupCandidateActionFlags.CleanupActiveEffect))
            {
                EffectRuntimeUtility.CleanupActiveEffect(em, ref ecb, ge, currentFrame);
            }
            else if (HasCleanupAction(candidate.ActionFlags, EEffectCleanupCandidateActionFlags.DestroyEffectEntity))
            {
                EffectRuntimeUtility.DestroyEffectEntity(em, ref ecb, ge);
            }
            else
            {
                return;
            }

            if (!Contains(handledEffects, ge))
                handledEffects.Add(ge);
        }

        private static bool HasCleanupAction(int actionFlags, EEffectCleanupCandidateActionFlags action)
        {
            return (actionFlags & (int)action) != 0;
        }

        private static bool Contains(NativeList<Entity> entities, Entity entity)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                if (entities[i] == entity)
                    return true;
            }

            return false;
        }

        private struct EffectCleanupCandidate
        {
            public Entity Effect;
            public int ActionFlags;
            public int SourceOrder;
            public int OwnerIndex;
            public int OwnerVersion;
            public int SlotSequence;
            public int SlotIndex;
            public int EffectIndex;
            public int EffectVersion;
        }

        [System.Flags]
        private enum EEffectCleanupCandidateActionFlags : int
        {
            None = 0,
            CleanupActiveEffect = 1 << 0,
            DestroyEffectEntity = 1 << 1,
            SkipFinalDestroy = 1 << 2,
        }

        [BurstCompile]
        private struct CollectCleanupEffectCandidatesJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityHandle;

            [ReadOnly]
            public ComponentTypeHandle<CEffectContext> EffectContextHandle;

            public NativeQueue<EffectCleanupCandidate>.ParallelWriter Candidates;

            [BurstCompile]
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var actionFlags = CreateChunkCleanupActionFlags(chunk.Has(ref EffectContextHandle));
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var effect = entities[entityIndex];
                    Candidates.Enqueue(new EffectCleanupCandidate
                    {
                        Effect = effect,
                        ActionFlags = actionFlags,
                        SourceOrder = CleanupShellSourceOrder,
                        EffectIndex = effect.Index,
                        EffectVersion = effect.Version,
                    });
                }
            }
        }

        [BurstCompile]
        private struct CollectPendingRemoveSlotCandidatesJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityHandle;

            [ReadOnly]
            public BufferTypeHandle<BActiveEffectSlot> ActiveEffectSlotHandle;

            [ReadOnly]
            public ComponentLookup<CEffectContext> EffectContextLookup;

            [ReadOnly]
            public ComponentLookup<CEffectFinalDestroy> EffectFinalDestroyLookup;

            public NativeQueue<EffectCleanupCandidate>.ParallelWriter Candidates;

            [BurstCompile]
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var owners = chunk.GetNativeArray(EntityHandle);
                var slots = chunk.GetBufferAccessorRO(ref ActiveEffectSlotHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = owners[entityIndex];
                    var ownerSlots = slots[entityIndex];
                    for (var slotIndex = 0; slotIndex < ownerSlots.Length; slotIndex++)
                    {
                        var slot = ownerSlots[slotIndex];
                        if (slot.State != EActiveEffectSlotState.PendingRemove
                            || slot.ActiveEffectEntity == Entity.Null)
                        {
                            continue;
                        }

                        var actionFlags = CreateLookupCleanupActionFlags(
                            slot.ActiveEffectEntity,
                            EffectContextLookup,
                            EffectFinalDestroyLookup);
                        if (actionFlags == (int)EEffectCleanupCandidateActionFlags.None)
                            continue;

                        Candidates.Enqueue(new EffectCleanupCandidate
                        {
                            Effect = slot.ActiveEffectEntity,
                            ActionFlags = actionFlags,
                            SourceOrder = OwnerLocalPendingRemoveSourceOrder,
                            OwnerIndex = owner.Index,
                            OwnerVersion = owner.Version,
                            SlotSequence = slot.Sequence,
                            SlotIndex = slotIndex,
                            EffectIndex = slot.ActiveEffectEntity.Index,
                            EffectVersion = slot.ActiveEffectEntity.Version,
                        });
                    }
                }
            }
        }

        [BurstCompile]
        private struct CollectLegacyDestroyEffectCandidatesJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityHandle;

            [ReadOnly]
            public ComponentTypeHandle<CEffectContext> EffectContextHandle;

            public NativeQueue<EffectCleanupCandidate>.ParallelWriter Candidates;

            [BurstCompile]
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var actionFlags = CreateChunkCleanupActionFlags(chunk.Has(ref EffectContextHandle));
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var effect = entities[entityIndex];
                    Candidates.Enqueue(new EffectCleanupCandidate
                    {
                        Effect = effect,
                        ActionFlags = actionFlags,
                        SourceOrder = LegacyDestroySourceOrder,
                        EffectIndex = effect.Index,
                        EffectVersion = effect.Version,
                    });
                }
            }
        }

        private static int CreateChunkCleanupActionFlags(bool hasEffectContext)
        {
            return hasEffectContext
                ? (int)EEffectCleanupCandidateActionFlags.CleanupActiveEffect
                : (int)EEffectCleanupCandidateActionFlags.DestroyEffectEntity;
        }

        private static int CreateLookupCleanupActionFlags(
            Entity effect,
            ComponentLookup<CEffectContext> effectContextLookup,
            ComponentLookup<CEffectFinalDestroy> effectFinalDestroyLookup)
        {
            if (!effectContextLookup.EntityExists(effect))
                return (int)EEffectCleanupCandidateActionFlags.None;

            if (effectFinalDestroyLookup.HasComponent(effect))
                return (int)EEffectCleanupCandidateActionFlags.SkipFinalDestroy;

            return effectContextLookup.HasComponent(effect)
                ? (int)EEffectCleanupCandidateActionFlags.CleanupActiveEffect
                : (int)EEffectCleanupCandidateActionFlags.DestroyEffectEntity;
        }
    }
}
