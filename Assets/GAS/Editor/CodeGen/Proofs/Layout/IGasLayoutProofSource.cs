using GAS.Runtime;

namespace GAS.Editor.CodeGen.Proofs
{
    /// <summary>
    /// 在 canonical graph DTO 冻结前仅投影 LayoutProof 必需标量，禁止在 B 链复制或规范化第二套 graph。
    /// </summary>
    public interface IGasLayoutProofSource
    {
        int GraphSchemaVersion { get; }
        string GeneratorVersion { get; }
        string CanonicalGraphHash { get; }
        int AttributeCount { get; }
        int TagCount { get; }
        int AncestorIndexCount { get; }
        int TagQueryProgramCount { get; }
        int RequirementTagIndexCount { get; }
        int BlobRangeCount { get; }

        /// <summary>
        /// 按 canonical ordinal 读取一条 Attribute dense 映射，不允许按名称或 suffix 猜测。
        /// </summary>
        bool TryGetAttribute(int ordinal, out GasLayoutAttributeProofEntry entry);

        /// <summary>
        /// 按 canonical ordinal 读取一条 Tag dense 映射及其 ancestor range。
        /// </summary>
        bool TryGetTag(int ordinal, out GasLayoutTagProofEntry entry);

        /// <summary>
        /// 读取 canonical ancestor flat array 中的一个 dense Tag index。
        /// </summary>
        bool TryGetAncestorIndex(int ordinal, out int tagIndex);

        /// <summary>
        /// 按 canonical ordinal 读取一条预解析 TagQueryProgram。
        /// </summary>
        bool TryGetTagQueryProgram(int ordinal, out GasLayoutTagQueryProofEntry entry);

        /// <summary>
        /// 读取 TagQueryProgram flat array 中的一个 dense Tag index。
        /// </summary>
        bool TryGetRequirementTagIndex(int ordinal, out int tagIndex);

        /// <summary>
        /// 按 canonical ordinal 读取一个 Blob range 及其 backing length。
        /// </summary>
        bool TryGetBlobRange(int ordinal, out GasLayoutRangeProofEntry entry);
    }
}
