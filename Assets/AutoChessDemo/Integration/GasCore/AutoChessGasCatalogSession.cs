using GAS.Runtime;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    internal static class AutoChessGasCatalogSession
    {
        /// <summary>
        /// 返回当前宿主安装的 immutable Runtime v1 Catalog。
        /// </summary>
        internal static BlobAssetReference<GasDefinitionCatalogBlob> Catalog
            => AutoChessBattleDefinitionCatalogBuilder.Catalog;

        /// <summary>
        /// 返回从 generated draft 独立冻结、且不读取最终候选 header 的 Stage-B 校验期望值。
        /// </summary>
        internal static GasCatalogValidationExpectation Expectation
            => AutoChessBattleDefinitionCatalogBuilder.Expectation;

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
