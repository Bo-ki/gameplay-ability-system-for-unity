namespace GAS.Runtime
{
    /// <summary>
    /// 标识 Runtime v1 Catalog 安装前的 fail-closed 校验结果。
    /// </summary>
    public enum GasCatalogValidationError : byte
    {
        None = 0,
        SchemaVersionMismatch = 1,
        SchemaHashMismatch = 2,
        ContentHashMismatch = 3,
        AttributeLayoutHashMismatch = 4,
        TagCatalogHashMismatch = 5,
        AttributeLayoutInvalid = 6,
        TagCatalogInvalid = 7,
        IndexLengthMismatch = 8,
        IndexNotStrictlySorted = 9,
        IndexOutOfRange = 10,
        IndexDefinitionMismatch = 11,
        RangeOutOfBounds = 12,
        RequirementPhaseMismatch = 13,
        RequirementInvalid = 14,
        CaptureDescriptorInvalid = 15,
        TargetPolicyInvalid = 16,
        DefinitionPolicyInvalid = 17,
        DefinitionMaximaExceeded = 18,
        ModifierInvalid = 19,
        DirectProgramInvalid = 20,
        CueInvalid = 21,
        ValueViewContractMismatch = 22,
        EvaluatorProgramInvalid = 23,
        SetByCallerInvalid = 24,
        TargetDataInvalid = 25,
        EffectContextInvalid = 26,
    }

    /// <summary>
    /// 标识发生 range 校验失败的 Catalog 字段。
    /// </summary>
    public enum GasCatalogRangeKind : byte
    {
        None = 0,
        ApplicationRequirement = 1,
        OngoingRequirement = 2,
        RemovalRequirement = 3,
        ImmunityRequirement = 4,
        RequirementTags = 5,
        Capture = 6,
        Modifier = 7,
        DirectEffectProgram = 8,
        Cue = 9,
        ValueView = 10,
        TagAncestors = 11,
        EvaluatorProgram = 12,
        SetByCaller = 13,
        TargetData = 14,
        EffectContext = 15,
    }

    /// <summary>
    /// 保存 Session install 时必须与 Catalog 精确匹配的版本和 hash。
    /// </summary>
    public readonly struct GasCatalogValidationExpectation
    {
        public readonly int SchemaVersion;
        public readonly ulong SchemaHash;
        public readonly ulong ContentHash;
        public readonly ulong AttributeLayoutHash;
        public readonly ulong TagCatalogHash;

        /// <summary>
        /// 构造一个不允许 fallback 的 Catalog header 期望值。
        /// </summary>
        public GasCatalogValidationExpectation(
            int schemaVersion,
            ulong schemaHash,
            ulong contentHash,
            ulong attributeLayoutHash,
            ulong tagCatalogHash)
        {
            SchemaVersion = schemaVersion;
            SchemaHash = schemaHash;
            ContentHash = contentHash;
            AttributeLayoutHash = attributeLayoutHash;
            TagCatalogHash = tagCatalogHash;
        }
    }

    /// <summary>
    /// 返回机器可读错误、Definition 与字段位置，不依赖 managed 文本诊断。
    /// </summary>
    public readonly struct GasCatalogValidationResult
    {
        public readonly GasCatalogValidationError Error;
        public readonly GasCatalogRangeKind RangeKind;
        public readonly int DefinitionId;
        public readonly int ElementIndex;

        /// <summary>
        /// 构造一个纯数据 Catalog 校验结果。
        /// </summary>
        public GasCatalogValidationResult(
            GasCatalogValidationError error,
            GasCatalogRangeKind rangeKind = GasCatalogRangeKind.None,
            int definitionId = 0,
            int elementIndex = -1)
        {
            Error = error;
            RangeKind = rangeKind;
            DefinitionId = definitionId;
            ElementIndex = elementIndex;
        }

        /// <summary>
        /// 判断 Catalog 是否通过全部 fail-closed 校验。
        /// </summary>
        public bool Succeeded => Error == GasCatalogValidationError.None;
    }

    /// <summary>
    /// 对 immutable Catalog 执行无分配、无运行时全局访问的结构与语义契约校验。
    /// </summary>
    public static class GasDefinitionCatalogValidator
    {
        /// <summary>
        /// 按 header、布局、索引和 Definition 顺序完成 fail-closed 校验。
        /// </summary>
        public static GasCatalogValidationResult Validate(
            ref GasDefinitionCatalogBlob catalog,
            in GasCatalogValidationExpectation expectation)
        {
            var result = ValidateHeader(ref catalog, in expectation);
            if (!result.Succeeded)
                return result;

            result = ValidateAttributeLayout(ref catalog);
            if (!result.Succeeded)
                return result;

            result = ValidateTagCatalog(ref catalog);
            if (!result.Succeeded)
                return result;

            result = ValidateDefinitionIndices(ref catalog);
            if (!result.Succeeded)
                return result;

            result = ValidateAbilityDefinitions(ref catalog);
            return result.Succeeded ? ValidateGameplayEffectDefinitions(ref catalog) : result;
        }

        /// <summary>
        /// 精确匹配 schema/content/layout/tag hash，任一不符即拒绝安装。
        /// </summary>
        private static GasCatalogValidationResult ValidateHeader(
            ref GasDefinitionCatalogBlob catalog,
            in GasCatalogValidationExpectation expectation)
        {
            if (catalog.SchemaVersion != GasDefinitionCatalogSchema.Version
                || catalog.SchemaVersion != expectation.SchemaVersion)
                return Failure(GasCatalogValidationError.SchemaVersionMismatch);

            if (catalog.SchemaHash != expectation.SchemaHash)
                return Failure(GasCatalogValidationError.SchemaHashMismatch);

            if (catalog.ContentHash != expectation.ContentHash)
                return Failure(GasCatalogValidationError.ContentHashMismatch);

            if (catalog.AttributeLayout.LayoutHash != expectation.AttributeLayoutHash)
                return Failure(GasCatalogValidationError.AttributeLayoutHashMismatch);

            if (catalog.TagCatalog.CatalogHash != expectation.TagCatalogHash)
                return Failure(GasCatalogValidationError.TagCatalogHashMismatch);

            return Success();
        }

        /// <summary>
        /// 验证 AttributeLayout 按 stable ID 排序且 dense index 与数组位置一致。
        /// </summary>
        private static GasCatalogValidationResult ValidateAttributeLayout(
            ref GasDefinitionCatalogBlob catalog)
        {
            var previousId = int.MinValue;
            for (var index = 0; index < catalog.AttributeLayout.Entries.Length; index++)
            {
                var entry = catalog.AttributeLayout.Entries[index];
                if (entry.AttributeId <= 0
                    || entry.AttributeId <= previousId
                    || entry.LayoutIndex != index
                    || !IsFinite(entry.DefaultValue)
                    || !IsFinite(entry.MinimumValue)
                    || !IsFinite(entry.MaximumValue)
                    || entry.MinimumValue > entry.MaximumValue
                    || entry.DefaultValue < entry.MinimumValue
                    || entry.DefaultValue > entry.MaximumValue
                    || entry.ClampMinimum > 1
                    || entry.ClampMaximum > 1)
                {
                    return Failure(
                        GasCatalogValidationError.AttributeLayoutInvalid,
                        elementIndex: index);
                }

                previousId = entry.AttributeId;
            }

            return Success();
        }

        /// <summary>
        /// 验证 TagCatalog dense index、stable order 与 ancestor chain range。
        /// </summary>
        private static GasCatalogValidationResult ValidateTagCatalog(
            ref GasDefinitionCatalogBlob catalog)
        {
            var previousId = int.MinValue;
            for (var index = 0; index < catalog.TagCatalog.Entries.Length; index++)
            {
                var entry = catalog.TagCatalog.Entries[index];
                if (entry.TagId <= 0 || entry.TagId <= previousId || entry.TagIndex != index)
                    return Failure(GasCatalogValidationError.TagCatalogInvalid, elementIndex: index);

                if (!IsRangeValid(entry.AncestorIndexRange, catalog.TagCatalog.AncestorIndices.Length))
                {
                    return Failure(
                        GasCatalogValidationError.RangeOutOfBounds,
                        GasCatalogRangeKind.TagAncestors,
                        elementIndex: index);
                }

                if (!IsCanonicalAncestorRange(ref catalog, index, entry.AncestorIndexRange))
                    return Failure(GasCatalogValidationError.TagCatalogInvalid, elementIndex: index);

                previousId = entry.TagId;
            }

            for (var index = 0; index < catalog.TagCatalog.Entries.Length; index++)
            {
                if (!IsAncestorClosureValid(ref catalog, index))
                    return Failure(GasCatalogValidationError.TagCatalogInvalid, elementIndex: index);
            }

            return Success();
        }

        /// <summary>
        /// 验证 ancestor range 是包含自身一次的严格递增 dense-index 集合。
        /// </summary>
        private static bool IsCanonicalAncestorRange(
            ref GasDefinitionCatalogBlob catalog,
            int tagIndex,
            GasCatalogRange range)
        {
            var previousAncestor = -1;
            var containsSelf = false;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var ancestor = catalog.TagCatalog.AncestorIndices[range.Start + offset];
                if (ancestor <= previousAncestor || ancestor >= catalog.TagCatalog.Entries.Length)
                    return false;

                containsSelf |= ancestor == tagIndex;
                previousAncestor = ancestor;
            }

            return containsSelf;
        }

        /// <summary>
        /// 验证每个 ancestor 的完整闭包已进入当前 range，并拒绝非自身的 ancestor 环。
        /// </summary>
        private static bool IsAncestorClosureValid(
            ref GasDefinitionCatalogBlob catalog,
            int tagIndex)
        {
            var range = catalog.TagCatalog.Entries[tagIndex].AncestorIndexRange;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var ancestor = catalog.TagCatalog.AncestorIndices[range.Start + offset];
                var ancestorRange = catalog.TagCatalog.Entries[ancestor].AncestorIndexRange;
                if (ancestor != tagIndex && ContainsTagIndex(ref catalog, ancestorRange, tagIndex))
                    return false;

                for (var ancestorOffset = 0; ancestorOffset < ancestorRange.Count; ancestorOffset++)
                {
                    var transitiveAncestor = catalog.TagCatalog.AncestorIndices[
                        ancestorRange.Start + ancestorOffset];
                    if (!ContainsTagIndex(ref catalog, range, transitiveAncestor))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 在已规范化的 ancestor range 中查找指定 dense Tag index。
        /// </summary>
        private static bool ContainsTagIndex(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            int tagIndex)
        {
            for (var offset = 0; offset < range.Count; offset++)
            {
                var candidate = catalog.TagCatalog.AncestorIndices[range.Start + offset];
                if (candidate == tagIndex)
                    return true;
                if (candidate > tagIndex)
                    return false;
            }

            return false;
        }

        /// <summary>
        /// 验证 Ability 与 GameplayEffect 的排序索引完整映射同类 Definition。
        /// </summary>
        private static GasCatalogValidationResult ValidateDefinitionIndices(
            ref GasDefinitionCatalogBlob catalog)
        {
            if (catalog.AbilityIndex.Length != catalog.Abilities.Length
                || catalog.GameplayEffectIndex.Length != catalog.GameplayEffects.Length)
                return Failure(GasCatalogValidationError.IndexLengthMismatch);

            var result = ValidateAbilityIndex(ref catalog);
            return result.Succeeded ? ValidateGameplayEffectIndex(ref catalog) : result;
        }

        /// <summary>
        /// 验证 Ability index 严格递增且没有错配 Definition ID。
        /// </summary>
        private static GasCatalogValidationResult ValidateAbilityIndex(
            ref GasDefinitionCatalogBlob catalog)
        {
            var previousId = int.MinValue;
            for (var index = 0; index < catalog.AbilityIndex.Length; index++)
            {
                var entry = catalog.AbilityIndex[index];
                if (entry.DefinitionId <= previousId)
                    return Failure(GasCatalogValidationError.IndexNotStrictlySorted, elementIndex: index);

                if (entry.DefinitionIndex < 0 || entry.DefinitionIndex >= catalog.Abilities.Length)
                    return Failure(GasCatalogValidationError.IndexOutOfRange, elementIndex: index);

                if (catalog.Abilities[entry.DefinitionIndex].DefinitionId != entry.DefinitionId)
                    return Failure(GasCatalogValidationError.IndexDefinitionMismatch, elementIndex: index);

                previousId = entry.DefinitionId;
            }

            return Success();
        }

        /// <summary>
        /// 验证 GameplayEffect index 严格递增且没有错配 Definition ID。
        /// </summary>
        private static GasCatalogValidationResult ValidateGameplayEffectIndex(
            ref GasDefinitionCatalogBlob catalog)
        {
            var previousId = int.MinValue;
            for (var index = 0; index < catalog.GameplayEffectIndex.Length; index++)
            {
                var entry = catalog.GameplayEffectIndex[index];
                if (entry.DefinitionId <= previousId)
                    return Failure(GasCatalogValidationError.IndexNotStrictlySorted, elementIndex: index);

                if (entry.DefinitionIndex < 0 || entry.DefinitionIndex >= catalog.GameplayEffects.Length)
                    return Failure(GasCatalogValidationError.IndexOutOfRange, elementIndex: index);

                if (catalog.GameplayEffects[entry.DefinitionIndex].DefinitionId != entry.DefinitionId)
                    return Failure(GasCatalogValidationError.IndexDefinitionMismatch, elementIndex: index);

                previousId = entry.DefinitionId;
            }

            return Success();
        }

        /// <summary>
        /// 验证每个 Ability 的 owner commit、target、program/cue range 与静态上限。
        /// </summary>
        private static GasCatalogValidationResult ValidateAbilityDefinitions(
            ref GasDefinitionCatalogBlob catalog)
        {
            for (var index = 0; index < catalog.Abilities.Length; index++)
            {
                var definition = catalog.Abilities[index];
                if (definition.DefinitionId <= 0
                    || definition.Level <= 0
                    || definition.MaxConcurrentActivations < 0
                    || !IsCostMutationContractValid(ref catalog, in definition.CostMutationContract)
                    || !IsCooldownGateContractValid(ref catalog, in definition.CooldownGateContract))
                    return Failure(GasCatalogValidationError.DefinitionPolicyInvalid, definitionId: definition.DefinitionId);

                if (!IsTargetPolicyValid(definition.TargetPolicy))
                    return Failure(GasCatalogValidationError.TargetPolicyInvalid, definitionId: definition.DefinitionId);

                var result = ValidateAbilityRanges(ref catalog, in definition);
                if (!result.Succeeded)
                    return result;

                result = ValidateDirectProgram(ref catalog, definition.DirectEffectProgramRange, in definition.Maxima, definition.DefinitionId);
                if (!result.Succeeded)
                    return result;

                result = ValidateCues(ref catalog, definition.CueRange, definition.DefinitionId);
                if (!result.Succeeded)
                    return result;
            }

            return Success();
        }

        /// <summary>
        /// 验证 cost 关闭时为规范空值，启用时只引用 owner ASC 的有效属性并产生有限非零变更。
        /// </summary>
        private static bool IsCostMutationContractValid(
            ref GasDefinitionCatalogBlob catalog,
            in GasCostMutationContractBlob contract)
        {
            if (contract.Enabled > 1)
                return false;

            if (contract.Enabled == 0)
            {
                return contract.AttributeLayoutIndex == 0
                    && contract.BaseDelta == 0f
                    && contract.CurrentDelta == 0f;
            }

            return contract.AttributeLayoutIndex >= 0
                && contract.AttributeLayoutIndex < catalog.AttributeLayout.Entries.Length
                && IsFinite(contract.BaseDelta)
                && IsFinite(contract.CurrentDelta)
                && (contract.BaseDelta != 0f || contract.CurrentDelta != 0f);
        }

        /// <summary>
        /// 验证 cooldown 关闭时为规范空值，启用时具有稳定 key、正 duration 与可选有效 owned Tag。
        /// </summary>
        private static bool IsCooldownGateContractValid(
            ref GasDefinitionCatalogBlob catalog,
            in GasCooldownGateContractBlob contract)
        {
            if (contract.Enabled > 1)
                return false;

            if (contract.Enabled == 0)
            {
                return contract.GateKey == 0
                    && contract.DurationTicks == 0
                    && contract.OwnedTagIndex == 0;
            }

            return contract.GateKey > 0
                && contract.DurationTicks > 0
                && (contract.OwnedTagIndex == -1
                    || (contract.OwnedTagIndex >= 0
                        && contract.OwnedTagIndex < catalog.TagCatalog.Entries.Length));
        }

        /// <summary>
        /// 验证 Ability 的 program/cue ranges 均在根数组内且不超过 maxima。
        /// </summary>
        private static GasCatalogValidationResult ValidateAbilityRanges(
            ref GasDefinitionCatalogBlob catalog,
            in GasAbilityDefinitionBlob definition)
        {
            if (!IsRangeValid(definition.DirectEffectProgramRange, catalog.DirectEffectProgramNodes.Length))
                return RangeFailure(GasCatalogRangeKind.DirectEffectProgram, definition.DefinitionId);

            if (!IsRangeValid(definition.CueRange, catalog.CueReferences.Length))
                return RangeFailure(GasCatalogRangeKind.Cue, definition.DefinitionId);

            if (!AreMaximaNonNegative(in definition.Maxima)
                || definition.DirectEffectProgramRange.Count > definition.Maxima.MaximumDirectProgramNodeCount
                || definition.CueRange.Count > definition.Maxima.MaximumCueCount)
                return Failure(GasCatalogValidationError.DefinitionMaximaExceeded, definitionId: definition.DefinitionId);

            return Success();
        }

        /// <summary>
        /// 验证每个 GameplayEffect 的 phase range、capture、policy 与 evaluator 契约。
        /// </summary>
        private static GasCatalogValidationResult ValidateGameplayEffectDefinitions(
            ref GasDefinitionCatalogBlob catalog)
        {
            for (var index = 0; index < catalog.GameplayEffects.Length; index++)
            {
                var definition = catalog.GameplayEffects[index];
                var result = ValidateGameplayEffectHeader(in definition);
                if (!result.Succeeded)
                    return result;

                result = ValidateGameplayEffectRanges(ref catalog, in definition);
                if (!result.Succeeded)
                    return result;

                result = ValidateGameplayEffectPayloads(ref catalog, in definition);
                if (!result.Succeeded)
                    return result;
            }

            return Success();
        }

        /// <summary>
        /// 验证 GameplayEffect lifetime、target、period 与 stack policy 的内部一致性。
        /// </summary>
        private static GasCatalogValidationResult ValidateGameplayEffectHeader(
            in GasGameplayEffectDefinitionBlob definition)
        {
            if (definition.DefinitionId <= 0
                || !IsTargetPolicyValid(definition.TargetPolicy)
                || !IsLifetimeValid(definition.Lifetime)
                || definition.DurationTicks < 0
                || definition.PeriodTicks < 0
                || definition.StackLimit < 0
                || definition.ExecuteOnApplication > 1
                || !IsEffectPolicyEnumValid(in definition))
                return Failure(GasCatalogValidationError.DefinitionPolicyInvalid, definitionId: definition.DefinitionId);

            var instant = definition.Lifetime == GasEffectLifetimePolicy.Instant
                || definition.Lifetime == GasEffectLifetimePolicy.InstantExecution;
            if ((instant && (definition.DurationTicks != 0 || definition.PeriodTicks != 0 || definition.StackLimit != 0))
                || (definition.Lifetime == GasEffectLifetimePolicy.Duration && definition.DurationTicks <= 0)
                || (definition.Lifetime == GasEffectLifetimePolicy.Infinite && definition.DurationTicks != 0))
                return Failure(GasCatalogValidationError.DefinitionPolicyInvalid, definitionId: definition.DefinitionId);

            if ((definition.StackLimit == 0 && !HasNoStackPolicy(in definition))
                || (definition.StackLimit > 0 && !HasCompleteStackPolicy(in definition)))
                return Failure(GasCatalogValidationError.DefinitionPolicyInvalid, definitionId: definition.DefinitionId);

            if ((definition.PeriodTicks == 0 && definition.InhibitedPeriodPolicy != GasInhibitedPeriodPolicy.None)
                || (definition.PeriodTicks > 0 && definition.InhibitedPeriodPolicy == GasInhibitedPeriodPolicy.None))
                return Failure(GasCatalogValidationError.DefinitionPolicyInvalid, definitionId: definition.DefinitionId);

            return Success();
        }

        /// <summary>
        /// 验证 GameplayEffect 所有根数组 ranges 与每 Definition maxima。
        /// </summary>
        private static GasCatalogValidationResult ValidateGameplayEffectRanges(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition)
        {
            var result = ValidateRequirementRanges(ref catalog, in definition);
            if (!result.Succeeded)
                return result;

            if (!IsRangeValid(definition.CaptureRange, catalog.CaptureDescriptors.Length))
                return RangeFailure(GasCatalogRangeKind.Capture, definition.DefinitionId);
            if (!IsRangeValid(definition.ModifierRange, catalog.Modifiers.Length))
                return RangeFailure(GasCatalogRangeKind.Modifier, definition.DefinitionId);
            if (!IsRangeValid(definition.DirectEffectProgramRange, catalog.DirectEffectProgramNodes.Length))
                return RangeFailure(GasCatalogRangeKind.DirectEffectProgram, definition.DefinitionId);
            if (!IsRangeValid(definition.CueRange, catalog.CueReferences.Length))
                return RangeFailure(GasCatalogRangeKind.Cue, definition.DefinitionId);
            if (!IsRangeValid(definition.ValueViewRange, catalog.ValueViews.Length))
                return RangeFailure(GasCatalogRangeKind.ValueView, definition.DefinitionId);
            if (!IsRangeValid(definition.EvaluatorProgramRange, catalog.EvaluatorInstructions.Length))
                return RangeFailure(GasCatalogRangeKind.EvaluatorProgram, definition.DefinitionId);
            if (!IsRangeValid(definition.SetByCallerRange, catalog.SetByCallerDescriptors.Length))
                return RangeFailure(GasCatalogRangeKind.SetByCaller, definition.DefinitionId);
            if (!IsRangeValid(definition.TargetDataRange, catalog.TargetDataDescriptors.Length))
                return RangeFailure(GasCatalogRangeKind.TargetData, definition.DefinitionId);
            if (!IsRangeValid(definition.EffectContextFieldRange, catalog.EffectContextFieldDescriptors.Length))
                return RangeFailure(GasCatalogRangeKind.EffectContext, definition.DefinitionId);

            return ValidateGameplayEffectMaxima(in definition);
        }

        /// <summary>
        /// 验证四类 requirement 使用各自 range 且元素 phase 精确匹配。
        /// </summary>
        private static GasCatalogValidationResult ValidateRequirementRanges(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition)
        {
            var result = ValidateRequirementRange(ref catalog, definition.ApplicationRequirementRange,
                GasRequirementPhase.Application, GasCatalogRangeKind.ApplicationRequirement, definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            result = ValidateRequirementRange(ref catalog, definition.OngoingRequirementRange,
                GasRequirementPhase.Ongoing, GasCatalogRangeKind.OngoingRequirement, definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            result = ValidateRequirementRange(ref catalog, definition.RemovalRequirementRange,
                GasRequirementPhase.Removal, GasCatalogRangeKind.RemovalRequirement, definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            return ValidateRequirementRange(ref catalog, definition.ImmunityRequirementRange,
                GasRequirementPhase.Immunity, GasCatalogRangeKind.ImmunityRequirement, definition.DefinitionId);
        }

        /// <summary>
        /// 验证单个 requirement range 的 phase、Tag range 与稳定 Tag index。
        /// </summary>
        private static GasCatalogValidationResult ValidateRequirementRange(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            GasRequirementPhase expectedPhase,
            GasCatalogRangeKind rangeKind,
            int definitionId)
        {
            if (!IsRangeValid(range, catalog.Requirements.Length))
                return RangeFailure(rangeKind, definitionId);

            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var requirement = catalog.Requirements[elementIndex];
                if (requirement.Phase != expectedPhase)
                {
                    return Failure(
                        GasCatalogValidationError.RequirementPhaseMismatch,
                        rangeKind,
                        definitionId,
                        elementIndex);
                }

                if (!IsRequirementValid(ref catalog, in requirement))
                    return Failure(GasCatalogValidationError.RequirementInvalid, rangeKind, definitionId, elementIndex);
            }

            return Success();
        }

        /// <summary>
        /// 验证 requirement 的匹配枚举和严格递增 Tag index 列表。
        /// </summary>
        private static bool IsRequirementValid(
            ref GasDefinitionCatalogBlob catalog,
            in GasRequirementBlob requirement)
        {
            if (requirement.RequirementId <= 0
                || !IsTagRequirementMatchValid(requirement.Match)
                || !IsRangeValid(requirement.TagIndexRange, catalog.RequirementTagIndices.Length))
                return false;

            var previousIndex = -1;
            for (var offset = 0; offset < requirement.TagIndexRange.Count; offset++)
            {
                var tagIndex = catalog.RequirementTagIndices[requirement.TagIndexRange.Start + offset];
                if (tagIndex <= previousIndex || tagIndex >= catalog.TagCatalog.Entries.Length)
                    return false;

                previousIndex = tagIndex;
            }

            return requirement.TagIndexRange.Count > 0;
        }

        /// <summary>
        /// 验证 GameplayEffect 实际 range 数量没有超过生成期 maxima。
        /// </summary>
        private static GasCatalogValidationResult ValidateGameplayEffectMaxima(
            in GasGameplayEffectDefinitionBlob definition)
        {
            var requirementCount = definition.ApplicationRequirementRange.Count
                + definition.OngoingRequirementRange.Count
                + definition.RemovalRequirementRange.Count
                + definition.ImmunityRequirementRange.Count;
            if (!AreMaximaNonNegative(in definition.Maxima)
                || requirementCount > definition.Maxima.MaximumRequirementCount
                || definition.CaptureRange.Count > definition.Maxima.MaximumCaptureDescriptorCount
                || definition.ModifierRange.Count > definition.Maxima.MaximumModifierCount
                || definition.DirectEffectProgramRange.Count > definition.Maxima.MaximumDirectProgramNodeCount
                || definition.CueRange.Count > definition.Maxima.MaximumCueCount
                || definition.ValueViewRange.Count > definition.Maxima.MaximumValueViewCount
                || definition.EvaluatorProgramRange.Count > definition.Maxima.MaximumEvaluatorInstructionCount
                || definition.SetByCallerRange.Count > definition.Maxima.MaximumSetByCallerCount
                || definition.TargetDataRange.Count > definition.Maxima.MaximumTargetDataCount
                || definition.EffectContextFieldRange.Count > definition.Maxima.MaximumEffectContextFieldCount)
                return Failure(GasCatalogValidationError.DefinitionMaximaExceeded, definitionId: definition.DefinitionId);

            return Success();
        }

        /// <summary>
        /// 验证 GameplayEffect 引用的 capture、modifier、program、cue 与 ValueView 数据。
        /// </summary>
        private static GasCatalogValidationResult ValidateGameplayEffectPayloads(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition)
        {
            var result = ValidateCaptures(ref catalog, definition.CaptureRange, definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            result = ValidateModifiers(
                ref catalog,
                definition.ModifierRange,
                definition.EvaluatorProgramRange,
                definition.CaptureRange,
                definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            result = ValidateDirectProgram(ref catalog, definition.DirectEffectProgramRange, in definition.Maxima, definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            result = ValidateCues(ref catalog, definition.CueRange, definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            result = ValidateValueViews(ref catalog, definition.ValueViewRange, definition.RequiredValueViews, definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            result = ValidateEvaluatorProgram(
                ref catalog,
                definition.EvaluatorProgramRange,
                definition.CaptureRange.Count,
                definition.ValueViewRange.Count,
                definition.DefinitionId);
            return result.Succeeded ? ValidateSpecContracts(ref catalog, in definition) : result;
        }

        /// <summary>
        /// 验证 capture ordinal、ValueView、binding scope 和允许的解析 phase。
        /// </summary>
        private static GasCatalogValidationResult ValidateCaptures(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            int definitionId)
        {
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var capture = catalog.CaptureDescriptors[elementIndex];
                if (capture.CaptureOrdinal != offset
                    || capture.AttributeLayoutIndex < 0
                    || capture.AttributeLayoutIndex >= catalog.AttributeLayout.Entries.Length
                    || capture.ConsumerNodeOrdinal < 0
                    || capture.ConsumerFieldOrdinal < 0
                    || !IsCaptureEnumValid(in capture)
                    || !IsCapturePhaseCompatible(in capture))
                {
                    return Failure(
                        GasCatalogValidationError.CaptureDescriptorInvalid,
                        GasCatalogRangeKind.Capture,
                        definitionId,
                        elementIndex);
                }
            }

            return Success();
        }

        /// <summary>
        /// 验证 modifier 属性索引、枚举与自身 capture range。
        /// </summary>
        private static GasCatalogValidationResult ValidateModifiers(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            GasCatalogRange definitionEvaluatorRange,
            GasCatalogRange definitionCaptureRange,
            int definitionId)
        {
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var modifier = catalog.Modifiers[elementIndex];
                if (modifier.AttributeLayoutIndex < 0
                    || modifier.AttributeLayoutIndex >= catalog.AttributeLayout.Entries.Length
                    || !IsModifierOperationValid(modifier.Operation)
                    || !IsRangeValid(modifier.EvaluatorProgramRange, catalog.EvaluatorInstructions.Length)
                    || !IsRangeValid(modifier.CaptureRange, catalog.CaptureDescriptors.Length)
                    || !IsRangeContained(modifier.EvaluatorProgramRange, definitionEvaluatorRange)
                    || !IsRangeContained(modifier.CaptureRange, definitionCaptureRange))
                {
                    return Failure(
                        GasCatalogValidationError.ModifierInvalid,
                        GasCatalogRangeKind.Modifier,
                        definitionId,
                        elementIndex);
                }

                var result = ValidateEvaluatorProgram(
                    ref catalog,
                    modifier.EvaluatorProgramRange,
                    modifier.CaptureRange.Count,
                    0,
                    definitionId);
                if (!result.Succeeded)
                    return result;
            }

            return Success();
        }

        /// <summary>
        /// 验证 DirectEffectProgram ordinal、引用和有界输出总量。
        /// </summary>
        private static GasCatalogValidationResult ValidateDirectProgram(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            in GasDefinitionMaxima maxima,
            int definitionId)
        {
            var maximumOutputs = 0;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var node = catalog.DirectEffectProgramNodes[elementIndex];
                var found = GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                    ref catalog,
                    node.EffectDefinitionId,
                    out _);
                if (node.NodeOrdinal != offset
                    || node.MaximumTargetCount <= 0
                    || node.MaximumOutputCount <= 0
                    || !found
                    || maximumOutputs > int.MaxValue - node.MaximumOutputCount)
                {
                    return Failure(
                        GasCatalogValidationError.DirectProgramInvalid,
                        GasCatalogRangeKind.DirectEffectProgram,
                        definitionId,
                        elementIndex);
                }

                maximumOutputs += node.MaximumOutputCount;
            }

            return maximumOutputs <= maxima.MaximumDirectProgramOutputCount
                ? Success()
                : Failure(GasCatalogValidationError.DefinitionMaximaExceeded, definitionId: definitionId);
        }

        /// <summary>
        /// 验证 Cue ordinal、稳定 ID 与四阶段 flags。
        /// </summary>
        private static GasCatalogValidationResult ValidateCues(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            int definitionId)
        {
            const GasCuePhaseFlags allPhases = GasCuePhaseFlags.OnActive
                | GasCuePhaseFlags.WhileActive
                | GasCuePhaseFlags.Executed
                | GasCuePhaseFlags.Removed;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var cue = catalog.CueReferences[elementIndex];
                if (cue.CueDefinitionId <= 0
                    || cue.CueDefinitionOrdinal != offset
                    || cue.Phases == GasCuePhaseFlags.None
                    || (cue.Phases & ~allPhases) != 0)
                {
                    return Failure(
                        GasCatalogValidationError.CueInvalid,
                        GasCatalogRangeKind.Cue,
                        definitionId,
                        elementIndex);
                }
            }

            return Success();
        }

        /// <summary>
        /// 验证 evaluator 的 layout-resolved ValueView range 与声明 mask 精确相等。
        /// </summary>
        private static GasCatalogValidationResult ValidateValueViews(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            GasAttributeValueViewMask requiredViews,
            int definitionId)
        {
            var availableViews = GasAttributeValueViewMask.None;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var descriptor = catalog.ValueViews[elementIndex];
                var viewMask = ToValueViewMask(descriptor.ValueView);
                if (descriptor.AttributeLayoutIndex < 0
                    || descriptor.AttributeLayoutIndex >= catalog.AttributeLayout.Entries.Length
                    || viewMask == GasAttributeValueViewMask.None
                    || (availableViews & viewMask) != 0)
                {
                    return Failure(
                        GasCatalogValidationError.ValueViewContractMismatch,
                        GasCatalogRangeKind.ValueView,
                        definitionId,
                        elementIndex);
                }

                availableViews |= viewMask;
            }

            return availableViews == requiredViews
                ? Success()
                : Failure(GasCatalogValidationError.ValueViewContractMismatch, GasCatalogRangeKind.ValueView, definitionId);
        }

        /// <summary>
        /// 验证 postfix evaluator 指令、局部 operand index 与最终单值栈形状。
        /// </summary>
        private static GasCatalogValidationResult ValidateEvaluatorProgram(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            int captureCount,
            int valueViewCount,
            int definitionId)
        {
            if (range.Count == 0)
                return Success();

            var stackDepth = 0;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var instruction = catalog.EvaluatorInstructions[elementIndex];
                if (!TryGetEvaluatorStackEffect(
                    in instruction,
                    captureCount,
                    valueViewCount,
                    out var requiredDepth,
                    out var producedCount)
                    || stackDepth < requiredDepth)
                {
                    return Failure(
                        GasCatalogValidationError.EvaluatorProgramInvalid,
                        GasCatalogRangeKind.EvaluatorProgram,
                        definitionId,
                        elementIndex);
                }

                stackDepth = stackDepth - requiredDepth + producedCount;
            }

            return stackDepth == 1
                ? Success()
                : Failure(GasCatalogValidationError.EvaluatorProgramInvalid, GasCatalogRangeKind.EvaluatorProgram, definitionId);
        }

        /// <summary>
        /// 返回 evaluator 指令的消费/产出栈数量并验证 operand index。
        /// </summary>
        private static bool TryGetEvaluatorStackEffect(
            in GasEvaluatorInstructionBlob instruction,
            int captureCount,
            int valueViewCount,
            out int requiredDepth,
            out int producedCount)
        {
            requiredDepth = 0;
            producedCount = 0;
            switch (instruction.Opcode)
            {
                case GasEvaluatorOpcode.PushConstant:
                    producedCount = 1;
                    return !float.IsNaN(instruction.ConstantValue) && !float.IsInfinity(instruction.ConstantValue);
                case GasEvaluatorOpcode.PushCapture:
                    producedCount = 1;
                    return instruction.OperandIndex >= 0 && instruction.OperandIndex < captureCount;
                case GasEvaluatorOpcode.PushValueView:
                    producedCount = 1;
                    return instruction.OperandIndex >= 0 && instruction.OperandIndex < valueViewCount;
                case GasEvaluatorOpcode.PushStackCount:
                    producedCount = 1;
                    return true;
                case GasEvaluatorOpcode.Negate:
                    requiredDepth = 1;
                    producedCount = 1;
                    return true;
                case GasEvaluatorOpcode.Add:
                case GasEvaluatorOpcode.Subtract:
                case GasEvaluatorOpcode.Multiply:
                case GasEvaluatorOpcode.Divide:
                case GasEvaluatorOpcode.Maximum:
                case GasEvaluatorOpcode.Minimum:
                case GasEvaluatorOpcode.CompareLess:
                case GasEvaluatorOpcode.CompareLessOrEqual:
                case GasEvaluatorOpcode.CompareEqual:
                    requiredDepth = 2;
                    producedCount = 1;
                    return true;
                case GasEvaluatorOpcode.Clamp:
                case GasEvaluatorOpcode.Select:
                    requiredDepth = 3;
                    producedCount = 1;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 验证 Definition→Spec 的 SetByCaller、TargetData 与 EffectContext 字段契约。
        /// </summary>
        private static GasCatalogValidationResult ValidateSpecContracts(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition)
        {
            var result = ValidateSetByCaller(ref catalog, definition.SetByCallerRange, definition.DefinitionId);
            if (!result.Succeeded)
                return result;

            result = ValidateTargetData(ref catalog, definition.TargetDataRange, in definition.TargetPolicy, definition.DefinitionId);
            return result.Succeeded
                ? ValidateEffectContext(ref catalog, definition.EffectContextFieldRange, definition.DefinitionId)
                : result;
        }

        /// <summary>
        /// 验证 SetByCaller key 排序、field ordinal 与必填标记。
        /// </summary>
        private static GasCatalogValidationResult ValidateSetByCaller(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            int definitionId)
        {
            var previousKey = int.MinValue;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var descriptor = catalog.SetByCallerDescriptors[elementIndex];
                if (descriptor.KeyId <= previousKey
                    || descriptor.KeyId <= 0
                    || descriptor.FieldOrdinal != offset
                    || descriptor.Required > 1)
                {
                    return Failure(
                        GasCatalogValidationError.SetByCallerInvalid,
                        GasCatalogRangeKind.SetByCaller,
                        definitionId,
                        elementIndex);
                }

                previousKey = descriptor.KeyId;
            }

            return Success();
        }

        /// <summary>
        /// 验证 TargetData tagged-union 变体、ordinal 与 target policy 所需字段。
        /// </summary>
        private static GasCatalogValidationResult ValidateTargetData(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            in GasTargetPolicyBlob targetPolicy,
            int definitionId)
        {
            uint seenVariants = 0;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var descriptor = catalog.TargetDataDescriptors[elementIndex];
                var variantValid = descriptor.Variant >= GasTargetDataVariant.StableAsc
                    && descriptor.Variant <= GasTargetDataVariant.FrozenSpatialShape;
                var variantBit = variantValid ? 1u << (int)descriptor.Variant : 0u;
                if (!variantValid
                    || descriptor.FieldOrdinal != offset
                    || descriptor.Required > 1
                    || (seenVariants & variantBit) != 0)
                {
                    return Failure(
                        GasCatalogValidationError.TargetDataInvalid,
                        GasCatalogRangeKind.TargetData,
                        definitionId,
                        elementIndex);
                }

                seenVariants |= variantBit;
            }

            var hasStableAsc = (seenVariants & (1u << (int)GasTargetDataVariant.StableAsc)) != 0;
            var spatialMask = (1u << (int)GasTargetDataVariant.FrozenSpatialPoint)
                | (1u << (int)GasTargetDataVariant.FrozenSpatialHit)
                | (1u << (int)GasTargetDataVariant.FrozenSpatialShape);
            var missingAsc = targetPolicy.LogicalTarget == GasLogicalTargetPolicy.FrozenAsc && !hasStableAsc;
            var missingSpatial = targetPolicy.Spatial == GasSpatialTargetPolicy.FrozenSpatial
                && (seenVariants & spatialMask) == 0;
            return missingAsc || missingSpatial
                ? Failure(GasCatalogValidationError.TargetDataInvalid, GasCatalogRangeKind.TargetData, definitionId)
                : Success();
        }

        /// <summary>
        /// 验证 EffectContext 字段的封闭枚举、ordinal、唯一性与必填标记。
        /// </summary>
        private static GasCatalogValidationResult ValidateEffectContext(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            int definitionId)
        {
            uint seenFields = 0;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var elementIndex = range.Start + offset;
                var descriptor = catalog.EffectContextFieldDescriptors[elementIndex];
                var fieldValid = descriptor.Field >= GasEffectContextFieldKind.CausalityId
                    && descriptor.Field <= GasEffectContextFieldKind.Level;
                var fieldBit = fieldValid ? 1u << (int)descriptor.Field : 0u;
                if (!fieldValid
                    || descriptor.FieldOrdinal != offset
                    || descriptor.Required > 1
                    || (seenFields & fieldBit) != 0)
                {
                    return Failure(
                        GasCatalogValidationError.EffectContextInvalid,
                        GasCatalogRangeKind.EffectContext,
                        definitionId,
                        elementIndex);
                }

                seenFields |= fieldBit;
            }

            return Success();
        }

        /// <summary>
        /// 判断一个 range 是否落在指定根数组的合法半开区间内。
        /// </summary>
        private static bool IsRangeValid(GasCatalogRange range, int length)
        {
            return range.Start >= 0
                && range.Count >= 0
                && range.Count <= length
                && range.Start <= length - range.Count;
        }

        /// <summary>
        /// 判断子 range 是否完整落在所属 Definition 的半开总 range 内。
        /// </summary>
        private static bool IsRangeContained(GasCatalogRange inner, GasCatalogRange outer)
        {
            return inner.Start >= outer.Start
                && inner.Count <= outer.Count
                && inner.Start - outer.Start <= outer.Count - inner.Count;
        }

        /// <summary>
        /// 判断 Catalog 浮点配置是否为可执行的有限数值。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 判断 target policy 四个正交枚举是否均为受支持值。
        /// </summary>
        private static bool IsTargetPolicyValid(GasTargetPolicyBlob policy)
        {
            return policy.LogicalTarget >= GasLogicalTargetPolicy.Self
                && policy.LogicalTarget <= GasLogicalTargetPolicy.ResolveAtCommit
                && policy.Avatar >= GasAvatarTargetPolicy.FollowAsc
                && policy.Avatar <= GasAvatarTargetPolicy.RequireSameAvatar
                && policy.Spatial >= GasSpatialTargetPolicy.None
                && policy.Spatial <= GasSpatialTargetPolicy.ResampleAtApplication
                && policy.Life >= GasTargetLifePolicy.AliveOnly
                && policy.Life <= GasTargetLifePolicy.AnyLifeState;
        }

        /// <summary>
        /// 判断 Effect lifetime 是否属于 Runtime v1 封闭枚举。
        /// </summary>
        private static bool IsLifetimeValid(GasEffectLifetimePolicy lifetime)
        {
            return lifetime >= GasEffectLifetimePolicy.Instant
                && lifetime <= GasEffectLifetimePolicy.Infinite;
        }

        /// <summary>
        /// 判断 Effect 的 stack/time/evaluator policy 枚举是否均受支持。
        /// </summary>
        private static bool IsEffectPolicyEnumValid(in GasGameplayEffectDefinitionBlob definition)
        {
            return definition.StackPolicy >= GasStackPolicy.None
                && definition.StackPolicy <= GasStackPolicy.AggregateByTarget
                && definition.StackPayloadPolicy >= GasStackPayloadPolicy.None
                && definition.StackPayloadPolicy <= GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance
                && definition.StackLimitApplicationPolicy >= GasStackLimitApplicationPolicy.None
                && definition.StackLimitApplicationPolicy <= GasStackLimitApplicationPolicy.AcceptAndKeepLimit
                && definition.DurationRefreshPolicy >= GasDurationRefreshPolicy.Never
                && definition.DurationRefreshPolicy <= GasDurationRefreshPolicy.OnSuccessfulApplication
                && definition.PeriodResetPolicy >= GasPeriodResetPolicy.Never
                && definition.PeriodResetPolicy <= GasPeriodResetPolicy.OnSuccessfulApplication
                && definition.ExpiryPolicy >= GasExpiryPolicy.Remove
                && definition.ExpiryPolicy <= GasExpiryPolicy.RemoveOneStackAndRefreshDuration
                && definition.ExpiryPeriodPolicy >= GasExpiryPeriodPolicy.Stop
                && definition.ExpiryPeriodPolicy <= GasExpiryPeriodPolicy.Reset
                && definition.ExpirySameTickPolicy >= GasExpirySameTickPolicy.ExpiryBeforePeriodDue
                && definition.ExpirySameTickPolicy <= GasExpirySameTickPolicy.PeriodDueBeforeExpiry
                && definition.InhibitTimePolicy >= GasInhibitTimePolicy.PauseDuration
                && definition.InhibitTimePolicy <= GasInhibitTimePolicy.DurationContinues
                // v1 ActiveEffectSlot 尚无 inhibition anchor，PauseDuration 不能静默降级为 continue。
                && definition.InhibitTimePolicy != GasInhibitTimePolicy.PauseDuration
                && definition.InhibitedPeriodPolicy >= GasInhibitedPeriodPolicy.None
                && definition.InhibitedPeriodPolicy <= GasInhibitedPeriodPolicy.ContinueExecution
                && definition.MissedPeriodPolicy >= GasMissedPeriodPolicy.SkipNoCatchUp
                && definition.MissedPeriodPolicy <= GasMissedPeriodPolicy.CatchUpBounded
                // v1 lifecycle 只物化单次 claim，未生成 catch-up 预算与 execution ordinal。
                && definition.MissedPeriodPolicy == GasMissedPeriodPolicy.SkipNoCatchUp
                && IsSupportedStackKey(in definition);
        }

        /// <summary>
        /// 判断零 stack 定义是否完全关闭所有 stack identity/payload policy。
        /// </summary>
        private static bool HasNoStackPolicy(in GasGameplayEffectDefinitionBlob definition)
        {
            return definition.StackKey == GasStackKeyFields.None
                && definition.StackPolicy == GasStackPolicy.None
                && definition.StackPayloadPolicy == GasStackPayloadPolicy.None
                && definition.StackLimitApplicationPolicy == GasStackLimitApplicationPolicy.None;
        }

        /// <summary>
        /// 判断有 stack 定义是否同时声明 key、聚合与 payload policy。
        /// </summary>
        private static bool HasCompleteStackPolicy(in GasGameplayEffectDefinitionBlob definition)
        {
            const GasStackKeyFields required = GasStackKeyFields.Definition
                | GasStackKeyFields.TargetAsc
                | GasStackKeyFields.SourceAsc;
            const GasStackKeyFields all = required;
            return (definition.StackKey & required) == required
                && (definition.StackKey & ~all) == 0
                && definition.StackPolicy != GasStackPolicy.None
                && definition.StackPayloadPolicy != GasStackPayloadPolicy.None
                && definition.StackLimitApplicationPolicy != GasStackLimitApplicationPolicy.None;
        }

        /// <summary>
        /// v1 只物化 Definition/TargetASC/SourceASC 三维 stack key，拒绝未落地的 StackingId 维度。
        /// </summary>
        private static bool IsSupportedStackKey(
            in GasGameplayEffectDefinitionBlob definition)
        {
            return (definition.StackKey & GasStackKeyFields.StackingId) == 0;
        }

        /// <summary>
        /// 判断所有 per-definition maxima 均为非负静态容量。
        /// </summary>
        private static bool AreMaximaNonNegative(in GasDefinitionMaxima maxima)
        {
            return maxima.MaximumTargetCount >= 0
                && maxima.MaximumPlannedApplicationCount >= 0
                && maxima.MaximumRequirementCount >= 0
                && maxima.MaximumCaptureDescriptorCount >= 0
                && maxima.MaximumModifierCount >= 0
                && maxima.MaximumDirectProgramNodeCount >= 0
                && maxima.MaximumDirectProgramOutputCount >= 0
                && maxima.MaximumCueCount >= 0
                && maxima.MaximumValueViewCount >= 0
                && maxima.MaximumEvaluatorInstructionCount >= 0
                && maxima.MaximumSetByCallerCount >= 0
                && maxima.MaximumTargetDataCount >= 0
                && maxima.MaximumEffectContextFieldCount >= 0;
        }

        /// <summary>
        /// 判断 Tag requirement match 是否属于封闭枚举。
        /// </summary>
        private static bool IsTagRequirementMatchValid(GasTagRequirementMatch match)
        {
            return match >= GasTagRequirementMatch.All && match <= GasTagRequirementMatch.None;
        }

        /// <summary>
        /// 判断 capture 各枚举、Live scope 与 owner-gone policy 是否有效。
        /// </summary>
        private static bool IsCaptureEnumValid(in GasCaptureDescriptorBlob capture)
        {
            var ownerValid = capture.Owner >= GasCaptureOwner.Source && capture.Owner <= GasCaptureOwner.Target;
            var bindingValid = capture.Binding >= GasCaptureBinding.Snapshot && capture.Binding <= GasCaptureBinding.Live;
            var phaseValid = capture.Phase >= GasCapturePhase.OwnerPlanBuild
                && capture.Phase <= GasCapturePhase.CrossAscMaintenance;
            var goneValid = capture.GonePolicy >= GasCaptureGonePolicy.RejectApplication
                && capture.GonePolicy <= GasCaptureGonePolicy.RemoveConsumer;
            var scopeValid = capture.Binding == GasCaptureBinding.Snapshot
                ? capture.LiveScope == GasLiveCaptureScope.None
                : capture.LiveScope >= GasLiveCaptureScope.SameAsc && capture.LiveScope <= GasLiveCaptureScope.CrossAsc;
            return ownerValid && bindingValid && phaseValid && goneValid && scopeValid
                && ToValueViewMask(capture.ValueView) != GasAttributeValueViewMask.None;
        }

        /// <summary>
        /// 将四种 capture 组合约束到冻结的合法解析 phase。
        /// </summary>
        private static bool IsCapturePhaseCompatible(in GasCaptureDescriptorBlob capture)
        {
            if (capture.Binding == GasCaptureBinding.Snapshot)
            {
                return capture.Owner == GasCaptureOwner.Source
                    ? capture.Phase == GasCapturePhase.OwnerPlanBuild
                        || capture.Phase == GasCapturePhase.SourceSpecProjection
                    : capture.Phase == GasCapturePhase.TargetApplication;
            }

            if (capture.Owner == GasCaptureOwner.Target)
                return capture.LiveScope == GasLiveCaptureScope.SameAsc
                    && capture.Phase == GasCapturePhase.TargetStabilization;

            return capture.LiveScope == GasLiveCaptureScope.SameAsc
                ? capture.Phase == GasCapturePhase.TargetStabilization
                : capture.Phase == GasCapturePhase.CrossAscMaintenance;
        }

        /// <summary>
        /// 判断 modifier operation 是否属于封闭枚举。
        /// </summary>
        private static bool IsModifierOperationValid(GasModifierOperation operation)
        {
            return operation >= GasModifierOperation.Add && operation <= GasModifierOperation.Override;
        }

        /// <summary>
        /// 把单个 ValueView 转成可进行闭世界精确匹配的 bit。
        /// </summary>
        private static GasAttributeValueViewMask ToValueViewMask(GasAttributeValueView view)
        {
            switch (view)
            {
                case GasAttributeValueView.Base:
                    return GasAttributeValueViewMask.Base;
                case GasAttributeValueView.Current:
                    return GasAttributeValueViewMask.Current;
                case GasAttributeValueView.DefinitionMaxValue:
                    return GasAttributeValueViewMask.DefinitionMaxValue;
                case GasAttributeValueView.Final:
                    return GasAttributeValueViewMask.Final;
                case GasAttributeValueView.Bonus:
                    return GasAttributeValueViewMask.Bonus;
                case GasAttributeValueView.Contribution:
                    return GasAttributeValueViewMask.Contribution;
                case GasAttributeValueView.ModifierList:
                    return GasAttributeValueViewMask.ModifierList;
                default:
                    return GasAttributeValueViewMask.None;
            }
        }

        /// <summary>
        /// 创建无错误的校验结果。
        /// </summary>
        private static GasCatalogValidationResult Success()
        {
            return new GasCatalogValidationResult(GasCatalogValidationError.None);
        }

        /// <summary>
        /// 创建携带 Definition/字段定位的校验失败结果。
        /// </summary>
        private static GasCatalogValidationResult Failure(
            GasCatalogValidationError error,
            GasCatalogRangeKind rangeKind = GasCatalogRangeKind.None,
            int definitionId = 0,
            int elementIndex = -1)
        {
            return new GasCatalogValidationResult(error, rangeKind, definitionId, elementIndex);
        }

        /// <summary>
        /// 创建携带 range 字段和 Definition ID 的越界失败结果。
        /// </summary>
        private static GasCatalogValidationResult RangeFailure(
            GasCatalogRangeKind rangeKind,
            int definitionId)
        {
            return Failure(GasCatalogValidationError.RangeOutOfBounds, rangeKind, definitionId);
        }
    }
}
