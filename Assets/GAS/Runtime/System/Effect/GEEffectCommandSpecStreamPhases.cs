using Unity.Entities;
using static GAS.Runtime.EffectCommandSpecStreamPhaseUtility;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    public partial struct GEEffectCommandSpecStreamFramePrepareSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            EffectCommandSpecStream.PrepareFrameLocalData(
                em,
                streamEntity,
                GASRuntimeFrameContext.ResolveCurrentFrame(em));
        }
    }

    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(AbilityTryActivateSystem))]
    public partial struct GEEffectCommandIngestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }

    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]
    public partial struct GameplayFactProjectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var deltas = em.GetBuffer<AttributeModifierBuffer>(streamEntity);
            var facts = em.GetBuffer<GameplayEventBuffer>(streamEntity);

            var start = ClampCursor(stream.FactProjectionDeltaCursor, deltas.Length);
            for (var i = start; i < deltas.Length; i++)
            {
                var delta = deltas[i];
                facts.Add(new GameplayEventBuffer
                {
                    Sequence = Allocate(ref stream.NextFactSequence),
                    SourceCommandSequence = delta.SourceCommandSequence,
                    SourceSpecSequence = delta.SourceSpecSequence,
                    SourceDeltaSequence = delta.Sequence,
                    Frame = delta.Frame,
                    EventType = EGameplayEventType.AttributeBaseValueChanged,
                    Domain = EGameplayFactDomain.Attribute,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = delta.SourceAsc,
                    TargetAsc = delta.TargetAsc,
                    SourceAbility = delta.SourceAbility,
                    SourceEffect = delta.SourceEffect,
                    GameplayEffectCode = delta.GameplayEffectCode,
                    ContextId = delta.ContextId,
                    ParentContextId = delta.ParentContextId,
                    AttrSetCode = delta.AttrSetCode,
                    AttributeCode = delta.AttributeCode,
                    Value = delta.Magnitude,
                    OldValue = delta.OldValue,
                    NewValue = delta.NewValue,
                });
            }

            stream.FactProjectionDeltaCursor = deltas.Length;
            em.SetComponentData(streamEntity, stream);
        }
    }

    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GameplayFactProjectionSystem))]
    public partial struct GameplayFactEventBridgeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GameplayEventBusComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasBuffer<AttributeChangeEventBuffer>(eventBusEntity))
                return;

            var eventBusWriter = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            if (!eventBusWriter.IsCreated)
                return;

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var facts = em.GetBuffer<GameplayEventBuffer>(streamEntity);

            try
            {
                var start = ClampCursor(stream.EventBridgeFactCursor, facts.Length);
                for (var i = start; i < facts.Length; i++)
                {
                    var fact = facts[i];
                    if (fact.Domain != EGameplayFactDomain.Attribute
                        || fact.EventType != EGameplayEventType.AttributeBaseValueChanged
                        || fact.TargetAsc == Entity.Null)
                    {
                        continue;
                    }

                    eventBusWriter.EnqueueAttributeChangeEvent(new AttributeChangeEventBuffer
                    {
                        ASC = fact.TargetAsc,
                        SourceAsc = fact.SourceAsc,
                        SourceAbility = fact.SourceAbility,
                        GameplayEffect = fact.SourceEffect,
                        SourceFactSequence = fact.Sequence,
                        EventCode = fact.GameplayEffectCode,
                        AttrSetCode = fact.AttrSetCode,
                        AttributeCode = fact.AttributeCode,
                        OldValue = fact.OldValue,
                        NewValue = fact.NewValue,
                        ContextId = fact.ContextId,
                        IsBaseValue = true,
                    });
                }

                stream.EventBridgeFactCursor = facts.Length;
                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                eventBusWriter.Dispose();
            }
        }
    }

    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GameplayFactEventBridgeSystem))]
    public partial struct GEInstantEffectCueRequestProjectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GameplayEventBusComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasComponent<GameplayEventBusComponent>(eventBusEntity))
                return;

            var eventBusWriter = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            if (!eventBusWriter.IsCreated)
                return;

            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var specs = em.GetBuffer<GEEffectSpecBuffer>(streamEntity);
            var facts = em.GetBuffer<GameplayEventBuffer>(streamEntity);

            try
            {
                var start = ClampCursor(stream.CueProjectionSpecCursor, specs.Length);
                for (var i = start; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (spec.CueRequestOnApplyCode <= 0
                        || spec.TargetAsc == Entity.Null)
                    {
                        continue;
                    }

                    var factSequence = Allocate(ref stream.NextFactSequence);
                    facts.Add(new GameplayEventBuffer
                    {
                        Sequence = factSequence,
                        SourceCommandSequence = spec.SourceCommandSequence,
                        SourceSpecSequence = spec.Sequence,
                        Frame = spec.Frame,
                        EventType = EGameplayEventType.CueRequested,
                        Domain = EGameplayFactDomain.Cue,
                        Category = EGameplayFactCategory.Request,
                        Severity = EGameplayFactSeverity.Info,
                        SourceAsc = spec.SourceAsc,
                        TargetAsc = spec.TargetAsc,
                        SourceAbility = spec.SourceAbility,
                        SourceEffect = spec.SourceEffect,
                        GameplayEffectCode = spec.GameplayEffectCode,
                        ContextId = spec.ContextId,
                        ParentContextId = spec.ParentContextId,
                        EventCode = (int)EGameplayCueEvent.OnApply,
                        ReasonCode = spec.CueRequestOnApplyCode,
                    });

                    eventBusWriter.EnqueueCueRequest(new CueRequestBuffer
                    {
                        TargetAsc = spec.TargetAsc,
                        SourceAsc = spec.SourceAsc,
                        SourceAbility = spec.SourceAbility,
                        GameplayEffect = Entity.Null,
                        SourceEntity = spec.SourceAbility,
                        SourceType = CueSourceType.GameplayEffect,
                        CueEntity = Entity.Null,
                        SourceFactSequence = factSequence,
                        ContextId = spec.ContextId,
                        ReasonCode = spec.CueRequestOnApplyCode,
                        CueEvent = EGameplayCueEvent.OnApply,
                    });

                    eventBusWriter.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                    {
                        SourceFactSequence = factSequence,
                        Type = EGameplayEventType.CueRequested,
                        SourceAsc = spec.SourceAsc,
                        TargetAsc = spec.TargetAsc,
                        SourceAbility = spec.SourceAbility,
                        ContextId = spec.ContextId,
                        EventCode = (int)EGameplayCueEvent.OnApply,
                        ReasonCode = spec.CueRequestOnApplyCode,
                    });
                }

                stream.CueProjectionSpecCursor = specs.Length;
                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                eventBusWriter.Dispose();
            }
        }
    }


    internal static class EffectCommandSpecStreamPhaseUtility
    {
        public static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0)
                return 0;
            return cursor > length ? length : cursor;
        }

        public static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }
    }
}
