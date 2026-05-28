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
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(AbilityTryActivateSystem))]
    [UpdateBefore(typeof(AbilityCommitSystem))]
    [UpdateBefore(typeof(GEEffectCommandIngestSystem))]
    public partial struct AbilityCatalogCommitSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityCommitRequestComponent, AbilityStateComponent>()
                .Build();
            state.RequireForUpdate(_query);
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var abilityChunkCount = _query.CalculateChunkCount();
            if (abilityChunkCount <= 0)
                return;

            var commitRecordStream = new NativeStream(abilityChunkCount, Allocator.TempJob);
            var seeds = new NativeList<GECommandSeedRecord>(Allocator.Temp);
            var commandWriter = EffectCommandSpecStream.BeginCommandWriter(em, frame);
            ref var catalog = ref catalogComponent.Catalog.Value;

            try
            {
                var scanJob = new AbilityActivationCommitScanJob
                {
                    EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                    StateTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityStateComponent>(isReadOnly: true),
                    CommitRecordWriter = commitRecordStream.AsWriter(),
                };
                state.Dependency = scanJob.ScheduleParallel(_query, state.Dependency);
                state.Dependency.Complete();

                var commitRecordReader = commitRecordStream.AsReader();
                for (var streamIndex = 0; streamIndex < commitRecordReader.ForEachCount; streamIndex++)
                {
                    var recordCount = commitRecordReader.BeginForEachIndex(streamIndex);
                    for (var i = 0; i < recordCount; i++)
                    {
                        var commitRecord = commitRecordReader.Read<AbilityActivationCommitRecord>();
                        ApplyAbilityActivationCommitRecord(
                            em,
                            in commitRecord,
                            frame,
                            ref catalog,
                            ref commandWriter,
                            ref seeds);
                    }
                    commitRecordReader.EndForEachIndex();
                }

                commandWriter.Flush();
            }
            finally
            {
                seeds.Dispose();
                commitRecordStream.Dispose();
            }
        }

        private struct AbilityActivationCommitRecord
        {
            public Entity Ability;
            public AbilityStateComponent State;
        }

        private struct AbilityActivationCommitScanJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AbilityStateComponent> StateTypeHandle;
            public NativeStream.Writer CommitRecordWriter;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                CommitRecordWriter.BeginForEachIndex(unfilteredChunkIndex);
                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    CommitRecordWriter.Write(new AbilityActivationCommitRecord
                    {
                        Ability = abilities[entityIndex],
                        State = states[entityIndex],
                    });
                }
                CommitRecordWriter.EndForEachIndex();
            }
        }

        private static void ApplyAbilityActivationCommitRecord(
            EntityManager em,
            in AbilityActivationCommitRecord commitRecord,
            int frame,
            ref GASDefinitionCatalogBlob catalog,
            ref EffectCommandSpecStream.CommandWriter commandWriter,
            ref NativeList<GECommandSeedRecord> seeds)
        {
            var ability = commitRecord.Ability;
            if (!em.Exists(ability))
                return;

            var state = commitRecord.State;
            var target = ResolveMainTarget(em, ability, state.Owner);
            seeds.Clear();

            var committed = TryCommitAbility(
                em,
                ability,
                state,
                target,
                frame,
                ref catalog,
                ref commandWriter,
                ref seeds,
                out var nextRuntime);

            if (committed)
            {
                em.SetComponentData(ability, nextRuntime);
                if (em.HasComponent<AbilityAutoEndOnCommitComponent>(ability)
                    && em.IsComponentEnabled<AbilityAutoEndOnCommitComponent>(ability))
                {
                    AbilityRuntimeActions.RequestAbilityEnd(
                        ability,
                        em,
                        EAbilityLifecycleReason.ActivationCompleted,
                        sourceAbility: ability,
                        sourceAbilityCode: state.Code);
                }
            }

            if (em.HasComponent<AbilityCommitRequestComponent>(ability))
                em.SetComponentEnabled<AbilityCommitRequestComponent>(ability, false);
        }

        private static bool TryCommitAbility(
            EntityManager em,
            Entity ability,
            in AbilityStateComponent state,
            Entity target,
            int frame,
            ref GASDefinitionCatalogBlob catalog,
            ref EffectCommandSpecStream.CommandWriter commandWriter,
            ref NativeList<GECommandSeedRecord> seeds,
            out AbilityStateComponent nextRuntime)
        {
            nextRuntime = state;
            if (state.Phase == EAbilityPhase.Activating || state.Phase == EAbilityPhase.Active)
                return false;

            if (!GASGeneratedRuntimeDefinitionResolver.TryBuildAbilityActivationPlan(
                ref catalog,
                state.Code,
                state.Owner,
                target,
                ability,
                frame,
                state.Level,
                out var plan))
                return false;

            var ownerTags = em.HasComponent<TagMaskComponent>(state.Owner)
                ? em.GetComponentData<TagMaskComponent>(state.Owner)
                : default;
            if (!GASGeneratedRequirementEvaluator.EvaluateAbilityRequirements(
                ref catalog,
                plan.AbilityDefinitionIndex,
                in ownerTags,
                out _))
                return false;

            ref readonly var abilityDefinition = ref GASGeneratedDefinitionCatalogLookup.GetAbility(ref catalog, plan.AbilityDefinitionIndex);
            ApplyActivationOwnedTags(em, ability, state.Owner, ref catalog, in abilityDefinition);

            GASGeneratedRuntimeDefinitionResolver.WriteGECommandSeeds(
                ref catalog,
                in plan,
                contextId: 0,
                parentContextId: 0,
                ref seeds);
            for (var i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (seed.FailureReasonCode != GASFailureReasonCodes.None)
                    continue;
                commandWriter.AppendCommand(ToEffectCommand(in seed));
            }

            nextRuntime.Phase = EAbilityPhase.Active;
            nextRuntime.Timer = 0f;
            nextRuntime.RemainingFrame = -1;
            return true;
        }

        private static GEEffectCommandBuffer ToEffectCommand(in GECommandSeedRecord seed)
        {
            var isActiveMutation = (seed.Flags & GASGECommandSeedFlags.ActiveMutation) != 0;
            return new GEEffectCommandBuffer
            {
                Frame = seed.Frame,
                Kind = isActiveMutation ? GEEffectCommandKind.ActiveMutation : GEEffectCommandKind.Instant,
                Source = seed.Source,
                SourceAsc = seed.SourceAsc,
                TargetAsc = seed.TargetAsc,
                SourceAbility = seed.SourceAbility,
                SourceEffect = seed.SourceEffect,
                Instigator = seed.SourceAsc,
                Causer = seed.SourceAbility,
                GameplayEffectCode = seed.GameplayEffectCode,
                Level = seed.Level,
                DurationFrameOverride = seed.DurationFrameOverride,
                ContextId = seed.ContextId,
                ParentContextId = seed.ParentContextId,
                TargetDataKind = seed.TargetAsc == seed.SourceAsc ? ETargetDataKind.Self : ETargetDataKind.Entity,
                Flags = seed.Flags,
            };
        }

        private static void ApplyActivationOwnedTags(
            EntityManager em,
            Entity ability,
            Entity owner,
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogAbilityDefinitionBlob abilityDefinition)
        {
            var tagMaskIndex = abilityDefinition.ActivationOwnedTagMaskIndex;
            if (tagMaskIndex < 0 || tagMaskIndex >= catalog.TagMasks.Length)
                return;
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasComponent<TagMaskComponent>(owner))
                return;

            var ownedMask = catalog.TagMasks[tagMaskIndex].Mask;
            var ownerTags = em.GetComponentData<TagMaskComponent>(owner);
            ownerTags.Mask0 |= ownedMask.Mask0;
            ownerTags.Mask1 |= ownedMask.Mask1;
            ownerTags.Mask2 |= ownedMask.Mask2;
            ownerTags.Mask3 |= ownedMask.Mask3;
            em.SetComponentData(owner, ownerTags);

            if (!em.HasBuffer<TagTemporarySourceBuffer>(owner))
                return;

            var tempSources = em.GetBuffer<TagTemporarySourceBuffer>(owner);
            for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
            {
                if (!ownedMask.HasTag(tagIndex))
                    continue;
                tempSources.Add(new TagTemporarySourceBuffer
                {
                    TagIndex = tagIndex,
                    Source = ability,
                });
            }
        }

        private static Entity ResolveMainTarget(EntityManager em, Entity ability, Entity fallbackTarget)
        {
            if (em.HasComponent<AbilityMainTargetComponent>(ability))
            {
                var target = em.GetComponentData<AbilityMainTargetComponent>(ability).TargetAsc;
                if (IsAvailableAsc(em, target))
                    return target;
            }
            return fallbackTarget;
        }

        private static bool IsAvailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                && em.Exists(asc)
                && !ASCEntityFactory.IsDestroying(em, asc);
        }
    }
}
