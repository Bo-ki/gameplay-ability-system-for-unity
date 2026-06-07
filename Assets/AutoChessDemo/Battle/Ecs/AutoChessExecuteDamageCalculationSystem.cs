using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using GAS.Runtime;
using GAS.Runtime.Generated;

namespace GAS.AutoChessDemo
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEEffectSpecBuildSystem))]
    [UpdateBefore(typeof(GASAttributeModifierDeltaApplySystem))]
    public partial struct AutoChessExecuteDamageCalculationSystem : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _calculationQuery;
        private EntityQuery _ownerSpecQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AutoChessBattleDriverComponent>(),
                },
            });
            _calculationQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AutoChessExecuteDamageCalculationComponent>(),
                },
            });
            _ownerSpecQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                    ComponentType.ReadOnly<GEEffectSpecBuffer>(),
                    ComponentType.ReadWrite<AttributeModifierBuffer>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadWrite<PendingAttributeModifierComponent>(),
                    ComponentType.ReadOnly<ASCDestroyingComponent>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });

            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_calculationQuery);
            state.RequireForUpdate(_ownerSpecQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var driverEntity = _driverQuery.GetSingletonEntity();
            var driver = SystemAPI.GetComponent<AutoChessBattleDriverComponent>(driverEntity);
            if (!driver.Enabled)
                return;

            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            if (driver.LastExecutionFrame == frame)
                return;

            var calculation = _calculationQuery.GetSingleton<AutoChessExecuteDamageCalculationComponent>();
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            var stats = new NativeReference<ExecutionCalculationStats>(Allocator.TempJob);
            stats.Value = default;
            var calculationHandle = new ExecuteDamageCalculationChunkJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                SpecTypeHandle = SystemAPI.GetBufferTypeHandle<GEEffectSpecBuffer>(isReadOnly: true),
                DeltaBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeModifierBuffer>(),
                AttributeBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: true),
                OwnerFactBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(isReadOnly: false),
                PendingOwnerTypeHandle =
                    SystemAPI.GetComponentTypeHandle<PendingAttributeModifierComponent>(isReadOnly: false),
                DestroyingTypeHandle = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),
                DriverEntity = driverEntity,
                Driver = driver,
                Calculation = calculation,
                StreamEntity = streamEntity,
                Frame = frame,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                Stats = stats,
            }.Schedule(_ownerSpecQuery, state.Dependency);
            var applyStatsHandle = new ApplyExecutionCalculationStatsJob
            {
                DriverEntity = driverEntity,
                Driver = driver,
                Frame = frame,
                Stats = stats,
                DriverLookup = SystemAPI.GetComponentLookup<AutoChessBattleDriverComponent>(),
            }.Schedule(calculationHandle);
            state.Dependency = stats.Dispose(applyStatsHandle);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct ExecuteDamageCalculationChunkJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public BufferTypeHandle<GEEffectSpecBuffer> SpecTypeHandle;
            public BufferTypeHandle<AttributeModifierBuffer> DeltaBufferTypeHandle;
            [ReadOnly] public BufferTypeHandle<AttributeValueBuffer> AttributeBufferTypeHandle;
            public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactBufferTypeHandle;
            public ComponentTypeHandle<PendingAttributeModifierComponent> PendingOwnerTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ASCDestroyingComponent> DestroyingTypeHandle;
            public Entity DriverEntity;
            public AutoChessBattleDriverComponent Driver;
            public AutoChessExecuteDamageCalculationComponent Calculation;
            public Entity StreamEntity;
            public int Frame;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public NativeReference<ExecutionCalculationStats> Stats;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                    return;

                var stats = default(ExecutionCalculationStats);
                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var specs = chunk.GetBufferAccessor(ref SpecTypeHandle);
                var deltas = chunk.GetBufferAccessor(ref DeltaBufferTypeHandle);
                var attributes = chunk.GetBufferAccessor(ref AttributeBufferTypeHandle);
                var pendingOwners = chunk.GetNativeArray(ref PendingOwnerTypeHandle);
                var pendingMask = chunk.GetEnabledMask(ref PendingOwnerTypeHandle);
                var destroyingMask = chunk.GetEnabledMask(ref DestroyingTypeHandle);
                var hasOwnerFactBuffer = chunk.Has(ref OwnerFactBufferTypeHandle);
                var ownerFacts = hasOwnerFactBuffer
                    ? chunk.GetBufferAccessor(ref OwnerFactBufferTypeHandle)
                    : default;
                var stream = StreamLookup[StreamEntity];
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (destroyingMask[entityIndex])
                        continue;

                    var owner = entities[entityIndex];
                    var ownerSpecs = specs[entityIndex];
                    if (ownerSpecs.Length <= 0)
                        continue;

                    var ownerAttributes = attributes[entityIndex];
                    var targetDeltas = deltas[entityIndex];
                    var targetFacts = hasOwnerFactBuffer ? ownerFacts[entityIndex] : default;
                    for (var i = 0; i < ownerSpecs.Length; i++)
                    {
                        var spec = ownerSpecs[i];
                        stats.SpecScanCount++;
                        if (spec.GameplayEffectCode != Calculation.GameplayEffectCode)
                            continue;

                        stats.MatchedEffectSpecCount++;
                        var targetAsc = ResolveTargetAsc(in spec, owner);
                        if (CompareEntity(targetAsc, owner) != 0)
                        {
                            stats.TargetOwnerMismatchCount++;
                            continue;
                        }

                        if (!TryEvaluateExecuteDamage(
                                ownerAttributes,
                                in Calculation,
                                out var damage,
                                out var rejectReason))
                        {
                            if (rejectReason == ExecuteDamageRejectReason.MissingAttribute)
                                stats.MissingAttributeCount++;
                            else if (rejectReason == ExecuteDamageRejectReason.EvaluatorRejected)
                                stats.EvaluatorRejectCount++;
                            continue;
                        }

                        var commandFrame = spec.Frame != 0 ? spec.Frame : Frame;
                        var deltaSequence = Allocate(ref stream.NextDeltaSequence);
                        var factSequence = Allocate(ref stream.NextFactSequence);
                        targetDeltas.Add(new AttributeModifierBuffer
                        {
                            Sequence = deltaSequence,
                            SourceCommandSequence = spec.SourceCommandSequence,
                            SourceSpecSequence = spec.Sequence,
                            Frame = commandFrame,
                            SourceAsc = spec.SourceAsc,
                            TargetAsc = targetAsc,
                            SourceAbility = spec.SourceAbility,
                            SourceEffect = spec.SourceEffect,
                            GameplayEffectCode = spec.GameplayEffectCode,
                            ContextId = spec.ContextId,
                            ParentContextId = spec.ParentContextId,
                            AttrSetCode = Calculation.HealthAttrSetCode,
                            AttributeCode = Calculation.HealthAttrCode,
                            Op = EModifierOp.Subtract,
                            ValueKind = AttributeDeltaValueKind.BaseValue,
                            Magnitude = damage,
                            Flags = AttributeModifierBufferFlags.RequiresCoreApply,
                        });
                        pendingOwners[entityIndex] = new PendingAttributeModifierComponent
                        {
                            LastWriteFrame = commandFrame,
                            PendingCount = targetDeltas.Length,
                        };
                        pendingMask[entityIndex] = true;
                        stats.OutputWriteCount++;

                        if (!hasOwnerFactBuffer)
                            continue;

                        targetFacts.Add(new OwnerLocalGameplayFactBuffer
                        {
                            Fact = new GameplayEventBuffer
                            {
                                Sequence = factSequence,
                                SourceCommandSequence = spec.SourceCommandSequence,
                                SourceSpecSequence = spec.Sequence,
                                SourceDeltaSequence = deltaSequence,
                                Frame = commandFrame,
                                EventType = EGameplayEventType.ExecutionCalculationOutputUpdated,
                                Domain = EGameplayFactDomain.ExecutionCalculation,
                                Category = EGameplayFactCategory.StateChange,
                                Severity = EGameplayFactSeverity.Info,
                                SourceAsc = spec.SourceAsc,
                                TargetAsc = targetAsc,
                                SourceAbility = spec.SourceAbility,
                                SourceEffect = spec.SourceEffect,
                                GameplayEffectCode = spec.GameplayEffectCode,
                                ContextId = spec.ContextId,
                                ParentContextId = spec.ParentContextId,
                                EventCode = Calculation.CalculationCode,
                                AttrSetCode = Calculation.HealthAttrSetCode,
                                AttributeCode = Calculation.HealthAttrCode,
                                ReasonCode = Calculation.OutputKey,
                                Value = damage,
                            },
                        });
                    }
                }

                StreamLookup[StreamEntity] = stream;
                AddStats(stats);
            }

            private void AddStats(in ExecutionCalculationStats value)
            {
                var stats = Stats.Value;
                stats.SpecScanCount += value.SpecScanCount;
                stats.MatchedEffectSpecCount += value.MatchedEffectSpecCount;
                stats.TargetOwnerMismatchCount += value.TargetOwnerMismatchCount;
                stats.MissingAttributeCount += value.MissingAttributeCount;
                stats.EvaluatorRejectCount += value.EvaluatorRejectCount;
                stats.OutputWriteCount += value.OutputWriteCount;
                Stats.Value = stats;
            }
        }

        [BurstCompile]
        private struct ApplyExecutionCalculationStatsJob : IJob
        {
            public Entity DriverEntity;
            public AutoChessBattleDriverComponent Driver;
            public int Frame;
            [ReadOnly] public NativeReference<ExecutionCalculationStats> Stats;
            public ComponentLookup<AutoChessBattleDriverComponent> DriverLookup;

            public void Execute()
            {
                var stats = Stats.Value;
                Driver.LastExecutionFrame = Frame;
                Driver.ExecutionSpecScanCount += stats.SpecScanCount;
                Driver.ExecutionMatchedEffectSpecCount += stats.MatchedEffectSpecCount;
                Driver.ExecutionTargetOwnerMismatchCount += stats.TargetOwnerMismatchCount;
                Driver.ExecutionMissingAttributeCount += stats.MissingAttributeCount;
                Driver.ExecutionEvaluatorRejectCount += stats.EvaluatorRejectCount;
                Driver.ExecutionOutputWriteCount += stats.OutputWriteCount;
                DriverLookup[DriverEntity] = Driver;
            }
        }

        private struct ExecutionCalculationStats
        {
            public int SpecScanCount;
            public int MatchedEffectSpecCount;
            public int TargetOwnerMismatchCount;
            public int MissingAttributeCount;
            public int EvaluatorRejectCount;
            public int OutputWriteCount;
        }

        private enum ExecuteDamageRejectReason : byte
        {
            None = 0,
            MissingAttribute = 1,
            EvaluatorRejected = 2,
        }

        private static Entity ResolveTargetAsc(in GEEffectSpecBuffer spec, Entity owner)
        {
            return spec.TargetAsc != Entity.Null ? spec.TargetAsc : owner;
        }

        private static int CompareEntity(Entity left, Entity right)
        {
            var result = left.Index.CompareTo(right.Index);
            if (result != 0)
                return result;

            return left.Version.CompareTo(right.Version);
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }
        private static bool TryEvaluateExecuteDamage(
            DynamicBuffer<AttributeValueBuffer> attributes,
            in AutoChessExecuteDamageCalculationComponent calculation,
            out float damage,
            out ExecuteDamageRejectReason rejectReason)
        {
            damage = 0f;
            rejectReason = ExecuteDamageRejectReason.None;

            var attrIndex = IndexOfAttribute(
                attributes,
                calculation.HealthAttrSetCode,
                calculation.HealthAttrCode);
            if (attrIndex < 0)
            {
                rejectReason = ExecuteDamageRejectReason.MissingAttribute;
                return false;
            }

            var attribute = attributes[attrIndex];
            if (!AutoChessGeneratedExecutionEvaluator.TryEvaluateExecuteDamage(
                    attribute.BaseValue,
                    attribute.MaxValue,
                    attribute.IsClampMax,
                    calculation.BaseDamage,
                    calculation.MissingHealthCoefficient,
                    calculation.MinDamage,
                    calculation.MaxDamage,
                    out var output))
            {
                rejectReason = ExecuteDamageRejectReason.EvaluatorRejected;
                return false;
            }

            damage = output.Damage;
            return true;
        }

        private static int IndexOfAttribute(
            DynamicBuffer<AttributeValueBuffer> attributes,
            int attrSetCode,
            int attrCode)
        {
            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.AttrSetCode == attrSetCode && attribute.Code == attrCode)
                    return i;
            }

            return -1;
        }
    }
}
