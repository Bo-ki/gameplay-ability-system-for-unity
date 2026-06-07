using System;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    internal readonly struct AutoChessBattleUnitKey : IEquatable<AutoChessBattleUnitKey>
    {
        private readonly int _value;

        private AutoChessBattleUnitKey(int value)
        {
            _value = value;
        }

        public bool IsValid => _value > 0;

        internal static AutoChessBattleUnitKey Create(int value)
        {
            return new AutoChessBattleUnitKey(value);
        }

        internal int ReportKey => _value;

        public bool Equals(AutoChessBattleUnitKey other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is AutoChessBattleUnitKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value;
        }

        public override string ToString()
        {
            return IsValid ? $"BattleUnit:{_value}" : "BattleUnit:Invalid";
        }
    }

    internal readonly struct AutoChessGasBattleUnitHandle
    {
        private readonly AutoChessBattleUnitKey _key;

        public AutoChessGasBattleUnitHandle(AutoChessBattleUnitKey key)
        {
            _key = key;
        }

        public bool IsValid => _key.IsValid;

        internal AutoChessBattleUnitKey Key => _key;
    }

    internal readonly struct AutoChessGasBattleDriverHandle
    {
        private readonly int _driverId;
        private readonly int _version;

        internal AutoChessGasBattleDriverHandle(int driverId, int version)
        {
            _driverId = driverId;
            _version = version;
        }

        public bool IsValid => _driverId > 0 && _version > 0;

        internal int DriverId => _driverId;

        internal int Version => _version;
    }

    internal readonly struct AutoChessCombatAttributeSnapshot
    {
        public readonly float Health;
        public readonly float Energy;
        public readonly bool Alive;

        public AutoChessCombatAttributeSnapshot(float health, float energy)
        {
            Health = health;
            Energy = energy;
            Alive = health > 0f;
        }
    }

    internal struct AutoChessGasCoreOfficialToolDiffCapture
    {
        private GasRuntimeOfficialToolDiffCapture _capture;
        private byte _started;

        public AutoChessGasCoreOfficialToolDiffCapture(GasRuntimeOfficialToolDiffCapture capture)
        {
            _capture = capture;
            _started = 1;
        }

        public GasRuntimeOfficialToolDiffSnapshot End()
        {
            if (_started == 0)
                return GasRuntimeOfficialToolDiffSnapshot.Unavailable;

            _started = 0;
            return _capture.End();
        }
    }

    internal readonly struct AutoChessGasCoreObservationSnapshot
    {
        public readonly GasStructuredLogExportSnapshot StructuredLog;
        public readonly string AssertionLog;
        public readonly GasRuntimeDiagnosticSnapshot RuntimeDiagnostics;
        public readonly string RuntimeDiagnosticsLog;
        public readonly AutoChessBattleEventCounts EventCounts;

        public AutoChessGasCoreObservationSnapshot(
            GasStructuredLogExportSnapshot structuredLog,
            string assertionLog,
            GasRuntimeDiagnosticSnapshot runtimeDiagnostics,
            string runtimeDiagnosticsLog,
            AutoChessBattleEventCounts eventCounts)
        {
            StructuredLog = structuredLog;
            AssertionLog = assertionLog ?? string.Empty;
            RuntimeDiagnostics = runtimeDiagnostics;
            RuntimeDiagnosticsLog = runtimeDiagnosticsLog ?? string.Empty;
            EventCounts = eventCounts;
        }
    }
}
