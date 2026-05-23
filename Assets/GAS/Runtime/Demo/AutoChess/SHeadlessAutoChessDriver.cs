using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SAscCommandRequest))]
    [UpdateBefore(typeof(SAbilityCommandRequest))]
    public partial struct SHeadlessAutoChessDriver : ISystem
    {
        private const int ControlDecisionStride = 3;

        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, BAttribute, BGrantedAbility, CTagMask>()
                .Build();
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<CGameplayEventBus>();
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

            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            if (driver.LastDecisionFrame == frame)
                return;
            SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity);

            using var units = _unitQuery.ToEntityArray(Allocator.Temp);
            if (TryResolveWinner(em, units, out var winner))
            {
                driver.Completed = true;
                driver.Winner = winner;
                driver.LastDecisionFrame = frame;
                em.SetComponentData(driverEntity, driver);
                return;
            }

            if (!TryFindNextActor(
                    em,
                    units,
                    driver.NextTurnOrder,
                    out var actor,
                    out var unit,
                    out var nextTurnOrder,
                    out var startedNewRound))
            {
                driver.Completed = true;
                driver.Winner = HeadlessAutoChessTeam.Draw;
                driver.LastDecisionFrame = frame;
                em.SetComponentData(driverEntity, driver);
                return;
            }

            if (startedNewRound)
                driver.Round++;

            driver.NextTurnOrder = nextTurnOrder;
            driver.TurnCount++;
            driver.LastDecisionFrame = frame;

            if (IsCrowdControlled(em, actor, unit))
            {
                driver.CrowdControlTurnSkippedCount++;
                EmitControlTurnSkipped(em, eventBusEntity, actor, unit, driver);
                em.SetComponentData(driverEntity, driver);
                return;
            }

            if (TrySelectCommand(
                    em,
                    units,
                    actor,
                    unit,
                    driver.Round,
                    out var abilityCode,
                    out var target,
                    out var targetPolicy,
                    out var isManaAbility,
                    out var isControlAbility,
                    out var isSupportAbility,
                    out var isSummonAbility))
            {
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                var request = ecb.CreateEntity();
                ecb.SetName(request, $"AutoChessActivate_{abilityCode}");
                ecb.AddComponent(request, new CAbilityCommandRequest
                {
                    Owner = actor,
                    AbilityCode = abilityCode,
                    CommandType = EAbilityCommandType.Activate,
                    TargetAsc = target,
                });
                ecb.Playback(em);

                driver.IssuedCommandCount++;
                if (isSummonAbility)
                    driver.IssuedSummonAbilityCommandCount++;
                else if (isSupportAbility)
                    driver.IssuedSupportAbilityCommandCount++;
                else if (isControlAbility)
                    driver.IssuedControlAbilityCommandCount++;
                else if (isManaAbility)
                    driver.IssuedManaAbilityCommandCount++;
                else
                    driver.IssuedPrimaryCommandCount++;

                if (!isSummonAbility)
                {
                    if (targetPolicy == HeadlessAutoChessTargetPolicy.LowestHealth)
                        driver.LowestHealthTargetCount++;
                    else
                        driver.FrontlineTargetCount++;
                }

                if (isSupportAbility && abilityCode == HeadlessAutoChessScenario.AbilityPlayerCleanse)
                    EmitCleanseRequested(em, eventBusEntity, actor, target);
            }

            em.SetComponentData(driverEntity, driver);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool TryFindNextActor(
            EntityManager em,
            NativeArray<Entity> units,
            int cursor,
            out Entity actor,
            out CHeadlessAutoChessUnit actorUnit,
            out int nextTurnOrder,
            out bool startedNewRound)
        {
            actor = Entity.Null;
            actorUnit = default;
            nextTurnOrder = cursor;
            startedNewRound = false;

            if (TryFindActorAtOrAfter(em, units, cursor, out actor, out actorUnit))
            {
                nextTurnOrder = actorUnit.TurnOrder + 1;
                return true;
            }

            if (!TryFindActorAtOrAfter(em, units, int.MinValue, out actor, out actorUnit))
                return false;

            startedNewRound = true;
            nextTurnOrder = actorUnit.TurnOrder + 1;
            return true;
        }

        private static bool TryFindActorAtOrAfter(
            EntityManager em,
            NativeArray<Entity> units,
            int cursor,
            out Entity actor,
            out CHeadlessAutoChessUnit actorUnit)
        {
            actor = Entity.Null;
            actorUnit = default;
            var bestOrder = int.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (unit.TurnOrder < cursor || !IsAlive(em, candidate, unit))
                    continue;
                if (unit.TurnOrder > bestOrder)
                    continue;
                if (unit.TurnOrder == bestOrder && unit.Slot > bestSlot)
                    continue;
                if (unit.TurnOrder == bestOrder
                    && unit.Slot == bestSlot
                    && actor != Entity.Null
                    && candidate.Index >= actor.Index)
                {
                    continue;
                }

                actor = candidate;
                actorUnit = unit;
                bestOrder = unit.TurnOrder;
                bestSlot = unit.Slot;
            }

            return actor != Entity.Null;
        }

        private static bool TrySelectCommand(
            EntityManager em,
            NativeArray<Entity> units,
            Entity actor,
            in CHeadlessAutoChessUnit unit,
            int round,
            out int abilityCode,
            out Entity target,
            out HeadlessAutoChessTargetPolicy targetPolicy,
            out bool isManaAbility,
            out bool isControlAbility,
            out bool isSupportAbility,
            out bool isSummonAbility)
        {
            abilityCode = 0;
            target = Entity.Null;
            targetPolicy = HeadlessAutoChessTargetPolicy.Frontline;
            isManaAbility = false;
            isControlAbility = false;
            isSupportAbility = false;
            isSummonAbility = false;

            if (unit.SupportAbilityCode > 0
                && CanActivate(em, actor, unit, unit.SupportAbilityCode, unit.SupportCooldownTagIndex))
            {
                var supportTarget = FindSupportTarget(em, units, actor, unit, unit.SupportTargetPolicy);
                if (supportTarget != Entity.Null)
                {
                    abilityCode = unit.SupportAbilityCode;
                    target = supportTarget;
                    targetPolicy = unit.SupportTargetPolicy;
                    isSupportAbility = true;
                    return true;
                }
            }

            if (unit.SummonAbilityCode > 0
                && CanActivate(em, actor, unit, unit.SummonAbilityCode, unit.SummonCooldownTagIndex)
                && !HasReachedActiveSummonLimit(em, units, actor, unit.MaxActiveSummons))
            {
                abilityCode = unit.SummonAbilityCode;
                target = actor;
                targetPolicy = HeadlessAutoChessTargetPolicy.Frontline;
                isSummonAbility = true;
                return true;
            }

            if (unit.ManaAbilityCode > 0
                && GetAttribute(em, actor, unit.ManaAttrSetCode, unit.ManaAttrCode) >= unit.ManaAbilityThreshold
                && CanActivate(em, actor, unit, unit.ManaAbilityCode, unit.ManaCooldownTagIndex))
            {
                var manaTarget = FindAliveEnemy(em, units, unit, unit.ManaTargetPolicy);
                if (manaTarget != Entity.Null)
                {
                    abilityCode = unit.ManaAbilityCode;
                    target = manaTarget;
                    targetPolicy = unit.ManaTargetPolicy;
                    isManaAbility = true;
                    return true;
                }
            }

            if (unit.ControlAbilityCode > 0
                && ShouldConsiderControl(round)
                && CanActivate(em, actor, unit, unit.ControlAbilityCode, unit.ControlCooldownTagIndex))
            {
                var controlTarget = FindUncontrolledAliveEnemy(
                    em,
                    units,
                    unit,
                    unit.ControlTargetPolicy);
                if (controlTarget != Entity.Null)
                {
                    abilityCode = unit.ControlAbilityCode;
                    target = controlTarget;
                    targetPolicy = unit.ControlTargetPolicy;
                    isControlAbility = true;
                    return true;
                }
            }

            if (unit.PrimaryAbilityCode <= 0
                || !CanActivate(em, actor, unit, unit.PrimaryAbilityCode, unit.PrimaryCooldownTagIndex))
            {
                return false;
            }

            target = FindAliveEnemy(em, units, unit, unit.PrimaryTargetPolicy);
            if (target == Entity.Null)
                return false;

            abilityCode = unit.PrimaryAbilityCode;
            targetPolicy = unit.PrimaryTargetPolicy;
            return true;
        }

        private static bool ShouldConsiderControl(int round)
        {
            return ControlDecisionStride <= 1 || round % ControlDecisionStride == 0;
        }

        private static bool HasReachedActiveSummonLimit(
            EntityManager em,
            NativeArray<Entity> units,
            Entity owner,
            int maxActiveSummons)
        {
            if (maxActiveSummons <= 0)
                return false;

            var activeSummons = 0;
            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                if (!em.HasComponent<CHeadlessAutoChessSummonedUnit>(candidate))
                    continue;

                var summoned = em.GetComponentData<CHeadlessAutoChessSummonedUnit>(candidate);
                if (summoned.OwnerAsc != owner || summoned.DespawnRequested)
                    continue;

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (!IsAlive(em, candidate, unit))
                    continue;

                activeSummons++;
                if (activeSummons >= maxActiveSummons)
                    return true;
            }

            return false;
        }

        private static Entity FindSupportTarget(
            EntityManager em,
            NativeArray<Entity> units,
            Entity actor,
            in CHeadlessAutoChessUnit source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return source.SupportAbilityCode == HeadlessAutoChessScenario.AbilityPlayerCleanse
                ? FindStunnedAlly(em, units, actor, source, policy)
                : FindShieldSupportTarget(em, units, source, policy);
        }

        private static Entity FindUncontrolledAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            in CHeadlessAutoChessUnit source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthAliveEnemy(em, units, source, source.CrowdControlTagIndex)
                : FindFrontlineAliveEnemy(em, units, source, source.CrowdControlTagIndex);
        }

        private static Entity FindAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            in CHeadlessAutoChessUnit source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthAliveEnemy(em, units, source, -1)
                : FindFrontlineAliveEnemy(em, units, source, -1);
        }

        private static Entity FindShieldSupportTarget(
            EntityManager em,
            NativeArray<Entity> units,
            in CHeadlessAutoChessUnit source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthUnshieldedAlly(em, units, source)
                : FindFrontlineUnshieldedAlly(em, units, source);
        }

        private static Entity FindFrontlineAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            in CHeadlessAutoChessUnit source,
            int excludedTagIndex)
        {
            var target = Entity.Null;
            var bestBoardX = source.Team == HeadlessAutoChessTeam.Player ? int.MaxValue : int.MinValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (unit.Team == source.Team
                    || !IsAlive(em, candidate, unit)
                    || HasDenseTag(em, candidate, excludedTagIndex))
                {
                    continue;
                }

                var betterFile = source.Team == HeadlessAutoChessTeam.Player
                    ? unit.BoardX < bestBoardX
                    : unit.BoardX > bestBoardX;
                var sameFile = unit.BoardX == bestBoardX;
                if (!betterFile && (!sameFile || unit.Slot >= bestSlot))
                    continue;

                target = candidate;
                bestBoardX = unit.BoardX;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindFrontlineUnshieldedAlly(
            EntityManager em,
            NativeArray<Entity> units,
            in CHeadlessAutoChessUnit source)
        {
            var target = Entity.Null;
            var bestBoardX = source.Team == HeadlessAutoChessTeam.Player ? int.MinValue : int.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (unit.Team != source.Team || !IsAlive(em, candidate, unit) || HasShield(em, candidate, unit))
                    continue;

                var betterFile = source.Team == HeadlessAutoChessTeam.Player
                    ? unit.BoardX > bestBoardX
                    : unit.BoardX < bestBoardX;
                var sameFile = unit.BoardX == bestBoardX;
                if (!betterFile && (!sameFile || unit.Slot >= bestSlot))
                    continue;

                target = candidate;
                bestBoardX = unit.BoardX;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindLowestHealthUnshieldedAlly(
            EntityManager em,
            NativeArray<Entity> units,
            in CHeadlessAutoChessUnit source)
        {
            var target = Entity.Null;
            var bestHealth = float.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (unit.Team != source.Team || !IsAlive(em, candidate, unit) || HasShield(em, candidate, unit))
                    continue;

                var health = GetAttribute(em, candidate, unit.HealthAttrSetCode, unit.HealthAttrCode);
                if (health > bestHealth)
                    continue;
                if (health == bestHealth && unit.Slot >= bestSlot)
                    continue;

                target = candidate;
                bestHealth = health;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindStunnedAlly(
            EntityManager em,
            NativeArray<Entity> units,
            Entity actor,
            in CHeadlessAutoChessUnit source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthStunnedAlly(em, units, actor, source)
                : FindFrontlineStunnedAlly(em, units, actor, source);
        }

        private static Entity FindFrontlineStunnedAlly(
            EntityManager em,
            NativeArray<Entity> units,
            Entity actor,
            in CHeadlessAutoChessUnit source)
        {
            var target = Entity.Null;
            var bestBoardX = source.Team == HeadlessAutoChessTeam.Player ? int.MinValue : int.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (candidate == actor
                    || unit.Team != source.Team
                    || !IsAlive(em, candidate, unit)
                    || !HasDenseTag(em, candidate, HeadlessAutoChessScenario.TagAutoChessStunned))
                {
                    continue;
                }

                var betterFile = source.Team == HeadlessAutoChessTeam.Player
                    ? unit.BoardX > bestBoardX
                    : unit.BoardX < bestBoardX;
                var sameFile = unit.BoardX == bestBoardX;
                if (!betterFile && (!sameFile || unit.Slot >= bestSlot))
                    continue;

                target = candidate;
                bestBoardX = unit.BoardX;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindLowestHealthStunnedAlly(
            EntityManager em,
            NativeArray<Entity> units,
            Entity actor,
            in CHeadlessAutoChessUnit source)
        {
            var target = Entity.Null;
            var bestHealth = float.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (candidate == actor
                    || unit.Team != source.Team
                    || !IsAlive(em, candidate, unit)
                    || !HasDenseTag(em, candidate, HeadlessAutoChessScenario.TagAutoChessStunned))
                {
                    continue;
                }

                var health = GetAttribute(em, candidate, unit.HealthAttrSetCode, unit.HealthAttrCode);
                if (health > bestHealth)
                    continue;
                if (health == bestHealth && unit.Slot >= bestSlot)
                    continue;

                target = candidate;
                bestHealth = health;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindLowestHealthAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            in CHeadlessAutoChessUnit source,
            int excludedTagIndex)
        {
            var target = Entity.Null;
            var bestHealth = float.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (unit.Team == source.Team
                    || !IsAlive(em, candidate, unit)
                    || HasDenseTag(em, candidate, excludedTagIndex))
                {
                    continue;
                }

                var health = GetAttribute(em, candidate, unit.HealthAttrSetCode, unit.HealthAttrCode);
                if (health > bestHealth)
                    continue;
                if (health == bestHealth && unit.Slot >= bestSlot)
                    continue;

                target = candidate;
                bestHealth = health;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static bool CanActivate(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit,
            int abilityCode,
            int cooldownTagIndex)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && !em.HasComponent<CAscDestroying>(asc)
                   && !HasDenseTag(em, asc, cooldownTagIndex)
                   && IsAlive(em, asc, unit)
                   && !IsAbilityBusy(em, asc, abilityCode);
        }

        private static bool IsCrowdControlled(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit)
        {
            return HasDenseTag(em, asc, unit.CrowdControlTagIndex);
        }

        private static bool HasShield(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit)
        {
            return GetAttribute(em, asc, unit.ShieldAttrSetCode, unit.ShieldAttrCode) > 0f;
        }

        private static void EmitControlTurnSkipped(
            EntityManager em,
            Entity eventBusEntity,
            Entity actor,
            in CHeadlessAutoChessUnit unit,
            in CHeadlessAutoChessDriver driver)
        {
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessControlTurnSkipped,
                SourceAsc = actor,
                TargetAsc = actor,
                EventCode = unit.CrowdControlTagIndex,
                ReasonCode = driver.Round,
                Value = driver.TurnCount,
            });
        }

        private static void EmitCleanseRequested(
            EntityManager em,
            Entity eventBusEntity,
            Entity source,
            Entity target)
        {
            EventBusHelper.EnqueueGameplayEvent(em, eventBusEntity, new BGameplayEvent
            {
                Type = EGameplayEventType.AutoChessCleanseRequested,
                SourceAsc = source,
                TargetAsc = target,
                EventCode = HeadlessAutoChessScenario.GameplayEffectPlayerCleanse,
                ReasonCode = HeadlessAutoChessScenario.TagAutoChessStunned,
                Value = 1f,
            });
        }

        private static bool IsAbilityBusy(EntityManager em, Entity asc, int abilityCode)
        {
            if (!em.HasBuffer<BGrantedAbility>(asc))
                return true;

            var abilities = em.GetBuffer<BGrantedAbility>(asc);
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i].AbilityEntity;
                if (!IsAbilityWithCode(em, ability, abilityCode))
                    continue;

                if (em.HasComponent<CAbilityInTryActivate>(ability)
                    || em.HasComponent<CAbilityCommitRequest>(ability)
                    || em.HasComponent<CAbilityActive>(ability))
                {
                    return true;
                }

                if (!em.HasComponent<CAbilityRuntimeState>(ability))
                    return false;

                var runtime = em.GetComponentData<CAbilityRuntimeState>(ability);
                return runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
            }

            return true;
        }

        private static bool IsAbilityWithCode(EntityManager em, Entity ability, int abilityCode)
        {
            if (ability == Entity.Null || !em.Exists(ability))
                return false;
            if (em.HasComponent<CAbilityBaseInfo>(ability)
                && em.GetComponentData<CAbilityBaseInfo>(ability).Code == abilityCode)
            {
                return true;
            }

            if (!em.HasComponent<CAbilityConfig>(ability))
                return false;

            var config = em.GetComponentData<CAbilityConfig>(ability).Config;
            return config.IsCreated && config.Value.Code == abilityCode;
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
                if (!CanStillParticipateInResolution(em, units[i], unit))
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

        private static bool CanStillParticipateInResolution(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit)
        {
            return IsAlive(em, asc, unit)
                   || (asc != Entity.Null
                       && em.Exists(asc)
                       && em.HasComponent<CHeadlessAutoChessPassiveState>(asc)
                       && em.GetComponentData<CHeadlessAutoChessPassiveState>(asc).RevivePending);
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

        private static bool HasDenseTag(EntityManager em, Entity asc, int denseTagIndex)
        {
            return CTagMask.IsValidIndex(denseTagIndex)
                   && asc != Entity.Null
                   && em.Exists(asc)
                   && em.HasComponent<CTagMask>(asc)
                   && em.GetComponentData<CTagMask>(asc).HasTag(denseTagIndex);
        }
    }
}
