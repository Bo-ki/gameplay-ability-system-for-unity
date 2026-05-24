using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessCounterReaction))]
    [UpdateBefore(typeof(SHeadlessAutoChessSynergyProjection))]
    public partial struct SHeadlessAutoChessCleanseReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _cleanseUnitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessCleanseFacts, BHeadlessAutoChessGameplayEffectAppliedFact>()
                .Build();
            _cleanseUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessCleanseRules, CHeadlessAutoChessCleanseState>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_cleanseUnitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity)
                || !em.HasBuffer<BGameplayEvent>(eventBusEntity)
                || !em.HasBuffer<BTagChangeEvent>(eventBusEntity))
            {
                return;
            }

            using var cleansers = _cleanseUnitQuery.ToEntityArray(Allocator.Temp);
            if (_driverQuery.IsEmptyIgnoreFilter || cleansers.Length == 0)
                return;

            var driverEntity = _driverQuery.GetSingletonEntity();
            var facts = em.GetComponentData<CHeadlessAutoChessCleanseFacts>(driverEntity);
            var appliedFacts = em.GetBuffer<BHeadlessAutoChessGameplayEffectAppliedFact>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            ResetProcessedCountsIfFrameChanged(ref facts, frame);

            ProjectCleanseRequests(em, eventBusEntity, ref facts, frame);
            ProjectCleanseEffectAppliedFacts(em, eventBusEntity, appliedFacts, ref facts, frame);
            ProjectCleanseRemovedTagFacts(em, eventBusEntity, cleansers, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessCleanseFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedGameplayEventCount = 0;
            facts.ProcessedGameplayEffectAppliedFactCount = 0;
            facts.ProcessedTagEventCount = 0;
        }

        private static void ProjectCleanseRequests(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessCleanseFacts facts,
            int frame)
        {
            using var gameplayEvents = EventBusHelper.SnapshotBufferRange<BGameplayEvent>(
                em,
                eventBusEntity,
                facts.ProcessedGameplayEventCount,
                Allocator.Temp,
                out var eventCount);

            for (var i = 0; i < gameplayEvents.Length; i++)
            {
                var evt = gameplayEvents[i];
                if (evt.Type != EGameplayEventType.AutoChessCleanseRequested
                    || !TryGetCleanseRules(em, evt.SourceAsc, out _))
                {
                    continue;
                }

                var cleanseState = em.GetComponentData<CHeadlessAutoChessCleanseState>(evt.SourceAsc);
                cleanseState.CleanseRequestCount++;
                cleanseState.LastCleanseFrame = frame;
                cleanseState.LastCleanseSource = evt.SourceAsc;
                cleanseState.LastCleanseTarget = evt.TargetAsc;
                em.SetComponentData(evt.SourceAsc, cleanseState);

                facts.CleanseRequestedFactCount++;
            }

            facts.ProcessedGameplayEventCount = eventCount;
        }

        private static void ProjectCleanseEffectAppliedFacts(
            EntityManager em,
            Entity eventBusEntity,
            DynamicBuffer<BHeadlessAutoChessGameplayEffectAppliedFact> appliedFacts,
            ref CHeadlessAutoChessCleanseFacts facts,
            int frame)
        {
            var eventCount = appliedFacts.Length;
            var start = EventBusHelper.ClampProcessedCount(
                appliedFacts,
                facts.ProcessedGameplayEffectAppliedFactCount);
            using var gameplayEvents = EventBusHelper.CopyBufferRange(
                appliedFacts,
                start,
                eventCount,
                Allocator.Temp);

            for (var i = 0; i < gameplayEvents.Length; i++)
            {
                var evt = gameplayEvents[i];
                if (!TryGetCleanseRules(em, evt.SourceAsc, out var rules))
                {
                    continue;
                }

                if (IsCleanseAppliedEvent(em, evt, rules))
                {
                    var cleanseState = em.GetComponentData<CHeadlessAutoChessCleanseState>(evt.SourceAsc);
                    cleanseState.CleanseAppliedCount++;
                    cleanseState.LastCleanseFrame = frame;
                    cleanseState.LastCleanseSource = evt.SourceAsc;
                    cleanseState.LastCleanseTarget = evt.TargetAsc;
                    em.SetComponentData(evt.SourceAsc, cleanseState);

                    facts.CleanseAppliedFactCount++;
                    EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                    {
                        Type = EGameplayEventType.AutoChessCleanseApplied,
                        SourceAsc = evt.SourceAsc,
                        TargetAsc = evt.TargetAsc,
                        SourceAbility = evt.SourceAbility,
                        GameplayEffect = evt.GameplayEffect,
                        ContextId = evt.ContextId,
                        EventCode = rules.CleanseGameplayEffectCode,
                        ReasonCode = rules.RemovableTagIndex,
                        Value = cleanseState.CleanseAppliedCount,
                    });
                }

                if (rules.RallyGameplayEffectCode > 0
                    && IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.RallyGameplayEffectCode))
                {
                    var cleanseState = em.GetComponentData<CHeadlessAutoChessCleanseState>(evt.SourceAsc);
                    cleanseState.CleanseRallyAppliedCount++;
                    cleanseState.LastCleanseFrame = frame;
                    cleanseState.LastCleanseSource = evt.SourceAsc;
                    cleanseState.LastCleanseTarget = evt.TargetAsc;
                    em.SetComponentData(evt.SourceAsc, cleanseState);

                    facts.CleanseRallyAppliedFactCount++;
                    EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                    {
                        Type = EGameplayEventType.AutoChessCleanseRallyApplied,
                        SourceAsc = evt.SourceAsc,
                        TargetAsc = evt.TargetAsc,
                        SourceAbility = evt.SourceAbility,
                        GameplayEffect = evt.GameplayEffect,
                        ContextId = evt.ContextId,
                        EventCode = rules.RallyGameplayEffectCode,
                        ReasonCode = rules.RemovableTagIndex,
                        Value = cleanseState.CleanseRallyAppliedCount,
                    });
                }
            }

            facts.ProcessedGameplayEffectAppliedFactCount = eventCount;
        }

        private static void ProjectCleanseRemovedTagFacts(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<Entity> cleansers,
            ref CHeadlessAutoChessCleanseFacts facts,
            int frame)
        {
            using var tagEvents = EventBusHelper.SnapshotBufferRange<BTagChangeEvent>(
                em,
                eventBusEntity,
                facts.ProcessedTagEventCount,
                Allocator.Temp,
                out var eventCount);

            for (var i = 0; i < tagEvents.Length; i++)
            {
                var evt = tagEvents[i];
                if (evt.Added
                    || !TryFindRecentCleanser(
                        em,
                        cleansers,
                        evt.ASC,
                        evt.TagIndex,
                        frame,
                        out var cleanser,
                        out var rules,
                        out var cleanseState))
                {
                    continue;
                }

                cleanseState.CleanseEffectRemovedCount++;
                cleanseState.LastCleanseFrame = frame;
                cleanseState.LastCleanseSource = cleanser;
                cleanseState.LastCleanseTarget = evt.ASC;
                em.SetComponentData(cleanser, cleanseState);

                facts.CleanseEffectRemovedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessCleanseEffectRemoved,
                    SourceAsc = cleanser,
                    TargetAsc = evt.ASC,
                    EventCode = rules.CleanseGameplayEffectCode,
                    ReasonCode = evt.TagIndex,
                    Value = cleanseState.CleanseEffectRemovedCount,
                });

                TryRequestCleanseRally(
                    em,
                    eventBusEntity,
                    cleanser,
                    evt.ASC,
                    rules,
                    ref cleanseState,
                    ref facts,
                    frame);
            }

            facts.ProcessedTagEventCount = eventCount;
        }

        private static void TryRequestCleanseRally(
            EntityManager em,
            Entity eventBusEntity,
            Entity cleanser,
            Entity target,
            in CHeadlessAutoChessCleanseRules rules,
            ref CHeadlessAutoChessCleanseState cleanseState,
            ref CHeadlessAutoChessCleanseFacts facts,
            int frame)
        {
            if (rules.RallyGameplayEffectCode <= 0
                || cleanser == Entity.Null
                || target == Entity.Null
                || !em.Exists(cleanser)
                || !em.Exists(target)
                || !IsAliveAutoChessUnit(em, target))
            {
                return;
            }

            CreateApplyRequest(
                em,
                cleanser,
                target,
                rules.RallyGameplayEffectCode,
                "AutoChessCleanseRally");

            cleanseState.CleanseRallyRequestCount++;
            cleanseState.LastCleanseFrame = frame;
            cleanseState.LastCleanseSource = cleanser;
            cleanseState.LastCleanseTarget = target;
            em.SetComponentData(cleanser, cleanseState);

            facts.CleanseRallyRequestedFactCount++;
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessCleanseRallyRequested,
                SourceAsc = cleanser,
                TargetAsc = target,
                EventCode = rules.RallyGameplayEffectCode,
                ReasonCode = rules.RemovableTagIndex,
                Value = cleanseState.CleanseRallyRequestCount,
            });
        }

        private static bool TryGetCleanseRules(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessCleanseRules rules)
        {
            if (asc != Entity.Null
                && em.Exists(asc)
                && em.HasComponent<CHeadlessAutoChessCleanseRules>(asc)
                && em.HasComponent<CHeadlessAutoChessCleanseState>(asc))
            {
                rules = em.GetComponentData<CHeadlessAutoChessCleanseRules>(asc);
                return true;
            }

            rules = default;
            return false;
        }

        private static bool IsCleanseAppliedEvent(
            EntityManager em,
            in BHeadlessAutoChessGameplayEffectAppliedFact evt,
            in CHeadlessAutoChessCleanseRules rules)
        {
            if (evt.TargetAsc == Entity.Null || evt.TargetAsc == evt.SourceAsc)
                return false;

            return IsAbilityWithCode(em, evt.SourceAbility, rules.CleanseAbilityCode)
                   || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.CleanseGameplayEffectCode);
        }

        private static bool TryFindRecentCleanser(
            EntityManager em,
            NativeArray<Entity> cleansers,
            Entity target,
            int removedTagIndex,
            int frame,
            out Entity cleanser,
            out CHeadlessAutoChessCleanseRules rules,
            out CHeadlessAutoChessCleanseState cleanseState)
        {
            cleanser = Entity.Null;
            rules = default;
            cleanseState = default;

            if (target == Entity.Null)
                return false;

            for (var i = 0; i < cleansers.Length; i++)
            {
                var candidate = cleansers[i];
                if (candidate == Entity.Null || !em.Exists(candidate))
                    continue;

                var candidateRules = em.GetComponentData<CHeadlessAutoChessCleanseRules>(candidate);
                if (candidateRules.RemovableTagIndex != removedTagIndex)
                    continue;

                var candidateState = em.GetComponentData<CHeadlessAutoChessCleanseState>(candidate);
                if (candidateState.LastCleanseTarget != target
                    || candidateState.LastCleanseFrame < frame - 1
                    || candidateState.LastCleanseFrame > frame)
                {
                    continue;
                }

                cleanser = candidate;
                rules = candidateRules;
                cleanseState = candidateState;
                return true;
            }

            return false;
        }

        private static bool IsAbilityWithCode(EntityManager em, Entity ability, int abilityCode)
        {
            if (abilityCode <= 0 || ability == Entity.Null || !em.Exists(ability))
                return false;

            if (em.HasComponent<CAbilityBaseInfo>(ability)
                && em.GetComponentData<CAbilityBaseInfo>(ability).Code == abilityCode)
            {
                return true;
            }

            if (!em.HasComponent<CAbilityConfig>(ability))
                return false;

            var config = em.GetComponentData<CAbilityConfig>(ability).Config;
            return config.IsCreated && config.Value.Code == abilityCode;
        }

        private static bool IsGameplayEffectWithCode(EntityManager em, Entity gameplayEffect, int gameplayEffectCode)
        {
            return gameplayEffectCode > 0
                   && gameplayEffect != Entity.Null
                   && em.Exists(gameplayEffect)
                   && em.HasComponent<CEffectSpecData>(gameplayEffect)
                   && em.GetComponentData<CEffectSpecData>(gameplayEffect).GameplayEffectCode == gameplayEffectCode;
        }

        private static void CreateApplyRequest(
            EntityManager em,
            Entity source,
            Entity target,
            int gameplayEffectCode,
            string namePrefix)
        {
            var targetKind = source == target ? ETargetDataKind.Self : ETargetDataKind.Entity;
            GameplayEffectLegacyBridge.ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
                em,
                new CApplyGameplayEffectRequest
                {
                    SourceAsc = source,
                    Instigator = source,
                    Causer = source,
                    GameplayEffectCode = gameplayEffectCode,
                    Level = 1,
                },
                target,
                targetKind,
                namePrefix);
        }

        private static bool IsAliveAutoChessUnit(EntityManager em, Entity asc)
        {
            if (asc == Entity.Null
                || !em.Exists(asc)
                || !em.HasComponent<CHeadlessAutoChessUnit>(asc)
                || !em.HasBuffer<BAttribute>(asc))
            {
                return false;
            }

            var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
            return GetAttribute(em, asc, unit.HealthAttrSetCode, unit.HealthAttrCode) > 0f;
        }

        private static float GetAttribute(
            EntityManager em,
            Entity asc,
            int attrSetCode,
            int attrCode)
        {
            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<BAttribute>(asc))
                return 0f;

            var attributes = em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == attrSetCode && attribute.Code == attrCode)
                    return attribute.CurrentValue;
            }

            return 0f;
        }

        private static int ResolveCurrentFrame(EntityManager em)
        {
            var globalTimer = GASManager.EntityGlobalTimer;
            if (globalTimer != Entity.Null
                && em.Exists(globalTimer)
                && em.HasComponent<GlobalTimer>(globalTimer))
            {
                return em.GetComponentData<GlobalTimer>(globalTimer).Frame;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GlobalTimer>());
            return query.IsEmptyIgnoreFilter ? 0 : query.GetSingleton<GlobalTimer>().Frame;
        }
    }
}
