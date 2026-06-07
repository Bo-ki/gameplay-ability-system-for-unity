using Unity.Collections;
using Unity.Entities;
using GAS.Runtime;
using GAS.Runtime.Generated;

namespace GAS.AutoChessDemo
{
    public static class AutoChessBattleDefinitionCatalogBuilder
    {
        private static BlobAssetReference<GASDefinitionCatalogBlob> _installedCatalog;
        private static Entity _catalogEntity;
        private static Entity _executionCalculationEntity;
        private static World _catalogWorld;

        public static void Install(EntityManager entityManager)
        {
            if (_installedCatalog.IsCreated)
                _installedCatalog.Dispose();

            _installedCatalog = GASGeneratedDefinitionCatalogBuilder.BuildCatalog(Allocator.Persistent);
            _catalogEntity = ResolveCatalogEntity(entityManager);
            _catalogWorld = entityManager.World;
            entityManager.SetName(_catalogEntity, "AutoChessBattleDefinitionCatalog");
            entityManager.SetComponentData(_catalogEntity, new GASDefinitionCatalogComponent
            {
                Catalog = _installedCatalog,
                Revision = GASGeneratedDefinitionCatalogInfo.SchemaVersion,
            });
            InstallExecutionCalculationConfig(entityManager);
        }

        public static void Uninstall(EntityManager entityManager)
        {
            if (entityManager.World == null || !entityManager.World.IsCreated)
            {
                DisposeInstalledCatalog();
                return;
            }

            if (_catalogWorld == entityManager.World
                && _catalogEntity != Entity.Null
                && entityManager.Exists(_catalogEntity))
            {
                entityManager.SetComponentData(_catalogEntity, new GASDefinitionCatalogComponent
                {
                    Catalog = default,
                    Revision = 0,
                });
            }

            if (_catalogWorld == entityManager.World
                && _executionCalculationEntity != Entity.Null
                && entityManager.Exists(_executionCalculationEntity))
            {
                entityManager.SetComponentData(
                    _executionCalculationEntity,
                    default(AutoChessExecuteDamageCalculationComponent));
            }

            DisposeInstalledCatalog();
        }

        private static void DisposeInstalledCatalog()
        {
            if (!_installedCatalog.IsCreated)
                return;

            _installedCatalog.Dispose();
            _installedCatalog = default;
        }

        private static Entity ResolveCatalogEntity(EntityManager entityManager)
        {
            if (_catalogWorld == entityManager.World
                && _catalogEntity != Entity.Null
                && entityManager.Exists(_catalogEntity))
                return _catalogEntity;

            return entityManager.CreateEntity(ComponentType.ReadWrite<GASDefinitionCatalogComponent>());
        }

        private static void InstallExecutionCalculationConfig(EntityManager entityManager)
        {
            var row = AutoChessGeneratedConfig.ExecuteDamageCalculation;
            _executionCalculationEntity = ResolveExecutionCalculationEntity(entityManager);
            entityManager.SetName(_executionCalculationEntity, "AutoChessExecuteDamageCalculationConfig");
            entityManager.SetComponentData(_executionCalculationEntity, new AutoChessExecuteDamageCalculationComponent
            {
                GameplayEffectCode = row.GameplayEffectCode,
                CalculationCode = row.CalculationCode,
                OutputKey = row.OutputKey,
                HealthAttrSetCode = row.HealthAttrSetCode,
                HealthAttrCode = row.HealthAttrCode,
                BaseDamage = row.BaseDamage,
                MissingHealthCoefficient = row.MissingHealthCoefficient,
                MinDamage = row.MinDamage,
                MaxDamage = row.MaxDamage,
            });
        }

        private static Entity ResolveExecutionCalculationEntity(EntityManager entityManager)
        {
            if (_catalogWorld == entityManager.World
                && _executionCalculationEntity != Entity.Null
                && entityManager.Exists(_executionCalculationEntity))
                return _executionCalculationEntity;

            return entityManager.CreateEntity(ComponentType.ReadWrite<AutoChessExecuteDamageCalculationComponent>());
        }
    }
}
