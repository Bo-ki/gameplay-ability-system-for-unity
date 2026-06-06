using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    [DisableAutoCreation]
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
            var ascChunkCount = _ascQuery.CalculateChunkCountWithoutFiltering();
            if (ascChunkCount <= 0)
                return;

            var ascRecordStream = new NativeStream(ascChunkCount, Allocator.TempJob);
            var effectChunkCount = _effectQuery.CalculateChunkCountWithoutFiltering();
            var effectRecordStream = new NativeStream(
                effectChunkCount > 0 ? effectChunkCount : 1,
                Allocator.TempJob);
            var abilityChunkCount = _abilityQuery.CalculateChunkCountWithoutFiltering();
            var abilityRecordStream = new NativeStream(
                abilityChunkCount > 0 ? abilityChunkCount : 1,
                Allocator.TempJob);
            var ascRecords = new NativeList<ASCDestroyFinalizeRecord>(Allocator.TempJob);
            var effectReferenceRecords = new NativeList<ASCReferencingEffectRecord>(Allocator.TempJob);
            var abilityOwnerRecords = new NativeList<ASCOwnedAbilityRecord>(Allocator.TempJob);
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var entityTypeHandle = SystemAPI.GetEntityTypeHandle();
            var originalDependency = state.Dependency;
            var ascScanHandle = ScheduleDestroyingASCScan(
                ascRecordStream,
                entityTypeHandle,
                originalDependency);
            var effectScanHandle = ScheduleReferencingEffectScan(
                effectChunkCount,
                effectRecordStream,
                entityTypeHandle,
                SystemAPI.GetComponentTypeHandle<GEContextComponent>(isReadOnly: true),
                originalDependency);
            var abilityScanHandle = ScheduleAbilityOwnerScan(
                abilityChunkCount,
                abilityRecordStream,
                entityTypeHandle,
                SystemAPI.GetComponentTypeHandle<AbilityStateComponent>(isReadOnly: true),
                originalDependency);
            var scanHandle = JobHandle.CombineDependencies(
                ascScanHandle,
                effectScanHandle,
                abilityScanHandle);
            var applyJob = new ASCDestroyFinalizeApplyJob
            {
                ASCRecordStream = ascRecordStream,
                EffectRecordStream = effectRecordStream,
                AbilityRecordStream = abilityRecordStream,
                ASCRecords = ascRecords,
                EffectReferenceRecords = effectReferenceRecords,
                AbilityOwnerRecords = abilityOwnerRecords,
                ActiveEffectLookup = SystemAPI.GetBufferLookup<LegacyGameplayEffectEntityBuffer>(isReadOnly: true),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: true),
                EffectLifecycleLookup = SystemAPI.GetComponentLookup<GEEffectLifecycleComponent>(isReadOnly: true),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: true),
                Ecb = ecb,
            };
            var applyHandle = applyJob.Schedule(scanHandle);
            var disposeHandle = ascRecordStream.Dispose(applyHandle);
            disposeHandle = JobHandle.CombineDependencies(disposeHandle, effectRecordStream.Dispose(applyHandle));
            disposeHandle = JobHandle.CombineDependencies(disposeHandle, abilityRecordStream.Dispose(applyHandle));

            var listDisposeHandle = JobHandle.CombineDependencies(
                ascRecords.Dispose(applyHandle),
                effectReferenceRecords.Dispose(applyHandle),
                abilityOwnerRecords.Dispose(applyHandle));
            state.Dependency = JobHandle.CombineDependencies(disposeHandle, listDisposeHandle);
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

        private JobHandle ScheduleDestroyingASCScan(
            NativeStream ascRecordStream,
            EntityTypeHandle entityTypeHandle,
            JobHandle dependency)
        {
            var scanJob = new ASCDestroyFinalizeScanJob
            {
                EntityTypeHandle = entityTypeHandle,
                ASCRecordWriter = ascRecordStream.AsWriter(),
            };
            return scanJob.ScheduleParallel(_ascQuery, dependency);
        }

        private JobHandle ScheduleReferencingEffectScan(
            int effectChunkCount,
            NativeStream effectRecordStream,
            EntityTypeHandle entityTypeHandle,
            ComponentTypeHandle<GEContextComponent> effectContextTypeHandle,
            JobHandle dependency)
        {
            if (effectChunkCount <= 0)
                return dependency;

            var scanJob = new ASCReferencingEffectScanJob
            {
                EntityTypeHandle = entityTypeHandle,
                EffectContextTypeHandle = effectContextTypeHandle,
                EffectRecordWriter = effectRecordStream.AsWriter(),
            };
            return scanJob.ScheduleParallel(_effectQuery, dependency);
        }

        private JobHandle ScheduleAbilityOwnerScan(
            int abilityChunkCount,
            NativeStream abilityRecordStream,
            EntityTypeHandle entityTypeHandle,
            ComponentTypeHandle<AbilityStateComponent> abilityStateTypeHandle,
            JobHandle dependency)
        {
            if (abilityChunkCount <= 0)
                return dependency;

            var scanJob = new ASCOwnedAbilityScanJob
            {
                EntityTypeHandle = entityTypeHandle,
                AbilityStateTypeHandle = abilityStateTypeHandle,
                AbilityRecordWriter = abilityRecordStream.AsWriter(),
            };
            return scanJob.ScheduleParallel(_abilityQuery, dependency);
        }

        [BurstCompile]
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

        [BurstCompile]
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

        [BurstCompile]
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

        [BurstCompile]
        private struct ASCDestroyFinalizeApplyJob : IJob
        {
            public NativeStream ASCRecordStream;
            public NativeStream EffectRecordStream;
            public NativeStream AbilityRecordStream;
            public NativeList<ASCDestroyFinalizeRecord> ASCRecords;
            public NativeList<ASCReferencingEffectRecord> EffectReferenceRecords;
            public NativeList<ASCOwnedAbilityRecord> AbilityOwnerRecords;
            [ReadOnly] public BufferLookup<LegacyGameplayEffectEntityBuffer> ActiveEffectLookup;
            [ReadOnly] public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;
            [ReadOnly] public ComponentLookup<GEEffectLifecycleComponent> EffectLifecycleLookup;
            [ReadOnly] public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            public EntityCommandBuffer Ecb;

            public void Execute()
            {
                ReadDestroyingASCs();
                ReadReferencingEffects();
                ReadAbilityOwners();

                for (var i = 0; i < ASCRecords.Length; i++)
                {
                    var asc = ASCRecords[i].ASC;
                    if (asc == Entity.Null)
                        continue;

                    if (HasOwnedTargetEffect(asc)
                        || HasReferencingEffect(asc)
                        || HasOwnedAbility(asc))
                    {
                        continue;
                    }

                    Ecb.DestroyEntity(asc);
                }
            }

            private void ReadDestroyingASCs()
            {
                var ascRecordReader = ASCRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < ascRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = ascRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                        ASCRecords.Add(ascRecordReader.Read<ASCDestroyFinalizeRecord>());
                    ascRecordReader.EndForEachIndex();
                }
            }

            private void ReadReferencingEffects()
            {
                if (!EffectRecordStream.IsCreated)
                    return;

                var effectRecordReader = EffectRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < effectRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = effectRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                        EffectReferenceRecords.Add(effectRecordReader.Read<ASCReferencingEffectRecord>());
                    effectRecordReader.EndForEachIndex();
                }
            }

            private void ReadAbilityOwners()
            {
                if (!AbilityRecordStream.IsCreated)
                    return;

                var abilityRecordReader = AbilityRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < abilityRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = abilityRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                        AbilityOwnerRecords.Add(abilityRecordReader.Read<ASCOwnedAbilityRecord>());
                    abilityRecordReader.EndForEachIndex();
                }
            }

            private bool HasOwnedTargetEffect(Entity asc)
            {
                if (!ActiveEffectLookup.HasBuffer(asc))
                    return false;

                var activeEffects = ActiveEffectLookup[asc];
                for (var i = 0; i < activeEffects.Length; i++)
                {
                    var effect = activeEffects[i].GameplayEffect;
                    if (effect != Entity.Null && EffectLifecycleLookup.HasComponent(effect))
                        return true;
                }

                return false;
            }

            private bool HasReferencingEffect(Entity asc)
            {
                for (var i = 0; i < EffectReferenceRecords.Length; i++)
                {
                    var effectReference = EffectReferenceRecords[i];
                    if (effectReference.SourceAsc == asc || effectReference.TargetAsc == asc)
                        return true;
                }

                return false;
            }

            private bool HasOwnedAbility(Entity asc)
            {
                if (AbilitySlotLookup.HasBuffer(asc))
                {
                    var grantedAbilities = AbilitySlotLookup[asc];
                    for (var i = 0; i < grantedAbilities.Length; i++)
                    {
                        var ability = grantedAbilities[i].AbilityEntity;
                        if (ability != Entity.Null && AbilityStateLookup.HasComponent(ability))
                            return true;
                    }
                }

                for (var i = 0; i < AbilityOwnerRecords.Length; i++)
                {
                    var abilityOwner = AbilityOwnerRecords[i];
                    if (abilityOwner.Ability != Entity.Null
                        && AbilityStateLookup.HasComponent(abilityOwner.Ability)
                        && abilityOwner.Owner == asc)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
