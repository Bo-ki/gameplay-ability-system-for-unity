using System;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal sealed class AutoChessBattleSession
    {
        public readonly AutoChessGameRoomDefinition Room;
        private readonly AutoChessBattleUnitRuntime[] _units;

        public Entity DriverEntity { get; private set; }

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

            DriverEntity = AutoChessGasCoreBridge.CreateBattleDriver();
        }

        public void CacheGrantedAbilityEntities()
        {
            for (var i = 0; i < _units.Length; i++)
                AutoChessGasCoreBridge.CacheGrantedAbilityEntities(_units[i].GasHandle);
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
            return AutoChessGasCoreBridge.GetBattleDriverStats(DriverEntity);
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
                    unit.GasHandle.AscEntity,
                    unit.Definition.Team,
                    unit.Definition.Slot,
                    unit.Health,
                    unit.Energy,
                    unit.Alive);
            }

            return units;
        }

        public void Close()
        {
            AutoChessGasCoreBridge.DestroyBattleDriver(DriverEntity);
            DriverEntity = Entity.Null;

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
                var alive = gasHandle.AscEntity != Entity.Null;
                return new AutoChessBattleUnitRuntime(Definition, gasHandle, Health, Energy, alive);
            }

            public AutoChessBattleUnitRuntime Refresh()
            {
                var health = AutoChessGasCoreBridge.ReadCombatAttribute(
                    GasHandle.AscEntity,
                    AutoChessBattleRules.AttributeHealth);
                var energy = AutoChessGasCoreBridge.ReadCombatAttribute(
                    GasHandle.AscEntity,
                    AutoChessBattleRules.AttributeEnergy);
                return new AutoChessBattleUnitRuntime(Definition, GasHandle, health, energy, health > 0f);
            }
        }
    }
}
