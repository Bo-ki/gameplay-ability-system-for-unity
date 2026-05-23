using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessPoisonReaction))]
    [UpdateBefore(typeof(SHeadlessAutoChessSynergyProjection))]
    public partial struct SHeadlessAutoChessExecuteReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _executeUnitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessExecuteFacts>()
                .Build();
            _executeUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessExecuteRules, CHeadlessAutoChessExecuteState, BAttribute, CTagMask>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_executeUnitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity)
                || !em.HasBuffer<BGameplayEvent>(eventBusEntity)
                || !em.HasBuffer<BDamageEvent>(eventBusEntity)
                || !em.HasBuffer<BAttributeChangeEvent>(eventBusEntity))
            {
                return;
            }

            if (_driverQuery.IsEmptyIgnoreFilter)
                return;

            var driverEntity = _driverQuery.GetSingletonEntity();
            var facts = em.GetComponentData<CHeadlessAutoChessExecuteFacts>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            ResetProcessedCountsIfFrameChanged(ref facts, frame);

            ProjectExecuteTriggers(em, eventBusEntity, ref facts, frame);
            ProjectExecuteDamageResolvedFacts(em, eventBusEntity, ref facts, frame);
            ProjectExecuteDamageAppliedFacts(em, eventBusEntity, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessExecuteFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedGameplayEventCount = 0;
            facts.ProcessedDamageEventCount = 0;
            facts.ProcessedAttributeEventCount = 0;
        }

        private static void ProjectExecuteTriggers(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessExecuteFacts facts,
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
                if (!TryGetExecuteRules(em, evt.Source, out var rules)
                    || rules.DamageGameplayEffectCode <= 0
                    || evt.Source == Entity.Null
                    || evt.Target == Entity.Null
                    || evt.Source == evt.Target
                    || evt.Amount < rules.MinDamage
                    || !IsAliveAutoChessUnit(em, evt.Source)
                    || !IsAliveAutoChessUnit(em, evt.Target)
                    || !IsEnemy(em, evt.Source, evt.Target)
                    || !HasDenseTag(em, evt.Source, rules.ReadyTagIndex)
                    || HasDenseTag(em, evt.Target, rules.ExecutedTagIndex))
                {
                    continue;
                }

                var targetHealth = GetAttribute(em, evt.Target, rules.HealthAttrSetCode, rules.HealthAttrCode);
                if (targetHealth > rules.HealthThreshold)
                    continue;

                CreateApplyRequest(
                    em,
                    evt.Source,
                    evt.Target,
                    rules.DamageGameplayEffectCode,
                    "AutoChessExecuteDamage");

                var executeState = em.GetComponentData<CHeadlessAutoChessExecuteState>(evt.Source);
                executeState.TriggerRequestCount++;
                executeState.LastExecuteFrame = frame;
                executeState.LastExecuteSource = evt.Source;
                executeState.LastExecuteTarget = evt.Target;
                executeState.LastExecuteDamage = evt.Amount;
                em.SetComponentData(evt.Source, executeState);

                facts.ExecuteTriggeredFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessExecuteTriggered,
                    SourceAsc = evt.Source,
                    TargetAsc = evt.Target,
                    EventCode = rules.DamageGameplayEffectCode,
                    ReasonCode = rules.ReadyTagIndex,
                    Value = targetHealth,
                });
            }

            facts.ProcessedDamageEventCount = em.Exists(eventBusEntity)
                                              && em.HasBuffer<BDamageEvent>(eventBusEntity)
                ? em.GetBuffer<BDamageEvent>(eventBusEntity).Length
                : eventCount;
        }

        private static void ProjectExecuteDamageResolvedFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessExecuteFacts facts,
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
                if (evt.Type != EGameplayEventType.AutoChessDamageTypeResolved
                    || evt.SourceAsc == Entity.Null
                    || evt.TargetAsc == Entity.Null
                    || !TryGetExecuteRules(em, evt.SourceAsc, out var rules)
                    || !IsExecuteDamageResolvedEvent(em, evt, rules))
                {
                    continue;
                }

                ProjectExecuteDamageApplied(
                    em,
                    eventBusEntity,
                    evt.SourceAsc,
                    evt.TargetAsc,
                    evt.SourceAbility,
                    evt.GameplayEffect,
                    evt.ContextId,
                    evt.Value,
                    rules,
                    ref facts,
                    frame);
            }

            facts.ProcessedGameplayEventCount = em.Exists(eventBusEntity)
                                                && em.HasBuffer<BGameplayEvent>(eventBusEntity)
                ? em.GetBuffer<BGameplayEvent>(eventBusEntity).Length
                : eventCount;
        }

        private static void ProjectExecuteDamageAppliedFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessExecuteFacts facts,
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
                    || evt.SourceAsc == Entity.Null
                    || evt.NewValue >= evt.OldValue
                    || !TryGetExecuteRules(em, evt.SourceAsc, out var rules)
                    || evt.AttrSetCode != rules.HealthAttrSetCode
                    || evt.AttributeCode != rules.HealthAttrCode
                    || !IsExecuteDamageAttributeEvent(em, evt, rules))
                {
                    continue;
                }

                var amount = evt.OldValue - evt.NewValue;
                ProjectExecuteDamageApplied(
                    em,
                    eventBusEntity,
                    evt.SourceAsc,
                    evt.ASC,
                    evt.SourceAbility,
                    evt.GameplayEffect,
                    evt.ContextId,
                    amount,
                    rules,
                    ref facts,
                    frame);
            }

            facts.ProcessedAttributeEventCount = eventCount;
        }

        private static bool TryGetExecuteRules(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessExecuteRules rules)
        {
            if (asc != Entity.Null
                && em.Exists(asc)
                && em.HasComponent<CHeadlessAutoChessExecuteRules>(asc)
                && em.HasComponent<CHeadlessAutoChessExecuteState>(asc))
            {
                rules = em.GetComponentData<CHeadlessAutoChessExecuteRules>(asc);
                return true;
            }

            rules = default;
            return false;
        }

        private static bool IsExecuteDamageAttributeEvent(
            EntityManager em,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessExecuteRules rules)
        {
            return rules.DamageGameplayEffectCode > 0
                   && (evt.EventCode == rules.DamageGameplayEffectCode
                       || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.DamageGameplayEffectCode));
        }

        private static bool IsExecuteDamageResolvedEvent(
            EntityManager em,
            in BGameplayEvent evt,
            in CHeadlessAutoChessExecuteRules rules)
        {
            return rules.DamageGameplayEffectCode > 0
                   && rules.DamageTypeCode > 0
                   && evt.EventCode == rules.DamageTypeCode
                   && IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.DamageGameplayEffectCode);
        }

        private static void ProjectExecuteDamageApplied(
            EntityManager em,
            Entity eventBusEntity,
            Entity source,
            Entity target,
            Entity sourceAbility,
            Entity gameplayEffect,
            int contextId,
            float amount,
            in CHeadlessAutoChessExecuteRules rules,
            ref CHeadlessAutoChessExecuteFacts facts,
            int frame)
        {
            if (amount <= 0f
                || source == Entity.Null
                || !em.Exists(source)
                || !em.HasComponent<CHeadlessAutoChessExecuteState>(source))
            {
                return;
            }

            var executeState = em.GetComponentData<CHeadlessAutoChessExecuteState>(source);
            executeState.DamageAppliedCount++;
            executeState.LastExecuteFrame = frame;
            executeState.LastExecuteSource = source;
            executeState.LastExecuteTarget = target;
            executeState.LastExecuteGameplayEffect = gameplayEffect;
            executeState.LastExecuteDamage = amount;
            em.SetComponentData(source, executeState);

            facts.ExecuteDamageAppliedFactCount++;
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessExecuteDamageApplied,
                SourceAsc = source,
                TargetAsc = target,
                SourceAbility = sourceAbility,
                GameplayEffect = gameplayEffect,
                ContextId = contextId,
                EventCode = rules.DamageGameplayEffectCode,
                ReasonCode = rules.ExecutedTagIndex,
                Value = amount,
            });
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

            var targetKind = source == target ? ETargetDataKind.Self : ETargetDataKind.Entity;
            GameplayEffectRequestWriter.ApplyFastOrCreateSingleTargetRequest(
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
