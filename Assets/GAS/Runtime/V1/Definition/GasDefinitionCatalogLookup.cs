namespace GAS.Runtime
{
    /// <summary>
    /// 为 Runtime v1 提供只读、无状态且确定性的 Catalog 二分查找入口。
    /// </summary>
    public static class GasDefinitionCatalogLookup
    {
        /// <summary>
        /// 按稳定 Ability Definition ID 查找 Definition 数组索引。
        /// </summary>
        public static bool TryGetAbilityIndex(
            ref GasDefinitionCatalogBlob catalog,
            int definitionId,
            out int definitionIndex)
        {
            var lower = 0;
            var upper = catalog.AbilityIndex.Length - 1;
            while (lower <= upper)
            {
                var middle = lower + ((upper - lower) >> 1);
                var entry = catalog.AbilityIndex[middle];
                if (entry.DefinitionId == definitionId)
                {
                    definitionIndex = entry.DefinitionIndex;
                    return true;
                }

                if (entry.DefinitionId < definitionId)
                    lower = middle + 1;
                else
                    upper = middle - 1;
            }

            definitionIndex = -1;
            return false;
        }

        /// <summary>
        /// 按稳定 GameplayEffect Definition ID 查找 Definition 数组索引。
        /// </summary>
        public static bool TryGetGameplayEffectIndex(
            ref GasDefinitionCatalogBlob catalog,
            int definitionId,
            out int definitionIndex)
        {
            var lower = 0;
            var upper = catalog.GameplayEffectIndex.Length - 1;
            while (lower <= upper)
            {
                var middle = lower + ((upper - lower) >> 1);
                var entry = catalog.GameplayEffectIndex[middle];
                if (entry.DefinitionId == definitionId)
                {
                    definitionIndex = entry.DefinitionIndex;
                    return true;
                }

                if (entry.DefinitionId < definitionId)
                    lower = middle + 1;
                else
                    upper = middle - 1;
            }

            definitionIndex = -1;
            return false;
        }

        /// <summary>
        /// 按稳定 Attribute ID 查找 dense AttributeLayout index。
        /// </summary>
        public static bool TryGetAttributeLayoutIndex(
            ref GasDefinitionCatalogBlob catalog,
            int attributeId,
            out int layoutIndex)
        {
            var lower = 0;
            var upper = catalog.AttributeLayout.Entries.Length - 1;
            while (lower <= upper)
            {
                var middle = lower + ((upper - lower) >> 1);
                var entry = catalog.AttributeLayout.Entries[middle];
                if (entry.AttributeId == attributeId)
                {
                    layoutIndex = entry.LayoutIndex;
                    return true;
                }

                if (entry.AttributeId < attributeId)
                    lower = middle + 1;
                else
                    upper = middle - 1;
            }

            layoutIndex = -1;
            return false;
        }

        /// <summary>
        /// 按稳定 Tag ID 查找 dense TagCatalog index。
        /// </summary>
        public static bool TryGetTagIndex(
            ref GasDefinitionCatalogBlob catalog,
            int tagId,
            out int tagIndex)
        {
            var lower = 0;
            var upper = catalog.TagCatalog.Entries.Length - 1;
            while (lower <= upper)
            {
                var middle = lower + ((upper - lower) >> 1);
                var entry = catalog.TagCatalog.Entries[middle];
                if (entry.TagId == tagId)
                {
                    tagIndex = entry.TagIndex;
                    return true;
                }

                if (entry.TagId < tagId)
                    lower = middle + 1;
                else
                    upper = middle - 1;
            }

            tagIndex = -1;
            return false;
        }

        /// <summary>
        /// 通过已校验索引返回 Ability Definition 的只读引用。
        /// </summary>
        public static ref readonly GasAbilityDefinitionBlob GetAbility(
            ref GasDefinitionCatalogBlob catalog,
            int definitionIndex)
        {
            return ref catalog.Abilities[definitionIndex];
        }

        /// <summary>
        /// 通过已校验索引返回 GameplayEffect Definition 的只读引用。
        /// </summary>
        public static ref readonly GasGameplayEffectDefinitionBlob GetGameplayEffect(
            ref GasDefinitionCatalogBlob catalog,
            int definitionIndex)
        {
            return ref catalog.GameplayEffects[definitionIndex];
        }
    }
}
