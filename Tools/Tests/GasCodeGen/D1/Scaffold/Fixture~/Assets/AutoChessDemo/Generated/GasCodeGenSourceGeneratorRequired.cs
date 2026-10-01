namespace GAS.Generated.CodeGen
{
    /// <summary>
    /// 让既有 AutoChess 程序集硬引用本次 Source Generator marker，禁止新增第四个生成程序集。
    /// </summary>
    internal static class GasCodeGenSourceGeneratorRequired
    {
        internal const string TargetAssembly = GasCodeGenSourceGeneratorMarker.TargetAssembly;
        internal const string SelectorSha256 = GasCodeGenSourceGeneratorMarker.SelectorSha256;
        internal const string ArtifactManifestHash = GasCodeGenSourceGeneratorMarker.ArtifactManifestHash;
        internal const string SourceArtifactInventoryHash =
            GasCodeGenSourceGeneratorMarker.SourceArtifactInventoryHash;
    }
}
