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
        public int OwnerLocalSpecCount;
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

    [InternalBufferCapacity(4)]
    public struct ActiveEffectNextFrameMutationCommandBuffer : IBufferElementData
    {
        public GEEffectCommandBuffer Command;
    }

    [InternalBufferCapacity(8)]
    public struct ActiveEffectNextFrameMutationSetByCallerValueBuffer : IBufferElementData
    {
        public GESetByCallerValueBuffer Value;
    }

    [InternalBufferCapacity(4)]
    public struct OwnerLocalInstantNextFrameCommandBuffer : IBufferElementData
    {
        public GEEffectCommandBuffer Command;
    }

    [InternalBufferCapacity(8)]
    public struct OwnerLocalInstantNextFrameSetByCallerValueBuffer : IBufferElementData
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
            private int _currentFrame;
            private bool _isCreated;

            internal CommandWriter(
                EntityManager em,
                Entity streamEntity,
                GEEffectCommandStreamComponent stream,
                int currentFrame)
            {
                _em = em;
                _streamEntity = streamEntity;
                _stream = stream;
                _currentFrame = currentFrame;
                _isCreated = true;
            }

            public bool IsCreated => _isCreated;
            public Entity StreamEntity => _streamEntity;
            public int CurrentFrame => _currentFrame;

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

            public GEEffectCommandBuffer AppendOwnerLocalInstantCommand(
                in GEEffectCommandBuffer command,
                DynamicBuffer<GEEffectCommandBuffer> ownerCommands,
                DynamicBuffer<GESetByCallerValueBuffer> ownerSetByCallerValues,
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
                ownerCommands.Add(resolved);
                return resolved;
            }

            public GEEffectCommandBuffer AppendOwnerLocalInstantCommand(
                in GEEffectCommandBuffer command,
                DynamicBuffer<GEEffectCommandBuffer> ownerCommands,
                DynamicBuffer<GESetByCallerValueBuffer> ownerSetByCallerValues,
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
                ownerCommands.Add(resolved);
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
            private int _currentFrame;
            private bool _isCreated;

            internal GameplayEventWriter(
                EntityManager em,
                Entity streamEntity,
                GEEffectCommandStreamComponent stream,
                int currentFrame)
            {
                _em = em;
                _streamEntity = streamEntity;
                _stream = stream;
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
                var owner = ResolveFactOwner(in resolved);
                if (owner == Entity.Null
                    || !_em.Exists(owner)
                    || !_em.HasBuffer<OwnerLocalGameplayFactBuffer>(owner))
                {
                    return default;
                }

                if (resolved.Sequence <= 0)
                    resolved.Sequence = Allocate(ref _stream.NextFactSequence);
                if (resolved.Frame <= 0)
                    resolved.Frame = _currentFrame;

                _em.GetBuffer<OwnerLocalGameplayFactBuffer>(owner).Add(new OwnerLocalGameplayFactBuffer
                {
                    Fact = resolved,
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

            private static Entity ResolveFactOwner(in GameplayEventBuffer fact)
            {
                if (fact.TargetAsc != Entity.Null)
                    return fact.TargetAsc;
                if (fact.SourceAsc != Entity.Null)
                    return fact.SourceAsc;
                return Entity.Null;
            }
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

            return em.HasComponent<GEEffectCommandStreamComponent>(streamEntity);
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
            return new CommandWriter(
                em,
                streamEntity,
                stream,
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
            return new GameplayEventWriter(
                em,
                streamEntity,
                stream,
                currentFrame);
        }

        public static void ClearFrameLocalData(EntityManager em, Entity streamEntity, int frame)
        {
            if (streamEntity == Entity.Null || !em.Exists(streamEntity))
                return;

            if (!HasRequiredBuffers(em, streamEntity))
                return;

            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            stream.LastClearedFrame = frame;
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

            stream.LastClearedFrame = frame;
            ResetFrameLocalCounters(ref stream);
            em.SetComponentData(streamEntity, stream);
        }

        public static void PrepareFrameLocalData(
            ref GEEffectCommandStreamComponent stream,
            int frame)
        {
            if (stream.LastClearedFrame == frame)
                return;

            stream.LastClearedFrame = frame;
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
            stream.OwnerLocalSpecCount = 0;
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
            DynamicBuffer<GESetByCallerValueBuffer> target,
            IReadOnlyList<GESetByCallerRequestValueBuffer> source,
            int commandSequence)
        {
            if (source == null)
                return;

            for (var i = 0; i < source.Count; i++)
            {
                var value = source[i];
                target.Add(new GESetByCallerValueBuffer
                {
                    CommandSequence = commandSequence,
                    SpecSequence = 0,
                    Key = value.Key,
                    Value = value.Value,
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

        private static void CopyRequestSetByCallerValues(
            DynamicBuffer<GESetByCallerValueBuffer> target,
            DynamicBuffer<GESetByCallerRequestValueBuffer> source,
            int commandSequence)
        {
            if (!source.IsCreated)
                return;

            for (var i = 0; i < source.Length; i++)
            {
                var value = source[i];
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
