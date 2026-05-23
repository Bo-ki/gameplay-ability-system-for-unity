using System;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    internal static class EffectRuntimeUtility
    {
        public static void PlaybackAndReset(ref EntityCommandBuffer ecb, EntityManager em)
        {
            ecb.Playback(em);
            ecb.Dispose();
            ecb = new EntityCommandBuffer(Allocator.Temp);
        }

        public static void EnsureLifecycle(
            EntityManager em,
            Entity ge,
            EGameplayEffectLifecycleState state,
            int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            EnsureLifecycle(em, ref ecb, ge, state, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void EnsureLifecycle(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            EGameplayEffectLifecycleState state,
            int currentFrame)
        {
            if (em.HasComponent<CEffectLifecycle>(ge))
                return;

            ecb.AddComponent(ge, new CEffectLifecycle
            {
                State = state,
                PreviousState = state,
                StateStartFrame = currentFrame,
            });
        }

        public static bool IsPendingApply(EntityManager em, Entity ge)
        {
            return ResolveLifecycleState(em, ge) == EGameplayEffectLifecycleState.PendingApply;
        }

        public static bool IsActive(EntityManager em, Entity ge)
        {
            return ResolveLifecycleState(em, ge) == EGameplayEffectLifecycleState.Active;
        }

        private static bool IsApplied(EntityManager em, Entity ge)
        {
            var state = ResolveLifecycleState(em, ge);
            return state == EGameplayEffectLifecycleState.Active
                   || state == EGameplayEffectLifecycleState.Inhibited;
        }

        private static EGameplayEffectLifecycleState ResolveLifecycleState(EntityManager em, Entity ge)
        {
            if (em.HasComponent<CEffectLifecycle>(ge))
                return em.GetComponentData<CEffectLifecycle>(ge).State;

            return em.HasComponent<CDurationRuntime>(ge) && em.GetComponentData<CDurationRuntime>(ge).Active
                ? EGameplayEffectLifecycleState.Active
                : EGameplayEffectLifecycleState.PendingApply;
        }

        private static EGameplayEffectLifecycleState ResolveCleanupState(EntityManager em, Entity ge)
        {
            if (em.HasComponent<CEffectLifecycle>(ge))
            {
                var lifecycle = em.GetComponentData<CEffectLifecycle>(ge);
                return lifecycle.State == EGameplayEffectLifecycleState.PendingRemove
                    ? lifecycle.PreviousState
                    : lifecycle.State;
            }

            return ResolveLifecycleState(em, ge);
        }

        private static void SetLifecycle(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            EGameplayEffectLifecycleState state,
            int currentFrame)
        {
            if (!em.Exists(ge))
                return;

            if (!em.HasComponent<CEffectLifecycle>(ge))
            {
                ecb.AddComponent(ge, new CEffectLifecycle
                {
                    State = state,
                    PreviousState = state,
                    StateStartFrame = currentFrame,
                });
                return;
            }

            var lifecycle = em.GetComponentData<CEffectLifecycle>(ge);
            if (lifecycle.State == state)
                return;

            lifecycle.PreviousState = lifecycle.State;
            lifecycle.State = state;
            lifecycle.StateStartFrame = currentFrame;
            ecb.SetComponent(ge, lifecycle);
        }

        public static void MarkEffectForRemoval(EntityManager em, Entity ge, int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            MarkEffectForRemoval(em, ref ecb, ge, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void MarkEffectForRemoval(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int currentFrame)
        {
            if (!em.Exists(ge))
                return;

            SetLifecycle(em, ref ecb, ge, EGameplayEffectLifecycleState.PendingRemove, currentFrame);

            if (!em.HasComponent<CEffectDestroy>(ge))
                ecb.AddComponent<CEffectDestroy>(ge);
        }

        public static bool ShouldReject(EntityManager em, Entity target, Entity ge)
        {
            return ShouldReject(em, target, ge, out _);
        }

        public static bool ShouldReject(
            EntityManager em,
            Entity target,
            Entity ge,
            out TagRequirementEvaluationResult evaluation)
        {
            evaluation = EvaluateApplicationRequirements(em, target, ge);
            return !evaluation.Passed;
        }

        public static TagRequirementEvaluationResult EvaluateApplicationRequirements(
            EntityManager em,
            Entity target,
            Entity ge)
        {
            var targetEvaluation = TagRequirementEvaluator.EvaluateRequired(em, target, default);
            if (!targetEvaluation.Passed)
                return targetEvaluation;

            var targetTags = em.GetComponentData<CTagMask>(target);
            if (!TryGetStaticDefinitionBlob(em, ge, out var blob))
            {
                return TagRequirementEvaluator.Fail(
                    ETagRequirementEvaluationMode.Required,
                    ETagRequirementFailure.MissingDefinition,
                    target);
            }

            ref var definition = ref blob.Value;
            if (definition.HasApplicationRequiredTags
                && !TagRequirementEvaluator
                    .EvaluateRequired(targetTags, definition.ApplicationRequiredTags, target)
                    .Passed)
            {
                return TagRequirementEvaluator.EvaluateRequired(
                    targetTags,
                    definition.ApplicationRequiredTags,
                    target);
            }

            if (definition.HasImmunityTags
                && !TagRequirementEvaluator
                    .EvaluateImmunity(targetTags, definition.ImmunityTags, target)
                    .Passed)
            {
                return TagRequirementEvaluator.EvaluateImmunity(
                    targetTags,
                    definition.ImmunityTags,
                    target);
            }

            return TagRequirementEvaluator.Pass(ETagRequirementEvaluationMode.Required, target);
        }

        public static void EnqueueApplicationRejectedEvent(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            in TagRequirementEvaluationResult evaluation)
        {
            EnqueueGameplayEvent(
                em,
                ge,
                context,
                EGameplayEventType.GameplayEffectApplicationRejected,
                ResolveGameplayEffectCode(em, ge),
                (float)(int)evaluation.Mode,
                (int)evaluation.Failure);
        }

        public static void ApplyInstantEffect(EntityManager em, Entity ge, in CEffectContext context)
        {
            EnqueueCueRequests<CCueOnApply>(em, ge, context.TargetAsc, EGameplayCueEvent.OnApply);
            EnqueueCueRequestOnApply(em, ge, context.TargetAsc, EGameplayCueEvent.OnApply);
            ApplyInstantModifiers(em, ge, context);
            EnqueueGameplayEvent(em, ge, context, EGameplayEventType.GameplayEffectApplied);
        }

        public static bool RemoveActiveGameplayEffectsWithTags(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var removed = RemoveActiveGameplayEffectsWithTags(em, ref ecb, ge, context, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
            return removed;
        }

        public static bool RemoveActiveGameplayEffectsWithTags(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            int currentFrame)
        {
            if (!TryGetRemoveGameplayEffectsRequirement(em, ge, out var requirement)
                || requirement.IsEmpty)
            {
                return false;
            }

            var target = context.TargetAsc;
            if (!em.Exists(target) || !em.HasBuffer<BGameplayEffect>(target))
                return false;

            var activeEffects = em.GetBuffer<BGameplayEffect>(target);
            if (activeEffects.Length == 0)
                return false;

            var removed = false;
            for (var i = 0; i < activeEffects.Length; i++)
            {
                var activeEffect = activeEffects[i].GameplayEffect;
                if (activeEffect == ge
                    || !em.Exists(activeEffect)
                    || em.HasComponent<CEffectDestroy>(activeEffect)
                    || !MatchesEffectTags(em, activeEffect, requirement))
                {
                    continue;
                }

                MarkEffectForRemoval(em, ref ecb, activeEffect, currentFrame);
                removed = true;
            }

            return removed;
        }

        private static bool TryGetRemoveGameplayEffectsRequirement(
            EntityManager em,
            Entity ge,
            out TagRequirementMask requirement)
        {
            requirement = default;

            if (!TryGetStaticDefinitionBlob(em, ge, out var blob))
                return false;

            ref var definition = ref blob.Value;
            if (!definition.HasRemoveGameplayEffectsWithTags)
                return false;

            requirement = definition.RemoveGameplayEffectsWithTags;
            return true;
        }

        public static bool IsAppliedDurationEffect(EntityManager em, Entity ge, in CEffectContext context)
        {
            if (IsApplied(em, ge))
                return true;

            var target = context.TargetAsc;
            if (!em.Exists(target) || !em.HasBuffer<BGameplayEffect>(target))
                return false;

            var activeEffects = em.GetBuffer<BGameplayEffect>(target);
            for (var i = 0; i < activeEffects.Length; i++)
            {
                if (activeEffects[i].GameplayEffect == ge)
                    return true;
            }

            return false;
        }

        public static bool TryGetStaticDefinitionBlob(
            EntityManager em,
            Entity ge,
            out BlobAssetReference<GEStaticDefinitionBlob> blob)
        {
            blob = default;

            if (!em.Exists(ge) || !em.HasComponent<CEffectSpecData>(ge))
                return false;

            var spec = em.GetComponentData<CEffectSpecData>(ge);
            return spec.GameplayEffectCode > 0
                   && GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                       em,
                       spec.GameplayEffectCode,
                       out blob);
        }

        public static bool MeetsOngoingRequirements(EntityManager em, Entity ge, in CEffectContext context)
        {
            return EvaluateOngoingRequirements(em, ge, context).Passed;
        }

        public static TagRequirementEvaluationResult EvaluateOngoingRequirements(
            EntityManager em,
            Entity ge,
            in CEffectContext context)
        {
            if (!TryGetStaticDefinitionBlob(em, ge, out var blob))
                return TagRequirementEvaluator.Pass(ETagRequirementEvaluationMode.Required, context.TargetAsc);

            ref var definition = ref blob.Value;
            if (!definition.HasOngoingRequiredTags)
                return TagRequirementEvaluator.Pass(ETagRequirementEvaluationMode.Required, context.TargetAsc);

            return TagRequirementEvaluator.EvaluateRequired(
                em,
                context.TargetAsc,
                definition.OngoingRequiredTags);
        }

        public static bool HasOngoingRequirements(EntityManager em, Entity ge)
        {
            if (TryGetStaticDefinitionBlob(em, ge, out var blob))
                return blob.Value.HasOngoingRequiredTags;

            return false;
        }

        public static Entity CreateDerivedApplyRequest(
            EntityManager em,
            Entity sourceEffect,
            in CEffectContext context,
            int gameplayEffectCode)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var request = CreateDerivedApplyRequest(em, ref ecb, sourceEffect, context, gameplayEffectCode);
            ecb.Playback(em);
            ecb.Dispose();
            return request;
        }

        public static Entity CreateDerivedApplyRequest(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity sourceEffect,
            in CEffectContext context,
            int gameplayEffectCode)
        {
            if (gameplayEffectCode <= 0)
                return Entity.Null;

            var targetKind = context.TargetAsc == context.SourceAsc ? ETargetDataKind.Self : ETargetDataKind.Entity;
            var request = new CApplyGameplayEffectRequest
            {
                SourceAsc = context.SourceAsc,
                SourceAbility = context.SourceAbility,
                SourceEffect = sourceEffect,
                Instigator = context.Instigator,
                Causer = context.Causer != Entity.Null ? context.Causer : sourceEffect,
                GameplayEffectCode = gameplayEffectCode,
                Level = ResolveSourceEffectLevel(em, sourceEffect),
                ParentContextId = context.ContextId,
            };

            if (TryApplyDerivedFastInstant(em, sourceEffect, request, context.TargetAsc, targetKind))
                return Entity.Null;

            var requestEntity = GameplayEffectRequestWriter.Create(
                ref ecb,
                request,
                new CTargetDataHeader
                {
                    SourceAsc = context.SourceAsc,
                    SourceAbility = context.SourceAbility,
                    Kind = targetKind,
                },
                "DerivedApplyGERequest");

            GameplayEffectRequestWriter.AddTarget(ref ecb, requestEntity, context.TargetAsc);
            CopySetByCallerValues(em, ref ecb, sourceEffect, requestEntity);
            return requestEntity;
        }

        private static bool TryApplyDerivedFastInstant(
            EntityManager em,
            Entity sourceEffect,
            in CApplyGameplayEffectRequest request,
            Entity target,
            ETargetDataKind targetKind)
        {
            if (sourceEffect == Entity.Null
                || !em.Exists(sourceEffect)
                || !em.HasBuffer<BSetByCallerValue>(sourceEffect))
            {
                return GameplayEffectRequestWriter.TryApplyFastInstantModifier(
                    em,
                    request,
                    target,
                    targetKind);
            }

            var values = em.GetBuffer<BSetByCallerValue>(sourceEffect);
            if (values.Length == 0)
            {
                return GameplayEffectRequestWriter.TryApplyFastInstantModifier(
                    em,
                    request,
                    target,
                    targetKind);
            }

            return values.Length == 1
                   && GameplayEffectRequestWriter.TryApplyFastInstantModifier(
                       em,
                       request,
                       target,
                       targetKind,
                       values[0]);
        }

        private static int ResolveSourceEffectLevel(EntityManager em, Entity sourceEffect)
        {
            if (sourceEffect == Entity.Null
                || !em.Exists(sourceEffect)
                || !em.HasComponent<CEffectSpecData>(sourceEffect))
            {
                return 1;
            }

            var level = em.GetComponentData<CEffectSpecData>(sourceEffect).Level;
            return level > 0 ? level : 1;
        }

        private static int ResolveGameplayEffectCode(EntityManager em, Entity ge)
        {
            if (ge == Entity.Null || !em.Exists(ge) || !em.HasComponent<CEffectSpecData>(ge))
                return 0;

            return em.GetComponentData<CEffectSpecData>(ge).GameplayEffectCode;
        }

        private static void CopySetByCallerValues(EntityManager em, Entity sourceEffect, Entity request)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            CopySetByCallerValues(em, ref ecb, sourceEffect, request);
            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void CopySetByCallerValues(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity sourceEffect,
            Entity request)
        {
            if (!em.HasBuffer<BSetByCallerValue>(sourceEffect))
                return;

            var sourceValues = em.GetBuffer<BSetByCallerValue>(sourceEffect);
            if (sourceValues.Length == 0)
                return;

            GameplayEffectRequestWriter.AddSetByCallerValues(ref ecb, request, sourceValues);
        }

        public static bool TryMergeStackingApplication(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var merged = TryMergeStackingApplication(em, ref ecb, ge, context, ref duration, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
            return merged;
        }

        public static bool TryMergeStackingApplication(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            if (!em.HasComponent<CStackingDefinition>(ge))
                return false;

            var incomingDefinition = em.GetComponentData<CStackingDefinition>(ge);
            if (incomingDefinition.StackingCode == 0)
            {
                InitializeStackCount(em, ge, 1);
                return false;
            }

            var existing = FindMatchingStackedEffect(em, ge, context, incomingDefinition);
            if (existing == Entity.Null)
            {
                InitializeStackCount(em, ge, 1);
                return false;
            }

            var existingDefinition = em.GetComponentData<CStackingDefinition>(existing);
            var currentStackCount = GetCurrentStackCount(em, existing);
            var limitCount = ResolveLimitCount(incomingDefinition, existingDefinition);

            if (currentStackCount < limitCount)
            {
                SetStackCount(em, existing, currentStackCount + 1);
                RefreshStackedEffect(em, ref ecb, existing, incomingDefinition, currentFrame);
                DestroyEffectEntity(em, ref ecb, ge);
                return true;
            }

            EnqueueStackEvent(em, existing, context, EGameplayEventType.StackOverflow, existingDefinition, currentStackCount);
            CreateOverflowRequests(em, ref ecb, ge, context);

            if (incomingDefinition.ClearStackOnOverflow)
            {
                EnqueueStackEvent(
                    em,
                    existing,
                    context,
                    EGameplayEventType.StackClearedByOverflow,
                    existingDefinition,
                    currentStackCount);
                MarkEffectForRemoval(em, ref ecb, existing, currentFrame);
            }
            else if (incomingDefinition.DenyOverflowApplication)
            {
                EnqueueStackEvent(
                    em,
                    existing,
                    context,
                    EGameplayEventType.StackOverflowDenied,
                    existingDefinition,
                    currentStackCount);
            }
            else
            {
                RefreshStackedEffect(em, ref ecb, existing, incomingDefinition, currentFrame);
                EnqueueStackEvent(
                    em,
                    existing,
                    context,
                    EGameplayEventType.StackOverflowRefreshed,
                    existingDefinition,
                    currentStackCount);
            }

            DestroyEffectEntity(em, ref ecb, ge);
            return true;
        }

        private static bool MatchesEffectTags(EntityManager em, Entity ge, in TagRequirementMask requirement)
        {
            var tags = BuildEffectTags(em, ge);
            return !tags.IsEmpty && requirement.Evaluate(tags);
        }

        private static CTagMask BuildEffectTags(EntityManager em, Entity ge)
        {
            var tags = new CTagMask();
            if (!TryGetStaticDefinitionBlob(em, ge, out var blob))
                return tags;

            ref var definition = ref blob.Value;
            AddTags(ref tags, definition.AssetTags);
            AddTags(ref tags, definition.GrantedTags);

            return tags;
        }

        private static void AddTags(ref CTagMask tags, in CTagMask source)
        {
            tags.Mask0 |= source.Mask0;
            tags.Mask1 |= source.Mask1;
            tags.Mask2 |= source.Mask2;
            tags.Mask3 |= source.Mask3;
        }

        public static void ActivateDurationEffect(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ActivateDurationEffect(em, ref ecb, ge, context, ref duration, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void ActivateDurationEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            EnqueueCueRequests<CCueOnApply>(em, ge, context.TargetAsc, EGameplayCueEvent.OnApply);
            EnqueueCueRequestOnApply(em, ge, context.TargetAsc, EGameplayCueEvent.OnApply);
            EnqueueCueRequests<CCueOnAdd>(em, ge, context.TargetAsc, EGameplayCueEvent.OnAdd);

            AddRuntimeModifiers(em, ge, context);
            AddGrantedTags(em, ref ecb, ge, context.TargetAsc);
            AddGrantedAbilities(em, ref ecb, ge, context.TargetAsc);
            AddEffectToTarget(em, ge, context);

            duration.Active = true;
            duration.ActiveTime = currentFrame;
            duration.LastActiveTime = currentFrame;
            duration.RemainingTime = duration.ResolvedDuration;
            SetLifecycle(em, ref ecb, ge, EGameplayEffectLifecycleState.Active, currentFrame);

            SetPeriodStartTime(em, ref ecb, ge, currentFrame);

            EnqueueCueRequests<CCueOnActivate>(em, ge, context.TargetAsc, EGameplayCueEvent.OnActivate);
            EnqueueCueRequests<CCueOnTick>(em, ge, context.TargetAsc, EGameplayCueEvent.OnTick);
            EnqueueGameplayEvent(em, ge, context, EGameplayEventType.GameplayEffectApplied);
        }

        public static void ApplyInactiveDurationEffect(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ApplyInactiveDurationEffect(em, ref ecb, ge, context, ref duration, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void ApplyInactiveDurationEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            EnqueueCueRequests<CCueOnApply>(em, ge, context.TargetAsc, EGameplayCueEvent.OnApply);
            EnqueueCueRequestOnApply(em, ge, context.TargetAsc, EGameplayCueEvent.OnApply);
            EnqueueCueRequests<CCueOnAdd>(em, ge, context.TargetAsc, EGameplayCueEvent.OnAdd);
            AddEffectToTarget(em, ge, context);

            duration.Active = false;
            duration.ActiveTime = currentFrame;
            duration.LastActiveTime = currentFrame;
            duration.RemainingTime = duration.ResolvedDuration;
            SetLifecycle(em, ref ecb, ge, EGameplayEffectLifecycleState.Inhibited, currentFrame);

            EnqueueGameplayEvent(em, ge, context, EGameplayEventType.GameplayEffectApplied);
            EnqueueGameplayEvent(
                em,
                ge,
                context,
                EGameplayEventType.GameplayEffectInhibited,
                (int)EGameplayEffectLifecycleState.PendingApply,
                (int)EGameplayEffectLifecycleState.Inhibited);
        }

        public static void DeactivateOngoingEffect(EntityManager em, Entity ge, int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            DeactivateOngoingEffect(em, ref ecb, ge, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void DeactivateOngoingEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int currentFrame)
        {
            if (!em.Exists(ge)
                || !em.HasComponent<CEffectContext>(ge)
                || !em.HasComponent<CDurationDefinition>(ge)
                || !em.HasComponent<CDurationRuntime>(ge))
                return;

            var duration = em.GetComponentData<CDurationRuntime>(ge);
            if (!duration.Active)
                return;

            var context = em.GetComponentData<CEffectContext>(ge);
            var target = context.TargetAsc;
            var definition = em.GetComponentData<CDurationDefinition>(ge);

            EnqueueCueRequests<CCueOnDeactivate>(em, ge, target, EGameplayCueEvent.OnDeactivate);
            EnqueueStopCueRequests<CCueOnTick>(em, ge);
            RemoveGrantedAbilities(em, ref ecb, ge, target);
            RemoveRuntimeModifiers(em, ge, context);
            RemoveGrantedTags(em, ref ecb, ge, target);

            if (definition.StopTickWhenDeactivated && duration.ResolvedDuration > 0)
            {
                var elapsed = currentFrame - duration.ActiveTime;
                duration.RemainingTime = Math.Max(0, duration.ResolvedDuration - elapsed);
                duration.LastActiveTime = currentFrame;
            }

            duration.Active = false;
            ecb.SetComponent(ge, duration);
            SetLifecycle(em, ref ecb, ge, EGameplayEffectLifecycleState.Inhibited, currentFrame);
            EnqueueGameplayEvent(
                em,
                ge,
                context,
                EGameplayEventType.GameplayEffectInhibited,
                (int)EGameplayEffectLifecycleState.Active,
                (int)EGameplayEffectLifecycleState.Inhibited);
        }

        public static void ReactivateOngoingEffect(EntityManager em, Entity ge, int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ReactivateOngoingEffect(em, ref ecb, ge, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void ReactivateOngoingEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int currentFrame)
        {
            if (!em.Exists(ge)
                || !em.HasComponent<CEffectContext>(ge)
                || !em.HasComponent<CDurationDefinition>(ge)
                || !em.HasComponent<CDurationRuntime>(ge))
                return;

            var duration = em.GetComponentData<CDurationRuntime>(ge);
            if (duration.Active)
                return;

            var context = em.GetComponentData<CEffectContext>(ge);
            var definition = em.GetComponentData<CDurationDefinition>(ge);

            AddRuntimeModifiers(em, ge, context);
            AddGrantedTags(em, ref ecb, ge, context.TargetAsc);
            AddGrantedAbilities(em, ref ecb, ge, context.TargetAsc);

            duration.Active = true;
            if (definition.StopTickWhenDeactivated && duration.ResolvedDuration > 0 && duration.RemainingTime > 0)
                duration.ActiveTime = currentFrame - (duration.ResolvedDuration - duration.RemainingTime);

            if (ShouldResetPeriodWhenReactivated(em, ge))
                SetPeriodStartTime(em, ref ecb, ge, currentFrame);

            ecb.SetComponent(ge, duration);
            SetLifecycle(em, ref ecb, ge, EGameplayEffectLifecycleState.Active, currentFrame);
            EnqueueGameplayEvent(
                em,
                ge,
                context,
                EGameplayEventType.GameplayEffectReactivated,
                (int)EGameplayEffectLifecycleState.Inhibited,
                (int)EGameplayEffectLifecycleState.Active);

            EnqueueCueRequests<CCueOnActivate>(em, ge, context.TargetAsc, EGameplayCueEvent.OnActivate);
            EnqueueCueRequests<CCueOnTick>(em, ge, context.TargetAsc, EGameplayCueEvent.OnTick);
        }

        private static Entity FindMatchingStackedEffect(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            in CStackingDefinition incomingDefinition)
        {
            var target = context.TargetAsc;
            if (!em.HasBuffer<BGameplayEffect>(target))
                return Entity.Null;

            var effects = em.GetBuffer<BGameplayEffect>(target);
            for (var i = 0; i < effects.Length; i++)
            {
                var existing = effects[i].GameplayEffect;
                if (existing == ge
                    || !em.Exists(existing)
                    || em.HasComponent<CEffectDestroy>(existing)
                    || !em.HasComponent<CStackingDefinition>(existing)
                    || !em.HasComponent<CEffectContext>(existing))
                {
                    continue;
                }

                if (!IsApplied(em, existing))
                    continue;

                var existingDefinition = em.GetComponentData<CStackingDefinition>(existing);
                if (existingDefinition.StackingCode != incomingDefinition.StackingCode)
                    continue;

                if (incomingDefinition.StackType == EffectStackType.AggregateBySource)
                {
                    var existingContext = em.GetComponentData<CEffectContext>(existing);
                    if (existingContext.SourceAsc != context.SourceAsc)
                        continue;
                }

                return existing;
            }

            return Entity.Null;
        }

        private static void InitializeStackCount(EntityManager em, Entity ge, int stackCount)
        {
            SetStackCount(em, ge, stackCount);
        }

        private static void SetStackCount(EntityManager em, Entity ge, int stackCount)
        {
            if (!em.HasComponent<CStackingRuntime>(ge))
                return;

            stackCount = NormalizeStackCount(stackCount);
            var oldStackCount = GetCurrentStackCount(em, ge);
            var changed = false;

            var runtime = em.GetComponentData<CStackingRuntime>(ge);
            if (runtime.StackCount != stackCount)
            {
                runtime.StackCount = stackCount;
                em.SetComponentData(ge, runtime);
                changed = true;
            }

            if (em.HasComponent<CEffectSpecData>(ge))
            {
                var spec = em.GetComponentData<CEffectSpecData>(ge);
                if (spec.StackCount != stackCount)
                {
                    spec.StackCount = stackCount;
                    em.SetComponentData(ge, spec);
                    changed = true;
                }

                if (em.HasComponent<CEffectContext>(ge))
                {
                    var effectContext = em.GetComponentData<CEffectContext>(ge);
                    EffectMagnitudeResolver.ResolveModifiers(em, ge, effectContext, spec);
                }
            }

            if (!changed || !em.HasComponent<CEffectContext>(ge) || !IsApplied(em, ge))
                return;

            var context = em.GetComponentData<CEffectContext>(ge);
            EnqueueGameplayEvent(
                em,
                ge,
                context,
                EGameplayEventType.StackCountChanged,
                oldStackCount,
                stackCount);

            if (IsActive(em, ge))
                SyncRuntimeModifiersFromResolved(em, ge, context);
        }

        private static int GetCurrentStackCount(EntityManager em, Entity ge)
        {
            if (em.HasComponent<CStackingRuntime>(ge))
                return NormalizeStackCount(em.GetComponentData<CStackingRuntime>(ge).StackCount);

            return 1;
        }

        private static int NormalizeStackCount(int stackCount)
        {
            return stackCount > 0 ? stackCount : 1;
        }

        private static int ResolveLimitCount(
            in CStackingDefinition incomingDefinition,
            in CStackingDefinition existingDefinition)
        {
            if (incomingDefinition.LimitCount > 0)
                return incomingDefinition.LimitCount;

            if (existingDefinition.LimitCount > 0)
                return existingDefinition.LimitCount;

            return int.MaxValue;
        }

        private static void RefreshStackedEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity existing,
            in CStackingDefinition incomingDefinition,
            int currentFrame)
        {
            if (incomingDefinition.EffectDurationRefreshPolicy == EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication
                && em.HasComponent<CDurationRuntime>(existing))
            {
                var duration = em.GetComponentData<CDurationRuntime>(existing);
                RefreshDuration(em, ref ecb, existing, ref duration, currentFrame);
            }

            if (incomingDefinition.EffectPeriodResetPolicy == EffectPeriodResetPolicy.ResetOnSuccessfulApplication
                && HasPeriod(em, existing))
                SetPeriodStartTime(em, ref ecb, existing, currentFrame);
        }

        private static bool HasPeriod(EntityManager em, Entity ge)
        {
            return em.HasComponent<CPeriodDefinition>(ge);
        }

        private static bool ShouldResetPeriodWhenReactivated(EntityManager em, Entity ge)
        {
            return em.HasComponent<CPeriodDefinition>(ge)
                   && em.GetComponentData<CPeriodDefinition>(ge).ResetTimeCountWhenDeactivated;
        }

        private static void SetPeriodStartTime(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int currentFrame)
        {
            if (!HasPeriod(em, ge))
                return;

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
        }

        public static void HandleDurationExpired(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            HandleDurationExpired(em, ref ecb, ge, context, ref duration, currentFrame);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void HandleDurationExpired(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            if (!em.HasComponent<CStackingDefinition>(ge))
            {
                MarkEffectForRemoval(em, ref ecb, ge, currentFrame);
                return;
            }

            var definition = em.GetComponentData<CStackingDefinition>(ge);
            if (definition.StackingCode == 0)
            {
                MarkEffectForRemoval(em, ref ecb, ge, currentFrame);
                return;
            }

            switch (definition.EffectExpirationPolicy)
            {
                case EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration:
                    var currentStackCount = GetCurrentStackCount(em, ge);
                    if (currentStackCount <= 1)
                    {
                        MarkEffectForRemoval(em, ref ecb, ge, currentFrame);
                        return;
                    }

                    SetStackCount(em, ge, currentStackCount - 1);
                    RefreshDuration(em, ref ecb, ge, ref duration, currentFrame);
                    break;

                case EffectExpirationPolicy.RefreshDuration:
                    RefreshDuration(em, ref ecb, ge, ref duration, currentFrame);
                    break;

                case EffectExpirationPolicy.ClearEntireStack:
                default:
                    MarkEffectForRemoval(em, ref ecb, ge, currentFrame);
                    break;
            }
        }

        private static void RefreshDuration(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            ref CDurationRuntime duration,
            int currentFrame)
        {
            duration.ActiveTime = currentFrame;
            duration.LastActiveTime = currentFrame;
            duration.RemainingTime = duration.ResolvedDuration;
            ecb.SetComponent(ge, duration);
        }

        private static void CreateOverflowRequests(EntityManager em, Entity ge, in CEffectContext context)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            CreateOverflowRequests(em, ref ecb, ge, context);
            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void CreateOverflowRequests(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            in CEffectContext context)
        {
            if (!TryGetStaticDefinitionBlob(em, ge, out var blob))
                return;

            ref var definition = ref blob.Value;
            if (!definition.HasStacking)
                return;

            for (var i = 0; i < definition.OverflowEffectCodes.Length; i++)
                CreateDerivedApplyRequest(em, ref ecb, ge, context, definition.OverflowEffectCodes[i]);
        }

        private static void EnqueueStackEvent(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            EGameplayEventType type,
            in CStackingDefinition definition,
            int stackCount)
        {
            EnqueueGameplayEvent(em, ge, context, type, definition.StackingCode, stackCount);
        }

        public static void CleanupActiveEffect(EntityManager em, Entity ge)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            CleanupActiveEffect(em, ref ecb, ge);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void CleanupActiveEffect(EntityManager em, ref EntityCommandBuffer ecb, Entity ge)
        {
            if (!em.Exists(ge))
                return;

            if (!em.HasComponent<CEffectContext>(ge))
            {
                DestroyEffectEntity(em, ref ecb, ge);
                return;
            }

            var context = em.GetComponentData<CEffectContext>(ge);
            var target = context.TargetAsc;
            var cleanupState = ResolveCleanupState(em, ge);

            if (cleanupState == EGameplayEffectLifecycleState.Active)
            {
                EnqueueCueRequests<CCueOnDeactivate>(em, ge, target, EGameplayCueEvent.OnDeactivate);
                EnqueueStopCueRequests<CCueOnTick>(em, ge);
                RemoveGrantedAbilities(em, ref ecb, ge, target);
                RemoveRuntimeModifiers(em, ge, context);
                RemoveGrantedTags(em, ref ecb, ge, target);
            }

            if (cleanupState == EGameplayEffectLifecycleState.Active
                || cleanupState == EGameplayEffectLifecycleState.Inhibited)
            {
                RemoveEffectFromTarget(em, ge, context);
                EnqueueCueRequests<CCueOnRemove>(em, ge, target, EGameplayCueEvent.OnRemove);
            }

            if (em.HasComponent<CDurationRuntime>(ge))
            {
                var duration = em.GetComponentData<CDurationRuntime>(ge);
                duration.Active = false;
                ecb.SetComponent(ge, duration);
            }

            DestroyEffectEntity(em, ref ecb, ge);
        }

        public static void DestroyEffectEntity(EntityManager em, Entity ge)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            DestroyEffectEntity(em, ref ecb, ge);
            ecb.Playback(em);
            ecb.Dispose();
        }

        public static void DestroyEffectEntity(EntityManager em, ref EntityCommandBuffer ecb, Entity ge)
        {
            if (!em.Exists(ge))
                return;

            DestroyNestedEffects(em, ge);
            EnqueueKillCueRequests<CCueOnApply>(em, ge);
            EnqueueKillCueRequests<CCueOnAdd>(em, ge);
            EnqueueKillCueRequests<CCueOnActivate>(em, ge);
            EnqueueKillCueRequests<CCueOnTick>(em, ge);
            EnqueueKillCueRequests<CCueOnDeactivate>(em, ge);
            EnqueueKillCueRequests<CCueOnRemove>(em, ge);

            ecb.DestroyEntity(ge);
        }

        private static void ApplyInstantModifiers(EntityManager em, Entity ge, in CEffectContext context)
        {
            var target = context.TargetAsc;
            if (!em.HasBuffer<BAttribute>(target))
                return;

            var attributes = em.GetBuffer<BAttribute>(target);

            if (!em.HasBuffer<BResolvedModifier>(ge))
                return;

            var resolvedModifiers = em.GetBuffer<BResolvedModifier>(ge);
            foreach (var modifier in resolvedModifiers)
            {
                var attrIndex = attributes.IndexOfAttribute(modifier.AttrSetCode, modifier.AttributeCode);
                if (attrIndex == -1)
                    continue;

                var attribute = attributes[attrIndex];
                var oldValue = attribute.BaseValue;
                var oldCurrentValue = attribute.CurrentValue;
                var newValue = AttributeHelper.ApplyModifier(attribute.BaseValue, modifier.Op, modifier.Magnitude);
                attribute.CurrentValue = newValue;
                AttributeHelper.Clamp(ref attribute);
                newValue = attribute.CurrentValue;
                attribute.BaseValue = newValue;
                attribute.CurrentValue = newValue;

                if (newValue != oldValue)
                {
                    attribute.Dirty = true;
                    if (oldCurrentValue != attribute.CurrentValue)
                    {
                        attribute.PreviousCurrentValue = oldCurrentValue;
                        attribute.CurrentValueChangePending = true;
                    }
                    EnqueueAttributeChangeEvent(em, ge, context, modifier.AttrSetCode, modifier.AttributeCode, oldValue, newValue, true);
                }

                attributes[attrIndex] = attribute;
            }
        }

        private static void AddRuntimeModifiers(EntityManager em, Entity ge, in CEffectContext context)
        {
            var target = context.TargetAsc;
            if (!em.HasBuffer<BActiveModifier>(target))
                return;

            var activeModifiers = em.GetBuffer<BActiveModifier>(target);

            if (!em.HasBuffer<BResolvedModifier>(ge))
                return;

            var resolvedModifiers = em.GetBuffer<BResolvedModifier>(ge);
            foreach (var modifier in resolvedModifiers)
            {
                activeModifiers.Add(new BActiveModifier
                {
                    AttrSetCode = modifier.AttrSetCode,
                    AttributeCode = modifier.AttributeCode,
                    SourceEntity = modifier.SourceEffect,
                    Magnitude = modifier.Magnitude,
                    Op = modifier.Op,
                });

                AttributeHelper.MarkCurrentValueDirty(target, modifier.AttrSetCode, modifier.AttributeCode);
                EnqueueGameplayEvent(em, ge, context, EGameplayEventType.ActiveModifierAdded, modifier.AttributeCode, modifier.Magnitude);
            }
        }

        private static void RemoveRuntimeModifiers(EntityManager em, Entity ge, in CEffectContext context)
        {
            var target = context.TargetAsc;
            if (!em.HasBuffer<BActiveModifier>(target))
                return;

            var activeModifiers = em.GetBuffer<BActiveModifier>(target);
            for (var i = activeModifiers.Length - 1; i >= 0; i--)
            {
                var modifier = activeModifiers[i];
                if (modifier.SourceEntity != ge)
                    continue;

                AttributeHelper.MarkCurrentValueDirty(target, modifier.AttrSetCode, modifier.AttributeCode);
                EnqueueGameplayEvent(em, ge, context, EGameplayEventType.ActiveModifierRemoved, modifier.AttributeCode, modifier.Magnitude);
                activeModifiers.RemoveAt(i);
            }
        }

        public static void SyncRuntimeModifiersFromResolved(EntityManager em, Entity ge, in CEffectContext context)
        {
            var target = context.TargetAsc;
            if (!em.HasBuffer<BActiveModifier>(target))
                return;

            if (!em.HasBuffer<BResolvedModifier>(ge))
            {
                RemoveRuntimeModifiers(em, ge, context);
                return;
            }

            var activeModifiers = em.GetBuffer<BActiveModifier>(target);
            var resolvedModifiers = em.GetBuffer<BResolvedModifier>(ge);
            var activeModifierCount = CountActiveModifiers(activeModifiers, ge);
            if (activeModifierCount != resolvedModifiers.Length)
            {
                RemoveRuntimeModifiers(em, ge, context);
                AddRuntimeModifiers(em, ge, context);
                return;
            }

            var resolvedIndex = 0;
            for (var i = 0; i < activeModifiers.Length; i++)
            {
                var activeModifier = activeModifiers[i];
                if (activeModifier.SourceEntity != ge)
                    continue;

                var resolvedModifier = resolvedModifiers[resolvedIndex++];
                if (activeModifier.AttrSetCode == resolvedModifier.AttrSetCode
                    && activeModifier.AttributeCode == resolvedModifier.AttributeCode
                    && activeModifier.Op == resolvedModifier.Op
                    && activeModifier.Magnitude == resolvedModifier.Magnitude
                    && activeModifier.SourceEntity == resolvedModifier.SourceEffect)
                {
                    continue;
                }

                AttributeHelper.MarkCurrentValueDirty(
                    target,
                    activeModifier.AttrSetCode,
                    activeModifier.AttributeCode);

                activeModifier.AttrSetCode = resolvedModifier.AttrSetCode;
                activeModifier.AttributeCode = resolvedModifier.AttributeCode;
                activeModifier.SourceEntity = resolvedModifier.SourceEffect;
                activeModifier.Magnitude = resolvedModifier.Magnitude;
                activeModifier.Op = resolvedModifier.Op;
                activeModifiers[i] = activeModifier;

                AttributeHelper.MarkCurrentValueDirty(
                    target,
                    activeModifier.AttrSetCode,
                    activeModifier.AttributeCode);
                EnqueueGameplayEvent(
                    em,
                    ge,
                    context,
                    EGameplayEventType.ActiveModifierUpdated,
                    activeModifier.AttributeCode,
                    activeModifier.Magnitude);
            }
        }

        private static int CountActiveModifiers(DynamicBuffer<BActiveModifier> activeModifiers, Entity sourceEffect)
        {
            var count = 0;
            for (var i = 0; i < activeModifiers.Length; i++)
            {
                if (activeModifiers[i].SourceEntity == sourceEffect)
                    count++;
            }

            return count;
        }

        private static void AddGrantedTags(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            Entity target)
        {
            if (!em.HasComponent<CTagMask>(target)
                || !TryGetStaticDefinitionBlob(em, ge, out var blob)
                || blob.Value.GrantedTags.IsEmpty)
            {
                return;
            }

            var tags = em.GetComponentData<CTagMask>(target);
            var grantedTags = blob.Value.GrantedTags;
            var tempSources = em.HasBuffer<BTempTagSource>(target)
                ? em.GetBuffer<BTempTagSource>(target)
                : default;

            for (var tagIndex = 0; tagIndex < CTagMask.Capacity; tagIndex++)
            {
                if (!grantedTags.HasTag(tagIndex))
                    continue;

                var wasActive = tags.HasTag(tagIndex);
                tags.AddTag(tagIndex);

                if (tempSources.IsCreated)
                {
                    tempSources.Add(new BTempTagSource
                    {
                        TagIndex = tagIndex,
                        Source = ge,
                    });
                }

                if (!wasActive && tags.HasTag(tagIndex))
                    EnqueueTagChangeEvent(em, target, tagIndex, added: true);
            }

            ecb.SetComponent(target, tags);
        }

        private static void RemoveGrantedTags(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            Entity target)
        {
            if (!em.HasComponent<CTagMask>(target)
                || !TryGetStaticDefinitionBlob(em, ge, out var blob)
                || blob.Value.GrantedTags.IsEmpty)
            {
                return;
            }

            var hasTempSources = em.HasBuffer<BTempTagSource>(target);
            var tempSources = hasTempSources ? em.GetBuffer<BTempTagSource>(target) : default;

            if (hasTempSources)
            {
                for (var i = tempSources.Length - 1; i >= 0; i--)
                {
                    if (tempSources[i].Source == ge)
                        tempSources.RemoveAt(i);
                }
            }

            var tags = em.GetComponentData<CTagMask>(target);
            var grantedTags = blob.Value.GrantedTags;
            for (var tagIndex = 0; tagIndex < CTagMask.Capacity; tagIndex++)
            {
                if (!grantedTags.HasTag(tagIndex))
                    continue;

                var wasActive = tags.HasTag(tagIndex);
                if (!HasFixedTag(em, target, tagIndex)
                    && (!hasTempSources || !HasTemporarySourceForTag(tempSources, tagIndex)))
                {
                    tags.RemoveTag(tagIndex);
                }

                if (wasActive && !tags.HasTag(tagIndex))
                    EnqueueTagChangeEvent(em, target, tagIndex, added: false);
            }

            ecb.SetComponent(target, tags);
        }

        private static void AddGrantedAbilities(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            Entity target)
        {
            if (!em.HasBuffer<BGrantedAbility>(target))
                return;

            if (!TryGetStaticDefinitionBlob(em, ge, out var blob))
                return;

            ref var definitions = ref blob.Value.GrantedAbilities;
            if (definitions.Length == 0)
                return;

            if (!em.HasBuffer<BGrantedAbilityRuntime>(ge))
            {
                ecb.AddBuffer<BGrantedAbilityRuntime>(ge);
                PlaybackAndReset(ref ecb, em);
            }

            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                AddGrantedAbility(
                    em,
                    ref ecb,
                    ge,
                    target,
                    i,
                    definition.AbilityCode,
                    definition.Level,
                    definition.ActivationPolicy,
                    definition.DeactivationPolicy,
                    definition.RemovePolicy);
            }
        }

        private static void AddGrantedAbility(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            Entity target,
            int configIndex,
            int abilityCode,
            int level,
            GrantedAbilityActivationPolicy activationPolicy,
            GrantedAbilityDeactivationPolicy deactivationPolicy,
            GrantedAbilityRemovePolicy removePolicy)
        {
            var abilityEntity = FindRuntimeGrantedAbility(em, ge, configIndex);
            var queuedTryActivate = false;
            if (abilityEntity == Entity.Null || !em.Exists(abilityEntity))
            {
                var effectCode = em.HasComponent<CEffectSpecData>(ge)
                    ? em.GetComponentData<CEffectSpecData>(ge).GameplayEffectCode
                    : 0;
                var abilityConfig = AbilityConfigRegistry.GetConfigByID(
                    abilityCode,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        effectCode,
                        ConfigRegistryReferenceKind.GameplayEffectGrantedAbility));
                if (abilityConfig == null)
                    return;

                abilityEntity = AbilityEntityFactory.CreateAbilityEntity(abilityConfig);
                if (em.HasComponent<CAbilityBaseInfo>(abilityEntity))
                {
                    var baseInfo = em.GetComponentData<CAbilityBaseInfo>(abilityEntity);
                    baseInfo.Owner = target;
                    baseInfo.Level = level;
                    ecb.SetComponent(abilityEntity, baseInfo);
                }

                ecb.AddComponent(abilityEntity, new CGrantedByEffect
                {
                    SourceEffect = ge,
                    ActivationPolicy = activationPolicy,
                    DeactivationPolicy = deactivationPolicy,
                    RemovePolicy = removePolicy,
                });

                AddAbilityToTargetBuffer(em, target, abilityEntity);
                AddRuntimeGrantedAbility(em, ref ecb, ge, configIndex, abilityEntity);

                if (activationPolicy is GrantedAbilityActivationPolicy.WhenAdded
                    or GrantedAbilityActivationPolicy.SyncWithEffect)
                {
                    if (!em.HasComponent<CAbilityInTryActivate>(abilityEntity))
                    {
                        ecb.AddComponent<CAbilityInTryActivate>(abilityEntity);
                        queuedTryActivate = true;
                    }
                }
            }

            if (activationPolicy != GrantedAbilityActivationPolicy.SyncWithEffect)
                return;

            if (abilityEntity == Entity.Null || !em.Exists(abilityEntity) || em.HasComponent<CAbilityActive>(abilityEntity))
                return;

            if (!queuedTryActivate && !em.HasComponent<CAbilityInTryActivate>(abilityEntity))
                ecb.AddComponent<CAbilityInTryActivate>(abilityEntity);
        }

        private static void RemoveGrantedAbilities(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            Entity target)
        {
            if (!em.HasBuffer<BGrantedAbilityRuntime>(ge))
                return;

            var runtimeBuffer = em.GetBuffer<BGrantedAbilityRuntime>(ge);
            var runtimeAbilities = new NativeArray<BGrantedAbilityRuntime>(runtimeBuffer.Length, Allocator.Temp);
            for (var i = 0; i < runtimeBuffer.Length; i++)
                runtimeAbilities[i] = runtimeBuffer[i];

            if (!TryGetStaticDefinitionBlob(em, ge, out var blob))
            {
                runtimeAbilities.Dispose();
                return;
            }

            ref var definitions = ref blob.Value.GrantedAbilities;
            for (var i = runtimeAbilities.Length - 1; i >= 0; i--)
            {
                var runtime = runtimeAbilities[i];
                if (runtime.ConfigIndex < 0 || runtime.ConfigIndex >= definitions.Length)
                    continue;

                var definition = definitions[runtime.ConfigIndex];
                RemoveGrantedAbility(
                    em,
                    ref ecb,
                    ge,
                    target,
                    i,
                    runtime.AbilityEntity,
                    definition.DeactivationPolicy,
                    definition.RemovePolicy);
            }

            runtimeAbilities.Dispose();
        }

        private static void RemoveGrantedAbility(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            Entity target,
            int runtimeBufferIndex,
            Entity abilityEntity,
            GrantedAbilityDeactivationPolicy deactivationPolicy,
            GrantedAbilityRemovePolicy removePolicy)
        {
            if (abilityEntity == Entity.Null || !em.Exists(abilityEntity))
                return;

            if (deactivationPolicy == GrantedAbilityDeactivationPolicy.SyncWithEffect
                && removePolicy != GrantedAbilityRemovePolicy.SyncWithEffect
                && em.HasComponent<CAbilityActive>(abilityEntity))
            {
                AbilityRuntimeActions.RequestAbilityCancel(
                    abilityEntity,
                    em,
                    ref ecb,
                    EAbilityLifecycleReason.GrantedEffectDeactivated,
                    sourceEffect: ge);
            }

            if (removePolicy != GrantedAbilityRemovePolicy.SyncWithEffect)
                return;

            RemoveAbilityFromTargetBuffer(em, target, abilityEntity);
            CancelOrDestroyAbility(em, ref ecb, abilityEntity, ge);
            SetRuntimeGrantedAbility(em, ge, runtimeBufferIndex, Entity.Null);
        }

        private static Entity FindRuntimeGrantedAbility(EntityManager em, Entity ge, int configIndex)
        {
            if (!em.HasBuffer<BGrantedAbilityRuntime>(ge))
                return Entity.Null;

            var runtimeAbilities = em.GetBuffer<BGrantedAbilityRuntime>(ge);
            for (var i = 0; i < runtimeAbilities.Length; i++)
            {
                if (runtimeAbilities[i].ConfigIndex == configIndex)
                    return runtimeAbilities[i].AbilityEntity;
            }

            return Entity.Null;
        }

        private static void AddAbilityToTargetBuffer(EntityManager em, Entity target, Entity abilityEntity)
        {
            if (!em.HasBuffer<BGrantedAbility>(target))
                return;

            var abilityBuffer = em.GetBuffer<BGrantedAbility>(target);
            abilityBuffer.Add(new BGrantedAbility { AbilityEntity = abilityEntity });
        }

        private static void AddRuntimeGrantedAbility(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ge,
            int configIndex,
            Entity abilityEntity)
        {
            if (!em.HasBuffer<BGrantedAbilityRuntime>(ge))
            {
                ecb.AddBuffer<BGrantedAbilityRuntime>(ge);
                PlaybackAndReset(ref ecb, em);
            }

            var runtimeAbilities = em.GetBuffer<BGrantedAbilityRuntime>(ge);
            for (var i = 0; i < runtimeAbilities.Length; i++)
            {
                if (runtimeAbilities[i].ConfigIndex != configIndex)
                    continue;

                var runtime = runtimeAbilities[i];
                runtime.AbilityEntity = abilityEntity;
                runtimeAbilities[i] = runtime;
                return;
            }

            runtimeAbilities.Add(new BGrantedAbilityRuntime
            {
                ConfigIndex = configIndex,
                AbilityEntity = abilityEntity,
            });
        }

        private static void SetRuntimeGrantedAbility(EntityManager em, Entity ge, int bufferIndex, Entity abilityEntity)
        {
            if (!em.HasBuffer<BGrantedAbilityRuntime>(ge))
                return;

            var runtimeAbilities = em.GetBuffer<BGrantedAbilityRuntime>(ge);
            if (bufferIndex < 0 || bufferIndex >= runtimeAbilities.Length)
                return;

            var runtime = runtimeAbilities[bufferIndex];
            runtime.AbilityEntity = abilityEntity;
            runtimeAbilities[bufferIndex] = runtime;
        }

        private static void RemoveAbilityFromTargetBuffer(EntityManager em, Entity target, Entity abilityEntity)
        {
            if (!em.HasBuffer<BGrantedAbility>(target))
                return;

            var abilityBuffer = em.GetBuffer<BGrantedAbility>(target);
            for (var i = abilityBuffer.Length - 1; i >= 0; i--)
            {
                if (abilityBuffer[i].AbilityEntity == abilityEntity)
                    abilityBuffer.RemoveAt(i);
            }
        }

        private static void CancelOrDestroyAbility(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity abilityEntity,
            Entity sourceEffect)
        {
            if (!em.Exists(abilityEntity))
                return;

            if (em.HasComponent<CAbilityActive>(abilityEntity))
            {
                AbilityRuntimeActions.RequestAbilityCancel(
                    abilityEntity,
                    em,
                    ref ecb,
                    EAbilityLifecycleReason.GrantedEffectRemoved,
                    sourceEffect: sourceEffect);

                if (!em.HasComponent<CAbilityDestroyOnCleanup>(abilityEntity))
                    ecb.AddComponent<CAbilityDestroyOnCleanup>(abilityEntity);
                return;
            }

            DestroyAbilityEntity(em, ref ecb, abilityEntity);
        }

        private static void DestroyAbilityEntity(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity abilityEntity)
        {
            if (!em.Exists(abilityEntity))
                return;

            if (em.HasComponent<CAbilityConfig>(abilityEntity))
            {
                var config = em.GetComponentData<CAbilityConfig>(abilityEntity).Config;
                if (config.IsCreated)
                    config.Dispose();
            }

            ecb.DestroyEntity(abilityEntity);
        }

        private static void AddEffectToTarget(EntityManager em, Entity ge, in CEffectContext context)
        {
            var target = context.TargetAsc;
            if (!em.HasBuffer<BGameplayEffect>(target))
                return;

            var effects = em.GetBuffer<BGameplayEffect>(target);
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i].GameplayEffect == ge)
                    return;
            }

            effects.Add(new BGameplayEffect { GameplayEffect = ge });
        }

        private static void RemoveEffectFromTarget(EntityManager em, Entity ge, in CEffectContext context)
        {
            var target = context.TargetAsc;
            if (!em.HasBuffer<BGameplayEffect>(target))
                return;

            var effects = em.GetBuffer<BGameplayEffect>(target);
            for (var i = effects.Length - 1; i >= 0; i--)
            {
                if (effects[i].GameplayEffect == ge)
                    effects.RemoveAt(i);
            }

            EnqueueGameplayEvent(em, ge, context, EGameplayEventType.GameplayEffectRemoved);
        }

        private static void DestroyNestedEffects(EntityManager em, Entity ge)
        {
        }

        private static void EnqueueCueRequests<TCueComponent>(
            EntityManager em,
            Entity ge,
            Entity target,
            EGameplayCueEvent cueEvent)
            where TCueComponent : unmanaged, IComponentData
        {
            if (!TryGetCueArray(em, ge, out NativeArray<Entity> cues, typeof(TCueComponent)))
                return;

            for (var i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (em.Exists(cue))
                    EnqueueCueRequest(em, ge, cue, target, cueEvent);
            }
        }

        private static void EnqueueCueRequestOnApply(
            EntityManager em,
            Entity ge,
            Entity target,
            EGameplayCueEvent cueEvent)
        {
            if (!em.Exists(ge) || !em.HasComponent<CGameplayEffectCueRequestOnApply>(ge))
                return;

            var cue = em.GetComponentData<CGameplayEffectCueRequestOnApply>(ge);
            var context = em.HasComponent<CEffectContext>(ge)
                ? em.GetComponentData<CEffectContext>(ge)
                : default;
            var sourceAsc = context.SourceAsc != Entity.Null ? context.SourceAsc : Entity.Null;
            var sourceAbility = context.SourceAbility != Entity.Null ? context.SourceAbility : Entity.Null;

            EventBusHelper.EnqueueCueRequest(em, GASManager.EntityEventBus, new BCueRequest
            {
                TargetAsc = target,
                SourceAsc = sourceAsc,
                SourceAbility = sourceAbility,
                GameplayEffect = ge,
                SourceEntity = ge,
                SourceType = CueSourceType.GameplayEffect,
                CueEntity = Entity.Null,
                ContextId = context.ContextId,
                CueEvent = cueEvent,
            });

            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = EGameplayEventType.CueRequested,
                SourceAsc = sourceAsc,
                TargetAsc = target,
                SourceAbility = sourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = (int)cueEvent,
                ReasonCode = cue.CueCode,
            });
        }

        private static void EnqueueStopCueRequests<TCueComponent>(EntityManager em, Entity ge)
            where TCueComponent : unmanaged, IComponentData
        {
            if (!TryGetCueArray(em, ge, out NativeArray<Entity> cues, typeof(TCueComponent)))
                return;

            for (var i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (!em.Exists(cue) || !em.HasComponent<MCCue>(cue))
                    continue;

                EnqueueCueRequest(em, ge, cue, ResolveCueTarget(em, ge), EGameplayCueEvent.StopTick);
            }
        }

        private static void EnqueueKillCueRequests<TCueComponent>(EntityManager em, Entity ge)
            where TCueComponent : unmanaged, IComponentData
        {
            if (!TryGetCueArray(em, ge, out NativeArray<Entity> cues, typeof(TCueComponent)))
                return;

            for (var i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (!em.Exists(cue))
                    continue;

                EnqueueCueRequest(em, ge, cue, ResolveCueTarget(em, ge), EGameplayCueEvent.Kill);
            }
        }

        private static Entity ResolveCueTarget(EntityManager em, Entity ge)
        {
            return em.Exists(ge) && em.HasComponent<CEffectContext>(ge)
                ? em.GetComponentData<CEffectContext>(ge).TargetAsc
                : Entity.Null;
        }

        private static void EnqueueCueRequest(
            EntityManager em,
            Entity ge,
            Entity cue,
            Entity target,
            EGameplayCueEvent cueEvent)
        {
            var context = em.Exists(ge) && em.HasComponent<CEffectContext>(ge)
                ? em.GetComponentData<CEffectContext>(ge)
                : default;

            var sourceAsc = context.SourceAsc != Entity.Null ? context.SourceAsc : Entity.Null;
            var sourceAbility = context.SourceAbility != Entity.Null ? context.SourceAbility : Entity.Null;
            var contextId = context.ContextId;

            EventBusHelper.EnqueueCueRequest(em, GASManager.EntityEventBus, new BCueRequest
            {
                TargetAsc = target,
                SourceAsc = sourceAsc,
                SourceAbility = sourceAbility,
                GameplayEffect = ge,
                SourceEntity = ge,
                SourceType = CueSourceType.GameplayEffect,
                CueEntity = cue,
                ContextId = contextId,
                CueEvent = cueEvent,
            });

            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = EGameplayEventType.CueRequested,
                SourceAsc = sourceAsc,
                TargetAsc = target,
                SourceAbility = sourceAbility,
                GameplayEffect = ge,
                ContextId = contextId,
                EventCode = (int)cueEvent,
            });
        }

        private static bool TryGetCueArray(
            EntityManager em,
            Entity ge,
            out NativeArray<Entity> cues,
            Type componentType)
        {
            cues = default;

            if (componentType == typeof(CCueOnApply) && em.HasComponent<CCueOnApply>(ge))
                cues = em.GetComponentData<CCueOnApply>(ge).cues;
            else if (componentType == typeof(CCueOnAdd) && em.HasComponent<CCueOnAdd>(ge))
                cues = em.GetComponentData<CCueOnAdd>(ge).cues;
            else if (componentType == typeof(CCueOnActivate) && em.HasComponent<CCueOnActivate>(ge))
                cues = em.GetComponentData<CCueOnActivate>(ge).cues;
            else if (componentType == typeof(CCueOnTick) && em.HasComponent<CCueOnTick>(ge))
                cues = em.GetComponentData<CCueOnTick>(ge).cues;
            else if (componentType == typeof(CCueOnDeactivate) && em.HasComponent<CCueOnDeactivate>(ge))
                cues = em.GetComponentData<CCueOnDeactivate>(ge).cues;
            else if (componentType == typeof(CCueOnRemove) && em.HasComponent<CCueOnRemove>(ge))
                cues = em.GetComponentData<CCueOnRemove>(ge).cues;

            return cues.IsCreated;
        }

        private static bool HasFixedTag(EntityManager em, Entity target, int tagIndex)
        {
            return em.HasComponent<CFixedTagMask>(target)
                   && em.GetComponentData<CFixedTagMask>(target).Mask.HasTag(tagIndex);
        }

        private static bool HasTemporarySourceForTag(DynamicBuffer<BTempTagSource> sources, int tagIndex)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                if (sources[i].TagIndex == tagIndex)
                    return true;
            }

            return false;
        }

        private static void EnqueueGameplayEvent(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            EGameplayEventType type,
            int eventCode = 0,
            float value = 0f,
            int reasonCode = 0)
        {
            EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
            {
                Type = type,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = eventCode,
                ReasonCode = reasonCode,
                Value = value,
            });
        }

        private static void EnqueueAttributeChangeEvent(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            int attrSetCode,
            int attributeCode,
            float oldValue,
            float newValue,
            bool isBaseValue)
        {
            EventBusHelper.EnqueueAttributeChangeEvent(em, GASManager.EntityEventBus, new BAttributeChangeEvent
            {
                ASC = context.TargetAsc,
                SourceAsc = context.SourceAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                EventCode = em.Exists(ge) && em.HasComponent<CEffectSpecData>(ge)
                    ? em.GetComponentData<CEffectSpecData>(ge).GameplayEffectCode
                    : 0,
                AttrSetCode = attrSetCode,
                AttributeCode = attributeCode,
                OldValue = oldValue,
                NewValue = newValue,
                ContextId = context.ContextId,
                IsBaseValue = isBaseValue,
            });
        }

        private static void EnqueueTagChangeEvent(
            EntityManager em,
            Entity asc,
            int tagIndex,
            bool added)
        {
            EventBusHelper.EnqueueTagChangeEvent(em, GASManager.EntityEventBus, new BTagChangeEvent
            {
                ASC = asc,
                TagIndex = tagIndex,
                Added = added,
            });
        }

    }
}

