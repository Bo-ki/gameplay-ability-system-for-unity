using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 通过 production Catalog、正式 Stage-B 与唯一 WorldOwner 执行 Runtime V1 的四个最小验收向量。
    /// </summary>
    public static class RuntimeV1RunnableScenarioRunner
    {
        public const int FrozenScale = 1;
        public const int FrozenAscCount = 4;
        private const int MaxBattleTicks = 96;
        private const float VectorHealthMultiplier = 10f;
        private const float NumericTolerance = 0.001f;
        private const int StackThreePeriodCount = 3;
        private const int RefreshedStackPeriodCount = 4;

        /// <summary>
        /// 验证 Accepted 请求在 WholeTick fault 后只发布一个 SessionFault terminal，并同步关闭 Gate。
        /// </summary>
        public static RuntimeV1RunnableBoundaryEvidence RunBoundaryFaultVector()
        {
            try
            {
                using (var session = RuntimeV1ValidationSession.Open(VectorHealthMultiplier))
                    return ExecuteBoundaryFault(session);
            }
            catch (Exception exception)
            {
                return RuntimeV1RunnableBoundaryEvidence.FromFailure(exception);
            }
        }

        /// <summary>
        /// 验证 production Ability 的 Activate/Commit terminal、正常结束和 ASC 死亡收口。
        /// </summary>
        public static RuntimeV1RunnableAbilityEvidence RunAbilityVector()
        {
            try
            {
                var normalEnd = ExecuteNormalAbilityEnd();
                var ownerTerminal = ExecuteAbilityOwnerTerminal();
                var passed = normalEnd.Passed && ownerTerminal.Passed;
                var summary = "activate=" + normalEnd.ActivateTerminalCount
                              + ", commit=" + normalEnd.CommitTerminalCount
                              + ", normalEnd=" + normalEnd.NormalEndObserved
                              + ", ownerTerminal=" + ownerTerminal.OwnerTerminalObserved;
                return new RuntimeV1RunnableAbilityEvidence(
                    CreateVectorResult("Ability", passed, summary,
                        normalEnd.SemanticState + "|" + ownerTerminal.SemanticState,
                        normalEnd.State + "|" + ownerTerminal.State),
                    normalEnd.SupportProfileAdmission,
                    normalEnd.ActivateTerminalCount,
                    normalEnd.CommitTerminalCount,
                    normalEnd.NormalEndObserved,
                    ownerTerminal.OwnerTerminalObserved,
                    passed ? string.Empty : summary);
            }
            catch (Exception exception)
            {
                return RuntimeV1RunnableAbilityEvidence.FromFailure(exception);
            }
        }

        /// <summary>
        /// 验证 production 9203 的创建、合并、快照数值、逐层过期与最终移除。
        /// </summary>
        public static RuntimeV1RunnableGameplayEffectEvidence RunGameplayEffect9203Vector()
        {
            try
            {
                using (var session = RuntimeV1ValidationSession.Open(VectorHealthMultiplier))
                    return ExecuteGameplayEffect9203(session);
            }
            catch (Exception exception)
            {
                return RuntimeV1RunnableGameplayEffectEvidence.FromFailure(exception);
            }
        }

        /// <summary>
        /// 通过 production RunDefault 运行 Scale=1 标准战局并返回精确终局与事实哈希。
        /// </summary>
        public static RuntimeV1RunnableAutoChessEvidence RunAutoChessVector()
        {
            try
            {
                var bootstrapAdmission = GasStageBSpawnFaultReason.SessionLayout;
                var hooks = new AutoChessBattleProfileHooks(
                    () => bootstrapAdmission = AutoChessGasBattleEntityLifecycle.LastBootstrapFailure,
                    null);
                var options = new AutoChessBattleOptions(
                    MaxBattleTicks,
                    FrozenScale,
                    captureOfficialToolDiff: false,
                    debuggerEnabled: false,
                    captureSystemTimings: false,
                    captureBufferPressure: false);
                var result = AutoChessBattleManager.RunDefault(options, hooks);
                return CreateAutoChessEvidence(in result, bootstrapAdmission);
            }
            catch (Exception exception)
            {
                AutoChessBattleManager.ShutdownRuntime();
                return RuntimeV1RunnableAutoChessEvidence.FromFailure(exception);
            }
        }

        /// <summary>
        /// 对三个 Player 向量哈希生成最终聚合身份，字段顺序固定为 Ability、9203、AutoChess。
        /// </summary>
        public static string ComputeAggregateHash(
            RuntimeV1RunnableVectorResult ability,
            RuntimeV1RunnableVectorResult gameplayEffect9203,
            RuntimeV1RunnableVectorResult autoChess,
            bool semantic)
        {
            return ComputeHash(
                semantic ? ability.SemanticHash : ability.StateHash,
                semantic ? gameplayEffect9203.SemanticHash : gameplayEffect9203.StateHash,
                semantic ? autoChess.SemanticHash : autoChess.StateHash);
        }

        /// <summary>
        /// 破坏已 Ready Session 的 owner-plan 上限，验证 fault terminal 与 gate close 的同批次桥接。
        /// </summary>
        private static RuntimeV1RunnableBoundaryEvidence ExecuteBoundaryFault(
            RuntimeV1ValidationSession session)
        {
            session.OverrideOwnerPlanCapacity(0);
            var accepted = session.RequestEffect(0, 2, AutoChessBattleRules.GameplayEffectPlayerAttackDamage);
            Require(accepted.IsAccepted, "Boundary fault vector request was not accepted.");
            session.Tick();
            Require(session.Owner.Port.TryReadRequestTerminal(accepted.RequestKey, out var terminal),
                "Boundary fault vector terminal is missing.");
            var drained = session.Owner.Port.TryDrainRequestTerminals(out var terminals);
            var rejected = session.RequestEffect(0, 2, AutoChessBattleRules.GameplayEffectPlayerAttackDamage);
            var observation = session.ReadObservation();
            var passed = drained && terminals.Length == 1 && terminal.Equals(terminals[0])
                         && terminal.Status == GasRequestTerminalStatus.SessionFault
                         && terminal.FaultId != 0 && observation.FaultId == terminal.FaultId
                         && rejected.Status == GasCommandAcceptStatus.FaultClosed;
            var summary = "terminalCount=" + (drained ? terminals.Length : 0)
                          + ", status=" + terminal.Status
                          + ", gate=" + rejected.Status;
            return new RuntimeV1RunnableBoundaryEvidence(
                passed,
                session.SupportProfileAdmission,
                drained ? terminals.Length : 0,
                terminal.Status == GasRequestTerminalStatus.SessionFault,
                rejected.Status == GasCommandAcceptStatus.FaultClosed,
                summary,
                passed ? string.Empty : summary);
        }

        /// <summary>
        /// 执行 Activate→Commit→Completed，并读取同一激活槽的冻结终态。
        /// </summary>
        private static AbilitySubVector ExecuteNormalAbilityEnd()
        {
            using (var session = RuntimeV1ValidationSession.Open(VectorHealthMultiplier))
            {
                var grant = session.ResolveGrant(0, AutoChessBattleRules.AbilityPlayerAttack);
                var activate = session.RequestActivate(0, in grant);
                var activateTerminal = session.TickAndDrainSingleTerminal(
                    in activate, out var activateTerminalCount);
                Require(activateTerminal.AbilityResult == GasAbilityCommandResult.Activated,
                    "Ability Activate did not succeed.");
                var commit = session.RequestCommit(0, 2, activateTerminal.AbilityActivation);
                var commitTerminal = session.TickAndDrainSingleTerminal(
                    in commit, out var commitTerminalCount);
                var activation = session.ReadActivation(0, activateTerminal.AbilityActivation);
                var ended = commitTerminal.AbilityResult == GasAbilityCommandResult.Committed
                            && activation.Header.StorageState == GasSlabSlotState.Tombstone
                            && activation.Phase == GasAbilityActivationPhase.Ended
                            && activation.EndReason == GasAbilityEndReason.Completed
                            && activation.WasCancelled == 0;
                return new AbilitySubVector(
                    ended,
                    session.SupportProfileAdmission,
                    activateTerminalCount,
                    commitTerminalCount,
                    ended,
                    false,
                    activateTerminal.AbilityResult + "/" + commitTerminal.AbilityResult,
                    activation.Phase + "/" + activation.EndReason + "/" + activation.WasCancelled);
            }
        }

        /// <summary>
        /// 保持一个未提交激活存活，再以 production 9202 杀死 owner 并验证 OwnerTerminal 收口。
        /// </summary>
        private static AbilitySubVector ExecuteAbilityOwnerTerminal()
        {
            using (var session = RuntimeV1ValidationSession.Open(1f))
            {
                var grant = session.ResolveGrant(0, AutoChessBattleRules.AbilityPlayerAttack);
                var activate = session.RequestActivate(0, in grant);
                var terminal = session.TickAndDrainSingleTerminal(
                    in activate, out var activateTerminalCount);
                Require(terminal.AbilityResult == GasAbilityCommandResult.Activated,
                    "OwnerTerminal setup Activate did not succeed.");
                for (var index = 0; index < 9; index++)
                {
                    var damage = session.RequestEffect(
                        2, 0, AutoChessBattleRules.GameplayEffectEnemyAttackDamage);
                    var damageTerminal = session.TickAndDrainSingleTerminal(in damage, out _);
                    Require(damageTerminal.EffectResult == GasEffectRequestResult.AppliedInstant,
                        "OwnerTerminal setup damage did not apply.");
                }
                var activation = session.ReadActivation(0, terminal.AbilityActivation);
                var lifecycle = session.ReadLifecycle(0);
                var ended = lifecycle.State == GasAscLifecycleState.Dead
                            && activation.Header.StorageState == GasSlabSlotState.Tombstone
                            && activation.Phase == GasAbilityActivationPhase.Ended
                            && activation.EndReason == GasAbilityEndReason.OwnerTerminal
                            && activation.WasCancelled == 1;
                return new AbilitySubVector(
                    ended,
                    session.SupportProfileAdmission,
                    activateTerminalCount,
                    0,
                    false,
                    ended,
                    terminal.AbilityResult.ToString(),
                    lifecycle.State + "/" + activation.EndReason + "/" + activation.WasCancelled);
            }
        }

        /// <summary>
        /// 连续应用三次 9203，并沿三个 duration 窗口采集 period 数值和 stack 生命周期。
        /// </summary>
        private static RuntimeV1RunnableGameplayEffectEvidence ExecuteGameplayEffect9203(
            RuntimeV1ValidationSession session)
        {
            var terminals = new GasRequestTerminal[3];
            var terminalCount = 0;
            var applicationPeriodDeltas = new List<float>();
            var periodDeltas = new List<float>();
            for (var index = 0; index < terminals.Length; index++)
            {
                var accepted = session.RequestEffect(0, 2, AutoChessBattleRules.GameplayEffectPlayerPoison);
                terminals[index] = session.TickAndDrainSingleTerminal(in accepted, out var drainedCount);
                terminalCount += drainedCount;
                session.DrainPeriodDeltas(applicationPeriodDeltas);
            }
            Require(applicationPeriodDeltas.Count == 0,
                "Production 9203 emitted a period before the successful reapply reset window closed.");
            var activeHandle = terminals[0].ActiveEffect;
            var stacked = terminals[0].EffectResult == GasEffectRequestResult.CreatedActive
                          && terminals[1].EffectResult == GasEffectRequestResult.MergedStack
                          && terminals[2].EffectResult == GasEffectRequestResult.MergedStack
                          && terminals[0].ApplicationId != 0
                          && terminals[1].ApplicationId != 0
                          && terminals[2].ApplicationId != 0
                          && terminals[0].ApplicationId != terminals[1].ApplicationId
                          && terminals[1].ApplicationId != terminals[2].ApplicationId
                          && terminals[0].ApplicationId != terminals[2].ApplicationId
                          && activeHandle.IsValid
                          && terminals[1].ActiveEffect.Equals(activeHandle)
                          && terminals[2].ActiveEffect.Equals(activeHandle);
            var initial = session.ReadActiveEffect(2, activeHandle);
            Require(initial.StackCount == 3, "Production 9203 did not reach stack three.");
            var stackThreeDeltas = new List<float>();
            var stackTwo = AdvanceToExpiry(session, 2, activeHandle, initial.EndTick, stackThreeDeltas);
            var stackTwoDeltas = new List<float>();
            var stackOne = AdvanceToExpiry(session, 2, activeHandle, stackTwo.EndTick, stackTwoDeltas);
            var stackOneDeltas = new List<float>();
            AdvanceUntil(session, stackOne.EndTick, stackOneDeltas);
            var removed = !session.TryReadLiveActiveEffect(2, activeHandle, out _);
            periodDeltas.AddRange(stackThreeDeltas);
            periodDeltas.AddRange(stackTwoDeltas);
            periodDeltas.AddRange(stackOneDeltas);
            var numeric = IsExactPeriodStage(stackThreeDeltas, -14.4f, StackThreePeriodCount)
                          && IsExactPeriodStage(stackTwoDeltas, -9.6f, RefreshedStackPeriodCount)
                          && IsExactPeriodStage(stackOneDeltas, -4.8f, RefreshedStackPeriodCount);
            var passed = terminalCount == terminals.Length && stacked && numeric && stackTwo.StackCount == 2
                          && stackOne.StackCount == 1 && removed;
            var summary = "terminals=" + terminalCount
                          + ", stacks=3>2>1>0, periodStages="
                          + FormatDeltas(stackThreeDeltas) + "|"
                          + FormatDeltas(stackTwoDeltas) + "|"
                          + FormatDeltas(stackOneDeltas);
            return new RuntimeV1RunnableGameplayEffectEvidence(
                CreateVectorResult("GameplayEffect9203", passed, summary,
                    terminals[0].EffectResult + "/" + terminals[1].EffectResult + "/"
                    + terminals[2].EffectResult + "/" + FormatDeltas(periodDeltas),
                    initial.StackCount + "/" + stackTwo.StackCount + "/"
                    + stackOne.StackCount + "/" + removed),
                session.SupportProfileAdmission,
                terminalCount,
                stacked,
                numeric,
                removed,
                periodDeltas.ToArray(),
                passed ? string.Empty : summary);
        }

        /// <summary>
        /// 推进到指定 EndTick 并要求同一 active handle 仍存活，供逐层 expiry 验收。
        /// </summary>
        private static ActiveEffectSlot AdvanceToExpiry(
            RuntimeV1ValidationSession session,
            int targetIndex,
            in ActiveEffectHandle activeEffect,
            ulong endTick,
            List<float> periodDeltas)
        {
            AdvanceUntil(session, endTick, periodDeltas);
            return session.ReadActiveEffect(targetIndex, activeEffect);
        }

        /// <summary>
        /// 推进到目标 tick，并在每个 outer fence 后消费不可变 PeriodTick facts。
        /// </summary>
        private static void AdvanceUntil(
            RuntimeV1ValidationSession session,
            ulong targetTick,
            List<float> periodDeltas)
        {
            while (session.CurrentTick < targetTick)
            {
                session.Tick();
                session.DrainPeriodDeltas(periodDeltas);
            }
        }

        /// <summary>
        /// 将 production AutoChess 结果收缩为固定规模、故障与终局证据。
        /// </summary>
        private static RuntimeV1RunnableAutoChessEvidence CreateAutoChessEvidence(
            in AutoChessBattleResult result,
            GasStageBSpawnFaultReason bootstrapAdmission)
        {
            var playerAlive = 0;
            var enemyAlive = 0;
            var stateBuilder = new StringBuilder(256);
            for (var index = 0; index < result.Units.Length; index++)
            {
                var unit = result.Units[index];
                if (unit.Alive && unit.Team == AutoChessTeam.Player)
                    playerAlive++;
                if (unit.Alive && unit.Team == AutoChessTeam.Enemy)
                    enemyAlive++;
                stateBuilder.Append(unit.Id).Append(':').Append((int)unit.Team).Append(':')
                    .Append(unit.Alive ? 1 : 0).Append(':')
                    .Append(unit.Health.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            }
            var observation = result.RuntimeV1Observation;
            var sessionObservation = observation.SessionObservation;
            var passed = bootstrapAdmission == GasStageBSpawnFaultReason.None
                         && result.Completed && result.Winner == AutoChessTeam.Player
                         && result.ScenarioScale == FrozenScale
                         && result.Units.Length == FrozenAscCount
                         && observation.HasSessionObservation
                         && sessionObservation.Exists
                         && sessionObservation.SessionState == GasSessionLifecycleState.Terminalizing
                         && sessionObservation.FirstBattleState == GasBattleInstanceState.OutcomeFrozen
                         && !sessionObservation.FirstBattleIngressOpen
                         && sessionObservation.AscCount == FrozenAscCount
                         && sessionObservation.ReadyAscCount == FrozenAscCount
                         && playerAlive > 0 && enemyAlive == 0
                         && observation.FaultFactCount == 0
                         && observation.InvalidFactCount == 0
                         && observation.DrainFailureCount == 0;
            var summary = "winner=" + result.Winner + ", units=" + result.Units.Length
                           + ", alive=" + playerAlive + "/" + enemyAlive
                           + ", session=" + sessionObservation.SessionState
                           + ", battle=" + sessionObservation.FirstBattleState
                           + ", ingressOpen=" + sessionObservation.FirstBattleIngressOpen
                           + ", readyAscs=" + sessionObservation.ReadyAscCount + "/" + sessionObservation.AscCount
                           + ", facts=0x" + observation.BoundarySequenceHash.ToString("X8");
            return new RuntimeV1RunnableAutoChessEvidence(
                CreateVectorResult("AutoChess", passed, summary,
                    result.Winner + "/" + sessionObservation.SessionState + "/"
                    + sessionObservation.FirstBattleState + "/" + sessionObservation.FirstBattleIngressOpen + "/"
                    + sessionObservation.AscCount + "/" + sessionObservation.ReadyAscCount + "/"
                    + observation.BoundarySequenceHash.ToString("X8") + "/"
                    + observation.BoundaryFactCount,
                    stateBuilder.ToString() + sessionObservation.SessionState + "/"
                    + sessionObservation.FirstBattleState + "/" + sessionObservation.FirstBattleIngressOpen),
                bootstrapAdmission.ToString(),
                result.ScenarioScale,
                sessionObservation.AscCount,
                sessionObservation.ReadyAscCount,
                sessionObservation.SessionState,
                sessionObservation.FirstBattleState,
                sessionObservation.FirstBattleIngressOpen,
                result.Winner,
                playerAlive,
                enemyAlive,
                observation.FaultFactCount,
                observation.InvalidFactCount,
                observation.DrainFailureCount,
                passed ? string.Empty : summary);
        }

        /// <summary>
        /// 创建带 canonical SHA-256 的单向量结果。
        /// </summary>
        private static RuntimeV1RunnableVectorResult CreateVectorResult(
            string name,
            bool passed,
            string summary,
            string semanticState,
            string state)
        {
            return new RuntimeV1RunnableVectorResult
            {
                Name = name,
                Passed = passed,
                Summary = summary,
                SemanticHash = ComputeHash(name, semanticState),
                StateHash = ComputeHash(name, state),
            };
        }

        /// <summary>
        /// 对按调用顺序拼接的 UTF-8 字段计算 lowercase SHA-256。
        /// </summary>
        private static string ComputeHash(params string[] values)
        {
            var canonical = string.Join("\n", values ?? Array.Empty<string>());
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                    builder.Append(bytes[index].ToString("x2"));
                return builder.ToString();
            }
        }

        /// <summary>
        /// 验证一个 stack 生命周期阶段只产生精确数量的冻结 period 数值。
        /// </summary>
        private static bool IsExactPeriodStage(List<float> values, float expected, int expectedCount)
        {
            if (values.Count != expectedCount)
                return false;
            for (var index = 0; index < values.Count; index++)
                if (Math.Abs(values[index] - expected) > NumericTolerance)
                    return false;
            return true;
        }

        /// <summary>
        /// 将 period 数值按事实顺序编码为 invariant 文本。
        /// </summary>
        private static string FormatDeltas(List<float> values)
        {
            var builder = new StringBuilder(values.Count * 8);
            for (var index = 0; index < values.Count; index++)
            {
                if (index > 0)
                    builder.Append(',');
                builder.Append(values[index].ToString("R", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        /// <summary>
        /// 将未满足的冻结前置条件提升为显式验收失败。
        /// </summary>
        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        /// <summary>
        /// 保存 Ability 子向量的最小组合状态，避免将测试断言塞入 production Runner。
        /// </summary>
        private readonly struct AbilitySubVector
        {
            public readonly bool Passed;
            public readonly string SupportProfileAdmission;
            public readonly int ActivateTerminalCount;
            public readonly int CommitTerminalCount;
            public readonly bool NormalEndObserved;
            public readonly bool OwnerTerminalObserved;
            public readonly string SemanticState;
            public readonly string State;

            /// <summary>
            /// 创建一个 Ability 子向量结果。
            /// </summary>
            public AbilitySubVector(
                bool passed,
                string supportProfileAdmission,
                int activateTerminalCount,
                int commitTerminalCount,
                bool normalEndObserved,
                bool ownerTerminalObserved,
                string semanticState,
                string state)
            {
                Passed = passed;
                SupportProfileAdmission = supportProfileAdmission;
                ActivateTerminalCount = activateTerminalCount;
                CommitTerminalCount = commitTerminalCount;
                NormalEndObserved = normalEndObserved;
                OwnerTerminalObserved = ownerTerminalObserved;
                SemanticState = semanticState;
                State = state;
            }
        }

        /// <summary>
        /// 建立四单位 production Catalog Session，并封装验证层唯一允许的测试读写入口。
        /// </summary>
        private sealed class RuntimeV1ValidationSession : IDisposable
        {
            private readonly AutoChessGasBattleUnitHandle[] _handles;
            private readonly OwnerAscHandle[] _owners;
            private ulong _requestOrdinal;
            // 保存下一个未消费的结构化日志位置，避免验证层再次消费唯一 Boundary ring。
            private int _structuredLogCursor;

            public GasRuntimeWorldOwner Owner => AutoChessGasRuntimeHost.RuntimeOwner;
            public string SupportProfileAdmission { get; }
            public ulong CurrentTick => ReadObservation().CurrentTick;

            /// <summary>
            /// 保存已完成 Stage-B 的业务句柄与稳定 owner 映射。
            /// </summary>
            private RuntimeV1ValidationSession(
                AutoChessGasBattleUnitHandle[] handles,
                OwnerAscHandle[] owners,
                string supportProfileAdmission)
            {
                _handles = handles;
                _owners = owners;
                SupportProfileAdmission = supportProfileAdmission;
                _structuredLogCursor = AutoChessGasObservationGateway
                    .CreateStructuredLogSnapshot().EntryCount;
            }

            /// <summary>
            /// 安装 production Catalog、提交四单位 Stage-B 并推进两次 finalize maintenance。
            /// </summary>
            public static RuntimeV1ValidationSession Open(float healthMultiplier)
            {
                Require(AutoChessGasRuntimeHost.EnsureRuntimeInitialized(),
                    "Runtime V1 validation host failed to initialize.");
                var room = AutoChessGameRoomFactory.CreateDefaultRoom(FrozenScale, healthMultiplier);
                Require(room.Units.Length == FrozenAscCount,
                    "Runtime V1 validation room does not contain four units.");
                var handles = new AutoChessGasBattleUnitHandle[room.Units.Length];
                try
                {
                    for (var index = 0; index < handles.Length; index++)
                        handles[index] = AutoChessGasBattleEntityLifecycle.CreateBattleUnit(room.Units[index]);
                    Require(AutoChessGasBattleEntityLifecycle.TryBootstrapSession(handles),
                        "Runtime V1 validation Stage-B rejected production input: "
                        + AutoChessGasBattleEntityLifecycle.LastBootstrapFailure);
                    var admission = AutoChessGasBattleEntityLifecycle.LastBootstrapFailure.ToString();
                    TickBootstrapMaintenance();
                    var owners = ResolveOwners(handles);
                    return new RuntimeV1ValidationSession(handles, owners, admission);
                }
                catch
                {
                    DestroyHandles(handles);
                    AutoChessGasRuntimeHost.ShutdownRuntime();
                    throw;
                }
            }

            /// <summary>
            /// 删除业务句柄并关闭独立验证 World。
            /// </summary>
            public void Dispose()
            {
                DestroyHandles(_handles);
                AutoChessGasRuntimeHost.ShutdownRuntime();
            }

            /// <summary>
            /// 推进一次正式 outer batch。
            /// </summary>
            public void Tick()
            {
                var timing = default(AutoChessBattleRuntimeTiming);
                Require(AutoChessGasRuntimeTicker.TickRuntime(false, ref timing),
                    "Runtime V1 validation tick failed.");
            }

            /// <summary>
            /// 读取唯一 Session 的 managed observation。
            /// </summary>
            public GasRuntimeSessionObservation ReadObservation()
            {
                var observation = default(GasRuntimeSessionObservation);
                Require(Owner != null && Owner.TryReadSessionObservation(out observation),
                    "Runtime V1 validation session observation is unavailable.");
                return observation;
            }

            /// <summary>
            /// 解析指定单位的唯一 bootstrap grant。
            /// </summary>
            public GrantedAbilityHandle ResolveGrant(int unitIndex, int abilityDefinitionId)
            {
                var owner = _owners[unitIndex];
                var receipt = Owner.ResolveBootstrapGrantedAbility(in owner, abilityDefinitionId);
                Require(receipt.IsResolved, "Runtime V1 bootstrap grant resolution failed: " + receipt.Status);
                return receipt.GrantedAbility;
            }

            /// <summary>
            /// 提交当前 Tick due 的 Activate 请求。
            /// </summary>
            public GasCommandAcceptResult RequestActivate(
                int sourceIndex,
                in GrantedAbilityHandle grant)
            {
                var context = CreateContext(sourceIndex, -1);
                return Owner.Port.RequestActivate(
                    in context, in grant, BoundaryCommandPayloadDescriptor.None, ReadOnlySpan<byte>.Empty);
            }

            /// <summary>
            /// 提交带显式 production ASC target 的 Commit 请求。
            /// </summary>
            public GasCommandAcceptResult RequestCommit(
                int sourceIndex,
                int targetIndex,
                in AbilityActivationHandle activation)
            {
                var context = CreateContext(sourceIndex, targetIndex);
                return Owner.Port.RequestCommit(
                    in context, in activation, BoundaryCommandPayloadDescriptor.None, ReadOnlySpan<byte>.Empty);
            }

            /// <summary>
            /// 通过 AutoChess 唯一 typed bridge 提交 production ApplyEffect。
            /// </summary>
            public GasCommandAcceptResult RequestEffect(
                int sourceIndex,
                int targetIndex,
                int definitionId)
            {
                return AutoChessGasBattleEntityLifecycle.RequestApplyEffect(
                    _handles[sourceIndex], _handles[targetIndex], definitionId, CurrentTick);
            }

            /// <summary>
            /// 推进一次 tick 并从 Gate terminal ledger 实测且消费刚接受请求的唯一结果。
            /// </summary>
            public GasRequestTerminal TickAndDrainSingleTerminal(
                in GasCommandAcceptResult accepted,
                out int terminalCount)
            {
                Require(accepted.IsAccepted, "Runtime V1 validation request was not accepted: " + accepted.Status);
                Tick();
                Require(Owner.Port.TryDrainRequestTerminals(out var terminals),
                    "Runtime V1 request terminal ledger is empty.");
                terminalCount = terminals.Length;
                Require(terminalCount == 1 && terminals[0].RequestKey.Equals(accepted.RequestKey),
                    "Runtime V1 request terminal ledger did not contain exactly the accepted request.");
                return terminals[0];
            }

            /// <summary>
            /// 读取指定 activation handle 的 durable 槽状态。
            /// </summary>
            public AbilityActivationSlot ReadActivation(
                int unitIndex,
                in AbilityActivationHandle activation)
            {
                var entityManager = ResolveEntityManager();
                var entity = ResolveAscEntity(entityManager, _owners[unitIndex]);
                var values = entityManager.GetBuffer<AbilityActivationSlot>(entity, true);
                Require(activation.SlotIndex >= 0 && activation.SlotIndex < values.Length,
                    "Runtime V1 activation slot is outside durable storage.");
                return values[activation.SlotIndex];
            }

            /// <summary>
            /// 读取指定单位的 ASC 生命周期。
            /// </summary>
            public AscLifecycle ReadLifecycle(int unitIndex)
            {
                var entityManager = ResolveEntityManager();
                var entity = ResolveAscEntity(entityManager, _owners[unitIndex]);
                return entityManager.GetComponentData<AscLifecycle>(entity);
            }

            /// <summary>
            /// 读取同一 active handle 的 live durable 槽，不存在时显式失败。
            /// </summary>
            public ActiveEffectSlot ReadActiveEffect(
                int unitIndex,
                in ActiveEffectHandle activeEffect)
            {
                Require(TryReadLiveActiveEffect(unitIndex, activeEffect, out var slot),
                    "Runtime V1 active effect is not live.");
                return slot;
            }

            /// <summary>
            /// 尝试读取同一 active handle 的 live durable 槽。
            /// </summary>
            public bool TryReadLiveActiveEffect(
                int unitIndex,
                in ActiveEffectHandle activeEffect,
                out ActiveEffectSlot slot)
            {
                var entityManager = ResolveEntityManager();
                var entity = ResolveAscEntity(entityManager, _owners[unitIndex]);
                var values = entityManager.GetBuffer<ActiveEffectSlot>(entity, true);
                for (var index = 0; index < values.Length; index++)
                {
                    var candidate = values[index];
                    if (candidate.Header.StorageState == GasSlabSlotState.Live
                        && candidate.State == GasSlotBusinessState.Active
                        && candidate.Handle.Equals(activeEffect))
                    {
                        slot = candidate;
                        return true;
                    }
                }
                slot = default;
                return false;
            }

            /// <summary>
            /// 从唯一 Boundary consumer 的结构化日志增量采集 production 9203 PeriodTick current delta。
            /// </summary>
            public void DrainPeriodDeltas(List<float> destination)
            {
                var snapshot = AutoChessGasObservationGateway.CreateStructuredLogSnapshot();
                Require(_structuredLogCursor >= 0 && _structuredLogCursor <= snapshot.EntryCount,
                    "Runtime V1 structured log cursor regressed.");
                for (var index = _structuredLogCursor; index < snapshot.EntryCount; index++)
                {
                    var entry = snapshot.Entries[index];
                    if (entry.Module == EGasStructuredLogModule.Damage
                        && entry.FactDomain == EGameplayFactDomain.Damage
                        && entry.GameplayEventType == EGameplayEventType.RuntimeDamageApplied
                        && entry.ReplayKind == EDebugReplayEventKind.Damage
                        && entry.EventCode ==
                        (int)GasGameplayEffectApplicationOutcome.PeriodTickExecuted
                        && entry.ReasonCode == AutoChessBattleRules.GameplayEffectPlayerPoison)
                        destination.Add(entry.Value);
                }
                _structuredLogCursor = snapshot.EntryCount;
            }

            /// <summary>
            /// 仅用于 fault 向量，把已通过 Stage-B 的 owner plan 上限压到零。
            /// </summary>
            public void OverrideOwnerPlanCapacity(int capacity)
            {
                var entityManager = ResolveEntityManager();
                using (var query = entityManager.CreateEntityQuery(
                           ComponentType.ReadOnly<GasSessionIdentity>(),
                           ComponentType.ReadWrite<GasScaleProfile>()))
                {
                    Require(query.CalculateEntityCount() == 1,
                        "Runtime V1 validation Session cardinality is invalid.");
                    var session = query.GetSingletonEntity();
                    var profile = entityManager.GetComponentData<GasScaleProfile>(session);
                    profile.MaxOwnerPlanCount = capacity;
                    entityManager.SetComponentData(session, profile);
                }
            }

            /// <summary>
            /// 创建 source 与可选 target 均冻结的 Ability 请求上下文。
            /// </summary>
            private GasBoundaryCommandContext CreateContext(int sourceIndex, int targetIndex)
            {
                var battle = new BattleInstanceHandle(1, 1, 1);
                var source = _owners[sourceIndex];
                var targetOwner = targetIndex >= 0 ? _owners[targetIndex] : default;
                var target = targetIndex >= 0
                    ? BoundaryTargetRef.ForAsc(
                        in battle, in targetOwner, (ulong)(targetIndex + 1), 1)
                    : BoundaryTargetRef.None;
                var ordinal = ++_requestOrdinal;
                return new GasBoundaryCommandContext(
                    1, 0xA0000000UL + ordinal, 0xB0000000UL + ordinal, CurrentTick,
                    true, in battle, in source, in target);
            }

            /// <summary>
            /// 返回当前验证 World 的 EntityManager。
            /// </summary>
            private static EntityManager ResolveEntityManager()
            {
                var entityManager = default(EntityManager);
                Require(AutoChessGasRuntimeHost.TryResolveEntityManager(out entityManager),
                    "Runtime V1 validation EntityManager is unavailable.");
                return entityManager;
            }

            /// <summary>
            /// 以稳定 OwnerAscHandle 精确解析唯一 ASC 实体。
            /// </summary>
            private static Entity ResolveAscEntity(
                EntityManager entityManager,
                in OwnerAscHandle ownerAsc)
            {
                using (var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GasAscIdentity>()))
                using (var entities = query.ToEntityArray(Allocator.Temp))
                using (var identities = query.ToComponentDataArray<GasAscIdentity>(Allocator.Temp))
                {
                    var match = Entity.Null;
                    var count = 0;
                    for (var index = 0; index < identities.Length; index++)
                    {
                        if (!identities[index].OwnerAsc.Equals(ownerAsc))
                            continue;
                        match = entities[index];
                        count++;
                    }
                    Require(count == 1, "Runtime V1 validation ASC cardinality is invalid.");
                    return match;
                }
            }

            /// <summary>
            /// 推进 Stage-B playback 与 Ready publish 两个 maintenance batch。
            /// </summary>
            private static void TickBootstrapMaintenance()
            {
                var timing = default(AutoChessBattleRuntimeTiming);
                Require(AutoChessGasRuntimeTicker.TickRuntime(false, ref timing),
                    "Runtime V1 Stage-B playback tick failed.");
                Require(AutoChessGasRuntimeTicker.TickRuntime(false, ref timing),
                    "Runtime V1 Stage-B Ready tick failed.");
                Require(AutoChessGasRuntimeHost.RuntimeOwner.TryReadSessionObservation(out var observation)
                        && observation.ReadyAscCount == FrozenAscCount,
                    "Runtime V1 Stage-B did not publish four Ready ASCs.");
            }

            /// <summary>
            /// 解析全部业务句柄的稳定 owner 身份。
            /// </summary>
            private static OwnerAscHandle[] ResolveOwners(AutoChessGasBattleUnitHandle[] handles)
            {
                var owners = new OwnerAscHandle[handles.Length];
                for (var index = 0; index < handles.Length; index++)
                {
                    Require(AutoChessGasBattleEntityLifecycle.TryResolveOwnerAsc(
                            handles[index], out owners[index]),
                        "Runtime V1 business handle owner resolution failed.");
                }
                return owners;
            }

            /// <summary>
            /// 释放当前验证会话创建的全部业务句柄。
            /// </summary>
            private static void DestroyHandles(AutoChessGasBattleUnitHandle[] handles)
            {
                if (handles == null)
                    return;
                for (var index = 0; index < handles.Length; index++)
                    AutoChessGasBattleEntityLifecycle.DestroyBattleUnit(handles[index]);
            }
        }
    }

    /// <summary>
    /// 保存 Boundary fault-close 向量的精确 terminal 与 Gate 证据。
    /// </summary>
    public sealed class RuntimeV1RunnableBoundaryEvidence
    {
        public readonly bool Passed;
        public readonly string SupportProfileAdmission;
        public readonly int TerminalCount;
        public readonly bool SessionFaultObserved;
        public readonly bool GateClosed;
        public readonly string Summary;
        public readonly string Failure;

        /// <summary>
        /// 创建 Boundary fault-close 证据。
        /// </summary>
        public RuntimeV1RunnableBoundaryEvidence(
            bool passed,
            string supportProfileAdmission,
            int terminalCount,
            bool sessionFaultObserved,
            bool gateClosed,
            string summary,
            string failure)
        {
            Passed = passed;
            SupportProfileAdmission = supportProfileAdmission;
            TerminalCount = terminalCount;
            SessionFaultObserved = sessionFaultObserved;
            GateClosed = gateClosed;
            Summary = summary ?? string.Empty;
            Failure = failure ?? string.Empty;
        }

        /// <summary>
        /// 将未捕获异常转换为可断言的失败证据。
        /// </summary>
        public static RuntimeV1RunnableBoundaryEvidence FromFailure(Exception exception)
        {
            return new RuntimeV1RunnableBoundaryEvidence(
                false, string.Empty, 0, false, false, string.Empty, exception.ToString());
        }
    }

    /// <summary>
    /// 保存 Ability 向量的 terminal 计数与两类结束原因。
    /// </summary>
    public sealed class RuntimeV1RunnableAbilityEvidence
    {
        public readonly RuntimeV1RunnableVectorResult Vector;
        public readonly string SupportProfileAdmission;
        public readonly int ActivateTerminalCount;
        public readonly int CommitTerminalCount;
        public readonly bool NormalEndObserved;
        public readonly bool OwnerTerminalObserved;
        public readonly string Failure;

        /// <summary>
        /// 创建 Ability 向量证据。
        /// </summary>
        public RuntimeV1RunnableAbilityEvidence(
            RuntimeV1RunnableVectorResult vector,
            string supportProfileAdmission,
            int activateTerminalCount,
            int commitTerminalCount,
            bool normalEndObserved,
            bool ownerTerminalObserved,
            string failure)
        {
            Vector = vector;
            SupportProfileAdmission = supportProfileAdmission ?? string.Empty;
            ActivateTerminalCount = activateTerminalCount;
            CommitTerminalCount = commitTerminalCount;
            NormalEndObserved = normalEndObserved;
            OwnerTerminalObserved = ownerTerminalObserved;
            Failure = failure ?? string.Empty;
        }

        /// <summary>
        /// 将未捕获异常转换为可断言的失败证据。
        /// </summary>
        public static RuntimeV1RunnableAbilityEvidence FromFailure(Exception exception)
        {
            return new RuntimeV1RunnableAbilityEvidence(
                new RuntimeV1RunnableVectorResult
                {
                    Name = "Ability",
                    Passed = false,
                    Summary = exception.Message,
                },
                string.Empty, 0, 0, false, false, exception.ToString());
        }
    }

    /// <summary>
    /// 保存 9203 stack、period 数值与最终移除证据。
    /// </summary>
    public sealed class RuntimeV1RunnableGameplayEffectEvidence
    {
        public readonly RuntimeV1RunnableVectorResult Vector;
        public readonly string SupportProfileAdmission;
        public readonly int TerminalCount;
        public readonly bool StackTerminalSequenceObserved;
        public readonly bool NumericPeriodsObserved;
        public readonly bool FinalRemovalObserved;
        public readonly float[] PeriodDeltas;
        public readonly string Failure;

        /// <summary>
        /// 创建 9203 数值生命周期证据。
        /// </summary>
        public RuntimeV1RunnableGameplayEffectEvidence(
            RuntimeV1RunnableVectorResult vector,
            string supportProfileAdmission,
            int terminalCount,
            bool stackTerminalSequenceObserved,
            bool numericPeriodsObserved,
            bool finalRemovalObserved,
            float[] periodDeltas,
            string failure)
        {
            Vector = vector;
            SupportProfileAdmission = supportProfileAdmission ?? string.Empty;
            TerminalCount = terminalCount;
            StackTerminalSequenceObserved = stackTerminalSequenceObserved;
            NumericPeriodsObserved = numericPeriodsObserved;
            FinalRemovalObserved = finalRemovalObserved;
            PeriodDeltas = periodDeltas ?? Array.Empty<float>();
            Failure = failure ?? string.Empty;
        }

        /// <summary>
        /// 将未捕获异常转换为可断言的失败证据。
        /// </summary>
        public static RuntimeV1RunnableGameplayEffectEvidence FromFailure(Exception exception)
        {
            return new RuntimeV1RunnableGameplayEffectEvidence(
                new RuntimeV1RunnableVectorResult
                {
                    Name = "GameplayEffect9203",
                    Passed = false,
                    Summary = exception.Message,
                },
                string.Empty, 0, false, false, false, Array.Empty<float>(), exception.ToString());
        }
    }

    /// <summary>
    /// 保存 production AutoChess Scale=1 终局、故障计数与存活基数。
    /// </summary>
    public sealed class RuntimeV1RunnableAutoChessEvidence
    {
        public readonly RuntimeV1RunnableVectorResult Vector;
        public readonly string SupportProfileAdmission;
        public readonly int Scale;
        public readonly int AscCount;
        public readonly int ReadyAscCount;
        public readonly GasSessionLifecycleState SessionState;
        public readonly GasBattleInstanceState FirstBattleState;
        public readonly bool FirstBattleIngressOpen;
        public readonly AutoChessTeam Winner;
        public readonly int PlayerAliveCount;
        public readonly int EnemyAliveCount;
        public readonly int FaultFactCount;
        public readonly int InvalidFactCount;
        public readonly int DrainFailureCount;
        public readonly string Failure;

        /// <summary>
        /// 创建 AutoChess 终局证据。
        /// </summary>
        public RuntimeV1RunnableAutoChessEvidence(
            RuntimeV1RunnableVectorResult vector,
            string supportProfileAdmission,
            int scale,
            int ascCount,
            int readyAscCount,
            GasSessionLifecycleState sessionState,
            GasBattleInstanceState firstBattleState,
            bool firstBattleIngressOpen,
            AutoChessTeam winner,
            int playerAliveCount,
            int enemyAliveCount,
            int faultFactCount,
            int invalidFactCount,
            int drainFailureCount,
            string failure)
        {
            Vector = vector;
            SupportProfileAdmission = supportProfileAdmission ?? string.Empty;
            Scale = scale;
            AscCount = ascCount;
            ReadyAscCount = readyAscCount;
            SessionState = sessionState;
            FirstBattleState = firstBattleState;
            FirstBattleIngressOpen = firstBattleIngressOpen;
            Winner = winner;
            PlayerAliveCount = playerAliveCount;
            EnemyAliveCount = enemyAliveCount;
            FaultFactCount = faultFactCount;
            InvalidFactCount = invalidFactCount;
            DrainFailureCount = drainFailureCount;
            Failure = failure ?? string.Empty;
        }

        /// <summary>
        /// 将未捕获异常转换为可断言的失败证据。
        /// </summary>
        public static RuntimeV1RunnableAutoChessEvidence FromFailure(Exception exception)
        {
            return new RuntimeV1RunnableAutoChessEvidence(
                new RuntimeV1RunnableVectorResult
                {
                    Name = "AutoChess",
                    Passed = false,
                    Summary = exception.Message,
                },
                string.Empty, 0, 0, 0, default, default, false,
                AutoChessTeam.None, 0, 0, 0, 0, 0, exception.ToString());
        }
    }
}
