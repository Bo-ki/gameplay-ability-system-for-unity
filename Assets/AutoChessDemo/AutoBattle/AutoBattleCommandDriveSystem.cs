using Unity.Collections;
using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
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

            using (var units = new NativeList<AutoBattleUnitTargetStateRecord>(Allocator.Temp))
            {
                foreach (var (unit, tags, attributes, entity)
                         in SystemAPI.Query<
                                 RefRO<AutoBattleUnitComponent>,
                                 RefRO<TagMaskComponent>,
                                 DynamicBuffer<AttributeValueBuffer>>()
                             .WithNone<ASCDestroyingComponent>()
                             .WithEntityAccess())
                {
                    var unitValue = unit.ValueRO;
                    var health = GetAttribute(
                        attributes,
                        unitValue.HealthAttrSetCode,
                        unitValue.HealthAttrCode);
                    if (health <= 0f)
                        continue;

                    units.Add(new AutoBattleUnitTargetStateRecord
                    {
                        Asc = entity,
                        Team = unitValue.Team,
                        Slot = unitValue.Slot,
                        PrimaryAbilityCode = unitValue.PrimaryAbilityCode,
                        FinisherAbilityCode = unitValue.FinisherAbilityCode,
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
                }

                if (units.Length > 0)
                    FlushCommandRequests(state.EntityManager, units.AsArray(), ref driver);

                driver.LastDecisionFrame = frame;
                SystemAPI.SetComponent(driverEntity, driver);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void FlushCommandRequests(
            EntityManager em,
            NativeArray<AutoBattleUnitTargetStateRecord> units,
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
                        units,
                        out var abilityCode,
                        out var target,
                        out var policy,
                        out var isFinisher))
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
            NativeArray<AutoBattleUnitTargetStateRecord> units,
            out int abilityCode,
            out Entity target,
            out AutoBattleTargetPolicy policy,
            out bool isFinisher)
        {
            abilityCode = 0;
            target = Entity.Null;
            policy = AutoBattleTargetPolicy.Frontline;
            isFinisher = false;

            if (source.FinisherAbilityCode > 0
                && TryFindAliveEnemy(source, units, source.FinisherTargetPolicy, out var finisherTarget)
                && finisherTarget.Health <= source.FinisherHealthThreshold)
            {
                abilityCode = source.FinisherAbilityCode;
                target = finisherTarget.Asc;
                policy = source.FinisherTargetPolicy;
                isFinisher = true;
                return true;
            }

            if (source.PrimaryAbilityCode <= 0
                || !TryFindAliveEnemy(source, units, source.PrimaryTargetPolicy, out var primaryTarget))
            {
                return false;
            }

            abilityCode = source.PrimaryAbilityCode;
            target = primaryTarget.Asc;
            policy = source.PrimaryTargetPolicy;
            return true;
        }

        private static bool TryFindAliveEnemy(
            in AutoBattleUnitTargetStateRecord source,
            NativeArray<AutoBattleUnitTargetStateRecord> units,
            AutoBattleTargetPolicy policy,
            out AutoBattleUnitTargetStateRecord target)
        {
            target = default;
            var found = false;

            for (var i = 0; i < units.Length; i++)
            {
                var candidate = units[i];
                if (candidate.Team == source.Team
                    || candidate.Asc == Entity.Null
                    || candidate.Health <= 0f)
                {
                    continue;
                }

                if (!found || IsBetterTarget(candidate, target, policy))
                {
                    target = candidate;
                    found = true;
                }
            }

            return found;
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
    }
}
