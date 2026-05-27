using System.Collections.Generic;
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
        Overflow = 7,
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
        public int EventBridgeFactCursor;
        public int CueProjectionSpecCursor;
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
        public int CueRequestOnApplyCode;
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
        public int EventCode;
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

        private static bool _hasCachedStreamOwner;
        private static EntityManager _cachedEntityManager;
        private static Entity _cachedStreamEntity;

        public struct CommandWriter
        {
            private EntityManager _em;
            private Entity _streamEntity;
            private CEffectCommandSpecStream _stream;
            private DynamicBuffer<BEffectCommand> _commands;
            private DynamicBuffer<BEffectCommandSetByCallerValue> _setByCallerBuffer;
            private int _currentFrame;
            private bool _isCreated;

            internal CommandWriter(
                EntityManager em,
                Entity streamEntity,
                CEffectCommandSpecStream stream,
                DynamicBuffer<BEffectCommand> commands,
                DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerBuffer,
                int currentFrame)
            {
                _em = em;
                _streamEntity = streamEntity;
                _stream = stream;
                _commands = commands;
                _setByCallerBuffer = setByCallerBuffer;
                _currentFrame = currentFrame;
                _isCreated = true;
            }

            public bool IsCreated => _isCreated;
            public Entity StreamEntity => _streamEntity;
            public int CurrentFrame => _currentFrame;

            public BEffectCommand AppendCommand(in BEffectCommand command)
            {
                return AppendCommand(command, null);
            }

            public BEffectCommand AppendCommand(
                in BEffectCommand command,
                IReadOnlyList<BSetByCallerValue> setByCallerValues)
            {
                if (!_isCreated)
                    return default;

                var resolved = PrepareCommand(
                    ref _stream,
                    _setByCallerBuffer,
                    command,
                    setByCallerValues?.Count ?? 0,
                    _currentFrame);

                if (setByCallerValues != null)
                {
                    for (var i = 0; i < setByCallerValues.Count; i++)
                    {
                        var value = setByCallerValues[i];
                        _setByCallerBuffer.Add(new BEffectCommandSetByCallerValue
                        {
                            CommandSequence = resolved.Sequence,
                            SpecSequence = 0,
                            Key = value.Key,
                            Value = value.Value,
                        });
                    }
                }

                _commands.Add(resolved);
                return resolved;
            }

            public BEffectCommand AppendCommand(
                in BEffectCommand command,
                DynamicBuffer<BSetByCallerValue> setByCallerValues)
            {
                if (!_isCreated)
                    return default;

                var setByCallerCount = setByCallerValues.IsCreated ? setByCallerValues.Length : 0;
                var resolved = PrepareCommand(
                    ref _stream,
                    _setByCallerBuffer,
                    command,
                    setByCallerCount,
                    _currentFrame);

                if (setByCallerValues.IsCreated)
                {
                    for (var i = 0; i < setByCallerValues.Length; i++)
                    {
                        var value = setByCallerValues[i];
                        _setByCallerBuffer.Add(new BEffectCommandSetByCallerValue
                        {
                            CommandSequence = resolved.Sequence,
                            SpecSequence = 0,
                            Key = value.Key,
                            Value = value.Value,
                        });
                    }
                }

                _commands.Add(resolved);
                return resolved;
            }

            public void Flush()
            {
                if (!_isCreated
                    || _streamEntity == Entity.Null
                    || !_em.Exists(_streamEntity))
                {
                    return;
                }

                _em.SetComponentData(_streamEntity, _stream);
            }
        }

        public struct ParallelCommandFanInRecord
        {
            public int ProducerIndex;
            public int LocalIndex;
            public BEffectCommand Command;
        }

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
            RegisterKnownSingleton(em, streamEntity);
            return streamEntity;
        }

        public static bool TryGetSingleton(EntityManager em, out Entity streamEntity)
        {
            return TryResolveKnownSingleton(em, out streamEntity);
        }

        public static void RegisterKnownSingleton(EntityManager em, Entity streamEntity)
        {
            if (!IsValidStreamOwner(em, streamEntity))
                return;

            _cachedEntityManager = em;
            _cachedStreamEntity = streamEntity;
            _hasCachedStreamOwner = true;
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

        private static bool TryResolveKnownSingleton(EntityManager em, out Entity streamEntity)
        {
            if (TryResolveCachedSingleton(em, out streamEntity))
                return true;

            if (TryResolveGasManagerSingleton(em, out streamEntity))
                return true;

            streamEntity = Entity.Null;
            return false;
        }

        private static bool TryResolveCachedSingleton(EntityManager em, out Entity streamEntity)
        {
            if (!_hasCachedStreamOwner || !_cachedEntityManager.Equals(em))
            {
                streamEntity = Entity.Null;
                return false;
            }

            if (IsValidStreamOwner(em, _cachedStreamEntity))
            {
                streamEntity = _cachedStreamEntity;
                return true;
            }

            _hasCachedStreamOwner = false;
            _cachedStreamEntity = Entity.Null;
            streamEntity = Entity.Null;
            return false;
        }

        private static bool TryResolveGasManagerSingleton(EntityManager em, out Entity streamEntity)
        {
            if (!GASManager.IsInitialized || !GASManager.EntityManager.Equals(em))
            {
                streamEntity = Entity.Null;
                return false;
            }

            streamEntity = GASManager.EntityEffectCommandSpecStream;
            if (!IsValidStreamOwner(em, streamEntity))
            {
                streamEntity = Entity.Null;
                return false;
            }

            RegisterKnownSingleton(em, streamEntity);
            return true;
        }

        private static bool IsValidStreamOwner(EntityManager em, Entity streamEntity)
        {
            return streamEntity != Entity.Null
                   && em.Exists(streamEntity)
                   && em.HasComponent<CEffectCommandSpecStream>(streamEntity);
        }

        public static CommandWriter BeginCommandWriter(EntityManager em)
        {
            var streamEntity = EnsureSingleton(em);
            return BeginCommandWriter(em, streamEntity, GASRuntimeFrameContext.ResolveCurrentFrame(em));
        }

        public static CommandWriter BeginCommandWriter(EntityManager em, int currentFrame)
        {
            var streamEntity = EnsureSingleton(em);
            return BeginCommandWriter(em, streamEntity, currentFrame);
        }

        public static CommandWriter BeginCommandWriter(
            EntityManager em,
            Entity streamEntity,
            int currentFrame)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                streamEntity = EnsureSingleton(em);
            else
                EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var commands = em.GetBuffer<BEffectCommand>(streamEntity);
            var setByCallerBuffer = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);
            return new CommandWriter(
                em,
                streamEntity,
                stream,
                commands,
                setByCallerBuffer,
                currentFrame);
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
            var writer = BeginCommandWriter(em);
            var resolved = writer.AppendCommand(command, setByCallerValues);
            writer.Flush();
            return resolved;
        }

        public static BEffectCommand AppendCommand(
            EntityManager em,
            in BEffectCommand command,
            DynamicBuffer<BSetByCallerValue> setByCallerValues)
        {
            var writer = BeginCommandWriter(em);
            var resolved = writer.AppendCommand(command, setByCallerValues);
            writer.Flush();
            return resolved;
        }

        public static int MergeParallelCommandFanIn(
            EntityManager em,
            Entity streamEntity,
            IReadOnlyList<ParallelCommandFanInRecord> records,
            int currentFrame)
        {
            if (records == null || records.Count == 0)
                return 0;

            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                streamEntity = EnsureSingleton(em);
            else
                EnsureBuffers(em, streamEntity);

            var sorted = new List<ParallelCommandFanInRecord>(records.Count);
            for (var i = 0; i < records.Count; i++)
                sorted.Add(records[i]);
            SortParallelCommandFanInRecords(sorted);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var commands = em.GetBuffer<BEffectCommand>(streamEntity);
            var setByCallerBuffer = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);

            for (var i = 0; i < sorted.Count; i++)
            {
                var resolved = PrepareCommand(
                    ref stream,
                    setByCallerBuffer,
                    sorted[i].Command,
                    setByCallerCount: 0,
                    currentFrame);
                AdvanceAfterExplicitSequence(ref stream.NextCommandSequence, resolved.Sequence);
                AdvanceAfterExplicitSequence(ref stream.NextContextId, resolved.ContextId);
                commands.Add(resolved);
            }

            em.SetComponentData(streamEntity, stream);
            return sorted.Count;
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
            stream.EventBridgeFactCursor = 0;
            stream.CueProjectionSpecCursor = 0;
            em.SetComponentData(streamEntity, stream);
        }

        public static void PrepareFrameLocalData(EntityManager em, Entity streamEntity, int frame)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                return;

            EnsureBuffers(em, streamEntity);
            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            if (stream.LastClearedFrame == frame)
                return;

            var commands = em.GetBuffer<BEffectCommand>(streamEntity);
            var setByCallerValues = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);
            CompactConsumedCommands(
                commands,
                setByCallerValues,
                MinCursor(
                    ClampCursor(stream.SpecBuildCommandCursor, commands.Length),
                    ClampCursor(stream.ActiveMutationCommandCursor, commands.Length)));

            em.GetBuffer<BInstantEffectSpec>(streamEntity).Clear();
            em.GetBuffer<BAttributeDelta>(streamEntity).Clear();
            em.GetBuffer<BActiveEffectMutation>(streamEntity).Clear();
            em.GetBuffer<BTypedSimulationFact>(streamEntity).Clear();

            stream.LastClearedFrame = frame;
            stream.SpecBuildCommandCursor = 0;
            stream.ActiveMutationCommandCursor = 0;
            stream.DeltaApplySpecCursor = 0;
            stream.FactProjectionDeltaCursor = 0;
            stream.EventBridgeFactCursor = 0;
            stream.CueProjectionSpecCursor = 0;
            em.SetComponentData(streamEntity, stream);
        }

        public static BEffectCommand AppendLegacyRequestBridgeCommand(
            EntityManager em,
            in CApplyGameplayEffectRequest request,
            Entity targetAsc,
            ETargetDataKind targetDataKind)
        {
            return ToCommand(request, targetAsc, targetDataKind, EEffectCommandSource.LegacyRequestBridge);
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

        private static BEffectCommand PrepareCommand(
            ref CEffectCommandSpecStream stream,
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerBuffer,
            in BEffectCommand command,
            int setByCallerCount,
            int currentFrame)
        {
            var resolved = command;
            if (resolved.Sequence <= 0)
                resolved.Sequence = Allocate(ref stream.NextCommandSequence);
            if (resolved.Frame <= 0)
                resolved.Frame = currentFrame;
            if (resolved.ContextId <= 0)
                resolved.ContextId = Allocate(ref stream.NextContextId);
            if (resolved.TargetAsc == Entity.Null)
                resolved.TargetAsc = resolved.SourceAsc;
            if (resolved.Instigator == Entity.Null)
                resolved.Instigator = resolved.SourceAsc;
            if (resolved.Causer == Entity.Null)
                resolved.Causer = resolved.SourceAbility;

            resolved.SetByCallerStart = setByCallerBuffer.Length;
            resolved.SetByCallerCount = setByCallerCount;
            return resolved;
        }

        private static void SortParallelCommandFanInRecords(List<ParallelCommandFanInRecord> records)
        {
            for (var i = 1; i < records.Count; i++)
            {
                var value = records[i];
                var j = i - 1;
                while (j >= 0 && CompareParallelCommandFanInRecords(records[j], value) > 0)
                {
                    records[j + 1] = records[j];
                    j--;
                }

                records[j + 1] = value;
            }
        }

        private static int CompareParallelCommandFanInRecords(
            in ParallelCommandFanInRecord left,
            in ParallelCommandFanInRecord right)
        {
            var result = CompareEntity(left.Command.TargetAsc, right.Command.TargetAsc);
            if (result != 0)
                return result;

            result = left.Command.Sequence.CompareTo(right.Command.Sequence);
            if (result != 0)
                return result;

            result = left.ProducerIndex.CompareTo(right.ProducerIndex);
            if (result != 0)
                return result;

            return left.LocalIndex.CompareTo(right.LocalIndex);
        }

        private static int CompareEntity(Entity left, Entity right)
        {
            var result = left.Index.CompareTo(right.Index);
            if (result != 0)
                return result;

            return left.Version.CompareTo(right.Version);
        }

        private static void AdvanceAfterExplicitSequence(ref int next, int value)
        {
            if (next <= value)
                next = value + 1;
        }

        private static void CompactConsumedCommands(
            DynamicBuffer<BEffectCommand> commands,
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues,
            int consumedCommandCount)
        {
            if (consumedCommandCount <= 0)
                return;

            if (consumedCommandCount >= commands.Length)
            {
                commands.Clear();
                setByCallerValues.Clear();
                return;
            }

            var setByCallerDropCount = setByCallerValues.Length;
            var hasKeptSetByCallerRange = false;
            for (var i = consumedCommandCount; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.SetByCallerCount <= 0)
                    continue;

                hasKeptSetByCallerRange = true;
                if (command.SetByCallerStart < setByCallerDropCount)
                    setByCallerDropCount = command.SetByCallerStart;
            }

            commands.RemoveRange(0, consumedCommandCount);
            if (hasKeptSetByCallerRange)
            {
                setByCallerDropCount = ClampCursor(setByCallerDropCount, setByCallerValues.Length);
                if (setByCallerDropCount > 0)
                    setByCallerValues.RemoveRange(0, setByCallerDropCount);
            }
            else
            {
                setByCallerValues.Clear();
                setByCallerDropCount = 0;
            }

            for (var i = 0; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.SetByCallerCount > 0)
                    command.SetByCallerStart -= setByCallerDropCount;
                else
                    command.SetByCallerStart = 0;

                commands[i] = command;
            }
        }

        private static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0)
                return 0;
            return cursor > length ? length : cursor;
        }

        private static int MinCursor(int left, int right)
        {
            return left < right ? left : right;
        }

    }
}
