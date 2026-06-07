using System.Collections.Generic;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum GEEffectCommandKind : byte
    {
        Instant = 0,
        ActiveMutation = 1,
    }

    public enum GEEffectCommandSource : byte
    {
        Unknown = 0,
        Ability = 1,
        Period = 3,
        Passive = 4,
        RuntimeBoundary = 5,
        Overflow = 7,
    }

    public enum ActiveEffectMutationKind : byte
    {
        Apply = 0,
        Refresh = 1,
        Stack = 2,
        PeriodTick = 3,
        Remove = 4,
        GrantedState = 5,
    }

    public enum AttributeDeltaValueKind : byte
    {
        CurrentValue = 0,
        BaseValue = 1,
    }

    public static class AttributeModifierBufferFlags
    {
        public const int RequiresCoreApply = 1 << 0;
        public const int AppliedByCore = 1 << 1;

        public static bool RequiresApply(int flags)
        {
            return (flags & RequiresCoreApply) != 0
                   && (flags & AppliedByCore) == 0;
        }

        public static bool ShouldProjectFact(int flags)
        {
            return (flags & RequiresCoreApply) == 0
                   || (flags & AppliedByCore) != 0;
        }
    }

    public struct GEEffectCommandStreamComponent : IComponentData
    {
        public int Version;
        public int NextContextId;
        public int NextCommandSequence;
        public int NextSpecSequence;
        public int NextDeltaSequence;
        public int NextFactSequence;
        public int LastClearedFrame;
        public int SpecBuildCommandCursor;
        public int DeltaApplySpecCursor;
        public int FactProjectionDeltaCursor;
        public int EventBridgeFactCursor;
        public int CueProjectionSpecCursor;
        public int ActiveMutationCommandCount;
        public int ActiveMutationOwnerGroupCount;
        public int ActiveMutationMaxOwnerRange;
        public int ActiveMutationSortMoveCount;
        public int ActiveMutationEstimatedRandomLookupCount;
        public int ActiveMutationOwnerResourceLookupCount;
        public int ActiveMutationMigrationCarrierCount;
        public int PendingAttributeDeltaCount;
        public int PendingAttributeAppliedDeltaCount;
        public int PendingAttributeSkippedDeltaCount;
        public int PendingAttributeTargetGroupCount;
        public int PendingAttributeMaxTargetRange;
        public int PendingAttributeEstimatedRandomLookupCount;
        public int PendingAttributeFactPatchCount;
        public int PendingAttributeMigrationCarrierCount;
        public int OwnerLocalFactCount;
        public int OwnerLocalFactOwnerGroupCount;
        public int OwnerLocalFactMaxOwnerRange;
        public int OwnerLocalFactFlushCount;
        public int MagnitudeSourceCurrentValueLookupCount;
        public int MagnitudeSourceCapturedValueHitCount;
        public int MagnitudeSourceCaptureMissCount;
        public int MagnitudeSourceCaptureMissLiveLookupCount;
        public int MagnitudeSourceFallbackValueCount;
        public int MagnitudeSourceFallbackFactCount;
        public int MagnitudeSourceSourceAttributeLookupCount;
        public int MagnitudeSourceTargetAttributeLookupCount;
        public int MagnitudeSourceExecutionInputLookupCount;
        public int ActiveEffectSlotSourceSnapshotCapacity;
        public int ActiveEffectSlotSourceSnapshotGatherAttemptCount;
        public int ActiveEffectSlotSourceSnapshotWriteCount;
        public int ActiveEffectSlotSourceSnapshotWriteFailureCount;
        public int ActiveEffectSlotSourceSnapshotAttributeMissCount;
        public int ActiveEffectSlotSourceSnapshotApplyHitCount;
        public int ActiveEffectSlotSourceSnapshotApplyMissCount;
        public int ActiveEffectSlotSourceSnapshotFallbackCount;
        public int ActiveEffectSlotSourceSnapshotCapacityPressureCount;
        public int ActiveEffectSlotSourceSnapshotSpillCount;
    }

    [InternalBufferCapacity(0)]
    public struct GEEffectCommandBuffer : IBufferElementData
    {
        public int Sequence;
        public int Frame;
        public GEEffectCommandKind Kind;
        public GEEffectCommandSource Source;
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

    [InternalBufferCapacity(0)]
    public struct GESetByCallerValueBuffer : IBufferElementData
    {
        public int CommandSequence;
        public int SpecSequence;
        public int Key;
        public float Value;
    }

    [InternalBufferCapacity(0)]
    public struct GEEffectSpecBuffer : IBufferElementData
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

    [InternalBufferCapacity(0)]
    public struct AttributeModifierBuffer : IBufferElementData
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
        public AttributeDeltaValueKind ValueKind;
        public float Magnitude;
        public float OldValue;
        public float NewValue;
        public int Flags;
    }

    [InternalBufferCapacity(0)]
    public struct ActiveEffectMutationBuffer : IBufferElementData
    {
        public int Sequence;
        public int SourceCommandSequence;
        public int Frame;
        public ActiveEffectMutationKind Kind;
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

    [InternalBufferCapacity(4)]
    public struct ActiveEffectMutationCommandBuffer : IBufferElementData
    {
        public GEEffectCommandBuffer Command;
    }

    [InternalBufferCapacity(8)]
    public struct ActiveEffectMutationSetByCallerValueBuffer : IBufferElementData
    {
        public GESetByCallerValueBuffer Value;
    }

    [InternalBufferCapacity(0)]
    public struct GameplayEventBuffer : IBufferElementData
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

    [InternalBufferCapacity(8)]
    public struct OwnerLocalGameplayFactBuffer : IBufferElementData
    {
        public GameplayEventBuffer Fact;
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
            private GEEffectCommandStreamComponent _stream;
            private DynamicBuffer<GEEffectCommandBuffer> _commands;
            private DynamicBuffer<GESetByCallerValueBuffer> _setByCallerBuffer;
            private int _currentFrame;
            private bool _isCreated;

            internal CommandWriter(
                EntityManager em,
                Entity streamEntity,
                GEEffectCommandStreamComponent stream,
                DynamicBuffer<GEEffectCommandBuffer> commands,
                DynamicBuffer<GESetByCallerValueBuffer> setByCallerBuffer,
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

            public GEEffectCommandBuffer AppendCommand(in GEEffectCommandBuffer command)
            {
                return AppendCommand(command, null);
            }

            public GEEffectCommandBuffer AppendCommand(
                in GEEffectCommandBuffer command,
                IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
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
                        _setByCallerBuffer.Add(new GESetByCallerValueBuffer
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

            public GEEffectCommandBuffer AppendCommand(
                in GEEffectCommandBuffer command,
                DynamicBuffer<GESetByCallerRequestValueBuffer> setByCallerValues)
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
                        _setByCallerBuffer.Add(new GESetByCallerValueBuffer
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

            public GEEffectCommandBuffer AppendCommand(
                in GEEffectCommandBuffer command,
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerValues,
                int sourceSequence,
                int sourceGameplayEffectCode)
            {
                if (!_isCreated)
                    return default;

                var setByCallerCount = CountSetByCallerValues(
                    setByCallerValues,
                    sourceSequence,
                    sourceGameplayEffectCode);
                var resolved = PrepareCommand(
                    ref _stream,
                    _setByCallerBuffer,
                    command,
                    setByCallerCount,
                    _currentFrame);

                CopySetByCallerValues(
                    _setByCallerBuffer,
                    setByCallerValues,
                    sourceSequence,
                    sourceGameplayEffectCode,
                    resolved.Sequence);

                _commands.Add(resolved);
                return resolved;
            }

            public GEEffectCommandBuffer AppendOwnerLocalActiveMutationCommand(
                in GEEffectCommandBuffer command,
                DynamicBuffer<ActiveEffectMutationCommandBuffer> ownerCommands,
                DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> ownerSetByCallerValues,
                IReadOnlyList<GESetByCallerRequestValueBuffer> setByCallerValues)
            {
                if (!_isCreated
                    || !ownerCommands.IsCreated
                    || !ownerSetByCallerValues.IsCreated)
                {
                    return default;
                }

                var resolved = PrepareCommand(
                    ref _stream,
                    ownerSetByCallerValues.Length,
                    command,
                    setByCallerValues?.Count ?? 0,
                    _currentFrame);

                CopyRequestSetByCallerValues(ownerSetByCallerValues, setByCallerValues, resolved.Sequence);
                ownerCommands.Add(new ActiveEffectMutationCommandBuffer
                {
                    Command = resolved,
                });
                return resolved;
            }

            public GEEffectCommandBuffer AppendOwnerLocalActiveMutationCommand(
                in GEEffectCommandBuffer command,
                DynamicBuffer<ActiveEffectMutationCommandBuffer> ownerCommands,
                DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> ownerSetByCallerValues,
                DynamicBuffer<GESetByCallerRequestValueBuffer> setByCallerValues)
            {
                if (!_isCreated
                    || !ownerCommands.IsCreated
                    || !ownerSetByCallerValues.IsCreated)
                {
                    return default;
                }

                var setByCallerCount = setByCallerValues.IsCreated ? setByCallerValues.Length : 0;
                var resolved = PrepareCommand(
                    ref _stream,
                    ownerSetByCallerValues.Length,
                    command,
                    setByCallerCount,
                    _currentFrame);

                CopyRequestSetByCallerValues(ownerSetByCallerValues, setByCallerValues, resolved.Sequence);
                ownerCommands.Add(new ActiveEffectMutationCommandBuffer
                {
                    Command = resolved,
                });
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

        public struct GameplayEventWriter
        {
            private EntityManager _em;
            private Entity _streamEntity;
            private GEEffectCommandStreamComponent _stream;
            private DynamicBuffer<GameplayEventBuffer> _facts;
            private int _currentFrame;
            private bool _isCreated;

            internal GameplayEventWriter(
                EntityManager em,
                Entity streamEntity,
                GEEffectCommandStreamComponent stream,
                DynamicBuffer<GameplayEventBuffer> facts,
                int currentFrame)
            {
                _em = em;
                _streamEntity = streamEntity;
                _stream = stream;
                _facts = facts;
                _currentFrame = currentFrame;
                _isCreated = true;
            }

            public bool IsCreated => _isCreated;

            public Entity StreamEntity => _streamEntity;

            public int CurrentFrame => _currentFrame;

            public GameplayEventBuffer AppendGameplayEvent(in GameplayEventBuffer fact)
            {
                if (!_isCreated)
                    return default;

                var resolved = fact;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = Allocate(ref _stream.NextFactSequence);
                if (resolved.Frame <= 0)
                    resolved.Frame = _currentFrame;

                _facts.Add(resolved);
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
            public GEEffectCommandBuffer Command;
        }

        public static Entity EnsureSingleton(EntityManager em)
        {
            if (TryGetSingleton(em, out var streamEntity))
            {
                return streamEntity;
            }

            streamEntity = em.CreateEntity(GASRuntimeEntityArchetypes.EffectCommandStream(em));
            em.SetComponentData(streamEntity, new GEEffectCommandStreamComponent
            {
                Version = CurrentVersion,
                NextContextId = 1,
                NextCommandSequence = 1,
                NextSpecSequence = 1,
                NextDeltaSequence = 1,
                NextFactSequence = 1,
            });
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

        public static void ResetKnownSingleton(EntityManager em)
        {
            if (_hasCachedStreamOwner && _cachedEntityManager.Equals(em))
            {
                _cachedEntityManager = default;
                _cachedStreamEntity = Entity.Null;
                _hasCachedStreamOwner = false;
            }
        }

        public static bool HasRequiredBuffers(EntityManager em, Entity streamEntity)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                return false;

            return em.HasBuffer<GEEffectCommandBuffer>(streamEntity)
                   && em.HasBuffer<GESetByCallerValueBuffer>(streamEntity)
                   && em.HasBuffer<GEEffectSpecBuffer>(streamEntity)
                   && em.HasBuffer<GameplayEventBuffer>(streamEntity);
        }

        public static void EnsureBuffers(EntityManager em, Entity streamEntity)
        {
            // Bootstrap creates stream owners with GASRuntimeEntityArchetypes.EffectCommandStream.
            // Runtime lanes only validate the layout; they must not add buffers lazily.
            _ = HasRequiredBuffers(em, streamEntity);
        }

        private static bool TryResolveKnownSingleton(EntityManager em, out Entity streamEntity)
        {
            if (TryResolveCachedSingleton(em, out streamEntity))
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

        private static bool IsValidStreamOwner(EntityManager em, Entity streamEntity)
        {
            return streamEntity != Entity.Null
                   && em.World != null
                   && em.World.IsCreated
                   && em.Exists(streamEntity)
                   && em.HasComponent<GEEffectCommandStreamComponent>(streamEntity);
        }

        public static CommandWriter BeginCommandWriter(
            EntityManager em,
            Entity streamEntity,
            int currentFrame)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                return default;
            if (!HasRequiredBuffers(em, streamEntity))
                return default;

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            var setByCallerBuffer = em.GetBuffer<GESetByCallerValueBuffer>(streamEntity);
            return new CommandWriter(
                em,
                streamEntity,
                stream,
                commands,
                setByCallerBuffer,
                currentFrame);
        }

        public static GameplayEventWriter BeginGameplayEventWriter(
            EntityManager em,
            Entity streamEntity,
            int currentFrame)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                return default;
            if (!HasRequiredBuffers(em, streamEntity))
                return default;

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var facts = em.GetBuffer<GameplayEventBuffer>(streamEntity);
            return new GameplayEventWriter(
                em,
                streamEntity,
                stream,
                facts,
                currentFrame);
        }

        public static GEEffectCommandBuffer AppendPreparedCommand(
            ref GEEffectCommandStreamComponent stream,
            DynamicBuffer<GEEffectCommandBuffer> commands,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerBuffer,
            in GEEffectCommandBuffer command,
            int currentFrame)
        {
            var resolved = PrepareCommand(
                ref stream,
                setByCallerBuffer,
                command,
                setByCallerCount: 0,
                currentFrame);
            commands.Add(resolved);
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

            if (streamEntity == Entity.Null || !em.Exists(streamEntity) || !HasRequiredBuffers(em, streamEntity))
                return 0;

            var sorted = new List<ParallelCommandFanInRecord>(records.Count);
            for (var i = 0; i < records.Count; i++)
                sorted.Add(records[i]);
            SortParallelCommandFanInRecords(sorted);

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            var setByCallerBuffer = em.GetBuffer<GESetByCallerValueBuffer>(streamEntity);

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

            if (!HasRequiredBuffers(em, streamEntity))
                return;
            em.GetBuffer<GEEffectCommandBuffer>(streamEntity).Clear();
            em.GetBuffer<GESetByCallerValueBuffer>(streamEntity).Clear();
            em.GetBuffer<GEEffectSpecBuffer>(streamEntity).Clear();
            em.GetBuffer<GameplayEventBuffer>(streamEntity).Clear();

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            stream.LastClearedFrame = frame;
            stream.SpecBuildCommandCursor = 0;
            stream.DeltaApplySpecCursor = 0;
            stream.FactProjectionDeltaCursor = 0;
            stream.EventBridgeFactCursor = 0;
            stream.CueProjectionSpecCursor = 0;
            ResetFrameLocalCounters(ref stream);
            em.SetComponentData(streamEntity, stream);
        }

        public static void PrepareFrameLocalData(EntityManager em, Entity streamEntity, int frame)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                return;

            if (!HasRequiredBuffers(em, streamEntity))
                return;
            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            if (stream.LastClearedFrame == frame)
                return;

            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            var setByCallerValues = em.GetBuffer<GESetByCallerValueBuffer>(streamEntity);
            CompactConsumedCommands(
                commands,
                setByCallerValues,
                ClampCursor(stream.SpecBuildCommandCursor, commands.Length));

            em.GetBuffer<GEEffectSpecBuffer>(streamEntity).Clear();
            em.GetBuffer<GameplayEventBuffer>(streamEntity).Clear();

            stream.LastClearedFrame = frame;
            stream.SpecBuildCommandCursor = 0;
            stream.DeltaApplySpecCursor = 0;
            stream.FactProjectionDeltaCursor = 0;
            stream.EventBridgeFactCursor = 0;
            stream.CueProjectionSpecCursor = 0;
            ResetFrameLocalCounters(ref stream);
            em.SetComponentData(streamEntity, stream);
        }

        public static void PrepareFrameLocalData(
            ref GEEffectCommandStreamComponent stream,
            DynamicBuffer<GEEffectCommandBuffer> commands,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            DynamicBuffer<GEEffectSpecBuffer> specs,
            DynamicBuffer<GameplayEventBuffer> facts,
            int frame)
        {
            if (stream.LastClearedFrame == frame)
                return;

            CompactConsumedCommands(
                commands,
                setByCallerValues,
                ClampCursor(stream.SpecBuildCommandCursor, commands.Length));

            specs.Clear();
            facts.Clear();

            stream.LastClearedFrame = frame;
            stream.SpecBuildCommandCursor = 0;
            stream.DeltaApplySpecCursor = 0;
            stream.FactProjectionDeltaCursor = 0;
            stream.EventBridgeFactCursor = 0;
            stream.CueProjectionSpecCursor = 0;
            ResetFrameLocalCounters(ref stream);
        }

        private static void ResetFrameLocalCounters(ref GEEffectCommandStreamComponent stream)
        {
            stream.ActiveMutationCommandCount = 0;
            stream.ActiveMutationOwnerGroupCount = 0;
            stream.ActiveMutationMaxOwnerRange = 0;
            stream.ActiveMutationSortMoveCount = 0;
            stream.ActiveMutationEstimatedRandomLookupCount = 0;
            stream.ActiveMutationOwnerResourceLookupCount = 0;
            stream.ActiveMutationMigrationCarrierCount = 0;
            stream.PendingAttributeDeltaCount = 0;
            stream.PendingAttributeAppliedDeltaCount = 0;
            stream.PendingAttributeSkippedDeltaCount = 0;
            stream.PendingAttributeTargetGroupCount = 0;
            stream.PendingAttributeMaxTargetRange = 0;
            stream.PendingAttributeEstimatedRandomLookupCount = 0;
            stream.PendingAttributeFactPatchCount = 0;
            stream.PendingAttributeMigrationCarrierCount = 0;
            stream.OwnerLocalFactCount = 0;
            stream.OwnerLocalFactOwnerGroupCount = 0;
            stream.OwnerLocalFactMaxOwnerRange = 0;
            stream.OwnerLocalFactFlushCount = 0;
            stream.MagnitudeSourceCurrentValueLookupCount = 0;
            stream.MagnitudeSourceCapturedValueHitCount = 0;
            stream.MagnitudeSourceCaptureMissCount = 0;
            stream.MagnitudeSourceCaptureMissLiveLookupCount = 0;
            stream.MagnitudeSourceFallbackValueCount = 0;
            stream.MagnitudeSourceFallbackFactCount = 0;
            stream.MagnitudeSourceSourceAttributeLookupCount = 0;
            stream.MagnitudeSourceTargetAttributeLookupCount = 0;
            stream.MagnitudeSourceExecutionInputLookupCount = 0;
            stream.ActiveEffectSlotSourceSnapshotCapacity = 0;
            stream.ActiveEffectSlotSourceSnapshotGatherAttemptCount = 0;
            stream.ActiveEffectSlotSourceSnapshotWriteCount = 0;
            stream.ActiveEffectSlotSourceSnapshotWriteFailureCount = 0;
            stream.ActiveEffectSlotSourceSnapshotAttributeMissCount = 0;
            stream.ActiveEffectSlotSourceSnapshotApplyHitCount = 0;
            stream.ActiveEffectSlotSourceSnapshotApplyMissCount = 0;
            stream.ActiveEffectSlotSourceSnapshotFallbackCount = 0;
            stream.ActiveEffectSlotSourceSnapshotCapacityPressureCount = 0;
            stream.ActiveEffectSlotSourceSnapshotSpillCount = 0;
        }

        public static void AddMagnitudeSourceCounters(
            EntityManager em,
            Entity streamEntity,
            int currentValueLookups,
            int capturedValueHits,
            int captureMisses,
            int captureMissLiveLookups,
            int fallbackValues,
            int fallbackFacts,
            int sourceAttributeLookups,
            int targetAttributeLookups,
            int executionInputLookups)
        {
            if (streamEntity == Entity.Null
                || !em.Exists(streamEntity)
                || !em.HasComponent<GEEffectCommandStreamComponent>(streamEntity))
            {
                return;
            }

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            AddMagnitudeSourceCounters(
                ref stream,
                currentValueLookups,
                capturedValueHits,
                captureMisses,
                captureMissLiveLookups,
                fallbackValues,
                fallbackFacts,
                sourceAttributeLookups,
                targetAttributeLookups,
                executionInputLookups);
            em.SetComponentData(streamEntity, stream);
        }

        public static void AddMagnitudeSourceCounters(
            ref GEEffectCommandStreamComponent stream,
            int currentValueLookups,
            int capturedValueHits,
            int captureMisses,
            int captureMissLiveLookups,
            int fallbackValues,
            int fallbackFacts,
            int sourceAttributeLookups,
            int targetAttributeLookups,
            int executionInputLookups)
        {
            stream.MagnitudeSourceCurrentValueLookupCount += currentValueLookups;
            stream.MagnitudeSourceCapturedValueHitCount += capturedValueHits;
            stream.MagnitudeSourceCaptureMissCount += captureMisses;
            stream.MagnitudeSourceCaptureMissLiveLookupCount += captureMissLiveLookups;
            stream.MagnitudeSourceFallbackValueCount += fallbackValues;
            stream.MagnitudeSourceFallbackFactCount += fallbackFacts;
            stream.MagnitudeSourceSourceAttributeLookupCount += sourceAttributeLookups;
            stream.MagnitudeSourceTargetAttributeLookupCount += targetAttributeLookups;
            stream.MagnitudeSourceExecutionInputLookupCount += executionInputLookups;
        }

        public static void SetActiveEffectSlotSourceSnapshotCapacity(
            ref GEEffectCommandStreamComponent stream,
            int capacity)
        {
            stream.ActiveEffectSlotSourceSnapshotCapacity = capacity < 0 ? 0 : capacity;
        }

        public static void AddActiveEffectSlotSourceSnapshotCounters(
            ref GEEffectCommandStreamComponent stream,
            int gatherAttempts,
            int snapshotWrites,
            int snapshotWriteFailures,
            int attributeMisses,
            int applyHits,
            int applyMisses,
            int fallbackValues,
            int capacityPressure,
            int spillCount)
        {
            stream.ActiveEffectSlotSourceSnapshotGatherAttemptCount += gatherAttempts;
            stream.ActiveEffectSlotSourceSnapshotWriteCount += snapshotWrites;
            stream.ActiveEffectSlotSourceSnapshotWriteFailureCount += snapshotWriteFailures;
            stream.ActiveEffectSlotSourceSnapshotAttributeMissCount += attributeMisses;
            stream.ActiveEffectSlotSourceSnapshotApplyHitCount += applyHits;
            stream.ActiveEffectSlotSourceSnapshotApplyMissCount += applyMisses;
            stream.ActiveEffectSlotSourceSnapshotFallbackCount += fallbackValues;
            stream.ActiveEffectSlotSourceSnapshotCapacityPressureCount += capacityPressure;
            stream.ActiveEffectSlotSourceSnapshotSpillCount += spillCount;
        }

        public static GEEffectCommandBuffer ToCommand(
            in GEApplyRequestComponent request,
            Entity targetAsc,
            ETargetDataKind targetDataKind,
            GEEffectCommandSource source)
        {
            return new GEEffectCommandBuffer
            {
                Kind = request.DurationFrameOverride > 0
                    ? GEEffectCommandKind.ActiveMutation
                    : GEEffectCommandKind.Instant,
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

        private static GEEffectCommandBuffer PrepareCommand(
            ref GEEffectCommandStreamComponent stream,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerBuffer,
            in GEEffectCommandBuffer command,
            int setByCallerCount,
            int currentFrame)
        {
            return PrepareCommand(
                ref stream,
                setByCallerBuffer.Length,
                command,
                setByCallerCount,
                currentFrame);
        }

        private static GEEffectCommandBuffer PrepareCommand(
            ref GEEffectCommandStreamComponent stream,
            int setByCallerStart,
            in GEEffectCommandBuffer command,
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

            resolved.SetByCallerStart = setByCallerStart;
            resolved.SetByCallerCount = setByCallerCount;
            return resolved;
        }

        private static void CopyRequestSetByCallerValues(
            DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> target,
            IReadOnlyList<GESetByCallerRequestValueBuffer> source,
            int commandSequence)
        {
            if (source == null)
                return;

            for (var i = 0; i < source.Count; i++)
            {
                var value = source[i];
                target.Add(new ActiveEffectMutationSetByCallerValueBuffer
                {
                    Value = new GESetByCallerValueBuffer
                    {
                        CommandSequence = commandSequence,
                        SpecSequence = 0,
                        Key = value.Key,
                        Value = value.Value,
                    },
                });
            }
        }

        private static void CopyRequestSetByCallerValues(
            DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> target,
            DynamicBuffer<GESetByCallerRequestValueBuffer> source,
            int commandSequence)
        {
            if (!source.IsCreated)
                return;

            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                target.Add(new ActiveEffectMutationSetByCallerValueBuffer
                {
                    Value = new GESetByCallerValueBuffer
                    {
                        CommandSequence = commandSequence,
                        SpecSequence = 0,
                        Key = value.Key,
                        Value = value.Value,
                    },
                });
            }
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
            DynamicBuffer<GEEffectCommandBuffer> commands,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
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

        private static int CountSetByCallerValues(
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerValues,
            int sourceSequence,
            int sourceGameplayEffectCode)
        {
            if (!setByCallerValues.IsCreated
                || sourceSequence <= 0
                || sourceGameplayEffectCode <= 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < setByCallerValues.Length; i++)
            {
                var value = setByCallerValues[i];
                if (value.SourceSequence == sourceSequence
                    && value.SourceGameplayEffectCode == sourceGameplayEffectCode)
                {
                    count++;
                }
            }

            return count;
        }

        private static void CopySetByCallerValues(
            DynamicBuffer<GESetByCallerValueBuffer> target,
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> source,
            int sourceSequence,
            int sourceGameplayEffectCode,
            int commandSequence)
        {
            if (!source.IsCreated
                || sourceSequence <= 0
                || sourceGameplayEffectCode <= 0)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
                if (value.SourceSequence != sourceSequence
                    || value.SourceGameplayEffectCode != sourceGameplayEffectCode)
                {
                    continue;
                }

                target.Add(new GESetByCallerValueBuffer
                {
                    CommandSequence = commandSequence,
                    SpecSequence = 0,
                    Key = value.Key,
                    Value = value.Value,
                });
            }
        }

    }
}
