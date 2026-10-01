using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 为跨程序集验证公开 production generated Catalog，避免复制 fixture 或反射内部生成类型。
    /// </summary>
    public static class AutoChessRuntimeV1CatalogAccess
    {
        /// <summary>
        /// 从当前 production generated pure data 构建独立 Catalog Blob 与外部冻结期望。
        /// </summary>
        public static BlobAssetReference<GasDefinitionCatalogBlob> BuildProductionCatalog(
            Allocator allocator,
            out GasCatalogValidationExpectation expectation)
        {
            return AutoChessGeneratedDefinitionCatalog.Build(allocator, out expectation);
        }
    }
}
