using Unity.Collections;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(ASCCommandRequestSystem))]
    [UpdateBefore(typeof(AbilityCommandRequestSystem))]
    public partial struct AutoBattleCommandDriveSystem : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<AutoBattleCommandDriverComponent>()
                .Build();
            _unitQuery = SystemAPI.QueryBuilder()
                .WithAll<AutoBattleUnitComponent, AttributeValueBuffer, TagMaskComponent>()
                .WithNone<ASCDestroyingComponent>()
                .Build();

            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_unitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var driverEntity = _driverQuery.GetSingletonEntity();
            var driver = SystemAPI.GetComponent<AutoBattleCommandDriverComponent>(driverEntity);
            if (!driver.Enabled)
                return;

            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            if (driver.LastDecisionFrame == frame)
                return;

            var unitCapacity = _unitQuery.CalculateEntityCount();
            if (unitCapacity < 1)
                unitCapacity = 1;

            var attributesByAsc = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true);
            using (var units = new NativeList<AutoBattleUnitTargetStateRecord>(unitCapacity, Allocator.Temp))
            {
                var maxBattleGroup = -1;
                var playerAliveCount = 0;
                var enemyAliveCount = 0;
                foreach (var (unit, tags, entity)
                         in SystemAPI.Query<
                                 RefRO<AutoBattleUnitComponent>,
                                 RefRO<TagMaskComponent>>()
                             .WithNone<ASCDestroyingComponent>()
                             .WithEntityAccess())
                {
                    if (!attributesByAsc.HasBuffer(entity))
                        continue;

                    var attributes = attributesByAsc[entity];
                    var unitValue = unit.ValueRO;
                    var health = GetAttribute(
                        attributes,
                        unitValue.HealthAttrSetCode,
                        unitValue.HealthAttrCode);
                    if (health <= 0f)
                        continue;

                    if (unitValue.Team == HeadlessAutoBattleTeam.Player)
                        playerAliveCount++;
                    else if (unitValue.Team == HeadlessAutoBattleTeam.Enemy)
                        enemyAliveCount++;

                    units.Add(new AutoBattleUnitTargetStateRecord
                    {
                        Asc = entity,
                        BattleGroup = unitValue.BattleGroup,
                        Team = unitValue.Team,
                        Slot = unitValue.Slot,
                        PrimaryAbilityCode = unitValue.PrimaryAbilityCode,
                        FinisherAbilityCode = unitValue.FinisherAbilityCode,
                        PrimaryAbilityEntity = unitValue.PrimaryAbilityEntity,
                        FinisherAbilityEntity = unitValue.FinisherAbilityEntity,
                        CooldownTagIndex = unitValue.CooldownTagIndex,
                        FinisherHealthThreshold = unitValue.FinisherHealthThreshold,
                        PrimaryTargetPolicy = unitValue.PrimaryTargetPolicy,
                        FinisherTargetPolicy = unitValue.FinisherTargetPolicy,
                        Tags = tags.ValueRO,
                        Health = health,
                        Energy = GetAttribute(
                            attributes,
                            unitValue.EnergyAttrSetCode,
                            unitValue.EnergyAttrCode),
                    });
                    if (unitValue.BattleGroup > maxBattleGroup)
                        maxBattleGroup = unitValue.BattleGroup;
                }

                if (units.Length > 0)
                {
                    using var targetCache = new NativeArray<AutoBattleGroupTargetCacheRecord>(
                        maxBattleGroup + 1,
                        Allocator.Temp);
                    BuildTargetCache(units.AsArray(), targetCache);
                    FlushCommandRequests(state.EntityManager, units.AsArray(), targetCache, ref driver);
                }

                driver.LastDecisionFrame = frame;
                driver.LastOutcomeFrame = frame;
                driver.PlayerAliveCount = playerAliveCount;
                driver.EnemyAliveCount = enemyAliveCount;
                SystemAPI.SetComponent(driverEntity, driver);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void FlushCommandRequests(
            EntityManager em,
            NativeArray<AutoBattleUnitTargetStateRecord> units,
            NativeArray<AutoBattleGroupTargetCacheRecord> targetCache,
            ref AutoBattleCommandDriverComponent driver)
        {
            var issuedPrimary = 0;
            var issuedFinisher = 0;
            var lowestHealthSelections = 0;
            var commandBuffer = ResolveCommandBuffer(em);

            for (var i = 0; i < units.Length; i++)
            {
                var source = units[i];
                if (!CanActivate(source))
                    continue;

                if (!TrySelectCommand(
                        source,
                        targetCache,
                        out var abilityCode,
                        out var target,
                        out var policy,
                        out var isFinisher,
                        out var abilityEntity))
                {
                    continue;
                }

                if (commandBuffer.IsCreated)
                {
                    commandBuffer.Add(new AbilityCommandBuffer
                    {
                        Command = new AbilityCommandRequestComponent
                        {
                            Owner = source.Asc,
                            AbilityCode = abilityCode,
                            AbilityEntity = abilityEntity,
                            CommandType = EAbilityCommandType.Activate,
                            TargetAsc = target,
                        },
                    });
                }

                if (isFinisher)
                    issuedFinisher++;
                else
                    issuedPrimary++;
                if (policy == AutoBattleTargetPolicy.LowestHealth)
                    lowestHealthSelections++;
            }

            driver.IssuedCommandCount += issuedPrimary + issuedFinisher;
            driver.IssuedPrimaryCommandCount += issuedPrimary;
            driver.IssuedFinisherCommandCount += issuedFinisher;
            driver.LowestHealthTargetCount += lowestHealthSelections;
        }

        private static DynamicBuffer<AbilityCommandBuffer> ResolveCommandBuffer(EntityManager em)
        {
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || streamEntity == Entity.Null
                || !em.Exists(streamEntity)
                || !em.HasBuffer<AbilityCommandBuffer>(streamEntity))
            {
                return default;
            }

            return em.GetBuffer<AbilityCommandBuffer>(streamEntity);
        }

        private static bool TrySelectCommand(
            in AutoBattleUnitTargetStateRecord source,
            NativeArray<AutoBattleGroupTargetCacheRecord> targetCache,
            out int abilityCode,
            out Entity target,
            out AutoBattleTargetPolicy policy,
            out bool isFinisher,
            out Entity abilityEntity)
        {
            abilityCode = 0;
            target = Entity.Null;
            policy = AutoBattleTargetPolicy.Frontline;
            isFinisher = false;
            abilityEntity = Entity.Null;

            if (source.FinisherAbilityCode > 0
                && source.FinisherAbilityEntity != Entity.Null
                && TryFindAliveEnemy(source, targetCache, source.FinisherTargetPolicy, out var finisherTarget)
                && finisherTarget.Health <= source.FinisherHealthThreshold)
            {
                abilityCode = source.FinisherAbilityCode;
                abilityEntity = source.FinisherAbilityEntity;
                target = finisherTarget.Asc;
                policy = source.FinisherTargetPolicy;
                isFinisher = true;
                return true;
            }

            if (source.PrimaryAbilityCode <= 0
                || source.PrimaryAbilityEntity == Entity.Null
                || !TryFindAliveEnemy(source, targetCache, source.PrimaryTargetPolicy, out var primaryTarget))
            {
                return false;
            }

            abilityCode = source.PrimaryAbilityCode;
            abilityEntity = source.PrimaryAbilityEntity;
            target = primaryTarget.Asc;
            policy = source.PrimaryTargetPolicy;
            return true;
        }

        private static bool TryFindAliveEnemy(
            in AutoBattleUnitTargetStateRecord source,
            NativeArray<AutoBattleGroupTargetCacheRecord> targetCache,
            AutoBattleTargetPolicy policy,
            out AutoBattleUnitTargetStateRecord target)
        {
            target = default;
            if (source.BattleGroup < 0 || source.BattleGroup >= targetCache.Length)
                return false;

            var cache = targetCache[source.BattleGroup];
            if (source.Team == HeadlessAutoBattleTeam.Player)
                return TryResolveCachedTarget(in cache.Enemy, policy, out target);
            if (source.Team == HeadlessAutoBattleTeam.Enemy)
                return TryResolveCachedTarget(in cache.Player, policy, out target);

            return false;
        }

        private static void BuildTargetCache(
            NativeArray<AutoBattleUnitTargetStateRecord> units,
            NativeArray<AutoBattleGroupTargetCacheRecord> targetCache)
        {
            for (var i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                if (unit.BattleGroup < 0
                    || unit.BattleGroup >= targetCache.Length
                    || unit.Asc == Entity.Null
                    || unit.Health <= 0f)
                {
                    continue;
                }

                var cache = targetCache[unit.BattleGroup];
                if (unit.Team == HeadlessAutoBattleTeam.Player)
                    UpdateTeamTargetCache(ref cache.Player, in unit);
                else if (unit.Team == HeadlessAutoBattleTeam.Enemy)
                    UpdateTeamTargetCache(ref cache.Enemy, in unit);
                targetCache[unit.BattleGroup] = cache;
            }
        }

        private static void UpdateTeamTargetCache(
            ref AutoBattleTeamTargetCacheRecord cache,
            in AutoBattleUnitTargetStateRecord unit)
        {
            if (cache.HasFrontline == 0
                || IsBetterTarget(unit, cache.Frontline, AutoBattleTargetPolicy.Frontline))
            {
                cache.Frontline = unit;
                cache.HasFrontline = 1;
            }

            if (cache.HasLowestHealth == 0
                || IsBetterTarget(unit, cache.LowestHealth, AutoBattleTargetPolicy.LowestHealth))
            {
                cache.LowestHealth = unit;
                cache.HasLowestHealth = 1;
            }
        }

        private static bool TryResolveCachedTarget(
            in AutoBattleTeamTargetCacheRecord cache,
            AutoBattleTargetPolicy policy,
            out AutoBattleUnitTargetStateRecord target)
        {
            if (policy == AutoBattleTargetPolicy.LowestHealth && cache.HasLowestHealth != 0)
            {
                target = cache.LowestHealth;
                return true;
            }

            if (cache.HasFrontline != 0)
            {
                target = cache.Frontline;
                return true;
            }

            target = default;
            return false;
        }

        private static bool IsBetterTarget(
            in AutoBattleUnitTargetStateRecord candidate,
            in AutoBattleUnitTargetStateRecord current,
            AutoBattleTargetPolicy policy)
        {
            if (policy == AutoBattleTargetPolicy.LowestHealth)
            {
                if (candidate.Health < current.Health)
                    return true;
                if (candidate.Health > current.Health)
                    return false;
            }

            if (candidate.Slot < current.Slot)
                return true;
            if (candidate.Slot > current.Slot)
                return false;

            if (candidate.Asc.Index < current.Asc.Index)
                return true;
            if (candidate.Asc.Index > current.Asc.Index)
                return false;

            return candidate.Asc.Version < current.Asc.Version;
        }

        private static bool CanActivate(in AutoBattleUnitTargetStateRecord source)
        {
            return source.Asc != Entity.Null
                   && source.Energy >= 1f
                   && !HasDenseTag(source.Tags, source.CooldownTagIndex);
        }

        private static float GetAttribute(
            DynamicBuffer<AttributeValueBuffer> attributes,
            int attrSetCode,
            int attrCode)
        {
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == attrSetCode && attribute.Code == attrCode)
                    return attribute.CurrentValue;
            }

            return 0f;
        }

        private static bool HasDenseTag(TagMaskComponent tags, int denseTagIndex)
        {
            return TagMaskComponent.IsValidIndex(denseTagIndex)
                   && tags.HasTag(denseTagIndex);
        }

        private struct AutoBattleGroupTargetCacheRecord
        {
            public AutoBattleTeamTargetCacheRecord Player;
            public AutoBattleTeamTargetCacheRecord Enemy;
        }

        private struct AutoBattleTeamTargetCacheRecord
        {
            public byte HasFrontline;
            public byte HasLowestHealth;
            public AutoBattleUnitTargetStateRecord Frontline;
            public AutoBattleUnitTargetStateRecord LowestHealth;
        }
    }
}
