namespace GAS.Runtime
{
    /// <summary>
    /// 按版本化 canonical 字段序列计算 Definition Catalog 的内容身份，避免 Blob header 自证或平台内存布局污染哈希。
    /// </summary>
    internal static class GasDefinitionCatalogContentHasher
    {
        // 8-byte little-endian ASCII domain tags make the persisted FNV protocol reviewable and collision-separated.
        private const ulong CatalogDomainTag = 0x31474F4C41544143UL; // "CATALOG1"
        private const ulong ContentDomainTag = 0x544E45544E4F4347UL; // "GCONTENT"
        private const ulong AttributeLayoutDomainTag = 0x3154554F59414C41UL; // "ALAYOUT1"
        private const ulong TagCatalogDomainTag = 0x3130544143474154UL; // "TAGCAT01"
        private const int CanonicalFormatVersion = 1;

        /// <summary>
        /// 固定根数组的 canonical ordinal，确保不同数组及空序列之间不可互换。
        /// </summary>
        private enum RootArrayOrdinal : int
        {
            AttributeEntries = 1,
            TagEntries = 2,
            TagAncestorIndices = 3,
            AbilityIndex = 4,
            Abilities = 5,
            GameplayEffectIndex = 6,
            GameplayEffects = 7,
            Requirements = 8,
            RequirementTagIndices = 9,
            CaptureDescriptors = 10,
            Modifiers = 11,
            DirectEffectProgramNodes = 12,
            CueReferences = 13,
            ValueViews = 14,
            EvaluatorInstructions = 15,
            SetByCallerDescriptors = 16,
            TargetDataDescriptors = 17,
            EffectContextFieldDescriptors = 18,
        }

        /// <summary>
        /// 从候选 Catalog 的全部 Runtime 消费字段计算平台无关的 FNV-1a 64 位内容哈希。
        /// </summary>
        internal static ulong Compute(ref GasDefinitionCatalogBlob catalog)
        {
            var hash = GasBoundaryFnv1A64.Create();
            AddDomain(ref hash, ContentDomainTag);
            AddAttributeEntries(ref hash, ref catalog.AttributeLayout);
            AddTagCatalog(ref hash, ref catalog.TagCatalog);
            AddDefinitionIndices(ref hash, RootArrayOrdinal.AbilityIndex, ref catalog.AbilityIndex);
            AddAbilities(ref hash, ref catalog);
            AddDefinitionIndices(ref hash, RootArrayOrdinal.GameplayEffectIndex, ref catalog.GameplayEffectIndex);
            AddGameplayEffects(ref hash, ref catalog);
            AddRequirements(ref hash, ref catalog);
            AddIntArray(ref hash, RootArrayOrdinal.RequirementTagIndices, ref catalog.RequirementTagIndices);
            AddCaptureDescriptors(ref hash, ref catalog);
            AddModifiers(ref hash, ref catalog);
            AddDirectEffectProgramNodes(ref hash, ref catalog);
            AddCueReferences(ref hash, ref catalog);
            AddValueViews(ref hash, ref catalog);
            AddEvaluatorInstructions(ref hash, ref catalog);
            AddSetByCallerDescriptors(ref hash, ref catalog);
            AddTargetDataDescriptors(ref hash, ref catalog);
            AddEffectContextFieldDescriptors(ref hash, ref catalog);
            return hash.Value;
        }

        /// <summary>
        /// 计算 AttributeLayout 条目的独立 canonical hash，不读取其 LayoutHash 镜像。
        /// </summary>
        internal static ulong ComputeAttributeLayout(ref GasAttributeLayoutBlob layout)
        {
            var hash = GasBoundaryFnv1A64.Create();
            AddDomain(ref hash, AttributeLayoutDomainTag);
            AddAttributeEntries(ref hash, ref layout);
            return hash.Value;
        }

        /// <summary>
        /// 计算 TagCatalog 条目与 ancestor 闭包的独立 canonical hash，不读取其 CatalogHash 镜像。
        /// </summary>
        internal static ulong ComputeTagCatalog(ref GasTagCatalogBlob catalog)
        {
            var hash = GasBoundaryFnv1A64.Create();
            AddDomain(ref hash, TagCatalogDomainTag);
            AddTagCatalog(ref hash, ref catalog);
            return hash.Value;
        }

        /// <summary>
        /// 先写入两个子目录 hash，再以排除全部镜像字段的 canonical 内容封印 Catalog。
        /// </summary>
        internal static void Stamp(ref GasDefinitionCatalogBlob catalog)
        {
            catalog.AttributeLayout.LayoutHash = ComputeAttributeLayout(ref catalog.AttributeLayout);
            catalog.TagCatalog.CatalogHash = ComputeTagCatalog(ref catalog.TagCatalog);
            catalog.ContentHash = Compute(ref catalog);
        }

        /// <summary>
        /// 写入 Catalog 目的域与 canonical 编码版本，隔离其它 FNV 协议和未来编码修订。
        /// </summary>
        private static void AddDomain(ref GasBoundaryFnv1A64 hash, ulong purposeDomainTag)
        {
            hash.AddUInt64(CatalogDomainTag);
            hash.AddUInt64(purposeDomainTag);
            hash.AddInt32(CanonicalFormatVersion);
        }

        /// <summary>
        /// 写入数组字段 ordinal 与元素数量，避免不同分段产生相同拼接字节。
        /// </summary>
        private static void AddArrayHeader(
            ref GasBoundaryFnv1A64 hash,
            RootArrayOrdinal ordinal,
            int length)
        {
            hash.AddInt32((int)ordinal);
            hash.AddInt32(length);
        }

        /// <summary>
        /// 写入 AttributeLayout 的全部语义条目，不包含其派生 LayoutHash 镜像。
        /// </summary>
        private static void AddAttributeEntries(
            ref GasBoundaryFnv1A64 hash,
            ref GasAttributeLayoutBlob layout)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.AttributeEntries, layout.Entries.Length);
            for (var index = 0; index < layout.Entries.Length; index++)
            {
                ref var entry = ref layout.Entries[index];
                hash.AddInt32(entry.AttributeId);
                hash.AddInt32(entry.LayoutIndex);
                hash.AddByte((byte)entry.DomainRole);
                hash.AddFloat(entry.DefaultValue);
                hash.AddFloat(entry.MinimumValue);
                hash.AddFloat(entry.MaximumValue);
                hash.AddByte(entry.ClampMinimum);
                hash.AddByte(entry.ClampMaximum);
            }
        }

        /// <summary>
        /// 写入 Tag 条目和 ancestor 闭包，不包含其派生 CatalogHash 镜像。
        /// </summary>
        private static void AddTagCatalog(
            ref GasBoundaryFnv1A64 hash,
            ref GasTagCatalogBlob catalog)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.TagEntries, catalog.Entries.Length);
            for (var index = 0; index < catalog.Entries.Length; index++)
            {
                ref var entry = ref catalog.Entries[index];
                hash.AddInt32(entry.TagId);
                hash.AddInt32(entry.TagIndex);
                AddRange(ref hash, in entry.AncestorIndexRange);
            }

            AddIntArray(
                ref hash,
                RootArrayOrdinal.TagAncestorIndices,
                ref catalog.AncestorIndices);
        }

        /// <summary>
        /// 写入 Ability 或 GameplayEffect 的稳定 Definition 索引。
        /// </summary>
        private static void AddDefinitionIndices(
            ref GasBoundaryFnv1A64 hash,
            RootArrayOrdinal ordinal,
            ref Unity.Entities.BlobArray<GasDefinitionIndexEntry> entries)
        {
            AddArrayHeader(ref hash, ordinal, entries.Length);
            for (var index = 0; index < entries.Length; index++)
            {
                ref var entry = ref entries[index];
                hash.AddInt32(entry.DefinitionId);
                hash.AddInt32(entry.DefinitionIndex);
            }
        }

        /// <summary>
        /// 写入一个 canonical int 数组及其根字段身份。
        /// </summary>
        private static void AddIntArray(
            ref GasBoundaryFnv1A64 hash,
            RootArrayOrdinal ordinal,
            ref Unity.Entities.BlobArray<int> values)
        {
            AddArrayHeader(ref hash, ordinal, values.Length);
            for (var index = 0; index < values.Length; index++)
                hash.AddInt32(values[index]);
        }

        /// <summary>
        /// 写入全部 Ability Definition 及其 owner-local 合约。
        /// </summary>
        private static void AddAbilities(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.Abilities, catalog.Abilities.Length);
            for (var index = 0; index < catalog.Abilities.Length; index++)
            {
                ref var definition = ref catalog.Abilities[index];
                hash.AddInt32(definition.DefinitionId);
                hash.AddInt32(definition.Level);
                hash.AddInt32(definition.MaxConcurrentActivations);
                AddRange(ref hash, in definition.ActivationRequirementRange);
                AddCostMutation(ref hash, in definition.CostMutationContract);
                AddCooldownGate(ref hash, in definition.CooldownGateContract);
                AddTargetPolicy(ref hash, in definition.TargetPolicy);
                AddRange(ref hash, in definition.DirectEffectProgramRange);
                AddRange(ref hash, in definition.CueRange);
                AddMaxima(ref hash, in definition.Maxima);
            }
        }

        /// <summary>
        /// 写入 Ability 的确定性 cost mutation 合约。
        /// </summary>
        private static void AddCostMutation(
            ref GasBoundaryFnv1A64 hash,
            in GasCostMutationContractBlob contract)
        {
            hash.AddByte(contract.Enabled);
            hash.AddInt32(contract.AttributeLayoutIndex);
            hash.AddFloat(contract.BaseDelta);
            hash.AddFloat(contract.CurrentDelta);
        }

        /// <summary>
        /// 写入 Ability 的 cooldown gate 合约。
        /// </summary>
        private static void AddCooldownGate(
            ref GasBoundaryFnv1A64 hash,
            in GasCooldownGateContractBlob contract)
        {
            hash.AddByte(contract.Enabled);
            hash.AddInt32(contract.GateKey);
            hash.AddInt32(contract.DurationTicks);
            hash.AddInt32(contract.OwnedTagIndex);
        }

        /// <summary>
        /// 写入全部 GameplayEffect Definition 及其生命周期策略和 payload ranges。
        /// </summary>
        private static void AddGameplayEffects(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.GameplayEffects, catalog.GameplayEffects.Length);
            for (var index = 0; index < catalog.GameplayEffects.Length; index++)
            {
                ref var definition = ref catalog.GameplayEffects[index];
                AddGameplayEffectHeader(ref hash, in definition);
                AddGameplayEffectRanges(ref hash, in definition);
                AddGameplayEffectPolicies(ref hash, in definition);
                AddMaxima(ref hash, in definition.Maxima);
            }
        }

        /// <summary>
        /// 写入 GameplayEffect 的身份、lifetime 与 target policy。
        /// </summary>
        private static void AddGameplayEffectHeader(
            ref GasBoundaryFnv1A64 hash,
            in GasGameplayEffectDefinitionBlob definition)
        {
            hash.AddInt32(definition.DefinitionId);
            hash.AddByte((byte)definition.Lifetime);
            AddTargetPolicy(ref hash, in definition.TargetPolicy);
        }

        /// <summary>
        /// 写入 GameplayEffect 引用的全部规范 payload ranges。
        /// </summary>
        private static void AddGameplayEffectRanges(
            ref GasBoundaryFnv1A64 hash,
            in GasGameplayEffectDefinitionBlob definition)
        {
            AddRange(ref hash, in definition.ApplicationRequirementRange);
            AddRange(ref hash, in definition.OngoingRequirementRange);
            AddRange(ref hash, in definition.RemovalRequirementRange);
            AddRange(ref hash, in definition.ImmunityRequirementRange);
            AddRange(ref hash, in definition.CaptureRange);
            AddRange(ref hash, in definition.ModifierRange);
            AddRange(ref hash, in definition.DirectEffectProgramRange);
            AddRange(ref hash, in definition.CueRange);
            AddRange(ref hash, in definition.ValueViewRange);
            AddRange(ref hash, in definition.EvaluatorProgramRange);
            AddRange(ref hash, in definition.SetByCallerRange);
            AddRange(ref hash, in definition.TargetDataRange);
            AddRange(ref hash, in definition.EffectContextFieldRange);
        }

        /// <summary>
        /// 写入 GameplayEffect 的 value-view、时间、stack、expiry 与 inhibit 策略。
        /// </summary>
        private static void AddGameplayEffectPolicies(
            ref GasBoundaryFnv1A64 hash,
            in GasGameplayEffectDefinitionBlob definition)
        {
            hash.AddUInt16((ushort)definition.RequiredValueViews);
            hash.AddInt32(definition.DurationTicks);
            hash.AddInt32(definition.PeriodTicks);
            hash.AddInt32(definition.StackLimit);
            hash.AddByte((byte)definition.StackKey);
            hash.AddByte((byte)definition.StackPolicy);
            hash.AddByte((byte)definition.StackPayloadPolicy);
            hash.AddByte((byte)definition.StackLimitApplicationPolicy);
            hash.AddByte((byte)definition.DurationRefreshPolicy);
            hash.AddByte((byte)definition.PeriodResetPolicy);
            hash.AddByte((byte)definition.ExpiryPolicy);
            hash.AddByte((byte)definition.ExpiryPeriodPolicy);
            hash.AddByte((byte)definition.ExpirySameTickPolicy);
            hash.AddByte((byte)definition.InhibitTimePolicy);
            hash.AddByte((byte)definition.InhibitedPeriodPolicy);
            hash.AddByte((byte)definition.MissedPeriodPolicy);
            hash.AddByte(definition.ExecuteOnApplication);
        }

        /// <summary>
        /// 写入 Requirement phase、匹配方式与 tag index range。
        /// </summary>
        private static void AddRequirements(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.Requirements, catalog.Requirements.Length);
            for (var index = 0; index < catalog.Requirements.Length; index++)
            {
                ref var requirement = ref catalog.Requirements[index];
                hash.AddInt32(requirement.RequirementId);
                hash.AddByte((byte)requirement.Phase);
                hash.AddByte((byte)requirement.Match);
                AddRange(ref hash, in requirement.TagIndexRange);
            }
        }

        /// <summary>
        /// 写入 capture owner、binding、phase、scope 与 consumer ordinal。
        /// </summary>
        private static void AddCaptureDescriptors(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.CaptureDescriptors, catalog.CaptureDescriptors.Length);
            for (var index = 0; index < catalog.CaptureDescriptors.Length; index++)
            {
                ref var descriptor = ref catalog.CaptureDescriptors[index];
                hash.AddInt32(descriptor.CaptureOrdinal);
                hash.AddByte((byte)descriptor.Owner);
                hash.AddByte((byte)descriptor.Binding);
                hash.AddByte((byte)descriptor.Phase);
                hash.AddByte((byte)descriptor.LiveScope);
                hash.AddByte((byte)descriptor.GonePolicy);
                hash.AddByte((byte)descriptor.ValueView);
                hash.AddInt32(descriptor.AttributeLayoutIndex);
                hash.AddInt32(descriptor.ConsumerNodeOrdinal);
                hash.AddInt32(descriptor.ConsumerFieldOrdinal);
            }
        }

        /// <summary>
        /// 写入 Modifier 的属性目标、operation 与 evaluator/capture ranges。
        /// </summary>
        private static void AddModifiers(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.Modifiers, catalog.Modifiers.Length);
            for (var index = 0; index < catalog.Modifiers.Length; index++)
            {
                ref var modifier = ref catalog.Modifiers[index];
                hash.AddInt32(modifier.AttributeLayoutIndex);
                hash.AddByte((byte)modifier.Operation);
                AddRange(ref hash, in modifier.EvaluatorProgramRange);
                AddRange(ref hash, in modifier.CaptureRange);
            }
        }

        /// <summary>
        /// 写入 DirectEffect program 的 node ordinal、目标 Effect 与生成上界。
        /// </summary>
        private static void AddDirectEffectProgramNodes(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(
                ref hash,
                RootArrayOrdinal.DirectEffectProgramNodes,
                catalog.DirectEffectProgramNodes.Length);
            for (var index = 0; index < catalog.DirectEffectProgramNodes.Length; index++)
            {
                ref var node = ref catalog.DirectEffectProgramNodes[index];
                hash.AddInt32(node.NodeOrdinal);
                hash.AddInt32(node.EffectDefinitionId);
                hash.AddInt32(node.MaximumTargetCount);
                hash.AddInt32(node.MaximumOutputCount);
            }
        }

        /// <summary>
        /// 写入 Cue stable identity、definition ordinal 与 phase mask。
        /// </summary>
        private static void AddCueReferences(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.CueReferences, catalog.CueReferences.Length);
            for (var index = 0; index < catalog.CueReferences.Length; index++)
            {
                ref var cue = ref catalog.CueReferences[index];
                hash.AddInt32(cue.CueDefinitionId);
                hash.AddInt32(cue.CueDefinitionOrdinal);
                hash.AddByte((byte)cue.Phases);
            }
        }

        /// <summary>
        /// 写入 Attribute value-view descriptor 的 layout index 与视图种类。
        /// </summary>
        private static void AddValueViews(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(ref hash, RootArrayOrdinal.ValueViews, catalog.ValueViews.Length);
            for (var index = 0; index < catalog.ValueViews.Length; index++)
            {
                ref var descriptor = ref catalog.ValueViews[index];
                hash.AddInt32(descriptor.AttributeLayoutIndex);
                hash.AddByte((byte)descriptor.ValueView);
            }
        }

        /// <summary>
        /// 写入 evaluator opcode、operand index 与常量的 IEEE-754 原始位。
        /// </summary>
        private static void AddEvaluatorInstructions(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(
                ref hash,
                RootArrayOrdinal.EvaluatorInstructions,
                catalog.EvaluatorInstructions.Length);
            for (var index = 0; index < catalog.EvaluatorInstructions.Length; index++)
            {
                ref var instruction = ref catalog.EvaluatorInstructions[index];
                hash.AddByte((byte)instruction.Opcode);
                hash.AddInt32(instruction.OperandIndex);
                hash.AddFloat(instruction.ConstantValue);
            }
        }

        /// <summary>
        /// 写入 SetByCaller key、field ordinal 与 required 标志。
        /// </summary>
        private static void AddSetByCallerDescriptors(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(
                ref hash,
                RootArrayOrdinal.SetByCallerDescriptors,
                catalog.SetByCallerDescriptors.Length);
            for (var index = 0; index < catalog.SetByCallerDescriptors.Length; index++)
            {
                ref var descriptor = ref catalog.SetByCallerDescriptors[index];
                hash.AddInt32(descriptor.KeyId);
                hash.AddInt32(descriptor.FieldOrdinal);
                hash.AddByte(descriptor.Required);
            }
        }

        /// <summary>
        /// 写入 TargetData variant、field ordinal 与 required 标志。
        /// </summary>
        private static void AddTargetDataDescriptors(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(
                ref hash,
                RootArrayOrdinal.TargetDataDescriptors,
                catalog.TargetDataDescriptors.Length);
            for (var index = 0; index < catalog.TargetDataDescriptors.Length; index++)
            {
                ref var descriptor = ref catalog.TargetDataDescriptors[index];
                hash.AddByte((byte)descriptor.Variant);
                hash.AddInt32(descriptor.FieldOrdinal);
                hash.AddByte(descriptor.Required);
            }
        }

        /// <summary>
        /// 写入 EffectContext field kind、field ordinal 与 required 标志。
        /// </summary>
        private static void AddEffectContextFieldDescriptors(
            ref GasBoundaryFnv1A64 hash,
            ref GasDefinitionCatalogBlob catalog)
        {
            AddArrayHeader(
                ref hash,
                RootArrayOrdinal.EffectContextFieldDescriptors,
                catalog.EffectContextFieldDescriptors.Length);
            for (var index = 0; index < catalog.EffectContextFieldDescriptors.Length; index++)
            {
                ref var descriptor = ref catalog.EffectContextFieldDescriptors[index];
                hash.AddByte((byte)descriptor.Field);
                hash.AddInt32(descriptor.FieldOrdinal);
                hash.AddByte(descriptor.Required);
            }
        }

        /// <summary>
        /// 写入 Logical/Avatar/Spatial/Life 四维 target policy。
        /// </summary>
        private static void AddTargetPolicy(
            ref GasBoundaryFnv1A64 hash,
            in GasTargetPolicyBlob policy)
        {
            hash.AddByte((byte)policy.LogicalTarget);
            hash.AddByte((byte)policy.Avatar);
            hash.AddByte((byte)policy.Spatial);
            hash.AddByte((byte)policy.Life);
        }

        /// <summary>
        /// 写入生成期证明的全部 definition maxima 上界。
        /// </summary>
        private static void AddMaxima(
            ref GasBoundaryFnv1A64 hash,
            in GasDefinitionMaxima maxima)
        {
            hash.AddInt32(maxima.MaximumTargetCount);
            hash.AddInt32(maxima.MaximumPlannedApplicationCount);
            hash.AddInt32(maxima.MaximumRequirementCount);
            hash.AddInt32(maxima.MaximumCaptureDescriptorCount);
            hash.AddInt32(maxima.MaximumModifierCount);
            hash.AddInt32(maxima.MaximumDirectProgramNodeCount);
            hash.AddInt32(maxima.MaximumDirectProgramOutputCount);
            hash.AddInt32(maxima.MaximumCueCount);
            hash.AddInt32(maxima.MaximumValueViewCount);
            hash.AddInt32(maxima.MaximumEvaluatorInstructionCount);
            hash.AddInt32(maxima.MaximumSetByCallerCount);
            hash.AddInt32(maxima.MaximumTargetDataCount);
            hash.AddInt32(maxima.MaximumEffectContextFieldCount);
        }

        /// <summary>
        /// 写入一个半开区间的起点与元素数量。
        /// </summary>
        private static void AddRange(
            ref GasBoundaryFnv1A64 hash,
            in GasCatalogRange range)
        {
            hash.AddInt32(range.Start);
            hash.AddInt32(range.Count);
        }
    }
}
