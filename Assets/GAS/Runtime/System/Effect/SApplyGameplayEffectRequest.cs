using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 消费 Ability / 外部命令写入的 GE 施加请求，创建带上下文的 GE instance。
    /// </summary>
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SAbilityCommit))]
    public partial struct SApplyGameplayEffectRequest : ISystem
    {
        private EntityQuery _query;
        private int _nextContextId;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CApplyGameplayEffectRequest>()
                .Build();
            _nextContextId = 1;
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var currentFrame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            var requests = _query.ToEntityArray(Allocator.Temp);
            var hasEventBus = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity);
            using var gameplayEventBatch = hasEventBus
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            foreach (var requestEntity in requests)
            {
                if (!em.Exists(requestEntity) || !em.HasComponent<CApplyGameplayEffectRequest>(requestEntity))
                    continue;

                var request = em.GetComponentData<CApplyGameplayEffectRequest>(requestEntity);
                if (IsUnavailableAsc(em, request.SourceAsc))
                {
                    em.DestroyEntity(requestEntity);
                    continue;
                }

                var targetKind = GetTargetDataKind(em, requestEntity);
                ProcessTargets(
                    em,
                    requestEntity,
                    request,
                    targetKind,
                    currentFrame,
                    hasEventBus,
                    eventBusEntity);

                em.DestroyEntity(requestEntity);
            }

            requests.Dispose();
        }

        private CEffectContext CreateContext(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetKind,
            bool hasEventBus,
            Entity eventBusEntity)
        {
            var contextId = hasEventBus
                ? EventBusHelper.AllocateGameplayEffectContextId(em, eventBusEntity)
                : AllocateLocalContextId();

            return new CEffectContext
            {
                SourceAsc = request.SourceAsc,
                TargetAsc = targetAsc,
                SourceAbility = request.SourceAbility,
                SourceEffect = request.SourceEffect,
                Instigator = request.Instigator != Entity.Null ? request.Instigator : request.SourceAsc,
                Causer = request.Causer != Entity.Null ? request.Causer : request.SourceAbility,
                ContextId = contextId,
                ParentContextId = request.ParentContextId,
                TargetDataKind = targetKind,
            };
        }

        private int AllocateLocalContextId()
        {
            if (_nextContextId <= 0)
                _nextContextId = 1;

            return _nextContextId++;
        }

        private void ProcessTargets(
            EntityManager em,
            Entity requestEntity,
            in CApplyGameplayEffectRequest request,
            ETargetDataKind targetKind,
            int currentFrame,
            bool hasEventBus,
            Entity eventBusEntity)
        {
            if (!em.HasBuffer<BTargetEntity>(requestEntity))
            {
                ProcessTarget(
                    em,
                    requestEntity,
                    request,
                    targetKind,
                    request.SourceAsc,
                    currentFrame,
                    hasEventBus,
                    eventBusEntity);
                return;
            }

            var targets = em.GetBuffer<BTargetEntity>(requestEntity);
            if (targets.Length == 0)
            {
                ProcessTarget(
                    em,
                    requestEntity,
                    request,
                    targetKind,
                    request.SourceAsc,
                    currentFrame,
                    hasEventBus,
                    eventBusEntity);
                return;
            }

            var targetCount = targets.Length;
            if (targetCount == 1)
            {
                var target = targets[0].TargetAsc;
                ProcessTarget(
                    em,
                    requestEntity,
                    request,
                    targetKind,
                    target,
                    currentFrame,
                    hasEventBus,
                    eventBusEntity);
                return;
            }

            var targetSnapshot = new NativeArray<Entity>(targetCount, Allocator.Temp);
            try
            {
                for (var i = 0; i < targetCount; i++)
                    targetSnapshot[i] = targets[i].TargetAsc;

                for (var i = 0; i < targetSnapshot.Length; i++)
                {
                    ProcessTarget(
                        em,
                        requestEntity,
                        request,
                        targetKind,
                        targetSnapshot[i],
                        currentFrame,
                        hasEventBus,
                        eventBusEntity);
                }
            }
            finally
            {
                targetSnapshot.Dispose();
            }
        }

        private void ProcessTarget(
            EntityManager em,
            Entity requestEntity,
            in CApplyGameplayEffectRequest request,
            ETargetDataKind targetKind,
            Entity target,
            int currentFrame,
            bool hasEventBus,
            Entity eventBusEntity)
        {
            if (target == Entity.Null || IsUnavailableAsc(em, target))
                return;

            var context = CreateContext(em, request, target, targetKind, hasEventBus, eventBusEntity);
            if (TryApplyFastInstantModifier(
                    em,
                    requestEntity,
                    request,
                    context,
                    hasEventBus,
                    eventBusEntity,
                    false,
                    default))
            {
                return;
            }

            var ge = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(
                em,
                request.GameplayEffectCode,
                new ConfigRegistryReferenceContext(
                    ConfigRegistryConfigKind.None,
                    0,
                    ConfigRegistryReferenceKind.ApplyGameplayEffectRequest));
            if (ge == Entity.Null)
                return;

            var spec = CreateSpec(em, ge, request.GameplayEffectCode, request.Level, request.DurationFrameOverride);

            SetOrAdd(em, ge, context);
            SetOrAdd(em, ge, spec);
            InitializeDurationRuntime(em, ge, spec);
            SetOrAdd(em, ge, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.PendingApply,
                PreviousState = EGameplayEffectLifecycleState.PendingApply,
                StateStartFrame = currentFrame,
            });

            if (context.SourceAbility != Entity.Null)
            {
                SetOrAdd(em, ge, new CCreatedByAbility
                {
                    sourceAbility = context.SourceAbility,
                });
            }

            CopySetByCallerValuesToEffect(em, requestEntity, ge);
            CopyTargetDataSummaryToEffect(em, requestEntity, ge);
            EffectMagnitudeResolver.ResolveModifiers(em, ge, context, spec);
            EnqueueEffectInstancedEvent(em, hasEventBus, eventBusEntity, ge, context, request.GameplayEffectCode);
        }

        internal static bool TryApplyFastInstantModifierDirect(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity target,
            ETargetDataKind targetKind)
        {
            return TryApplyFastInstantModifierDirect(
                em,
                request,
                target,
                targetKind,
                false,
                default);
        }

        internal static bool TryApplyFastInstantModifierDirect(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity target,
            ETargetDataKind targetKind,
            in BSetByCallerValue setByCallerValue)
        {
            return TryApplyFastInstantModifierDirect(
                em,
                request,
                target,
                targetKind,
                true,
                setByCallerValue);
        }

        private static bool TryApplyFastInstantModifierDirect(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity target,
            ETargetDataKind targetKind,
            bool hasDirectSetByCaller,
            in BSetByCallerValue directSetByCaller)
        {
            var eventBusEntity = GASManager.EntityEventBus;
            if (eventBusEntity == Entity.Null
                || !em.Exists(eventBusEntity)
                || !em.HasComponent<CGameplayEventBus>(eventBusEntity)
                || target == Entity.Null
                || IsUnavailableAsc(em, target)
                || IsUnavailableAsc(em, request.SourceAsc))
            {
                return false;
            }

            if (!TryPrepareFastInstantModifiers(
                    em,
                    Entity.Null,
                    request,
                    true,
                    hasDirectSetByCaller,
                    directSetByCaller,
                    out var blob))
            {
                return false;
            }

            var context = CreateContext(em, request, target, targetKind, eventBusEntity);
            ref var definition = ref blob.Value;
            ApplyPreparedFastInstantModifiers(
                em,
                request,
                context,
                eventBusEntity,
                ref definition,
                Entity.Null,
                hasDirectSetByCaller,
                directSetByCaller);
            return true;
        }

        private static CEffectContext CreateContext(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetKind,
            Entity eventBusEntity)
        {
            return new CEffectContext
            {
                SourceAsc = request.SourceAsc,
                TargetAsc = targetAsc,
                SourceAbility = request.SourceAbility,
                SourceEffect = request.SourceEffect,
                Instigator = request.Instigator != Entity.Null ? request.Instigator : request.SourceAsc,
                Causer = request.Causer != Entity.Null ? request.Causer : request.SourceAbility,
                ContextId = EventBusHelper.AllocateGameplayEffectContextId(em, eventBusEntity),
                ParentContextId = request.ParentContextId,
                TargetDataKind = targetKind,
            };
        }

        private static bool TryApplyFastInstantModifier(
            EntityManager em,
            Entity requestEntity,
            in CApplyGameplayEffectRequest request,
            in CEffectContext context,
            bool hasEventBus,
            Entity eventBusEntity,
            bool hasDirectSetByCaller,
            in BSetByCallerValue directSetByCaller)
        {
            if (!TryPrepareFastInstantModifiers(
                    em,
                    requestEntity,
                    request,
                    hasEventBus,
                    hasDirectSetByCaller,
                    directSetByCaller,
                    out var blob))
            {
                return false;
            }

            ref var definition = ref blob.Value;
            ApplyPreparedFastInstantModifiers(
                em,
                request,
                context,
                eventBusEntity,
                ref definition,
                requestEntity,
                hasDirectSetByCaller,
                directSetByCaller);
            return true;
        }

        private static bool TryPrepareFastInstantModifiers(
            EntityManager em,
            Entity requestEntity,
            in CApplyGameplayEffectRequest request,
            bool hasEventBus,
            bool hasDirectSetByCaller,
            in BSetByCallerValue directSetByCaller,
            out BlobAssetReference<GEStaticDefinitionBlob> blob)
        {
            if (!hasEventBus
                || request.DurationFrameOverride > 0
                || !GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                    em,
                    request.GameplayEffectCode,
                    out blob,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        request.GameplayEffectCode,
                        ConfigRegistryReferenceKind.ApplyGameplayEffectRequest)))
            {
                blob = default;
                return false;
            }

            ref var definition = ref blob.Value;
            if (!CanApplyFastInstantModifiers(ref definition))
                return false;

            for (var i = 0; i < definition.Modifiers.Length; i++)
            {
                if (!CanResolveFastModifierMagnitude(in definition.Modifiers[i]))
                    return false;
            }

            return true;
        }

        private static void ApplyPreparedFastInstantModifiers(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            in CEffectContext context,
            Entity eventBusEntity,
            ref GEStaticDefinitionBlob definition,
            Entity requestEntity,
            bool hasDirectSetByCaller,
            in BSetByCallerValue directSetByCaller)
        {
            EnqueueEffectInstancedEvent(
                em,
                true,
                eventBusEntity,
                Entity.Null,
                context,
                request.GameplayEffectCode);
            EnqueueFastCueRequestOnApply(em, eventBusEntity, context, ref definition);
            ApplyFastInstantModifiers(
                em,
                eventBusEntity,
                context,
                ref definition,
                requestEntity,
                hasDirectSetByCaller,
                directSetByCaller,
                request.GameplayEffectCode);
            EnqueueFastGameplayEffectAppliedEvent(em, eventBusEntity, context, request.GameplayEffectCode);
        }

        private static bool CanApplyFastInstantModifiers(ref GEStaticDefinitionBlob definition)
        {
            if (definition.HasDuration
                || definition.HasPeriod
                || definition.HasStacking
                || definition.HasApplicationRequiredTags
                || definition.HasOngoingRequiredTags
                || definition.HasRemoveGameplayEffectsWithTags
                || definition.HasImmunityTags
                || !definition.GrantedTags.IsEmpty
                || definition.GrantedAbilities.Length != 0
                || definition.Modifiers.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < definition.Modifiers.Length; i++)
            {
                if (!CanResolveFastModifierMagnitude(in definition.Modifiers[i]))
                    return false;
            }

            return true;
        }

        private static bool CanResolveFastModifierMagnitude(in GEModifierDefinition modifier)
        {
            return modifier.MagnitudeSource == EMagnitudeSource.Constant
                   || modifier.MagnitudeSource == EMagnitudeSource.SetByCaller;
        }

        private static bool TryResolveFastModifierMagnitude(
            EntityManager em,
            Entity requestEntity,
            bool hasDirectSetByCaller,
            in BSetByCallerValue directSetByCaller,
            in GEModifierDefinition modifier,
            out float magnitude)
        {
            switch (modifier.MagnitudeSource)
            {
                case EMagnitudeSource.Constant:
                    magnitude = ApplyFastMagnitudeTransform(modifier.Magnitude, in modifier);
                    return true;
                case EMagnitudeSource.SetByCaller:
                    var value = modifier.FallbackMagnitude;
                    if (hasDirectSetByCaller && directSetByCaller.Key == modifier.MagnitudeKey)
                    {
                        value = directSetByCaller.Value;
                    }
                    else if (requestEntity != Entity.Null
                             && EffectMagnitudeResolver.TryGetSetByCallerValue(
                                 em,
                                 requestEntity,
                                 modifier.MagnitudeKey,
                                 out var requestValue))
                    {
                        value = requestValue;
                    }

                    magnitude = ApplyFastMagnitudeTransform(value, in modifier);
                    return true;
                default:
                    magnitude = 0f;
                    return false;
            }
        }

        private static float ApplyFastMagnitudeTransform(float rawMagnitude, in GEModifierDefinition modifier)
        {
            var coefficient = modifier.Coefficient == 0f ? 1f : modifier.Coefficient;
            return ((rawMagnitude + modifier.PreAdd) * coefficient) + modifier.PostAdd;
        }

        private static void ApplyFastInstantModifiers(
            EntityManager em,
            Entity eventBusEntity,
            in CEffectContext context,
            ref GEStaticDefinitionBlob definition,
            Entity requestEntity,
            bool hasDirectSetByCaller,
            in BSetByCallerValue directSetByCaller,
            int gameplayEffectCode)
        {
            var target = context.TargetAsc;
            if (!em.HasBuffer<BAttribute>(target))
                return;

            var attributes = em.GetBuffer<BAttribute>(target);

            for (var i = 0; i < definition.Modifiers.Length; i++)
            {
                var modifier = definition.Modifiers[i];
                if (!TryResolveFastModifierMagnitude(
                    em,
                    requestEntity,
                    hasDirectSetByCaller,
                    directSetByCaller,
                    in modifier,
                    out var magnitude))
                {
                    continue;
                }

                var attrIndex = attributes.IndexOfAttribute(modifier.AttrSetCode, modifier.AttributeCode);
                if (attrIndex == -1)
                    continue;

                var attribute = attributes[attrIndex];
                var oldValue = attribute.BaseValue;
                var oldCurrentValue = attribute.CurrentValue;
                var newValue = AttributeHelper.ApplyModifier(attribute.BaseValue, modifier.Operation, magnitude);
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

                    EventBusHelper.EnqueueAttributeChangeEvent(em, eventBusEntity, new BAttributeChangeEvent
                    {
                        ASC = target,
                        SourceAsc = context.SourceAsc,
                        SourceAbility = context.SourceAbility,
                        GameplayEffect = Entity.Null,
                        EventCode = gameplayEffectCode,
                        AttrSetCode = modifier.AttrSetCode,
                        AttributeCode = modifier.AttributeCode,
                        OldValue = oldValue,
                        NewValue = newValue,
                        ContextId = context.ContextId,
                        IsBaseValue = true,
                    });
                }

                attributes[attrIndex] = attribute;
            }
        }

        private static void EnqueueFastCueRequestOnApply(
            EntityManager em,
            Entity eventBusEntity,
            in CEffectContext context,
            ref GEStaticDefinitionBlob definition)
        {
            if (!definition.HasCueRequestOnApply)
                return;

            EventBusHelper.EnqueueCueRequest(em, eventBusEntity, new BCueRequest
            {
                TargetAsc = context.TargetAsc,
                SourceAsc = context.SourceAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = Entity.Null,
                SourceEntity = context.SourceAbility,
                SourceType = CueSourceType.GameplayEffect,
                CueEntity = Entity.Null,
                ContextId = context.ContextId,
                CueEvent = EGameplayCueEvent.OnApply,
            });

            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.CueRequested,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                ContextId = context.ContextId,
                EventCode = (int)EGameplayCueEvent.OnApply,
                ReasonCode = definition.CueRequestOnApplyCode,
            });
        }

        private static void EnqueueFastGameplayEffectAppliedEvent(
            EntityManager em,
            Entity eventBusEntity,
            in CEffectContext context,
            int gameplayEffectCode)
        {
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.GameplayEffectApplied,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                ContextId = context.ContextId,
                EventCode = gameplayEffectCode,
            });
        }

        private static CEffectSpecData CreateSpec(
            EntityManager em,
            Entity ge,
            int gameplayEffectCode,
            int level,
            int durationFrameOverride)
        {
            return new CEffectSpecData
            {
                GameplayEffectCode = gameplayEffectCode,
                Level = level,
                StackCount = GetStackCount(em, ge),
                DurationFrameOverride = durationFrameOverride,
            };
        }

        private static int GetStackCount(EntityManager em, Entity ge)
        {
            if (!em.HasComponent<CStackingRuntime>(ge))
                return 1;

            var stackCount = em.GetComponentData<CStackingRuntime>(ge).StackCount;
            return stackCount > 0 ? stackCount : 1;
        }

        private static void InitializeDurationRuntime(
            EntityManager em,
            Entity ge,
            in CEffectSpecData spec)
        {
            if (!em.HasComponent<CDurationDefinition>(ge))
                return;

            var definition = em.GetComponentData<CDurationDefinition>(ge);
            var runtime = em.HasComponent<CDurationRuntime>(ge)
                ? em.GetComponentData<CDurationRuntime>(ge)
                : default;

            runtime.ResolvedDuration = spec.DurationFrameOverride > 0
                ? spec.DurationFrameOverride
                : definition.Duration;
            runtime.ResolvedTimeUnit = spec.DurationFrameOverride > 0
                ? TimeUnit.Frame
                : definition.TimeUnit;

            if (em.HasComponent<CDurationRuntime>(ge))
                em.SetComponentData(ge, runtime);
            else
                em.AddComponentData(ge, runtime);
        }

        private static ETargetDataKind GetTargetDataKind(EntityManager em, Entity requestEntity)
        {
            if (em.HasComponent<CTargetDataHeader>(requestEntity))
                return em.GetComponentData<CTargetDataHeader>(requestEntity).Kind;

            if (!em.HasBuffer<BTargetEntity>(requestEntity))
                return ETargetDataKind.Self;

            return em.GetBuffer<BTargetEntity>(requestEntity).Length > 1
                ? ETargetDataKind.EntityList
                : ETargetDataKind.Entity;
        }

        private static bool IsUnavailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null && (!em.Exists(asc) || em.HasComponent<CAscDestroying>(asc));
        }

        private static void CopySetByCallerValuesToEffect(
            EntityManager em,
            Entity requestEntity,
            Entity ge)
        {
            if (!em.HasBuffer<BSetByCallerValue>(requestEntity))
                return;

            var sourceLength = em.GetBuffer<BSetByCallerValue>(requestEntity).Length;
            if (sourceLength == 0)
                return;

            var target = em.HasBuffer<BSetByCallerValue>(ge)
                ? em.GetBuffer<BSetByCallerValue>(ge)
                : em.AddBuffer<BSetByCallerValue>(ge);
            var source = em.GetBuffer<BSetByCallerValue>(requestEntity);
            for (var i = 0; i < sourceLength; i++)
                target.Add(source[i]);
        }

        private static void CopySetByCallerValuesToEffect(
            EntityManager em,
            Entity ge,
            NativeArray<BSetByCallerValue> values)
        {
            if (!values.IsCreated || values.Length == 0)
                return;

            if (em.HasBuffer<BSetByCallerValue>(ge))
            {
                var buffer = em.GetBuffer<BSetByCallerValue>(ge);
                for (var i = 0; i < values.Length; i++)
                    buffer.Add(values[i]);

                return;
            }

            var ecbBuffer = em.AddBuffer<BSetByCallerValue>(ge);
            for (var i = 0; i < values.Length; i++)
                ecbBuffer.Add(values[i]);
        }

        private static void CopyTargetDataSummaryToEffect(
            EntityManager em,
            Entity requestEntity,
            Entity ge)
        {
            CopyTargetPointsToEffect(em, requestEntity, ge);
            CopyTargetDirectionsToEffect(em, requestEntity, ge);
            CopyTargetHitsToEffect(em, requestEntity, ge);
        }

        private static void CopyTargetPointsToEffect(
            EntityManager em,
            Entity requestEntity,
            Entity ge)
        {
            if (!em.HasBuffer<BTargetPoint>(requestEntity))
                return;

            var source = em.GetBuffer<BTargetPoint>(requestEntity);
            if (source.Length == 0)
                return;

            var target = em.HasBuffer<BEffectTargetPoint>(ge)
                ? em.GetBuffer<BEffectTargetPoint>(ge)
                : em.AddBuffer<BEffectTargetPoint>(ge);
            source = em.GetBuffer<BTargetPoint>(requestEntity);

            for (var i = 0; i < source.Length; i++)
            {
                target.Add(new BEffectTargetPoint
                {
                    Position = source[i].Position,
                });
            }
        }

        private static void CopyTargetDirectionsToEffect(
            EntityManager em,
            Entity requestEntity,
            Entity ge)
        {
            if (!em.HasBuffer<BTargetDirection>(requestEntity))
                return;

            var source = em.GetBuffer<BTargetDirection>(requestEntity);
            if (source.Length == 0)
                return;

            var target = em.HasBuffer<BEffectTargetDirection>(ge)
                ? em.GetBuffer<BEffectTargetDirection>(ge)
                : em.AddBuffer<BEffectTargetDirection>(ge);
            source = em.GetBuffer<BTargetDirection>(requestEntity);

            for (var i = 0; i < source.Length; i++)
            {
                target.Add(new BEffectTargetDirection
                {
                    Direction = source[i].Direction,
                });
            }
        }

        private static void CopyTargetHitsToEffect(
            EntityManager em,
            Entity requestEntity,
            Entity ge)
        {
            if (!em.HasBuffer<BTargetHit>(requestEntity))
                return;

            var source = em.GetBuffer<BTargetHit>(requestEntity);
            if (source.Length == 0)
                return;

            var target = em.HasBuffer<BEffectTargetHit>(ge)
                ? em.GetBuffer<BEffectTargetHit>(ge)
                : em.AddBuffer<BEffectTargetHit>(ge);
            source = em.GetBuffer<BTargetHit>(requestEntity);

            for (var i = 0; i < source.Length; i++)
            {
                target.Add(new BEffectTargetHit
                {
                    HitEntity = source[i].HitEntity,
                    Position = source[i].Position,
                    Normal = source[i].Normal,
                    SurfaceCode = source[i].SurfaceCode,
                });
            }
        }

        private static void EnqueueEffectInstancedEvent(
            EntityManager em,
            bool hasEventBus,
            Entity eventBusEntity,
            Entity ge,
            in CEffectContext context,
            int effectCode)
        {
            if (!hasEventBus)
                return;

            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.GameplayEffectInstanced,
                SourceAsc = context.SourceAsc,
                TargetAsc = context.TargetAsc,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = effectCode,
            });
        }

        private static void SetOrAdd<T>(
            EntityManager em,
            Entity entity,
            T component)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                em.SetComponentData(entity, component);
            else
                em.AddComponentData(entity, component);
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
