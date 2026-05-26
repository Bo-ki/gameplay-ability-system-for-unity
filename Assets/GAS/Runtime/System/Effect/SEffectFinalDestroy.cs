using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Finalizes cleaned GE entities after semantic cleanup has resolved owner-local state.
    /// </summary>
    [UpdateInGroup(typeof(GASEffectGroup))]
    [UpdateAfter(typeof(SEffectRemove))]
    [UpdateBefore(typeof(SEffectTick))]
    public partial struct SEffectFinalDestroy : ISystem
    {
        private EntityQuery _finalDestroyQuery;
        private EntityTypeHandle _entityHandle;

        public void OnCreate(ref SystemState state)
        {
            _finalDestroyQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectFinalDestroy>()
                .Build();
            _entityHandle = state.GetEntityTypeHandle();
            state.RequireForUpdate(_finalDestroyQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var candidateQueue = new NativeQueue<FinalDestroyCandidate>(Allocator.TempJob);
            _entityHandle.Update(ref state);
            var collectHandle = new CollectFinalDestroyCandidatesJob
            {
                EntityHandle = _entityHandle,
                Candidates = candidateQueue.AsParallelWriter(),
            }.ScheduleParallel(_finalDestroyQuery, state.Dependency);
            collectHandle.Complete();

            var candidates = new NativeList<FinalDestroyCandidate>(Allocator.Temp);
            while (candidateQueue.TryDequeue(out var candidate))
                candidates.Add(candidate);
            candidateQueue.Dispose();

            if (candidates.Length == 0)
            {
                candidates.Dispose();
                return;
            }

            SortFinalDestroyCandidates(candidates);
            var currentFrame = SystemAPI.TryGetSingleton<GlobalTimer>(out var timer)
                ? timer.Frame
                : GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            for (var i = 0; i < candidates.Length; i++)
            {
                var ge = candidates[i].Effect;
                if (!em.Exists(ge) || !em.HasComponent<CEffectFinalDestroy>(ge))
                    continue;

                EffectRuntimeUtility.FinalizeEffectDestroy(em, ref ecb, ge, currentFrame);
            }

            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                currentFrame,
                EGasRuntimeDiagnosticModule.Effect);
            ecb.Dispose();
            candidates.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void SortFinalDestroyCandidates(NativeList<FinalDestroyCandidate> candidates)
        {
            for (var i = 1; i < candidates.Length; i++)
            {
                var current = candidates[i];
                var j = i - 1;
                while (j >= 0 && CompareFinalDestroyCandidates(candidates[j], current) > 0)
                {
                    candidates[j + 1] = candidates[j];
                    j--;
                }

                candidates[j + 1] = current;
            }
        }

        private static int CompareFinalDestroyCandidates(
            in FinalDestroyCandidate left,
            in FinalDestroyCandidate right)
        {
            var result = left.EffectIndex.CompareTo(right.EffectIndex);
            if (result != 0)
                return result;

            return left.EffectVersion.CompareTo(right.EffectVersion);
        }

        private struct FinalDestroyCandidate
        {
            public Entity Effect;
            public int EffectIndex;
            public int EffectVersion;
        }

        [BurstCompile]
        private struct CollectFinalDestroyCandidatesJob : IJobChunk
        {
            [ReadOnly]
            public EntityTypeHandle EntityHandle;

            public NativeQueue<FinalDestroyCandidate>.ParallelWriter Candidates;

            [BurstCompile]
            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var effect = entities[entityIndex];
                    Candidates.Enqueue(new FinalDestroyCandidate
                    {
                        Effect = effect,
                        EffectIndex = effect.Index,
                        EffectVersion = effect.Version,
                    });
                }
            }
        }
    }
}
