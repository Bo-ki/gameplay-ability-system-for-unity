using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SAbilityStateCleanup))]
    [UpdateBefore(typeof(SHeadlessAutoChessPassiveReaction))]
    public partial struct SHeadlessAutoChessSummonLifecycle : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _summonQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver, CHeadlessAutoChessSummonFacts>()
                .Build();
            _summonQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessSummonedUnit>()
                .Build();
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate(_driverQuery);
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
            var driver = em.GetComponentData<CHeadlessAutoChessDriver>(driverEntity);
            var facts = em.GetComponentData<CHeadlessAutoChessSummonFacts>(driverEntity);
            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;

            if (facts.LastProjectionFrame != frame)
            {
                facts.LastProjectionFrame = frame;
                facts.ProcessedGameplayEventCount = 0;
            }

            ProcessGameplayEvents(em, eventBusEntity, driver, ref facts, frame);
            ProcessExpiredSummons(em, eventBusEntity, driver, ref facts, frame);
            facts.ActiveSummonCount = CountActiveSummons(em, _summonQuery);

            em.SetComponentData(driverEntity, facts);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ProcessGameplayEvents(
            EntityManager em,
            Entity eventBusEntity,
            in CHeadlessAutoChessDriver driver,
            ref CHeadlessAutoChessSummonFacts facts,
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
                if (evt.Type == EGameplayEventType.GameplayEffectApplied)
                {
                    TrySpawnSummon(em, eventBusEntity, evt, driver, ref facts, frame);
                    continue;
                }

                if (evt.Type == EGameplayEventType.AutoChessUnitDefeated)
                    TryDespawnSummon(em, eventBusEntity, evt.TargetAsc, EGameplayEventType.AutoChessSummonDespawned, 2, ref facts, frame);
            }

            facts.ProcessedGameplayEventCount = em.Exists(eventBusEntity)
                                                && em.HasBuffer<BGameplayEvent>(eventBusEntity)
                ? em.GetBuffer<BGameplayEvent>(eventBusEntity).Length
                : eventCount;
        }

        private static void TrySpawnSummon(
            EntityManager em,
            Entity eventBusEntity,
            in BGameplayEvent evt,
            in CHeadlessAutoChessDriver driver,
            ref CHeadlessAutoChessSummonFacts facts,
            int frame)
        {
            var ge = evt.GameplayEffect;
            if (ge == Entity.Null
                || !em.Exists(ge)
                || !em.HasComponent<CHeadlessAutoChessSummonRequest>(ge)
                || !em.HasComponent<CEffectContext>(ge)
                || !em.HasComponent<CEffectSpecData>(ge))
            {
                return;
            }

            var request = em.GetComponentData<CHeadlessAutoChessSummonRequest>(ge);
            var context = em.GetComponentData<CEffectContext>(ge);
            var spec = em.GetComponentData<CEffectSpecData>(ge);
            var owner = context.TargetAsc != Entity.Null ? context.TargetAsc : context.SourceAsc;
            if (!IsAliveAutoChessUnit(em, owner))
                return;

            var ownerUnit = em.GetComponentData<CHeadlessAutoChessUnit>(owner);
            if (ownerUnit.MaxActiveSummons > 0
                && CountActiveSummonsForOwner(em, owner) >= ownerUnit.MaxActiveSummons)
            {
                return;
            }

            var serial = facts.NextSummonSerial + 1;
            facts.NextSummonSerial = serial;
            facts.SummonRequestedFactCount++;
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessSummonRequested,
                SourceAsc = owner,
                TargetAsc = owner,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = spec.GameplayEffectCode,
                ReasonCode = request.SummonedUnitCode,
                Value = request.LifetimeTurns,
            });

            var summon = AbilitySystemEntityFactory.Create(em);
            em.SetName(summon, $"AutoChessSummon_{request.SummonedUnitCode}_{serial}");
            var summonUnit = CreateSummonedUnit(ownerUnit, request, serial, driver);
            em.AddComponentData(summon, summonUnit);
            em.AddComponentData(summon, new CHeadlessAutoChessSummonedUnit
            {
                OwnerAsc = owner,
                SourceAbility = context.SourceAbility,
                SourceGameplayEffect = ge,
                SummonedUnitCode = request.SummonedUnitCode,
                SummonGameplayEffectCode = spec.GameplayEffectCode,
                SummonSerial = serial,
                SpawnFrame = frame,
                SpawnTurn = driver.TurnCount,
                ExpireTurn = driver.TurnCount + request.LifetimeTurns,
            });
            CreateInitializeRequest(em, summon, request);

            facts.SummonSpawnedFactCount++;
            facts.ActiveSummonCount++;
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessSummonSpawned,
                SourceAsc = owner,
                TargetAsc = summon,
                SourceAbility = context.SourceAbility,
                GameplayEffect = ge,
                ContextId = context.ContextId,
                EventCode = request.SummonedUnitCode,
                ReasonCode = serial,
                Value = request.LifetimeTurns,
            });
        }

        private static CHeadlessAutoChessUnit CreateSummonedUnit(
            in CHeadlessAutoChessUnit ownerUnit,
            in CHeadlessAutoChessSummonRequest request,
            int serial,
            in CHeadlessAutoChessDriver driver)
        {
            var direction = ownerUnit.Team == HeadlessAutoChessTeam.Enemy ? -1 : 1;
            return new CHeadlessAutoChessUnit
            {
                Team = ownerUnit.Team,
                Slot = request.SlotOffset + serial,
                BoardX = Clamp(ownerUnit.BoardX + request.BoardXOffset * direction, 0, driver.BoardWidth - 1),
                BoardY = Clamp(ownerUnit.BoardY + request.BoardYOffset, 0, driver.BoardHeight - 1),
                TurnOrder = ownerUnit.TurnOrder + request.TurnOrderOffset + serial,
                PrimaryAbilityCode = request.PrimaryAbilityCode,
                ManaAbilityCode = 0,
                ControlAbilityCode = 0,
                SupportAbilityCode = 0,
                SummonAbilityCode = 0,
                HealthAttrSetCode = request.HealthAttrSetCode,
                HealthAttrCode = request.HealthAttrCode,
                ManaAttrSetCode = request.ManaAttrSetCode,
                ManaAttrCode = request.ManaAttrCode,
                ShieldAttrSetCode = request.ShieldAttrSetCode,
                ShieldAttrCode = request.ShieldAttrCode,
                ArcaneResistanceAttrSetCode = request.ArcaneResistanceAttrSetCode,
                ArcaneResistanceAttrCode = request.ArcaneResistanceAttrCode,
                PrimaryCooldownTagIndex = request.PrimaryCooldownTagIndex,
                ManaCooldownTagIndex = -1,
                ControlCooldownTagIndex = -1,
                SupportCooldownTagIndex = -1,
                SummonCooldownTagIndex = -1,
                CrowdControlTagIndex = HeadlessAutoChessScenario.TagAutoChessStunned,
                ManaAbilityThreshold = 0f,
                MaxActiveSummons = 0,
                PrimaryTargetPolicy = request.PrimaryTargetPolicy,
                ManaTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                ControlTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
                SupportTargetPolicy = HeadlessAutoChessTargetPolicy.Frontline,
            };
        }

        private static void CreateInitializeRequest(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessSummonRequest request)
        {
            var init = em.CreateEntity();
            em.SetName(init, $"AscInitializeRequest_Summon_{asc.Index}_{init.Index}");
            em.AddComponentData(init, new CAscInitializeRequest
            {
                ASC = asc,
                Level = 1,
            });

            if (request.FixedTagCode > 0)
            {
                var tags = em.AddBuffer<BAscInitFixedTag>(init);
                tags.Add(new BAscInitFixedTag
                {
                    TagCode = request.FixedTagCode,
                });
            }

            var attributes = em.AddBuffer<BAscInitAttribute>(init);
            attributes.Add(new BAscInitAttribute
            {
                AttrSetCode = request.HealthAttrSetCode,
                AttributeCode = request.HealthAttrCode,
                BaseValue = request.Health,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = request.MaxHealth,
            });
            attributes.Add(new BAscInitAttribute
            {
                AttrSetCode = request.ManaAttrSetCode,
                AttributeCode = request.ManaAttrCode,
                BaseValue = request.Mana,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = request.MaxMana,
            });
            attributes.Add(new BAscInitAttribute
            {
                AttrSetCode = request.ShieldAttrSetCode,
                AttributeCode = request.ShieldAttrCode,
                BaseValue = request.Shield,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = request.MaxShield,
            });
            attributes.Add(new BAscInitAttribute
            {
                AttrSetCode = request.ArcaneResistanceAttrSetCode,
                AttributeCode = request.ArcaneResistanceAttrCode,
                BaseValue = request.ArcaneResistance,
                IsClampMin = true,
                IsClampMax = true,
                MinValue = 0f,
                MaxValue = request.MaxArcaneResistance,
            });

            if (request.PrimaryAbilityCode > 0)
            {
                var abilities = em.AddBuffer<BAscInitAbility>(init);
                abilities.Add(new BAscInitAbility
                {
                    AbilityCode = request.PrimaryAbilityCode,
                });
            }
        }

        private static void ProcessExpiredSummons(
            EntityManager em,
            Entity eventBusEntity,
            in CHeadlessAutoChessDriver driver,
            ref CHeadlessAutoChessSummonFacts facts,
            int frame)
        {
            using var summons = em.CreateEntityQuery(
                    ComponentType.ReadOnly<CHeadlessAutoChessUnit>(),
                    ComponentType.ReadWrite<CHeadlessAutoChessSummonedUnit>())
                .ToEntityArray(Allocator.Temp);

            for (var i = 0; i < summons.Length; i++)
            {
                var summon = summons[i];
                if (!em.Exists(summon) || em.HasComponent<CAscDestroying>(summon))
                    continue;

                var summoned = em.GetComponentData<CHeadlessAutoChessSummonedUnit>(summon);
                if (summoned.DespawnRequested || driver.TurnCount < summoned.ExpireTurn)
                    continue;

                TryDespawnSummon(
                    em,
                    eventBusEntity,
                    summon,
                    EGameplayEventType.AutoChessSummonExpired,
                    1,
                    ref facts,
                    frame);
            }
        }

        private static bool TryDespawnSummon(
            EntityManager em,
            Entity eventBusEntity,
            Entity summon,
            EGameplayEventType firstEventType,
            int reasonCode,
            ref CHeadlessAutoChessSummonFacts facts,
            int frame)
        {
            if (summon == Entity.Null
                || !em.Exists(summon)
                || !em.HasComponent<CHeadlessAutoChessSummonedUnit>(summon))
            {
                return false;
            }

            var summoned = em.GetComponentData<CHeadlessAutoChessSummonedUnit>(summon);
            if (summoned.DespawnRequested)
                return false;

            summoned.DespawnRequested = true;
            em.SetComponentData(summon, summoned);

            if (firstEventType == EGameplayEventType.AutoChessSummonExpired)
            {
                facts.SummonExpiredFactCount++;
                EmitSummonLifecycleEvent(em, eventBusEntity, summon, summoned, firstEventType, reasonCode, frame);
            }

            facts.SummonDespawnedFactCount++;
            EmitSummonLifecycleEvent(
                em,
                eventBusEntity,
                summon,
                summoned,
                EGameplayEventType.AutoChessSummonDespawned,
                reasonCode,
                frame);

            CreateDestroyRequest(em, summon);
            return true;
        }

        private static void EmitSummonLifecycleEvent(
            EntityManager em,
            Entity eventBusEntity,
            Entity summon,
            in CHeadlessAutoChessSummonedUnit summoned,
            EGameplayEventType type,
            int reasonCode,
            int frame)
        {
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = type,
                SourceAsc = summoned.OwnerAsc,
                TargetAsc = summon,
                SourceAbility = summoned.SourceAbility,
                GameplayEffect = summoned.SourceGameplayEffect,
                EventCode = summoned.SummonedUnitCode,
                ReasonCode = reasonCode,
                Value = frame - summoned.SpawnFrame,
            });
        }

        private static void CreateDestroyRequest(EntityManager em, Entity asc)
        {
            if (asc == Entity.Null
                || !em.Exists(asc)
                || em.HasComponent<CAscDestroying>(asc))
            {
                return;
            }

            var request = em.CreateEntity();
            em.SetName(request, $"AscDestroyRequest_Summon_{asc.Index}_{request.Index}");
            em.AddComponentData(request, new CAscDestroyRequest
            {
                ASC = asc,
            });
        }

        private static int CountActiveSummons(EntityManager em, EntityQuery summonQuery)
        {
            using var summons = summonQuery.ToEntityArray(Allocator.Temp);
            var count = 0;
            for (var i = 0; i < summons.Length; i++)
            {
                var summon = summons[i];
                if (IsActiveSummon(em, summon))
                    count++;
            }

            return count;
        }

        private static int CountActiveSummonsForOwner(EntityManager em, Entity owner)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CHeadlessAutoChessUnit>(),
                ComponentType.ReadOnly<CHeadlessAutoChessSummonedUnit>());
            using var summons = query.ToEntityArray(Allocator.Temp);
            var count = 0;
            for (var i = 0; i < summons.Length; i++)
            {
                var summon = summons[i];
                if (!IsActiveSummon(em, summon))
                    continue;

                var summoned = em.GetComponentData<CHeadlessAutoChessSummonedUnit>(summon);
                if (summoned.OwnerAsc == owner)
                    count++;
            }

            return count;
        }

        private static bool IsActiveSummon(EntityManager em, Entity summon)
        {
            return summon != Entity.Null
                   && em.Exists(summon)
                   && !em.HasComponent<CAscDestroying>(summon)
                   && em.HasComponent<CHeadlessAutoChessSummonedUnit>(summon)
                   && !em.GetComponentData<CHeadlessAutoChessSummonedUnit>(summon).DespawnRequested;
        }

        private static bool IsAliveAutoChessUnit(EntityManager em, Entity asc)
        {
            if (asc == Entity.Null
                || !em.Exists(asc)
                || em.HasComponent<CAscDestroying>(asc)
                || !em.HasComponent<CHeadlessAutoChessUnit>(asc)
                || !em.HasBuffer<BAttribute>(asc))
            {
                return false;
            }

            var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
            var attributes = em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == unit.HealthAttrSetCode && attribute.Code == unit.HealthAttrCode)
                    return attribute.CurrentValue > 0f;
            }

            return false;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }
    }
}
