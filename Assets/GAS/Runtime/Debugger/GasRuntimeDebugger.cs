using System;
using System.Text;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum EGasRuntimeDiagnosticKind : byte
    {
        TickSummary = 0,
        GroupTiming = 1,
        SystemTiming = 2,
        BufferPressure = 3,
        StructuralChange = 4,
    }

    public enum EGasRuntimeDiagnosticSeverity : byte
    {
        Trace = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    public enum EGasRuntimeDiagnosticModule : byte
    {
        Runtime = 0,
        Command = 1,
        ResetDirty = 2,
        Tag = 3,
        Effect = 4,
        Attribute = 5,
        Ability = 6,
        Cue = 7,
        EventBus = 8,
        Presentation = 9,
    }

    public struct CGasRuntimeDebugger : IComponentData
    {
        public byte Enabled;
        public byte CaptureSystemTimings;
        public byte CaptureBufferPressure;
        public int NextSequence;
        public int FirstRetainedSequence;
        public int DroppedEventCount;
        public int MaxRetainedEvents;
        public int SlowTickMicroseconds;
        public int SlowSystemMicroseconds;
        public int BufferPressureWarningPermille;
        public int BufferPressureErrorPermille;
    }

    public struct BGasRuntimeDiagnosticEvent : IBufferElementData
    {
        public int Sequence;
        public int Frame;
        public EGasRuntimeDiagnosticKind Kind;
        public EGasRuntimeDiagnosticSeverity Severity;
        public EGasRuntimeDiagnosticModule Module;
        public FixedString64Bytes GroupName;
        public FixedString64Bytes SystemName;
        public FixedString64Bytes BufferName;
        public Entity Entity;
        public int ElapsedMicroseconds;
        public int TotalMicroseconds;
        public int CallCount;
        public int Count;
        public int Capacity;
        public int ValueA;
        public int ValueB;
        public float Ratio;
    }

    public readonly struct GasRuntimeDiagnosticStats
    {
        public readonly int FirstRetainedSequence;
        public readonly int NextSequence;
        public readonly int DroppedEventCount;
        public readonly int RetainedEventCount;
        public readonly int WarningCount;
        public readonly int ErrorCount;
        public readonly int SlowSystemCount;
        public readonly int BufferPressureWarningCount;

        public GasRuntimeDiagnosticStats(
            int firstRetainedSequence,
            int nextSequence,
            int droppedEventCount,
            int retainedEventCount,
            int warningCount,
            int errorCount,
            int slowSystemCount,
            int bufferPressureWarningCount)
        {
            FirstRetainedSequence = firstRetainedSequence;
            NextSequence = nextSequence;
            DroppedEventCount = droppedEventCount;
            RetainedEventCount = retainedEventCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
            SlowSystemCount = slowSystemCount;
            BufferPressureWarningCount = bufferPressureWarningCount;
        }
    }

    public readonly struct GasRuntimeDiagnosticSnapshot
    {
        public readonly GasRuntimeDiagnosticStats Stats;
        public readonly BGasRuntimeDiagnosticEvent[] Events;

        public GasRuntimeDiagnosticSnapshot(
            in GasRuntimeDiagnosticStats stats,
            BGasRuntimeDiagnosticEvent[] events)
        {
            Stats = stats;
            Events = events ?? Array.Empty<BGasRuntimeDiagnosticEvent>();
        }

        public int EventCount => Events?.Length ?? 0;
    }

    public static class GasRuntimeDebugger
    {
        public const int DefaultDiagnosticCapacity = 4096;
        private const int DefaultSlowTickMicroseconds = 1000;
        private const int DefaultSlowSystemMicroseconds = 100;
        private const int DefaultBufferPressureWarningPermille = 700;
        private const int DefaultBufferPressureErrorPermille = 900;

        public static Entity CreateSingleton(EntityManager em)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, CreateDefaultState());
            em.AddBuffer<BGasRuntimeDiagnosticEvent>(entity).EnsureCapacity(DefaultDiagnosticCapacity);
            em.SetName(entity, "GasRuntimeDebugger");
            return entity;
        }

        public static CGasRuntimeDebugger CreateDefaultState()
        {
            return new CGasRuntimeDebugger
            {
                Enabled = 1,
                CaptureSystemTimings = 0,
                CaptureBufferPressure = 1,
                MaxRetainedEvents = DefaultDiagnosticCapacity,
                SlowTickMicroseconds = DefaultSlowTickMicroseconds,
                SlowSystemMicroseconds = DefaultSlowSystemMicroseconds,
                BufferPressureWarningPermille = DefaultBufferPressureWarningPermille,
                BufferPressureErrorPermille = DefaultBufferPressureErrorPermille,
            };
        }

        public static void Reset(EntityManager em, Entity debuggerEntity)
        {
            if (!CanUse(em, debuggerEntity))
                return;

            var state = em.GetComponentData<CGasRuntimeDebugger>(debuggerEntity);
            state.NextSequence = 0;
            state.FirstRetainedSequence = 0;
            state.DroppedEventCount = 0;
            em.SetComponentData(debuggerEntity, state);
            em.GetBuffer<BGasRuntimeDiagnosticEvent>(debuggerEntity).Clear();
        }

        public static void Configure(
            EntityManager em,
            Entity debuggerEntity,
            bool enabled,
            bool captureSystemTimings,
            bool captureBufferPressure)
        {
            if (!CanUse(em, debuggerEntity))
                return;

            var state = em.GetComponentData<CGasRuntimeDebugger>(debuggerEntity);
            state.Enabled = enabled ? (byte)1 : (byte)0;
            state.CaptureSystemTimings = captureSystemTimings ? (byte)1 : (byte)0;
            state.CaptureBufferPressure = captureBufferPressure ? (byte)1 : (byte)0;
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordTickTiming(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            long totalTicks,
            long commandTicks,
            long resetDirtyTicks,
            long tagTicks,
            long effectTicks,
            long attributeTicks,
            long abilityTicks,
            long cueTicks,
            long stopwatchFrequency)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log))
                return;

            var totalMicroseconds = ToMicroseconds(totalTicks, stopwatchFrequency);
            Append(
                log,
                ref state,
                new BGasRuntimeDiagnosticEvent
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.TickSummary,
                    Severity = SeverityForElapsed(totalMicroseconds, state.SlowTickMicroseconds),
                    Module = EGasRuntimeDiagnosticModule.Runtime,
                    GroupName = "Runtime",
                    ElapsedMicroseconds = totalMicroseconds,
                    TotalMicroseconds = totalMicroseconds,
                });

            AppendGroupTiming(log, ref state, frame, "Command", EGasRuntimeDiagnosticModule.Command, commandTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "ResetDirty", EGasRuntimeDiagnosticModule.ResetDirty, resetDirtyTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Tag", EGasRuntimeDiagnosticModule.Tag, tagTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Effect", EGasRuntimeDiagnosticModule.Effect, effectTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Attribute", EGasRuntimeDiagnosticModule.Attribute, attributeTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Ability", EGasRuntimeDiagnosticModule.Ability, abilityTicks, totalTicks, stopwatchFrequency);
            AppendGroupTiming(log, ref state, frame, "Cue", EGasRuntimeDiagnosticModule.Cue, cueTicks, totalTicks, stopwatchFrequency);
            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordSystemTimingAggregate(
            EntityManager em,
            Entity debuggerEntity,
            int frame,
            string groupName,
            string systemName,
            int callCount,
            long elapsedTicks,
            long stopwatchFrequency)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log)
                || state.CaptureSystemTimings == 0)
            {
                return;
            }

            var totalMicroseconds = ToMicroseconds(elapsedTicks, stopwatchFrequency);
            var averageMicroseconds = callCount > 0 ? totalMicroseconds / callCount : totalMicroseconds;
            Append(
                log,
                ref state,
                new BGasRuntimeDiagnosticEvent
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.SystemTiming,
                    Severity = SeverityForElapsed(averageMicroseconds, state.SlowSystemMicroseconds),
                    Module = ModuleFromGroup(groupName),
                    GroupName = groupName ?? string.Empty,
                    SystemName = systemName ?? string.Empty,
                    ElapsedMicroseconds = averageMicroseconds,
                    TotalMicroseconds = totalMicroseconds,
                    CallCount = callCount,
                });

            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static void RecordEventBusPressure(
            EntityManager em,
            Entity debuggerEntity,
            Entity eventBusEntity,
            int frame)
        {
            if (!TryGetWritableLog(em, debuggerEntity, out var state, out var log)
                || state.CaptureBufferPressure == 0
                || eventBusEntity == Entity.Null
                || !em.Exists(eventBusEntity))
            {
                return;
            }

            RecordBufferPressure<BGameplayEvent>(em, eventBusEntity, "BGameplayEvent", frame, log, ref state);
            RecordBufferPressure<BAttributeChangeEvent>(em, eventBusEntity, "BAttributeChangeEvent", frame, log, ref state);
            RecordBufferPressure<BCueRequest>(em, eventBusEntity, "BCueRequest", frame, log, ref state);
            RecordBufferPressure<BDamageEvent>(em, eventBusEntity, "BDamageEvent", frame, log, ref state);
            RecordBufferPressure<BTagChangeEvent>(em, eventBusEntity, "BTagChangeEvent", frame, log, ref state);
            ApplyRetention(log, ref state);
            em.SetComponentData(debuggerEntity, state);
        }

        public static GasRuntimeDiagnosticSnapshot CreateSnapshot(EntityManager em, Entity debuggerEntity)
        {
            if (!CanUse(em, debuggerEntity))
            {
                return new GasRuntimeDiagnosticSnapshot(
                    new GasRuntimeDiagnosticStats(0, 0, 0, 0, 0, 0, 0, 0),
                    Array.Empty<BGasRuntimeDiagnosticEvent>());
            }

            var state = em.GetComponentData<CGasRuntimeDebugger>(debuggerEntity);
            var log = em.GetBuffer<BGasRuntimeDiagnosticEvent>(debuggerEntity);
            var events = new BGasRuntimeDiagnosticEvent[log.Length];
            var warningCount = 0;
            var errorCount = 0;
            var slowSystemCount = 0;
            var bufferPressureWarningCount = 0;

            for (var i = 0; i < log.Length; i++)
            {
                var evt = log[i];
                events[i] = evt;
                if (evt.Severity >= EGasRuntimeDiagnosticSeverity.Warning)
                    warningCount++;
                if (evt.Severity >= EGasRuntimeDiagnosticSeverity.Error)
                    errorCount++;
                if (evt.Kind == EGasRuntimeDiagnosticKind.SystemTiming
                    && evt.Severity >= EGasRuntimeDiagnosticSeverity.Warning)
                {
                    slowSystemCount++;
                }
                if (evt.Kind == EGasRuntimeDiagnosticKind.BufferPressure
                    && evt.Severity >= EGasRuntimeDiagnosticSeverity.Warning)
                {
                    bufferPressureWarningCount++;
                }
            }

            return new GasRuntimeDiagnosticSnapshot(
                new GasRuntimeDiagnosticStats(
                    state.FirstRetainedSequence,
                    state.NextSequence,
                    state.DroppedEventCount,
                    log.Length,
                    warningCount,
                    errorCount,
                    slowSystemCount,
                    bufferPressureWarningCount),
                events);
        }

        public static string ExportToText(in GasRuntimeDiagnosticSnapshot snapshot, int maxEvents = 0)
        {
            var stats = snapshot.Stats;
            var builder = new StringBuilder(1024);
            builder.Append("runtimeDiagnostics|events=")
                .Append(stats.RetainedEventCount)
                .Append("|dropped=")
                .Append(stats.DroppedEventCount)
                .Append("|warnings=")
                .Append(stats.WarningCount)
                .Append("|errors=")
                .Append(stats.ErrorCount)
                .Append("|slowSystems=")
                .Append(stats.SlowSystemCount)
                .Append("|bufferPressureWarnings=")
                .Append(stats.BufferPressureWarningCount)
                .AppendLine();

            var events = snapshot.Events ?? Array.Empty<BGasRuntimeDiagnosticEvent>();
            var count = maxEvents > 0 && maxEvents < events.Length ? maxEvents : events.Length;
            for (var i = 0; i < count; i++)
                AppendEventLine(builder, events[i]);

            return builder.ToString();
        }

        private static bool CanUse(EntityManager em, Entity debuggerEntity)
        {
            return debuggerEntity != Entity.Null
                   && em.Exists(debuggerEntity)
                   && em.HasComponent<CGasRuntimeDebugger>(debuggerEntity)
                   && em.HasBuffer<BGasRuntimeDiagnosticEvent>(debuggerEntity);
        }

        private static bool TryGetWritableLog(
            EntityManager em,
            Entity debuggerEntity,
            out CGasRuntimeDebugger state,
            out DynamicBuffer<BGasRuntimeDiagnosticEvent> log)
        {
            state = default;
            log = default;
            if (!CanUse(em, debuggerEntity))
                return false;

            state = em.GetComponentData<CGasRuntimeDebugger>(debuggerEntity);
            if (state.Enabled == 0)
                return false;

            log = em.GetBuffer<BGasRuntimeDiagnosticEvent>(debuggerEntity);
            return true;
        }

        private static void AppendGroupTiming(
            DynamicBuffer<BGasRuntimeDiagnosticEvent> log,
            ref CGasRuntimeDebugger state,
            int frame,
            string groupName,
            EGasRuntimeDiagnosticModule module,
            long groupTicks,
            long totalTicks,
            long stopwatchFrequency)
        {
            if (groupTicks <= 0)
                return;

            var elapsedMicroseconds = ToMicroseconds(groupTicks, stopwatchFrequency);
            Append(
                log,
                ref state,
                new BGasRuntimeDiagnosticEvent
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.GroupTiming,
                    Severity = EGasRuntimeDiagnosticSeverity.Trace,
                    Module = module,
                    GroupName = groupName,
                    ElapsedMicroseconds = elapsedMicroseconds,
                    TotalMicroseconds = ToMicroseconds(totalTicks, stopwatchFrequency),
                    Ratio = totalTicks > 0 ? (float)((double)groupTicks / totalTicks) : 0f,
                });
        }

        private static void RecordBufferPressure<T>(
            EntityManager em,
            Entity entity,
            string bufferName,
            int frame,
            DynamicBuffer<BGasRuntimeDiagnosticEvent> log,
            ref CGasRuntimeDebugger state)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(entity))
                return;

            var buffer = em.GetBuffer<T>(entity);
            if (buffer.Length == 0 && buffer.Capacity > 0)
                return;

            var ratio = buffer.Capacity > 0
                ? (float)buffer.Length / buffer.Capacity
                : 1f;
            var severity = SeverityForPressure(
                buffer.Length,
                buffer.Capacity,
                state.BufferPressureWarningPermille,
                state.BufferPressureErrorPermille);

            Append(
                log,
                ref state,
                new BGasRuntimeDiagnosticEvent
                {
                    Frame = frame,
                    Kind = EGasRuntimeDiagnosticKind.BufferPressure,
                    Severity = severity,
                    Module = EGasRuntimeDiagnosticModule.EventBus,
                    BufferName = bufferName,
                    Entity = entity,
                    Count = buffer.Length,
                    Capacity = buffer.Capacity,
                    Ratio = ratio,
                });
        }

        private static void Append(
            DynamicBuffer<BGasRuntimeDiagnosticEvent> log,
            ref CGasRuntimeDebugger state,
            BGasRuntimeDiagnosticEvent evt)
        {
            evt.Sequence = state.NextSequence;
            state.NextSequence++;
            log.Add(evt);
        }

        private static void ApplyRetention(
            DynamicBuffer<BGasRuntimeDiagnosticEvent> log,
            ref CGasRuntimeDebugger state)
        {
            if (state.MaxRetainedEvents <= 0 || log.Length <= state.MaxRetainedEvents)
                return;

            var removeCount = log.Length - state.MaxRetainedEvents;
            log.RemoveRange(0, removeCount);
            state.FirstRetainedSequence = log.Length > 0 ? log[0].Sequence : state.NextSequence;
            state.DroppedEventCount += removeCount;
        }

        private static EGasRuntimeDiagnosticSeverity SeverityForElapsed(int elapsedMicroseconds, int warningThreshold)
        {
            if (warningThreshold <= 0 || elapsedMicroseconds < warningThreshold)
                return EGasRuntimeDiagnosticSeverity.Trace;

            return elapsedMicroseconds >= warningThreshold * 4
                ? EGasRuntimeDiagnosticSeverity.Error
                : EGasRuntimeDiagnosticSeverity.Warning;
        }

        private static EGasRuntimeDiagnosticSeverity SeverityForPressure(
            int count,
            int capacity,
            int warningPermille,
            int errorPermille)
        {
            if (count <= 0 || capacity <= 0)
                return EGasRuntimeDiagnosticSeverity.Trace;

            var permille = count * 1000 / capacity;
            if (errorPermille > 0 && permille >= errorPermille)
                return EGasRuntimeDiagnosticSeverity.Error;
            if (warningPermille > 0 && permille >= warningPermille)
                return EGasRuntimeDiagnosticSeverity.Warning;
            return EGasRuntimeDiagnosticSeverity.Trace;
        }

        private static int ToMicroseconds(long ticks, long stopwatchFrequency)
        {
            if (ticks <= 0 || stopwatchFrequency <= 0)
                return 0;

            var microseconds = ticks * 1000000d / stopwatchFrequency;
            if (microseconds >= int.MaxValue)
                return int.MaxValue;

            return (int)Math.Round(microseconds);
        }

        private static EGasRuntimeDiagnosticModule ModuleFromGroup(string groupName)
        {
            return groupName switch
            {
                "Command" => EGasRuntimeDiagnosticModule.Command,
                "ResetDirty" => EGasRuntimeDiagnosticModule.ResetDirty,
                "Tag" => EGasRuntimeDiagnosticModule.Tag,
                "Effect" => EGasRuntimeDiagnosticModule.Effect,
                "Attribute" => EGasRuntimeDiagnosticModule.Attribute,
                "Ability" => EGasRuntimeDiagnosticModule.Ability,
                "Cue" => EGasRuntimeDiagnosticModule.Cue,
                _ => EGasRuntimeDiagnosticModule.Runtime,
            };
        }

        private static void AppendEventLine(StringBuilder builder, in BGasRuntimeDiagnosticEvent evt)
        {
            builder.Append("runtimeDiagnostic|seq=")
                .Append(evt.Sequence)
                .Append("|frame=")
                .Append(evt.Frame)
                .Append("|kind=")
                .Append(evt.Kind)
                .Append("|severity=")
                .Append(evt.Severity)
                .Append("|module=")
                .Append(evt.Module);

            if (evt.GroupName.Length > 0)
                builder.Append("|group=").Append(evt.GroupName);
            if (evt.SystemName.Length > 0)
                builder.Append("|system=").Append(evt.SystemName);
            if (evt.BufferName.Length > 0)
                builder.Append("|buffer=").Append(evt.BufferName);
            if (evt.ElapsedMicroseconds != 0)
                builder.Append("|elapsedUs=").Append(evt.ElapsedMicroseconds);
            if (evt.TotalMicroseconds != 0)
                builder.Append("|totalUs=").Append(evt.TotalMicroseconds);
            if (evt.CallCount != 0)
                builder.Append("|calls=").Append(evt.CallCount);
            if (evt.Capacity != 0 || evt.Count != 0)
            {
                builder.Append("|count=")
                    .Append(evt.Count)
                    .Append("|capacity=")
                    .Append(evt.Capacity)
                    .Append("|ratio=")
                    .Append(evt.Ratio.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
            }

            builder.AppendLine();
        }
    }
}
