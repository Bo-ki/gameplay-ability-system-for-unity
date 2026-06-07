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
    }

    internal static class AutoChessBattleDriverRuntimeStore
    {
        private static World _driverWorld;
        private static Entity _driverEntity;

        public static Entity Ensure(EntityManager entityManager)
        {
            if (!CanUse(entityManager))
                return Entity.Null;

            if (_driverWorld == entityManager.World
                && _driverEntity != Entity.Null
                && entityManager.Exists(_driverEntity)
                && entityManager.HasComponent<AutoChessBattleDriverComponent>(_driverEntity))
            {
                return _driverEntity;
            }

            _driverEntity = entityManager.CreateEntity(ComponentType.ReadWrite<AutoChessBattleDriverComponent>());
            _driverWorld = entityManager.World;
            entityManager.SetName(_driverEntity, "AutoChessBattleCommandDriver");
            entityManager.SetComponentData(_driverEntity, CreateInitialState(enabled: false));
            return _driverEntity;
        }

        public static Entity ResetAndEnable(EntityManager entityManager)
        {
            var driver = Ensure(entityManager);
            if (driver == Entity.Null)
                return Entity.Null;

            entityManager.SetComponentData(driver, CreateInitialState(enabled: true));
            return driver;
        }

        public static AutoChessBattleDriverComponent Read(
            EntityManager entityManager,
            Entity driver)
        {
            return IsOwnedDriver(entityManager, driver)
                ? entityManager.GetComponentData<AutoChessBattleDriverComponent>(driver)
                : default;
        }

        public static void Disable(
            EntityManager entityManager,
            Entity driver)
        {
            if (!IsOwnedDriver(entityManager, driver))
                return;

            var state = entityManager.GetComponentData<AutoChessBattleDriverComponent>(driver);
            state.Enabled = false;
            entityManager.SetComponentData(driver, state);
        }

        public static void Uninstall(EntityManager entityManager)
        {
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
                return;
            }

            Reset();
        }

        private static bool IsOwnedDriver(
            EntityManager entityManager,
            Entity driver)
        {
            return CanUse(entityManager)
                   && _driverWorld == entityManager.World
                   && driver != Entity.Null
                   && driver == _driverEntity
                   && entityManager.Exists(driver)
                   && entityManager.HasComponent<AutoChessBattleDriverComponent>(driver);
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

        private static void Reset()
        {
            _driverWorld = null;
            _driverEntity = Entity.Null;
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
