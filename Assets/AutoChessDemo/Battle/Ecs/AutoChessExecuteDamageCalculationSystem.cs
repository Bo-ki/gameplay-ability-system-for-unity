using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GEExecutionCalculationExtensionSystemGroup))]
    public partial struct AutoChessExecuteDamageCalculationSystem : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _calculationQuery;
        private EntityQuery _ownerCommandQuery;

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
            _ownerCommandQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<ASCIdentityComponent>(),
                    ComponentType.ReadOnly<GEEffectCommandBuffer>(),
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
            state.RequireForUpdate(_ownerCommandQuery);
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
            state.Dependency = new ExecuteDamageCalculationChunkJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                CommandTypeHandle = SystemAPI.GetBufferTypeHandle<GEEffectCommandBuffer>(isReadOnly: true),
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
                DriverLookup = SystemAPI.GetComponentLookup<AutoChessBattleDriverComponent>(),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
            }.Schedule(_ownerCommandQuery, state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct ExecuteDamageCalculationChunkJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public BufferTypeHandle<GEEffectCommandBuffer> CommandTypeHandle;
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
            public ComponentLookup<AutoChessBattleDriverComponent> DriverLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                    return;

                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var commands = chunk.GetBufferAccessor(ref CommandTypeHandle);
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
                    var ownerCommands = commands[entityIndex];
                    if (ownerCommands.Length <= 0)
                        continue;

                    var ownerAttributes = attributes[entityIndex];
                    var targetDeltas = deltas[entityIndex];
                    var targetFacts = hasOwnerFactBuffer ? ownerFacts[entityIndex] : default;
                    for (var i = 0; i < ownerCommands.Length; i++)
                    {
                        var command = ownerCommands[i];
                        var targetAsc = ResolveTargetAsc(in command, owner);
                        if (command.GameplayEffectCode != Calculation.GameplayEffectCode
                            || CompareEntity(targetAsc, owner) != 0)
                        {
                            continue;
                        }

                        if (!TryEvaluateExecuteDamage(
                                ownerAttributes,
                                in Calculation,
                                out var damage))
                        {
                            continue;
                        }

                        var commandFrame = command.Frame != 0 ? command.Frame : Frame;
                        var deltaSequence = Allocate(ref stream.NextDeltaSequence);
                        var factSequence = Allocate(ref stream.NextFactSequence);
                        targetDeltas.Add(new AttributeModifierBuffer
                        {
                            Sequence = deltaSequence,
                            SourceCommandSequence = command.Sequence,
                            Frame = commandFrame,
                            SourceAsc = command.SourceAsc,
                            TargetAsc = targetAsc,
                            SourceAbility = command.SourceAbility,
                            SourceEffect = command.SourceEffect,
                            GameplayEffectCode = command.GameplayEffectCode,
                            ContextId = command.ContextId,
                            ParentContextId = command.ParentContextId,
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

                        if (!hasOwnerFactBuffer)
                            continue;

                        targetFacts.Add(new OwnerLocalGameplayFactBuffer
                        {
                            Fact = new GameplayEventBuffer
                            {
                                Sequence = factSequence,
                                SourceCommandSequence = command.Sequence,
                                SourceDeltaSequence = deltaSequence,
                                Frame = commandFrame,
                                EventType = EGameplayEventType.ExecutionCalculationOutputUpdated,
                                Domain = EGameplayFactDomain.ExecutionCalculation,
                                Category = EGameplayFactCategory.StateChange,
                                Severity = EGameplayFactSeverity.Info,
                                SourceAsc = command.SourceAsc,
                                TargetAsc = targetAsc,
                                SourceAbility = command.SourceAbility,
                                SourceEffect = command.SourceEffect,
                                GameplayEffectCode = command.GameplayEffectCode,
                                ContextId = command.ContextId,
                                ParentContextId = command.ParentContextId,
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

                Driver.LastExecutionFrame = Frame;
                DriverLookup[DriverEntity] = Driver;
            }
        }

        private static Entity ResolveTargetAsc(in GEEffectCommandBuffer command, Entity owner)
        {
            return command.TargetAsc != Entity.Null ? command.TargetAsc : owner;
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
            out float damage)
        {
            damage = 0f;

            var attrIndex = IndexOfAttribute(
                attributes,
                calculation.HealthAttrSetCode,
                calculation.HealthAttrCode);
            if (attrIndex < 0)
                return false;

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
