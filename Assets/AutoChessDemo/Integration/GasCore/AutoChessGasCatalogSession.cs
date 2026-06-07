using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasCatalogSession
    {
        public static void Install(EntityManager entityManager)
        {
            AutoChessBattleDefinitionCatalogBuilder.Install(entityManager);
            AutoChessBattleDriverRuntimeStore.Ensure(entityManager);
        }

        public static void Uninstall(EntityManager entityManager)
        {
            AutoChessBattleDriverRuntimeStore.Uninstall(entityManager);
            AutoChessBattleDefinitionCatalogBuilder.Uninstall(entityManager);
        }
    }
}
