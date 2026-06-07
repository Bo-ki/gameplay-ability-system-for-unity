using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasCatalogSession
    {
        internal static bool TryInstall()
        {
            if (!AutoChessGasRuntimeAccess.TryResolveDefinitionEntityManager(out var entityManager))
                return false;

            AutoChessBattleDefinitionCatalogBuilder.Install(entityManager);
            AutoChessBattleDriverRuntimeStore.Ensure(entityManager);
            return true;
        }

        internal static void Uninstall()
        {
            if (!AutoChessGasRuntimeAccess.TryResolveDefinitionEntityManager(out var entityManager))
                return;

            AutoChessBattleDriverRuntimeStore.Uninstall(entityManager);
            AutoChessBattleDefinitionCatalogBuilder.Uninstall(entityManager);
        }
    }
}
