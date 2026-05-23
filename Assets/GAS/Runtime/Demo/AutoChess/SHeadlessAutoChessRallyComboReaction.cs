using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessCleanseReaction))]
    [UpdateBefore(typeof(SHeadlessAutoChessSynergyProjection))]
    public partial struct SHeadlessAutoChessRallyComboReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _comboUnitQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessRallyComboFacts>()
                .Build();
            _comboUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessRallyComboRules, CHeadlessAutoChessRallyComboState, CTagMask>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, BAttribute>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_comboUnitQuery);
            state.RequireForUpdate(_unitQuery);
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

            using var units = _unitQuery.ToEntityArray(Allocator.Temp);
            if (_driverQuery.IsEmptyIgnoreFilter || units.Length == 0)
                return;

            var driverEntity = _driverQuery.GetSingletonEntity();
            var facts = em.GetComponentData<CHeadlessAutoChessRallyComboFacts>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            ResetProcessedCountsIfFrameChanged(ref facts, frame);

            ProjectRallyComboTriggers(em, eventBusEntity, units, ref facts, frame);
            ProjectRallyComboDamageAppliedFacts(em, eventBusEntity, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CHeadlessAutoChessRallyComboFacts facts,
            int frame)
        {
            if (facts.LastProjectionFrame == frame)
                return;

            facts.LastProjectionFrame = frame;
            facts.ProcessedGameplayEventCount = 0;
            facts.ProcessedAttributeEventCount = 0;
        }

        private static void ProjectRallyComboTriggers(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<Entity> units,
            ref CHeadlessAutoChessRallyComboFacts facts,
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
                var comboSource = evt.TargetAsc;
                if (evt.Type != EGameplayEventType.AutoChessCleanseRallyApplied
                    || !TryGetComboRules(em, comboSource, out var rules)
                    || rules.ComboDamageGameplayEffectCode <= 0
                    || !HasDenseTag(em, comboSource, rules.RalliedTagIndex)
                    || !TrySelectEnemyTarget(em, units, comboSource, out var comboTarget))
                {
                    continue;
                }

                var comboState = em.GetComponentData<CHeadlessAutoChessRallyComboState>(comboSource);
                if (comboState.LastRallyGameplayEffect == evt.GameplayEffect)
                    continue;

                CreateApplyRequest(
                    em,
                    comboSource,
                    comboTarget,
                    rules.ComboDamageGameplayEffectCode,
                    "AutoChessRallyCombo");

                comboState.ComboRequestCount++;
                comboState.LastComboFrame = frame;
                comboState.LastComboSource = comboSource;
                comboState.LastComboTarget = comboTarget;
                comboState.LastRallyGameplayEffect = evt.GameplayEffect;
                comboState.LastComboDamage = rules.ComboDamage;
                em.SetComponentData(comboSource, comboState);

                facts.RallyComboTriggeredFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessRallyComboTriggered,
                    SourceAsc = comboSource,
                    TargetAsc = comboTarget,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.ComboDamageGameplayEffectCode,
                    ReasonCode = rules.RalliedTagIndex,
                    Value = rules.ComboDamage,
                });
            }

            facts.ProcessedGameplayEventCount = em.Exists(eventBusEntity)
                                                && em.HasBuffer<BGameplayEvent>(eventBusEntity)
                ? em.GetBuffer<BGameplayEvent>(eventBusEntity).Length
                : eventCount;
        }

        private static void ProjectRallyComboDamageAppliedFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessRallyComboFacts facts,
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
                    || !TryGetComboRules(em, evt.SourceAsc, out var rules)
                    || evt.AttrSetCode != rules.HealthAttrSetCode
                    || evt.AttributeCode != rules.HealthAttrCode
                    || !IsComboDamageAttributeEvent(em, evt, rules))
                {
                    continue;
                }

                var amount = evt.OldValue - evt.NewValue;
                var comboState = em.GetComponentData<CHeadlessAutoChessRallyComboState>(evt.SourceAsc);
                comboState.ComboDamageAppliedCount++;
                comboState.LastComboFrame = frame;
                comboState.LastComboSource = evt.SourceAsc;
                comboState.LastComboTarget = evt.ASC;
                comboState.LastComboDamage = amount;
                em.SetComponentData(evt.SourceAsc, comboState);

                facts.RallyComboDamageAppliedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessRallyComboDamageApplied,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.ASC,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = rules.ComboDamageGameplayEffectCode,
                    ReasonCode = rules.RalliedTagIndex,
                    Value = amount,
                });
            }

            facts.ProcessedAttributeEventCount = eventCount;
        }

        private static bool TryGetComboRules(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessRallyComboRules rules)
        {
            if (asc != Entity.Null
                && em.Exists(asc)
                && em.HasComponent<CHeadlessAutoChessRallyComboRules>(asc)
                && em.HasComponent<CHeadlessAutoChessRallyComboState>(asc))
            {
                rules = em.GetComponentData<CHeadlessAutoChessRallyComboRules>(asc);
                return true;
            }

            rules = default;
            return false;
        }

        private static bool TrySelectEnemyTarget(
            EntityManager em,
            NativeArray<Entity> units,
            Entity source,
            out Entity target)
        {
            target = Entity.Null;
            if (source == Entity.Null || !em.Exists(source) || !em.HasComponent<CHeadlessAutoChessUnit>(source))
                return false;

            var sourceUnit = em.GetComponentData<CHeadlessAutoChessUnit>(source);
            var bestHealth = float.MaxValue;
            var bestTurnOrder = int.MaxValue;
            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                if (candidate == source
                    || candidate == Entity.Null
                    || !em.Exists(candidate)
                    || !em.HasComponent<CHeadlessAutoChessUnit>(candidate)
                    || !em.HasBuffer<BAttribute>(candidate))
                {
                    continue;
                }

                var candidateUnit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (candidateUnit.Team == sourceUnit.Team || candidateUnit.Team == HeadlessAutoChessTeam.None)
                    continue;

                var health = GetAttribute(em, candidate, candidateUnit.HealthAttrSetCode, candidateUnit.HealthAttrCode);
                if (health <= 0f)
                    continue;

                if (health < bestHealth
                    || (health == bestHealth && candidateUnit.TurnOrder < bestTurnOrder))
                {
                    target = candidate;
                    bestHealth = health;
                    bestTurnOrder = candidateUnit.TurnOrder;
                }
            }

            return target != Entity.Null;
        }

        private static bool IsComboDamageAttributeEvent(
            EntityManager em,
            in BAttributeChangeEvent evt,
            in CHeadlessAutoChessRallyComboRules rules)
        {
            return evt.EventCode == rules.ComboDamageGameplayEffectCode
                   || IsGameplayEffectWithCode(em, evt.GameplayEffect, rules.ComboDamageGameplayEffectCode);
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
