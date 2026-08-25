using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 解释 application/ongoing/immunity requirement 的 Tag presence 视图。
    /// </summary>
    internal static class GasGameplayEffectRequirements
    {
        /// <summary>
        /// 对指定 phase range 执行所有 requirement，返回首个失败的稳定索引。
        /// </summary>
        internal static bool Evaluate(
            ref GasDefinitionCatalogBlob catalog,
            in GasCatalogRange range,
            DynamicBuffer<TagCountSlot> tagCounts,
            out int failedRequirementIndex)
        {
            return Evaluate(
                ref catalog,
                in range,
                tagCounts,
                out failedRequirementIndex,
                out _);
        }

        /// <summary>
        /// 求值 requirement 并区分“条件不满足”和 catalog/range 结构损坏，避免 immunity 结构错误被放行。
        /// </summary>
        internal static bool Evaluate(
            ref GasDefinitionCatalogBlob catalog,
            in GasCatalogRange range,
            DynamicBuffer<TagCountSlot> tagCounts,
            out int failedRequirementIndex,
            out bool malformed)
        {
            failedRequirementIndex = -1;
            malformed = false;
            if (range.Count == 0)
            {
                // 空 range 仅在其起点仍落于数组边界时有效，避免损坏 immunity/application range 被静默放行。
                malformed = range.Start < 0 || range.Start > catalog.Requirements.Length;
                return !malformed;
            }
            if (!IsRangeValid(in range, catalog.Requirements.Length))
            {
                malformed = true;
                return false;
            }
            var satisfied = true;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var requirementIndex = range.Start + offset;
                var requirement = catalog.Requirements[requirementIndex];
                var requirementSatisfied = EvaluateOne(
                    ref catalog,
                    in requirement,
                    tagCounts,
                    out malformed);
                if (malformed)
                {
                    failedRequirementIndex = requirementIndex;
                    return false;
                }
                if (!requirementSatisfied && satisfied)
                {
                    failedRequirementIndex = requirementIndex;
                    satisfied = false;
                }
            }
            return satisfied;
        }

        /// <summary>
        /// 按 All/Any/None 对一个 requirement 的 Tag range 求值。
        /// </summary>
        private static bool EvaluateOne(
            ref GasDefinitionCatalogBlob catalog,
            in GasRequirementBlob requirement,
            DynamicBuffer<TagCountSlot> tagCounts,
            out bool malformed)
        {
            malformed = false;
            var tags = requirement.TagIndexRange;
            if (!IsRangeValid(in tags, catalog.RequirementTagIndices.Length))
            {
                malformed = true;
                return false;
            }
            var matches = 0;
            for (var offset = 0; offset < tags.Count; offset++)
            {
                var tagIndex = catalog.RequirementTagIndices[tags.Start + offset];
                if (tagIndex < 0 || tagIndex >= tagCounts.Length)
                {
                    malformed = true;
                    return false;
                }
                if (tagCounts[tagIndex].InclusiveCount <= 0)
                    continue;
                matches++;
            }
            switch (requirement.Match)
            {
                case GasTagRequirementMatch.All:
                    return matches == tags.Count;
                case GasTagRequirementMatch.Any:
                    return matches > 0;
                case GasTagRequirementMatch.None:
                    return matches == 0;
                default:
                    malformed = true;
                    return false;
            }
        }

        /// <summary>
        /// 以减法检查 range，避免 Start+Count 整数回绕。
        /// </summary>
        private static bool IsRangeValid(in GasCatalogRange range, int length)
        {
            return range.Start >= 0 && range.Count > 0 && range.Start <= length &&
                   range.Count <= length - range.Start;
        }
    }
}
