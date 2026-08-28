namespace GAS.Runtime
{
    /// <summary>
    /// 标识 managed Boundary 日志快照的下一读取位置。
    /// </summary>
    public readonly struct GasReplayCursor
    {
        public readonly int NextLogIndex;

        /// <summary>
        /// 构造非负日志游标。
        /// </summary>
        public GasReplayCursor(int nextLogIndex)
        {
            NextLogIndex = nextLogIndex < 0 ? 0 : nextLogIndex;
        }
    }

    /// <summary>
    /// 描述 managed Boundary 日志快照的留存统计，不拥有任何 ECS buffer。
    /// </summary>
    public readonly struct GasReplaySinkStats
    {
        public readonly int FirstRetainedLogIndex;
        public readonly int LastRetainedLogIndex;
        public readonly int NextLogIndex;
        public readonly int RetainedEventCount;
        public readonly int DroppedEventCount;
        public readonly int MaxRetainedEvents;

        /// <summary>
        /// 构造只读日志统计。
        /// </summary>
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
    }
}
