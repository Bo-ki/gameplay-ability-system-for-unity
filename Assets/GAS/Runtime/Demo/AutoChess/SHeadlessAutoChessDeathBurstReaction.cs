using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessExecuteReaction))]
    [UpdateBefore(typeof(SHeadlessAutoChessSynergyProjection))]
    public partial struct SHeadlessAutoChessDeathBurstReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _burstUnitQuery;
        private EntityQuery _targetUnitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessDeathBurstFacts>()
                .Build();
            _burstUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessDeathBurstRules, CHeadlessAutoChessDeathBurstState, BAttribute, CTagMask>()
                .Build();
            _targetUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, BAttribute>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_burstUnitQuery);
            state.RequireForUpdate(_targetUnitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity)
                || !em.HasBuffer<BGameplayEvent>(eventBusEntity)
                || !em.HasBuffer<BAttributeChangeEvent>(eventBusEntity))
            {
                return;
            }

            using var drivers = _driverQuery.ToEntityArray(Allocator.Temp);
            using var targets = _targetUnitQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0 || targets.Length == 0)
                return;

            var driverEntity = drivers[0];
            var facts = em.GetComponentData<CHeadlessAutoChessDeathBurstFacts>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            ResetProcessedCountsIfFrameChanged(ref facts, frame);

            ProjectDeathBurstTriggers(em, eventBusEntity, targets, ref facts, frame);
            ProjectDeathBurstDamageAppliedFacts(em, eventBusEntity, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessDeathBurstFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedGameplayEventCount = 0;
            facts.ProcessedAttributeEventCount = 0;
        }

        private static void ProjectDeathBurstTriggers(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<Entity> targets,
            ref CHeadlessAutoChessDeathBurstFacts facts,
            int frame)
        {
            var gameplayEvents = em.GetBuffer<BGameplayEvent>(eventBusEntity);
            var eventCount = gameplayEvents.Length;
            var start = facts.ProcessedGameplayEventCount > eventCount
                ? 0
                : facts.ProcessedGameplayEventCount;
            using var eventSnapshot = EventBusHelper.CopyBufferRange(
                gameplayEvents,
                start,
                eventCount,
                Allocator.Temp);

            for (var i = 0; i < eventSnapshot.Length; i++)
            {
                var evt = eventSnapshot[i];
                if (evt.Type != EGameplayEventType.AutoChessUnitDefeated
                    || evt.SourceAsc == Entity.Null
                    || evt.TargetAsc == Entity.Null
                    || evt.SourceAsc == evt.TargetAsc
                    || !TryGetDeathBurstRules(em, evt.SourceAsc, out var rules)
                    || rules.DamageGameplayEffectCode <= 0
                    || !HasDenseTag(em, evt.SourceAsc, rules.ReadyTagIndex)
                    || !IsAliveAutoChessUnit(em, evt.SourceAsc)
                    || !IsEnemy(em, evt.SourceAsc, evt.TargetAsc)
                    || !TryFindDeathBurstTarget(em, targets, evt.SourceAsc, evt.TargetAsc, rules, out var target))
                {
                    continue;
                }

                CreateApplyRequest(
                    em,
                    evt.SourceAsc,
                    target,
                    rules.DamageGameplayEffectCode,
                    "AutoChessDeathBurst");

                var burstState = em.GetComponentData<CHeadlessAutoChessDeathBurstState>(evt.SourceAsc);
                burstState.TriggerRequestCount++;
                burstState.LastDeathBurstFrame = frame;
                burstState.LastDeathBurstSource = evt.SourceAsc;
                burstState.LastDeathBurstCorpse = evt.TargetAsc;
                burstState.LastDeathBurstTarget = target;
                burstState.LastDeathBurstDamage = rules.DamageAmount;
                em.SetComponentData(evt.SourceAsc, burstState);

                facts.DeathBurstTriggeredFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessDeathBurstTriggered,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = target,
                    EventCode = rules.DamageGameplayEffectCode,
                    ReasonCode = rules.ReadyTagIndex,
                    Value = rules.DamageAmount,
                });
            }

            facts.ProcessedGameplayEventCount = em.Exists(eventBusEntity)
                                                && em.HasBuffer<BGameplayEvent>(eventBusEntity)
                ? em.GetBuffer<BGameplayEvent>(eventBusEntity).Length
                : eventCount;
        }

        private static void ProjectDeathBurstDamageAppliedFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessDeathBurstFacts facts,
            int frame)
        {
            var attributeEvents = em.GetBuffer<BAttributeChangeEvent>(eventBusEntity);
            var eventCount = attributeEvents.Length;
            var start = facts.ProcessedAttributeEventCount > eventCount
                ? 0
                : facts.ProcessedAttributeEventCount;
            using var eventSnapshot = EventBusHelper.CopyBufferRange(
                attributeEvents,
                start,
                eventCount,
                Allocator.Temp);

            for (var i = 0; i < eventSnapshot.Length; i++)
            {
                var evt = eventSnapshot[i];
                if (evt.ASC == Entity.Null
                    || evt.SourceAsc == Entity.Null
                    || evt.NewValue >= evt.OldValue
                    || !TryGetDeathBurstRules(em, evt.SourceAsc, out var rules)
                    || evt.AttrSetCode != rules.HealthAttrSetCode
                    || evt.AttributeCode != rules.HealthAttrCode
                    || !IsDeathBurstDamageAttributeEvent(em, evt, rules))
                {
                    continue;
                }

                var amount = evt.OldValue - evt.NewValue;
                var burstState = em.GetComponentData<CHeadlessAutoChessDeathBurstState>(evt.SourceAsc);
                burstState.DamageAppliedCount++;
                burstState.LastDeathBurstFrame = frame;
                burstState.LastDeathBurstSource = evt.SourceAsc;
                burstState.LastDeathBurstTarget = evt.ASC;
                burstState.LastDeathBurstGameplayEffect = evt.GameplayEffect;
                burstState.LastDeathBurstDamage = amount;
                em.SetComponentData(evt.SourceAsc, burstState);

                facts.DeathBurstDamageAppliedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessDeathBurstDamageApplied,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.ASC,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.DamageGameplayEffectCode,
                    ReasonCode = rules.DamageTypeCode,
                    Value = amount,
                });
            }

            facts.ProcessedAttributeEventCount = eventCount;
        }

        private static bool TryGetDeathBurstRules(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessDeathBurstRules rules)
        {
            if (asc != Entity.Null
                && em.Exists(asc)
                && em.HasComponent<CHeadlessAutoChessDeathBurstRules>(asc)
                && em.HasComponent<CHeadlessAutoChessDeathBurstState>(asc))
            {
                rules = em.GetComponentData<CHeadlessAutoChessDeathBurstRules>(asc);
                return true;
            }

            rules = default;
            return false;
        }

        private static bool TryFindDeathBurstTarget(
            EntityManager em,
            NativeArray<Entity> candidates,
            Entity source,
            Entity corpse,
            in CHeadlessAutoChessDeathBurstRules rules,
            out Entity target)
        {
            target = Entity.Null;
            var fallback = Entity.Null;
            var bestDistance = int.MaxValue;
            var bestFallbackDistance = int.MaxValue;

            if (!em.HasComponent<CHeadlessAutoChessUnit>(corpse))
                return false;

            var corpseUnit = em.GetComponentData<CHeadlessAutoChessUnit>(corpse);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == Entity.Null
                    || candidate == source
                    || candidate == corpse
                    || !em.Exists(candidate)
                    || !em.HasComponent<CHeadlessAutoChessUnit>(candidate)
                    || !IsAliveAutoChessUnit(em, candidate)
                    || !IsEnemy(em, source, candidate))
                {
                    continue;
                }

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                var distance = BoardDistance(corpseUnit, unit);
                if (rules.MaxBoardDistance <= 0 || distance <= rules.MaxBoardDistance)
                {
                    if (distance < bestDistance
                        || (distance == bestDistance && unit.TurnOrder < GetTurnOrder(em, target)))
                    {
                        target = candidate;
                        bestDistance = distance;
                    }
                }
                else if (distance < bestFallbackDistance
                         || (distance == bestFallbackDistance && unit.TurnOrder < GetTurnOrder(em, fallback)))
                {
                    fallback = candidate;
                    bestFallbackDistance = distance;
                }
            }

            if (target != Entity.Null)
                return true;

            target = fallback;
            return target != Entity.Null;
        }

        private static bool IsDeathBurstDamageAttributeEvent(
            EntityManager em,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessDeathBurstRules rules)
        {
            return rules.DamageGameplayEffectCode > 0
                   && (evt.EventCode == rules.DamageGameplayEffectCode
                       || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.DamageGameplayEffectCode));
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
            if (source == Entity.Null || target == Entity.Null || gameplayEffectCode <= 0)
                return;

            var request = GameplayEffectRequestWriter.Create(
                em,
                new CApplyGameplayEffectRequest
                {
                    SourceAsc = source,
                    Instigator = source,
                    Causer = source,
                    GameplayEffectCode = gameplayEffectCode,
                    Level = 1,
                },
                new CTargetDataHeader
                {
                    SourceAsc = source,
                    Kind = source == target ? ETargetDataKind.Self : ETargetDataKind.Entity,
                },
                namePrefix);
            GameplayEffectRequestWriter.AddTarget(em, request, target);
        }

        private static bool IsEnemy(EntityManager em, Entity lhs, Entity rhs)
        {
            return lhs != Entity.Null
                   && rhs != Entity.Null
                   && em.HasComponent<CHeadlessAutoChessUnit>(lhs)
                   && em.HasComponent<CHeadlessAutoChessUnit>(rhs)
                   && em.GetComponentData<CHeadlessAutoChessUnit>(lhs).Team
                   != em.GetComponentData<CHeadlessAutoChessUnit>(rhs).Team;
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

        private static bool HasDenseTag(EntityManager em, Entity asc, int denseTagIndex)
        {
            return CTagMask.IsValidIndex(denseTagIndex)
                   && asc != Entity.Null
                   && em.Exists(asc)
                   && em.HasComponent<CTagMask>(asc)
                   && em.GetComponentData<CTagMask>(asc).HasTag(denseTagIndex);
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

        private static int BoardDistance(
            in CHeadlessAutoChessUnit lhs,
            in CHeadlessAutoChessUnit rhs)
        {
            return Abs(lhs.BoardX - rhs.BoardX) + Abs(lhs.BoardY - rhs.BoardY);
        }

        private static int Abs(int value)
        {
            return value < 0 ? -value : value;
        }

        private static int GetTurnOrder(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && em.HasComponent<CHeadlessAutoChessUnit>(asc)
                ? em.GetComponentData<CHeadlessAutoChessUnit>(asc).TurnOrder
                : int.MaxValue;
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
