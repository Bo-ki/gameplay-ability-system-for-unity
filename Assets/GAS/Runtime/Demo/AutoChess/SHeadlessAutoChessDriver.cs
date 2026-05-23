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
            if (_driverQuery.IsEmptyIgnoreFilter)
                return;

            var driverEntity = _driverQuery.GetSingletonEntity();
            var driver = em.GetComponentData<CHeadlessAutoChessDriver>(driverEntity);
            if (!driver.Enabled || driver.Completed)
                return;

            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            if (driver.LastDecisionFrame == frame)
                return;
            SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);

            using var units = _unitQuery.ToEntityArray(Allocator.Temp);
            using var unitSnapshots = BuildUnitSnapshots(em, units, Allocator.Temp);
            if (TryResolveWinner(unitSnapshots, out var winner))
            {
                driver.Completed = true;
                driver.Winner = winner;
                driver.LastDecisionFrame = frame;
                em.SetComponentData(driverEntity, driver);
                return;
            }

            if (!TryFindNextActor(
                    unitSnapshots,
                    driver.NextTurnOrder,
                    out var actorSnapshot,
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
            var actor = actorSnapshot.Entity;
            var unit = actorSnapshot.Unit;

            if (IsCrowdControlled(actorSnapshot))
            {
                driver.CrowdControlTurnSkippedCount++;
                EmitControlTurnSkipped(em, eventBusEntity, actor, unit, driver);
                em.SetComponentData(driverEntity, driver);
                return;
            }

            if (TrySelectCommand(
                    unitSnapshots,
                    actorSnapshot,
                    driver.Round,
                    out var abilityEntity,
                    out var abilityCode,
                    out var target,
                    out var targetPolicy,
                    out var isManaAbility,
                    out var isControlAbility,
                    out var isSupportAbility,
                    out var isSummonAbility))
            {
                RequestAbilityActivation(em, abilityEntity, target);

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
            NativeArray<UnitSnapshot> units,
            int cursor,
            out UnitSnapshot actorSnapshot,
            out int nextTurnOrder,
            out bool startedNewRound)
        {
            actorSnapshot = default;
            nextTurnOrder = cursor;
            startedNewRound = false;

            if (TryFindActorAtOrAfter(units, cursor, out actorSnapshot))
            {
                nextTurnOrder = actorSnapshot.Unit.TurnOrder + 1;
                return true;
            }

            if (!TryFindActorAtOrAfter(units, int.MinValue, out actorSnapshot))
                return false;

            startedNewRound = true;
            nextTurnOrder = actorSnapshot.Unit.TurnOrder + 1;
            return true;
        }

        private static bool TryFindActorAtOrAfter(
            NativeArray<UnitSnapshot> units,
            int cursor,
            out UnitSnapshot actorSnapshot)
        {
            actorSnapshot = default;
            var bestOrder = int.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = candidate.Unit;
                if (unit.TurnOrder < cursor || !candidate.Alive)
                    continue;
                if (unit.TurnOrder > bestOrder)
                    continue;
                if (unit.TurnOrder == bestOrder && unit.Slot > bestSlot)
                    continue;
                if (unit.TurnOrder == bestOrder
                    && unit.Slot == bestSlot
                    && actorSnapshot.Entity != Entity.Null
                    && candidate.Entity.Index >= actorSnapshot.Entity.Index)
                {
                    continue;
                }

                actorSnapshot = candidate;
                bestOrder = unit.TurnOrder;
                bestSlot = unit.Slot;
            }

            return actorSnapshot.Entity != Entity.Null;
        }

        private static bool TrySelectCommand(
            NativeArray<UnitSnapshot> units,
            in UnitSnapshot actorSnapshot,
            int round,
            out Entity abilityEntity,
            out int abilityCode,
            out Entity target,
            out HeadlessAutoChessTargetPolicy targetPolicy,
            out bool isManaAbility,
            out bool isControlAbility,
            out bool isSupportAbility,
            out bool isSummonAbility)
        {
            abilityEntity = Entity.Null;
            abilityCode = 0;
            target = Entity.Null;
            targetPolicy = HeadlessAutoChessTargetPolicy.Frontline;
            isManaAbility = false;
            isControlAbility = false;
            isSupportAbility = false;
            isSummonAbility = false;
            var actor = actorSnapshot.Entity;
            var unit = actorSnapshot.Unit;

            if (unit.SupportAbilityCode > 0
                && CanActivate(
                    actorSnapshot,
                    unit.SupportAbilityCode,
                    unit.SupportCooldownTagIndex,
                    actorSnapshot.SupportAbility,
                    actorSnapshot.SupportAbilityBusy))
            {
                var supportTarget = FindSupportTarget(units, actor, actorSnapshot, unit.SupportTargetPolicy);
                if (supportTarget != Entity.Null)
                {
                    abilityEntity = actorSnapshot.SupportAbility;
                    abilityCode = unit.SupportAbilityCode;
                    target = supportTarget;
                    targetPolicy = unit.SupportTargetPolicy;
                    isSupportAbility = true;
                    return true;
                }
            }

            if (unit.SummonAbilityCode > 0
                && CanActivate(
                    actorSnapshot,
                    unit.SummonAbilityCode,
                    unit.SummonCooldownTagIndex,
                    actorSnapshot.SummonAbility,
                    actorSnapshot.SummonAbilityBusy)
                && !HasReachedActiveSummonLimit(units, actor, unit.MaxActiveSummons))
            {
                abilityEntity = actorSnapshot.SummonAbility;
                abilityCode = unit.SummonAbilityCode;
                target = actor;
                targetPolicy = HeadlessAutoChessTargetPolicy.Frontline;
                isSummonAbility = true;
                return true;
            }

            if (unit.ManaAbilityCode > 0
                && actorSnapshot.Mana >= unit.ManaAbilityThreshold
                && CanActivate(
                    actorSnapshot,
                    unit.ManaAbilityCode,
                    unit.ManaCooldownTagIndex,
                    actorSnapshot.ManaAbility,
                    actorSnapshot.ManaAbilityBusy))
            {
                var manaTarget = FindAliveEnemy(units, actorSnapshot, unit.ManaTargetPolicy);
                if (manaTarget != Entity.Null)
                {
                    abilityEntity = actorSnapshot.ManaAbility;
                    abilityCode = unit.ManaAbilityCode;
                    target = manaTarget;
                    targetPolicy = unit.ManaTargetPolicy;
                    isManaAbility = true;
                    return true;
                }
            }

            if (unit.ControlAbilityCode > 0
                && ShouldConsiderControl(round)
                && CanActivate(
                    actorSnapshot,
                    unit.ControlAbilityCode,
                    unit.ControlCooldownTagIndex,
                    actorSnapshot.ControlAbility,
                    actorSnapshot.ControlAbilityBusy))
            {
                var controlTarget = FindUncontrolledAliveEnemy(
                    units,
                    actorSnapshot,
                    unit.ControlTargetPolicy);
                if (controlTarget != Entity.Null)
                {
                    abilityEntity = actorSnapshot.ControlAbility;
                    abilityCode = unit.ControlAbilityCode;
                    target = controlTarget;
                    targetPolicy = unit.ControlTargetPolicy;
                    isControlAbility = true;
                    return true;
                }
            }

            if (unit.PrimaryAbilityCode <= 0
                || !CanActivate(
                    actorSnapshot,
                    unit.PrimaryAbilityCode,
                    unit.PrimaryCooldownTagIndex,
                    actorSnapshot.PrimaryAbility,
                    actorSnapshot.PrimaryAbilityBusy))
            {
                return false;
            }

            target = FindAliveEnemy(units, actorSnapshot, unit.PrimaryTargetPolicy);
            if (target == Entity.Null)
                return false;

            abilityEntity = actorSnapshot.PrimaryAbility;
            abilityCode = unit.PrimaryAbilityCode;
            targetPolicy = unit.PrimaryTargetPolicy;
            return true;
        }

        private static bool ShouldConsiderControl(int round)
        {
            return ControlDecisionStride <= 1 || round % ControlDecisionStride == 0;
        }

        private static bool HasReachedActiveSummonLimit(
            NativeArray<UnitSnapshot> units,
            Entity owner,
            int maxActiveSummons)
        {
            if (maxActiveSummons <= 0)
                return false;

            var activeSummons = 0;
            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                if (!candidate.IsSummoned)
                    continue;

                if (candidate.Summoned.OwnerAsc != owner || candidate.Summoned.DespawnRequested)
                    continue;

                if (!candidate.Alive)
                    continue;

                activeSummons++;
                if (activeSummons >= maxActiveSummons)
                    return true;
            }

            return false;
        }

        private static Entity FindSupportTarget(
            NativeArray<UnitSnapshot> units,
            Entity actor,
            in UnitSnapshot source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return source.Unit.SupportAbilityCode == HeadlessAutoChessScenario.AbilityPlayerCleanse
                ? FindStunnedAlly(units, actor, source, policy)
                : FindShieldSupportTarget(units, source, policy);
        }

        private static Entity FindUncontrolledAliveEnemy(
            NativeArray<UnitSnapshot> units,
            in UnitSnapshot source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthAliveEnemy(units, source, source.Unit.CrowdControlTagIndex)
                : FindFrontlineAliveEnemy(units, source, source.Unit.CrowdControlTagIndex);
        }

        private static Entity FindAliveEnemy(
            NativeArray<UnitSnapshot> units,
            in UnitSnapshot source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthAliveEnemy(units, source, -1)
                : FindFrontlineAliveEnemy(units, source, -1);
        }

        private static Entity FindShieldSupportTarget(
            NativeArray<UnitSnapshot> units,
            in UnitSnapshot source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthUnshieldedAlly(units, source)
                : FindFrontlineUnshieldedAlly(units, source);
        }

        private static Entity FindFrontlineAliveEnemy(
            NativeArray<UnitSnapshot> units,
            in UnitSnapshot source,
            int excludedTagIndex)
        {
            var target = Entity.Null;
            var sourceUnit = source.Unit;
            var bestBoardX = sourceUnit.Team == HeadlessAutoChessTeam.Player ? int.MaxValue : int.MinValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = candidate.Unit;
                if (unit.Team == sourceUnit.Team
                    || !candidate.Alive
                    || candidate.HasTag(excludedTagIndex))
                {
                    continue;
                }

                var betterFile = sourceUnit.Team == HeadlessAutoChessTeam.Player
                    ? unit.BoardX < bestBoardX
                    : unit.BoardX > bestBoardX;
                var sameFile = unit.BoardX == bestBoardX;
                if (!betterFile && (!sameFile || unit.Slot >= bestSlot))
                    continue;

                target = candidate.Entity;
                bestBoardX = unit.BoardX;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindFrontlineUnshieldedAlly(
            NativeArray<UnitSnapshot> units,
            in UnitSnapshot source)
        {
            var target = Entity.Null;
            var sourceUnit = source.Unit;
            var bestBoardX = sourceUnit.Team == HeadlessAutoChessTeam.Player ? int.MinValue : int.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = candidate.Unit;
                if (unit.Team != sourceUnit.Team || !candidate.Alive || candidate.HasShield)
                    continue;

                var betterFile = sourceUnit.Team == HeadlessAutoChessTeam.Player
                    ? unit.BoardX > bestBoardX
                    : unit.BoardX < bestBoardX;
                var sameFile = unit.BoardX == bestBoardX;
                if (!betterFile && (!sameFile || unit.Slot >= bestSlot))
                    continue;

                target = candidate.Entity;
                bestBoardX = unit.BoardX;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindLowestHealthUnshieldedAlly(
            NativeArray<UnitSnapshot> units,
            in UnitSnapshot source)
        {
            var target = Entity.Null;
            var bestHealth = float.MaxValue;
            var bestSlot = int.MaxValue;
            var sourceUnit = source.Unit;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = candidate.Unit;
                if (unit.Team != sourceUnit.Team || !candidate.Alive || candidate.HasShield)
                    continue;

                var health = candidate.Health;
                if (health > bestHealth)
                    continue;
                if (health == bestHealth && unit.Slot >= bestSlot)
                    continue;

                target = candidate.Entity;
                bestHealth = health;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindStunnedAlly(
            NativeArray<UnitSnapshot> units,
            Entity actor,
            in UnitSnapshot source,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthStunnedAlly(units, actor, source)
                : FindFrontlineStunnedAlly(units, actor, source);
        }

        private static Entity FindFrontlineStunnedAlly(
            NativeArray<UnitSnapshot> units,
            Entity actor,
            in UnitSnapshot source)
        {
            var target = Entity.Null;
            var sourceUnit = source.Unit;
            var bestBoardX = sourceUnit.Team == HeadlessAutoChessTeam.Player ? int.MinValue : int.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = candidate.Unit;
                if (candidate.Entity == actor
                    || unit.Team != sourceUnit.Team
                    || !candidate.Alive
                    || !candidate.HasTag(HeadlessAutoChessScenario.TagAutoChessStunned))
                {
                    continue;
                }

                var betterFile = sourceUnit.Team == HeadlessAutoChessTeam.Player
                    ? unit.BoardX > bestBoardX
                    : unit.BoardX < bestBoardX;
                var sameFile = unit.BoardX == bestBoardX;
                if (!betterFile && (!sameFile || unit.Slot >= bestSlot))
                    continue;

                target = candidate.Entity;
                bestBoardX = unit.BoardX;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindLowestHealthStunnedAlly(
            NativeArray<UnitSnapshot> units,
            Entity actor,
            in UnitSnapshot source)
        {
            var target = Entity.Null;
            var bestHealth = float.MaxValue;
            var bestSlot = int.MaxValue;
            var sourceUnit = source.Unit;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = candidate.Unit;
                if (candidate.Entity == actor
                    || unit.Team != sourceUnit.Team
                    || !candidate.Alive
                    || !candidate.HasTag(HeadlessAutoChessScenario.TagAutoChessStunned))
                {
                    continue;
                }

                var health = candidate.Health;
                if (health > bestHealth)
                    continue;
                if (health == bestHealth && unit.Slot >= bestSlot)
                    continue;

                target = candidate.Entity;
                bestHealth = health;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static Entity FindLowestHealthAliveEnemy(
            NativeArray<UnitSnapshot> units,
            in UnitSnapshot source,
            int excludedTagIndex)
        {
            var target = Entity.Null;
            var bestHealth = float.MaxValue;
            var bestSlot = int.MaxValue;
            var sourceUnit = source.Unit;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var unit = candidate.Unit;
                if (unit.Team == sourceUnit.Team
                    || !candidate.Alive
                    || candidate.HasTag(excludedTagIndex))
                {
                    continue;
                }

                var health = candidate.Health;
                if (health > bestHealth)
                    continue;
                if (health == bestHealth && unit.Slot >= bestSlot)
                    continue;

                target = candidate.Entity;
                bestHealth = health;
                bestSlot = unit.Slot;
            }

            return target;
        }

        private static bool CanActivate(
            in UnitSnapshot unit,
            int abilityCode,
            int cooldownTagIndex,
            Entity abilityEntity,
            bool abilityBusy)
        {
            return unit.Entity != Entity.Null
                   && abilityCode > 0
                   && abilityEntity != Entity.Null
                   && !unit.AscDestroying
                   && !unit.HasTag(cooldownTagIndex)
                   && unit.Alive
                   && !abilityBusy;
        }

        private static bool IsCrowdControlled(in UnitSnapshot unit)
        {
            return unit.HasTag(unit.Unit.CrowdControlTagIndex);
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

        private static bool TryResolveWinner(
            NativeArray<UnitSnapshot> units,
            out HeadlessAutoChessTeam winner)
        {
            var playerAlive = false;
            var enemyAlive = false;

            for (var i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                if (!CanStillParticipateInResolution(unit))
                    continue;

                if (unit.Unit.Team == HeadlessAutoChessTeam.Player)
                    playerAlive = true;
                else if (unit.Unit.Team == HeadlessAutoChessTeam.Enemy)
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

        private static bool CanStillParticipateInResolution(in UnitSnapshot unit)
        {
            return unit.Alive || unit.RevivePending || unit.CanRequestRevive;
        }

        private static NativeArray<UnitSnapshot> BuildUnitSnapshots(
            EntityManager em,
            NativeArray<Entity> units,
            Allocator allocator)
        {
            var snapshots = new NativeArray<UnitSnapshot>(units.Length, allocator);
            for (var i = 0; i < units.Length; i++)
            {
                var entity = units[i];
                if (entity == Entity.Null || !em.Exists(entity))
                    continue;

                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(entity);
                var ascDestroying = em.HasComponent<CAscDestroying>(entity);
                ReadCoreAttributes(em, entity, unit, out var health, out var shield, out var mana);
                var isSummoned = em.HasComponent<CHeadlessAutoChessSummonedUnit>(entity);
                var passiveState = em.HasComponent<CHeadlessAutoChessPassiveState>(entity)
                    ? em.GetComponentData<CHeadlessAutoChessPassiveState>(entity)
                    : default;
                var passiveRules = em.HasComponent<CHeadlessAutoChessPassiveRules>(entity)
                    ? em.GetComponentData<CHeadlessAutoChessPassiveRules>(entity)
                    : default;
                var snapshot = new UnitSnapshot
                {
                    Entity = entity,
                    Unit = unit,
                    Tags = em.HasComponent<CTagMask>(entity)
                        ? em.GetComponentData<CTagMask>(entity)
                        : default,
                    Summoned = isSummoned
                        ? em.GetComponentData<CHeadlessAutoChessSummonedUnit>(entity)
                        : default,
                    Health = health,
                    Shield = shield,
                    Mana = mana,
                    Alive = !ascDestroying && health > 0f,
                    AscDestroying = ascDestroying,
                    RevivePending = passiveState.RevivePending,
                    CanRequestRevive = passiveRules.ReviveGameplayEffectCode > 0
                                       && passiveRules.MaxReviveCount > 0
                                       && !passiveState.RevivePending
                                       && passiveState.ReviveCount < passiveRules.MaxReviveCount,
                    IsSummoned = isSummoned,
                };
                ResolveAbilitySlots(em, entity, ref snapshot);
                snapshots[i] = snapshot;
            }

            return snapshots;
        }

        private static void ResolveAbilitySlots(
            EntityManager em,
            Entity asc,
            ref UnitSnapshot snapshot)
        {
            if (asc == Entity.Null || !em.Exists(asc))
                return;

            if (!em.HasComponent<CHeadlessAutoChessAbilitySlots>(asc))
                TryCacheAbilitySlots(em, asc, snapshot.Unit);

            if (!em.HasComponent<CHeadlessAutoChessAbilitySlots>(asc))
                return;

            var slots = em.GetComponentData<CHeadlessAutoChessAbilitySlots>(asc);
            snapshot.PrimaryAbility = slots.PrimaryAbility;
            snapshot.ManaAbility = slots.ManaAbility;
            snapshot.ControlAbility = slots.ControlAbility;
            snapshot.SupportAbility = slots.SupportAbility;
            snapshot.SummonAbility = slots.SummonAbility;
            snapshot.PrimaryAbilityBusy = IsAbilityBusy(em, slots.PrimaryAbility);
            snapshot.ManaAbilityBusy = IsAbilityBusy(em, slots.ManaAbility);
            snapshot.ControlAbilityBusy = IsAbilityBusy(em, slots.ControlAbility);
            snapshot.SupportAbilityBusy = IsAbilityBusy(em, slots.SupportAbility);
            snapshot.SummonAbilityBusy = IsAbilityBusy(em, slots.SummonAbility);
        }

        private static void TryCacheAbilitySlots(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit)
        {
            if (!em.HasBuffer<BGrantedAbility>(asc))
                return;

            var slots = new CHeadlessAutoChessAbilitySlots();
            var abilities = em.GetBuffer<BGrantedAbility>(asc);
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i].AbilityEntity;
                if (!TryReadAbilityCode(em, ability, out var abilityCode))
                    continue;

                if (abilityCode == unit.PrimaryAbilityCode)
                {
                    slots.PrimaryAbility = ability;
                    continue;
                }

                if (abilityCode == unit.ManaAbilityCode)
                {
                    slots.ManaAbility = ability;
                    continue;
                }

                if (abilityCode == unit.ControlAbilityCode)
                {
                    slots.ControlAbility = ability;
                    continue;
                }

                if (abilityCode == unit.SupportAbilityCode)
                {
                    slots.SupportAbility = ability;
                    continue;
                }

                if (abilityCode == unit.SummonAbilityCode)
                {
                    slots.SummonAbility = ability;
                }
            }

            if (slots.PrimaryAbility != Entity.Null
                || slots.ManaAbility != Entity.Null
                || slots.ControlAbility != Entity.Null
                || slots.SupportAbility != Entity.Null
                || slots.SummonAbility != Entity.Null)
            {
                em.AddComponentData(asc, slots);
            }
        }

        private static void RequestAbilityActivation(
            EntityManager em,
            Entity ability,
            Entity targetAsc)
        {
            if (ability == Entity.Null || !em.Exists(ability))
                return;

            SetMainTarget(em, ability, targetAsc);
            if (!em.HasComponent<CAbilityInTryActivate>(ability))
                em.AddComponent<CAbilityInTryActivate>(ability);
        }

        private static void SetMainTarget(
            EntityManager em,
            Entity ability,
            Entity targetAsc)
        {
            if (targetAsc == Entity.Null
                || !em.Exists(targetAsc)
                || em.HasComponent<CAscDestroying>(targetAsc))
            {
                if (em.HasComponent<CAbilityMainTarget>(ability))
                    em.RemoveComponent<CAbilityMainTarget>(ability);
                return;
            }

            var target = new CAbilityMainTarget
            {
                TargetAsc = targetAsc,
            };

            if (em.HasComponent<CAbilityMainTarget>(ability))
                em.SetComponentData(ability, target);
            else
                em.AddComponentData(ability, target);
        }

        private struct UnitSnapshot
        {
            public Entity Entity;
            public CHeadlessAutoChessUnit Unit;
            public CTagMask Tags;
            public CHeadlessAutoChessSummonedUnit Summoned;
            public float Health;
            public float Shield;
            public float Mana;
            public bool Alive;
            public bool AscDestroying;
            public bool RevivePending;
            public bool CanRequestRevive;
            public bool IsSummoned;
            public Entity PrimaryAbility;
            public Entity ManaAbility;
            public Entity ControlAbility;
            public Entity SupportAbility;
            public Entity SummonAbility;
            public bool PrimaryAbilityBusy;
            public bool ManaAbilityBusy;
            public bool ControlAbilityBusy;
            public bool SupportAbilityBusy;
            public bool SummonAbilityBusy;

            public bool HasShield => Shield > 0f;

            public bool HasTag(int denseTagIndex)
            {
                return CTagMask.IsValidIndex(denseTagIndex) && Tags.HasTag(denseTagIndex);
            }
        }

        private static bool TryReadAbilityCode(
            EntityManager em,
            Entity ability,
            out int abilityCode)
        {
            if (ability == Entity.Null || !em.Exists(ability))
            {
                abilityCode = 0;
                return false;
            }

            if (em.HasComponent<CAbilityBaseInfo>(ability))
            {
                abilityCode = em.GetComponentData<CAbilityBaseInfo>(ability).Code;
                return abilityCode > 0;
            }

            if (em.HasComponent<CAbilityConfig>(ability))
            {
                var config = em.GetComponentData<CAbilityConfig>(ability).Config;
                if (config.IsCreated)
                {
                    abilityCode = config.Value.Code;
                    return abilityCode > 0;
                }
            }

            abilityCode = 0;
            return false;
        }

        private static bool IsAbilityBusy(EntityManager em, Entity ability)
        {
            if (ability == Entity.Null || !em.Exists(ability))
                return true;

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

        private static void ReadCoreAttributes(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit,
            out float health,
            out float shield,
            out float mana)
        {
            health = 0f;
            shield = 0f;
            mana = 0f;

            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<BAttribute>(asc))
                return;

            var attributes = em.GetBuffer<BAttribute>(asc);
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == unit.HealthAttrSetCode
                    && attribute.Code == unit.HealthAttrCode)
                {
                    health = attribute.CurrentValue;
                }
                else if (attribute.AttrSetCode == unit.ShieldAttrSetCode
                         && attribute.Code == unit.ShieldAttrCode)
                {
                    shield = attribute.CurrentValue;
                }
                else if (attribute.AttrSetCode == unit.ManaAttrSetCode
                         && attribute.Code == unit.ManaAttrCode)
                {
                    mana = attribute.CurrentValue;
                }
            }
        }
    }
}
