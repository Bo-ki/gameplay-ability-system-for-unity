namespace GAS.Generated.CodeGen
{
    /// <summary>
    /// 让 Editor 稳定程序集硬引用本次 Source Generator marker，确保 analyzer 未运行时普通编译失败。
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
