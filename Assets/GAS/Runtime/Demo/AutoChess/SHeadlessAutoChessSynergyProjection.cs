using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessPassiveReaction))]
    public partial struct SHeadlessAutoChessSynergyProjection : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessSynergyFacts, CHeadlessAutoChessSynergyState>()
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
            using var units = _unitQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0 || units.Length == 0)
                return;

            var driverEntity = drivers[0];
            var driver = em.GetComponentData<CHeadlessAutoChessDriver>(driverEntity);
            var facts = em.GetComponentData<CHeadlessAutoChessSynergyFacts>(driverEntity);
            var synergyState = em.GetComponentData<CHeadlessAutoChessSynergyState>(driverEntity);
            var frame = ResolveCurrentFrame(em);

            if (facts.LastProjectionFrame != frame)
            {
                facts.LastProjectionFrame = frame;
                facts.ProcessedGameplayEventCount = 0;
            }

            ProjectPeriodicTickFacts(em, eventBusEntity, ref facts);

            if (!driver.Completed && synergyState.LastEvaluationFrame != frame)
            {
                EvaluateSynergies(em, eventBusEntity, units, ref facts, ref synergyState, frame);
                synergyState.LastEvaluationFrame = frame;
            }

            em.SetComponentData(driverEntity, facts);
            em.SetComponentData(driverEntity, synergyState);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void EvaluateSynergies(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<Entity> units,
            ref CHeadlessAutoChessSynergyFacts facts,
            ref CHeadlessAutoChessSynergyState synergyState,
            int frame)
        {
            using var evaluations = new NativeList<SynergyEvaluation>(Allocator.Temp);
            BuildEvaluations(em, units, evaluations);

            var nextActiveMask = synergyState.ActiveSynergyMask;
            for (var i = 0; i < evaluations.Length; i++)
            {
                var evaluation = evaluations[i];
                if (!TryCreateSynergyMask(evaluation.SynergyCode, out var synergyMask))
                    continue;

                var wasActive = (synergyState.ActiveSynergyMask & synergyMask) != 0;
                var isActive = evaluation.AliveMemberCount >= evaluation.Threshold;
                if (isActive)
                {
                    if (!wasActive)
                    {
                        nextActiveMask |= synergyMask;
                        ActivateSynergy(em, eventBusEntity, units, evaluation, ref facts, frame);
                    }

                    continue;
                }

                if (!wasActive)
                    continue;

                nextActiveMask &= ~synergyMask;
                EmitSynergyExpired(em, eventBusEntity, evaluation, ref facts, frame);
            }

            synergyState.ActiveSynergyMask = nextActiveMask;
        }

        private static void BuildEvaluations(
            EntityManager em,
            NativeArray<Entity> units,
            NativeList<SynergyEvaluation> evaluations)
        {
            for (var i = 0; i < units.Length; i++)
            {
                var asc = units[i];
                if (!em.Exists(asc) || !em.HasComponent<CHeadlessAutoChessUnit>(asc))
                    continue;

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
                if (!IsAlive(em, asc, unit) || !em.HasBuffer<BHeadlessAutoChessSynergyMember>(asc))
                    continue;

                var memberships = em.GetBuffer<BHeadlessAutoChessSynergyMember>(asc);
                for (var j = 0; j < memberships.Length; j++)
                    AddOrIncrementEvaluation(evaluations, unit.Team, asc, memberships[j]);
            }
        }

        private static void AddOrIncrementEvaluation(
            NativeList<SynergyEvaluation> evaluations,
            HeadlessAutoChessTeam team,
            Entity sourceAsc,
            in BHeadlessAutoChessSynergyMember membership)
        {
            if (membership.SynergyCode <= 0 || membership.Threshold <= 0)
                return;

            var index = IndexOfEvaluation(evaluations, team, membership.SynergyCode);
            if (index < 0)
            {
                evaluations.Add(new SynergyEvaluation
                {
                    Team = team,
                    SynergyCode = membership.SynergyCode,
                    Threshold = membership.Threshold,
                    AllyBuffGameplayEffectCode = membership.AllyBuffGameplayEffectCode,
                    EnemyDebuffGameplayEffectCode = membership.EnemyDebuffGameplayEffectCode,
                    AliveMemberCount = 1,
                    SourceAsc = sourceAsc,
                });
                return;
            }

            var evaluation = evaluations[index];
            evaluation.AliveMemberCount++;
            if (evaluation.SourceAsc == Entity.Null)
                evaluation.SourceAsc = sourceAsc;
            evaluations[index] = evaluation;
        }

        private static int IndexOfEvaluation(
            NativeList<SynergyEvaluation> evaluations,
            HeadlessAutoChessTeam team,
            int synergyCode)
        {
            for (var i = 0; i < evaluations.Length; i++)
            {
                var evaluation = evaluations[i];
                if (evaluation.Team == team && evaluation.SynergyCode == synergyCode)
                    return i;
            }

            return -1;
        }

        private static void ActivateSynergy(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<Entity> units,
            in SynergyEvaluation evaluation,
            ref CHeadlessAutoChessSynergyFacts facts,
            int frame)
        {
            facts.SynergyActivatedFactCount++;
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessSynergyActivated,
                SourceAsc = evaluation.SourceAsc,
                EventCode = evaluation.SynergyCode,
                ReasonCode = evaluation.Threshold,
                Value = evaluation.AliveMemberCount,
            });

            RequestAllyBuffs(em, eventBusEntity, units, evaluation, ref facts);
            RequestEnemyDebuffs(em, eventBusEntity, units, evaluation, ref facts);
        }

        private static void RequestAllyBuffs(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<Entity> units,
            in SynergyEvaluation evaluation,
            ref CHeadlessAutoChessSynergyFacts facts)
        {
            if (evaluation.AllyBuffGameplayEffectCode <= 0)
                return;

            for (var i = 0; i < units.Length; i++)
            {
                var target = units[i];
                if (!TryGetAliveUnit(em, target, out var unit) || unit.Team != evaluation.Team)
                    continue;

                CreateApplyRequest(
                    em,
                    evaluation.SourceAsc,
                    target,
                    evaluation.AllyBuffGameplayEffectCode,
                    "AutoChessSynergyBuff");

                facts.AllyBuffRequestedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessSynergyAllyBuffRequested,
                    SourceAsc = evaluation.SourceAsc,
                    TargetAsc = target,
                    EventCode = evaluation.AllyBuffGameplayEffectCode,
                    ReasonCode = evaluation.SynergyCode,
                    Value = evaluation.AliveMemberCount,
                });
            }
        }

        private static void RequestEnemyDebuffs(
            EntityManager em,
            Entity eventBusEntity,
            NativeArray<Entity> units,
            in SynergyEvaluation evaluation,
            ref CHeadlessAutoChessSynergyFacts facts)
        {
            if (evaluation.EnemyDebuffGameplayEffectCode <= 0)
                return;

            for (var i = 0; i < units.Length; i++)
            {
                var target = units[i];
                if (!TryGetAliveUnit(em, target, out var unit) || unit.Team == evaluation.Team)
                    continue;

                CreateApplyRequest(
                    em,
                    evaluation.SourceAsc,
                    target,
                    evaluation.EnemyDebuffGameplayEffectCode,
                    "AutoChessSynergyDebuff");

                facts.EnemyDebuffRequestedFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessSynergyEnemyDebuffRequested,
                    SourceAsc = evaluation.SourceAsc,
                    TargetAsc = target,
                    EventCode = evaluation.EnemyDebuffGameplayEffectCode,
                    ReasonCode = evaluation.SynergyCode,
                    Value = evaluation.AliveMemberCount,
                });
            }
        }

        private static void ProjectPeriodicTickFacts(
            EntityManager em,
            Entity eventBusEntity,
            ref CHeadlessAutoChessSynergyFacts facts)
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
                if (evt.Type != EGameplayEventType.GameplayEffectInstanced
                    || evt.EventCode != HeadlessAutoChessScenario.GameplayEffectArcaneStormPeriodDamage
                    || !HasAppliedEvent(eventSnapshot, evt.GameplayEffect))
                {
                    continue;
                }

                facts.PeriodicTickFactCount++;
                EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
                {
                    Type = EGameplayEventType.AutoChessSynergyPeriodTicked,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.TargetAsc,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = evt.EventCode,
                    ReasonCode = HeadlessAutoChessScenario.SynergyArcane,
                    Value = HeadlessAutoChessScenario.ArcaneStormTickDamage,
                });
            }

            facts.ProcessedGameplayEventCount = em.Exists(eventBusEntity)
                                                && em.HasBuffer<BGameplayEvent>(eventBusEntity)
                ? em.GetBuffer<BGameplayEvent>(eventBusEntity).Length
                : eventCount;
        }

        private static bool HasAppliedEvent(
            NativeArray<BGameplayEvent> gameplayEvents,
            Entity gameplayEffect)
        {
            if (gameplayEffect == Entity.Null)
                return false;

            for (var i = 0; i < gameplayEvents.Length; i++)
            {
                var evt = gameplayEvents[i];
                if (evt.Type == EGameplayEventType.GameplayEffectApplied
                    && evt.GameplayEffect == gameplayEffect)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EmitSynergyExpired(
            EntityManager em,
            Entity eventBusEntity,
            in SynergyEvaluation evaluation,
            ref CHeadlessAutoChessSynergyFacts facts,
            int frame)
        {
            facts.SynergyExpiredFactCount++;
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessSynergyExpired,
                SourceAsc = evaluation.SourceAsc,
                EventCode = evaluation.SynergyCode,
                ReasonCode = frame,
                Value = evaluation.AliveMemberCount,
            });
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

        private static bool TryGetAliveUnit(
            EntityManager em,
            Entity asc,
            out CHeadlessAutoChessUnit unit)
        {
            unit = default;
            if (asc == Entity.Null
                || !em.Exists(asc)
                || !em.HasComponent<CHeadlessAutoChessUnit>(asc)
                || !em.HasBuffer<BAttribute>(asc))
            {
                return false;
            }

            unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
            return IsAlive(em, asc, unit);
        }

        private static bool IsAlive(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit)
        {
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

        private static bool TryCreateSynergyMask(int synergyCode, out int mask)
        {
            mask = 0;
            if (synergyCode <= 0 || synergyCode >= 31)
                return false;

            mask = 1 << synergyCode;
            return true;
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

        private struct SynergyEvaluation
        {
            public HeadlessAutoChessTeam Team;
            public int SynergyCode;
            public int Threshold;
            public int AllyBuffGameplayEffectCode;
            public int EnemyDebuffGameplayEffectCode;
            public int AliveMemberCount;
            public Entity SourceAsc;
        }
    }
}
