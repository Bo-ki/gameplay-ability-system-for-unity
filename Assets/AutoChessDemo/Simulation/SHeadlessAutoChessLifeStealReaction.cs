using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessRallyComboReaction))]
    [UpdateBefore(typeof(SHeadlessAutoChessSynergyProjection))]
    public partial struct SHeadlessAutoChessLifeStealReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _lifeStealUnitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessLifeStealFacts>()
                .Build();
            _lifeStealUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessLifeStealRules, CHeadlessAutoChessLifeStealState, BAttribute, CTagMask>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_lifeStealUnitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity)
                || !em.HasBuffer<BDamageEvent>(eventBusEntity)
                || !em.HasBuffer<BAttributeChangeEvent>(eventBusEntity))
            {
                return;
            }

            if (_driverQuery.IsEmptyIgnoreFilter)
                return;

            var driverEntity = _driverQuery.GetSingletonEntity();
            var facts = em.GetComponentData<CHeadlessAutoChessLifeStealFacts>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            ResetProcessedCountsIfFrameChanged(ref facts, frame);

            ProjectLifeStealTriggers(em, eventBusEntity, ref facts, frame);
            ProjectLifeStealHealedFacts(em, eventBusEntity, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessLifeStealFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedDamageEventCount = 0;
            facts.ProcessedAttributeEventCount = 0;
        }

        private static void ProjectLifeStealTriggers(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessLifeStealFacts facts,
            int frame)
        {
            using var damageEvents = EventBusHelper.SnapshotBufferRange<BDamageEvent>(
                em,
                eventBusEntity,
                facts.ProcessedDamageEventCount,
                Allocator.Temp,
                out var eventCount);

            for (var i = 0; i < damageEvents.Length; i++)
            {
                var evt = damageEvents[i];
                if (!TryGetLifeStealRules(em, evt.Source, out var rules)
                    || evt.Source == Entity.Null
                    || evt.Target == Entity.Null
                    || evt.Source == evt.Target
                    || evt.Amount < rules.MinDamage
                    || !IsAliveAutoChessUnit(em, evt.Source)
                    || !IsAliveAutoChessUnit(em, evt.Target)
                    || !IsEnemy(em, evt.Source, evt.Target)
                    || !HasDenseTag(em, evt.Source, rules.ReadyTagIndex))
                {
                    continue;
                }

                var ratio = GetAttribute(
                    em,
                    evt.Source,
                    rules.LifeStealRatioAttrSetCode,
                    rules.LifeStealRatioAttrCode);
                var healAmount = math.max(0f, evt.Amount * ratio);
                if (healAmount <= 0f)
                    continue;

                CreateApplyRequest(
                    em,
                    evt.Source,
                    evt.Source,
                    rules.HealGameplayEffectCode,
                    rules.SetByCallerHealAmountKey,
                    healAmount,
                    "AutoChessLifeStealHeal");

                var state = em.GetComponentData<CHeadlessAutoChessLifeStealState>(evt.Source);
                state.TriggerRequestCount++;
                state.LastLifeStealFrame = frame;
                state.LastLifeStealSource = evt.Source;
                state.LastLifeStealTarget = evt.Target;
                state.LastLifeStealDamage = evt.Amount;
                state.LastLifeStealHealAmount = healAmount;
                em.SetComponentData(evt.Source, state);

                facts.LifeStealTriggeredFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessLifeStealTriggered,
                    SourceAsc = evt.Source,
                    TargetAsc = evt.Source,
                    EventCode = rules.HealGameplayEffectCode,
                    ReasonCode = rules.ReadyTagIndex,
                    Value = healAmount,
                });
            }

            facts.ProcessedDamageEventCount = eventCount;
        }

        private static void ProjectLifeStealHealedFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessLifeStealFacts facts,
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
                    || evt.NewValue <= evt.OldValue
                    || !TryGetLifeStealRules(em, evt.ASC, out var rules)
                    || evt.AttrSetCode != rules.HealthAttrSetCode
                    || evt.AttributeCode != rules.HealthAttrCode
                    || !IsLifeStealHealAttributeEvent(em, evt, rules))
                {
                    continue;
                }

                var amount = evt.NewValue - evt.OldValue;
                var state = em.GetComponentData<CHeadlessAutoChessLifeStealState>(evt.ASC);
                state.HealAppliedCount++;
                state.LastLifeStealFrame = frame;
                state.LastLifeStealSource = evt.SourceAsc;
                state.LastLifeStealTarget = evt.ASC;
                state.LastLifeStealGameplayEffect = evt.GameplayEffect;
                state.LastLifeStealHealAmount = amount;
                em.SetComponentData(evt.ASC, state);

                facts.LifeStealHealedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessLifeStealHealed,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.ASC,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.HealGameplayEffectCode,
                    ReasonCode = rules.SetByCallerHealAmountKey,
                    Value = amount,
                });
            }

            facts.ProcessedAttributeEventCount = eventCount;
        }

        private static bool TryGetLifeStealRules(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessLifeStealRules rules)
        {
            if (asc != Entity.Null
                && em.Exists(asc)
                && em.HasComponent<CHeadlessAutoChessLifeStealRules>(asc)
                && em.HasComponent<CHeadlessAutoChessLifeStealState>(asc))
            {
                rules = em.GetComponentData<CHeadlessAutoChessLifeStealRules>(asc);
                return true;
            }

            rules = default;
            return false;
        }

        private static bool IsLifeStealHealAttributeEvent(
            EntityManager em,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessLifeStealRules rules)
        {
            return evt.EventCode == rules.HealGameplayEffectCode
                   || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.HealGameplayEffectCode);
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
            int setByCallerKey,
            float setByCallerValue,
            string namePrefix)
        {
            if (source == Entity.Null || target == Entity.Null || gameplayEffectCode <= 0)
                return;

            var targetKind = source == target ? ETargetDataKind.Self : ETargetDataKind.Entity;
            var requestData = new CApplyGameplayEffectRequest
            {
                SourceAsc = source,
                Instigator = source,
                Causer = source,
                GameplayEffectCode = gameplayEffectCode,
                Level = 1,
            };
            var setByCaller = new BSetByCallerValue
            {
                Key = setByCallerKey,
                Value = setByCallerValue,
            };
            GameplayEffectLegacyBridge.ApplyLegacyInstantBypassOrCreateSingleTargetRequest(
                em,
                requestData,
                target,
                targetKind,
                setByCaller,
                namePrefix);
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
