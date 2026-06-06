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
                entityManager.DestroyEntity(_catalogEntity);
            }

            _catalogEntity = Entity.Null;
            _catalogWorld = null;
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
    }
}
