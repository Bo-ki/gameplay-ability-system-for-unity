using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessPassiveReaction))]
    [UpdateBefore(typeof(SHeadlessAutoChessCounterReaction))]
    public partial struct SHeadlessAutoChessEnrageReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _enrageUnitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessEnrageFacts, BHeadlessAutoChessGameplayEffectAppliedFact>()
                .Build();
            _enrageUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessEnrageRules, CHeadlessAutoChessEnrageState, BAttribute, CTagMask>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_enrageUnitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity)
                || !em.HasBuffer<BGameplayEvent>(eventBusEntity)
                || !em.HasBuffer<BAttributeChangeEvent>(eventBusEntity))
            {
                return;
            }

            if (_driverQuery.IsEmptyIgnoreFilter)
                return;

            var driverEntity = _driverQuery.GetSingletonEntity();
            var facts = em.GetComponentData<CHeadlessAutoChessEnrageFacts>(driverEntity);
            var appliedFacts = em.GetBuffer<BHeadlessAutoChessGameplayEffectAppliedFact>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            ResetProcessedCountsIfFrameChanged(ref facts, frame);

            ProjectEnrageAppliedFacts(em, eventBusEntity, appliedFacts, ref facts, frame);
            ProjectEnrageTriggers(em, eventBusEntity, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessEnrageFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedGameplayEventCount = 0;
            facts.ProcessedGameplayEffectAppliedFactCount = 0;
            facts.ProcessedAttributeEventCount = 0;
        }

        private static void ProjectEnrageAppliedFacts(
            EntityManager em,
            Entity eventBusEntity,
            DynamicBuffer<BHeadlessAutoChessGameplayEffectAppliedFact> appliedFacts,
            ref CHeadlessAutoChessEnrageFacts facts,
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
                if (evt.TargetAsc == Entity.Null
                    || !TryGetEnrageRules(em, evt.TargetAsc, out var rules)
                    || !IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.EnrageGameplayEffectCode))
                {
                    continue;
                }

                var counterDamage = GetAttributeCurrentValue(
                    em,
                    evt.TargetAsc,
                    rules.CounterDamageAttrSetCode,
                    rules.CounterDamageAttrCode);
                var enrageState = em.GetComponentData<CHeadlessAutoChessEnrageState>(evt.TargetAsc);
                enrageState.AppliedCount++;
                enrageState.LastEnrageFrame = frame;
                enrageState.LastEnrageSource = evt.SourceAsc;
                enrageState.LastEnrageTarget = evt.TargetAsc;
                enrageState.LastEnrageGameplayEffect = evt.GameplayEffect;
                enrageState.LastCounterDamage = counterDamage;
                em.SetComponentData(evt.TargetAsc, enrageState);

                facts.EnrageAppliedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessEnrageApplied,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.TargetAsc,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.EnrageGameplayEffectCode,
                    ReasonCode = rules.EnragedTagIndex,
                    Value = counterDamage,
                });
            }

            facts.ProcessedGameplayEffectAppliedFactCount = eventCount;
        }

        private static void ProjectEnrageTriggers(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessEnrageFacts facts,
            int frame)
        {
            using var attributeEvents = EventBusHelper.SnapshotBufferRange<BAttributeChangeEvent>(
                em,
                eventBusEntity,
                facts.ProcessedAttributeEventCount,
                Allocator.Temp,
                out var eventCount);

            for (var i = 0; i < attributeEvents.Length; i++)
            {
                var evt = attributeEvents[i];
                if (evt.ASC == Entity.Null
                    || evt.NewValue >= evt.OldValue
                    || evt.NewValue <= 0f
                    || !TryGetEnrageRules(em, evt.ASC, out var rules)
                    || evt.AttrSetCode != rules.HealthAttrSetCode
                    || evt.AttributeCode != rules.HealthAttrCode
                    || rules.EnrageGameplayEffectCode <= 0
                    || rules.CounterDamageBonus <= 0f
                    || HasDenseTag(em, evt.ASC, rules.EnragedTagIndex))
                {
                    continue;
                }

                var enrageState = em.GetComponentData<CHeadlessAutoChessEnrageState>(evt.ASC);
                if (enrageState.TriggerRequestCount > 0 || enrageState.AppliedCount > 0)
                    continue;

                if (!TryGetAttributeMaxValue(em, evt.ASC, rules.HealthAttrSetCode, rules.HealthAttrCode, out var maxHealth))
                    continue;

                var threshold = maxHealth > 0f
                    ? maxHealth * rules.HealthThresholdRatio
                    : evt.OldValue * rules.HealthThresholdRatio;
                if (threshold <= 0f || evt.OldValue <= threshold || evt.NewValue > threshold)
                    continue;

                CreateApplyRequest(
                    em,
                    evt.ASC,
                    rules.EnrageGameplayEffectCode,
                    "AutoChessEnrage");

                var counterDamage = GetAttributeCurrentValue(
                    em,
                    evt.ASC,
                    rules.CounterDamageAttrSetCode,
                    rules.CounterDamageAttrCode);
                enrageState.TriggerRequestCount++;
                enrageState.LastEnrageFrame = frame;
                enrageState.LastEnrageSource = evt.ASC;
                enrageState.LastEnrageTarget = evt.ASC;
                enrageState.LastHealth = evt.NewValue;
                enrageState.LastCounterDamage = counterDamage + rules.CounterDamageBonus;
                em.SetComponentData(evt.ASC, enrageState);

                facts.EnrageTriggeredFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessEnrageTriggered,
                    SourceAsc = evt.ASC,
                    TargetAsc = evt.ASC,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.EnrageGameplayEffectCode,
                    ReasonCode = rules.EnragedTagIndex,
                    Value = rules.CounterDamageBonus,
                });
            }

            facts.ProcessedAttributeEventCount = eventCount;
        }

        private static bool TryGetEnrageRules(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessEnrageRules rules)
        {
            if (asc != Entity.Null
                && em.Exists(asc)
                && em.HasComponent<CHeadlessAutoChessEnrageRules>(asc)
                && em.HasComponent<CHeadlessAutoChessEnrageState>(asc))
            {
                rules = em.GetComponentData<CHeadlessAutoChessEnrageRules>(asc);
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

        private static void CreateApplyRequest(
            EntityManager em,
            Entity asc,
            int gameplayEffectCode,
            string namePrefix)
        {
            if (asc == Entity.Null || gameplayEffectCode <= 0)
                return;

            GameplayEffectRequestWriter.ApplyFastOrCreateSingleTargetRequest(
                em,
                new CApplyGameplayEffectRequest
                {
                    SourceAsc = asc,
                    Instigator = asc,
                    Causer = asc,
                    GameplayEffectCode = gameplayEffectCode,
                    Level = 1,
                },
                asc,
                ETargetDataKind.Self,
                namePrefix);
        }

        private static bool HasDenseTag(EntityManager em, Entity asc, int denseTagIndex)
        {
            return CTagMask.IsValidIndex(denseTagIndex)
                   && asc != Entity.Null
                   && em.Exists(asc)
                   && em.HasComponent<CTagMask>(asc)
                   && em.GetComponentData<CTagMask>(asc).HasTag(denseTagIndex);
        }

        private static bool TryGetAttributeMaxValue(
            EntityManager em,
            Entity asc,
            int attrSetCode,
            int attrCode,
            out float maxValue)
        {
            maxValue = 0f;
            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<BAttribute>(asc))
                return false;

            var attributes = em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == attrSetCode && attribute.Code == attrCode)
                {
                    maxValue = attribute.MaxValue > 0f ? attribute.MaxValue : attribute.BaseValue;
                    return true;
                }
            }

            return false;
        }

        private static float GetAttributeCurrentValue(
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
