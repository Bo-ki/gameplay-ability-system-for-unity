using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    [UpdateAfter(typeof(CueDestroySystem))]
    public partial struct ASCDestroyFinalizeSystem : ISystem
    {
        private EntityQuery _ascQuery;
        private EntityQuery _effectQuery;
        private EntityQuery _abilityQuery;

        public void OnCreate(ref SystemState state)
        {
            _ascQuery = SystemAPI.QueryBuilder()
                .WithAll<ASCDestroyingComponent>()
                .Build();
            _effectQuery = SystemAPI.QueryBuilder()
                .WithAll<GEContextComponent>()
                .Build();
            _abilityQuery = SystemAPI.QueryBuilder()
                .WithAll<AbilityStateComponent>()
                .Build();
            state.RequireForUpdate(_ascQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var ascChunkCount = _ascQuery.CalculateChunkCount();
            if (ascChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var ascRecordStream = new NativeStream(ascChunkCount, Allocator.TempJob);
            var ascRecords = new NativeList<ASCDestroyFinalizeRecord>(Allocator.Temp);
            var effectReferenceRecords = new NativeList<ASCReferencingEffectRecord>(Allocator.Temp);
            var abilityOwnerRecords = new NativeList<ASCOwnedAbilityRecord>(Allocator.Temp);
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            try
            {
                var entityTypeHandle = SystemAPI.GetEntityTypeHandle();
                ScanDestroyingASCs(ref state, ascRecordStream, entityTypeHandle);
                ReadDestroyingASCs(ascRecordStream, ascRecords);
                ScanReferencingEffects(
                    ref state,
                    effectReferenceRecords,
                    entityTypeHandle,
                    SystemAPI.GetComponentTypeHandle<GEContextComponent>(isReadOnly: true));
                ScanAbilityOwners(
                    ref state,
                    abilityOwnerRecords,
                    entityTypeHandle,
                    SystemAPI.GetComponentTypeHandle<AbilityStateComponent>(isReadOnly: true));

                for (var i = 0; i < ascRecords.Length; i++)
                {
                    var asc = ascRecords[i].ASC;
                    if (!em.Exists(asc))
                        continue;

                    if (HasOwnedTargetEffect(em, asc)
                        || HasReferencingEffect(asc, effectReferenceRecords)
                        || HasOwnedAbility(em, asc, abilityOwnerRecords))
                    {
                        continue;
                    }

                    EntityHelper.UnbindGameObjectToEntity(asc);
                    ecb.DestroyEntity(asc);
                }
            }
            finally
            {
                abilityOwnerRecords.Dispose();
                effectReferenceRecords.Dispose();
                ascRecords.Dispose();
                ascRecordStream.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private struct ASCDestroyFinalizeRecord
        {
            public Entity ASC;
        }

        private struct ASCReferencingEffectRecord
        {
            public Entity Effect;
            public Entity SourceAsc;
            public Entity TargetAsc;
        }

        private struct ASCOwnedAbilityRecord
        {
            public Entity Ability;
            public Entity Owner;
        }

        private void ScanDestroyingASCs(
            ref SystemState state,
            NativeStream ascRecordStream,
            EntityTypeHandle entityTypeHandle)
        {
            var scanJob = new ASCDestroyFinalizeScanJob
            {
                EntityTypeHandle = entityTypeHandle,
                ASCRecordWriter = ascRecordStream.AsWriter(),
            };
            state.Dependency = scanJob.ScheduleParallel(_ascQuery, state.Dependency);
            state.Dependency.Complete();
        }

        private static void ReadDestroyingASCs(
            NativeStream ascRecordStream,
            NativeList<ASCDestroyFinalizeRecord> ascRecords)
        {
            var ascRecordReader = ascRecordStream.AsReader();
            for (var streamIndex = 0; streamIndex < ascRecordReader.ForEachCount; streamIndex++)
            {
                var recordCount = ascRecordReader.BeginForEachIndex(streamIndex);
                for (var i = 0; i < recordCount; i++)
                    ascRecords.Add(ascRecordReader.Read<ASCDestroyFinalizeRecord>());
                ascRecordReader.EndForEachIndex();
            }
        }

        private void ScanReferencingEffects(
            ref SystemState state,
            NativeList<ASCReferencingEffectRecord> effectReferenceRecords,
            EntityTypeHandle entityTypeHandle,
            ComponentTypeHandle<GEContextComponent> effectContextTypeHandle)
        {
            var effectChunkCount = _effectQuery.CalculateChunkCount();
            if (effectChunkCount <= 0)
                return;

            var effectRecordStream = new NativeStream(effectChunkCount, Allocator.TempJob);
            try
            {
                var scanJob = new ASCReferencingEffectScanJob
                {
                    EntityTypeHandle = entityTypeHandle,
                    EffectContextTypeHandle = effectContextTypeHandle,
                    EffectRecordWriter = effectRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_effectQuery, state.Dependency);
                state.Dependency.Complete();

                var effectRecordReader = effectRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < effectRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = effectRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                        effectReferenceRecords.Add(effectRecordReader.Read<ASCReferencingEffectRecord>());
                    effectRecordReader.EndForEachIndex();
                }
            }
            finally
            {
                effectRecordStream.Dispose();
            }
        }

        private void ScanAbilityOwners(
            ref SystemState state,
            NativeList<ASCOwnedAbilityRecord> abilityOwnerRecords,
            EntityTypeHandle entityTypeHandle,
            ComponentTypeHandle<AbilityStateComponent> abilityStateTypeHandle)
        {
            var abilityChunkCount = _abilityQuery.CalculateChunkCount();
            if (abilityChunkCount <= 0)
                return;

            var abilityRecordStream = new NativeStream(abilityChunkCount, Allocator.TempJob);
            try
            {
                var scanJob = new ASCOwnedAbilityScanJob
                {
                    EntityTypeHandle = entityTypeHandle,
                    AbilityStateTypeHandle = abilityStateTypeHandle,
                    AbilityRecordWriter = abilityRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_abilityQuery, state.Dependency);
                state.Dependency.Complete();

                var abilityRecordReader = abilityRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < abilityRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = abilityRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                        abilityOwnerRecords.Add(abilityRecordReader.Read<ASCOwnedAbilityRecord>());
                    abilityRecordReader.EndForEachIndex();
                }
            }
            finally
            {
                abilityRecordStream.Dispose();
            }
        }

        private struct ASCDestroyFinalizeScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public NativeStream.Writer ASCRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                ASCRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var ascs = chunk.GetNativeArray(EntityTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    ASCRecordWriter.Write(new ASCDestroyFinalizeRecord
                    {
                        ASC = ascs[entityIndex],
                    });
                }
                ASCRecordWriter.EndForEachIndex();
            }
        }

        private struct ASCReferencingEffectScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<GEContextComponent> EffectContextTypeHandle;
            public NativeStream.Writer EffectRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                EffectRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var effects = chunk.GetNativeArray(EntityTypeHandle);
                var contexts = chunk.GetNativeArray(ref EffectContextTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var context = contexts[entityIndex];
                    EffectRecordWriter.Write(new ASCReferencingEffectRecord
                    {
                        Effect = effects[entityIndex],
                        SourceAsc = context.SourceAsc,
                        TargetAsc = context.TargetAsc,
                    });
                }
                EffectRecordWriter.EndForEachIndex();
            }
        }

        private struct ASCOwnedAbilityScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AbilityStateComponent> AbilityStateTypeHandle;
            public NativeStream.Writer AbilityRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                AbilityRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref AbilityStateTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    AbilityRecordWriter.Write(new ASCOwnedAbilityRecord
                    {
                        Ability = abilities[entityIndex],
                        Owner = states[entityIndex].Owner,
                    });
                }
                AbilityRecordWriter.EndForEachIndex();
            }
        }

        private static bool HasOwnedTargetEffect(EntityManager em, Entity asc)
        {
            if (!em.HasBuffer<LegacyGameplayEffectEntityBuffer>(asc))
                return false;

            var activeEffects = em.GetBuffer<LegacyGameplayEffectEntityBuffer>(asc);
            for (var i = 0; i < activeEffects.Length; i++)
            {
                var effect = activeEffects[i].GameplayEffect;
                if (effect != Entity.Null && em.Exists(effect))
                    return true;
            }

            return false;
        }

        private static bool HasReferencingEffect(
            Entity asc,
            NativeList<ASCReferencingEffectRecord> effectReferenceRecords)
        {
            for (var i = 0; i < effectReferenceRecords.Length; i++)
            {
                var effectReference = effectReferenceRecords[i];
                if (effectReference.SourceAsc == asc || effectReference.TargetAsc == asc)
                    return true;
            }

            return false;
        }

        private static bool HasOwnedAbility(
            EntityManager em,
            Entity asc,
            NativeList<ASCOwnedAbilityRecord> abilityOwnerRecords)
        {
            if (em.HasBuffer<AbilitySlotBuffer>(asc))
            {
                var grantedAbilities = em.GetBuffer<AbilitySlotBuffer>(asc);
                for (var i = 0; i < grantedAbilities.Length; i++)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (ability != Entity.Null && em.Exists(ability))
                        return true;
                }
            }

            for (var i = 0; i < abilityOwnerRecords.Length; i++)
            {
                var abilityOwner = abilityOwnerRecords[i];
                if (abilityOwner.Ability != Entity.Null && abilityOwner.Owner == asc)
                    return true;
            }

            return false;
        }
    }
}
