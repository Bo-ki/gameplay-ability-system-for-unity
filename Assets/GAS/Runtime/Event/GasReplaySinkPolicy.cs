using Unity.Entities;

namespace GAS.Runtime
{
    public readonly struct GasReplayCursor
    {
        public readonly int NextLogIndex;

        public GasReplayCursor(int nextLogIndex)
        {
            NextLogIndex = nextLogIndex < 0 ? 0 : nextLogIndex;
        }

        public static GasReplayCursor FromLastConsumed(int logIndex)
        {
            return new GasReplayCursor(logIndex + 1);
        }
    }

    public readonly struct GasReplayRetentionPolicy
    {
        public readonly int MaxRetainedEvents;

        public GasReplayRetentionPolicy(int maxRetainedEvents)
        {
            MaxRetainedEvents = maxRetainedEvents < 0 ? 0 : maxRetainedEvents;
        }

        public bool IsUnlimited => MaxRetainedEvents <= 0;

        public static GasReplayRetentionPolicy Unlimited => new GasReplayRetentionPolicy(0);

        public static GasReplayRetentionPolicy KeepLatest(int maxRetainedEvents)
        {
            return new GasReplayRetentionPolicy(maxRetainedEvents);
        }
    }

    public readonly struct GasReplaySinkStats
    {
        public readonly int FirstRetainedLogIndex;
        public readonly int LastRetainedLogIndex;
        public readonly int NextLogIndex;
        public readonly int RetainedEventCount;
        public readonly int DroppedEventCount;
        public readonly int MaxRetainedEvents;

        public GasReplaySinkStats(
            int firstRetainedLogIndex,
            int lastRetainedLogIndex,
            int nextLogIndex,
            int retainedEventCount,
            int droppedEventCount,
            int maxRetainedEvents)
        {
            FirstRetainedLogIndex = firstRetainedLogIndex;
            LastRetainedLogIndex = lastRetainedLogIndex;
            NextLogIndex = nextLogIndex;
            RetainedEventCount = retainedEventCount;
            DroppedEventCount = droppedEventCount;
            MaxRetainedEvents = maxRetainedEvents < 0 ? 0 : maxRetainedEvents;
        }

        public bool HasEvents => RetainedEventCount > 0;

        public bool HasDroppedEvents => DroppedEventCount > 0;

        public bool IsUnlimitedRetention => MaxRetainedEvents <= 0;
    }

    public struct GasReplayEventFilter
    {
        public bool HasMinLogIndex;
        public int MinLogIndex;
        public bool HasMaxLogIndex;
        public int MaxLogIndex;
        public bool HasMinFrame;
        public int MinFrame;
        public bool HasMaxFrame;
        public int MaxFrame;
        public bool HasKind;
        public EDebugReplayEventKind Kind;
        public bool HasGameplayEventType;
        public EGameplayEventType GameplayEventType;
        public bool HasSourceAsc;
        public Entity SourceAsc;
        public bool HasTargetAsc;
        public Entity TargetAsc;
        public bool HasContextId;
        public int ContextId;
        public bool HasEventCode;
        public int EventCode;

        public static GasReplayEventFilter All => default;

        public bool Matches(in BDebugReplayEvent evt)
        {
            return GasReplaySinkPolicy.Matches(evt, this);
        }
    }

    public static class GasReplaySinkPolicy
    {
        public const int UnlimitedRetention = 0;

        public static GasReplayRetentionPolicy GetRetentionPolicy(in CGameplayEventLogSink sinkState)
        {
            return new GasReplayRetentionPolicy(sinkState.MaxRetainedEvents);
        }

        public static GasReplaySinkStats GetStats(
            in CGameplayEventLogSink sinkState,
            DynamicBuffer<BDebugReplayEvent> log)
        {
            var retainedCount = log.Length;
            var firstRetainedLogIndex = retainedCount > 0
                ? log[0].LogIndex
                : sinkState.NextLogIndex;
            var lastRetainedLogIndex = retainedCount > 0
                ? log[retainedCount - 1].LogIndex
                : sinkState.NextLogIndex - 1;

            return new GasReplaySinkStats(
                firstRetainedLogIndex,
                lastRetainedLogIndex,
                sinkState.NextLogIndex,
                retainedCount,
                sinkState.DroppedEventCount,
                sinkState.MaxRetainedEvents);
        }

        public static bool IsCursorExpired(in GasReplayCursor cursor, in GasReplaySinkStats stats)
        {
            return stats.HasEvents && cursor.NextLogIndex < stats.FirstRetainedLogIndex;
        }

        public static bool IsCursorAtEnd(in GasReplayCursor cursor, in GasReplaySinkStats stats)
        {
            return cursor.NextLogIndex >= stats.NextLogIndex;
        }

        public static void ApplyRetention(
            DynamicBuffer<BDebugReplayEvent> log,
            ref CGameplayEventLogSink sinkState)
        {
            var retentionPolicy = GetRetentionPolicy(sinkState);
            if (retentionPolicy.IsUnlimited)
            {
                SyncFirstRetainedIndex(log, ref sinkState);
                return;
            }

            while (log.Length > retentionPolicy.MaxRetainedEvents)
            {
                log.RemoveAt(0);
                sinkState.DroppedEventCount++;
            }

            SyncFirstRetainedIndex(log, ref sinkState);
        }

        public static bool Matches(in BDebugReplayEvent evt, in GasReplayEventFilter filter)
        {
            if (filter.HasMinLogIndex && evt.LogIndex < filter.MinLogIndex)
                return false;

            if (filter.HasMaxLogIndex && evt.LogIndex > filter.MaxLogIndex)
                return false;

            if (filter.HasMinFrame && evt.Frame < filter.MinFrame)
                return false;

            if (filter.HasMaxFrame && evt.Frame > filter.MaxFrame)
                return false;

            if (filter.HasKind && evt.Kind != filter.Kind)
                return false;

            if (filter.HasGameplayEventType && evt.GameplayEventType != filter.GameplayEventType)
                return false;

            if (filter.HasSourceAsc && evt.SourceAsc != filter.SourceAsc)
                return false;

            if (filter.HasTargetAsc && evt.TargetAsc != filter.TargetAsc)
                return false;

            if (filter.HasContextId && evt.ContextId != filter.ContextId)
                return false;

            if (filter.HasEventCode && evt.EventCode != filter.EventCode)
                return false;

            return true;
        }

        private static void SyncFirstRetainedIndex(
            DynamicBuffer<BDebugReplayEvent> log,
            ref CGameplayEventLogSink sinkState)
        {
            sinkState.FirstRetainedLogIndex = log.Length > 0
                ? log[0].LogIndex
                : sinkState.NextLogIndex;
        }
    }
}
