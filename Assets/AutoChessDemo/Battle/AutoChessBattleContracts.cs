using System;
using System.Diagnostics;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public enum AutoChessTeam : byte
    {
        None = 0,
        Player = 1,
        Enemy = 2,
        Draw = 3,
    }

    public readonly struct AutoChessBattleOptions
    {
        public readonly int MaxTicks;
        public readonly int PostVictoryFlushTicks;
        public readonly int Scale;
        public readonly float HealthMultiplier;
        public readonly double MinimumBattleSeconds;
        private readonly byte _captureOfficialToolDiff;
        private readonly byte _debuggerEnabled;
        private readonly byte _captureSystemTimings;
        private readonly byte _captureBufferPressure;

        public bool CaptureOfficialToolDiff => _captureOfficialToolDiff != 2;
        public bool DebuggerEnabled => _debuggerEnabled != 2;
        public bool CaptureSystemTimings => _captureSystemTimings != 2;
        public bool CaptureBufferPressure => _captureBufferPressure != 2;

        public AutoChessBattleOptions(
            int maxTicks,
            int postVictoryFlushTicks,
            int scale = 1,
            bool captureOfficialToolDiff = true,
            bool debuggerEnabled = true,
            bool captureSystemTimings = true,
            bool captureBufferPressure = true,
            float healthMultiplier = 1f,
            double minimumBattleSeconds = 0d)
        {
            MaxTicks = maxTicks;
            PostVictoryFlushTicks = postVictoryFlushTicks;
            Scale = scale;
            HealthMultiplier = healthMultiplier;
            MinimumBattleSeconds = minimumBattleSeconds;
            _captureOfficialToolDiff = captureOfficialToolDiff ? (byte)1 : (byte)2;
            _debuggerEnabled = debuggerEnabled ? (byte)1 : (byte)2;
            _captureSystemTimings = captureSystemTimings ? (byte)1 : (byte)2;
            _captureBufferPressure = captureBufferPressure ? (byte)1 : (byte)2;
        }

        public AutoChessBattleOptions Normalize()
        {
            return new AutoChessBattleOptions(
                MaxTicks > 0 ? MaxTicks : 64,
                PostVictoryFlushTicks >= 0 ? PostVictoryFlushTicks : 4,
                Scale > 0 ? Scale : 1,
                CaptureOfficialToolDiff,
                DebuggerEnabled,
                CaptureSystemTimings,
                CaptureBufferPressure,
                HealthMultiplier > 0f ? HealthMultiplier : 1f,
                MinimumBattleSeconds > 0d ? MinimumBattleSeconds : 0d);
        }
    }

    public readonly struct AutoChessBattleProfileHooks
    {
        public readonly Action BeforeMeasuredWindow;
        public readonly Action AfterMeasuredWindow;

        public AutoChessBattleProfileHooks(
            Action beforeMeasuredWindow,
            Action afterMeasuredWindow)
        {
            BeforeMeasuredWindow = beforeMeasuredWindow;
            AfterMeasuredWindow = afterMeasuredWindow;
        }
    }

    public readonly struct AutoChessBattleUnitResult
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string OwnerName;
        public readonly string ArchetypeName;
        public readonly AutoChessTeam Team;
        public readonly int Slot;
        public readonly float Health;
        public readonly float Energy;
        public readonly bool Alive;

        public AutoChessBattleUnitResult(
            string id,
            string displayName,
            string ownerName,
            string archetypeName,
            AutoChessTeam team,
            int slot,
            float health,
            float energy,
            bool alive)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            OwnerName = string.IsNullOrWhiteSpace(ownerName)
                ? AutoChessBattleRules.GetTeamName(team)
                : ownerName;
            ArchetypeName = string.IsNullOrWhiteSpace(archetypeName) ? DisplayName : archetypeName;
            Team = team;
            Slot = slot;
            Health = health;
            Energy = energy;
            Alive = alive;
        }
    }

    public readonly struct AutoChessBattleEventCounts
    {
        public readonly int ReplayEvents;
        public readonly int StructuredLogEntries;
        public readonly int AbilityCommitSucceeded;
        public readonly int GameplayEffectInstanced;
        public readonly int GameplayEffectApplied;
        public readonly int GameplayEffectRemoved;
        public readonly int ExecutionCalculationOutputUpdated;
        public readonly int AttributeChanges;
        public readonly int PeriodTickDamageFacts;
        public readonly float PeriodTickDamageTotal;
        public readonly int TagChanges;
        public readonly int CueRequests;

        public AutoChessBattleEventCounts(
            int replayEvents,
            int structuredLogEntries,
            int abilityCommitSucceeded,
            int gameplayEffectInstanced,
            int gameplayEffectApplied,
            int gameplayEffectRemoved,
            int executionCalculationOutputUpdated,
            int attributeChanges,
            int periodTickDamageFacts,
            float periodTickDamageTotal,
            int tagChanges,
            int cueRequests)
        {
            ReplayEvents = replayEvents;
            StructuredLogEntries = structuredLogEntries;
            AbilityCommitSucceeded = abilityCommitSucceeded;
            GameplayEffectInstanced = gameplayEffectInstanced;
            GameplayEffectApplied = gameplayEffectApplied;
            GameplayEffectRemoved = gameplayEffectRemoved;
            ExecutionCalculationOutputUpdated = executionCalculationOutputUpdated;
            AttributeChanges = attributeChanges;
            PeriodTickDamageFacts = periodTickDamageFacts;
            PeriodTickDamageTotal = periodTickDamageTotal;
            TagChanges = tagChanges;
            CueRequests = cueRequests;
        }
    }

    public enum AutoChessBattleReportEventKind : byte
    {
        SkillResolved = 0,
        DamageApplied = 1,
        UnitDied = 2,
    }

    internal enum AutoChessBattleReportFactKind : byte
    {
        SkillResolved = 0,
        CombatHealthReduced = 1,
    }

    internal readonly struct AutoChessBattleReportFact
    {
        public readonly int Frame;
        public readonly AutoChessBattleReportFactKind Kind;
        public readonly int SourceUnitIndex;
        public readonly int TargetUnitIndex;
        public readonly int AbilityCode;
        public readonly int GameplayEffectCode;
        public readonly float Value;
        public readonly float OldValue;
        public readonly float NewValue;

        public AutoChessBattleReportFact(
            int frame,
            AutoChessBattleReportFactKind kind,
            int sourceUnitIndex,
            int targetUnitIndex,
            int abilityCode,
            int gameplayEffectCode,
            float value,
            float oldValue,
            float newValue)
        {
            Frame = frame;
            Kind = kind;
            SourceUnitIndex = sourceUnitIndex;
            TargetUnitIndex = targetUnitIndex;
            AbilityCode = abilityCode;
            GameplayEffectCode = gameplayEffectCode;
            Value = value;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public readonly struct AutoChessBattleReportUnit
    {
        public readonly int UnitIndex;
        public readonly string UnitId;
        public readonly string DisplayName;
        public readonly string OwnerName;
        public readonly AutoChessTeam Team;
        public readonly int Slot;
        public readonly int BattleGroup;

        public AutoChessBattleReportUnit(
            int unitIndex,
            string unitId,
            string displayName,
            string ownerName,
            AutoChessTeam team,
            int slot,
            int battleGroup)
        {
            UnitIndex = unitIndex;
            UnitId = unitId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? UnitId : displayName;
            OwnerName = ownerName ?? string.Empty;
            Team = team;
            Slot = slot;
            BattleGroup = battleGroup;
        }
    }

    public readonly struct AutoChessBattleReportEvent
    {
        public readonly int Frame;
        public readonly AutoChessBattleReportEventKind Kind;
        public readonly int SourceUnitIndex;
        public readonly int TargetUnitIndex;
        public readonly int AbilityCode;
        public readonly int GameplayEffectCode;
        public readonly float Value;
        public readonly float OldValue;
        public readonly float NewValue;

        public AutoChessBattleReportEvent(
            int frame,
            AutoChessBattleReportEventKind kind,
            int sourceUnitIndex,
            int targetUnitIndex,
            int abilityCode,
            int gameplayEffectCode,
            float value,
            float oldValue,
            float newValue)
        {
            Frame = frame;
            Kind = kind;
            SourceUnitIndex = sourceUnitIndex;
            TargetUnitIndex = targetUnitIndex;
            AbilityCode = abilityCode;
            GameplayEffectCode = gameplayEffectCode;
            Value = value;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    public readonly struct AutoChessBattleReport
    {
        public readonly AutoChessBattleReportUnit[] Units;
        public readonly AutoChessBattleReportEvent[] Events;

        public AutoChessBattleReport(
            AutoChessBattleReportUnit[] units,
            AutoChessBattleReportEvent[] events)
        {
            Units = units ?? Array.Empty<AutoChessBattleReportUnit>();
            Events = events ?? Array.Empty<AutoChessBattleReportEvent>();
        }
    }

    public struct AutoChessBattleSystemTiming
    {
        public int Samples;
        public long TotalTicks;
        public long MaxTicks;

        public double AverageMilliseconds => Samples > 0 ? ToMilliseconds(TotalTicks) / Samples : 0d;

        public double MaxMilliseconds => ToMilliseconds(MaxTicks);

        public void Add(long elapsedTicks)
        {
            Samples++;
            TotalTicks += elapsedTicks;
            if (elapsedTicks > MaxTicks)
                MaxTicks = elapsedTicks;
        }

        private static double ToMilliseconds(long stopwatchTicks)
        {
            return stopwatchTicks * 1000d / Stopwatch.Frequency;
        }
    }

    public struct AutoChessBattleRuntimeTiming
    {
        public AutoChessBattleSystemTiming TickTotal;
        public AutoChessBattleSystemTiming FramePrepare;
        public AutoChessBattleSystemTiming CommandResolve;
        public AutoChessBattleSystemTiming CoreSimulation;
        public AutoChessBattleSystemTiming StructuralCommit;
        public AutoChessBattleSystemTiming BoundaryProjection;
        public AutoChessBattleSystemTiming DependencyDrain;
        public AutoChessBattleSystemTiming CoreRuntime;
        public AutoChessBattleSystemTiming Boundary;
        public AutoChessBattleSystemTiming Runner;
        public AutoChessBattleSystemTiming Debugger;
        public AutoChessBattleSystemTiming Physics;
        public AutoChessBattleSystemTiming Render;

        public void Add(
            long framePrepareTicks,
            long commandResolveTicks,
            long coreSimulationTicks,
            long structuralCommitTicks,
            long boundaryProjectionTicks,
            long dependencyDrainTicks)
        {
            var totalTicks = framePrepareTicks
                             + commandResolveTicks
                             + coreSimulationTicks
                             + structuralCommitTicks
                             + boundaryProjectionTicks
                             + dependencyDrainTicks;
            TickTotal.Add(totalTicks);
            FramePrepare.Add(framePrepareTicks);
            CommandResolve.Add(commandResolveTicks);
            CoreSimulation.Add(coreSimulationTicks);
            StructuralCommit.Add(structuralCommitTicks);
            BoundaryProjection.Add(boundaryProjectionTicks);
            DependencyDrain.Add(dependencyDrainTicks);
            CoreRuntime.Add(
                framePrepareTicks
                + commandResolveTicks
                + coreSimulationTicks
                + structuralCommitTicks);
            Boundary.Add(boundaryProjectionTicks);
            Runner.Add(dependencyDrainTicks);
        }

        public void AddDebuggerExport(long debuggerTicks)
        {
            Debugger.Add(debuggerTicks);
        }
    }

    public readonly struct AutoChessValidationEvidence
    {
        public readonly bool Completed;
        public readonly AutoChessTeam Winner;
        public readonly AutoChessTeam ExpectedWinner;
        public readonly int ScenarioScale;
        public readonly int UnitCount;
        public readonly int BattleTicks;
        public readonly int TotalTicks;
        public readonly int WarmupDroppedTicks;
        public readonly int MeasuredTicks;
        public readonly int CommandCount;
        public readonly int AttributeChangeCount;
        public readonly int PeriodTickDamageFactCount;
        public readonly float PeriodTickDamageTotal;
        public readonly int ExecutionOutputCount;
        public readonly int CueRequestCount;
        public readonly int RuntimeEventCount;
        public readonly int DebugWarningCount;
        public readonly int DebugErrorCount;
        public readonly int BlockingDebugErrorCount;
        public readonly int CoreRequestCount;
        public readonly int CoreFactCount;
        public readonly int CoreDeltaCount;
        public readonly int CoreCueCount;
        public readonly int PresentationMarkerCount;
        public readonly int PresentationSourceLineCount;
        public readonly int PresentationDisplayLineCount;
        public readonly int PresentationDroppedLineCount;
        public readonly int PeakEventBusLength;
        public readonly int ReplayLag;
        public readonly int ProcessWarmupRuns;
        public readonly int ProofOnlyApiMask;
        public readonly int ReselectTriggerMask;
        public readonly int ActiveEffectSlotCount;
        public readonly int ActiveEffectSlotActiveCount;
        public readonly int ActiveEffectChunkSkipDuePeriodSlotCount;
        public readonly int ActiveMutationCommandCount;
        public readonly int ActiveMutationOwnerGroupCount;
        public readonly int ActiveMutationMaxOwnerRange;
        public readonly int ActiveMutationSortMoveCount;
        public readonly int ActiveMutationEstimatedRandomLookupCount;
        public readonly int ActiveMutationOwnerResourceLookupCount;
        public readonly int ActiveMutationMigrationCarrierCount;
        public readonly int PendingAttributeDeltaCount;
        public readonly int PendingAttributeAppliedDeltaCount;
        public readonly int PendingAttributeSkippedDeltaCount;
        public readonly int PendingAttributeTargetGroupCount;
        public readonly int PendingAttributeMaxTargetRange;
        public readonly int PendingAttributeEstimatedRandomLookupCount;
        public readonly int PendingAttributeFactPatchCount;
        public readonly int PendingAttributeMigrationCarrierCount;
        public readonly int StreamCarrierPressureWarningCount;
        public readonly int StreamCarrierPeakCount;
        public readonly int StreamCarrierPeakCapacity;
        public readonly double TotalElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly uint FactsHash;
        public readonly uint SummaryHash;
        public readonly bool EcsRuntimeTickOnly;
        public readonly bool OfficialDiffSeparatePass;
        public readonly bool JournalingAvailable;
        public readonly bool JournalingCaptured;
        public readonly bool ProfilerAvailable;
        public readonly string ProfilerCaptureState;
        public readonly string PhysicsDisabledReason;
        public readonly string RenderDisabledReason;
        public readonly string PresentationDisabledReason;

        public AutoChessValidationEvidence(
            bool completed,
            AutoChessTeam winner,
            AutoChessTeam expectedWinner,
            int scenarioScale,
            int unitCount,
            int battleTicks,
            int totalTicks,
            int warmupDroppedTicks,
            int measuredTicks,
            int commandCount,
            int attributeChangeCount,
            int periodTickDamageFactCount,
            float periodTickDamageTotal,
            int executionOutputCount,
            int cueRequestCount,
            int runtimeEventCount,
            int debugWarningCount,
            int debugErrorCount,
            int blockingDebugErrorCount,
            int coreRequestCount,
            int coreFactCount,
            int coreDeltaCount,
            int coreCueCount,
            int presentationMarkerCount,
            int presentationSourceLineCount,
            int presentationDisplayLineCount,
            int presentationDroppedLineCount,
            int peakEventBusLength,
            int replayLag,
            int processWarmupRuns,
            int proofOnlyApiMask,
            int reselectTriggerMask,
            int activeEffectSlotCount,
            int activeEffectSlotActiveCount,
            int activeEffectChunkSkipDuePeriodSlotCount,
            int activeMutationCommandCount,
            int activeMutationOwnerGroupCount,
            int activeMutationMaxOwnerRange,
            int activeMutationSortMoveCount,
            int activeMutationEstimatedRandomLookupCount,
            int activeMutationOwnerResourceLookupCount,
            int activeMutationMigrationCarrierCount,
            int pendingAttributeDeltaCount,
            int pendingAttributeAppliedDeltaCount,
            int pendingAttributeSkippedDeltaCount,
            int pendingAttributeTargetGroupCount,
            int pendingAttributeMaxTargetRange,
            int pendingAttributeEstimatedRandomLookupCount,
            int pendingAttributeFactPatchCount,
            int pendingAttributeMigrationCarrierCount,
            int streamCarrierPressureWarningCount,
            int streamCarrierPeakCount,
            int streamCarrierPeakCapacity,
            double totalElapsedMilliseconds,
            double averageTickMilliseconds,
            uint factsHash,
            uint summaryHash,
            bool ecsRuntimeTickOnly,
            bool officialDiffSeparatePass,
            bool journalingAvailable,
            bool journalingCaptured,
            bool profilerAvailable,
            string profilerCaptureState,
            string physicsDisabledReason,
            string renderDisabledReason,
            string presentationDisabledReason)
        {
            Completed = completed;
            Winner = winner;
            ExpectedWinner = expectedWinner;
            ScenarioScale = scenarioScale;
            UnitCount = unitCount;
            BattleTicks = battleTicks;
            TotalTicks = totalTicks;
            WarmupDroppedTicks = warmupDroppedTicks;
            MeasuredTicks = measuredTicks;
            CommandCount = commandCount;
            AttributeChangeCount = attributeChangeCount;
            PeriodTickDamageFactCount = periodTickDamageFactCount;
            PeriodTickDamageTotal = periodTickDamageTotal;
            ExecutionOutputCount = executionOutputCount;
            CueRequestCount = cueRequestCount;
            RuntimeEventCount = runtimeEventCount;
            DebugWarningCount = debugWarningCount;
            DebugErrorCount = debugErrorCount;
            BlockingDebugErrorCount = blockingDebugErrorCount;
            CoreRequestCount = coreRequestCount;
            CoreFactCount = coreFactCount;
            CoreDeltaCount = coreDeltaCount;
            CoreCueCount = coreCueCount;
            PresentationMarkerCount = presentationMarkerCount;
            PresentationSourceLineCount = presentationSourceLineCount;
            PresentationDisplayLineCount = presentationDisplayLineCount;
            PresentationDroppedLineCount = presentationDroppedLineCount;
            PeakEventBusLength = peakEventBusLength;
            ReplayLag = replayLag;
            ProcessWarmupRuns = processWarmupRuns;
            ProofOnlyApiMask = proofOnlyApiMask;
            ReselectTriggerMask = reselectTriggerMask;
            ActiveEffectSlotCount = activeEffectSlotCount;
            ActiveEffectSlotActiveCount = activeEffectSlotActiveCount;
            ActiveEffectChunkSkipDuePeriodSlotCount = activeEffectChunkSkipDuePeriodSlotCount;
            ActiveMutationCommandCount = activeMutationCommandCount;
            ActiveMutationOwnerGroupCount = activeMutationOwnerGroupCount;
            ActiveMutationMaxOwnerRange = activeMutationMaxOwnerRange;
            ActiveMutationSortMoveCount = activeMutationSortMoveCount;
            ActiveMutationEstimatedRandomLookupCount = activeMutationEstimatedRandomLookupCount;
            ActiveMutationOwnerResourceLookupCount = activeMutationOwnerResourceLookupCount;
            ActiveMutationMigrationCarrierCount = activeMutationMigrationCarrierCount;
            PendingAttributeDeltaCount = pendingAttributeDeltaCount;
            PendingAttributeAppliedDeltaCount = pendingAttributeAppliedDeltaCount;
            PendingAttributeSkippedDeltaCount = pendingAttributeSkippedDeltaCount;
            PendingAttributeTargetGroupCount = pendingAttributeTargetGroupCount;
            PendingAttributeMaxTargetRange = pendingAttributeMaxTargetRange;
            PendingAttributeEstimatedRandomLookupCount = pendingAttributeEstimatedRandomLookupCount;
            PendingAttributeFactPatchCount = pendingAttributeFactPatchCount;
            PendingAttributeMigrationCarrierCount = pendingAttributeMigrationCarrierCount;
            StreamCarrierPressureWarningCount = streamCarrierPressureWarningCount;
            StreamCarrierPeakCount = streamCarrierPeakCount;
            StreamCarrierPeakCapacity = streamCarrierPeakCapacity;
            TotalElapsedMilliseconds = totalElapsedMilliseconds;
            AverageTickMilliseconds = averageTickMilliseconds;
            FactsHash = factsHash;
            SummaryHash = summaryHash;
            EcsRuntimeTickOnly = ecsRuntimeTickOnly;
            OfficialDiffSeparatePass = officialDiffSeparatePass;
            JournalingAvailable = journalingAvailable;
            JournalingCaptured = journalingCaptured;
            ProfilerAvailable = profilerAvailable;
            ProfilerCaptureState = profilerCaptureState ?? string.Empty;
            PhysicsDisabledReason = physicsDisabledReason ?? string.Empty;
            RenderDisabledReason = renderDisabledReason ?? string.Empty;
            PresentationDisabledReason = presentationDisabledReason ?? string.Empty;
        }
    }

    public readonly struct AutoChessBattleResult
    {
        public readonly string RoomId;
        public readonly bool Completed;
        public readonly AutoChessTeam Winner;
        public readonly int ScenarioScale;
        public readonly int BattleTicks;
        public readonly int TotalTicks;
        public readonly int WarmupDroppedTicks;
        public readonly int MeasuredTicks;
        public readonly int DriverIssuedCommands;
        public readonly int DriverIssuedPrimaryCommands;
        public readonly int DriverIssuedFinisherCommands;
        public readonly int DriverLowestHealthTargetSelections;
        public readonly long ElapsedTicks;
        public readonly double ElapsedMilliseconds;
        public readonly long MeasuredElapsedTicks;
        public readonly double MeasuredElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly AutoChessBattleRuntimeTiming RuntimeTiming;
        public readonly AutoChessBattleUnitResult[] Units;
        public readonly AutoChessBattleEventCounts EventCounts;
        public readonly GasRuntimeDiagnosticSnapshot RuntimeDiagnostics;
        public readonly string RuntimeDiagnosticsLog;
        public readonly GasRuntimeOfficialToolDiffSnapshot OfficialToolDiff;
        public readonly GasStructuredLogExportSnapshot StructuredLogSnapshot;
        public readonly AutoChessBattleReport BattleReport;
        public readonly AutoChessBattleLogSnapshot BattleLog;
        public readonly string AssertionLog;

        public AutoChessBattleResult(
            string roomId,
            bool completed,
            AutoChessTeam winner,
            int scenarioScale,
            int battleTicks,
            int totalTicks,
            int warmupDroppedTicks,
            int measuredTicks,
            int driverIssuedCommands,
            int driverIssuedPrimaryCommands,
            int driverIssuedFinisherCommands,
            int driverLowestHealthTargetSelections,
            long elapsedTicks,
            double elapsedMilliseconds,
            long measuredElapsedTicks,
            double measuredElapsedMilliseconds,
            AutoChessBattleRuntimeTiming runtimeTiming,
            AutoChessBattleUnitResult[] units,
            AutoChessBattleEventCounts eventCounts,
            GasRuntimeDiagnosticSnapshot runtimeDiagnostics,
            string runtimeDiagnosticsLog,
            GasRuntimeOfficialToolDiffSnapshot officialToolDiff,
            GasStructuredLogExportSnapshot structuredLogSnapshot,
            AutoChessBattleReport battleReport,
            AutoChessBattleLogSnapshot battleLog,
            string assertionLog)
        {
            RoomId = roomId ?? string.Empty;
            Completed = completed;
            Winner = winner;
            ScenarioScale = scenarioScale;
            BattleTicks = battleTicks;
            TotalTicks = totalTicks;
            WarmupDroppedTicks = warmupDroppedTicks;
            MeasuredTicks = measuredTicks;
            DriverIssuedCommands = driverIssuedCommands;
            DriverIssuedPrimaryCommands = driverIssuedPrimaryCommands;
            DriverIssuedFinisherCommands = driverIssuedFinisherCommands;
            DriverLowestHealthTargetSelections = driverLowestHealthTargetSelections;
            ElapsedTicks = elapsedTicks;
            ElapsedMilliseconds = elapsedMilliseconds;
            MeasuredElapsedTicks = measuredElapsedTicks;
            MeasuredElapsedMilliseconds = measuredElapsedMilliseconds;
            AverageTickMilliseconds = measuredTicks > 0 ? measuredElapsedMilliseconds / measuredTicks : 0d;
            RuntimeTiming = runtimeTiming;
            Units = units ?? Array.Empty<AutoChessBattleUnitResult>();
            EventCounts = eventCounts;
            RuntimeDiagnostics = runtimeDiagnostics;
            RuntimeDiagnosticsLog = runtimeDiagnosticsLog ?? string.Empty;
            OfficialToolDiff = officialToolDiff;
            StructuredLogSnapshot = structuredLogSnapshot;
            BattleReport = battleReport;
            BattleLog = battleLog;
            AssertionLog = assertionLog ?? string.Empty;
        }

        public AutoChessBattleResult WithOfficialToolDiff(GasRuntimeOfficialToolDiffSnapshot officialToolDiff)
        {
            return new AutoChessBattleResult(
                RoomId,
                Completed,
                Winner,
                ScenarioScale,
                BattleTicks,
                TotalTicks,
                WarmupDroppedTicks,
                MeasuredTicks,
                DriverIssuedCommands,
                DriverIssuedPrimaryCommands,
                DriverIssuedFinisherCommands,
                DriverLowestHealthTargetSelections,
                ElapsedTicks,
                ElapsedMilliseconds,
                MeasuredElapsedTicks,
                MeasuredElapsedMilliseconds,
                RuntimeTiming,
                Units,
                EventCounts,
                RuntimeDiagnostics,
                RuntimeDiagnosticsLog,
                officialToolDiff,
                StructuredLogSnapshot,
                BattleReport,
                BattleLog,
                AssertionLog);
        }
    }
}
