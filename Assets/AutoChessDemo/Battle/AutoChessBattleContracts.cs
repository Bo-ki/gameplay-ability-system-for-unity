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

    /// <summary>
    /// 描述业务侧的静态目标选择偏好；Runtime v1 实际目标仍由稳定 BoundaryTargetRef 冻结。
    /// </summary>
    public enum AutoChessTargetPolicy : byte
    {
        Frontline = 0,
        LowestHealth = 1,
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

    /// <summary>
    /// 保存 AutoChess 从 Runtime v1 immutable Boundary ring 汇总出的事实与 Session 诊断快照。
    /// 该快照只描述已经由 managed gateway 接管的 Runtime v1 事实，不把旁路诊断计数冒充为业务语义。
    /// </summary>
    public readonly struct AutoChessGasV1ObservationSnapshot
    {
        public readonly int BoundaryBatchCount;
        public readonly int BoundaryFactCount;
        public readonly int GameplayFactCount;
        public readonly int TeardownFactCount;
        public readonly int NoFactReceiptCount;
        public readonly int DeadShellCount;
        public readonly int AttributeFactCount;
        public readonly int TagFactCount;
        public readonly int AbilityLifecycleFactCount;
        public readonly int EffectLifecycleFactCount;
        public readonly int ExecutionCalculationFactCount;
        public readonly int PeriodTickFactCount;
        public readonly float PeriodTickDamageTotal;
        public readonly int CueFactCount;
        public readonly int BattleOutcomeFactCount;
        public readonly int SessionLifecycleFactCount;
        public readonly int DeathFactCount;
        public readonly int FaultFactCount;
        public readonly int RejectedEffectFactCount;
        public readonly int StructuredLogEntryCount;
        public readonly int NegativeHealthFactCount;
        public readonly float NegativeHealthTotal;
        public readonly int InvalidFactCount;
        public readonly int RingHighWater;
        public readonly int RingCapacity;
        public readonly int DrainFailureCount;
        public readonly GasBoundaryDrainFailure LastDrainFailure;
        public readonly uint BoundarySequenceHash;
        public readonly bool HasSessionObservation;
        public readonly GasRuntimeSessionObservation SessionObservation;

        /// <summary>
        /// 创建不可变 Runtime v1 Boundary/Session 观测快照。
        /// </summary>
        public AutoChessGasV1ObservationSnapshot(
            int boundaryBatchCount,
            int boundaryFactCount,
            int gameplayFactCount,
            int teardownFactCount,
            int noFactReceiptCount,
            int deadShellCount,
            int attributeFactCount,
             int tagFactCount,
             int abilityLifecycleFactCount,
             int effectLifecycleFactCount,
             int executionCalculationFactCount,
             int periodTickFactCount,
             float periodTickDamageTotal,
             int cueFactCount,
            int battleOutcomeFactCount,
            int sessionLifecycleFactCount,
            int deathFactCount,
             int faultFactCount,
             int rejectedEffectFactCount,
             int structuredLogEntryCount,
             int negativeHealthFactCount,
            float negativeHealthTotal,
            int invalidFactCount,
            int ringHighWater,
            int ringCapacity,
            int drainFailureCount,
            GasBoundaryDrainFailure lastDrainFailure,
            uint boundarySequenceHash,
            bool hasSessionObservation,
            GasRuntimeSessionObservation sessionObservation)
        {
            BoundaryBatchCount = boundaryBatchCount;
            BoundaryFactCount = boundaryFactCount;
            GameplayFactCount = gameplayFactCount;
            TeardownFactCount = teardownFactCount;
            NoFactReceiptCount = noFactReceiptCount;
            DeadShellCount = deadShellCount;
            AttributeFactCount = attributeFactCount;
            TagFactCount = tagFactCount;
            AbilityLifecycleFactCount = abilityLifecycleFactCount;
            EffectLifecycleFactCount = effectLifecycleFactCount;
            ExecutionCalculationFactCount = executionCalculationFactCount;
            PeriodTickFactCount = periodTickFactCount;
            PeriodTickDamageTotal = periodTickDamageTotal;
            CueFactCount = cueFactCount;
            BattleOutcomeFactCount = battleOutcomeFactCount;
            SessionLifecycleFactCount = sessionLifecycleFactCount;
            DeathFactCount = deathFactCount;
            FaultFactCount = faultFactCount;
            RejectedEffectFactCount = rejectedEffectFactCount;
            StructuredLogEntryCount = structuredLogEntryCount;
            NegativeHealthFactCount = negativeHealthFactCount;
            NegativeHealthTotal = negativeHealthTotal;
            InvalidFactCount = invalidFactCount;
            RingHighWater = ringHighWater;
            RingCapacity = ringCapacity;
            DrainFailureCount = drainFailureCount;
            LastDrainFailure = lastDrainFailure;
            BoundarySequenceHash = boundarySequenceHash;
            HasSessionObservation = hasSessionObservation;
            SessionObservation = sessionObservation;
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
        public readonly int ExecutionSpecScanCount;
        public readonly int ExecutionMatchedEffectSpecCount;
        public readonly int ExecutionTargetOwnerMismatchCount;
        public readonly int ExecutionMissingAttributeCount;
        public readonly int ExecutionEvaluatorRejectCount;
        public readonly int ExecutionOutputWriteCount;
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
        public readonly int PeakBoundaryRingLength;
        public readonly int ReplayLag;
        public readonly int ProcessWarmupRuns;
        public readonly int ProofOnlyApiMask;
        public readonly int ReselectTriggerMask;
        public readonly int RuntimeAccessContractEntryCount;
        public readonly int RuntimeAccessEcsHandleProxyCount;
        public readonly int RuntimeAccessManualSyncCount;
        public readonly int RuntimeAccessPerformancePassRiskCount;
        public readonly int RuntimeAccessBattleHashAffectingCount;
        public readonly int RuntimeAccessCapabilityMask;
        public readonly int GasConceptCoverageMask;
        public readonly int GasConceptMissingMask;
        public readonly int RuntimeTraceAbilityCode;
        public readonly int RuntimeTraceStageMask;
        public readonly int RuntimeTraceMissingStageMask;
        public readonly int RuntimeTraceSeedCount;
        public readonly int RuntimeTraceModifierCount;
        public readonly int RuntimeTraceFactCount;
        public readonly int RuntimeTraceCueCount;
        public readonly int RuntimeTraceActiveMutationSeedCount;
        public readonly int RuntimeTraceExecutionCalculationModifierCount;
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
        public readonly int OwnerLocalFactCount;
        public readonly int OwnerLocalFactOwnerGroupCount;
        public readonly int OwnerLocalFactMaxOwnerRange;
        public readonly int OwnerLocalFactFlushCount;
        public readonly int OwnerLocalFactChangedChunkCount;
        public readonly int OwnerLocalFactScannedOwnerCount;
        public readonly int OwnerLocalFactDirtyOwnerCount;
        public readonly int OwnerLocalFactSkippedOwnerCount;
        public readonly int OwnerLocalFactClearedOwnerCount;
        public readonly int StreamCarrierPressureWarningCount;
        public readonly int StreamCarrierPeakCount;
        public readonly int StreamCarrierPeakCapacity;
        public readonly int ObservationMaterializedQueryCount;
        public readonly int ObservationMaterializedEntityCount;
        public readonly int ObservationMaterializationElapsedMicroseconds;
        public readonly int ObservationPerformancePollutionRiskCount;
        public readonly int MagnitudeSourceCurrentValueLookupCount;
        public readonly int MagnitudeSourceCapturedValueHitCount;
        public readonly int MagnitudeSourceCaptureMissCount;
        public readonly int MagnitudeSourceCaptureMissLiveLookupCount;
        public readonly int MagnitudeSourceFallbackValueCount;
        public readonly int MagnitudeSourceFallbackFactCount;
        public readonly int MagnitudeSourceSourceAttributeLookupCount;
        public readonly int MagnitudeSourceTargetAttributeLookupCount;
        public readonly int MagnitudeSourceExecutionInputLookupCount;
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
            int executionSpecScanCount,
            int executionMatchedEffectSpecCount,
            int executionTargetOwnerMismatchCount,
            int executionMissingAttributeCount,
            int executionEvaluatorRejectCount,
            int executionOutputWriteCount,
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
            int peakBoundaryRingLength,
            int replayLag,
            int processWarmupRuns,
            int proofOnlyApiMask,
            int reselectTriggerMask,
            int runtimeAccessContractEntryCount,
            int runtimeAccessEcsHandleProxyCount,
            int runtimeAccessManualSyncCount,
            int runtimeAccessPerformancePassRiskCount,
            int runtimeAccessBattleHashAffectingCount,
            int runtimeAccessCapabilityMask,
            int gasConceptCoverageMask,
            int gasConceptMissingMask,
            int runtimeTraceAbilityCode,
            int runtimeTraceStageMask,
            int runtimeTraceMissingStageMask,
            int runtimeTraceSeedCount,
            int runtimeTraceModifierCount,
            int runtimeTraceFactCount,
            int runtimeTraceCueCount,
            int runtimeTraceActiveMutationSeedCount,
            int runtimeTraceExecutionCalculationModifierCount,
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
            int ownerLocalFactCount,
            int ownerLocalFactOwnerGroupCount,
            int ownerLocalFactMaxOwnerRange,
            int ownerLocalFactFlushCount,
            int ownerLocalFactChangedChunkCount,
            int ownerLocalFactScannedOwnerCount,
            int ownerLocalFactDirtyOwnerCount,
            int ownerLocalFactSkippedOwnerCount,
            int ownerLocalFactClearedOwnerCount,
            int streamCarrierPressureWarningCount,
            int streamCarrierPeakCount,
            int streamCarrierPeakCapacity,
            int observationMaterializedQueryCount,
            int observationMaterializedEntityCount,
            int observationMaterializationElapsedMicroseconds,
            int observationPerformancePollutionRiskCount,
            int magnitudeSourceCurrentValueLookupCount,
            int magnitudeSourceCapturedValueHitCount,
            int magnitudeSourceCaptureMissCount,
            int magnitudeSourceCaptureMissLiveLookupCount,
            int magnitudeSourceFallbackValueCount,
            int magnitudeSourceFallbackFactCount,
            int magnitudeSourceSourceAttributeLookupCount,
            int magnitudeSourceTargetAttributeLookupCount,
            int magnitudeSourceExecutionInputLookupCount,
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
            ExecutionSpecScanCount = executionSpecScanCount;
            ExecutionMatchedEffectSpecCount = executionMatchedEffectSpecCount;
            ExecutionTargetOwnerMismatchCount = executionTargetOwnerMismatchCount;
            ExecutionMissingAttributeCount = executionMissingAttributeCount;
            ExecutionEvaluatorRejectCount = executionEvaluatorRejectCount;
            ExecutionOutputWriteCount = executionOutputWriteCount;
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
            PeakBoundaryRingLength = peakBoundaryRingLength;
            ReplayLag = replayLag;
            ProcessWarmupRuns = processWarmupRuns;
            ProofOnlyApiMask = proofOnlyApiMask;
            ReselectTriggerMask = reselectTriggerMask;
            RuntimeAccessContractEntryCount = runtimeAccessContractEntryCount;
            RuntimeAccessEcsHandleProxyCount = runtimeAccessEcsHandleProxyCount;
            RuntimeAccessManualSyncCount = runtimeAccessManualSyncCount;
            RuntimeAccessPerformancePassRiskCount = runtimeAccessPerformancePassRiskCount;
            RuntimeAccessBattleHashAffectingCount = runtimeAccessBattleHashAffectingCount;
            RuntimeAccessCapabilityMask = runtimeAccessCapabilityMask;
            GasConceptCoverageMask = gasConceptCoverageMask;
            GasConceptMissingMask = gasConceptMissingMask;
            RuntimeTraceAbilityCode = runtimeTraceAbilityCode;
            RuntimeTraceStageMask = runtimeTraceStageMask;
            RuntimeTraceMissingStageMask = runtimeTraceMissingStageMask;
            RuntimeTraceSeedCount = runtimeTraceSeedCount;
            RuntimeTraceModifierCount = runtimeTraceModifierCount;
            RuntimeTraceFactCount = runtimeTraceFactCount;
            RuntimeTraceCueCount = runtimeTraceCueCount;
            RuntimeTraceActiveMutationSeedCount = runtimeTraceActiveMutationSeedCount;
            RuntimeTraceExecutionCalculationModifierCount = runtimeTraceExecutionCalculationModifierCount;
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
            OwnerLocalFactCount = ownerLocalFactCount;
            OwnerLocalFactOwnerGroupCount = ownerLocalFactOwnerGroupCount;
            OwnerLocalFactMaxOwnerRange = ownerLocalFactMaxOwnerRange;
            OwnerLocalFactFlushCount = ownerLocalFactFlushCount;
            OwnerLocalFactChangedChunkCount = ownerLocalFactChangedChunkCount;
            OwnerLocalFactScannedOwnerCount = ownerLocalFactScannedOwnerCount;
            OwnerLocalFactDirtyOwnerCount = ownerLocalFactDirtyOwnerCount;
            OwnerLocalFactSkippedOwnerCount = ownerLocalFactSkippedOwnerCount;
            OwnerLocalFactClearedOwnerCount = ownerLocalFactClearedOwnerCount;
            StreamCarrierPressureWarningCount = streamCarrierPressureWarningCount;
            StreamCarrierPeakCount = streamCarrierPeakCount;
            StreamCarrierPeakCapacity = streamCarrierPeakCapacity;
            ObservationMaterializedQueryCount = observationMaterializedQueryCount;
            ObservationMaterializedEntityCount = observationMaterializedEntityCount;
            ObservationMaterializationElapsedMicroseconds = observationMaterializationElapsedMicroseconds;
            ObservationPerformancePollutionRiskCount = observationPerformancePollutionRiskCount;
            MagnitudeSourceCurrentValueLookupCount = magnitudeSourceCurrentValueLookupCount;
            MagnitudeSourceCapturedValueHitCount = magnitudeSourceCapturedValueHitCount;
            MagnitudeSourceCaptureMissCount = magnitudeSourceCaptureMissCount;
            MagnitudeSourceCaptureMissLiveLookupCount = magnitudeSourceCaptureMissLiveLookupCount;
            MagnitudeSourceFallbackValueCount = magnitudeSourceFallbackValueCount;
            MagnitudeSourceFallbackFactCount = magnitudeSourceFallbackFactCount;
            MagnitudeSourceSourceAttributeLookupCount = magnitudeSourceSourceAttributeLookupCount;
            MagnitudeSourceTargetAttributeLookupCount = magnitudeSourceTargetAttributeLookupCount;
            MagnitudeSourceExecutionInputLookupCount = magnitudeSourceExecutionInputLookupCount;
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
        public readonly int AcceptedCommandCount;
        public readonly long ElapsedTicks;
        public readonly double ElapsedMilliseconds;
        public readonly long MeasuredElapsedTicks;
        public readonly double MeasuredElapsedMilliseconds;
        public readonly double AverageTickMilliseconds;
        public readonly AutoChessBattleRuntimeTiming RuntimeTiming;
        public readonly AutoChessBattleUnitResult[] Units;
        public readonly AutoChessGasV1ObservationSnapshot RuntimeV1Observation;
        public readonly GasRuntimeDiagnosticSnapshot RuntimeDiagnostics;
        public readonly GasRuntimeOfficialToolDiffSnapshot OfficialToolDiff;
        public readonly GasStructuredLogExportSnapshot StructuredLogSnapshot;
        public readonly AutoChessBattleReport BattleReport;
        public readonly AutoChessBattleLogSnapshot BattleLog;

        public AutoChessBattleResult(
            string roomId,
            bool completed,
            AutoChessTeam winner,
            int scenarioScale,
            int battleTicks,
            int totalTicks,
            int warmupDroppedTicks,
            int measuredTicks,
            int acceptedCommandCount,
            long elapsedTicks,
            double elapsedMilliseconds,
            long measuredElapsedTicks,
            double measuredElapsedMilliseconds,
            AutoChessBattleRuntimeTiming runtimeTiming,
            AutoChessBattleUnitResult[] units,
            AutoChessGasV1ObservationSnapshot runtimeV1Observation,
            GasRuntimeDiagnosticSnapshot runtimeDiagnostics,
            GasRuntimeOfficialToolDiffSnapshot officialToolDiff,
            GasStructuredLogExportSnapshot structuredLogSnapshot,
            AutoChessBattleReport battleReport,
            AutoChessBattleLogSnapshot battleLog)
        {
            RoomId = roomId ?? string.Empty;
            Completed = completed;
            Winner = winner;
            ScenarioScale = scenarioScale;
            BattleTicks = battleTicks;
            TotalTicks = totalTicks;
            WarmupDroppedTicks = warmupDroppedTicks;
            MeasuredTicks = measuredTicks;
            AcceptedCommandCount = acceptedCommandCount;
            ElapsedTicks = elapsedTicks;
            ElapsedMilliseconds = elapsedMilliseconds;
            MeasuredElapsedTicks = measuredElapsedTicks;
            MeasuredElapsedMilliseconds = measuredElapsedMilliseconds;
            AverageTickMilliseconds = measuredTicks > 0 ? measuredElapsedMilliseconds / measuredTicks : 0d;
            RuntimeTiming = runtimeTiming;
            Units = units ?? Array.Empty<AutoChessBattleUnitResult>();
            RuntimeV1Observation = runtimeV1Observation;
            RuntimeDiagnostics = runtimeDiagnostics;
            OfficialToolDiff = officialToolDiff;
            StructuredLogSnapshot = structuredLogSnapshot;
            BattleReport = battleReport;
            BattleLog = battleLog;
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
                AcceptedCommandCount,
                ElapsedTicks,
                ElapsedMilliseconds,
                MeasuredElapsedTicks,
                MeasuredElapsedMilliseconds,
                RuntimeTiming,
                Units,
                RuntimeV1Observation,
                RuntimeDiagnostics,
                officialToolDiff,
                StructuredLogSnapshot,
                BattleReport,
                BattleLog);
        }
    }
}
