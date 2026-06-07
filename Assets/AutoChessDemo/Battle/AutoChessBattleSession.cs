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
            if (driverStats.LastOutcomeFrame >= 0)
            {
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

            RefreshUnits();
            var playerAlive = false;
            var enemyAlive = false;

            for (var i = 0; i < _units.Length; i++)
            {
                var unit = _units[i];
                if (!unit.Alive)
                    continue;

                if (unit.Definition.Team == AutoChessTeam.Player)
                    playerAlive = true;
                else if (unit.Definition.Team == AutoChessTeam.Enemy)
                    enemyAlive = true;
            }

            if (playerAlive && enemyAlive)
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

        public AutoChessBattleDriverComponent GetDriverStats()
        {
            return AutoChessGasCoreBridge.GetBattleDriverStats(DriverHandle);
        }

        public AutoChessBattleUnitResult[] CreateUnitResults()
        {
            RefreshUnits();

            var units = new AutoChessBattleUnitResult[_units.Length];
            for (var i = 0; i < _units.Length; i++)
            {
                var unit = _units[i];
                units[i] = new AutoChessBattleUnitResult(
                    unit.Definition.Id,
                    unit.Definition.DisplayName,
                    Room.GetPlayerName(unit.Definition.Team),
                    unit.Definition.ArchetypeName,
                    unit.Definition.Team,
                    unit.Definition.Slot,
                    unit.Health,
                    unit.Energy,
                    unit.Alive);
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

        private void RefreshUnits()
        {
            for (var i = 0; i < _units.Length; i++)
                _units[i] = _units[i].Refresh();
        }

        private readonly struct AutoChessBattleUnitRuntime
        {
            public readonly AutoChessUnitDefinition Definition;
            public readonly AutoChessGasBattleUnitHandle GasHandle;
            public readonly float Health;
            public readonly float Energy;
            public readonly bool Alive;

            public AutoChessBattleUnitRuntime(AutoChessUnitDefinition definition)
                : this(definition, default, definition.Health, definition.Energy, false)
            {
            }

            private AutoChessBattleUnitRuntime(
                AutoChessUnitDefinition definition,
                AutoChessGasBattleUnitHandle gasHandle,
                float health,
                float energy,
                bool alive)
            {
                Definition = definition;
                GasHandle = gasHandle;
                Health = health;
                Energy = energy;
                Alive = alive;
            }

            public AutoChessBattleUnitRuntime WithGasHandle(AutoChessGasBattleUnitHandle gasHandle)
            {
                var alive = gasHandle.IsValid;
                return new AutoChessBattleUnitRuntime(Definition, gasHandle, Health, Energy, alive);
            }

            public AutoChessBattleUnitRuntime Refresh()
            {
                var attributes = AutoChessGasCoreBridge.ReadCombatAttributes(GasHandle);
                return new AutoChessBattleUnitRuntime(
                    Definition,
                    GasHandle,
                    attributes.Health,
                    attributes.Energy,
                    attributes.Alive);
            }
        }
    }
}
