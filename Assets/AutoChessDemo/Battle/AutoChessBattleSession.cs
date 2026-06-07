using System;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal sealed class AutoChessBattleSession
    {
        public readonly AutoChessGameRoomDefinition Room;
        private readonly AutoChessBattleUnitRuntime[] _units;

        public AutoChessGasBattleDriverHandle DriverHandle { get; private set; }

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
            for (var i = 0; i < _units.Length; i++)
            {
                var definition = _units[i].Definition;
                var handle = AutoChessGasCoreBridge.CreateBattleUnit(definition);
                _units[i] = _units[i].WithGasHandle(handle);
            }

            DriverHandle = AutoChessGasCoreBridge.CreateBattleDriver();
        }

        public bool TryResolveWinner(out AutoChessTeam winner)
        {
            var driverStats = GetDriverStats();
            if (driverStats.LastOutcomeFrame < 0)
            {
                winner = AutoChessTeam.None;
                return false;
            }

            var playerAliveFromDriver = driverStats.PlayerAliveCount > 0;
            var enemyAliveFromDriver = driverStats.EnemyAliveCount > 0;
            if (playerAliveFromDriver && enemyAliveFromDriver)
            {
                winner = AutoChessTeam.None;
                return false;
            }

            winner = playerAliveFromDriver == enemyAliveFromDriver
                ? AutoChessTeam.Draw
                : playerAliveFromDriver
                    ? AutoChessTeam.Player
                    : AutoChessTeam.Enemy;
            return true;
        }

        public AutoChessBattleDriverComponent GetDriverStats()
        {
            return AutoChessGasCoreBridge.GetBattleDriverStats(DriverHandle);
        }

        public AutoChessBattleUnitResult[] CreateUnitResults(
            in GasStructuredLogExportSnapshot structuredLog)
        {
            var snapshots = CreateCombatAttributeSnapshots(structuredLog);
            var units = new AutoChessBattleUnitResult[_units.Length];
            for (var i = 0; i < _units.Length; i++)
            {
                var unit = _units[i];
                var snapshot = i < snapshots.Length ? snapshots[i] : default;
                units[i] = new AutoChessBattleUnitResult(
                    unit.Definition.Id,
                    unit.Definition.DisplayName,
                    Room.GetPlayerName(unit.Definition.Team),
                    unit.Definition.ArchetypeName,
                    unit.Definition.Team,
                    unit.Definition.Slot,
                    snapshot.Health,
                    snapshot.Energy,
                    snapshot.Alive);
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

        public void Close()
        {
            AutoChessGasCoreBridge.CloseBattleDriver(DriverHandle);
            DriverHandle = default;

            for (var i = 0; i < _units.Length; i++)
            {
                AutoChessGasCoreBridge.DestroyBattleUnit(_units[i].GasHandle);
                _units[i] = _units[i].WithGasHandle(default);
            }
        }

        private AutoChessCombatAttributeSnapshot[] CreateCombatAttributeSnapshots(
            in GasStructuredLogExportSnapshot structuredLog)
        {
            var handles = new AutoChessGasBattleUnitHandle[_units.Length];
            var definitions = new AutoChessUnitDefinition[_units.Length];
            for (var i = 0; i < _units.Length; i++)
            {
                handles[i] = _units[i].GasHandle;
                definitions[i] = _units[i].Definition;
            }

            return AutoChessGasBattleUnitSnapshotProjector.Project(
                structuredLog,
                handles,
                definitions);
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
