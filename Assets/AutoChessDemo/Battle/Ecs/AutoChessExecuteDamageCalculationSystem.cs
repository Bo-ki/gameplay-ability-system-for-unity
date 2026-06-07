using Unity.Burst;
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

            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate(_driverQuery);
            state.RequireForUpdate(_calculationQuery);
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
            state.Dependency = new ExecuteDamageCalculationJob
            {
                DriverEntity = driverEntity,
                Driver = driver,
                Calculation = calculation,
                StreamEntity = streamEntity,
                Frame = frame,
                DriverLookup = SystemAPI.GetComponentLookup<AutoChessBattleDriverComponent>(),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: true),
                DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true),
                PendingOwnerLookup = SystemAPI.GetComponentLookup<PendingAttributeModifierComponent>(),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),
            }.Schedule(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct ExecuteDamageCalculationJob : IJob
        {
            public Entity DriverEntity;
            public AutoChessBattleDriverComponent Driver;
            public AutoChessExecuteDamageCalculationComponent Calculation;
            public Entity StreamEntity;
            public int Frame;
            public ComponentLookup<AutoChessBattleDriverComponent> DriverLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<AttributeModifierBuffer> DeltaLookup;
            public BufferLookup<OwnerLocalGameplayFactBuffer> OwnerFactLookup;
            [ReadOnly] public BufferLookup<AttributeValueBuffer> AttributeLookup;
            public ComponentLookup<PendingAttributeModifierComponent> PendingOwnerLookup;
            [ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity))
                    return;

                var stream = StreamLookup[StreamEntity];
                var commands = CommandLookup[StreamEntity];

                for (var i = 0; i < commands.Length; i++)
                {
                    var command = commands[i];
                    if (command.GameplayEffectCode != Calculation.GameplayEffectCode
                        || command.TargetAsc == Entity.Null)
                    {
                        continue;
                    }

                    var targetAsc = command.TargetAsc;
                    if (!AttributeLookup.HasBuffer(targetAsc)
                        || !DeltaLookup.HasBuffer(targetAsc)
                        || !OwnerFactLookup.HasBuffer(targetAsc)
                        || !PendingOwnerLookup.HasComponent(targetAsc)
                        || IsDestroying(DestroyingLookup, targetAsc))
                    {
                        continue;
                    }

                    var attributes = AttributeLookup[targetAsc];
                    if (!TryEvaluateExecuteDamage(
                            attributes,
                            in Calculation,
                            out var damage))
                    {
                        continue;
                    }

                    var commandFrame = command.Frame != 0 ? command.Frame : Frame;
                    var deltaSequence = Allocate(ref stream.NextDeltaSequence);
                    var factSequence = Allocate(ref stream.NextFactSequence);
                    var targetDeltas = DeltaLookup[targetAsc];
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
                    PendingOwnerLookup[targetAsc] = new PendingAttributeModifierComponent
                    {
                        LastWriteFrame = commandFrame,
                        PendingCount = targetDeltas.Length,
                    };
                    PendingOwnerLookup.SetComponentEnabled(targetAsc, true);

                    OwnerFactLookup[targetAsc].Add(new OwnerLocalGameplayFactBuffer
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

                StreamLookup[StreamEntity] = stream;

                Driver.LastExecutionFrame = Frame;
                DriverLookup[DriverEntity] = Driver;
            }
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }

        private static bool IsDestroying(
            ComponentLookup<ASCDestroyingComponent> destroyingLookup,
            Entity asc)
        {
            return destroyingLookup.HasComponent(asc)
                   && destroyingLookup.IsComponentEnabled(asc);
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
