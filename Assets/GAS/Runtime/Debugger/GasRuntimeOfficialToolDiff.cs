using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.Entities;
using UnityEngine;

#if ENABLE_PROFILER
using Unity.Profiling;
using UnityEngine.Profiling;
#endif

namespace GAS.Runtime
{
    public readonly struct GasRuntimeOfficialToolDiffSnapshot
    {
        public readonly bool JournalingAvailable;
        public readonly bool JournalingCaptured;
        public readonly int JournalingRecordCount;
        public readonly int JournalingWorldRecordCount;
        public readonly int JournalingStructuralRecordCount;
        public readonly int JournalingCreateEntityCount;
        public readonly int JournalingDestroyEntityCount;
        public readonly int JournalingAddComponentCount;
        public readonly int JournalingRemoveComponentCount;
        public readonly int JournalingEnableComponentCount;
        public readonly int JournalingDisableComponentCount;
        public readonly int JournalingSetComponentDataCount;
        public readonly int JournalingSetBufferCount;
        public readonly int JournalingGetComponentDataRwCount;
        public readonly int JournalingGetBufferRwCount;
        public readonly bool ProfilerAvailable;
        public readonly bool ProfilerEnabled;
        public readonly bool StructuralChangesProfilerCategoryEnabled;
        public readonly bool MemoryProfilerCategoryEnabled;
        public readonly string ProfilerCaptureState;
        public readonly string JournalingRecordTopN;
        public readonly string JournalingSystemTopN;
        public readonly string JournalingComponentTopN;

        public GasRuntimeOfficialToolDiffSnapshot(
            bool journalingAvailable,
            bool journalingCaptured,
            int journalingRecordCount,
            int journalingWorldRecordCount,
            int journalingStructuralRecordCount,
            int journalingCreateEntityCount,
            int journalingDestroyEntityCount,
            int journalingAddComponentCount,
            int journalingRemoveComponentCount,
            int journalingEnableComponentCount,
            int journalingDisableComponentCount,
            int journalingSetComponentDataCount,
            int journalingSetBufferCount,
            int journalingGetComponentDataRwCount,
            int journalingGetBufferRwCount,
            bool profilerAvailable,
            bool profilerEnabled,
            bool structuralChangesProfilerCategoryEnabled,
            bool memoryProfilerCategoryEnabled,
            string profilerCaptureState,
            string journalingRecordTopN = "",
            string journalingSystemTopN = "",
            string journalingComponentTopN = "")
        {
            JournalingAvailable = journalingAvailable;
            JournalingCaptured = journalingCaptured;
            JournalingRecordCount = journalingRecordCount;
            JournalingWorldRecordCount = journalingWorldRecordCount;
            JournalingStructuralRecordCount = journalingStructuralRecordCount;
            JournalingCreateEntityCount = journalingCreateEntityCount;
            JournalingDestroyEntityCount = journalingDestroyEntityCount;
            JournalingAddComponentCount = journalingAddComponentCount;
            JournalingRemoveComponentCount = journalingRemoveComponentCount;
            JournalingEnableComponentCount = journalingEnableComponentCount;
            JournalingDisableComponentCount = journalingDisableComponentCount;
            JournalingSetComponentDataCount = journalingSetComponentDataCount;
            JournalingSetBufferCount = journalingSetBufferCount;
            JournalingGetComponentDataRwCount = journalingGetComponentDataRwCount;
            JournalingGetBufferRwCount = journalingGetBufferRwCount;
            ProfilerAvailable = profilerAvailable;
            ProfilerEnabled = profilerEnabled;
            StructuralChangesProfilerCategoryEnabled = structuralChangesProfilerCategoryEnabled;
            MemoryProfilerCategoryEnabled = memoryProfilerCategoryEnabled;
            ProfilerCaptureState = profilerCaptureState ?? string.Empty;
            JournalingRecordTopN = journalingRecordTopN ?? string.Empty;
            JournalingSystemTopN = journalingSystemTopN ?? string.Empty;
            JournalingComponentTopN = journalingComponentTopN ?? string.Empty;
        }

        public static GasRuntimeOfficialToolDiffSnapshot Unavailable =>
            new GasRuntimeOfficialToolDiffSnapshot(
                false,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                false,
                false,
                "official profiler modules not captured");
    }

    public struct GasRuntimeOfficialToolDiffCapture
    {
        private World _world;
        private bool _journalingAvailable;
        private bool _previousJournalingEnabled;
        private bool _journalingCaptureStarted;
        private bool _releaseJournalingStateOnEnd;
        private bool _profilerAvailable;
        private bool _profilerEnabled;
        private bool _structuralChangesCategoryEnabled;
        private bool _memoryCategoryEnabled;
        private string _profilerCaptureState;

        public static GasRuntimeOfficialToolDiffCapture Begin(World world)
        {
            var capture = new GasRuntimeOfficialToolDiffCapture
            {
                _world = world,
                _profilerCaptureState = "official profiler modules not captured",
            };

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            capture._journalingAvailable = true;
            capture._previousJournalingEnabled = EntitiesJournaling.Enabled;
            capture._releaseJournalingStateOnEnd = !capture._previousJournalingEnabled && Application.isBatchMode;
            EntitiesJournaling.Enabled = true;
            EntitiesJournaling.Clear();
            capture._journalingCaptureStarted = EntitiesJournaling.Enabled;
#endif

#if ENABLE_PROFILER
            capture._profilerAvailable = true;
            capture._profilerEnabled = Profiler.enabled;
            capture._structuralChangesCategoryEnabled =
                Profiler.IsCategoryEnabled(new ProfilerCategory("Entities Structural Changes"));
            capture._memoryCategoryEnabled =
                Profiler.IsCategoryEnabled(new ProfilerCategory("Entities Memory"));
            capture._profilerCaptureState = capture._profilerEnabled
                ? "profiler enabled; module counter data not exported by headless runner"
                : "profiler disabled; Entities profiler modules collect no data";
#endif

            return capture;
        }

        public GasRuntimeOfficialToolDiffSnapshot End()
        {
            var snapshot = CaptureSnapshot();

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (_journalingAvailable)
            {
                EntitiesJournaling.Enabled = _previousJournalingEnabled;

                if (_releaseJournalingStateOnEnd)
                    ReleaseJournalingStateForHeadless();
            }
#endif

            return snapshot;
        }

        private GasRuntimeOfficialToolDiffSnapshot CaptureSnapshot()
        {
            var journalingRecordCount = 0;
            var journalingWorldRecordCount = 0;
            var journalingStructuralRecordCount = 0;
            var journalingCreateEntityCount = 0;
            var journalingDestroyEntityCount = 0;
            var journalingAddComponentCount = 0;
            var journalingRemoveComponentCount = 0;
            var journalingEnableComponentCount = 0;
            var journalingDisableComponentCount = 0;
            var journalingSetComponentDataCount = 0;
            var journalingSetBufferCount = 0;
            var journalingGetComponentDataRwCount = 0;
            var journalingGetBufferRwCount = 0;
            var journalingRecordTopN = string.Empty;
            var journalingSystemTopN = string.Empty;
            var journalingComponentTopN = string.Empty;

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            if (_journalingCaptureStarted)
            {
                var recordCounts = new Dictionary<string, int>(32);
                var systemCounts = new Dictionary<string, int>(64);
                var componentCounts = new Dictionary<string, int>(64);
                var records = EntitiesJournaling.GetRecords(EntitiesJournaling.Ordering.Ascending);
                for (var i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    journalingRecordCount++;
                    if (!IsTargetWorld(record))
                        continue;

                    journalingWorldRecordCount++;
                    var recordTypeName = record.RecordType.ToString();
                    AddCount(recordCounts, recordTypeName);
                    AddCount(systemCounts, recordTypeName + "@" + SafeName(record.ExecutingSystem.Name, "NoExecutingSystem"));
                    var componentTypes = record.ComponentTypes;
                    for (var componentIndex = 0; componentIndex < componentTypes.Length; componentIndex++)
                        AddCount(componentCounts, recordTypeName + ":" + SafeName(componentTypes[componentIndex].Name, "NoComponent"));

                    switch (record.RecordType)
                    {
                        case EntitiesJournaling.RecordType.CreateEntity:
                            journalingStructuralRecordCount++;
                            journalingCreateEntityCount++;
                            break;
                        case EntitiesJournaling.RecordType.DestroyEntity:
                            journalingStructuralRecordCount++;
                            journalingDestroyEntityCount++;
                            break;
                        case EntitiesJournaling.RecordType.AddComponent:
                            journalingStructuralRecordCount++;
                            journalingAddComponentCount++;
                            break;
                        case EntitiesJournaling.RecordType.RemoveComponent:
                            journalingStructuralRecordCount++;
                            journalingRemoveComponentCount++;
                            break;
                        case EntitiesJournaling.RecordType.EnableComponent:
                            journalingEnableComponentCount++;
                            break;
                        case EntitiesJournaling.RecordType.DisableComponent:
                            journalingDisableComponentCount++;
                            break;
                        case EntitiesJournaling.RecordType.SetComponentData:
                            journalingSetComponentDataCount++;
                            break;
                        case EntitiesJournaling.RecordType.SetBuffer:
                            journalingSetBufferCount++;
                            break;
                        case EntitiesJournaling.RecordType.GetComponentDataRW:
                            journalingGetComponentDataRwCount++;
                            break;
                        case EntitiesJournaling.RecordType.GetBufferRW:
                            journalingGetBufferRwCount++;
                            break;
                    }
                }

                journalingRecordTopN = FormatTopN(recordCounts, 6);
                journalingSystemTopN = FormatTopN(systemCounts, 8);
                journalingComponentTopN = FormatTopN(componentCounts, 8);
            }
#endif

            return new GasRuntimeOfficialToolDiffSnapshot(
                _journalingAvailable,
                _journalingCaptureStarted && journalingRecordCount > 0,
                journalingRecordCount,
                journalingWorldRecordCount,
                journalingStructuralRecordCount,
                journalingCreateEntityCount,
                journalingDestroyEntityCount,
                journalingAddComponentCount,
                journalingRemoveComponentCount,
                journalingEnableComponentCount,
                journalingDisableComponentCount,
                journalingSetComponentDataCount,
                journalingSetBufferCount,
                journalingGetComponentDataRwCount,
                journalingGetBufferRwCount,
                _profilerAvailable,
                _profilerEnabled,
                _structuralChangesCategoryEnabled,
                _memoryCategoryEnabled,
                _profilerCaptureState,
                journalingRecordTopN,
                journalingSystemTopN,
                journalingComponentTopN);
        }

        private static void AddCount(Dictionary<string, int> counts, string key)
        {
            if (string.IsNullOrEmpty(key))
                key = "Unknown";

            counts.TryGetValue(key, out var count);
            counts[key] = count + 1;
        }

        private static string FormatTopN(Dictionary<string, int> counts, int limit)
        {
            if (counts == null || counts.Count == 0 || limit <= 0)
                return string.Empty;

            var entries = new List<KeyValuePair<string, int>>(counts);
            entries.Sort(static (left, right) =>
            {
                var valueCompare = right.Value.CompareTo(left.Value);
                return valueCompare != 0
                    ? valueCompare
                    : string.CompareOrdinal(left.Key, right.Key);
            });

            var builder = new StringBuilder(256);
            var count = Math.Min(limit, entries.Count);
            for (var i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append(';');
                builder.Append(entries[i].Key)
                    .Append('=')
                    .Append(entries[i].Value);
            }

            return builder.ToString();
        }

        private static string SafeName(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
        private bool IsTargetWorld(EntitiesJournaling.RecordView record)
        {
            return _world != null
                   && _world.IsCreated
                   && record.World.SequenceNumber == _world.SequenceNumber;
        }

        private static void ReleaseJournalingStateForHeadless()
        {
            var shutdownMethod = typeof(EntitiesJournaling).GetMethod(
                "Shutdown",
                BindingFlags.Static | BindingFlags.NonPublic);
            shutdownMethod?.Invoke(null, null);
        }
#endif
    }
}
