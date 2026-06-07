using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Entities;

namespace GAS.Runtime
{
    public enum EGasStructuredLogFormatKind : byte
    {
        HumanReadable = 0,
        AssertionText = 1,
    }

    public readonly struct GasStructuredLogFormatOptions
    {
        public readonly EGasStructuredLogFormatKind Kind;
        public readonly bool IncludeHeader;

        public GasStructuredLogFormatOptions(
            EGasStructuredLogFormatKind kind,
            bool includeHeader)
        {
            Kind = kind;
            IncludeHeader = includeHeader;
        }

        public static GasStructuredLogFormatOptions HumanReadable =>
            new GasStructuredLogFormatOptions(EGasStructuredLogFormatKind.HumanReadable, true);

        public static GasStructuredLogFormatOptions AssertionText =>
            new GasStructuredLogFormatOptions(EGasStructuredLogFormatKind.AssertionText, true);
    }

    public struct GasStructuredLogFilter
    {
        public bool HasMinLevel;
        public EGasStructuredLogLevel MinLevel;
        public bool HasModule;
        public EGasStructuredLogModule Module;
        public bool HasFactCategory;
        public EGameplayFactCategory FactCategory;
        public bool HasFactSeverity;
        public EGameplayFactSeverity FactSeverity;
        public bool HasReplayKind;
        public EDebugReplayEventKind ReplayKind;
        public bool HasGameplayEventType;
        public EGameplayEventType GameplayEventType;
        public bool HasSourceAsc;
        public Entity SourceAsc;
        public bool HasTargetAsc;
        public Entity TargetAsc;
        public bool HasSourceReportKey;
        public int SourceReportKey;
        public bool HasTargetReportKey;
        public int TargetReportKey;
        public bool HasContextId;
        public int ContextId;
        public bool HasEventCode;
        public int EventCode;

        public static GasStructuredLogFilter All => default;

        public bool Matches(in GasStructuredLogEntry entry)
        {
            if (HasMinLevel && entry.Level < MinLevel)
                return false;

            if (HasModule && entry.Module != Module)
                return false;

            if (HasFactCategory && entry.FactCategory != FactCategory)
                return false;

            if (HasFactSeverity && entry.FactSeverity != FactSeverity)
                return false;

            if (HasReplayKind && entry.ReplayKind != ReplayKind)
                return false;

            if (HasGameplayEventType && entry.GameplayEventType != GameplayEventType)
                return false;

            if (HasSourceAsc && entry.SourceAsc != SourceAsc)
                return false;

            if (HasTargetAsc && entry.TargetAsc != TargetAsc)
                return false;

            if (HasSourceReportKey && entry.SourceReportKey != SourceReportKey)
                return false;

            if (HasTargetReportKey && entry.TargetReportKey != TargetReportKey)
                return false;

            if (HasContextId && entry.ContextId != ContextId)
                return false;

            if (HasEventCode && entry.EventCode != EventCode)
                return false;

            return true;
        }
    }

    public readonly struct GasStructuredLogExportSnapshot
    {
        public readonly GasReplayCursor Cursor;
        public readonly bool CursorExpired;
        public readonly GasReplaySinkStats ReplayStats;
        public readonly GasStructuredLogEntry[] Entries;

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

    public readonly struct GasStructuredLogFileExportResult
    {
        public readonly string Path;
        public readonly int EntryCount;
        public readonly long ByteCount;

        public GasStructuredLogFileExportResult(
            string path,
            int entryCount,
            long byteCount)
        {
            Path = path;
            EntryCount = entryCount;
            ByteCount = byteCount;
        }
    }

    public static class GasStructuredLogFormatter
    {
        public static string Format(
            in GasStructuredLogEntry entry,
            in GasStructuredLogFormatOptions options)
        {
            return options.Kind == EGasStructuredLogFormatKind.AssertionText
                ? FormatAssertion(entry)
                : FormatHumanReadable(entry);
        }

        private static string FormatHumanReadable(in GasStructuredLogEntry entry)
        {
            var builder = new StringBuilder(192);
            builder.Append('[');
            builder.Append("log=");
            builder.Append(entry.LogIndex);
            builder.Append(" frame=");
            builder.Append(entry.Frame);
            builder.Append(" seq=");
            builder.Append(entry.Sequence);
            builder.Append("] ");
            builder.Append(entry.Level);
            builder.Append(' ');
            builder.Append(entry.Module);
            builder.Append(' ');
            builder.Append(entry.FactCategory);
            builder.Append(' ');
            builder.Append(entry.ReplayKind);

            if (entry.ReplayKind == EDebugReplayEventKind.GameplayEvent)
            {
                builder.Append('/');
                builder.Append(entry.GameplayEventType);
            }
            else if (entry.ReplayKind == EDebugReplayEventKind.CueRequest)
            {
                builder.Append('/');
                builder.Append(entry.CueEvent);
            }

            AppendHumanField(builder, "event", entry.EventCode);
            AppendHumanField(builder, "reason", entry.ReasonCode);
            AppendHumanField(builder, "relatedAbilityCode", entry.RelatedAbilityCode);
            AppendHumanField(builder, "context", entry.ContextId);
            AppendHumanField(builder, "attrSet", entry.AttrSetCode);
            AppendHumanField(builder, "attribute", entry.AttributeCode);
            AppendHumanField(builder, "tag", entry.TagIndex);
            AppendHumanField(builder, "value", entry.Value);
            AppendHumanField(builder, "old", entry.OldValue);
            AppendHumanField(builder, "new", entry.NewValue);
            AppendHumanField(builder, "damage", entry.DamageAmount);
            AppendHumanField(builder, "sourceReportKey", entry.SourceReportKey);
            AppendHumanField(builder, "targetReportKey", entry.TargetReportKey);
            AppendHumanField(builder, "sourceAsc", entry.SourceAsc);
            AppendHumanField(builder, "targetAsc", entry.TargetAsc);
            AppendHumanField(builder, "ability", entry.SourceAbility);
            AppendHumanField(builder, "effect", entry.GameplayEffect);
            AppendHumanField(builder, "cue", entry.CueEntity);

            return builder.ToString();
        }

        private static string FormatAssertion(in GasStructuredLogEntry entry)
        {
            var builder = new StringBuilder(256);
            builder.Append("entry");
            AppendAssertionField(builder, "log", entry.LogIndex);
            AppendAssertionField(builder, "frame", entry.Frame);
            AppendAssertionField(builder, "seq", entry.Sequence);
            AppendAssertionField(builder, "level", entry.Level);
            AppendAssertionField(builder, "module", entry.Module);
            AppendAssertionField(builder, "domain", entry.FactDomain);
            AppendAssertionField(builder, "category", entry.FactCategory);
            AppendAssertionField(builder, "severity", entry.FactSeverity);
            AppendAssertionField(builder, "kind", entry.ReplayKind);
            AppendAssertionField(builder, "type", entry.GameplayEventType);
            AppendAssertionField(builder, "cueEvent", entry.CueEvent);
            AppendAssertionField(builder, "event", entry.EventCode);
            AppendAssertionField(builder, "reason", entry.ReasonCode);
            AppendAssertionField(builder, "relatedAbilityCode", entry.RelatedAbilityCode);
            AppendAssertionField(builder, "context", entry.ContextId);
            AppendAssertionField(builder, "attrSet", entry.AttrSetCode);
            AppendAssertionField(builder, "attribute", entry.AttributeCode);
            AppendAssertionField(builder, "tag", entry.TagIndex);
            AppendAssertionField(builder, "value", FormatFloat(entry.Value));
            AppendAssertionField(builder, "old", FormatFloat(entry.OldValue));
            AppendAssertionField(builder, "new", FormatFloat(entry.NewValue));
            AppendAssertionField(builder, "damage", FormatFloat(entry.DamageAmount));
            AppendAssertionField(builder, "flag", entry.Flag);
            AppendAssertionField(builder, "sourceReportKey", entry.SourceReportKey);
            AppendAssertionField(builder, "targetReportKey", entry.TargetReportKey);
            AppendAssertionField(builder, "sourceAsc", FormatEntity(entry.SourceAsc));
            AppendAssertionField(builder, "targetAsc", FormatEntity(entry.TargetAsc));
            AppendAssertionField(builder, "ability", FormatEntity(entry.SourceAbility));
            AppendAssertionField(builder, "effect", FormatEntity(entry.GameplayEffect));
            AppendAssertionField(builder, "sourceEntity", FormatEntity(entry.SourceEntity));
            AppendAssertionField(builder, "relatedAbility", FormatEntity(entry.RelatedAbility));
            AppendAssertionField(builder, "cue", FormatEntity(entry.CueEntity));
            return builder.ToString();
        }

        private static void AppendHumanField(StringBuilder builder, string name, int value)
        {
            if (value == 0)
                return;

            builder.Append(' ');
            builder.Append(name);
            builder.Append('=');
            builder.Append(value);
        }

        private static void AppendHumanField(StringBuilder builder, string name, float value)
        {
            if (Math.Abs(value) <= float.Epsilon)
                return;

            builder.Append(' ');
            builder.Append(name);
            builder.Append('=');
            builder.Append(FormatFloat(value));
        }

        private static void AppendHumanField(StringBuilder builder, string name, Entity value)
        {
            if (value == Entity.Null)
                return;

            builder.Append(' ');
            builder.Append(name);
            builder.Append('=');
            builder.Append(FormatEntity(value));
        }

        private static void AppendAssertionField(StringBuilder builder, string name, object value)
        {
            builder.Append('|');
            builder.Append(name);
            builder.Append('=');
            builder.Append(value);
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null
                ? "none"
                : entity.Index.ToString(CultureInfo.InvariantCulture)
                  + ":"
                  + entity.Version.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }
    }

    public static class GasStructuredLogExporter
    {
        public static GasStructuredLogExportSnapshot CreateSnapshot(
            DynamicBuffer<ReplayLogEventBuffer> replayLog,
            in GameplayEventLogSinkComponent sinkState)
        {
            var stats = GasReplaySinkPolicy.GetStats(sinkState, replayLog);
            return CreateSnapshot(
                replayLog,
                stats,
                new GasReplayCursor(stats.FirstRetainedLogIndex),
                GasReplayEventFilter.All,
                GasStructuredLogFilter.All);
        }

        public static GasStructuredLogExportSnapshot CreateSnapshot(
            EntityManager entityManager,
            DynamicBuffer<ReplayLogEventBuffer> replayLog,
            in GameplayEventLogSinkComponent sinkState)
        {
            var stats = GasReplaySinkPolicy.GetStats(sinkState, replayLog);
            return CreateSnapshot(
                entityManager,
                true,
                replayLog,
                stats,
                new GasReplayCursor(stats.FirstRetainedLogIndex),
                GasReplayEventFilter.All,
                GasStructuredLogFilter.All);
        }

        public static GasStructuredLogExportSnapshot CreateSnapshot(
            DynamicBuffer<ReplayLogEventBuffer> replayLog,
            in GameplayEventLogSinkComponent sinkState,
            in GasReplayCursor cursor,
            in GasReplayEventFilter replayFilter,
            in GasStructuredLogFilter logFilter)
        {
            return CreateSnapshot(
                replayLog,
                GasReplaySinkPolicy.GetStats(sinkState, replayLog),
                cursor,
                replayFilter,
                logFilter);
        }

        public static GasStructuredLogExportSnapshot CreateSnapshot(
            EntityManager entityManager,
            DynamicBuffer<ReplayLogEventBuffer> replayLog,
            in GameplayEventLogSinkComponent sinkState,
            in GasReplayCursor cursor,
            in GasReplayEventFilter replayFilter,
            in GasStructuredLogFilter logFilter)
        {
            return CreateSnapshot(
                entityManager,
                true,
                replayLog,
                GasReplaySinkPolicy.GetStats(sinkState, replayLog),
                cursor,
                replayFilter,
                logFilter);
        }

        public static string ExportToText(
            in GasStructuredLogExportSnapshot snapshot,
            in GasStructuredLogFormatOptions options)
        {
            var builder = new StringBuilder(Math.Max(256, snapshot.EntryCount * 192));

            if (options.IncludeHeader)
                AppendHeader(builder, snapshot, options.Kind);

            var entries = snapshot.Entries ?? Array.Empty<GasStructuredLogEntry>();
            for (var i = 0; i < entries.Length; i++)
                builder.AppendLine(GasStructuredLogFormatter.Format(entries[i], options));

            return builder.ToString();
        }

        public static GasStructuredLogFileExportResult WriteTextFile(
            string path,
            in GasStructuredLogExportSnapshot snapshot,
            in GasStructuredLogFormatOptions options)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var text = ExportToText(snapshot, options);
            File.WriteAllText(path, text, Encoding.UTF8);

            return new GasStructuredLogFileExportResult(
                path,
                snapshot.EntryCount,
                Encoding.UTF8.GetByteCount(text));
        }

        private static GasStructuredLogExportSnapshot CreateSnapshot(
            DynamicBuffer<ReplayLogEventBuffer> replayLog,
            in GasReplaySinkStats stats,
            in GasReplayCursor cursor,
            in GasReplayEventFilter replayFilter,
            in GasStructuredLogFilter logFilter)
        {
            return CreateSnapshot(
                default,
                false,
                replayLog,
                stats,
                cursor,
                replayFilter,
                logFilter);
        }

        private static GasStructuredLogExportSnapshot CreateSnapshot(
            EntityManager entityManager,
            bool resolveBoundaryReportKeys,
            DynamicBuffer<ReplayLogEventBuffer> replayLog,
            in GasReplaySinkStats stats,
            in GasReplayCursor cursor,
            in GasReplayEventFilter replayFilter,
            in GasStructuredLogFilter logFilter)
        {
            var entries = new List<GasStructuredLogEntry>(replayLog.Length);

            for (var i = 0; i < replayLog.Length; i++)
            {
                var replayEvent = replayLog[i];
                if (replayEvent.LogIndex < cursor.NextLogIndex)
                    continue;

                if (!replayFilter.Matches(replayEvent))
                    continue;

                var entry = GasStructuredLogView.FromReplay(replayEvent);
                if (resolveBoundaryReportKeys)
                {
                    entry = entry.WithBoundaryReportKeys(
                        ResolveBoundaryReportKey(entityManager, entry.SourceAsc),
                        ResolveBoundaryReportKey(entityManager, entry.TargetAsc));
                }

                if (!logFilter.Matches(entry))
                    continue;

                entries.Add(entry);
            }

            return new GasStructuredLogExportSnapshot(
                cursor,
                GasReplaySinkPolicy.IsCursorExpired(cursor, stats),
                stats,
                entries.ToArray());
        }

        private static int ResolveBoundaryReportKey(EntityManager entityManager, Entity asc)
        {
            if (asc == Entity.Null
                || entityManager.World == null
                || !entityManager.World.IsCreated
                || !entityManager.Exists(asc)
                || !entityManager.HasComponent<ASCBoundaryReportKeyComponent>(asc))
            {
                return 0;
            }

            var reportKey = entityManager.GetComponentData<ASCBoundaryReportKeyComponent>(asc);
            return reportKey.IsValid ? reportKey.Key : 0;
        }

        private static void AppendHeader(
            StringBuilder builder,
            in GasStructuredLogExportSnapshot snapshot,
            EGasStructuredLogFormatKind kind)
        {
            var stats = snapshot.ReplayStats;
            if (kind == EGasStructuredLogFormatKind.AssertionText)
            {
                builder.Append("stats");
                builder.Append("|entries=");
                builder.Append(snapshot.EntryCount);
                builder.Append("|retained=");
                builder.Append(stats.RetainedEventCount);
                builder.Append("|first=");
                builder.Append(stats.FirstRetainedLogIndex);
                builder.Append("|last=");
                builder.Append(stats.LastRetainedLogIndex);
                builder.Append("|next=");
                builder.Append(stats.NextLogIndex);
                builder.Append("|dropped=");
                builder.Append(stats.DroppedEventCount);
                builder.Append("|max=");
                builder.Append(stats.MaxRetainedEvents);
                builder.Append("|cursor=");
                builder.Append(snapshot.Cursor.NextLogIndex);
                builder.Append("|cursorExpired=");
                builder.Append(snapshot.CursorExpired);
                builder.AppendLine();
                return;
            }

            builder.Append("# GAS structured log entries=");
            builder.Append(snapshot.EntryCount);
            builder.Append(" retained=");
            builder.Append(stats.RetainedEventCount);
            builder.Append(" first=");
            builder.Append(stats.FirstRetainedLogIndex);
            builder.Append(" last=");
            builder.Append(stats.LastRetainedLogIndex);
            builder.Append(" next=");
            builder.Append(stats.NextLogIndex);
            builder.Append(" dropped=");
            builder.Append(stats.DroppedEventCount);
            builder.Append(" max=");
            builder.Append(stats.MaxRetainedEvents);
            builder.Append(" cursor=");
            builder.Append(snapshot.Cursor.NextLogIndex);
            builder.Append(" cursorExpired=");
            builder.Append(snapshot.CursorExpired);
            builder.AppendLine();
        }
    }
}
