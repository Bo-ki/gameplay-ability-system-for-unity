using GAS.Runtime.Generated;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SHeadlessAutoChessAbilitySlotLink))]
    public partial struct SHeadlessAutoChessDriver : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessDriver>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoChessUnit, CHeadlessAutoChessAbilitySlots, BAttribute, CTagMask>()
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

            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            if (driver.LastDecisionFrame == frame)
                return;

            using var units = _unitQuery.ToEntityArray(Allocator.Temp);
            if (TryResolveWinner(em, units, out var winner))
            {
                driver.Completed = true;
                driver.Winner = winner;
                driver.LastDecisionFrame = frame;
                em.SetComponentData(driverEntity, driver);
                return;
            }

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            var issuedPrimary = 0;
            var issuedMana = 0;
            var issuedControl = 0;
            var issuedSupport = 0;
            var issuedSummon = 0;
            var skippedCrowdControl = 0;
            var frontlineTargets = 0;
            var lowestHealthTargets = 0;

            for (var i = 0; i < units.Length; i++)
            {
                var asc = units[i];
                var unit = em.GetComponentData<CHeadlessAutoChessUnit>(asc);
                var slots = em.GetComponentData<CHeadlessAutoChessAbilitySlots>(asc);
                if (!IsAlive(em, asc, in unit))
                    continue;

                if (IsCrowdControlled(em, asc, in unit))
                {
                    skippedCrowdControl++;
                    continue;
                }

                if (!TrySelectCommand(
                        em,
                        units,
                        asc,
                        in unit,
                        in slots,
                        out var abilityCode,
                        out var target,
                        out var targetPolicy,
                        out var commandKind))
                {
                    continue;
                }

                var command = ecb.CreateEntity();
                ecb.SetName(command, $"AutoChessActivate_{abilityCode}");
                ecb.AddComponent(command, new CAbilityCommandRequest
                {
                    Owner = asc,
                    AbilityCode = abilityCode,
                    CommandType = EAbilityCommandType.Activate,
                    TargetAsc = target,
                });

                if (commandKind == AutoChessDriverCommandKind.Mana)
                    issuedMana++;
                else if (commandKind == AutoChessDriverCommandKind.Control)
                    issuedControl++;
                else if (commandKind == AutoChessDriverCommandKind.Support)
                    issuedSupport++;
                else if (commandKind == AutoChessDriverCommandKind.Summon)
                    issuedSummon++;
                else
                    issuedPrimary++;

                if (targetPolicy == HeadlessAutoChessTargetPolicy.LowestHealth)
                    lowestHealthTargets++;
                else
                    frontlineTargets++;
            }

            ecb.Playback(em);

            driver.LastDecisionFrame = frame;
            driver.TurnCount++;
            driver.IssuedPrimaryCommandCount += issuedPrimary;
            driver.IssuedManaAbilityCommandCount += issuedMana;
            driver.IssuedControlAbilityCommandCount += issuedControl;
            driver.IssuedSupportAbilityCommandCount += issuedSupport;
            driver.IssuedSummonAbilityCommandCount += issuedSummon;
            driver.CrowdControlTurnSkippedCount += skippedCrowdControl;
            driver.IssuedCommandCount += issuedPrimary + issuedMana + issuedControl + issuedSupport + issuedSummon;
            driver.FrontlineTargetCount += frontlineTargets;
            driver.LowestHealthTargetCount += lowestHealthTargets;
            em.SetComponentData(driverEntity, driver);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool TrySelectCommand(
            EntityManager em,
            NativeArray<Entity> units,
            Entity asc,
            in CHeadlessAutoChessUnit unit,
            in CHeadlessAutoChessAbilitySlots slots,
            out int abilityCode,
            out Entity target,
            out HeadlessAutoChessTargetPolicy targetPolicy,
            out AutoChessDriverCommandKind commandKind)
        {
            abilityCode = 0;
            target = Entity.Null;
            targetPolicy = HeadlessAutoChessTargetPolicy.Frontline;
            commandKind = AutoChessDriverCommandKind.Primary;

            if (TrySelectSupportCommand(
                    em,
                    units,
                    asc,
                    in unit,
                    in slots,
                    out abilityCode,
                    out target,
                    out targetPolicy))
            {
                commandKind = AutoChessDriverCommandKind.Support;
                return true;
            }

            if (unit.SummonAbilityCode > 0
                && CountActiveSummons(em, units, asc) < unit.MaxActiveSummons
                && CanActivate(em, asc, unit.SummonAbilityCode, slots.SummonAbility, unit.SummonCooldownTagIndex))
            {
                abilityCode = unit.SummonAbilityCode;
                target = asc;
                targetPolicy = HeadlessAutoChessTargetPolicy.Frontline;
                commandKind = AutoChessDriverCommandKind.Summon;
                return true;
            }

            if (unit.ControlAbilityCode == HeadlessAutoChessScenario.AbilityPlayerControlStun
                && CanActivate(em, asc, unit.ControlAbilityCode, slots.ControlAbility, unit.ControlCooldownTagIndex))
            {
                target = FindAliveEnemyWithoutDenseTag(
                    em,
                    units,
                    unit.Team,
                    unit.ControlTargetPolicy,
                    unit.CrowdControlTagIndex);
                if (target != Entity.Null)
                {
                    abilityCode = unit.ControlAbilityCode;
                    targetPolicy = unit.ControlTargetPolicy;
                    commandKind = AutoChessDriverCommandKind.Control;
                    return true;
                }
            }

            if (unit.ManaAbilityCode > 0
                && GetAttribute(em, asc, unit.ManaAttrSetCode, unit.ManaAttrCode) >= unit.ManaAbilityThreshold
                && CanActivate(em, asc, unit.ManaAbilityCode, slots.ManaAbility, unit.ManaCooldownTagIndex))
            {
                target = FindAliveEnemy(em, units, unit.Team, unit.ManaTargetPolicy);
                if (target != Entity.Null)
                {
                    abilityCode = unit.ManaAbilityCode;
                    targetPolicy = unit.ManaTargetPolicy;
                    commandKind = AutoChessDriverCommandKind.Mana;
                    return true;
                }
            }

            if (unit.PrimaryAbilityCode <= 0
                || !CanActivate(em, asc, unit.PrimaryAbilityCode, slots.PrimaryAbility, unit.PrimaryCooldownTagIndex))
            {
                return false;
            }

            target = FindAliveEnemy(em, units, unit.Team, unit.PrimaryTargetPolicy);
            if (target == Entity.Null)
                return false;

            abilityCode = unit.PrimaryAbilityCode;
            targetPolicy = unit.PrimaryTargetPolicy;
            commandKind = AutoChessDriverCommandKind.Primary;
            return true;
        }

        private static bool TrySelectSupportCommand(
            EntityManager em,
            NativeArray<Entity> units,
            Entity asc,
            in CHeadlessAutoChessUnit unit,
            in CHeadlessAutoChessAbilitySlots slots,
            out int abilityCode,
            out Entity target,
            out HeadlessAutoChessTargetPolicy targetPolicy)
        {
            abilityCode = 0;
            target = Entity.Null;
            targetPolicy = unit.SupportTargetPolicy;

            if (unit.SupportAbilityCode <= 0
                || !CanActivate(em, asc, unit.SupportAbilityCode, slots.SupportAbility, unit.SupportCooldownTagIndex))
            {
                return false;
            }

            if (unit.SupportAbilityCode == HeadlessAutoChessScenario.AbilityPlayerCleanse)
                target = FindAliveAllyWithDenseTag(em, units, unit.Team, unit.CrowdControlTagIndex);
            else if (unit.SupportAbilityCode == HeadlessAutoChessScenario.AbilityPlayerBarrier)
                target = FindLowestShieldAliveAlly(em, units, unit.Team, unit.ShieldAttrSetCode, unit.ShieldAttrCode);

            if (target == Entity.Null)
                return false;

            abilityCode = unit.SupportAbilityCode;
            return true;
        }

        private static Entity FindAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoChessTeam team,
            HeadlessAutoChessTargetPolicy policy)
        {
            return policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthAliveEnemy(em, units, team)
                : FindFrontlineAliveEnemy(em, units, team);
        }

        private static Entity FindAliveEnemyWithoutDenseTag(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoChessTeam team,
            HeadlessAutoChessTargetPolicy policy,
            int denseTagIndex)
        {
            var target = policy == HeadlessAutoChessTargetPolicy.LowestHealth
                ? FindLowestHealthAliveEnemy(em, units, team, denseTagIndex)
                : FindFrontlineAliveEnemy(em, units, team, denseTagIndex);
            return target;
        }

        private static Entity FindFrontlineAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoChessTeam team)
        {
            return FindFrontlineAliveEnemy(em, units, team, -1);
        }

        private static Entity FindFrontlineAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoChessTeam team,
            int excludedDenseTagIndex)
        {
            var bestTarget = Entity.Null;
            var bestRank = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var candidateUnit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (candidateUnit.Team == team
                    || !IsAlive(em, candidate, in candidateUnit)
                    || HasDenseTag(em, candidate, excludedDenseTagIndex))
                {
                    continue;
                }

                var rank = GetFrontlineRank(candidateUnit);
                if (rank > bestRank)
                    continue;
                if (rank == bestRank
                    && bestTarget != Entity.Null
                    && candidate.Index >= bestTarget.Index)
                {
                    continue;
                }

                bestTarget = candidate;
                bestRank = rank;
            }

            return bestTarget;
        }

        private static Entity FindLowestHealthAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoChessTeam team)
        {
            return FindLowestHealthAliveEnemy(em, units, team, -1);
        }

        private static Entity FindLowestHealthAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoChessTeam team,
            int excludedDenseTagIndex)
        {
            var bestTarget = Entity.Null;
            var bestHealth = float.MaxValue;
            var bestRank = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var candidateUnit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (candidateUnit.Team == team
                    || !IsAlive(em, candidate, in candidateUnit)
                    || HasDenseTag(em, candidate, excludedDenseTagIndex))
                {
                    continue;
                }

                var health = GetAttribute(
                    em,
                    candidate,
                    candidateUnit.HealthAttrSetCode,
                    candidateUnit.HealthAttrCode);
                var rank = GetFrontlineRank(candidateUnit);
                if (health > bestHealth)
                    continue;
                if (health == bestHealth && rank > bestRank)
                    continue;
                if (health == bestHealth
                    && rank == bestRank
                    && bestTarget != Entity.Null
                    && candidate.Index >= bestTarget.Index)
                {
                    continue;
                }

                bestTarget = candidate;
                bestHealth = health;
                bestRank = rank;
            }

            return bestTarget;
        }

        private static Entity FindAliveAllyWithDenseTag(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoChessTeam team,
            int denseTagIndex)
        {
            var bestTarget = Entity.Null;
            var bestRank = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var candidateUnit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (candidateUnit.Team != team
                    || !IsAlive(em, candidate, in candidateUnit)
                    || !HasDenseTag(em, candidate, denseTagIndex))
                {
                    continue;
                }

                var rank = GetFrontlineRank(candidateUnit);
                if (rank > bestRank)
                    continue;
                if (rank == bestRank
                    && bestTarget != Entity.Null
                    && candidate.Index >= bestTarget.Index)
                {
                    continue;
                }

                bestTarget = candidate;
                bestRank = rank;
            }

            return bestTarget;
        }

        private static Entity FindLowestShieldAliveAlly(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoChessTeam team,
            int shieldAttrSetCode,
            int shieldAttrCode)
        {
            var bestTarget = Entity.Null;
            var bestShield = float.MaxValue;
            var bestRank = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var candidateUnit = em.GetComponentData<CHeadlessAutoChessUnit>(candidate);
                if (candidateUnit.Team != team || !IsAlive(em, candidate, in candidateUnit))
                    continue;
                if (HasDenseTag(em, candidate, AutoChessTagBits.AutoChessShieldedIndex))
                    continue;

                var shield = GetAttribute(em, candidate, shieldAttrSetCode, shieldAttrCode);
                var maxShield = GetAttributeMax(em, candidate, shieldAttrSetCode, shieldAttrCode);
                if (maxShield > 0f && shield >= maxShield)
                    continue;

                var rank = GetFrontlineRank(candidateUnit);
                if (shield > bestShield)
                    continue;
                if (shield == bestShield && rank > bestRank)
                    continue;
                if (shield == bestShield
                    && rank == bestRank
                    && bestTarget != Entity.Null
                    && candidate.Index >= bestTarget.Index)
                {
                    continue;
                }

                bestTarget = candidate;
                bestShield = shield;
                bestRank = rank;
            }

            return bestTarget;
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

        private static int GetFrontlineRank(in CHeadlessAutoChessUnit unit)
        {
            var boardRank = unit.Team == HeadlessAutoChessTeam.Player
                ? -unit.BoardX
                : unit.BoardX;
            return boardRank * 1000 + unit.BoardY * 10 + unit.Slot;
        }

        private static bool CanActivate(
            EntityManager em,
            Entity asc,
            int abilityCode,
            Entity ability,
            int cooldownTagIndex)
        {
            return abilityCode > 0
                   && asc != Entity.Null
                   && em.Exists(asc)
                   && ability != Entity.Null
                   && em.Exists(ability)
                   && !em.HasComponent<CAscDestroying>(asc)
                   && !HasDenseTag(em, asc, cooldownTagIndex)
                   && !IsAbilityBusy(em, ability);
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

        private static bool IsCrowdControlled(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoChessUnit unit)
        {
            return HasDenseTag(em, asc, unit.CrowdControlTagIndex);
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

        private static float GetAttributeMax(
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
                    return attribute.MaxValue;
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

        private static bool TryResolveWinner(
            EntityManager em,
            NativeArray<Entity> units,
            out HeadlessAutoChessTeam winner)
        {
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

        private enum AutoChessDriverCommandKind : byte
        {
            Primary,
            Mana,
            Control,
            Support,
            Summon,
        }
    }
}
