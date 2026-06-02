using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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

            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;
            var eventBusEntity = SystemAPI.GetSingletonEntity<GameplayEventBusComponent>();

            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            var damageDeltas = new NativeList<AutoChessExecuteDamageDeltaRecord>(Allocator.Temp);
            var attributesByAsc = SystemAPI.GetBufferLookup<AttributeValueBuffer>();
            var destroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true);

            try
            {
                CollectDamageDeltas(
                    commands,
                    attributesByAsc,
                    destroyingLookup,
                    frame,
                    damageDeltas);
                AppendDamageDeltasAndEvents(em, streamEntity, eventBusEntity, damageDeltas.AsArray());

                driver.LastExecutionFrame = frame;
                SystemAPI.SetComponent(driverEntity, driver);
            }
            finally
            {
                damageDeltas.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void CollectDamageDeltas(
            DynamicBuffer<GEEffectCommandBuffer> commands,
            BufferLookup<AttributeValueBuffer> attributesByAsc,
            ComponentLookup<ASCDestroyingComponent> destroyingLookup,
            int frame,
            NativeList<AutoChessExecuteDamageDeltaRecord> damageDeltas)
        {
            for (var i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.GameplayEffectCode != AutoChessBattleRules.GameplayEffectPlayerExecute
                    || command.TargetAsc == Entity.Null)
                {
                    continue;
                }

                var targetAsc = command.TargetAsc;
                if (!attributesByAsc.HasBuffer(targetAsc)
                    || IsDestroying(destroyingLookup, targetAsc))
                {
                    continue;
                }

                var attributes = attributesByAsc[targetAsc];
                if (!ApplyExecuteDamage(attributes, in command, out var oldValue, out var newValue, out var damage))
                    continue;

                damageDeltas.Add(new AutoChessExecuteDamageDeltaRecord
                {
                    CommandSequence = command.Sequence,
                    Frame = command.Frame != 0 ? command.Frame : frame,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = targetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = command.SourceEffect,
                    GameplayEffectCode = command.GameplayEffectCode,
                    ContextId = command.ContextId,
                    ParentContextId = command.ParentContextId,
                    AttrSetCode = AutoChessBattleRules.AttributeSetCombat,
                    AttributeCode = AutoChessBattleRules.AttributeHealth,
                    OutputKey = AutoChessBattleRules.ExecutionCalculationExecuteDamageOutput,
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
            NativeArray<AutoChessExecuteDamageDeltaRecord> damageDeltas)
        {
            if (damageDeltas.Length == 0)
                return;

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
                    AttributeHelper.MarkOwnerChangeEventPending(em, record.TargetAsc);

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
                        EventCode = AutoChessBattleRules.ExecutionCalculationExecuteDamage,
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
                        EventCode = AutoChessBattleRules.ExecutionCalculationExecuteDamage,
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

        private static bool IsDestroying(
            ComponentLookup<ASCDestroyingComponent> destroyingLookup,
            Entity asc)
        {
            return destroyingLookup.HasComponent(asc)
                   && destroyingLookup.IsComponentEnabled(asc);
        }

        private struct AutoChessExecuteDamageDeltaRecord
        {
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
