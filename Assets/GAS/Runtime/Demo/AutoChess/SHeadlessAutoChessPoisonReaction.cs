using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessLifeStealReaction))]
    [UpdateBefore(typeof(SHeadlessAutoChessSynergyProjection))]
    public partial struct SHeadlessAutoChessPoisonReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _poisonUnitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessPoisonFacts>()
                .Build();
            _poisonUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessPoisonRules, CHeadlessAutoChessPoisonState, BAttribute>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_poisonUnitQuery);
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
            if (drivers.Length == 0)
                return;

            var driverEntity = drivers[0];
            var facts = em.GetComponentData<CHeadlessAutoChessPoisonFacts>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            ResetProcessedCountsIfFrameChanged(ref facts, frame);

            ProjectAttributeDrivenPoisonFacts(em, eventBusEntity, ref facts, frame);
            ProjectPoisonStackEvents(em, eventBusEntity, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessPoisonFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedGameplayEventCount = 0;
            facts.ProcessedAttributeEventCount = 0;
        }

        private static void ProjectAttributeDrivenPoisonFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessPoisonFacts facts,
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
                    || evt.SourceAsc == evt.ASC
                    || evt.NewValue >= evt.OldValue
                    || !TryGetPoisonRules(em, evt.SourceAsc, out var rules)
                    || evt.AttrSetCode != rules.HealthAttrSetCode
                    || evt.AttributeCode != rules.HealthAttrCode
                    || !IsEnemy(em, evt.SourceAsc, evt.ASC))
                {
                    continue;
                }

                var amount = evt.OldValue - evt.NewValue;
                if (IsPoisonPeriodDamageAttributeEvent(em, evt, rules))
                {
                    ProjectPeriodDamageApplied(em, eventBusEntity, evt, rules, amount, ref facts, frame);
                    continue;
                }

                if (IsPoisonOverflowDamageAttributeEvent(em, evt, rules))
                {
                    ProjectOverflowDamageApplied(em, eventBusEntity, evt, rules, amount, ref facts, frame);
                    continue;
                }

                if (amount < rules.MinDamage
                    || evt.NewValue <= 0f
                    || !IsAliveAutoChessUnit(em, evt.SourceAsc))
                {
                    continue;
                }

                CreateApplyRequest(
                    em,
                    evt.SourceAsc,
                    evt.ASC,
                    rules.StackGameplayEffectCode,
                    "AutoChessPoisonStack");

                var poisonState = em.GetComponentData<CHeadlessAutoChessPoisonState>(evt.SourceAsc);
                poisonState.StackRequestCount++;
                poisonState.LastPoisonFrame = frame;
                poisonState.LastPoisonSource = evt.SourceAsc;
                poisonState.LastPoisonTarget = evt.ASC;
                poisonState.LastPoisonDamage = amount;
                em.SetComponentData(evt.SourceAsc, poisonState);

                facts.PoisonStackRequestedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessPoisonStackRequested,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.ASC,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.StackGameplayEffectCode,
                    ReasonCode = rules.PoisonedTagIndex,
                    Value = amount,
                });
            }

            facts.ProcessedAttributeEventCount = eventCount;
        }

        private static void ProjectPoisonStackEvents(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessPoisonFacts facts,
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
                if ((evt.Type != EGameplayEventType.StackCountChanged
                     && evt.Type != EGameplayEventType.StackOverflow)
                    || !TryGetPoisonRules(em, evt.SourceAsc, out var rules)
                    || !IsPoisonStackEvent(em, evt, rules))
                {
                    continue;
                }

                var stackCount = (int)evt.Value;
                var poisonState = em.GetComponentData<CHeadlessAutoChessPoisonState>(evt.SourceAsc);
                poisonState.LastPoisonFrame = frame;
                poisonState.LastPoisonSource = evt.SourceAsc;
                poisonState.LastPoisonTarget = evt.TargetAsc;
                poisonState.LastPoisonGameplayEffect = evt.GameplayEffect;
                poisonState.LastPoisonStackCount = stackCount;

                if (evt.Type == EGameplayEventType.StackCountChanged)
                {
                    poisonState.StackChangedCount++;
                    facts.PoisonStackChangedFactCount++;
                    EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                    {
                        Type = EGameplayEventType.AutoChessPoisonStackChanged,
                        SourceAsc = evt.SourceAsc,
                        TargetAsc = evt.TargetAsc,
                        SourceAbility = evt.SourceAbility,
                        GameplayEffect = evt.GameplayEffect,
                        ContextId = evt.ContextId,
                        EventCode = rules.StackingCode,
                        ReasonCode = rules.PoisonedTagIndex,
                        Value = stackCount,
                    });
                }
                else
                {
                    poisonState.OverflowTriggeredCount++;
                    facts.PoisonOverflowTriggeredFactCount++;
                    EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                    {
                        Type = EGameplayEventType.AutoChessPoisonOverflowTriggered,
                        SourceAsc = evt.SourceAsc,
                        TargetAsc = evt.TargetAsc,
                        SourceAbility = evt.SourceAbility,
                        GameplayEffect = evt.GameplayEffect,
                        ContextId = evt.ContextId,
                        EventCode = rules.StackingCode,
                        ReasonCode = rules.OverflowDamageGameplayEffectCode,
                        Value = stackCount,
                    });
                }

                em.SetComponentData(evt.SourceAsc, poisonState);
            }

            facts.ProcessedGameplayEventCount = em.Exists(eventBusEntity)
                                                && em.HasBuffer<BGameplayEvent>(eventBusEntity)
                ? em.GetBuffer<BGameplayEvent>(eventBusEntity).Length
                : eventCount;
        }

        private static void ProjectOverflowDamageApplied(
            EntityManager em,
            Entity eventBusEntity,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessPoisonRules rules,
            float amount,
            ref CHeadlessAutoChessPoisonFacts facts,
            int frame)
        {
            var poisonState = em.GetComponentData<CHeadlessAutoChessPoisonState>(evt.SourceAsc);
            poisonState.OverflowDamageAppliedCount++;
            poisonState.LastPoisonFrame = frame;
            poisonState.LastPoisonSource = evt.SourceAsc;
            poisonState.LastPoisonTarget = evt.ASC;
            poisonState.LastPoisonGameplayEffect = evt.GameplayEffect;
            poisonState.LastPoisonDamage = amount;
            em.SetComponentData(evt.SourceAsc, poisonState);

            facts.PoisonOverflowDamageAppliedFactCount++;
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessPoisonOverflowDamageApplied,
                SourceAsc = evt.SourceAsc,
                TargetAsc = evt.ASC,
                SourceAbility = evt.SourceAbility,
                GameplayEffect = evt.GameplayEffect,
                ContextId = evt.ContextId,
                EventCode = rules.OverflowDamageGameplayEffectCode,
                ReasonCode = rules.DamageTypeCode,
                Value = amount,
            });
        }

        private static void ProjectPeriodDamageApplied(
            EntityManager em,
            Entity eventBusEntity,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessPoisonRules rules,
            float amount,
            ref CHeadlessAutoChessPoisonFacts facts,
            int frame)
        {
            var poisonState = em.GetComponentData<CHeadlessAutoChessPoisonState>(evt.SourceAsc);
            poisonState.PeriodDamageAppliedCount++;
            poisonState.LastPoisonFrame = frame;
            poisonState.LastPoisonSource = evt.SourceAsc;
            poisonState.LastPoisonTarget = evt.ASC;
            poisonState.LastPoisonGameplayEffect = evt.GameplayEffect;
            poisonState.LastPoisonDamage = amount;
            em.SetComponentData(evt.SourceAsc, poisonState);

            facts.PoisonPeriodDamageAppliedFactCount++;
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessPoisonPeriodDamageApplied,
                SourceAsc = evt.SourceAsc,
                TargetAsc = evt.ASC,
                SourceAbility = evt.SourceAbility,
                GameplayEffect = evt.GameplayEffect,
                ContextId = evt.ContextId,
                EventCode = rules.PeriodDamageGameplayEffectCode,
                ReasonCode = rules.DamageTypeCode,
                Value = amount,
            });
        }

        private static bool TryGetPoisonRules(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessPoisonRules rules)
        {
            if (asc != Entity.Null
                && em.Exists(asc)
                && em.HasComponent<CHeadlessAutoChessPoisonRules>(asc)
                && em.HasComponent<CHeadlessAutoChessPoisonState>(asc))
            {
                rules = em.GetComponentData<CHeadlessAutoChessPoisonRules>(asc);
                return true;
            }

            rules = default;
            return false;
        }

        private static bool IsPoisonPeriodDamageAttributeEvent(
            EntityManager em,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessPoisonRules rules)
        {
            return rules.PeriodDamageGameplayEffectCode > 0
                   && (evt.EventCode == rules.PeriodDamageGameplayEffectCode
                       || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.PeriodDamageGameplayEffectCode));
        }

        private static bool IsPoisonOverflowDamageAttributeEvent(
            EntityManager em,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessPoisonRules rules)
        {
            return rules.OverflowDamageGameplayEffectCode > 0
                   && (evt.EventCode == rules.OverflowDamageGameplayEffectCode
                       || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.OverflowDamageGameplayEffectCode));
        }

        private static bool IsPoisonStackEvent(
            EntityManager em,
            in BGameplayEvent evt,
            in CHeadlessAutoChessPoisonRules rules)
        {
            return evt.EventCode == rules.StackingCode
                   || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.StackGameplayEffectCode);
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
            return em.HasComponent<CHeadlessAutoChessUnit>(lhs)
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
