using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateBefore(typeof(ASCCommandBufferResolveSystem))]
    public partial struct AutoChessBattleCommandDriveSystem : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _unitQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AutoChessBattleDriverComponent>(),
                },
            });
            _unitQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AutoChessBattleUnitComponent>(),
                    ComponentType.ReadOnly<AttributeValueBuffer>(),
                    ComponentType.ReadOnly<TagMaskComponent>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<ASCDestroyingComponent>(),
                },
            });

            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_unitQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var driverEntity = _driverQuery.GetSingletonEntity();
            var driver = SystemAPI.GetComponent<AutoChessBattleDriverComponent>(driverEntity);
            if (!driver.Enabled)
                return;

            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            if (driver.LastDecisionFrame == frame)
                return;

            var unitChunkCount = _unitQuery.CalculateChunkCountWithoutFiltering();
            var unitCapacity = unitChunkCount > 0 ? unitChunkCount * 64 : 1;
            var units = new NativeList<AutoChessBattleUnitTargetStateRecord>(unitCapacity, Allocator.TempJob);
            var targetCache = new NativeParallelHashMap<int, AutoChessBattleGroupTargetCacheRecord>(
                unitCapacity,
                Allocator.TempJob);
            var stats = new NativeReference<AutoChessBattleDecisionStats>(Allocator.TempJob);
            var flushStats = new NativeReference<AutoChessBattleFlushStats>(Allocator.TempJob);

            var collectHandle = new CollectUnitTargetStatesJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                UnitTypeHandle = SystemAPI.GetComponentTypeHandle<AutoChessBattleUnitComponent>(isReadOnly: true),
                TagTypeHandle = SystemAPI.GetComponentTypeHandle<TagMaskComponent>(isReadOnly: true),
                AttributeTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: true),
                Units = units,
                Stats = stats,
            }.Schedule(_unitQuery, state.Dependency);

            var cacheHandle = new BuildTargetCacheJob
            {
                Units = units,
                TargetCache = targetCache,
            }.Schedule(collectHandle);

            var flushHandle = new FlushCommandRequestsJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                UnitTypeHandle = SystemAPI.GetComponentTypeHandle<AutoChessBattleUnitComponent>(isReadOnly: true),
                TagTypeHandle = SystemAPI.GetComponentTypeHandle<TagMaskComponent>(isReadOnly: true),
                AttributeTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: true),
                AbilityCommandTypeHandle = SystemAPI.GetBufferTypeHandle<AbilityCommandBuffer>(),
                CommandPendingTypeHandle = SystemAPI.GetComponentTypeHandle<ASCCommandPendingComponent>(),
                Frame = frame,
                TargetCache = targetCache,
                FlushStats = flushStats,
            }.Schedule(_unitQuery, cacheHandle);

            var applyDriverHandle = new ApplyDriverStatsJob
            {
                DriverEntity = driverEntity,
                Driver = driver,
                Frame = frame,
                Stats = stats,
                FlushStats = flushStats,
                DriverLookup = SystemAPI.GetComponentLookup<AutoChessBattleDriverComponent>(),
            }.Schedule(flushHandle);

            var disposeUnitsHandle = units.Dispose(applyDriverHandle);
            var disposeCacheHandle = targetCache.Dispose(applyDriverHandle);
            var disposeStatsHandle = stats.Dispose(applyDriverHandle);
            var disposeFlushStatsHandle = flushStats.Dispose(applyDriverHandle);
            state.Dependency = JobHandle.CombineDependencies(
                collectHandle,
                JobHandle.CombineDependencies(
                    cacheHandle,
                    flushHandle,
                    JobHandle.CombineDependencies(
                        disposeUnitsHandle,
                        JobHandle.CombineDependencies(
                            disposeCacheHandle,
                            JobHandle.CombineDependencies(disposeStatsHandle, disposeFlushStatsHandle)))));
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct CollectUnitTargetStatesJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AutoChessBattleUnitComponent> UnitTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TagMaskComponent> TagTypeHandle;
            [ReadOnly] public BufferTypeHandle<AttributeValueBuffer> AttributeTypeHandle;
            public NativeList<AutoChessBattleUnitTargetStateRecord> Units;
            public NativeReference<AutoChessBattleDecisionStats> Stats;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var units = chunk.GetNativeArray(ref UnitTypeHandle);
                var tags = chunk.GetNativeArray(ref TagTypeHandle);
                var attributes = chunk.GetBufferAccessorRO(ref AttributeTypeHandle);
                var stats = Stats.Value;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var unitValue = units[entityIndex];
                    var unitAttributes = attributes[entityIndex];
                    var health = GetAttribute(
                        unitAttributes,
                        unitValue.HealthAttrSetCode,
                        unitValue.HealthAttrCode);
                    if (health <= 0f)
                        continue;

                    if (unitValue.Team == AutoChessTeam.Player)
                        stats.PlayerAliveCount++;
                    else if (unitValue.Team == AutoChessTeam.Enemy)
                        stats.EnemyAliveCount++;

                    Units.Add(new AutoChessBattleUnitTargetStateRecord
                    {
                        Asc = entities[entityIndex],
                        BattleGroup = unitValue.BattleGroup,
                        Team = unitValue.Team,
                        Slot = unitValue.Slot,
                        PrimaryAbilityCode = unitValue.PrimaryAbilityCode,
                        FinisherAbilityCode = unitValue.FinisherAbilityCode,
                        ActiveAbilityCode = unitValue.ActiveAbilityCode,
                        ActiveCastInterval = unitValue.ActiveCastInterval,
                        ActiveCastFrameOffset = unitValue.ActiveCastFrameOffset,
                        CooldownTagIndex = unitValue.CooldownTagIndex,
                        FinisherHealthThreshold = unitValue.FinisherHealthThreshold,
                        PrimaryTargetPolicy = unitValue.PrimaryTargetPolicy,
                        FinisherTargetPolicy = unitValue.FinisherTargetPolicy,
                        Tags = tags[entityIndex],
                        Health = health,
                        Energy = GetAttribute(
                            unitAttributes,
                            unitValue.EnergyAttrSetCode,
                            unitValue.EnergyAttrCode),
                    });
                }

                Stats.Value = stats;
            }
        }

        [BurstCompile]
        private struct BuildTargetCacheJob : IJob
        {
            public NativeList<AutoChessBattleUnitTargetStateRecord> Units;
            public NativeParallelHashMap<int, AutoChessBattleGroupTargetCacheRecord> TargetCache;

            public void Execute()
            {
                var units = Units.AsArray();
                if (units.Length > 0)
                    BuildTargetCache(units, TargetCache);
            }
        }

        [BurstCompile]
        private struct FlushCommandRequestsJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AutoChessBattleUnitComponent> UnitTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TagMaskComponent> TagTypeHandle;
            [ReadOnly] public BufferTypeHandle<AttributeValueBuffer> AttributeTypeHandle;
            public BufferTypeHandle<AbilityCommandBuffer> AbilityCommandTypeHandle;
            public ComponentTypeHandle<ASCCommandPendingComponent> CommandPendingTypeHandle;
            public int Frame;
            [ReadOnly] public NativeParallelHashMap<int, AutoChessBattleGroupTargetCacheRecord> TargetCache;
            public NativeReference<AutoChessBattleFlushStats> FlushStats;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var units = chunk.GetNativeArray(ref UnitTypeHandle);
                var tags = chunk.GetNativeArray(ref TagTypeHandle);
                var attributes = chunk.GetBufferAccessorRO(ref AttributeTypeHandle);
                var abilityCommands = chunk.GetBufferAccessor(ref AbilityCommandTypeHandle);
                var pendingMask = chunk.GetEnabledMask(ref CommandPendingTypeHandle);
                var flushStats = FlushStats.Value;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var unitValue = units[entityIndex];
                    var unitAttributes = attributes[entityIndex];
                    var health = GetAttribute(
                        unitAttributes,
                        unitValue.HealthAttrSetCode,
                        unitValue.HealthAttrCode);
                    if (health <= 0f)
                        continue;

                    var source = new AutoChessBattleUnitTargetStateRecord
                    {
                        Asc = entities[entityIndex],
                        BattleGroup = unitValue.BattleGroup,
                        Team = unitValue.Team,
                        Slot = unitValue.Slot,
                        PrimaryAbilityCode = unitValue.PrimaryAbilityCode,
                        FinisherAbilityCode = unitValue.FinisherAbilityCode,
                        ActiveAbilityCode = unitValue.ActiveAbilityCode,
                        ActiveCastInterval = unitValue.ActiveCastInterval,
                        ActiveCastFrameOffset = unitValue.ActiveCastFrameOffset,
                        CooldownTagIndex = unitValue.CooldownTagIndex,
                        FinisherHealthThreshold = unitValue.FinisherHealthThreshold,
                        PrimaryTargetPolicy = unitValue.PrimaryTargetPolicy,
                        FinisherTargetPolicy = unitValue.FinisherTargetPolicy,
                        Tags = tags[entityIndex],
                        Health = health,
                        Energy = GetAttribute(
                            unitAttributes,
                            unitValue.EnergyAttrSetCode,
                            unitValue.EnergyAttrCode),
                    };

                    if (!CanActivate(source)
                        || !TrySelectCommand(
                            source,
                            TargetCache,
                            Frame,
                            out var abilityCode,
                            out var target,
                            out var policy,
                            out var isFinisher,
                            out var isActive))
                    {
                        continue;
                    }

                    var commandBuffer = abilityCommands[entityIndex];
                    commandBuffer.Add(new AbilityCommandBuffer
                    {
                        Command = new AbilityCommand
                        {
                            Owner = source.Asc,
                            AbilityCode = abilityCode,
                            AbilityEntity = Entity.Null,
                            CommandType = EAbilityCommandType.Activate,
                            TargetAsc = target,
                        },
                    });
                    pendingMask[entityIndex] = true;

                    if (isActive)
                        flushStats.IssuedActive++;
                    else if (isFinisher)
                        flushStats.IssuedFinisher++;
                    else
                        flushStats.IssuedPrimary++;
                    if (policy == AutoChessTargetPolicy.LowestHealth)
                        flushStats.LowestHealthSelections++;
                }

                FlushStats.Value = flushStats;
            }
        }

        [BurstCompile]
        private struct ApplyDriverStatsJob : IJob
        {
            public Entity DriverEntity;
            public AutoChessBattleDriverComponent Driver;
            public int Frame;
            [ReadOnly] public NativeReference<AutoChessBattleDecisionStats> Stats;
            [ReadOnly] public NativeReference<AutoChessBattleFlushStats> FlushStats;
            public ComponentLookup<AutoChessBattleDriverComponent> DriverLookup;

            public void Execute()
            {
                var stats = Stats.Value;
                var flushStats = FlushStats.Value;
                Driver.LastDecisionFrame = Frame;
                Driver.LastOutcomeFrame = Frame;
                Driver.PlayerAliveCount = stats.PlayerAliveCount;
                Driver.EnemyAliveCount = stats.EnemyAliveCount;
                Driver.IssuedCommandCount += flushStats.IssuedPrimary + flushStats.IssuedFinisher;
                Driver.IssuedCommandCount += flushStats.IssuedActive;
                Driver.IssuedPrimaryCommandCount += flushStats.IssuedPrimary;
                Driver.IssuedFinisherCommandCount += flushStats.IssuedFinisher;
                Driver.IssuedActiveCommandCount += flushStats.IssuedActive;
                Driver.LowestHealthTargetCount += flushStats.LowestHealthSelections;
                DriverLookup[DriverEntity] = Driver;
            }
        }

        private static bool TrySelectCommand(
            in AutoChessBattleUnitTargetStateRecord source,
            NativeParallelHashMap<int, AutoChessBattleGroupTargetCacheRecord> targetCache,
            int frame,
            out int abilityCode,
            out Entity target,
            out AutoChessTargetPolicy policy,
            out bool isFinisher,
            out bool isActive)
        {
            abilityCode = 0;
            target = Entity.Null;
            policy = AutoChessTargetPolicy.Frontline;
            isFinisher = false;
            isActive = false;

            if (IsActiveAbilityDue(source, frame)
                && TryFindAliveEnemy(source, targetCache, source.PrimaryTargetPolicy, out var activeTarget))
            {
                abilityCode = source.ActiveAbilityCode;
                target = activeTarget.Asc;
                policy = source.PrimaryTargetPolicy;
                isActive = true;
                return true;
            }

            if (source.FinisherAbilityCode > 0
                && TryFindAliveEnemy(source, targetCache, source.FinisherTargetPolicy, out var finisherTarget)
                && finisherTarget.Health <= source.FinisherHealthThreshold)
            {
                abilityCode = source.FinisherAbilityCode;
                target = finisherTarget.Asc;
                policy = source.FinisherTargetPolicy;
                isFinisher = true;
                return true;
            }

            if (source.PrimaryAbilityCode <= 0
                || !TryFindAliveEnemy(source, targetCache, source.PrimaryTargetPolicy, out var primaryTarget))
            {
                return false;
            }

            abilityCode = source.PrimaryAbilityCode;
            target = primaryTarget.Asc;
            policy = source.PrimaryTargetPolicy;
            return true;
        }

        private static bool TryFindAliveEnemy(
            in AutoChessBattleUnitTargetStateRecord source,
            NativeParallelHashMap<int, AutoChessBattleGroupTargetCacheRecord> targetCache,
            AutoChessTargetPolicy policy,
            out AutoChessBattleUnitTargetStateRecord target)
        {
            target = default;
            if (!targetCache.TryGetValue(source.BattleGroup, out var cache))
                return false;

            if (source.Team == AutoChessTeam.Player)
                return TryResolveCachedTarget(in cache.Enemy, policy, out target);
            if (source.Team == AutoChessTeam.Enemy)
                return TryResolveCachedTarget(in cache.Player, policy, out target);

            return false;
        }

        private static void BuildTargetCache(
            NativeArray<AutoChessBattleUnitTargetStateRecord> units,
            NativeParallelHashMap<int, AutoChessBattleGroupTargetCacheRecord> targetCache)
        {
            targetCache.Clear();
            for (var i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                if (unit.Asc == Entity.Null
                    || unit.Health <= 0f)
                {
                    continue;
                }

                targetCache.TryGetValue(unit.BattleGroup, out var cache);
                if (unit.Team == AutoChessTeam.Player)
                    UpdateTeamTargetCache(ref cache.Player, in unit);
                else if (unit.Team == AutoChessTeam.Enemy)
                    UpdateTeamTargetCache(ref cache.Enemy, in unit);
                targetCache[unit.BattleGroup] = cache;
            }
        }

        private static void UpdateTeamTargetCache(
            ref AutoChessTeamTargetCacheRecord cache,
            in AutoChessBattleUnitTargetStateRecord unit)
        {
            if (cache.HasFrontline == 0
                || IsBetterTarget(unit, cache.Frontline, AutoChessTargetPolicy.Frontline))
            {
                cache.Frontline = unit;
                cache.HasFrontline = 1;
            }

            if (cache.HasLowestHealth == 0
                || IsBetterTarget(unit, cache.LowestHealth, AutoChessTargetPolicy.LowestHealth))
            {
                cache.LowestHealth = unit;
                cache.HasLowestHealth = 1;
            }
        }

        private static bool TryResolveCachedTarget(
            in AutoChessTeamTargetCacheRecord cache,
            AutoChessTargetPolicy policy,
            out AutoChessBattleUnitTargetStateRecord target)
        {
            if (policy == AutoChessTargetPolicy.LowestHealth && cache.HasLowestHealth != 0)
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
            in AutoChessBattleUnitTargetStateRecord candidate,
            in AutoChessBattleUnitTargetStateRecord current,
            AutoChessTargetPolicy policy)
        {
            if (policy == AutoChessTargetPolicy.LowestHealth)
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

        private static bool CanActivate(in AutoChessBattleUnitTargetStateRecord source)
        {
            return source.Asc != Entity.Null
                   && source.Energy >= 1f
                   && !HasDenseTag(source.Tags, source.CooldownTagIndex);
        }

        private static bool IsActiveAbilityDue(
            in AutoChessBattleUnitTargetStateRecord source,
            int frame)
        {
            if (source.ActiveAbilityCode <= 0 || source.ActiveCastInterval <= 0)
                return false;

            var normalizedFrame = frame + source.ActiveCastFrameOffset;
            return normalizedFrame >= 0 && normalizedFrame % source.ActiveCastInterval == 0;
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

        private struct AutoChessBattleGroupTargetCacheRecord
        {
            public AutoChessTeamTargetCacheRecord Player;
            public AutoChessTeamTargetCacheRecord Enemy;
        }

        private struct AutoChessTeamTargetCacheRecord
        {
            public byte HasFrontline;
            public byte HasLowestHealth;
            public AutoChessBattleUnitTargetStateRecord Frontline;
            public AutoChessBattleUnitTargetStateRecord LowestHealth;
        }

        private struct AutoChessBattleDecisionStats
        {
            public int PlayerAliveCount;
            public int EnemyAliveCount;
        }

        private struct AutoChessBattleFlushStats
        {
            public int IssuedPrimary;
            public int IssuedFinisher;
            public int IssuedActive;
            public int LowestHealthSelections;
        }
    }

}
