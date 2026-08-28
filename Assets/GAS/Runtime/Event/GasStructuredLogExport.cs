using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 保存一次 managed Boundary 日志的不可变复制结果；快照不持有 World、EntityManager 或 DynamicBuffer。
    /// </summary>
    public readonly struct GasStructuredLogExportSnapshot
    {
        public readonly GasReplayCursor Cursor;
        public readonly bool CursorExpired;
        public readonly GasReplaySinkStats ReplayStats;
        public readonly GasStructuredLogEntry[] Entries;

        /// <summary>
        /// 构造只读导出快照并归一化空数组。
        /// </summary>
        public GasStructuredLogExportSnapshot(
            GasReplayCursor cursor,
            bool cursorExpired,
            GasReplaySinkStats replayStats,
            GasStructuredLogEntry[] entries)
        {
            Cursor = cursor;
            CursorExpired = cursorExpired;
            ReplayStats = replayStats;
            Entries = entries ?? Array.Empty<GasStructuredLogEntry>();
        }

        public int EntryCount => Entries?.Length ?? 0;
    }
}
