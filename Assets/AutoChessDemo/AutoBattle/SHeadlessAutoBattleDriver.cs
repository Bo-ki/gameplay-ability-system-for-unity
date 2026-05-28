using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(ASCCommandRequestSystem))]
    [UpdateBefore(typeof(AbilityCommandRequestSystem))]
    public partial struct SHeadlessAutoBattleDriver : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoBattleDriver>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<CHeadlessAutoBattleUnit, AttributeValueBuffer, AbilitySlotBuffer, TagMaskComponent>()
                .Build();
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_unitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var drivers = _driverQuery.ToEntityArray(Allocator.Temp);
            if (drivers.Length == 0)
            {
                drivers.Dispose();
                return;
            }

            var driverEntity = drivers[0];
            var driver = em.GetComponentData<CHeadlessAutoBattleDriver>(driverEntity);
            if (!driver.Enabled)
            {
                drivers.Dispose();
                return;
            }

            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            if (driver.LastDecisionFrame == frame)
            {
                drivers.Dispose();
                return;
            }

            var units = _unitQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var issuedPrimary = 0;
            var issuedFinisher = 0;
            var lowestHealthSelections = 0;

            for (var i = 0; i < units.Length; i++)
            {
                var asc = units[i];
                var unit = em.GetComponentData<CHeadlessAutoBattleUnit>(asc);
                if (!IsAlive(em, asc, unit))
                    continue;

                if (!TrySelectCommand(
                        em,
                        units,
                        asc,
                        unit,
                        out var abilityCode,
                        out var target,
                        out var targetPolicy,
                        out var isFinisher))
                {
                    continue;
                }

                var request = ecb.CreateEntity();
                ecb.SetName(request, $"AutoBattleActivate_{abilityCode}");
                ecb.AddComponent(request, new AbilityCommandRequestComponent
                {
                    Owner = asc,
                    AbilityCode = abilityCode,
                    CommandType = EAbilityCommandType.Activate,
                    TargetAsc = target,
                });

                if (isFinisher)
                    issuedFinisher++;
                else
                    issuedPrimary++;
                if (targetPolicy == HeadlessAutoBattleTargetPolicy.LowestHealth)
                    lowestHealthSelections++;
            }

            ecb.Playback(em);
            ecb.Dispose();
            units.Dispose();

            driver.LastDecisionFrame = frame;
            driver.IssuedCommandCount += issuedPrimary + issuedFinisher;
            driver.IssuedPrimaryCommandCount += issuedPrimary;
            driver.IssuedFinisherCommandCount += issuedFinisher;
            driver.LowestHealthTargetCount += lowestHealthSelections;
            em.SetComponentData(driverEntity, driver);
            drivers.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool TrySelectCommand(
            EntityManager em,
            NativeArray<Entity> units,
            Entity asc,
            in CHeadlessAutoBattleUnit unit,
            out int abilityCode,
            out Entity target,
            out HeadlessAutoBattleTargetPolicy targetPolicy,
            out bool isFinisher)
        {
            abilityCode = 0;
            target = Entity.Null;
            targetPolicy = HeadlessAutoBattleTargetPolicy.Frontline;
            isFinisher = false;

            if (unit.FinisherAbilityCode > 0
                && CanActivate(em, asc, unit, unit.FinisherAbilityCode))
            {
                var finisherPolicy = unit.FinisherTargetPolicy;
                var finisherTarget = FindAliveEnemy(em, units, unit.Team, finisherPolicy);
                if (finisherTarget != Entity.Null
                    && IsUnderFinisherThreshold(em, finisherTarget, unit))
                {
                    abilityCode = unit.FinisherAbilityCode;
                    target = finisherTarget;
                    targetPolicy = finisherPolicy;
                    isFinisher = true;
                    return true;
                }
            }

            if (unit.PrimaryAbilityCode <= 0
                || !CanActivate(em, asc, unit, unit.PrimaryAbilityCode))
            {
                return false;
            }

            target = FindAliveEnemy(em, units, unit.Team, unit.PrimaryTargetPolicy);
            if (target == Entity.Null)
                return false;

            abilityCode = unit.PrimaryAbilityCode;
            targetPolicy = unit.PrimaryTargetPolicy;
            return true;
        }

        private static Entity FindAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoBattleTeam team,
            HeadlessAutoBattleTargetPolicy policy)
        {
            return policy == HeadlessAutoBattleTargetPolicy.LowestHealth
                ? FindLowestHealthAliveEnemy(em, units, team)
                : FindFrontlineAliveEnemy(em, units, team);
        }

        private static Entity FindFrontlineAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoBattleTeam team)
        {
            var bestTarget = Entity.Null;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var candidateUnit = em.GetComponentData<CHeadlessAutoBattleUnit>(candidate);
                if (candidateUnit.Team == team)
                    continue;
                if (!IsAlive(em, candidate, candidateUnit))
                    continue;
                if (candidateUnit.Slot > bestSlot)
                    continue;
                if (candidateUnit.Slot == bestSlot
                    && bestTarget != Entity.Null
                    && candidate.Index >= bestTarget.Index)
                {
                    continue;
                }

                bestTarget = candidate;
                bestSlot = candidateUnit.Slot;
            }

            return bestTarget;
        }

        private static Entity FindLowestHealthAliveEnemy(
            EntityManager em,
            NativeArray<Entity> units,
            HeadlessAutoBattleTeam team)
        {
            var bestTarget = Entity.Null;
            var bestHealth = float.MaxValue;
            var bestSlot = int.MaxValue;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                var candidateUnit = em.GetComponentData<CHeadlessAutoBattleUnit>(candidate);
                if (candidateUnit.Team == team)
                    continue;
                if (!IsAlive(em, candidate, candidateUnit))
                    continue;

                var candidateHealth = GetAttribute(
                    em,
                    candidate,
                    candidateUnit.HealthAttrSetCode,
                    candidateUnit.HealthAttrCode);
                if (candidateHealth > bestHealth)
                    continue;
                if (candidateHealth == bestHealth && candidateUnit.Slot > bestSlot)
                    continue;
                if (candidateHealth == bestHealth
                    && candidateUnit.Slot == bestSlot
                    && bestTarget != Entity.Null
                    && candidate.Index >= bestTarget.Index)
                {
                    continue;
                }

                bestTarget = candidate;
                bestHealth = candidateHealth;
                bestSlot = candidateUnit.Slot;
            }

            return bestTarget;
        }

        private static bool IsUnderFinisherThreshold(
            EntityManager em,
            Entity target,
            in CHeadlessAutoBattleUnit attacker)
        {
            if (attacker.FinisherHealthThreshold <= 0f)
                return false;

            return GetAttribute(
                       em,
                       target,
                       attacker.HealthAttrSetCode,
                       attacker.HealthAttrCode)
                   <= attacker.FinisherHealthThreshold;
        }

        private static bool CanActivate(
            EntityManager em,
            Entity asc,
            in CHeadlessAutoBattleUnit unit,
            int abilityCode)
        {
            if (asc == Entity.Null
                || !em.Exists(asc)
                || em.HasComponent<ASCDestroyingComponent>(asc)
                || HasDenseTag(em, asc, unit.CooldownTagIndex)
                || GetAttribute(em, asc, unit.EnergyAttrSetCode, unit.EnergyAttrCode) < 1f)
            {
                return false;
            }

            return !IsAbilityBusy(em, asc, abilityCode);
        }

        private static bool IsAbilityBusy(EntityManager em, Entity asc, int abilityCode)
        {
            if (!em.HasBuffer<AbilitySlotBuffer>(asc))
                return true;

            var abilities = em.GetBuffer<AbilitySlotBuffer>(asc);
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i].AbilityEntity;
                if (!IsAbilityWithCode(em, ability, abilityCode))
                    continue;

                if (em.HasComponent<AbilityActivationPendingComponent>(ability)
                    || em.HasComponent<AbilityCommitRequestComponent>(ability))
                {
                    return true;
                }

                if (!em.HasComponent<AbilityStateComponent>(ability))
                    return false;

                var runtime = em.GetComponentData<AbilityStateComponent>(ability);
                return runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
            }

            return true;
        }

        private static bool IsAbilityWithCode(EntityManager em, Entity ability, int abilityCode)
        {
            if (ability == Entity.Null || !em.Exists(ability))
                return false;
            if (em.HasComponent<AbilityStateComponent>(ability)
                && em.GetComponentData<AbilityStateComponent>(ability).Code == abilityCode)
            {
                return true;
            }

            return false;
        }

        private static bool IsAlive(EntityManager em, Entity asc, in CHeadlessAutoBattleUnit unit)
        {
            return asc != Entity.Null
                   && em.Exists(asc)
                   && !em.HasComponent<ASCDestroyingComponent>(asc)
                   && GetAttribute(em, asc, unit.HealthAttrSetCode, unit.HealthAttrCode) > 0f;
        }

        private static float GetAttribute(
            EntityManager em,
            Entity asc,
            int attrSetCode,
            int attrCode)
        {
            if (asc == Entity.Null || !em.Exists(asc) || !em.HasBuffer<AttributeValueBuffer>(asc))
                return 0f;

            var attributes = em.GetBuffer<AttributeValueBuffer>(asc);
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
            return TagMaskComponent.IsValidIndex(denseTagIndex)
                   && asc != Entity.Null
                   && em.Exists(asc)
                   && em.HasComponent<TagMaskComponent>(asc)
                   && em.GetComponentData<TagMaskComponent>(asc).HasTag(denseTagIndex);
        }
    }
}
