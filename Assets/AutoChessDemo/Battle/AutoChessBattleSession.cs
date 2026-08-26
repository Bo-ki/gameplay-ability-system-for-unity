using System;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal sealed class AutoChessBattleSession
    {
        public readonly AutoChessGameRoomDefinition Room;
        private readonly AutoChessBattleUnitRuntime[] _units;
        private bool _initialAttackQueued;
        private int _scheduledPhase;
        private int _acceptedCommandCount;
        public GasCommandAcceptStatus InitialAttackStatus { get; private set; }

        /// <summary>
        /// 返回本 Session 经唯一 CommandPort 接受的业务命令数，作为 v1 结果读模型字段。
        /// </summary>
        public int AcceptedCommandCount => _acceptedCommandCount;

        public AutoChessBattleSession(AutoChessGameRoomDefinition room)
        {
            Room = room;
            var definitions = room.Units ?? Array.Empty<AutoChessUnitDefinition>();
            _units = new AutoChessBattleUnitRuntime[definitions.Length];
            for (var i = 0; i < definitions.Length; i++)
                _units[i] = new AutoChessBattleUnitRuntime(definitions[i]);
        }

        public void Open()
        {
            var handles = new AutoChessGasBattleUnitHandle[_units.Length];
            for (var i = 0; i < _units.Length; i++)
            {
                var definition = _units[i].Definition;
                var handle = AutoChessGasCoreBridge.CreateBattleUnit(definition);
                _units[i] = _units[i].WithGasHandle(handle);
                handles[i] = handle;
            }

            if (!AutoChessGasBattleEntityLifecycle.TryBootstrapSession(handles))
                throw new InvalidOperationException(
                    "Runtime v1 AutoChess Stage-B SpawnBatch 录入失败，原因="
                    + AutoChessGasBattleEntityLifecycle.LastBootstrapFailure + "。");

            var definitionsForObservation = new AutoChessUnitDefinition[_units.Length];
            for (var i = 0; i < _units.Length; i++)
                definitionsForObservation[i] = _units[i].Definition;
            AutoChessGasObservationGateway.RegisterUnits(definitionsForObservation, handles);
        }

        /// <summary>
        /// 按输入顺序为所有玩家单位提交一次确定性 v1 ApplyEffect，供完整 Tick DAG 验证真实业务入口。
        /// </summary>
        public bool QueueInitialAttack(ulong availableTick)
        {
            if (_initialAttackQueued)
                return InitialAttackStatus == GasCommandAcceptStatus.Accepted ||
                       InitialAttackStatus == GasCommandAcceptStatus.DuplicateAccepted;

            var sources = FindUnits(AutoChessTeam.Player);
            var targets = FindUnits(AutoChessTeam.Enemy);
            if (sources.Length == 0 || targets.Length == 0)
                return false;

            var acceptedCount = 0;
            for (var index = 0; index < sources.Length; index++)
            {
                var result = AutoChessGasBattleEntityLifecycle.RequestApplyEffect(
                    sources[index],
                    targets[index % targets.Length],
                    AutoChessBattleRules.GameplayEffectPlayerAttackDamage,
                    availableTick);
                if (index == 0)
                    InitialAttackStatus = result.Status;
                if (result.IsAccepted)
                {
                    acceptedCount++;
                    _acceptedCommandCount++;
                }
            }

            _initialAttackQueued = true;
            return acceptedCount == sources.Length;
        }

        /// <summary>
        /// 按固定相位向唯一 Runtime v1 CommandPort 投递毒伤与执行命令，保证同一输入的命令序列稳定。
        /// </summary>
        public int QueueDeterministicCommands(ulong availableTick)
        {
            if (_scheduledPhase >= 4)
                return 0;

            // 先让首个 poison application 存活两个 tick，确保 due period 在 execute 前进入同一 Runtime v1 DAG。
            if (_scheduledPhase == 1 || _scheduledPhase == 2)
            {
                _scheduledPhase++;
                return 0;
            }

            var effectId = _scheduledPhase == 0
                ? AutoChessBattleRules.GameplayEffectPlayerPoison
                : AutoChessBattleRules.GameplayEffectPlayerExecute;
            var enemies = FindUnits(AutoChessTeam.Enemy);
            var acceptedCount = 0;
            var enemyOrdinal = 0;
            for (var index = 0; index < _units.Length; index++)
            {
                if (_units[index].Definition.Team != AutoChessTeam.Player ||
                    !_units[index].GasHandle.IsValid || enemies.Length == 0)
                    continue;

                var target = enemies[enemyOrdinal % enemies.Length];
                enemyOrdinal++;
                var result = AutoChessGasBattleEntityLifecycle.RequestApplyEffect(
                    _units[index].GasHandle,
                    target,
                    effectId,
                    availableTick);
                if (result.IsAccepted)
                {
                    acceptedCount++;
                    _acceptedCommandCount++;
                }
            }

            _scheduledPhase++;
            return acceptedCount;
        }

        public bool TryResolveWinner(out AutoChessTeam winner)
        {
            var playerAlive = false;
            var enemyAlive = false;
            var observedUnitCount = 0;
            for (var index = 0; index < _units.Length; index++)
            {
                if (!AutoChessGasObservationGateway.TryReadUnitState(
                        (ulong)(index + 1), out _, out _, out var alive))
                    continue;
                observedUnitCount++;
                if (!alive)
                    continue;
                if (_units[index].Definition.Team == AutoChessTeam.Player)
                    playerAlive = true;
                else if (_units[index].Definition.Team == AutoChessTeam.Enemy)
                    enemyAlive = true;
            }
            if (observedUnitCount != _units.Length || (playerAlive && enemyAlive))
            {
                winner = AutoChessTeam.None;
                return false;
            }

            winner = playerAlive == enemyAlive
                ? AutoChessTeam.Draw
                : playerAlive
                    ? AutoChessTeam.Player
                    : AutoChessTeam.Enemy;
            return true;
        }

        public AutoChessBattleUnitResult[] CreateUnitResults(
            in GasStructuredLogExportSnapshot structuredLog)
        {
            var units = new AutoChessBattleUnitResult[_units.Length];
            for (var i = 0; i < _units.Length; i++)
            {
                var unit = _units[i];
                var hasState = AutoChessGasObservationGateway.TryReadUnitState(
                    (ulong)(i + 1), out var health, out var energy, out var alive);
                units[i] = new AutoChessBattleUnitResult(
                    unit.Definition.Id,
                    unit.Definition.DisplayName,
                    Room.GetPlayerName(unit.Definition.Team),
                    unit.Definition.ArchetypeName,
                    unit.Definition.Team,
                    unit.Definition.Slot,
                    hasState ? health : unit.Definition.Health,
                    hasState ? energy : unit.Definition.Energy,
                    hasState && alive);
            }

            return units;
        }

        public AutoChessBattleReportFact[] CreateReportFacts(
            in GasStructuredLogExportSnapshot structuredLog)
        {
            var handles = new AutoChessGasBattleUnitHandle[_units.Length];
            for (var i = 0; i < _units.Length; i++)
                handles[i] = _units[i].GasHandle;

            return AutoChessGasCoreBridge.CreateReportFacts(structuredLog, handles);
        }

        /// <summary>
        /// 从 Runtime v1 managed read model 导出自包含结构化事实，供报告投影使用。
        /// </summary>
        public GasStructuredLogExportSnapshot CreateStructuredLogSnapshot()
        {
            return AutoChessGasObservationGateway.CreateStructuredLogSnapshot();
        }

        public void Close()
        {
            for (var i = 0; i < _units.Length; i++)
            {
                AutoChessGasCoreBridge.DestroyBattleUnit(_units[i].GasHandle);
                _units[i] = _units[i].WithGasHandle(default);
            }
        }

        /// <summary>
        /// 按房间输入顺序复制指定阵营的有效单位句柄，供调度器建立确定性目标配对。
        /// </summary>
        private AutoChessGasBattleUnitHandle[] FindUnits(AutoChessTeam team)
        {
            var count = 0;
            for (var index = 0; index < _units.Length; index++)
            {
                if (_units[index].Definition.Team == team && _units[index].GasHandle.IsValid)
                    count++;
            }

            var result = new AutoChessGasBattleUnitHandle[count];
            var writeIndex = 0;
            for (var index = 0; index < _units.Length; index++)
            {
                if (_units[index].Definition.Team != team || !_units[index].GasHandle.IsValid)
                    continue;
                result[writeIndex++] = _units[index].GasHandle;
            }

            return result;
        }

        private readonly struct AutoChessBattleUnitRuntime
        {
            public readonly AutoChessUnitDefinition Definition;
            public readonly AutoChessGasBattleUnitHandle GasHandle;

            public AutoChessBattleUnitRuntime(AutoChessUnitDefinition definition)
                : this(definition, default)
            {
            }

            private AutoChessBattleUnitRuntime(
                AutoChessUnitDefinition definition,
                AutoChessGasBattleUnitHandle gasHandle)
            {
                Definition = definition;
                GasHandle = gasHandle;
            }

            public AutoChessBattleUnitRuntime WithGasHandle(AutoChessGasBattleUnitHandle gasHandle)
            {
                return new AutoChessBattleUnitRuntime(Definition, gasHandle);
            }
        }
    }
}
