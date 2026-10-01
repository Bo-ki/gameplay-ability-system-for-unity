using GAS.Runtime;

namespace GAS.Editor.CodeGen.Proofs
{
    /// <summary>
    /// 在 canonical graph DTO 冻结前只暴露逐 Definition proof 标量、身份和 provenance，不复制 graph 结构。
    /// </summary>
    public interface IGasCapacityProofSource
    {
        int GraphSchemaVersion { get; }
        string GeneratorVersion { get; }
        string CanonicalGraphHash { get; }
        string ContractMatrixHash { get; }
        string TargetScaleId { get; }
        GasScaleProfile ScaleProfile { get; }
        GasProofProvenance ScaleProfileProvenance { get; }
        long DeclaredMemoryBudgetBytes { get; }
        GasProofProvenance MemoryBudgetProvenance { get; }
        int DefinitionCount { get; }

        /// <summary>
        /// 按 canonical ordinal 读取 stable DefinitionId 与 definition-level provenance。
        /// </summary>
        bool TryGetDefinitionIdentity(
            int definitionOrdinal,
            out GasProofDefinitionIdentity identity,
            out GasProofProvenance provenance);

        /// <summary>
        /// 读取一个 Definition 的显式非负 tight semantic bound；不适用维度必须由 A 投影为带 provenance 的零。
        /// </summary>
        bool TryGetBound(
            int definitionOrdinal,
            GasCapacityDimension dimension,
            out long maximum,
            out GasProofProvenance provenance);
    }
}
