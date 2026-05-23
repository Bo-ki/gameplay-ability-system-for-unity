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

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessPassiveReactionFacts>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, BAttribute>()
                .Build();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_unitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();

            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasBuffer<BGameplayEvent>(eventBusEntity))
                return;

            using var drivers = _driverQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0)
                return;

            var driverEntity = drivers[0];
            var facts = em.GetComponentData<CHeadlessAutoChessPassiveReactionFacts>(driverEntity);
            var frame = ResolveCurrentFrame(em);

            if (facts.LastReactionFrame != frame)
            {
                facts.LastReactionFrame = frame;
                facts.ProcessedGameplayEventCount = 0;
            }

            ProjectReviveAppliedFacts(em, eventBusEntity, ref facts, frame);
            ProjectDefeatReactions(em, eventBusEntity, ref facts, frame);

            em.SetComponentData(driverEntity, facts);
        }

        private static void ProjectDefeatReactions(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessPassiveReactionFacts facts,
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
                if (evt.Type != EGameplayEventType.AutoChessUnitDefeated)
                    continue;

                TryGrantKillMana(em, eventBusEntity, evt, frame, ref facts);
                TryRequestSelfRevive(em, eventBusEntity, evt, frame, ref facts);
            }

            facts.ProcessedGameplayEventCount = em.Exists(eventBusEntity)
                                                && em.HasBuffer<BGameplayEvent>(eventBusEntity)
                ? em.GetBuffer<BGameplayEvent>(eventBusEntity).Length
                : eventCount;
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
            ref CHeadlessAutoChessPassiveReactionFacts facts,
            int frame)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CHeadlessAutoChessUnit>(),
                ComponentType.ReadWrite<CHeadlessAutoChessPassiveState>(),
                ComponentType.ReadOnly<BAttribute>());
            using var units = query.ToEntityArray(Allocator.Temp);

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
