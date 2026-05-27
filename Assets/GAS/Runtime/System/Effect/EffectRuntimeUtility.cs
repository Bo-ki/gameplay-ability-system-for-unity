using Unity.Entities;

namespace GAS.Runtime
{
    internal static class EffectRuntimeUtility
    {
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

        public static Entity ApplyInstantEffect(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc,
            Entity sourceAbility = default,
            Entity instigator = default)
        {
            return Entity.Null;
        }

        public static void ApplyInactiveDurationEffect(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc) { }

        public static bool HasOngoingRequirements(EntityManager em, Entity ge)
        {
            return false;
        }

        public static bool TryMergeStackingApplication(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc,
            out bool wasMerged)
        {
            wasMerged = false;
            return false;
        }

        public static void HandleDurationExpired(
            EntityManager em,
            Entity ge) { }

        public static void ActivateDurationEffect(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc) { }

        public static void DeactivateOngoingEffect(
            EntityManager em,
            Entity ge) { }

        public static void ReactivateOngoingEffect(
            EntityManager em,
            Entity ge) { }

        public static void MarkEffectForRemoval(
            EntityManager em,
            Entity ge,
            int reasonCode = 0)
        {
            if (ge == Entity.Null || !em.Exists(ge))
                return;

            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var context = em.HasComponent<CEffectContext>(ge)
                ? em.GetComponentData<CEffectContext>(ge)
                : default;

            if (context.TargetAsc != Entity.Null)
            {
                ActiveEffectStore.TryMarkPendingRemove(em, ge, in context, frame);
                ActiveEffectStore.TryRecordLifecycleCleanup(
                    em,
                    ge,
                    in context,
                    EGameplayEffectLifecycleState.Active,
                    frame);
            }

            var lifecycle = em.HasComponent<CEffectLifecycle>(ge)
                ? em.GetComponentData<CEffectLifecycle>(ge)
                : default;
            lifecycle.PreviousState = lifecycle.State;
            lifecycle.State = EGameplayEffectLifecycleState.PendingRemove;
            lifecycle.StateStartFrame = frame;
            if (em.HasComponent<CEffectLifecycle>(ge))
                em.SetComponentData(ge, lifecycle);
            else
                em.AddComponentData(ge, lifecycle);

            if (!em.HasComponent<CEffectDestroy>(ge))
                em.AddComponent<CEffectDestroy>(ge);
        }

        public static void CleanupActiveEffect(
            EntityManager em,
            Entity ge)
        {
            var eventBusEntity = GASManager.IsInitialized
                ? GASManager.EntityEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            try
            {
                CleanupActiveEffect(em, ge, ref eventWriter);
            }
            finally
            {
                eventWriter.Dispose();
            }
        }

        public static void CleanupActiveEffect(
            EntityManager em,
            Entity ge,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (ge == Entity.Null || !em.Exists(ge))
                return;

            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!TryGetContext(em, ge, out var context))
            {
                if (!em.HasComponent<CEffectFinalDestroy>(ge))
                    em.AddComponent<CEffectFinalDestroy>(ge);
                return;
            }

            ActiveEffectStore.TryRecordLifecycleCleanup(
                em,
                ge,
                in context,
                ResolveCleanupState(em, ge),
                frame,
                out var cleanupSequence);

            RemoveActiveModifiersForEffect(em, context.TargetAsc, ge);
            RemoveGrantedTagsForEffect(em, context.TargetAsc, ge, ref eventWriter);
            RemoveTargetEffect(em, context.TargetAsc, ge);
            ActiveEffectStore.TryRemove(em, ge, in context, frame);

            if (cleanupSequence > 0)
                ActiveEffectStore.TryResolveLifecycleCleanupBeforeEntityDestroy(
                    em,
                    context.TargetAsc,
                    cleanupSequence,
                    frame);

            EnqueueRemovedEvent(ref eventWriter, em, ge, in context);

            if (!em.HasComponent<CEffectFinalDestroy>(ge))
                em.AddComponent<CEffectFinalDestroy>(ge);
        }

        public static bool ShouldReject(
            EntityManager em,
            Entity ge,
            Entity target,
            Entity sourceAsc)
        {
            return false;
        }

        public static void RemoveActiveGameplayEffectsWithTags(
            EntityManager em,
            Entity target,
            int tagIndex)
        {
            if (target == Entity.Null
                || !em.Exists(target)
                || !em.HasBuffer<BTempTagSource>(target))
            {
                return;
            }

            var sources = em.GetBuffer<BTempTagSource>(target);
            for (var i = sources.Length - 1; i >= 0; i--)
            {
                if (sources[i].TagIndex != tagIndex)
                    continue;

                var effect = sources[i].Source;
                if (effect != Entity.Null && em.Exists(effect))
                    MarkEffectForRemoval(em, effect);
            }
        }

        public static void FinalizeEffectDestroy(
            EntityManager em,
            Entity ge)
        {
            if (ge != Entity.Null && em.Exists(ge))
                em.DestroyEntity(ge);
        }

        public static void ApplyResolvedModifiersAsActive(
            EntityManager em,
            Entity ge,
            in CEffectContext context,
            in CEffectSpecData spec,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (context.TargetAsc == Entity.Null || !em.Exists(context.TargetAsc))
                return;

            EffectMagnitudeResolver.ResolveModifiers(em, ge, in context, in spec, ref eventWriter);
            if (!em.HasBuffer<BResolvedModifier>(ge))
                return;

            if (!em.HasBuffer<BActiveModifier>(context.TargetAsc))
                em.AddBuffer<BActiveModifier>(context.TargetAsc);

            var resolved = em.GetBuffer<BResolvedModifier>(ge);
            var active = em.GetBuffer<BActiveModifier>(context.TargetAsc);
            var attributes = em.HasBuffer<BAttribute>(context.TargetAsc)
                ? em.GetBuffer<BAttribute>(context.TargetAsc)
                : default;

            for (var i = 0; i < resolved.Length; i++)
            {
                var modifier = resolved[i];
                if (HasActiveModifier(active, ge, modifier.AttrSetCode, modifier.AttributeCode, modifier.Op))
                    continue;

                active.Add(new BActiveModifier
                {
                    AttrSetCode = modifier.AttrSetCode,
                    AttributeCode = modifier.AttributeCode,
                    SourceEntity = ge,
                    Magnitude = modifier.Magnitude,
                    Op = modifier.Op,
                });

                if (attributes.IsCreated)
                    AttributeHelper.MarkCurrentValueDirty(
                        attributes,
                        modifier.AttrSetCode,
                        modifier.AttributeCode);
            }
        }

        public static void RemoveActiveModifiersForEffect(
            EntityManager em,
            Entity owner,
            Entity ge)
        {
            if (owner == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<BActiveModifier>(owner))
            {
                return;
            }

            var modifiers = em.GetBuffer<BActiveModifier>(owner);
            var attributes = em.HasBuffer<BAttribute>(owner)
                ? em.GetBuffer<BAttribute>(owner)
                : default;

            for (var i = modifiers.Length - 1; i >= 0; i--)
            {
                var modifier = modifiers[i];
                if (modifier.SourceEntity != ge)
                    continue;

                modifiers.RemoveAt(i);
                if (attributes.IsCreated)
                    AttributeHelper.MarkCurrentValueDirty(
                        attributes,
                        modifier.AttrSetCode,
                        modifier.AttributeCode);
            }
        }

        public static void RemoveGrantedTagsForEffect(
            EntityManager em,
            Entity owner,
            Entity ge,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (owner == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<BTempTagSource>(owner))
            {
                return;
            }

            var sources = em.GetBuffer<BTempTagSource>(owner);
            for (var i = sources.Length - 1; i >= 0; i--)
            {
                var source = sources[i];
                if (source.Source != ge)
                    continue;

                sources.RemoveAt(i);
                TagRuntimeUtility.RemoveTagIndexFromEffectiveMaskIfUnreferenced(em, owner, source.TagIndex);
                if (eventWriter.IsCreated)
                {
                    eventWriter.EnqueueTagChangeEvent(new BTagChangeEvent
                    {
                        ASC = owner,
                        TagIndex = source.TagIndex,
                        Added = false,
                    });
                }
            }
        }

        public static void RemoveTargetEffect(
            EntityManager em,
            Entity owner,
            Entity ge)
        {
            if (owner == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<BGameplayEffect>(owner))
            {
                return;
            }

            var effects = em.GetBuffer<BGameplayEffect>(owner);
            for (var i = effects.Length - 1; i >= 0; i--)
            {
                if (effects[i].GameplayEffect == ge)
                    effects.RemoveAt(i);
            }
        }

        public static bool TryGetContext(
            EntityManager em,
            Entity ge,
            out CEffectContext context)
        {
            if (ge != Entity.Null && em.Exists(ge) && em.HasComponent<CEffectContext>(ge))
            {
                context = em.GetComponentData<CEffectContext>(ge);
                return context.TargetAsc != Entity.Null;
            }

            context = default;
            return false;
        }

        private static bool HasActiveModifier(
            DynamicBuffer<BActiveModifier> active,
            Entity ge,
            int attrSetCode,
            int attributeCode,
            EModifierOp op)
        {
            for (var i = 0; i < active.Length; i++)
            {
                var modifier = active[i];
                if (modifier.SourceEntity == ge
                    && modifier.AttrSetCode == attrSetCode
                    && modifier.AttributeCode == attributeCode
                    && modifier.Op == op)
                {
                    return true;
                }
            }

            return false;
        }

        private static EGameplayEffectLifecycleState ResolveCleanupState(EntityManager em, Entity ge)
        {
            if (em.HasComponent<CEffectLifecycle>(ge))
                return em.GetComponentData<CEffectLifecycle>(ge).State;

            return EGameplayEffectLifecycleState.Active;
        }

        private static void EnqueueRemovedEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            EntityManager em,
            Entity ge,
            in CEffectContext context)
        {
            if (!writer.IsCreated)
                return;

            var gameplayEffectCode = 0;
            if (em.HasComponent<CEffectSpecData>(ge))
                gameplayEffectCode = em.GetComponentData<CEffectSpecData>(ge).GameplayEffectCode;

            writer.EnqueueGameplayEvent(new BGameplayEvent
            {
                Type = EGameplayEventType.GameplayEffectRemoved,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = gameplayEffectCode,
            });
        }
    }
}
