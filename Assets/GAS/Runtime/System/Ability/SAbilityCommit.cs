using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Ability commit gate: validates tag/cost/cooldown/block rules, emits
    /// commit facts, and creates cost/cooldown/activation GameplayEffect requests.
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(STryActivateAbility))]
    [UpdateBefore(typeof(SAbilityTimelineAction))]
    [BurstCompile]
    public partial struct SAbilityCommit : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAbilityCommitRequest, CAbilityBaseInfo, CAbilityRuntimeState, CAbilityConfig>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var abilities = _query.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);

            foreach (var ability in abilities)
            {
                var baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
                var runtime = em.GetComponentData<CAbilityRuntimeState>(ability);
                var configRef = em.GetComponentData<CAbilityConfig>(ability).Config;

                var commit = configRef.IsCreated
                    ? TryCommitAbility(ability, baseInfo.Owner, runtime, ref configRef.Value, em)
                    : CommitFailure(AbilityActivationResult.FailOtherReason);

                EnqueueAbilityCommitFact(em, ability, baseInfo, commit);
                if (commit.Result == AbilityActivationResult.FailBlockedByActiveAbility)
                {
                    EnqueueAbilityBlockedByActiveAbilityFact(em, ability, baseInfo, commit.RelatedAbilityCode);
                }

                if (commit.Result == AbilityActivationResult.Success)
                {
                    ApplyActivationOwnedTags(ability, baseInfo.Owner, ref configRef.Value, em);
                    CancelMatchedAbilities(ability, baseInfo, ref configRef.Value, em);
                    AbilityRuntimeActions.RequestCostGameplayEffect(ability, em);
                    AbilityRuntimeActions.RequestCooldownGameplayEffect(ability, em);
                    ExecuteActivationEffects(ability, baseInfo.Owner, em);
                    ExecuteTargetActivationEffects(ability, baseInfo, em);

                    if (!em.HasComponent<CAbilityActive>(ability))
                        ecb.AddComponent<CAbilityActive>(ability);

                    runtime.Phase = EAbilityPhase.Active;
                    runtime.Timer = 0f;
                    runtime.RemainingFrame = -1;
                    em.SetComponentData(ability, runtime);

                    if (em.HasComponent<CAbilityAutoEndOnCommit>(ability))
                    {
                        AbilityRuntimeActions.RequestAbilityEnd(
                            ability,
                            em,
                            EAbilityLifecycleReason.TimelineCompleted,
                            sourceAbility: ability,
                            sourceAbilityCode: baseInfo.Code);
                    }
                }

                if (em.HasComponent<CAbilityCommitRequest>(ability))
                    ecb.RemoveComponent<CAbilityCommitRequest>(ability);
            }

            ecb.Playback(em);
            GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback(
                em,
                GasRuntimeDebugger.ResolveCurrentFrame(em),
                EGasRuntimeDiagnosticModule.Ability);
            ecb.Dispose();
            abilities.Dispose();
        }

        private static AbilityCommitEvaluation TryCommitAbility(
            Entity ability,
            Entity owner,
            CAbilityRuntimeState runtime,
            ref BlobAbilityConfig config,
            EntityManager em)
        {
            if (runtime.Phase == EAbilityPhase.Activating || runtime.Phase == EAbilityPhase.Active)
                return CommitFailure(AbilityActivationResult.FailHasActivated);
            if (!em.HasComponent<CTagMask>(owner) || !em.HasBuffer<BAttribute>(owner))
                return CommitFailure(AbilityActivationResult.FailOtherReason);

            var ownerTags = em.GetComponentData<CTagMask>(owner);
            var requiredEvaluation = TagRequirementEvaluator.EvaluateRequired(
                ownerTags,
                config.ActivationRequiredTags,
                owner);
            if (!requiredEvaluation.Passed)
            {
                return CommitFailure(
                    AbilityActivationResult.FailActivationRequiredTags,
                    tagFailure: requiredEvaluation.Failure);
            }

            var blockedEvaluation = TagRequirementEvaluator.EvaluateBlocked(
                ownerTags,
                config.ActivationBlockedTags,
                owner);
            if (!blockedEvaluation.Passed)
            {
                return CommitFailure(
                    AbilityActivationResult.FailActivationBlockedTags,
                    tagFailure: blockedEvaluation.Failure);
            }

            if (ownerTags.HasAnyTag(config.CooldownTags))
                return CommitFailure(AbilityActivationResult.FailCooldown);
            if (!HasEnoughCost(owner, ref config.CostModifiers, em))
                return CommitFailure(AbilityActivationResult.FailCost);
            if (IsBlockedByActiveAbility(
                    ability,
                    owner,
                    config.AssetTags,
                    em,
                    out var blockerAbility,
                    out var blockerAbilityCode))
            {
                return CommitFailure(
                    AbilityActivationResult.FailBlockedByActiveAbility,
                    blockerAbility,
                    blockerAbilityCode);
            }

            return CommitSuccess();
        }

        private static bool HasEnoughCost(
            Entity owner,
            ref BlobArray<BlobAbilityCostModifier> costModifiers,
            EntityManager em)
        {
            if (costModifiers.Length == 0)
                return true;

            var attributes = em.GetBuffer<BAttribute>(owner);
            for (var i = 0; i < costModifiers.Length; i++)
            {
                var modifier = costModifiers[i];
                var currentValue = 0f;
                var found = false;

                for (var j = 0; j < attributes.Length; j++)
                {
                    if (attributes[j].AttrSetCode != modifier.AttrSetCode
                        || attributes[j].Code != modifier.AttributeCode)
                    {
                        continue;
                    }

                    currentValue = attributes[j].CurrentValue;
                    found = true;
                    break;
                }

                if (!found)
                    return false;

                var afterCost = AttributeHelper.ApplyModifier(currentValue, modifier.Op, modifier.Magnitude);
                if (afterCost < 0f)
                    return false;
            }

            return true;
        }

        private static bool IsBlockedByActiveAbility(
            Entity ability,
            Entity owner,
            in CTagMask abilityAssetTags,
            EntityManager em,
            out Entity blockerAbility,
            out int blockerAbilityCode)
        {
            blockerAbility = Entity.Null;
            blockerAbilityCode = 0;

            if (!em.HasBuffer<BGrantedAbility>(owner))
                return false;

            var grantedAbilities = em.GetBuffer<BGrantedAbility>(owner);
            foreach (var grantedAbility in grantedAbilities)
            {
                var other = grantedAbility.AbilityEntity;
                if (other == ability || !em.Exists(other))
                    continue;
                if (!em.HasComponent<CAbilityRuntimeState>(other) || !em.HasComponent<CAbilityConfig>(other))
                    continue;

                var otherRuntime = em.GetComponentData<CAbilityRuntimeState>(other);
                if (otherRuntime.Phase != EAbilityPhase.Active && otherRuntime.Phase != EAbilityPhase.Activating)
                    continue;

                var otherConfig = em.GetComponentData<CAbilityConfig>(other).Config;
                if (!otherConfig.IsCreated)
                    continue;

                ref var otherBlob = ref otherConfig.Value;
                if (abilityAssetTags.HasAnyTag(otherBlob.BlockAbilityTags))
                {
                    blockerAbility = other;
                    blockerAbilityCode = GetAbilityCode(other, em);
                    return true;
                }
            }

            return false;
        }

        private static void ApplyActivationOwnedTags(
            Entity ability,
            Entity owner,
            ref BlobAbilityConfig config,
            EntityManager em)
        {
            var tags = em.GetComponentData<CTagMask>(owner);
            var hasTempSources = em.HasBuffer<BTempTagSource>(owner);
            var tempSources = hasTempSources ? em.GetBuffer<BTempTagSource>(owner) : default;
            for (var tagIndex = 0; tagIndex < CTagMask.Capacity; tagIndex++)
            {
                if (!config.ActivationOwnedTags.HasTag(tagIndex))
                    continue;

                tags.AddTag(tagIndex);
                if (hasTempSources)
                {
                    tempSources.Add(new BTempTagSource
                    {
                        TagIndex = tagIndex,
                        Source = ability,
                    });
                }
            }

            em.SetComponentData(owner, tags);
        }

        private static void ExecuteActivationEffects(Entity ability, Entity owner, EntityManager em)
        {
            if (!em.HasBuffer<BAbilityEffectOnActivate>(ability))
                return;

            using var effects = CopyActivationEffects(em, ability);
            for (var i = 0; i < effects.Length; i++)
            {
                var effectCode = effects[i].EffectCode;
                if (effectCode <= 0)
                    continue;

                var requestData = new CApplyGameplayEffectRequest
                {
                    SourceAsc = owner,
                    SourceAbility = ability,
                    Instigator = owner,
                    Causer = ability,
                    GameplayEffectCode = effectCode,
                    Level = em.GetComponentData<CAbilityBaseInfo>(ability).Level,
                };
                GameplayEffectRequestWriter.AppendSimpleInstantCommandOrCreateSingleTargetRequest(
                    em,
                    requestData,
                    owner,
                    ETargetDataKind.Self);
            }
        }

        private static void ExecuteTargetActivationEffects(
            Entity ability,
            in CAbilityBaseInfo baseInfo,
            EntityManager em)
        {
            if (!em.HasBuffer<BAbilityTargetEffectOnActivate>(ability))
                return;

            var target = ResolveMainTarget(em, ability, baseInfo.Owner);
            if (!IsAvailableAsc(em, target))
                return;

            using var effects = CopyTargetActivationEffects(em, ability);
            for (var i = 0; i < effects.Length; i++)
            {
                var effectCode = effects[i].EffectCode;
                if (effectCode <= 0)
                    continue;

                var targetKind = target == baseInfo.Owner ? ETargetDataKind.Self : ETargetDataKind.Entity;
                var requestData = new CApplyGameplayEffectRequest
                {
                    SourceAsc = baseInfo.Owner,
                    SourceAbility = ability,
                    Instigator = baseInfo.Owner,
                    Causer = ability,
                    GameplayEffectCode = effectCode,
                    Level = baseInfo.Level,
                };
                GameplayEffectRequestWriter.AppendSimpleInstantCommandOrCreateSingleTargetRequest(
                    em,
                    requestData,
                    target,
                    targetKind,
                    "AbilityTargetEffectsOnActivateRequest");
            }
        }

        private static NativeArray<BAbilityEffectOnActivate> CopyActivationEffects(
            EntityManager em,
            Entity ability)
        {
            var source = em.GetBuffer<BAbilityEffectOnActivate>(ability);
            var snapshot = new NativeArray<BAbilityEffectOnActivate>(source.Length, Allocator.Temp);
            for (var i = 0; i < source.Length; i++)
                snapshot[i] = source[i];

            return snapshot;
        }

        private static NativeArray<BAbilityTargetEffectOnActivate> CopyTargetActivationEffects(
            EntityManager em,
            Entity ability)
        {
            var source = em.GetBuffer<BAbilityTargetEffectOnActivate>(ability);
            var snapshot = new NativeArray<BAbilityTargetEffectOnActivate>(source.Length, Allocator.Temp);
            for (var i = 0; i < source.Length; i++)
                snapshot[i] = source[i];

            return snapshot;
        }

        private static Entity ResolveMainTarget(EntityManager em, Entity ability, Entity fallbackTarget)
        {
            if (em.HasComponent<CAbilityMainTarget>(ability))
            {
                var target = em.GetComponentData<CAbilityMainTarget>(ability).TargetAsc;
                if (IsAvailableAsc(em, target))
                    return target;
            }

            return fallbackTarget;
        }

        private static bool IsAvailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && !em.HasComponent<CAscDestroying>(asc);
        }

        private static void CancelMatchedAbilities(
            Entity ability,
            in CAbilityBaseInfo baseInfo,
            ref BlobAbilityConfig config,
            EntityManager em)
        {
            var owner = baseInfo.Owner;
            if (config.CancelAbilityTags.IsEmpty || !em.HasBuffer<BGrantedAbility>(owner))
                return;

            var grantedAbilities = em.GetBuffer<BGrantedAbility>(owner);
            var abilityEntities = new NativeArray<Entity>(grantedAbilities.Length, Allocator.Temp);
            for (var i = 0; i < grantedAbilities.Length; i++)
                abilityEntities[i] = grantedAbilities[i].AbilityEntity;

            for (var i = 0; i < abilityEntities.Length; i++)
            {
                var other = abilityEntities[i];
                if (other == ability || !em.Exists(other))
                    continue;
                if (!em.HasComponent<CAbilityRuntimeState>(other) || !em.HasComponent<CAbilityConfig>(other))
                    continue;

                var otherRuntime = em.GetComponentData<CAbilityRuntimeState>(other);
                if (otherRuntime.Phase != EAbilityPhase.Active && otherRuntime.Phase != EAbilityPhase.Activating)
                    continue;

                var otherConfig = em.GetComponentData<CAbilityConfig>(other).Config;
                if (!otherConfig.IsCreated)
                    continue;

                ref var otherBlob = ref otherConfig.Value;
                if (!otherBlob.AssetTags.HasAnyTag(config.CancelAbilityTags))
                    continue;

                AbilityRuntimeActions.RequestAbilityCancel(
                    other,
                    em,
                    EAbilityLifecycleReason.CancelMatchedAbility,
                    sourceAbility: ability,
                    sourceAbilityCode: baseInfo.Code);
            }

            abilityEntities.Dispose();
        }

        private static int GetAbilityCode(Entity ability, EntityManager em)
        {
            if (em.HasComponent<CAbilityBaseInfo>(ability))
                return em.GetComponentData<CAbilityBaseInfo>(ability).Code;

            if (em.HasComponent<CAbilityConfig>(ability))
            {
                var config = em.GetComponentData<CAbilityConfig>(ability).Config;
                if (config.IsCreated)
                    return config.Value.Code;
            }

            return 0;
        }

        private static void EnqueueAbilityCommitFact(
            EntityManager em,
            Entity ability,
            in CAbilityBaseInfo baseInfo,
            in AbilityCommitEvaluation commit)
        {
            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = commit.Result == AbilityActivationResult.Success
                    ? EGameplayEventType.AbilityCommitSucceeded
                    : EGameplayEventType.AbilityCommitFailed,
                SourceAsc = baseInfo.Owner,
                TargetAsc = baseInfo.Owner,
                SourceAbility = ability,
                RelatedAbility = commit.RelatedAbility,
                EventCode = baseInfo.Code,
                ReasonCode = (int)commit.TagFailure,
                RelatedAbilityCode = commit.RelatedAbilityCode,
                Value = (int)commit.Result,
            });
        }

        private static void EnqueueAbilityBlockedByActiveAbilityFact(
            EntityManager em,
            Entity ability,
            in CAbilityBaseInfo baseInfo,
            int blockerAbilityCode)
        {
            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = EGameplayEventType.AbilityActivationBlockedByAbility,
                SourceAsc = baseInfo.Owner,
                TargetAsc = baseInfo.Owner,
                SourceAbility = ability,
                EventCode = baseInfo.Code,
                Value = blockerAbilityCode,
            });
        }

        private static AbilityCommitEvaluation CommitSuccess()
        {
            return new AbilityCommitEvaluation
            {
                Result = AbilityActivationResult.Success,
            };
        }

        private static AbilityCommitEvaluation CommitFailure(
            AbilityActivationResult result,
            Entity relatedAbility = default,
            int relatedAbilityCode = 0,
            ETagRequirementFailure tagFailure = ETagRequirementFailure.None)
        {
            return new AbilityCommitEvaluation
            {
                Result = result,
                RelatedAbility = relatedAbility,
                RelatedAbilityCode = relatedAbilityCode,
                TagFailure = tagFailure,
            };
        }

        private struct AbilityCommitEvaluation
        {
            public AbilityActivationResult Result;
            public Entity RelatedAbility;
            public int RelatedAbilityCode;
            public ETagRequirementFailure TagFailure;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
