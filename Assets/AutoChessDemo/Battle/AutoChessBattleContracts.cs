using System;
using System.Diagnostics;
using Unity.Entities;
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
        public readonly Entity AscEntity;
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
            Entity ascEntity,
            AutoChessTeam team,
            int slot,
            float health,
            float energy,
            bool alive)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            OwnerName = string.IsNullOrWhiteSpace(ownerName)
                ? AutoChessBattleRules.GetTeamName(team)
                : ownerName;
            ArchetypeName = string.IsNullOrWhiteSpace(archetypeName) ? DisplayName : archetypeName;
            AscEntity = ascEntity;
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
            TagChanges = tagChanges;
            CueRequests = cueRequests;
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

        public void Add(
            long framePrepareTicks,
            long commandResolveTicks,
            long coreSimulationTicks,
            long structuralCommitTicks,
            long boundaryProjectionTicks)
        {
            var totalTicks = framePrepareTicks
                             + commandResolveTicks
                             + coreSimulationTicks
                             + structuralCommitTicks
                             + boundaryProjectionTicks;
            TickTotal.Add(totalTicks);
            FramePrepare.Add(framePrepareTicks);
            CommandResolve.Add(commandResolveTicks);
            CoreSimulation.Add(coreSimulationTicks);
            StructuralCommit.Add(structuralCommitTicks);
            BoundaryProjection.Add(boundaryProjectionTicks);
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
                BattleLog,
                AssertionLog);
        }
    }
}
