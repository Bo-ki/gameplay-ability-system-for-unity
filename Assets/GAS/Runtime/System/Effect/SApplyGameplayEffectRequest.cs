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
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var currentFrame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            var requests = _query.ToEntityArray(Allocator.Temp);
            var hasEventBus = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity);

            foreach (var requestEntity in requests)
            {
                if (!em.Exists(requestEntity) || !em.HasComponent<CApplyGameplayEffectRequest>(requestEntity))
                    continue;

                var request = em.GetComponentData<CApplyGameplayEffectRequest>(requestEntity);
                if (IsUnavailableAsc(em, request.SourceAsc))
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                var targetKind = GetTargetDataKind(em, requestEntity);
                var targets = CopyTargets(em, requestEntity, request.SourceAsc);
                var setByCallers = CopySetByCallerValues(em, requestEntity);

                for (var i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    if (target == Entity.Null || IsUnavailableAsc(em, target))
                        continue;

                    var ge = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(
                        em,
                        request.GameplayEffectCode,
                        new ConfigRegistryReferenceContext(
                            ConfigRegistryConfigKind.None,
                            0,
                            ConfigRegistryReferenceKind.ApplyGameplayEffectRequest));
                    if (ge == Entity.Null)
                        continue;

                    var context = CreateContext(request, target, targetKind);
                    var spec = CreateSpec(em, ge, request.GameplayEffectCode, request.Level, request.DurationFrameOverride);

                    SetOrAdd(em, ref ecb, ge, context);
                    SetOrAdd(em, ref ecb, ge, spec);
                    InitializeDurationRuntime(em, ref ecb, ge, spec);
                    SetOrAdd(em, ref ecb, ge, new CEffectLifecycle
                    {
                        State = EGameplayEffectLifecycleState.PendingApply,
                        PreviousState = EGameplayEffectLifecycleState.PendingApply,
                        StateStartFrame = currentFrame,
                    });

                    if (context.SourceAbility != Entity.Null)
                    {
                        SetOrAdd(em, ref ecb, ge, new CCreatedByAbility
                        {
                            sourceAbility = context.SourceAbility,
                        });
                    }

                    CopySetByCallerValuesToEffect(em, ref ecb, ge, setByCallers);
                    CopyTargetDataSummaryToEffect(em, ref ecb, requestEntity, ge);
                    PlaybackAndReset(ref ecb, em);

                    EffectMagnitudeResolver.ResolveModifiers(em, ref ecb, ge, context, spec);
                    EnqueueEffectInstancedEvent(em, hasEventBus, eventBusEntity, ge, context, request.GameplayEffectCode);
                }

                targets.Dispose();
                if (setByCallers.IsCreated)
                    setByCallers.Dispose();

                ecb.DestroyEntity(requestEntity);
            }

            ecb.Playback(em);
            ecb.Dispose();
            requests.Dispose();
        }

        private CEffectContext CreateContext(
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetKind)
        {
            if (_nextContextId <= 0)
                _nextContextId = 1;

            return new CEffectContext
            {
                SourceAsc = request.SourceAsc,
                TargetAsc = targetAsc,
                SourceAbility = request.SourceAbility,
                SourceEffect = request.SourceEffect,
                Instigator = request.Instigator != Entity.Null ? request.Instigator : request.SourceAsc,
                Causer = request.Causer != Entity.Null ? request.Causer : request.SourceAbility,
                ContextId = _nextContextId++,
                ParentContextId = request.ParentContextId,
                TargetDataKind = targetKind,
            };
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
            ref EntityCommandBuffer ecb,
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
                ecb.SetComponent(ge, runtime);
            else
                ecb.AddComponent(ge, runtime);
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

        private static NativeArray<Entity> CopyTargets(EntityManager em, Entity requestEntity, Entity fallbackTarget)
        {
            if (!em.HasBuffer<BTargetEntity>(requestEntity))
                return CreateFallbackTargets(fallbackTarget);

            var buffer = em.GetBuffer<BTargetEntity>(requestEntity);
            if (buffer.Length == 0)
                return CreateFallbackTargets(fallbackTarget);

            var targets = new NativeArray<Entity>(buffer.Length, Allocator.Temp);
            for (var i = 0; i < buffer.Length; i++)
                targets[i] = buffer[i].TargetAsc;

            return targets;
        }

        private static NativeArray<Entity> CreateFallbackTargets(Entity fallbackTarget)
        {
            var targets = new NativeArray<Entity>(1, Allocator.Temp);
            targets[0] = fallbackTarget;
            return targets;
        }

        private static bool IsUnavailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null && (!em.Exists(asc) || em.HasComponent<CAscDestroying>(asc));
        }

        private static NativeArray<BSetByCallerValue> CopySetByCallerValues(EntityManager em, Entity requestEntity)
        {
            if (!em.HasBuffer<BSetByCallerValue>(requestEntity))
                return default;

            var buffer = em.GetBuffer<BSetByCallerValue>(requestEntity);
            if (buffer.Length == 0)
                return default;

            var values = new NativeArray<BSetByCallerValue>(buffer.Length, Allocator.Temp);
            for (var i = 0; i < buffer.Length; i++)
                values[i] = buffer[i];

            return values;
        }

        private static void CopySetByCallerValuesToEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
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

            var ecbBuffer = ecb.AddBuffer<BSetByCallerValue>(ge);
            for (var i = 0; i < values.Length; i++)
                ecbBuffer.Add(values[i]);
        }

        private static void CopyTargetDataSummaryToEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity requestEntity,
            Entity ge)
        {
            CopyTargetPointsToEffect(em, ref ecb, requestEntity, ge);
            CopyTargetDirectionsToEffect(em, ref ecb, requestEntity, ge);
            CopyTargetHitsToEffect(em, ref ecb, requestEntity, ge);
        }

        private static void CopyTargetPointsToEffect(
            EntityManager em,
            ref EntityCommandBuffer ecb,
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
                : ecb.AddBuffer<BEffectTargetPoint>(ge);

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
            ref EntityCommandBuffer ecb,
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
                : ecb.AddBuffer<BEffectTargetDirection>(ge);

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
            ref EntityCommandBuffer ecb,
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
                : ecb.AddBuffer<BEffectTargetHit>(ge);

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
            ref EntityCommandBuffer ecb,
            Entity entity,
            T component)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                ecb.SetComponent(entity, component);
            else
                ecb.AddComponent(entity, component);
        }

        private static void PlaybackAndReset(ref EntityCommandBuffer ecb, EntityManager em)
        {
            ecb.Playback(em);
            ecb.Dispose();
            ecb = new EntityCommandBuffer(Allocator.Temp);
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
