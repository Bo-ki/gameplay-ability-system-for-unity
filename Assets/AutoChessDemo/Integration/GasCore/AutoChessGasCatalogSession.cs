namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasCatalogSession
    {
        internal static bool TryInstall()
        {
            return AutoChessGasRuntimeAccess.TryInstallDefinitionCatalogSession();
        }

        internal static void Uninstall()
        {
            AutoChessGasRuntimeAccess.UninstallDefinitionCatalogSession();
        }
    }
}
