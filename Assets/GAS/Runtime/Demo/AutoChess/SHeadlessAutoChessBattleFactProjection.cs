using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAttributeGroup))]
    [UpdateAfter(typeof(SAttributeChangeEventProjection))]
    public partial struct SHeadlessAutoChessBattleFactProjection : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessBattleFacts>()
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
            if (!em.Exists(eventBusEntity)
                || !em.HasBuffer<BGameplayEvent>(eventBusEntity)
                || !em.HasBuffer<BDamageEvent>(eventBusEntity))
            {
                return;
            }

            using var drivers = _driverQuery.ToEntityArray(Allocator.Temp);
            using var units = _unitQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0 || units.Length == 0)
                return;

            var driverEntity = drivers[0];
            var driver = em.GetComponentData<CHeadlessAutoChessDriver>(driverEntity);
            var facts = em.GetComponentData<CHeadlessAutoChessBattleFacts>(driverEntity);
            var frame = ResolveCurrentFrame(em);

            ProjectCombatAttributeFacts(em, eventBusEntity, ref facts, frame);
            ProjectUnitDefeatedFacts(em, eventBusEntity, units, driver, ref facts, frame);
            ProjectBattleResolvedFact(em, eventBusEntity, driverEntity, units, ref driver, ref facts, frame);

            em.SetComponentData(driverEntity, driver);
            em.SetComponentData(driverEntity, facts);
        }

        private static void ProjectCombatAttributeFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessBattleFacts facts,
            int frame)
        {
            if (!em.HasBuffer<BAttributeChangeEvent>(eventBusEntity))
                return;

            if (facts.LastDamageProjectionFrame != frame)
            {
                facts.LastDamageProjectionFrame = frame;
                facts.ProcessedAttributeEventCount = 0;
            }

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
                if (IsAutoChessHealthDamage(em, evt))
                {
                    var amount = evt.OldValue - evt.NewValue;
                    if (amount <= 0f)
                        continue;

                    SetDamageState(em, evt, amount, frame);
                    EventBusHelper.EnqueueDamageEvent(em, eventBusEntity, new BDamageEvent
                    {
                        Target = evt.ASC,
                        Source = evt.SourceAsc,
                        Amount = amount,
                    });
                    facts.HealthDamageFactCount++;
                    continue;
                }

                if (IsAutoChessShieldGain(em, evt))
                {
                    var amount = evt.NewValue - evt.OldValue;
                    if (amount <= 0f)
                        continue;

                    facts.ShieldAppliedFactCount++;
                    EmitShieldFact(
                        em,
                        eventBusEntity,
                        evt,
                        EGameplayEventType.AutoChessShieldApplied,
                        amount);
                    continue;
                }

                if (IsAutoChessShieldLoss(em, evt))
                {
                    var amount = evt.OldValue - evt.NewValue;
                    if (amount <= 0f)
                        continue;

                    facts.ShieldAbsorbedFactCount++;
                    EmitShieldFact(
                        em,
                        eventBusEntity,
                        evt,
                        EGameplayEventType.AutoChessShieldAbsorbed,
                        amount);

                    if (evt.NewValue <= 0f)
                    {
                        facts.ShieldBrokenFactCount++;
                        EmitShieldFact(
                            em,
                            eventBusEntity,
                            evt,
                            EGameplayEventType.AutoChessShieldBroken,
                            amount);
                    }
                }
            }

            facts.ProcessedAttributeEventCount = eventCount;
        }

        private static bool IsAutoChessHealthDamage(EntityManager em, in BAttributeChangeEvent evt)
        {
            return evt.ASC != Entity.Null
                   && em.Exists(evt.ASC)
                   && em.HasComponent<CHeadlessAutoChessUnit>(evt.ASC)
                   && evt.GameplayEffect != Entity.Null
                   && evt.AttrSetCode == HeadlessAutoChessScenario.AttributeSetCombat
                   && evt.AttributeCode == HeadlessAutoChessScenario.AttributeHealth
                   && evt.NewValue < evt.OldValue;
        }

        private static bool IsAutoChessShieldGain(EntityManager em, in BAttributeChangeEvent evt)
        {
            return IsAutoChessShieldEvent(em, evt) && evt.NewValue > evt.OldValue;
        }

        private static bool IsAutoChessShieldLoss(EntityManager em, in BAttributeChangeEvent evt)
        {
            return IsAutoChessShieldEvent(em, evt) && evt.NewValue < evt.OldValue;
        }

        private static bool IsAutoChessShieldEvent(EntityManager em, in BAttributeChangeEvent evt)
        {
            return evt.ASC != Entity.Null
                   && em.Exists(evt.ASC)
                   && em.HasComponent<CHeadlessAutoChessUnit>(evt.ASC)
                   && evt.GameplayEffect != Entity.Null
                   && evt.AttrSetCode == HeadlessAutoChessScenario.AttributeSetCombat
                   && evt.AttributeCode == HeadlessAutoChessScenario.AttributeShield;
        }

        private static void SetDamageState(
            EntityManager em,
            in BAttributeChangeEvent evt,
            float amount,
            int frame)
        {
            var state = new CHeadlessAutoChessDamageState
            {
                LastDamageSource = evt.SourceAsc,
                LastDamageFrame = frame,
                LastDamageAmount = amount,
            };

            if (em.HasComponent<CHeadlessAutoChessDamageState>(evt.ASC))
                em.SetComponentData(evt.ASC, state);
            else
                em.AddComponentData(evt.ASC, state);
        }

        private static void EmitShieldFact(
            EntityManager em,
            Entity eventBusEntity,
            in BAttributeChangeEvent evt,
            EGameplayEventType type,
            float amount)
        {
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = type,
                SourceAsc = evt.SourceAsc,
                TargetAsc = evt.ASC,
                SourceAbility = evt.SourceAbility,
                GameplayEffect = evt.GameplayEffect,
                ContextId = evt.ContextId,
                EventCode = HeadlessAutoChessScenario.AttributeShield,
                ReasonCode = evt.IsBaseValue ? 1 : 0,
                Value = amount,
            });
        }

        private static void ProjectUnitDefeatedFacts(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<Entity> units,
            in CHeadlessAutoChessDriver driver,
            ref CHeadlessAutoChessBattleFacts facts,
            int frame)
        {
            for (var i = 0; i < units.Length; i++)
            {
                var asc = units[i];
                if (em.HasComponent<CHeadlessAutoChessDeathState>(asc)
                    && em.GetComponentData<CHeadlessAutoChessDeathState>(asc).Defeated)
                {
                    continue;
                }

                if (IsRevivePending(em, asc))
                    continue;

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
                var health = GetAttribute(em, asc, unit.HealthAttrSetCode, unit.HealthAttrCode);
                if (health > 0f)
                    continue;

                var defeatedBy = em.HasComponent<CHeadlessAutoChessDamageState>(asc)
                    ? em.GetComponentData<CHeadlessAutoChessDamageState>(asc).LastDamageSource
                    : Entity.Null;
                var deathState = new CHeadlessAutoChessDeathState
                {
                    Defeated = true,
                    DefeatFrame = frame,
                    DefeatRound = driver.Round,
                    DefeatTurn = driver.TurnCount,
                    FinalHealth = health,
                    DefeatedBy = defeatedBy,
                };

                if (em.HasComponent<CHeadlessAutoChessDeathState>(asc))
                    em.SetComponentData(asc, deathState);
                else
                    em.AddComponentData(asc, deathState);

                facts.UnitDefeatedFactCount++;
                if (facts.FirstDefeatFrame == 0)
                    facts.FirstDefeatFrame = frame;
                facts.LastDefeatFrame = frame;

                if (unit.Team == HeadlessAutoChessTeam.Player)
                    facts.PlayerDefeatedCount++;
                else if (unit.Team == HeadlessAutoChessTeam.Enemy)
                    facts.EnemyDefeatedCount++;

                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessUnitDefeated,
                    SourceAsc = defeatedBy,
                    TargetAsc = asc,
                    EventCode = unit.Slot,
                    ReasonCode = (int)unit.Team,
                    Value = health,
                });
            }
        }

        private static void ProjectBattleResolvedFact(
            EntityManager em,
            Entity eventBusEntity,
            Entity driverEntity,
            NativeArray<Entity> units,
            ref CHeadlessAutoChessDriver driver,
            ref CHeadlessAutoChessBattleFacts facts,
            int frame)
        {
            if (facts.BattleResolved || !TryResolveWinner(em, units, out var winner))
                return;

            facts.BattleResolved = true;
            facts.BattleResolvedFrame = frame;
            facts.Winner = winner;
            facts.BattleResolvedFactCount++;

            driver.Completed = true;
            driver.Winner = winner;
            driver.LastDecisionFrame = frame;

            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessBattleResolved,
                SourceAsc = driverEntity,
                EventCode = (int)winner,
                ReasonCode = driver.Round,
                Value = driver.TurnCount,
            });
        }

        private static bool TryResolveWinner(
            EntityManager em,
            NativeArray<Entity> units,
            out HeadlessAutoChessTeam winner)
        {
            var playerAlive = false;
            var enemyAlive = false;

            for (var i = 0; i < units.Length; i++)
            {
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(units[i]);
                var health = GetAttribute(em, units[i], unit.HealthAttrSetCode, unit.HealthAttrCode);
                if (health <= 0f && !IsRevivePending(em, units[i]))
                    continue;

                if (unit.Team == HeadlessAutoChessTeam.Player)
                    playerAlive = true;
                else if (unit.Team == HeadlessAutoChessTeam.Enemy)
                    enemyAlive = true;
            }

            if (playerAlive && enemyAlive)
            {
                winner = HeadlessAutoChessTeam.None;
                return false;
            }

            winner = playerAlive == enemyAlive
                ? HeadlessAutoChessTeam.Draw
                : playerAlive
                    ? HeadlessAutoChessTeam.Player
                    : HeadlessAutoChessTeam.Enemy;
            return true;
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

        private static bool IsRevivePending(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && em.HasComponent<CHeadlessAutoChessPassiveState>(asc)
                   && em.GetComponentData<CHeadlessAutoChessPassiveState>(asc).RevivePending;
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
