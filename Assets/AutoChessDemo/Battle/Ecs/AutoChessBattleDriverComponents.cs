using Unity.Entities;
using GAS.Runtime;

namespace GAS.AutoChessDemo
{
    public enum AutoChessTargetPolicy : byte
    {
        Frontline = 0,
        LowestHealth = 1,
    }

    public struct AutoChessBattleDriverComponent : IComponentData
    {
        public bool Enabled;
        public int LastDecisionFrame;
        public int LastExecutionFrame;
        public int LastOutcomeFrame;
        public int PlayerAliveCount;
        public int EnemyAliveCount;
        public int IssuedCommandCount;
        public int IssuedPrimaryCommandCount;
        public int IssuedFinisherCommandCount;
        public int IssuedActiveCommandCount;
        public int LowestHealthTargetCount;
        public int ExecutionSpecScanCount;
        public int ExecutionMatchedEffectSpecCount;
        public int ExecutionTargetOwnerMismatchCount;
        public int ExecutionMissingAttributeCount;
        public int ExecutionEvaluatorRejectCount;
        public int ExecutionOutputWriteCount;
    }

    public readonly struct AutoChessBattleDriverOwnerSnapshot
    {
        public readonly bool OwnerInstalled;
        public readonly bool OwnerEnabled;
        public readonly bool HandleMatched;
        public readonly int DriverId;
        public readonly int Version;
        public readonly int StructuralCreateCount;
        public readonly int EnableCount;
        public readonly int DisableCount;
        public readonly int UninstallCount;

        public AutoChessBattleDriverOwnerSnapshot(
            bool ownerInstalled,
            bool ownerEnabled,
            bool handleMatched,
            int driverId,
            int version,
            int structuralCreateCount,
            int enableCount,
            int disableCount,
            int uninstallCount)
        {
            OwnerInstalled = ownerInstalled;
            OwnerEnabled = ownerEnabled;
            HandleMatched = handleMatched;
            DriverId = driverId;
            Version = version;
            StructuralCreateCount = structuralCreateCount;
            EnableCount = enableCount;
            DisableCount = disableCount;
            UninstallCount = uninstallCount;
        }
    }

    internal static class AutoChessBattleDriverRuntimeStore
    {
        private static int _nextDriverId;
        private static int _driverId;
        private static int _driverVersion;
        private static int _structuralCreateCount;
        private static int _enableCount;
        private static int _disableCount;
        private static int _uninstallCount;
        private static World _driverWorld;
        private static Entity _driverEntity;

        public static void Ensure(EntityManager entityManager)
        {
            EnsureEntity(entityManager);
        }

        public static AutoChessGasBattleDriverHandle ResetAndEnable(EntityManager entityManager)
        {
            var driver = EnsureEntity(entityManager);
            if (driver == Entity.Null)
                return default;

            AdvanceDriverVersion();
            _enableCount++;
            entityManager.SetComponentData(driver, CreateInitialState(enabled: true));
            return CreateHandle();
        }

        public static AutoChessBattleDriverComponent Read(
            EntityManager entityManager,
            AutoChessGasBattleDriverHandle handle)
        {
            return TryResolveOwnedDriver(entityManager, handle, out var driver)
                ? entityManager.GetComponentData<AutoChessBattleDriverComponent>(driver)
                : default;
        }

        public static void Disable(
            EntityManager entityManager,
            AutoChessGasBattleDriverHandle handle)
        {
            if (!TryResolveOwnedDriver(entityManager, handle, out var driver))
                return;

            var state = entityManager.GetComponentData<AutoChessBattleDriverComponent>(driver);
            state.Enabled = false;
            entityManager.SetComponentData(driver, state);
            _disableCount++;
            AdvanceDriverVersion();
        }

        public static void Uninstall(EntityManager entityManager)
        {
            _uninstallCount++;
            if (!CanUse(entityManager))
            {
                Reset();
                return;
            }

            if (_driverWorld == entityManager.World
                && _driverEntity != Entity.Null
                && entityManager.Exists(_driverEntity))
            {
                entityManager.SetComponentData(_driverEntity, CreateInitialState(enabled: false));
                ResetHandleState();
                return;
            }

            Reset();
        }

        public static AutoChessBattleDriverOwnerSnapshot CreateOwnerSnapshot(
            EntityManager entityManager,
            AutoChessGasBattleDriverHandle handle)
        {
            var ownerInstalled = TryResolveInstalledDriver(entityManager, out var driver);
            var ownerEnabled = ownerInstalled
                               && entityManager.GetComponentData<AutoChessBattleDriverComponent>(driver).Enabled;
            return new AutoChessBattleDriverOwnerSnapshot(
                ownerInstalled,
                ownerEnabled,
                IsCurrentHandle(handle),
                _driverId,
                _driverVersion,
                _structuralCreateCount,
                _enableCount,
                _disableCount,
                _uninstallCount);
        }

        public static void ResetRuntimeCache()
        {
            Reset();
            _structuralCreateCount = 0;
            _enableCount = 0;
            _disableCount = 0;
            _uninstallCount = 0;
        }

        private static Entity EnsureEntity(EntityManager entityManager)
        {
            if (!CanUse(entityManager))
                return Entity.Null;

            if (TryResolveInstalledDriver(entityManager, out var driver))
            {
                EnsureHandleIdentity();
                return driver;
            }

            _driverEntity = entityManager.CreateEntity(ComponentType.ReadWrite<AutoChessBattleDriverComponent>());
            _driverWorld = entityManager.World;
            _driverId = ++_nextDriverId;
            _driverVersion = 0;
            _structuralCreateCount++;
            entityManager.SetName(_driverEntity, "AutoChessBattleCommandDriver");
            entityManager.SetComponentData(_driverEntity, CreateInitialState(enabled: false));
            return _driverEntity;
        }

        private static bool TryResolveOwnedDriver(
            EntityManager entityManager,
            AutoChessGasBattleDriverHandle handle,
            out Entity driver)
        {
            driver = Entity.Null;
            if (!handle.IsValid
                || !IsCurrentHandle(handle)
                || !TryResolveInstalledDriver(entityManager, out driver))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveInstalledDriver(
            EntityManager entityManager,
            out Entity driver)
        {
            driver = Entity.Null;
            if (!CanUse(entityManager)
                || _driverWorld != entityManager.World
                || _driverEntity == Entity.Null
                || !entityManager.Exists(_driverEntity)
                || !entityManager.HasComponent<AutoChessBattleDriverComponent>(_driverEntity))
            {
                return false;
            }

            driver = _driverEntity;
            return true;
        }

        private static AutoChessBattleDriverComponent CreateInitialState(bool enabled)
        {
            return new AutoChessBattleDriverComponent
            {
                Enabled = enabled,
                LastDecisionFrame = -1,
                LastExecutionFrame = -1,
                LastOutcomeFrame = -1,
            };
        }

        private static bool CanUse(EntityManager entityManager)
        {
            return entityManager.World != null && entityManager.World.IsCreated;
        }

        private static AutoChessGasBattleDriverHandle CreateHandle()
        {
            return new AutoChessGasBattleDriverHandle(_driverId, _driverVersion);
        }

        private static bool IsCurrentHandle(AutoChessGasBattleDriverHandle handle)
        {
            return handle.IsValid
                   && handle.DriverId == _driverId
                   && handle.Version == _driverVersion;
        }

        private static void EnsureHandleIdentity()
        {
            if (_driverId <= 0)
                _driverId = ++_nextDriverId;
        }

        private static void AdvanceDriverVersion()
        {
            _driverVersion = _driverVersion == int.MaxValue ? 1 : _driverVersion + 1;
        }

        private static void ResetHandleState()
        {
            _driverId = 0;
            _driverVersion = 0;
        }

        private static void Reset()
        {
            _driverWorld = null;
            _driverEntity = Entity.Null;
            ResetHandleState();
        }
    }

    public struct AutoChessBattleUnitComponent : IComponentData
    {
        public int BattleGroup;
        public AutoChessTeam Team;
        public int Slot;
        public int PrimaryAbilityCode;
        public int FinisherAbilityCode;
        public int ActiveAbilityCode;
        public int ActiveCastInterval;
        public int ActiveCastFrameOffset;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public int EnergyAttrSetCode;
        public int EnergyAttrCode;
        public int CooldownTagIndex;
        public float FinisherHealthThreshold;
        public AutoChessTargetPolicy PrimaryTargetPolicy;
        public AutoChessTargetPolicy FinisherTargetPolicy;
    }

    public struct AutoChessExecuteDamageCalculationComponent : IComponentData
    {
        public int GameplayEffectCode;
        public int CalculationCode;
        public int OutputKey;
        public int HealthAttrSetCode;
        public int HealthAttrCode;
        public float BaseDamage;
        public float MissingHealthCoefficient;
        public float MinDamage;
        public float MaxDamage;
    }

    public struct AutoChessBattleUnitTargetStateRecord
    {
        public Entity Asc;
        public int BattleGroup;
        public AutoChessTeam Team;
        public int Slot;
        public int PrimaryAbilityCode;
        public int FinisherAbilityCode;
        public int ActiveAbilityCode;
        public int ActiveCastInterval;
        public int ActiveCastFrameOffset;
        public int CooldownTagIndex;
        public float FinisherHealthThreshold;
        public AutoChessTargetPolicy PrimaryTargetPolicy;
        public AutoChessTargetPolicy FinisherTargetPolicy;
        public TagMaskComponent Tags;
        public float Health;
        public float Energy;
    }

    public struct AutoChessBattleIssuedCommandRecord
    {
        public Entity SourceAsc;
        public Entity TargetAsc;
        public int AbilityCode;
        public AutoChessTargetPolicy TargetPolicy;
        public bool IsFinisher;
        public bool IsActive;
    }
}
