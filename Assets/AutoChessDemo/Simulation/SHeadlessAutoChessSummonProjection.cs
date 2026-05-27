using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SInstantEffectCueRequestProjection))]
    [UpdateBefore(typeof(SAscDestroyRequest))]
    public partial struct SHeadlessAutoChessSummonProjection : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessSummonFacts>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, BGameplayEffect, BAttribute>()
                .Build();
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_unitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var drivers = _driverQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0)
                return;

            var driverEntity = drivers[0];
            var driver = em.GetComponentData<CHeadlessAutoChessDriver>(driverEntity);
            if (!driver.Enabled || driver.Completed)
                return;

            var facts = em.GetComponentData<CHeadlessAutoChessSummonFacts>(driverEntity);
            var timer = SystemAPI.GetSingleton<GlobalTimer>();
            using var units = _unitQuery.ToEntityArray(Allocator.Temp);

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            try
            {
                ExpireSummons(
                    em,
                    units,
                    timer.Frame,
                    driver.TurnCount,
                    ref facts,
                    ref eventWriter);

                for (var i = 0; i < units.Length; i++)
                {
                    var ownerAsc = units[i];
                    var owner = em.GetComponentData<CHeadlessAutoChessUnit>(ownerAsc);
                    if (!IsAlive(em, ownerAsc, in owner))
                        continue;

                    var effects = em.GetBuffer<BGameplayEffect>(ownerAsc);
                    for (var j = 0; j < effects.Length; j++)
                    {
                        var effect = effects[j].GameplayEffect;
                        if (!CanProcessSummonEffect(em, effect))
                            continue;

                        var request = em.GetComponentData<CHeadlessAutoChessSummonRequest>(effect);
                        var context = em.HasComponent<CEffectContext>(effect)
                            ? em.GetComponentData<CEffectContext>(effect)
                            : default;
                        var spec = em.HasComponent<CEffectSpecData>(effect)
                            ? em.GetComponentData<CEffectSpecData>(effect)
                            : default;
                        var gameplayEffectCode = spec.GameplayEffectCode > 0
                            ? spec.GameplayEffectCode
                            : HeadlessAutoChessScenario.GameplayEffectPlayerSummonRequest;

                        if (CountActiveSummons(em, units, ownerAsc) >= owner.MaxActiveSummons)
                        {
                            MarkProcessed(em, effect, Entity.Null, timer.Frame, driver.TurnCount, 0);
                            continue;
                        }

                        facts.SummonRequestedFactCount++;
                        var summonSerial = ++facts.NextSummonSerial;
                        var summonedAsc = HeadlessAutoChessUnitFactory.CreateSummonedUnit(
                            em,
                            summonSerial,
                            ownerAsc,
                            context.SourceAbility,
                            effect,
                            gameplayEffectCode,
                            timer.Frame,
                            driver.TurnCount,
                            in owner,
                            in request);
                        MarkProcessed(em, effect, summonedAsc, timer.Frame, driver.TurnCount, summonSerial);

                        facts.SummonSpawnedFactCount++;
                        facts.ActiveSummonCount++;
                        driver.SpawnedUnitCount++;

                        EnqueueSummonEvents(
                            ref eventWriter,
                            ownerAsc,
                            summonedAsc,
                            context.SourceAbility,
                            effect,
                            gameplayEffectCode,
                            summonSerial);
                    }
                }
            }
            finally
            {
                eventWriter.Dispose();
            }

            facts.LastProjectionFrame = timer.Frame;
            em.SetComponentData(driverEntity, facts);
            em.SetComponentData(driverEntity, driver);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool CanProcessSummonEffect(EntityManager em, Entity effect)
        {
            return effect != Entity.Null
                   && em.Exists(effect)
                   && em.HasComponent<CHeadlessAutoChessSummonRequest>(effect)
                   && !em.HasComponent<CHeadlessAutoChessSummonProcessed>(effect);
        }

        private static void MarkProcessed(
            EntityManager em,
            Entity effect,
            Entity summonedAsc,
            int frame,
            int turn,
            int summonSerial)
        {
            var processed = new CHeadlessAutoChessSummonProcessed
            {
                SummonedAsc = summonedAsc,
                Frame = frame,
                Turn = turn,
                SummonSerial = summonSerial,
            };

            if (em.HasComponent<CHeadlessAutoChessSummonProcessed>(effect))
                em.SetComponentData(effect, processed);
            else
                em.AddComponentData(effect, processed);
        }

        private static void EnqueueSummonEvents(
            ref EventBusHelper.GameplayEventBusWriter writer,
            Entity ownerAsc,
            Entity summonedAsc,
            Entity sourceAbility,
            Entity effect,
            int gameplayEffectCode,
            int summonSerial)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessSummonRequested,
                SourceAsc = ownerAsc,
                TargetAsc = ownerAsc,
                SourceAbility = sourceAbility,
                GameplayEffect = effect,
                EventCode = gameplayEffectCode,
                Value = summonSerial,
            });
            writer.EnqueueGameplayEvent(new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessSummonSpawned,
                SourceAsc = ownerAsc,
                TargetAsc = summonedAsc,
                SourceAbility = sourceAbility,
                GameplayEffect = effect,
                EventCode = gameplayEffectCode,
                Value = summonSerial,
            });
        }

        private static void ExpireSummons(
            EntityManager em,
            NativeArray<Entity> units,
            int frame,
            int turn,
            ref CHeadlessAutoChessSummonFacts facts,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            for (var i = 0; i < units.Length; i++)
            {
                var asc = units[i];
                if (!em.HasComponent<CHeadlessAutoChessSummonedUnit>(asc))
                    continue;

                var summon = em.GetComponentData<CHeadlessAutoChessSummonedUnit>(asc);
                if (summon.DespawnRequested
                    || summon.ExpireTurn <= 0
                    || turn < summon.ExpireTurn)
                {
                    continue;
                }

                summon.DespawnRequested = true;
                em.SetComponentData(asc, summon);

                var request = em.CreateEntity();
                em.SetName(request, $"AutoChessSummonDestroy_{summon.SummonSerial}");
                em.AddComponentData(request, new CAscDestroyRequest { ASC = asc });

                facts.SummonExpiredFactCount++;
                facts.SummonDespawnedFactCount++;
                if (facts.ActiveSummonCount > 0)
                    facts.ActiveSummonCount--;

                EnqueueSummonExpiredEvents(
                    ref eventWriter,
                    summon.OwnerAsc,
                    asc,
                    summon.SourceAbility,
                    summon.SourceGameplayEffect,
                    summon.SummonGameplayEffectCode,
                    summon.SummonSerial,
                    frame);
            }
        }

        private static void EnqueueSummonExpiredEvents(
            ref EventBusHelper.GameplayEventBusWriter writer,
            Entity ownerAsc,
            Entity summonedAsc,
            Entity sourceAbility,
            Entity effect,
            int gameplayEffectCode,
            int summonSerial,
            int frame)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessSummonExpired,
                SourceAsc = ownerAsc,
                TargetAsc = summonedAsc,
                SourceAbility = sourceAbility,
                GameplayEffect = effect,
                EventCode = gameplayEffectCode,
                Value = summonSerial,
            });
            writer.EnqueueGameplayEvent(new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessSummonDespawned,
                SourceAsc = ownerAsc,
                TargetAsc = summonedAsc,
                SourceAbility = sourceAbility,
                GameplayEffect = effect,
                EventCode = gameplayEffectCode,
                Value = frame,
            });
        }

        private static int CountActiveSummons(
            EntityManager em,
            NativeArray<Entity> units,
            Entity ownerAsc)
        {
            var count = 0;
            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                if (!em.HasComponent<CHeadlessAutoChessSummonedUnit>(candidate))
                    continue;

                var summon = em.GetComponentData<CHeadlessAutoChessSummonedUnit>(candidate);
                if (summon.OwnerAsc != ownerAsc || summon.DespawnRequested)
                    continue;

                var candidateUnit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (IsAlive(em, candidate, in candidateUnit))
                    count++;
            }

            return count;
        }

        private static bool IsAlive(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && !em.HasComponent<CAscDestroying>(asc)
                   && GetAttribute(em, asc, unit.HealthAttrSetCode, unit.HealthAttrCode) > 0f;
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
    }
}
