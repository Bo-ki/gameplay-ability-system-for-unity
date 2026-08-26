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
        /// 返回与 Catalog header 精确匹配的 Stage-B 校验期望值。
        /// </summary>
        internal static GasCatalogValidationExpectation Expectation
            => new GasCatalogValidationExpectation(
                GasDefinitionCatalogSchema.Version,
                AutoChessBattleDefinitionCatalogBuilder.SchemaHash,
                AutoChessBattleDefinitionCatalogBuilder.ContentHash,
                AutoChessBattleDefinitionCatalogBuilder.AttributeHash,
                AutoChessBattleDefinitionCatalogBuilder.TagHash);

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
