using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    [UpdateInGroup(typeof(GEExecutionCalculationExtensionSystemGroup))]
    public partial struct AutoBattleExecuteDamageCalculationSystem : ISystem
    {
        private EntityQuery _driverQuery;
        private EntityQuery _targetQuery;

        public void OnCreate(ref SystemState state)
        {
            _driverQuery = SystemAPI.QueryBuilder()
                .WithAll<AutoBattleCommandDriverComponent>()
                .Build();
            _targetQuery = SystemAPI.QueryBuilder()
                .WithAll<AttributeValueBuffer>()
                .WithNone<ASCDestroyingComponent>()
                .Build();

            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GameplayEventBusComponent>();
            state.RequireForUpdate(_driverQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var driverEntity = _driverQuery.GetSingletonEntity();
            var driver = SystemAPI.GetComponent<AutoBattleCommandDriverComponent>(driverEntity);
            if (!driver.Enabled)
                return;

            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            if (driver.LastExecutionFrame == frame)
                return;

            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;
            var eventBusEntity = SystemAPI.GetSingletonEntity<GameplayEventBusComponent>();

            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            var executeCommands = new NativeList<AutoBattleExecuteCommandRecord>(Allocator.Temp);
            var damageDeltas = new NativeList<AutoBattleExecuteDamageDeltaRecord>(Allocator.Temp);

            try
            {
                CollectExecuteCommands(commands, executeCommands);
                if (executeCommands.Length > 0)
                {
                    var commandArray = executeCommands.AsArray();
                    foreach (var (attributes, targetAsc)
                             in SystemAPI.Query<DynamicBuffer<AttributeValueBuffer>>()
                                 .WithNone<ASCDestroyingComponent>()
                                 .WithEntityAccess())
                    {
                        ApplyExecuteDamageCommands(
                            targetAsc,
                            attributes,
                            commandArray,
                            frame,
                            damageDeltas);
                    }

                    AppendDamageDeltasAndEvents(em, streamEntity, eventBusEntity, damageDeltas.AsArray());
                }

                driver.LastExecutionFrame = frame;
                SystemAPI.SetComponent(driverEntity, driver);
            }
            finally
            {
                damageDeltas.Dispose();
                executeCommands.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void CollectExecuteCommands(
            DynamicBuffer<GEEffectCommandBuffer> commands,
            NativeList<AutoBattleExecuteCommandRecord> executeCommands)
        {
            for (var i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.GameplayEffectCode != HeadlessAutoBattleScenario.GameplayEffectPlayerExecute
                    || command.TargetAsc == Entity.Null)
                {
                    continue;
                }

                executeCommands.Add(new AutoBattleExecuteCommandRecord
                {
                    Order = executeCommands.Length,
                    CommandSequence = command.Sequence,
                    Frame = command.Frame,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = command.SourceEffect,
                    GameplayEffectCode = command.GameplayEffectCode,
                    ContextId = command.ContextId,
                    ParentContextId = command.ParentContextId,
                    AttrSetCode = HeadlessAutoBattleScenario.AttributeSetCombat,
                    AttributeCode = HeadlessAutoBattleScenario.AttributeHealth,
                    OutputKey = HeadlessAutoBattleScenario.ExecutionCalculationExecuteDamageOutput,
                    BaseDamage = 10f,
                    MissingHealthCoefficient = 0.5f,
                    MinDamage = 10f,
                    MaxDamage = 36f,
                });
            }
        }

        private static void ApplyExecuteDamageCommands(
            Entity targetAsc,
            DynamicBuffer<AttributeValueBuffer> attributes,
            NativeArray<AutoBattleExecuteCommandRecord> commands,
            int frame,
            NativeList<AutoBattleExecuteDamageDeltaRecord> damageDeltas)
        {
            for (var commandIndex = 0; commandIndex < commands.Length; commandIndex++)
            {
                var command = commands[commandIndex];
                if (command.TargetAsc != targetAsc)
                    continue;

                if (!ApplyExecuteDamage(attributes, in command, out var oldValue, out var newValue, out var damage))
                    continue;

                damageDeltas.Add(new AutoBattleExecuteDamageDeltaRecord
                {
                    Order = command.Order,
                    CommandSequence = command.CommandSequence,
                    Frame = command.Frame != 0 ? command.Frame : frame,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = command.SourceEffect,
                    GameplayEffectCode = command.GameplayEffectCode,
                    ContextId = command.ContextId,
                    ParentContextId = command.ParentContextId,
                    AttrSetCode = command.AttrSetCode,
                    AttributeCode = command.AttributeCode,
                    OutputKey = command.OutputKey,
                    Damage = damage,
                    OldValue = oldValue,
                    NewValue = newValue,
                });
            }
        }

        private static void AppendDamageDeltasAndEvents(
            EntityManager em,
            Entity streamEntity,
            Entity eventBusEntity,
            NativeArray<AutoBattleExecuteDamageDeltaRecord> damageDeltas)
        {
            if (damageDeltas.Length == 0)
                return;

            damageDeltas.Sort(new AutoBattleExecuteDamageDeltaRecordComparer());

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var deltas = em.GetBuffer<AttributeModifierBuffer>(streamEntity);
            var facts = em.GetBuffer<GameplayEventBuffer>(streamEntity);
            var eventWriter = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);

            try
            {
                for (var i = 0; i < damageDeltas.Length; i++)
                {
                    var record = damageDeltas[i];
                    var deltaSequence = Allocate(ref stream.NextDeltaSequence);
                    var factSequence = Allocate(ref stream.NextFactSequence);
                    deltas.Add(new AttributeModifierBuffer
                    {
                        Sequence = deltaSequence,
                        SourceCommandSequence = record.CommandSequence,
                        Frame = record.Frame,
                        SourceAsc = record.SourceAsc,
                        TargetAsc = record.TargetAsc,
                        SourceAbility = record.SourceAbility,
                        SourceEffect = record.SourceEffect,
                        GameplayEffectCode = record.GameplayEffectCode,
                        ContextId = record.ContextId,
                        ParentContextId = record.ParentContextId,
                        AttrSetCode = record.AttrSetCode,
                        AttributeCode = record.AttributeCode,
                        Op = EModifierOp.Subtract,
                        ValueKind = AttributeDeltaValueKind.BaseValue,
                        Magnitude = record.Damage,
                        OldValue = record.OldValue,
                        NewValue = record.NewValue,
                    });

                    facts.Add(new GameplayEventBuffer
                    {
                        Sequence = factSequence,
                        SourceCommandSequence = record.CommandSequence,
                        SourceDeltaSequence = deltaSequence,
                        Frame = record.Frame,
                        EventType = EGameplayEventType.ExecutionCalculationOutputUpdated,
                        Domain = EGameplayFactDomain.ExecutionCalculation,
                        Category = EGameplayFactCategory.StateChange,
                        Severity = EGameplayFactSeverity.Info,
                        SourceAsc = record.SourceAsc,
                        TargetAsc = record.TargetAsc,
                        SourceAbility = record.SourceAbility,
                        SourceEffect = record.SourceEffect,
                        GameplayEffectCode = record.GameplayEffectCode,
                        ContextId = record.ContextId,
                        ParentContextId = record.ParentContextId,
                        EventCode = HeadlessAutoBattleScenario.ExecutionCalculationExecuteDamage,
                        AttrSetCode = record.AttrSetCode,
                        AttributeCode = record.AttributeCode,
                        ReasonCode = record.OutputKey,
                        Value = record.Damage,
                        OldValue = record.OldValue,
                        NewValue = record.NewValue,
                    });

                    eventWriter.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                    {
                        SourceFactSequence = factSequence,
                        Type = EGameplayEventType.ExecutionCalculationOutputUpdated,
                        SourceAsc = record.SourceAsc,
                        TargetAsc = record.TargetAsc,
                        SourceAbility = record.SourceAbility,
                        GameplayEffect = record.SourceEffect,
                        ContextId = record.ContextId,
                        EventCode = HeadlessAutoBattleScenario.ExecutionCalculationExecuteDamage,
                        ReasonCode = record.OutputKey,
                        Value = record.Damage,
                    });
                }

                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                eventWriter.Dispose();
            }
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }

        private struct AutoBattleExecuteCommandRecord
        {
            public int Order;
            public int CommandSequence;
            public int Frame;
            public Entity SourceAsc;
            public Entity TargetAsc;
            public Entity SourceAbility;
            public Entity SourceEffect;
            public int GameplayEffectCode;
            public int ContextId;
            public int ParentContextId;
            public int AttrSetCode;
            public int AttributeCode;
            public int OutputKey;
            public float BaseDamage;
            public float MissingHealthCoefficient;
            public float MinDamage;
            public float MaxDamage;
        }

        private struct AutoBattleExecuteDamageDeltaRecord
        {
            public int Order;
            public int CommandSequence;
            public int Frame;
            public Entity SourceAsc;
            public Entity TargetAsc;
            public Entity SourceAbility;
            public Entity SourceEffect;
            public int GameplayEffectCode;
            public int ContextId;
            public int ParentContextId;
            public int AttrSetCode;
            public int AttributeCode;
            public int OutputKey;
            public float Damage;
            public float OldValue;
            public float NewValue;
        }

        private static bool ApplyExecuteDamage(
            DynamicBuffer<AttributeValueBuffer> attributes,
            in AutoBattleExecuteCommandRecord command,
            out float oldValue,
            out float newValue,
            out float damage)
        {
            oldValue = 0f;
            newValue = 0f;
            damage = 0f;

            var attrIndex = IndexOfAttribute(attributes, command.AttrSetCode, command.AttributeCode);
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
                command.BaseDamage + missingHealth * command.MissingHealthCoefficient,
                command.MinDamage,
                command.MaxDamage);
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

            attribute.Dirty = true;
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

        private struct AutoBattleExecuteDamageDeltaRecordComparer :
            IComparer<AutoBattleExecuteDamageDeltaRecord>
        {
            public int Compare(
                AutoBattleExecuteDamageDeltaRecord x,
                AutoBattleExecuteDamageDeltaRecord y)
            {
                return x.Order.CompareTo(y.Order);
            }
        }
    }
}
