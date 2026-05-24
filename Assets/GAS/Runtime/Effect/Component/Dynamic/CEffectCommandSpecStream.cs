using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum EEffectCommandKind : byte
    {
        Instant = 0,
        ActiveMutation = 1,
    }

    public enum EEffectCommandSource : byte
    {
        Unknown = 0,
        Ability = 1,
        Timeline = 2,
        Period = 3,
        Passive = 4,
        RuntimeBoundary = 5,
        LegacyRequestBridge = 6,
    }

    public enum EActiveEffectMutationKind : byte
    {
        Apply = 0,
        Refresh = 1,
        Stack = 2,
        PeriodTick = 3,
        Remove = 4,
        GrantedState = 5,
    }

    public enum EAttributeDeltaValueKind : byte
    {
        CurrentValue = 0,
        BaseValue = 1,
    }

    public struct CEffectCommandSpecStream : IComponentData
    {
        public int Version;
        public int NextContextId;
        public int NextCommandSequence;
        public int NextSpecSequence;
        public int NextDeltaSequence;
        public int NextFactSequence;
        public int LastClearedFrame;
        public int SpecBuildCommandCursor;
        public int ActiveMutationCommandCursor;
        public int DeltaApplySpecCursor;
        public int FactProjectionDeltaCursor;
    }

    [InternalBufferCapacity(64)]
    public struct BEffectCommand : IBufferElementData
    {
        public int Sequence;
        public int Frame;
        public EEffectCommandKind Kind;
        public EEffectCommandSource Source;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public Entity Instigator;
        public Entity Causer;
        public int GameplayEffectCode;
        public int Level;
        public int DurationFrameOverride;
        public int ContextId;
        public int ParentContextId;
        public ETargetDataKind TargetDataKind;
        public int SetByCallerStart;
        public int SetByCallerCount;
        public int Flags;
    }

    [InternalBufferCapacity(16)]
    public struct BEffectCommandSetByCallerValue : IBufferElementData
    {
        public int CommandSequence;
        public int SpecSequence;
        public int Key;
        public float Value;
    }

    [InternalBufferCapacity(64)]
    public struct BInstantEffectSpec : IBufferElementData
    {
        public int Sequence;
        public int SourceCommandSequence;
        public int Frame;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public Entity Instigator;
        public Entity Causer;
        public int GameplayEffectCode;
        public int Level;
        public int StackCount;
        public int DurationFrameOverride;
        public int ContextId;
        public int ParentContextId;
        public ETargetDataKind TargetDataKind;
        public int SetByCallerStart;
        public int SetByCallerCount;
        public int Flags;
    }

    [InternalBufferCapacity(128)]
    public struct BAttributeDelta : IBufferElementData
    {
        public int Sequence;
        public int SourceCommandSequence;
        public int SourceSpecSequence;
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
        public EModifierOp Op;
        public EAttributeDeltaValueKind ValueKind;
        public float Magnitude;
        public float OldValue;
        public float NewValue;
        public int Flags;
    }

    [InternalBufferCapacity(32)]
    public struct BActiveEffectMutation : IBufferElementData
    {
        public int Sequence;
        public int SourceCommandSequence;
        public int Frame;
        public EActiveEffectMutationKind Kind;
        public Entity ActiveEffect;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public int GameplayEffectCode;
        public int ContextId;
        public int ParentContextId;
        public int StackCount;
        public int DurationFrameOverride;
        public int PeriodFrame;
        public int Flags;
    }

    [InternalBufferCapacity(128)]
    public struct BTypedSimulationFact : IBufferElementData
    {
        public int Sequence;
        public int SourceCommandSequence;
        public int SourceSpecSequence;
        public int SourceDeltaSequence;
        public int Frame;
        public EGameplayEventType EventType;
        public EGameplayFactDomain Domain;
        public EGameplayFactCategory Category;
        public EGameplayFactSeverity Severity;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public int GameplayEffectCode;
        public int ContextId;
        public int ParentContextId;
        public int AttrSetCode;
        public int AttributeCode;
        public int ReasonCode;
        public float Value;
        public float OldValue;
        public float NewValue;
    }

    public static class EffectCommandSpecStream
    {
        public const int CurrentVersion = 1;

        public static Entity EnsureSingleton(EntityManager em)
        {
            if (TryGetSingleton(em, out var streamEntity))
            {
                EnsureBuffers(em, streamEntity);
                return streamEntity;
            }

            streamEntity = em.CreateEntity();
            em.AddComponentData(streamEntity, new CEffectCommandSpecStream
            {
                Version = CurrentVersion,
                NextContextId = 1,
                NextCommandSequence = 1,
                NextSpecSequence = 1,
                NextDeltaSequence = 1,
                NextFactSequence = 1,
            });
            EnsureBuffers(em, streamEntity);
            return streamEntity;
        }

        public static bool TryGetSingleton(EntityManager em, out Entity streamEntity)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<CEffectCommandSpecStream>());
            if (query.CalculateEntityCount() <= 0)
            {
                streamEntity = Entity.Null;
                return false;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            streamEntity = entities.Length > 0 ? entities[0] : Entity.Null;
            return streamEntity != Entity.Null;
        }

        public static void EnsureBuffers(EntityManager em, Entity streamEntity)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                return;

            if (!em.HasBuffer<BEffectCommand>(streamEntity))
                em.AddBuffer<BEffectCommand>(streamEntity);
            if (!em.HasBuffer<BEffectCommandSetByCallerValue>(streamEntity))
                em.AddBuffer<BEffectCommandSetByCallerValue>(streamEntity);
            if (!em.HasBuffer<BInstantEffectSpec>(streamEntity))
                em.AddBuffer<BInstantEffectSpec>(streamEntity);
            if (!em.HasBuffer<BAttributeDelta>(streamEntity))
                em.AddBuffer<BAttributeDelta>(streamEntity);
            if (!em.HasBuffer<BActiveEffectMutation>(streamEntity))
                em.AddBuffer<BActiveEffectMutation>(streamEntity);
            if (!em.HasBuffer<BTypedSimulationFact>(streamEntity))
                em.AddBuffer<BTypedSimulationFact>(streamEntity);
        }

        public static BEffectCommand AppendCommand(EntityManager em, in BEffectCommand command)
        {
            return AppendCommand(em, command, null);
        }

        public static BEffectCommand AppendCommand(
            EntityManager em,
            in BEffectCommand command,
            IReadOnlyList<BSetByCallerValue> setByCallerValues)
        {
            var streamEntity = EnsureSingleton(em);
            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var commands = em.GetBuffer<BEffectCommand>(streamEntity);
            var setByCallerBuffer = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);

            var resolved = command;
            if (resolved.Sequence <= 0)
                resolved.Sequence = Allocate(ref stream.NextCommandSequence);
            if (resolved.Frame <= 0)
                resolved.Frame = ResolveCurrentFrame(em);
            if (resolved.ContextId <= 0)
                resolved.ContextId = Allocate(ref stream.NextContextId);
            if (resolved.TargetAsc == Entity.Null)
                resolved.TargetAsc = resolved.SourceAsc;
            if (resolved.Instigator == Entity.Null)
                resolved.Instigator = resolved.SourceAsc;
            if (resolved.Causer == Entity.Null)
                resolved.Causer = resolved.SourceAbility;

            resolved.SetByCallerStart = setByCallerBuffer.Length;
            resolved.SetByCallerCount = setByCallerValues?.Count ?? 0;

            if (setByCallerValues != null)
            {
                for (var i = 0; i < setByCallerValues.Count; i++)
                {
                    var value = setByCallerValues[i];
                    setByCallerBuffer.Add(new BEffectCommandSetByCallerValue
                    {
                        CommandSequence = resolved.Sequence,
                        SpecSequence = 0,
                        Key = value.Key,
                        Value = value.Value,
                    });
                }
            }

            commands.Add(resolved);
            em.SetComponentData(streamEntity, stream);
            return resolved;
        }

        public static void ClearFrameLocalData(EntityManager em, Entity streamEntity, int frame)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                return;

            EnsureBuffers(em, streamEntity);
            em.GetBuffer<BEffectCommand>(streamEntity).Clear();
            em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity).Clear();
            em.GetBuffer<BInstantEffectSpec>(streamEntity).Clear();
            em.GetBuffer<BAttributeDelta>(streamEntity).Clear();
            em.GetBuffer<BActiveEffectMutation>(streamEntity).Clear();
            em.GetBuffer<BTypedSimulationFact>(streamEntity).Clear();

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            stream.LastClearedFrame = frame;
            stream.SpecBuildCommandCursor = 0;
            stream.ActiveMutationCommandCursor = 0;
            stream.DeltaApplySpecCursor = 0;
            stream.FactProjectionDeltaCursor = 0;
            em.SetComponentData(streamEntity, stream);
        }

        public static BEffectCommand AppendLegacyRequestBridgeCommand(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            IReadOnlyList<BSetByCallerValue> setByCallerValues = null)
        {
            return AppendCommand(
                em,
                ToCommand(request, targetAsc, targetDataKind, EEffectCommandSource.LegacyRequestBridge),
                setByCallerValues);
        }

        public static BEffectCommand ToCommand(
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            EEffectCommandSource source)
        {
            return new BEffectCommand
            {
                Kind = request.DurationFrameOverride > 0
                    ? EEffectCommandKind.ActiveMutation
                    : EEffectCommandKind.Instant,
                Source = source,
                SourceAsc = request.SourceAsc,
                TargetAsc = targetAsc,
                SourceAbility = request.SourceAbility,
                SourceEffect = request.SourceEffect,
                Instigator = request.Instigator != Entity.Null ? request.Instigator : request.SourceAsc,
                Causer = request.Causer != Entity.Null ? request.Causer : request.SourceAbility,
                GameplayEffectCode = request.GameplayEffectCode,
                Level = request.Level,
                DurationFrameOverride = request.DurationFrameOverride,
                ContextId = 0,
                ParentContextId = request.ParentContextId,
                TargetDataKind = targetDataKind,
            };
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }

        private static int ResolveCurrentFrame(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GlobalTimer>());
            if (query.CalculateEntityCount() <= 0)
                return 0;

            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
                return 0;

            return em.GetComponentData<GlobalTimer>(entities[0]).Frame;
        }
    }
}
