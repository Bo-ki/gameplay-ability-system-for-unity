using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SAbilityStateCleanup))]
    public partial struct SHeadlessAutoChessPassiveReaction : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;
        private EntityQuery _revivePendingUnitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessPassiveReactionFacts, BHeadlessAutoChessUnitDefeatedFact>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, BAttribute>()
                .Build();
            _revivePendingUnitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessPassiveState, BAttribute>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_unitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasBuffer<BGameplayEvent>(eventBusEntity))
                return;

            if (_driverQuery.IsEmptyIgnoreFilter)
                return;

            var driverEntity = _driverQuery.GetSingletonEntity();
            var facts = em.GetComponentData<CHeadlessAutoChessPassiveReactionFacts>(driverEntity);
            var unitDefeatedFacts = em.GetBuffer<BHeadlessAutoChessUnitDefeatedFact>(driverEntity);
            var frame = ResolveCurrentFrame(em);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);

            if (facts.LastReactionFrame != frame)
            {
                facts.LastReactionFrame = frame;
                facts.ProcessedGameplayEventCount = 0;
                facts.ProcessedUnitDefeatedFactCount = 0;
            }

            ProjectReviveAppliedFacts(em, eventBusEntity, _revivePendingUnitQuery, ref facts, frame);
            ProjectDefeatReactions(em, eventBusEntity, unitDefeatedFacts, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        private static void ProjectDefeatReactions(
            EntityManager em,
            Entity eventBusEntity,
            DynamicBuffer<BHeadlessAutoChessUnitDefeatedFact> unitDefeatedFacts,
            ref CHeadlessAutoChessPassiveReactionFacts facts,
            int frame)
        {
            var eventCount = unitDefeatedFacts.Length;
            var start = EventBusHelper.ClampProcessedCount(
                unitDefeatedFacts,
                facts.ProcessedUnitDefeatedFactCount);
            using var defeatFacts = EventBusHelper.CopyBufferRange(
                unitDefeatedFacts,
                start,
                eventCount,
                Allocator.Temp);

            for (var i = 0; i < defeatFacts.Length; i++)
            {
                var fact = defeatFacts[i];
                var evt = new BGameplayEvent
                {
                    SourceAsc = fact.SourceAsc,
                    TargetAsc = fact.TargetAsc,
                    EventCode = fact.UnitSlot,
                    ReasonCode = (int)fact.Team,
                    Value = fact.FinalHealth,
                };
                TryGrantKillMana(em, eventBusEntity, evt, frame, ref facts);
                TryRequestSelfRevive(em, eventBusEntity, evt, frame, ref facts);
            }

            facts.ProcessedUnitDefeatedFactCount = eventCount;
        }

        private static bool TryGrantKillMana(
            EntityManager em,
            Entity eventBusEntity,
            in BGameplayEvent defeatEvent,
            int frame,
            ref CHeadlessAutoChessPassiveReactionFacts facts)
        {
            var killer = defeatEvent.SourceAsc;
            var defeated = defeatEvent.TargetAsc;
            if (!IsAliveAutoChessUnit(em, killer)
                || !IsAutoChessUnit(em, defeated)
                || !em.HasComponent<CHeadlessAutoChessPassiveRules>(killer))
            {
                return false;
            }

            var killerUnit = em.GetComponentData<CHeadlessAutoChessUnit>(killer);
            var defeatedUnit = em.GetComponentData<CHeadlessAutoChessUnit>(defeated);
            if (killerUnit.Team == defeatedUnit.Team)
                return false;

            var rules = em.GetComponentData<CHeadlessAutoChessPassiveRules>(killer);
            if (rules.KillManaGainGameplayEffectCode <= 0)
                return false;

            CreateApplyRequest(
                em,
                killer,
                killer,
                rules.KillManaGainGameplayEffectCode,
                "AutoChessKillMana");

            var passiveState = GetOrCreatePassiveState(em, killer);
            passiveState.KillManaGainCount++;
            em.SetComponentData(killer, passiveState);

            facts.KillManaGrantedFactCount++;
            facts.PassiveTriggeredFactCount++;
            EmitPassiveTriggered(
                em,
                eventBusEntity,
                killer,
                defeated,
                rules.KillManaGainGameplayEffectCode,
                HeadlessAutoChessPassiveReactionKind.KillManaGain,
                HeadlessAutoChessScenario.KillManaGainAmount);
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessKillManaGranted,
                SourceAsc = killer,
                TargetAsc = killer,
                EventCode = rules.KillManaGainGameplayEffectCode,
                ReasonCode = defeatedUnit.Slot,
                Value = HeadlessAutoChessScenario.KillManaGainAmount,
            });
            return true;
        }

        private static bool TryRequestSelfRevive(
            EntityManager em,
            Entity eventBusEntity,
            in BGameplayEvent defeatEvent,
            int frame,
            ref CHeadlessAutoChessPassiveReactionFacts facts)
        {
            var defeated = defeatEvent.TargetAsc;
            if (!IsAutoChessUnit(em, defeated)
                || !em.HasComponent<CHeadlessAutoChessPassiveRules>(defeated))
            {
                return false;
            }

            var rules = em.GetComponentData<CHeadlessAutoChessPassiveRules>(defeated);
            if (rules.ReviveGameplayEffectCode <= 0 || rules.MaxReviveCount <= 0)
                return false;

            var passiveState = GetOrCreatePassiveState(em, defeated);
            if (passiveState.RevivePending || passiveState.ReviveCount >= rules.MaxReviveCount)
                return false;

            passiveState.ReviveCount++;
            passiveState.RevivePending = true;
            passiveState.LastReviveRequestFrame = frame;
            em.SetComponentData(defeated, passiveState);

            if (em.HasComponent<CHeadlessAutoChessDeathState>(defeated))
            {
                var deathState = em.GetComponentData<CHeadlessAutoChessDeathState>(defeated);
                deathState.Defeated = false;
                em.SetComponentData(defeated, deathState);
            }

            CreateApplyRequest(
                em,
                defeated,
                defeated,
                rules.ReviveGameplayEffectCode,
                "AutoChessRevive");

            var defeatedUnit = em.GetComponentData<CHeadlessAutoChessUnit>(defeated);
            facts.ReviveRequestedFactCount++;
            facts.PassiveTriggeredFactCount++;
            EmitPassiveTriggered(
                em,
                eventBusEntity,
                defeated,
                defeated,
                rules.ReviveGameplayEffectCode,
                HeadlessAutoChessPassiveReactionKind.SelfRevive,
                HeadlessAutoChessScenario.ReviveHealthAmount);
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessReviveRequested,
                SourceAsc = defeated,
                TargetAsc = defeated,
                EventCode = rules.ReviveGameplayEffectCode,
                ReasonCode = defeatedUnit.Slot,
                Value = HeadlessAutoChessScenario.ReviveHealthAmount,
            });
            return true;
        }

        private static void ProjectReviveAppliedFacts(
            EntityManager em,
            Entity eventBusEntity,
            EntityQuery revivePendingUnitQuery,
            ref CHeadlessAutoChessPassiveReactionFacts facts,
            int frame)
        {
            using var units = revivePendingUnitQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < units.Length; i++)
            {
                var asc = units[i];
                if (!em.Exists(asc))
                    continue;

                var passiveState = em.GetComponentData<CHeadlessAutoChessPassiveState>(asc);
                if (!passiveState.RevivePending)
                    continue;

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
                var health = GetAttribute(em, asc, unit.HealthAttrSetCode, unit.HealthAttrCode);
                if (health <= 0f)
                    continue;

                passiveState.RevivePending = false;
                passiveState.LastReviveAppliedFrame = frame;
                em.SetComponentData(asc, passiveState);

                facts.ReviveAppliedFactCount++;
                var effectCode = em.HasComponent<CHeadlessAutoChessPassiveRules>(asc)
                    ? em.GetComponentData<CHeadlessAutoChessPassiveRules>(asc).ReviveGameplayEffectCode
                    : 0;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessReviveApplied,
                    SourceAsc = asc,
                    TargetAsc = asc,
                    EventCode = effectCode,
                    ReasonCode = passiveState.ReviveCount,
                    Value = health,
                });
            }
        }

        private static void EmitPassiveTriggered(
            EntityManager em,
            Entity eventBusEntity,
            Entity source,
            Entity target,
            int gameplayEffectCode,
            HeadlessAutoChessPassiveReactionKind kind,
            float value)
        {
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessPassiveTriggered,
                SourceAsc = source,
                TargetAsc = target,
                EventCode = gameplayEffectCode,
                ReasonCode = (int)kind,
                Value = value,
            });
        }

        private static void CreateApplyRequest(
            EntityManager em,
            Entity source,
            Entity target,
            int gameplayEffectCode,
            string namePrefix)
        {
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

        private static CHeadlessAutoChessPassiveState GetOrCreatePassiveState(
            EntityManager em,
            Entity asc)
        {
            if (em.HasComponent<CHeadlessAutoChessPassiveState>(asc))
                return em.GetComponentData<CHeadlessAutoChessPassiveState>(asc);

            var state = new CHeadlessAutoChessPassiveState();
            em.AddComponentData(asc, state);
            return state;
        }

        private static bool IsAliveAutoChessUnit(EntityManager em, Entity asc)
        {
            if (!IsAutoChessUnit(em, asc))
                return false;

            var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
            return GetAttribute(em, asc, unit.HealthAttrSetCode, unit.HealthAttrCode) > 0f;
        }

        private static bool IsAutoChessUnit(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && em.HasComponent<CHeadlessAutoChessUnit>(asc)
                   && em.HasBuffer<BAttribute>(asc);
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

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
