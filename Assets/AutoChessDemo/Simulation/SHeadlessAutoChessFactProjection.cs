using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SInstantEffectCueRequestProjection))]
    [UpdateAfter(typeof(SHeadlessAutoChessSummonProjection))]
    [UpdateBefore(typeof(SAscDestroyRequest))]
    public partial struct SHeadlessAutoChessFactProjection : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    CHeadlessAutoChessDriver,
                    CHeadlessAutoChessBattleFacts,
                    CHeadlessAutoChessGameplayEffectFacts,
                    BHeadlessAutoChessUnitDefeatedFact,
                    BHeadlessAutoChessGameplayEffectAppliedFact>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, BAttribute>()
                .Build();
            state.RequireForUpdate(_driverQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var drivers = _driverQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0)
                return;

            var driverEntity = drivers[0];
            var driver = em.GetComponentData<CHeadlessAutoChessDriver>(driverEntity);
            var battleFacts = em.GetComponentData<CHeadlessAutoChessBattleFacts>(driverEntity);
            var effectFacts = em.GetComponentData<CHeadlessAutoChessGameplayEffectFacts>(driverEntity);
            var defeatedFacts = em.GetBuffer<BHeadlessAutoChessUnitDefeatedFact>(driverEntity);
            var appliedFacts = em.GetBuffer<BHeadlessAutoChessGameplayEffectAppliedFact>(driverEntity);
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            try
            {
                if (SystemAPI.TryGetSingletonEntity<CEffectCommandSpecStream>(out var streamEntity)
                    && em.Exists(streamEntity)
                    && em.HasBuffer<BTypedSimulationFact>(streamEntity))
                {
                    ProjectTypedFacts(
                        em,
                        em.GetBuffer<BTypedSimulationFact>(streamEntity),
                        ref battleFacts,
                        defeatedFacts,
                        frame,
                        ref eventWriter);
                }

                if (eventBusEntity != Entity.Null
                    && em.Exists(eventBusEntity)
                    && em.HasBuffer<BGameplayEvent>(eventBusEntity))
                {
                    ProjectGameplayEffectEvents(
                        em.GetBuffer<BGameplayEvent>(eventBusEntity),
                        ref effectFacts,
                        appliedFacts,
                        frame);
                }

                ResolveBattleOutcome(
                    em,
                    ref driver,
                    ref battleFacts,
                    frame,
                    ref eventWriter);
            }
            finally
            {
                eventWriter.Dispose();
            }

            em.SetComponentData(driverEntity, driver);
            em.SetComponentData(driverEntity, battleFacts);
            em.SetComponentData(driverEntity, effectFacts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ProjectTypedFacts(
            EntityManager em,
            DynamicBuffer<BTypedSimulationFact> facts,
            ref CHeadlessAutoChessBattleFacts battleFacts,
            DynamicBuffer<BHeadlessAutoChessUnitDefeatedFact> defeatedFacts,
            int frame,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            for (var i = 0; i < facts.Length; i++)
            {
                var fact = facts[i];
                if (fact.Sequence <= battleFacts.LastProcessedTypedFactSequence)
                    continue;
                if (fact.Sequence > battleFacts.LastProcessedTypedFactSequence)
                    battleFacts.LastProcessedTypedFactSequence = fact.Sequence;

                if (fact.Domain != EGameplayFactDomain.Attribute
                    || fact.EventType != EGameplayEventType.AttributeBaseValueChanged
                    || fact.TargetAsc == Entity.Null
                    || !em.Exists(fact.TargetAsc)
                    || !em.HasComponent<CHeadlessAutoChessUnit>(fact.TargetAsc))
                {
                    continue;
                }

                battleFacts.ProcessedAttributeEventCount++;
                battleFacts.LastTypedFactFrame = frame;

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(fact.TargetAsc);
                if (IsHealthAttribute(in unit, fact.AttrSetCode, fact.AttributeCode)
                    && fact.NewValue < fact.OldValue)
                {
                    battleFacts.HealthDamageFactCount++;
                    SetDamageState(em, fact.TargetAsc, fact.SourceAsc, frame, fact.OldValue - fact.NewValue);
                    if (fact.NewValue <= 0f)
                        RecordDefeat(
                            em,
                            fact.TargetAsc,
                            in unit,
                            in fact,
                            ref battleFacts,
                            defeatedFacts,
                            frame,
                            ref eventWriter);
                }
                else if (IsShieldAttribute(in unit, fact.AttrSetCode, fact.AttributeCode)
                         && fact.NewValue > fact.OldValue)
                {
                    battleFacts.ShieldAppliedFactCount++;
                }
            }
        }

        private static void ProjectGameplayEffectEvents(
            DynamicBuffer<BGameplayEvent> events,
            ref CHeadlessAutoChessGameplayEffectFacts effectFacts,
            DynamicBuffer<BHeadlessAutoChessGameplayEffectAppliedFact> appliedFacts,
            int frame)
        {
            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                if (evt.Sequence <= effectFacts.LastProcessedGameplayEventSequence)
                    continue;
                if (evt.Sequence > effectFacts.LastProcessedGameplayEventSequence)
                    effectFacts.LastProcessedGameplayEventSequence = evt.Sequence;

                effectFacts.ProcessedGameplayEventCount++;
                effectFacts.LastProjectionFrame = frame;
                if (evt.Type != EGameplayEventType.GameplayEffectApplied)
                    continue;

                effectFacts.GameplayEffectAppliedFactCount++;
                appliedFacts.Add(new BHeadlessAutoChessGameplayEffectAppliedFact
                {
                    Frame = evt.Frame != 0 ? evt.Frame : frame,
                    Sequence = evt.Sequence,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.TargetAsc,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    RelatedAbility = evt.RelatedAbility,
                    ContextId = evt.ContextId,
                    GameplayEffectCode = evt.EventCode,
                    ReasonCode = evt.ReasonCode,
                    RelatedAbilityCode = evt.RelatedAbilityCode,
                    Value = evt.Value,
                });
            }
        }

        private static void RecordDefeat(
            EntityManager em,
            Entity targetAsc,
            in CHeadlessAutoChessUnit unit,
            in BTypedSimulationFact fact,
            ref CHeadlessAutoChessBattleFacts battleFacts,
            DynamicBuffer<BHeadlessAutoChessUnitDefeatedFact> defeatedFacts,
            int frame,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            var deathState = em.HasComponent<CHeadlessAutoChessDeathState>(targetAsc)
                ? em.GetComponentData<CHeadlessAutoChessDeathState>(targetAsc)
                : default;
            if (deathState.Defeated)
                return;

            deathState.Defeated = true;
            deathState.DefeatFrame = frame;
            deathState.DefeatRound = 0;
            deathState.DefeatTurn = 0;
            deathState.FinalHealth = fact.NewValue;
            deathState.DefeatedBy = fact.SourceAsc;
            if (em.HasComponent<CHeadlessAutoChessDeathState>(targetAsc))
                em.SetComponentData(targetAsc, deathState);
            else
                em.AddComponentData(targetAsc, deathState);

            defeatedFacts.Add(new BHeadlessAutoChessUnitDefeatedFact
            {
                SourceAsc = fact.SourceAsc,
                TargetAsc = targetAsc,
                UnitSlot = unit.Slot,
                Team = unit.Team,
                Frame = frame,
                Round = 0,
                Turn = 0,
                FinalHealth = fact.NewValue,
            });

            if (unit.Team == HeadlessAutoChessTeam.Player)
                battleFacts.PlayerDefeatedCount++;
            else if (unit.Team == HeadlessAutoChessTeam.Enemy)
                battleFacts.EnemyDefeatedCount++;

            battleFacts.UnitDefeatedFactCount++;
            if (battleFacts.FirstDefeatFrame <= 0)
                battleFacts.FirstDefeatFrame = frame;
            battleFacts.LastDefeatFrame = frame;

            if (eventWriter.IsCreated)
            {
                eventWriter.EnqueueGameplayEvent(new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessUnitDefeated,
                    SourceAsc = fact.SourceAsc,
                    TargetAsc = targetAsc,
                    GameplayEffect = fact.SourceEffect,
                    ContextId = fact.ContextId,
                    EventCode = unit.Slot,
                    ReasonCode = (int)unit.Team,
                    Value = fact.NewValue,
                });
            }
        }

        private void ResolveBattleOutcome(
            EntityManager em,
            ref CHeadlessAutoChessDriver driver,
            ref CHeadlessAutoChessBattleFacts battleFacts,
            int frame,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (battleFacts.BattleResolved)
                return;

            using var units = _unitQuery.ToEntityArray(Allocator.Temp);
            var playerAlive = false;
            var enemyAlive = false;
            for (var i = 0; i < units.Length; i++)
            {
                var asc = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
                if (!IsAlive(em, asc, in unit))
                    continue;

                if (unit.Team == HeadlessAutoChessTeam.Player)
                    playerAlive = true;
                else if (unit.Team == HeadlessAutoChessTeam.Enemy)
                    enemyAlive = true;
            }

            if (playerAlive && enemyAlive)
                return;

            var winner = playerAlive == enemyAlive
                ? HeadlessAutoChessTeam.Draw
                : playerAlive
                    ? HeadlessAutoChessTeam.Player
                    : HeadlessAutoChessTeam.Enemy;

            battleFacts.BattleResolved = true;
            battleFacts.BattleResolvedFactCount++;
            battleFacts.BattleResolvedFrame = frame;
            battleFacts.Winner = winner;
            driver.Completed = true;
            driver.Winner = winner;

            if (eventWriter.IsCreated)
            {
                eventWriter.EnqueueGameplayEvent(new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessBattleResolved,
                    EventCode = (int)winner,
                    Value = frame,
                });
            }
        }

        private static void SetDamageState(
            EntityManager em,
            Entity targetAsc,
            Entity sourceAsc,
            int frame,
            float amount)
        {
            var state = em.HasComponent<CHeadlessAutoChessDamageState>(targetAsc)
                ? em.GetComponentData<CHeadlessAutoChessDamageState>(targetAsc)
                : default;
            state.LastDamageSource = sourceAsc;
            state.LastDamageFrame = frame;
            state.LastDamageAmount = amount;
            if (em.HasComponent<CHeadlessAutoChessDamageState>(targetAsc))
                em.SetComponentData(targetAsc, state);
            else
                em.AddComponentData(targetAsc, state);
        }

        private static bool IsAlive(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && !em.HasComponent<CAscDestroying>(asc)
                   && ReadAttribute(em, asc, unit.HealthAttrSetCode, unit.HealthAttrCode) > 0f;
        }

        private static bool IsHealthAttribute(
            in CHeadlessAutoChessUnit unit,
            int attrSetCode,
            int attrCode)
        {
            return attrSetCode == unit.HealthAttrSetCode && attrCode == unit.HealthAttrCode;
        }

        private static bool IsShieldAttribute(
            in CHeadlessAutoChessUnit unit,
            int attrSetCode,
            int attrCode)
        {
            return attrSetCode == unit.ShieldAttrSetCode && attrCode == unit.ShieldAttrCode;
        }

        private static float ReadAttribute(
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
    }
}
