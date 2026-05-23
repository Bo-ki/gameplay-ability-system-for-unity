using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessPassiveReaction))]
    public partial struct SHeadlessAutoChessCounterReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _counterUnitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessCounterFacts>()
                .Build();
            _counterUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessCounterRules, CHeadlessAutoChessCounterState, BAttribute, CTagMask>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_counterUnitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity)
                || !em.HasBuffer<BGameplayEvent>(eventBusEntity)
                || !em.HasBuffer<BAttributeChangeEvent>(eventBusEntity)
                || !em.HasBuffer<BDamageEvent>(eventBusEntity))
            {
                return;
            }

            using var drivers = _driverQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0)
                return;

            var driverEntity = drivers[0];
            var facts = em.GetComponentData<CHeadlessAutoChessCounterFacts>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            ResetProcessedCountsIfFrameChanged(ref facts, frame);

            ProjectEquipmentAppliedFacts(em, eventBusEntity, ref facts);
            ProjectCounterDamageAppliedFacts(em, eventBusEntity, ref facts, frame);
            ProjectCounterTriggers(em, eventBusEntity, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessCounterFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedGameplayEventCount = 0;
            facts.ProcessedAttributeEventCount = 0;
            facts.ProcessedDamageEventCount = 0;
        }

        private static void ProjectEquipmentAppliedFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessCounterFacts facts)
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
                if (evt.Type != EGameplayEventType.GameplayEffectApplied
                    || !TryGetCounterRules(em, evt.TargetAsc, out var rules)
                    || !IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.EquipmentGameplayEffectCode))
                {
                    continue;
                }

                var counterDamage = GetAttribute(
                    em,
                    evt.TargetAsc,
                    rules.CounterDamageAttrSetCode,
                    rules.CounterDamageAttrCode);
                var state = em.GetComponentData<CHeadlessAutoChessCounterState>(evt.TargetAsc);
                state.EquipmentAppliedCount++;
                em.SetComponentData(evt.TargetAsc, state);

                facts.EquipmentAppliedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessEquipmentApplied,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.TargetAsc,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.EquipmentGameplayEffectCode,
                    ReasonCode = rules.CounterReadyTagIndex,
                    Value = counterDamage,
                });
            }

            facts.ProcessedGameplayEventCount = em.Exists(eventBusEntity)
                                                && em.HasBuffer<BGameplayEvent>(eventBusEntity)
                ? em.GetBuffer<BGameplayEvent>(eventBusEntity).Length
                : eventCount;
        }

        private static void ProjectCounterDamageAppliedFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessCounterFacts facts,
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
                    || evt.AttrSetCode != HeadlessAutoChessScenario.AttributeSetCombat
                    || evt.AttributeCode != HeadlessAutoChessScenario.AttributeHealth
                    || !TryGetCounterRules(em, evt.SourceAsc, out var rules)
                    || !IsCounterDamageAttributeEvent(em, evt, rules, frame))
                {
                    continue;
                }

                var amount = evt.OldValue - evt.NewValue;
                var state = em.GetComponentData<CHeadlessAutoChessCounterState>(evt.SourceAsc);
                state.CounterDamageAppliedCount++;
                state.LastCounterFrame = frame;
                state.LastCounterSource = evt.SourceAsc;
                state.LastCounterTarget = evt.ASC;
                state.LastCounterDamage = amount;
                em.SetComponentData(evt.SourceAsc, state);

                facts.CounterDamageAppliedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessCounterDamageApplied,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.ASC,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.CounterDamageGameplayEffectCode,
                    ReasonCode = rules.SetByCallerCounterDamageAmountKey > 0
                        ? rules.SetByCallerCounterDamageAmountKey
                        : rules.CounterDamageAttrCode,
                    Value = amount,
                });
            }

            facts.ProcessedAttributeEventCount = eventCount;
        }

        private static void ProjectCounterTriggers(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessCounterFacts facts,
            int frame)
        {
            var damageEvents = em.GetBuffer<BDamageEvent>(eventBusEntity);
            var eventCount = damageEvents.Length;
            var start = facts.ProcessedDamageEventCount > eventCount
                ? 0
                : facts.ProcessedDamageEventCount;
            using var eventSnapshot = EventBusHelper.CopyBufferRange(
                damageEvents,
                start,
                eventCount,
                Allocator.Temp);

            for (var i = 0; i < eventSnapshot.Length; i++)
            {
                var evt = eventSnapshot[i];
                if (!TryGetCounterRules(em, evt.Target, out var rules)
                    || evt.Source == Entity.Null
                    || evt.Source == evt.Target
                    || evt.Amount < rules.MinIncomingDamage
                    || !IsAliveAutoChessUnit(em, evt.Target)
                    || !IsAliveAutoChessUnit(em, evt.Source)
                    || !IsEnemy(em, evt.Target, evt.Source)
                    || !HasDenseTag(em, evt.Target, rules.CounterReadyTagIndex))
                {
                    continue;
                }

                var counterDamage = GetAttribute(
                    em,
                    evt.Target,
                    rules.CounterDamageAttrSetCode,
                    rules.CounterDamageAttrCode);
                counterDamage = ResolvePendingEnrageCounterDamage(
                    em,
                    evt.Target,
                    frame,
                    counterDamage);
                if (counterDamage <= 0f)
                    continue;

                CreateApplyRequest(
                    em,
                    evt.Target,
                    evt.Source,
                    rules.CounterDamageGameplayEffectCode,
                    rules.SetByCallerCounterDamageAmountKey,
                    counterDamage,
                    "AutoChessCounter");

                var state = em.GetComponentData<CHeadlessAutoChessCounterState>(evt.Target);
                state.CounterRequestCount++;
                state.LastCounterFrame = frame;
                state.LastCounterSource = evt.Target;
                state.LastCounterTarget = evt.Source;
                state.LastCounterDamage = counterDamage;
                em.SetComponentData(evt.Target, state);

                facts.CounterTriggeredFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessCounterTriggered,
                    SourceAsc = evt.Target,
                    TargetAsc = evt.Source,
                    EventCode = rules.CounterDamageGameplayEffectCode,
                    ReasonCode = rules.CounterReadyTagIndex,
                    Value = counterDamage,
                });
            }

            facts.ProcessedDamageEventCount = eventCount;
        }

        private static float ResolvePendingEnrageCounterDamage(
            EntityManager em,
            Entity asc,
            int frame,
            float counterDamage)
        {
            if (asc == Entity.Null
                || !em.Exists(asc)
                || !em.HasComponent<CHeadlessAutoChessEnrageState>(asc))
            {
                return counterDamage;
            }

            var enrageState = em.GetComponentData<CHeadlessAutoChessEnrageState>(asc);
            if (enrageState.TriggerRequestCount <= enrageState.AppliedCount
                || enrageState.LastEnrageFrame != frame)
            {
                return counterDamage;
            }

            if (enrageState.LastCounterDamage > counterDamage)
                return enrageState.LastCounterDamage;

            if (em.HasComponent<CHeadlessAutoChessEnrageRules>(asc))
            {
                var enrageRules = em.GetComponentData<CHeadlessAutoChessEnrageRules>(asc);
                if (enrageRules.CounterDamageBonus > 0f)
                    return counterDamage + enrageRules.CounterDamageBonus;
            }

            return enrageState.LastCounterDamage;
        }

        private static void CreateApplyRequest(
            EntityManager em,
            Entity source,
            Entity target,
            int gameplayEffectCode,
            int setByCallerKey,
            float setByCallerValue,
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

            if (setByCallerKey > 0)
            {
                em.AddBuffer<BSetByCallerValue>(request).Add(new BSetByCallerValue
                {
                    Key = setByCallerKey,
                    Value = setByCallerValue,
                });
            }
        }

        private static bool TryGetCounterRules(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessCounterRules rules)
        {
            if (asc != Entity.Null
                && em.Exists(asc)
                && em.HasComponent<CHeadlessAutoChessCounterRules>(asc)
                && em.HasComponent<CHeadlessAutoChessCounterState>(asc))
            {
                rules = em.GetComponentData<CHeadlessAutoChessCounterRules>(asc);
                return true;
            }

            rules = default;
            return false;
        }

        private static bool IsGameplayEffectWithCode(EntityManager em, Entity gameplayEffect, int gameplayEffectCode)
        {
            return gameplayEffectCode > 0
                   && gameplayEffect != Entity.Null
                   && em.Exists(gameplayEffect)
                   && em.HasComponent<CEffectSpecData>(gameplayEffect)
                   && em.GetComponentData<CEffectSpecData>(gameplayEffect).GameplayEffectCode == gameplayEffectCode;
        }

        private static bool IsCounterDamageAttributeEvent(
            EntityManager em,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessCounterRules rules,
            int frame)
        {
            if (rules.CounterDamageGameplayEffectCode <= 0)
                return false;

            if (evt.EventCode == rules.CounterDamageGameplayEffectCode
                || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.CounterDamageGameplayEffectCode))
            {
                return true;
            }

            var amount = evt.OldValue - evt.NewValue;
            var counterState = em.GetComponentData<CHeadlessAutoChessCounterState>(evt.SourceAsc);
            return amount > 0f
                   && counterState.LastCounterSource == evt.SourceAsc
                   && counterState.LastCounterTarget == evt.ASC
                   && counterState.LastCounterFrame >= frame - 1
                   && counterState.LastCounterFrame <= frame
                   && math.abs(counterState.LastCounterDamage - amount) <= 0.001f;
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
