using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GEExecutionCalculationExtensionSystemGroup))]
    public partial struct AutoChessExecuteDamageCalculationSystem : ISystem
    {
        private EntityQuery _driverQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<AutoChessBattleDriverComponent>()
                .Build();

            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GameplayEventBusComponent>();
            state.RequireForUpdate(_driverQuery);
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

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            var eventBusEntity = SystemAPI.GetSingletonEntity<GameplayEventBusComponent>();

            state.Dependency = new ExecuteDamageCalculationJob
            {
                DriverEntity = driverEntity,
                Driver = driver,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                DriverLookup = SystemAPI.GetComponentLookup<AutoChessBattleDriverComponent>(),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: true),
                DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(),
                EventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),
                ChangeEventPendingLookup = SystemAPI.GetComponentLookup<AttributeChangeEventPendingComponent>(),
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
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;
            public ComponentLookup<AutoChessBattleDriverComponent> DriverLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<AttributeModifierBuffer> DeltaLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;
            public ComponentLookup<GameplayEventBusComponent> EventBusLookup;
            public BufferLookup<GameplayEventBusEventBuffer> GameplayEventLookup;
            public BufferLookup<AttributeValueBuffer> AttributeLookup;
            [ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;
            public ComponentLookup<AttributeChangeEventPendingComponent> ChangeEventPendingLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !DeltaLookup.HasBuffer(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                    return;

                var stream = StreamLookup[StreamEntity];
                var commands = CommandLookup[StreamEntity];
                var deltas = DeltaLookup[StreamEntity];
                var facts = FactLookup[StreamEntity];
                var canAppendGameplayEvent = EventBusEntity != Entity.Null
                                            && EventBusLookup.HasComponent(EventBusEntity)
                                            && GameplayEventLookup.HasBuffer(EventBusEntity);
                var eventBus = canAppendGameplayEvent
                    ? EventBusLookup[EventBusEntity]
                    : default;
                var gameplayEvents = canAppendGameplayEvent
                    ? GameplayEventLookup[EventBusEntity]
                    : default;

                for (var i = 0; i < commands.Length; i++)
                {
                    var command = commands[i];
                    if (command.GameplayEffectCode != AutoChessBattleRules.GameplayEffectPlayerExecute
                        || command.TargetAsc == Entity.Null)
                    {
                        continue;
                    }

                    var targetAsc = command.TargetAsc;
                    if (!AttributeLookup.HasBuffer(targetAsc)
                        || IsDestroying(DestroyingLookup, targetAsc))
                    {
                        continue;
                    }

                    var attributes = AttributeLookup[targetAsc];
                    if (!ApplyExecuteDamage(attributes, in command, out var oldValue, out var newValue, out var damage))
                        continue;

                    var commandFrame = command.Frame != 0 ? command.Frame : Frame;
                    var deltaSequence = Allocate(ref stream.NextDeltaSequence);
                    var factSequence = Allocate(ref stream.NextFactSequence);
                    deltas.Add(new AttributeModifierBuffer
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
                        AttrSetCode = AutoChessBattleRules.AttributeSetCombat,
                        AttributeCode = AutoChessBattleRules.AttributeHealth,
                        Op = EModifierOp.Subtract,
                        ValueKind = AttributeDeltaValueKind.BaseValue,
                        Magnitude = damage,
                        OldValue = oldValue,
                        NewValue = newValue,
                    });
                    MarkOwnerChangeEventPending(targetAsc);

                    facts.Add(new GameplayEventBuffer
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
                        EventCode = AutoChessBattleRules.ExecutionCalculationExecuteDamage,
                        AttrSetCode = AutoChessBattleRules.AttributeSetCombat,
                        AttributeCode = AutoChessBattleRules.AttributeHealth,
                        ReasonCode = AutoChessBattleRules.ExecutionCalculationExecuteDamageOutput,
                        Value = damage,
                        OldValue = oldValue,
                        NewValue = newValue,
                    });

                    if (!canAppendGameplayEvent)
                        continue;

                    gameplayEvents.Add(new GameplayEventBusEventBuffer
                    {
                        SourceFactSequence = factSequence,
                        Frame = Frame,
                        Sequence = eventBus.NextSequence,
                        Type = EGameplayEventType.ExecutionCalculationOutputUpdated,
                        SourceAsc = command.SourceAsc,
                        TargetAsc = targetAsc,
                        SourceAbility = command.SourceAbility,
                        GameplayEffect = command.SourceEffect,
                        ContextId = command.ContextId,
                        EventCode = AutoChessBattleRules.ExecutionCalculationExecuteDamage,
                        ReasonCode = AutoChessBattleRules.ExecutionCalculationExecuteDamageOutput,
                        Value = damage,
                    });
                    eventBus.NextSequence++;
                }

                StreamLookup[StreamEntity] = stream;
                if (canAppendGameplayEvent)
                    EventBusLookup[EventBusEntity] = eventBus;

                Driver.LastExecutionFrame = Frame;
                DriverLookup[DriverEntity] = Driver;
            }

            private void MarkOwnerChangeEventPending(Entity asc)
            {
                if (ChangeEventPendingLookup.HasComponent(asc))
                    ChangeEventPendingLookup.SetComponentEnabled(asc, true);
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
        private static bool ApplyExecuteDamage(
            DynamicBuffer<AttributeValueBuffer> attributes,
            in GEEffectCommandBuffer command,
            out float oldValue,
            out float newValue,
            out float damage)
        {
            oldValue = 0f;
            newValue = 0f;
            damage = 0f;

            var attrIndex = IndexOfAttribute(
                attributes,
                AutoChessBattleRules.AttributeSetCombat,
                AutoChessBattleRules.AttributeHealth);
            if (attrIndex < 0)
                return false;

            var attribute = attributes[attrIndex];
            oldValue = attribute.BaseValue;
            var oldCurrentValue = attribute.CurrentValue;
            if (oldValue <= 0f)
                return false;

            var maxValue = attribute.IsClampMax ? attribute.MaxValue : oldValue;
            var missingHealth = math.max(0f, maxValue - oldValue);
            damage = math.clamp(
                10f + missingHealth * 0.5f,
                10f,
                36f);
            newValue = oldValue - damage;

            attribute.CurrentValue = newValue;
            Clamp(ref attribute);
            newValue = attribute.CurrentValue;
            attribute.BaseValue = newValue;
            attribute.CurrentValue = newValue;
            if (newValue == oldValue)
            {
                attributes[attrIndex] = attribute;
                return false;
            }

            attribute.Dirty = false;
            if (oldCurrentValue != attribute.CurrentValue)
            {
                attribute.PreviousCurrentValue = oldCurrentValue;
                attribute.CurrentValueChangePending = true;
            }

            attributes[attrIndex] = attribute;
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

        private static void Clamp(ref AttributeValueBuffer attribute)
        {
            if (attribute.IsClampMin)
                attribute.CurrentValue = math.max(attribute.CurrentValue, attribute.MinValue);
            if (attribute.IsClampMax)
                attribute.CurrentValue = math.min(attribute.CurrentValue, attribute.MaxValue);
        }

    }
}
