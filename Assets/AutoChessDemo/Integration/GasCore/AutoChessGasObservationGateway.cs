using System;
using System.Collections.Generic;
using GAS.Runtime;
using Unity.Entities;
using Unity.Mathematics;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 通过唯一 Runtime v1 managed Boundary ring 汇总 AutoChess 观测，避免旁路诊断成为事实源。
    /// </summary>
    internal static class AutoChessGasObservationGateway
    {
        private static readonly AutoChessGasV1ObservationAccumulator s_runtimeV1Observation =
            new AutoChessGasV1ObservationAccumulator();

        public static GasRuntimeOfficialToolDiffSnapshot UnavailableOfficialToolDiff
            => GasRuntimeOfficialToolDiffSnapshot.Unavailable;

        public static AutoChessGasCoreOfficialToolDiffCapture BeginOfficialToolDiffCapture()
        {
            return AutoChessGasRuntimeAccess.TryBeginOfficialToolDiffCapture(out var capture)
                ? capture
                : default;
        }

        /// <summary>
        /// 注册本局业务单位与 Runtime v1 owner 的只读映射，供事实投影恢复单位状态。
        /// </summary>
        public static void RegisterUnits(
            AutoChessUnitDefinition[] definitions,
            AutoChessGasBattleUnitHandle[] handles)
        {
            s_runtimeV1Observation.RegisterUnits(definitions, handles);
        }

        /// <summary>
        /// 从唯一 managed read model 读取指定 ScenarioUnitId 的最终状态。
        /// </summary>
        public static bool TryReadUnitState(
            ulong scenarioUnitId,
            out float health,
            out float energy,
            out bool alive)
        {
            return s_runtimeV1Observation.TryReadUnitState(
                scenarioUnitId, out health, out energy, out alive);
        }

        /// <summary>
        /// 导出由 Runtime v1 Boundary facts 派生的结构化日志快照；该日志不参与模拟写入。
        /// </summary>
        public static GasStructuredLogExportSnapshot CreateStructuredLogSnapshot()
        {
            return s_runtimeV1Observation.CreateStructuredLogSnapshot();
        }

        /// <summary>
        /// 清空当前战局的 v1 managed 观测状态；诊断快照不参与事实重置。
        /// </summary>
        public static void ResetObservationState(in AutoChessBattleOptions options)
        {
            s_runtimeV1Observation.Reset();
        }

        /// <summary>
        /// 在每个成功 TickBatch 后有界地消费 ring，记录事实、空 shell、失败与 high-water evidence。
        /// </summary>
        public static bool DrainRuntimeV1BoundaryBatches()
        {
            if (!AutoChessGasRuntimeAccess.TryReadBoundaryRingStats(
                    out var pendingCount,
                    out var capacity))
            {
                CaptureSessionObservation();
                return true;
            }

            s_runtimeV1Observation.ObserveRing(pendingCount, capacity);
            if (capacity <= 0 || pendingCount < 0)
            {
                s_runtimeV1Observation.RecordDrainFailure(
                    GasBoundaryDrainFailure.StateInvariantViolation);
                CaptureSessionObservation();
                return false;
            }

            var drainedCount = 0;
            while (AutoChessGasRuntimeAccess.TryDequeueBoundaryBatch(out var batch))
            {
                s_runtimeV1Observation.ObserveBatch(in batch);
                drainedCount++;
                if (drainedCount <= capacity)
                    continue;

                s_runtimeV1Observation.RecordDrainFailure(
                    GasBoundaryDrainFailure.StateInvariantViolation);
                CaptureSessionObservation();
                return false;
            }

            if (pendingCount > drainedCount)
            {
                s_runtimeV1Observation.RecordDrainFailure(
                    GasBoundaryDrainFailure.StateInvariantViolation);
            }

            if (AutoChessGasRuntimeAccess.TryReadBoundaryDrainFailure(out var failure) &&
                failure != GasBoundaryDrainFailure.None)
            {
                s_runtimeV1Observation.RecordDrainFailure(failure);
            }

            CaptureSessionObservation();
            return pendingCount <= drainedCount;
        }

        /// <summary>
        /// 生成只依赖 v1 immutable facts 与 Session observation 的核心快照。
        /// </summary>
        public static AutoChessGasCoreObservationSnapshot CreateObservationSnapshot()
        {
            DrainRuntimeV1BoundaryBatches();
            CaptureSessionObservation();
            var observation = s_runtimeV1Observation.CreateSnapshot();
            return new AutoChessGasCoreObservationSnapshot(
                s_runtimeV1Observation.CreateStructuredLogSnapshot(),
                GasRuntimeV1DiagnosticSnapshot.Empty,
                observation);
        }

        /// <summary>
        /// 捕获最新 Session 诊断，避免把 inbox/首 ASC 健康状态埋在 ECS 内部。
        /// </summary>
        private static void CaptureSessionObservation()
        {
            if (AutoChessGasRuntimeAccess.TryReadSessionObservation(out var observation))
                s_runtimeV1Observation.SetSessionObservation(in observation);
        }

    }

    /// <summary>
    /// 在 managed 侧以有界计数汇总 Boundary facts，并保留确定性 sequence hash 与 Session 诊断。
    /// </summary>
    internal sealed class AutoChessGasV1ObservationAccumulator
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private readonly Dictionary<ulong, AutoChessGasUnitObservationState> _unitStates =
            new Dictionary<ulong, AutoChessGasUnitObservationState>();
        private readonly Dictionary<OwnerAscHandle, ulong> _scenarioIdsByAsc =
            new Dictionary<OwnerAscHandle, ulong>();
        private readonly List<GasStructuredLogEntry> _structuredLogEntries =
            new List<GasStructuredLogEntry>(256);
        private uint _boundarySequenceHash;
        private GasRuntimeSessionObservation _sessionObservation;
        private bool _hasSessionObservation;
        private int _boundaryBatchCount;
        private int _boundaryFactCount;
        private int _gameplayFactCount;
        private int _teardownFactCount;
        private int _noFactReceiptCount;
        private int _deadShellCount;
        private int _attributeFactCount;
        private int _tagFactCount;
        private int _abilityLifecycleFactCount;
        private int _effectLifecycleFactCount;
        private int _executionCalculationFactCount;
        private int _periodTickFactCount;
        private float _periodTickDamageTotal;
        private int _cueFactCount;
        private int _battleOutcomeFactCount;
        private int _sessionLifecycleFactCount;
        private int _deathFactCount;
        private int _faultFactCount;
        private int _rejectedEffectFactCount;
        private int _negativeHealthFactCount;
        private float _negativeHealthTotal;
        private int _invalidFactCount;
        private int _ringHighWater;
        private int _ringCapacity;
        private int _drainFailureCount;
        private GasBoundaryDrainFailure _lastDrainFailure;

        /// <summary>
        /// 保存单个业务单位由 Boundary facts 投影出的 Health/Energy/dead 状态。
        /// </summary>
        private struct AutoChessGasUnitObservationState
        {
            public float Health;
            public float Energy;
            public byte Dead;
        }

        /// <summary>
        /// 创建空的 v1 事实累加器。
        /// </summary>
        public AutoChessGasV1ObservationAccumulator()
        {
            Reset();
        }

        /// <summary>
        /// 建立 ScenarioUnitId、OwnerAscHandle 与业务初始状态的单一只读映射。
        /// </summary>
        public void RegisterUnits(
            AutoChessUnitDefinition[] definitions,
            AutoChessGasBattleUnitHandle[] handles)
        {
            _unitStates.Clear();
            _scenarioIdsByAsc.Clear();
            if (definitions == null || handles == null)
                return;

            var count = Math.Min(definitions.Length, handles.Length);
            for (var index = 0; index < count; index++)
            {
                if (!AutoChessGasBattleEntityLifecycle.TryResolveOwnerAsc(
                        handles[index], out var ownerAsc))
                    continue;
                var scenarioUnitId = (ulong)(index + 1);
                _scenarioIdsByAsc[ownerAsc] = scenarioUnitId;
                _unitStates[scenarioUnitId] = new AutoChessGasUnitObservationState
                {
                    // Stage-B 把业务初值冻结进 Catalog；read model 必须从同一输入初始化，禁止复制默认常量。
                    Health = definitions[index].Health,
                    Energy = definitions[index].Energy,
                    Dead = 0,
                };
            }
        }

        /// <summary>
        /// 读取已注册单位的当前数值与生命状态，未知 ScenarioUnitId 显式失败。
        /// </summary>
        public bool TryReadUnitState(
            ulong scenarioUnitId,
            out float health,
            out float energy,
            out bool alive)
        {
            if (_unitStates.TryGetValue(scenarioUnitId, out var state))
            {
                health = state.Health;
                energy = state.Energy;
                alive = state.Dead == 0 && health > 0f;
                return true;
            }
            health = 0f;
            energy = 0f;
            alive = false;
            return false;
        }

        /// <summary>
        /// 将当前 managed 结构化事实复制为不可变导出快照。
        /// </summary>
        public GasStructuredLogExportSnapshot CreateStructuredLogSnapshot()
        {
            var entries = _structuredLogEntries.ToArray();
            var count = entries.Length;
            var stats = new GasReplaySinkStats(
                count > 0 ? entries[0].LogIndex : 0,
                count > 0 ? entries[count - 1].LogIndex : -1,
                count,
                count,
                0,
                0);
            return new GasStructuredLogExportSnapshot(
                new GasReplayCursor(0),
                false,
                stats,
                entries);
        }

        /// <summary>
        /// 清空战局边界计数并保留下一局独立的 hash 根。
        /// </summary>
        public void Reset()
        {
            _unitStates.Clear();
            _scenarioIdsByAsc.Clear();
            _structuredLogEntries.Clear();
            _boundarySequenceHash = FnvOffset;
            _sessionObservation = default;
            _hasSessionObservation = false;
            _boundaryBatchCount = 0;
            _boundaryFactCount = 0;
            _gameplayFactCount = 0;
            _teardownFactCount = 0;
            _noFactReceiptCount = 0;
            _deadShellCount = 0;
            _attributeFactCount = 0;
            _tagFactCount = 0;
            _abilityLifecycleFactCount = 0;
            _effectLifecycleFactCount = 0;
            _executionCalculationFactCount = 0;
            _periodTickFactCount = 0;
            _periodTickDamageTotal = 0f;
            _cueFactCount = 0;
            _battleOutcomeFactCount = 0;
            _sessionLifecycleFactCount = 0;
            _deathFactCount = 0;
            _faultFactCount = 0;
            _rejectedEffectFactCount = 0;
            _negativeHealthFactCount = 0;
            _negativeHealthTotal = 0f;
            _invalidFactCount = 0;
            _ringHighWater = 0;
            _ringCapacity = 0;
            _drainFailureCount = 0;
            _lastDrainFailure = GasBoundaryDrainFailure.None;
        }

        /// <summary>
        /// 记录 ring 当前排队量并更新 high-water/capacity 证据。
        /// </summary>
        public void ObserveRing(int pendingCount, int capacity)
        {
            if (pendingCount > _ringHighWater)
                _ringHighWater = pendingCount;
            if (capacity > _ringCapacity)
                _ringCapacity = capacity;
        }

        /// <summary>
        /// 记录最新 owner drain 失败，禁止把 ring 阻断静默折叠成零事实。
        /// </summary>
        public void RecordDrainFailure(GasBoundaryDrainFailure failure)
        {
            if (failure == GasBoundaryDrainFailure.None)
                return;
            Increment(ref _drainFailureCount);
            _lastDrainFailure = failure;
        }

        /// <summary>
        /// 保存最新 Session observation，供最终快照携带 tick diagnostics/inbox/首 ASC 状态。
        /// </summary>
        public void SetSessionObservation(in GasRuntimeSessionObservation observation)
        {
            _sessionObservation = observation;
            _hasSessionObservation = observation.Exists;
        }

        /// <summary>
        /// 消费一份 immutable batch 并按 schema 分类其中的每条事实。
        /// </summary>
        public void ObserveBatch(in GasBoundaryDrainBatch batch)
        {
            Increment(ref _boundaryBatchCount);
            if (!batch.Receipt.IsValid)
                Increment(ref _invalidFactCount);

            var facts = batch.Facts;
            if (facts == null)
            {
                Increment(ref _invalidFactCount);
                return;
            }
            if (batch.IsNoFact)
            {
                if (batch.Receipt.IsValid)
                {
                    Increment(ref _noFactReceiptCount);
                    Increment(ref _deadShellCount);
                }
                if (facts.Count != 0)
                    Increment(ref _invalidFactCount);
                return;
            }
            if (facts.Count != batch.Receipt.FactCount)
                Increment(ref _invalidFactCount);
            for (var index = 0; index < facts.Count; index++)
            {
                var fact = facts[index];
                ObserveFact(in fact);
            }
        }

        /// <summary>
        /// 生成当前累计值的不可变快照。
        /// </summary>
        public AutoChessGasV1ObservationSnapshot CreateSnapshot()
        {
            return new AutoChessGasV1ObservationSnapshot(
                _boundaryBatchCount,
                _boundaryFactCount,
                _gameplayFactCount,
                _teardownFactCount,
                _noFactReceiptCount,
                _deadShellCount,
                _attributeFactCount,
                _tagFactCount,
                _abilityLifecycleFactCount,
                _effectLifecycleFactCount,
                _executionCalculationFactCount,
                _periodTickFactCount,
                _periodTickDamageTotal,
                _cueFactCount,
                _battleOutcomeFactCount,
                _sessionLifecycleFactCount,
                _deathFactCount,
                _faultFactCount,
                _rejectedEffectFactCount,
                _structuredLogEntries.Count,
                _negativeHealthFactCount,
                _negativeHealthTotal,
                _invalidFactCount,
                _ringHighWater,
                _ringCapacity,
                _drainFailureCount,
                _lastDrainFailure,
                _boundarySequenceHash,
                _hasSessionObservation,
                _sessionObservation);
        }

        /// <summary>
        /// 分类单条 fact、检查已冻结 payload schema，并更新 sequence hash。
        /// </summary>
        private void ObserveFact(in BoundaryFactBuffer fact)
        {
            HashFact(in fact);
            if (!IsSupportedFact(in fact))
            {
                Increment(ref _invalidFactCount);
                return;
            }

            Increment(ref _boundaryFactCount);
            if (fact.Plane == GasBoundaryFactPlane.Gameplay)
                Increment(ref _gameplayFactCount);
            else if (fact.Plane == GasBoundaryFactPlane.TeardownAudit)
                Increment(ref _teardownFactCount);
            else
                Increment(ref _invalidFactCount);

            AppendStructuredLog(in fact);

            switch (fact.Kind)
            {
                case GasBoundaryFactKind.AttributeChanged:
                    Increment(ref _attributeFactCount);
                    ObserveAttribute(in fact);
                    break;
                case GasBoundaryFactKind.TagChanged:
                    Increment(ref _tagFactCount);
                    break;
                case GasBoundaryFactKind.AbilityLifecycle:
                    Increment(ref _abilityLifecycleFactCount);
                    break;
                case GasBoundaryFactKind.EffectLifecycle:
                    Increment(ref _effectLifecycleFactCount);
                    ObserveEffect(in fact);
                    break;
                case GasBoundaryFactKind.ExecutionCalculation:
                    Increment(ref _executionCalculationFactCount);
                    break;
                case GasBoundaryFactKind.PeriodTick:
                    Increment(ref _periodTickFactCount);
                    ObservePeriodTick(in fact);
                    break;
                case GasBoundaryFactKind.Cue:
                    Increment(ref _cueFactCount);
                    break;
                case GasBoundaryFactKind.BattleOutcome:
                    Increment(ref _battleOutcomeFactCount);
                    break;
                case GasBoundaryFactKind.SessionLifecycle:
                    Increment(ref _sessionLifecycleFactCount);
                    break;
                case GasBoundaryFactKind.Death:
                    Increment(ref _deathFactCount);
                    MarkDead(in fact);
                    break;
                case GasBoundaryFactKind.Fault:
                    Increment(ref _faultFactCount);
                    break;
            }
        }

        /// <summary>
        /// 记录 v1 AttributeDelta 中明确可识别的 AutoChess Health 负向变化，不推断其 Period 语义。
        /// </summary>
        private void ObserveAttribute(in BoundaryFactBuffer fact)
        {
            if (fact.Payload.Kind != GasBoundaryPayloadKind.AttributeDelta)
                return;
            var delta = fact.Payload.Scalar9;
            if (TryResolveScenarioId(in fact, out var scenarioUnitId) &&
                _unitStates.TryGetValue(scenarioUnitId, out var state))
            {
                if (fact.Payload.Integer0 == AutoChessBattleRules.AttributeHealth)
                {
                    state.Health = fact.Payload.Scalar7;
                    if (delta < 0f && !float.IsNaN(delta) && !float.IsInfinity(delta))
                    {
                        Increment(ref _negativeHealthFactCount);
                        _negativeHealthTotal += -delta;
                    }
                }
                else if (fact.Payload.Integer0 == AutoChessBattleRules.AttributeEnergy)
                {
                    state.Energy = fact.Payload.Scalar7;
                }
                _unitStates[scenarioUnitId] = state;
            }
        }

        /// <summary>
        /// 记录 period body 的伤害证据；真实数值仍以同 tick AttributeChanged fact 为准。
        /// </summary>
        private void ObservePeriodTick(in BoundaryFactBuffer fact)
        {
            var delta = fact.Payload.Scalar9;
            if (!float.IsNaN(delta) && !float.IsInfinity(delta) && delta < 0f)
                _periodTickDamageTotal += -delta;
        }

        /// <summary>
        /// 将 Death fact 固化为 read-model dead latch，阻断后续旧日志推断。
        /// </summary>
        private void MarkDead(in BoundaryFactBuffer fact)
        {
            if (!TryResolveScenarioId(in fact, out var scenarioUnitId) ||
                !_unitStates.TryGetValue(scenarioUnitId, out var state))
                return;
            state.Dead = 1;
            state.Health = Math.Min(state.Health, 0f);
            _unitStates[scenarioUnitId] = state;
        }

        /// <summary>
        /// 按冻结 outcome ordinal 统计 rejected application，未知 ordinal 显式记为 invalid。
        /// </summary>
        private void ObserveEffect(in BoundaryFactBuffer fact)
        {
            var outcome = fact.Payload.Integer0;
            if (outcome >= 4 && outcome <= 10)
                Increment(ref _rejectedEffectFactCount);
            else if (outcome < 0 || outcome > 10)
                Increment(ref _invalidFactCount);
        }

        /// <summary>
        /// 检查 Boundary fact 的稳定序号、闭世界 kind 与当前 v1 payload schema。
        /// </summary>
        private static bool IsSupportedFact(in BoundaryFactBuffer fact)
        {
            if (fact.EventId.OwnerSequence == 0 ||
                fact.Kind == GasBoundaryFactKind.None ||
                !IsKnownKind(fact.Kind))
                return false;
            switch (fact.Kind)
            {
                case GasBoundaryFactKind.AttributeChanged:
                    return fact.Payload.SchemaVersion == 1 &&
                           fact.Payload.Kind == GasBoundaryPayloadKind.AttributeDelta;
                case GasBoundaryFactKind.EffectLifecycle:
                    return fact.Payload.SchemaVersion == 1 &&
                           fact.Payload.Kind == GasBoundaryPayloadKind.IntegerPair;
                case GasBoundaryFactKind.ExecutionCalculation:
                case GasBoundaryFactKind.PeriodTick:
                    return fact.Payload.SchemaVersion == 1 &&
                           fact.Payload.Kind == GasBoundaryPayloadKind.IntegerPair;
                case GasBoundaryFactKind.Death:
                    return fact.Payload.SchemaVersion == 1 &&
                           fact.Payload.Kind == GasBoundaryPayloadKind.Death;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 判断 fact kind 是否属于 Runtime v1 冻结闭世界。
        /// </summary>
        private static bool IsKnownKind(GasBoundaryFactKind kind)
        {
            return kind == GasBoundaryFactKind.AttributeChanged ||
                   kind == GasBoundaryFactKind.TagChanged ||
                   kind == GasBoundaryFactKind.AbilityLifecycle ||
                   kind == GasBoundaryFactKind.EffectLifecycle ||
                   kind == GasBoundaryFactKind.Cue ||
                   kind == GasBoundaryFactKind.BattleOutcome ||
                   kind == GasBoundaryFactKind.SessionLifecycle ||
                   kind == GasBoundaryFactKind.Fault ||
                   kind == GasBoundaryFactKind.Death ||
                   kind == GasBoundaryFactKind.ExecutionCalculation ||
                   kind == GasBoundaryFactKind.PeriodTick;
        }

        /// <summary>
        /// 由 ASC membership 或稳定 owner 反查 ScenarioUnitId，保持事实投影不依赖 Entity。
        /// </summary>
        private bool TryResolveScenarioId(
            in BoundaryFactBuffer fact,
            out ulong scenarioUnitId)
        {
            if (fact.OwnerScenarioUnitId != 0 &&
                _unitStates.ContainsKey(fact.OwnerScenarioUnitId))
            {
                scenarioUnitId = fact.OwnerScenarioUnitId;
                return true;
            }
            if (fact.TargetAsc.IsValid && _scenarioIdsByAsc.TryGetValue(
                    fact.TargetAsc, out scenarioUnitId))
                return true;
            if (fact.SourceAsc.IsValid && _scenarioIdsByAsc.TryGetValue(
                    fact.SourceAsc, out scenarioUnitId))
                return true;
            scenarioUnitId = 0;
            return false;
        }

        /// <summary>
        /// 把 Boundary fact 映射为只读结构化日志；日志不回流 Runtime。
        /// </summary>
        private void AppendStructuredLog(in BoundaryFactBuffer fact)
        {
            var sourceReportKey = ResolveReportKey(in fact.SourceAsc);
            var targetReportKey = ResolveReportKey(in fact.TargetAsc);
            var frame = fact.SimulationTick > int.MaxValue
                ? int.MaxValue
                : (int)fact.SimulationTick;
            var sequence = fact.EventId.OwnerSequence > int.MaxValue
                ? int.MaxValue
                : (int)fact.EventId.OwnerSequence;
            var payload = fact.Payload;
            var replayKind = EDebugReplayEventKind.GameplayEvent;
            var module = EGasStructuredLogModule.RuntimeBoundary;
            var domain = EGameplayFactDomain.RuntimeBoundary;
            var category = EGameplayFactCategory.StateChange;
            var severity = EGameplayFactSeverity.Info;
            var gameplayEventType = EGameplayEventType.RuntimeBoundaryStateChange;
            var cueEvent = EGameplayCueEvent.Play;
            var eventCode = payload.Integer2 > int.MaxValue
                ? int.MaxValue
                : (int)payload.Integer2;
            var attributeCode = 0;
            var attrSetCode = 0;
            var value = payload.Scalar9;
            var oldValue = payload.Scalar3;
            var newValue = payload.Scalar7;
            var damage = payload.Scalar9 < 0f ? -payload.Scalar9 : 0f;

            switch (fact.Kind)
            {
                case GasBoundaryFactKind.AttributeChanged:
                    replayKind = EDebugReplayEventKind.AttributeChange;
                    module = EGasStructuredLogModule.Attribute;
                    domain = EGameplayFactDomain.Attribute;
                    gameplayEventType = EGameplayEventType.AttributeBaseValueChanged;
                    attrSetCode = AutoChessBattleRules.AttributeSetCombat;
                    attributeCode = payload.Integer0 > int.MaxValue
                        ? int.MaxValue
                        : (int)payload.Integer0;
                    break;
                case GasBoundaryFactKind.EffectLifecycle:
                    module = EGasStructuredLogModule.GameplayEffect;
                    domain = EGameplayFactDomain.GameplayEffect;
                    var outcome = payload.Integer0;
                    gameplayEventType = outcome >= 4
                        ? EGameplayEventType.GameplayEffectApplicationRejected
                        : outcome == 2
                            ? EGameplayEventType.StackCountChanged
                            : EGameplayEventType.GameplayEffectApplied;
                    category = outcome >= 4
                        ? EGameplayFactCategory.Failure
                        : EGameplayFactCategory.StateTransition;
                    severity = outcome >= 4
                        ? EGameplayFactSeverity.Warning
                        : EGameplayFactSeverity.Info;
                    break;
                case GasBoundaryFactKind.ExecutionCalculation:
                    module = EGasStructuredLogModule.ExecutionCalculation;
                    domain = EGameplayFactDomain.ExecutionCalculation;
                    gameplayEventType = EGameplayEventType.ExecutionCalculationOutputUpdated;
                    eventCode = AutoChessBattleRules.ExecutionCalculationExecuteDamage;
                    value = payload.Scalar0;
                    oldValue = payload.Scalar1;
                    newValue = payload.Scalar2;
                    break;
                case GasBoundaryFactKind.PeriodTick:
                    replayKind = EDebugReplayEventKind.Damage;
                    module = EGasStructuredLogModule.Damage;
                    domain = EGameplayFactDomain.Damage;
                    gameplayEventType = EGameplayEventType.RuntimeDamageApplied;
                    eventCode = payload.Integer2 > int.MaxValue
                        ? int.MaxValue
                        : (int)payload.Integer2;
                    break;
                case GasBoundaryFactKind.Cue:
                    replayKind = EDebugReplayEventKind.CueRequest;
                    module = EGasStructuredLogModule.GameplayCue;
                    domain = EGameplayFactDomain.Cue;
                    gameplayEventType = EGameplayEventType.CueRequested;
                    break;
                case GasBoundaryFactKind.TagChanged:
                    replayKind = EDebugReplayEventKind.TagChange;
                    module = EGasStructuredLogModule.GameplayTag;
                    domain = EGameplayFactDomain.Tag;
                    gameplayEventType = EGameplayEventType.TagChanged;
                    break;
                case GasBoundaryFactKind.Death:
                    replayKind = EDebugReplayEventKind.Damage;
                    module = EGasStructuredLogModule.Damage;
                    domain = EGameplayFactDomain.Damage;
                    gameplayEventType = EGameplayEventType.RuntimeDamageApplied;
                    damage = payload.Scalar0;
                    break;
                case GasBoundaryFactKind.Fault:
                    category = EGameplayFactCategory.Failure;
                    severity = EGameplayFactSeverity.Error;
                    gameplayEventType = EGameplayEventType.RuntimeBoundaryFailure;
                    break;
            }

            _structuredLogEntries.Add(new GasStructuredLogEntry(
                _structuredLogEntries.Count,
                frame,
                sequence,
                severity == EGameplayFactSeverity.Error
                    ? EGasStructuredLogLevel.Error
                    : severity == EGameplayFactSeverity.Warning
                        ? EGasStructuredLogLevel.Warning
                        : EGasStructuredLogLevel.Info,
                module,
                domain,
                category,
                severity,
                replayKind,
                gameplayEventType,
                cueEvent,
                sourceReportKey,
                targetReportKey,
                0,
                eventCode,
                (int)payload.Integer1,
                0,
                attrSetCode,
                attributeCode,
                0,
                value,
                oldValue,
                newValue,
                damage,
                0));
        }

        /// <summary>
        /// 将稳定 ASC owner 映射成业务报告键，未知 owner 保留零并由报告层显式忽略。
        /// </summary>
        private int ResolveReportKey(in OwnerAscHandle owner)
        {
            return owner.IsValid && _scenarioIdsByAsc.TryGetValue(owner, out var id) &&
                   id <= int.MaxValue
                ? (int)id
                : 0;
        }

        /// <summary>
        /// 将 fact 的稳定身份、语义值和 payload 写入 FNV sequence hash。
        /// </summary>
        private void HashFact(in BoundaryFactBuffer fact)
        {
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.EventId.OwnerSequence);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.SimulationTick);
            _boundarySequenceHash = Mix(_boundarySequenceHash, (uint)fact.Kind);
            _boundarySequenceHash = Mix(_boundarySequenceHash, (uint)fact.Scope);
            _boundarySequenceHash = Mix(_boundarySequenceHash, (uint)fact.Plane);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.ScopeStableId);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.ScopeGeneration);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.BattleInstanceId);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.BattleInstanceGeneration);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.OwnerScenarioUnitId);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.SemanticPhaseOrdinal);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.WorkClassOrdinal);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.ParentCausalityId);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.SemanticId);
            HashAsc(in fact.SourceAsc);
            HashAsc(in fact.TargetAsc);
            HashActiveEffect(in fact.CueActiveEffect);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.CueActiveCycleOrdinal);
            _boundarySequenceHash = Mix(_boundarySequenceHash, fact.CueDefinitionOrdinal);
            HashPayload(in fact.Payload);
        }

        /// <summary>
        /// 将 ASC stable identity 写入 sequence hash。
        /// </summary>
        private void HashAsc(in OwnerAscHandle handle)
        {
            _boundarySequenceHash = Mix(_boundarySequenceHash, handle.AscStableId);
            _boundarySequenceHash = Mix(_boundarySequenceHash, handle.AscGeneration);
        }

        /// <summary>
        /// 将 Cue active-effect stable identity 写入 sequence hash。
        /// </summary>
        private void HashActiveEffect(in ActiveEffectHandle handle)
        {
            _boundarySequenceHash = Mix(_boundarySequenceHash, handle.SimulationEpoch);
            HashAsc(in handle.OwnerAsc);
            _boundarySequenceHash = Mix(_boundarySequenceHash, unchecked((uint)handle.SlotIndex));
            _boundarySequenceHash = Mix(_boundarySequenceHash, handle.SlotGeneration);
        }

        /// <summary>
        /// 将 tagged payload 的数值字段写入 sequence hash。
        /// </summary>
        private void HashPayload(in BoundaryFactPayload payload)
        {
            _boundarySequenceHash = Mix(_boundarySequenceHash, payload.SchemaVersion);
            _boundarySequenceHash = Mix(_boundarySequenceHash, (uint)payload.Kind);
            _boundarySequenceHash = Mix(_boundarySequenceHash, unchecked((ulong)payload.Integer0));
            _boundarySequenceHash = Mix(_boundarySequenceHash, unchecked((ulong)payload.Integer1));
            _boundarySequenceHash = Mix(_boundarySequenceHash, payload.StableId0);
            _boundarySequenceHash = Mix(_boundarySequenceHash, payload.StableId1);
            _boundarySequenceHash = Mix(_boundarySequenceHash, payload.StableId2);
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar0));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar1));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar2));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar3));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar4));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar5));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar6));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar7));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar8));
            _boundarySequenceHash = Mix(_boundarySequenceHash, math.asuint(payload.Scalar9));
        }

        /// <summary>
        /// 混合一个 32 位值到确定性 FNV-1a hash。
        /// </summary>
        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }

        /// <summary>
        /// 以固定低字节优先顺序混合一个 64 位稳定值。
        /// </summary>
        private static uint Mix(uint hash, ulong value)
        {
            hash = Mix(hash, (uint)value);
            return Mix(hash, (uint)(value >> 32));
        }

        /// <summary>
        /// 对 bounded int 计数执行饱和递增，避免 evidence 溢出后静默回绕。
        /// </summary>
        private static void Increment(ref int value)
        {
            if (value < int.MaxValue)
                value++;
        }
    }
}
