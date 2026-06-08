using Unity.Collections;
using Unity.Entities;
using GAS.Runtime;
using GAS.Runtime.Generated;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessBattleDefinitionCatalogBuilder
    {
        private static BlobAssetReference<GASDefinitionCatalogBlob> _installedCatalog;
        private static Entity _catalogEntity;
        private static Entity _executionCalculationEntity;
        private static World _catalogWorld;

        internal static void Install(EntityManager entityManager)
        {
            if (_installedCatalog.IsCreated)
                _installedCatalog.Dispose();

            _installedCatalog = BuildCatalog(Allocator.Persistent);
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

        internal static void Uninstall(EntityManager entityManager)
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

        internal static bool TryCreateRuntimeConceptEvidence(
            int abilityCode,
            int frame,
            out GASRuntimeConceptCoverageSnapshot coverage,
            out GASRuntimeTracePreview trace)
        {
            var catalog = _installedCatalog;
            var ownsCatalog = false;
            if (!catalog.IsCreated)
            {
                catalog = BuildCatalog(Allocator.Persistent);
                ownsCatalog = true;
            }

            try
            {
                ref var root = ref catalog.Value;
                coverage = GASRuntimeDefinitionResolver.CreateConceptCoverageSnapshot(ref root);
                return GASRuntimeDefinitionResolver.TryBuildRuntimeTracePreview(
                    ref root,
                    abilityCode,
                    Entity.Null,
                    Entity.Null,
                    Entity.Null,
                    frame,
                    requestedLevel: 1,
                    out trace);
            }
            finally
            {
                if (ownsCatalog && catalog.IsCreated)
                    catalog.Dispose();
            }
        }

        private static void DisposeInstalledCatalog()
        {
            if (!_installedCatalog.IsCreated)
                return;

            _installedCatalog.Dispose();
            _installedCatalog = default;
        }

        private static BlobAssetReference<GASDefinitionCatalogBlob> BuildCatalog(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GASDefinitionCatalogBlob>();
                GASGeneratedDefinitionCatalogData.Populate(ref builder, ref root);
                return builder.CreateBlobAssetReference<GASDefinitionCatalogBlob>(allocator);
            }
            finally
            {
                builder.Dispose();
            }
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
