using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 Catalog 对非有限属性、Tag 闭包和 modifier 子 range 的 fail-closed 规则。
    /// </summary>
    [TestFixture]
    public class RuntimeV1CatalogValidatorHardeningTests
    {
        private const ulong SchemaHash = 0x5101UL;
        private const ulong ContentHash = 0x5202UL;
        private const ulong AttributeLayoutHash = 0x5303UL;
        private const ulong TagCatalogHash = 0x5404UL;

        /// <summary>
        /// 标识测试要污染的 AttributeLayout 浮点字段。
        /// </summary>
        public enum AttributeFloatField : byte
        {
            Default = 1,
            Minimum = 2,
            Maximum = 3,
        }

        /// <summary>
        /// 标识测试使用的三种非有限 IEEE 754 数值。
        /// </summary>
        public enum NonFiniteValue : byte
        {
            NaN = 1,
            PositiveInfinity = 2,
            NegativeInfinity = 3,
        }

        /// <summary>
        /// 验证 default/min/max 任一出现 NaN 或 Infinity 都拒绝安装。
        /// </summary>
        [TestCase(AttributeFloatField.Default, NonFiniteValue.NaN)]
        [TestCase(AttributeFloatField.Default, NonFiniteValue.PositiveInfinity)]
        [TestCase(AttributeFloatField.Default, NonFiniteValue.NegativeInfinity)]
        [TestCase(AttributeFloatField.Minimum, NonFiniteValue.NaN)]
        [TestCase(AttributeFloatField.Minimum, NonFiniteValue.PositiveInfinity)]
        [TestCase(AttributeFloatField.Minimum, NonFiniteValue.NegativeInfinity)]
        [TestCase(AttributeFloatField.Maximum, NonFiniteValue.NaN)]
        [TestCase(AttributeFloatField.Maximum, NonFiniteValue.PositiveInfinity)]
        [TestCase(AttributeFloatField.Maximum, NonFiniteValue.NegativeInfinity)]
        public void AttributeLayout_非有限数值全部拒绝(
            AttributeFloatField field,
            NonFiniteValue valueKind)
        {
            using var catalog = CatalogFactory.CreateAttributeCatalog(field, valueKind);
            ref var root = ref catalog.Value;

            var result = Validate(ref root);

            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.AttributeLayoutInvalid));
            Assert.That(result.ElementIndex, Is.Zero);
        }

        /// <summary>
        /// 验证每条 ancestor range 必须显式包含自身 dense index。
        /// </summary>
        [Test]
        public void TagCatalog_缺少自身时拒绝()
        {
            AssertTagCatalogInvalid(new[] { new int[0] }, 0);
        }

        /// <summary>
        /// 验证 ancestor range 不接受重复 dense index。
        /// </summary>
        [Test]
        public void TagCatalog_重复Ancestor时拒绝()
        {
            AssertTagCatalogInvalid(new[] { new[] { 0, 0 } }, 0);
        }

        /// <summary>
        /// 验证 ancestor range 必须按 dense index 严格递增。
        /// </summary>
        [Test]
        public void TagCatalog_Ancestor未排序时拒绝()
        {
            AssertTagCatalogInvalid(new[] { new[] { 0 }, new[] { 1, 0 } }, 1);
        }

        /// <summary>
        /// 验证 leaf range 必须包含其 ancestor 已声明的完整传递闭包。
        /// </summary>
        [Test]
        public void TagCatalog_传递闭包不完整时拒绝()
        {
            AssertTagCatalogInvalid(
                new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 1, 2 } },
                2);
        }

        /// <summary>
        /// 验证两个非自身 Tag 不能互相声明为 ancestor。
        /// </summary>
        [Test]
        public void TagCatalog_Ancestor成环时拒绝()
        {
            AssertTagCatalogInvalid(new[] { new[] { 0, 1 }, new[] { 0, 1 } }, 0);
        }

        /// <summary>
        /// 验证规范化的反身传递 ancestor 闭包可以安装。
        /// </summary>
        [Test]
        public void TagCatalog_规范化闭包通过()
        {
            using var catalog = CatalogFactory.CreateTagCatalog(
                new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 0, 1, 2 } });
            ref var root = ref catalog.Value;

            Assert.That(Validate(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 验证 modifier evaluator 子 range 不能引用所属 Definition 总 range 之外的指令。
        /// </summary>
        [Test]
        public void Modifier_Evaluator子Range越出Definition时拒绝()
        {
            using var catalog = CatalogFactory.CreateModifierCatalog(
                Range(0, 1),
                Range(1, 1),
                Range(0, 0),
                Range(0, 0),
                false);
            ref var root = ref catalog.Value;

            AssertModifierInvalid(ref root);
        }

        /// <summary>
        /// 验证 modifier capture 子 range 不能引用所属 Definition 总 range 之外的 descriptor。
        /// </summary>
        [Test]
        public void Modifier_Capture子Range越出Definition时拒绝()
        {
            using var catalog = CatalogFactory.CreateModifierCatalog(
                Range(0, 1),
                Range(0, 1),
                Range(0, 0),
                Range(0, 1),
                false);
            ref var root = ref catalog.Value;

            AssertModifierInvalid(ref root);
        }

        /// <summary>
        /// 验证 modifier 的 evaluator/capture 子 range 均被总 range 包含时可通过。
        /// </summary>
        [Test]
        public void Modifier_两个子Range均被Definition包含时通过()
        {
            using var catalog = CatalogFactory.CreateModifierCatalog(
                Range(0, 1),
                Range(0, 1),
                Range(0, 1),
                Range(0, 1),
                true);
            ref var root = ref catalog.Value;

            Assert.That(Validate(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 构造简洁的 Catalog range 测试值。
        /// </summary>
        private static GasCatalogRange Range(int start, int count)
        {
            return new GasCatalogRange { Start = start, Count = count };
        }

        /// <summary>
        /// 验证指定 ancestor 图以预期 Tag 位置 fail-closed。
        /// </summary>
        private static void AssertTagCatalogInvalid(int[][] ancestorRanges, int expectedIndex)
        {
            using var catalog = CatalogFactory.CreateTagCatalog(ancestorRanges);
            ref var root = ref catalog.Value;

            var result = Validate(ref root);

            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.TagCatalogInvalid));
            Assert.That(result.ElementIndex, Is.EqualTo(expectedIndex));
        }

        /// <summary>
        /// 验证 modifier 失败携带所属 Definition 与元素位置。
        /// </summary>
        private static void AssertModifierInvalid(ref GasDefinitionCatalogBlob catalog)
        {
            var result = Validate(ref catalog);

            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.ModifierInvalid));
            Assert.That(result.RangeKind, Is.EqualTo(GasCatalogRangeKind.Modifier));
            Assert.That(result.DefinitionId, Is.EqualTo(CatalogFactory.EffectId));
            Assert.That(result.ElementIndex, Is.Zero);
        }

        /// <summary>
        /// 使用与测试 Blob 完全一致的 header expectation 执行安装前校验。
        /// </summary>
        private static GasCatalogValidationResult Validate(ref GasDefinitionCatalogBlob catalog)
        {
            var expectation = new GasCatalogValidationExpectation(
                GasDefinitionCatalogSchema.Version,
                SchemaHash,
                ContentHash,
                AttributeLayoutHash,
                TagCatalogHash);
            return GasDefinitionCatalogValidator.Validate(ref catalog, in expectation);
        }

        /// <summary>
        /// 构造只覆盖 Catalog validator 契约的最小 immutable 测试 Blob。
        /// </summary>
        private static class CatalogFactory
        {
            public const int EffectId = 8501;

            /// <summary>
            /// 构造只污染一个 AttributeLayout 浮点字段的 Catalog。
            /// </summary>
            public static BlobAssetReference<GasDefinitionCatalogBlob> CreateAttributeCatalog(
                AttributeFloatField field,
                NonFiniteValue valueKind)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                try
                {
                    ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                    PopulateHeader(ref root);
                    PopulateInvalidAttribute(ref builder, ref root, field, valueKind);
                    AllocateEmptyTagCatalog(ref builder, ref root);
                    AllocateEmptyRuntimeArrays(ref builder, ref root);
                    return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                }
                finally
                {
                    builder.Dispose();
                }
            }

            /// <summary>
            /// 构造指定反身 ancestor 图且不包含 Definition 的 Catalog。
            /// </summary>
            public static BlobAssetReference<GasDefinitionCatalogBlob> CreateTagCatalog(
                int[][] ancestorRanges)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                try
                {
                    ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                    PopulateHeader(ref root);
                    builder.Allocate(ref root.AttributeLayout.Entries, 0);
                    PopulateTagCatalog(ref builder, ref root, ancestorRanges);
                    AllocateEmptyRuntimeArrays(ref builder, ref root);
                    return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                }
                finally
                {
                    builder.Dispose();
                }
            }

            /// <summary>
            /// 构造一个带单 modifier 的合法或定向越界 GameplayEffect Catalog。
            /// </summary>
            public static BlobAssetReference<GasDefinitionCatalogBlob> CreateModifierCatalog(
                GasCatalogRange definitionEvaluatorRange,
                GasCatalogRange modifierEvaluatorRange,
                GasCatalogRange definitionCaptureRange,
                GasCatalogRange modifierCaptureRange,
                bool useCaptureInstruction)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                try
                {
                    ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                    PopulateHeader(ref root);
                    PopulateValidAttribute(ref builder, ref root);
                    AllocateEmptyTagCatalog(ref builder, ref root);
                    PopulateModifierPayloads(ref builder, ref root, modifierEvaluatorRange,
                        modifierCaptureRange, useCaptureInstruction);
                    PopulateEffect(ref builder, ref root, definitionEvaluatorRange, definitionCaptureRange);
                    AllocateEmptyNonEffectArrays(ref builder, ref root);
                    return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                }
                finally
                {
                    builder.Dispose();
                }
            }

            /// <summary>
            /// 写入测试 Catalog 与 expectation 共用的稳定 header。
            /// </summary>
            private static void PopulateHeader(ref GasDefinitionCatalogBlob root)
            {
                root.SchemaVersion = GasDefinitionCatalogSchema.Version;
                root.SchemaHash = SchemaHash;
                root.ContentHash = ContentHash;
                root.AttributeLayout.LayoutHash = AttributeLayoutHash;
                root.TagCatalog.CatalogHash = TagCatalogHash;
            }

            /// <summary>
            /// 写入一个仅目标字段为非有限值的 AttributeLayout。
            /// </summary>
            private static void PopulateInvalidAttribute(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root,
                AttributeFloatField field,
                NonFiniteValue valueKind)
            {
                var entry = ValidAttribute();
                var value = ResolveNonFinite(valueKind);
                if (field == AttributeFloatField.Default)
                    entry.DefaultValue = value;
                else if (field == AttributeFloatField.Minimum)
                    entry.MinimumValue = value;
                else
                    entry.MaximumValue = value;

                var entries = builder.Allocate(ref root.AttributeLayout.Entries, 1);
                entries[0] = entry;
            }

            /// <summary>
            /// 写入单个可被 capture 引用的合法 AttributeLayout entry。
            /// </summary>
            private static void PopulateValidAttribute(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                var entries = builder.Allocate(ref root.AttributeLayout.Entries, 1);
                entries[0] = ValidAttribute();
            }

            /// <summary>
            /// 创建有限且 clamp 合法的 AttributeLayout entry。
            /// </summary>
            private static GasAttributeLayoutEntryBlob ValidAttribute()
            {
                return new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 1,
                    LayoutIndex = 0,
                    DefaultValue = 10f,
                    MinimumValue = 0f,
                    MaximumValue = 100f,
                    ClampMinimum = 1,
                    ClampMaximum = 1,
                };
            }

            /// <summary>
            /// 将测试枚举转换为对应非有限浮点值。
            /// </summary>
            private static float ResolveNonFinite(NonFiniteValue valueKind)
            {
                if (valueKind == NonFiniteValue.NaN)
                    return float.NaN;
                return valueKind == NonFiniteValue.PositiveInfinity
                    ? float.PositiveInfinity
                    : float.NegativeInfinity;
            }

            /// <summary>
            /// 写入指定 flattened ancestor ranges 与稳定 dense Tag entries。
            /// </summary>
            private static void PopulateTagCatalog(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root,
                int[][] ancestorRanges)
            {
                var totalCount = 0;
                for (var index = 0; index < ancestorRanges.Length; index++)
                    totalCount += ancestorRanges[index].Length;

                var ancestors = builder.Allocate(ref root.TagCatalog.AncestorIndices, totalCount);
                var entries = builder.Allocate(ref root.TagCatalog.Entries, ancestorRanges.Length);
                var start = 0;
                for (var index = 0; index < ancestorRanges.Length; index++)
                {
                    var range = ancestorRanges[index];
                    for (var offset = 0; offset < range.Length; offset++)
                        ancestors[start + offset] = range[offset];
                    entries[index] = TagEntry(index, start, range.Length);
                    start += range.Length;
                }
            }

            /// <summary>
            /// 创建一个按 stable ID 与 dense index 对齐的 Tag entry。
            /// </summary>
            private static GasTagCatalogEntryBlob TagEntry(int index, int start, int count)
            {
                return new GasTagCatalogEntryBlob
                {
                    TagId = 100 + index,
                    TagIndex = index,
                    AncestorIndexRange = Range(start, count),
                };
            }

            /// <summary>
            /// 分配合法的空 TagCatalog。
            /// </summary>
            private static void AllocateEmptyTagCatalog(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                builder.Allocate(ref root.TagCatalog.Entries, 0);
                builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
            }

            /// <summary>
            /// 写入单 capture、单 modifier 与两条 evaluator 指令根数组。
            /// </summary>
            private static void PopulateModifierPayloads(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root,
                GasCatalogRange modifierEvaluatorRange,
                GasCatalogRange modifierCaptureRange,
                bool useCaptureInstruction)
            {
                var captures = builder.Allocate(ref root.CaptureDescriptors, 1);
                captures[0] = ValidCapture();
                var instructions = builder.Allocate(ref root.EvaluatorInstructions, 2);
                instructions[0] = Instruction(useCaptureInstruction);
                instructions[1] = Instruction(false);
                var modifiers = builder.Allocate(ref root.Modifiers, 1);
                modifiers[0] = new GasModifierDefinitionBlob
                {
                    AttributeLayoutIndex = 0,
                    Operation = GasModifierOperation.Add,
                    EvaluatorProgramRange = modifierEvaluatorRange,
                    CaptureRange = modifierCaptureRange,
                };
            }

            /// <summary>
            /// 创建可用于 Definition 和 modifier 的合法 Source Snapshot capture。
            /// </summary>
            private static GasCaptureDescriptorBlob ValidCapture()
            {
                return new GasCaptureDescriptorBlob
                {
                    CaptureOrdinal = 0,
                    Owner = GasCaptureOwner.Source,
                    Binding = GasCaptureBinding.Snapshot,
                    Phase = GasCapturePhase.SourceSpecProjection,
                    LiveScope = GasLiveCaptureScope.None,
                    GonePolicy = GasCaptureGonePolicy.RejectApplication,
                    ValueView = GasAttributeValueView.Base,
                    AttributeLayoutIndex = 0,
                    ConsumerNodeOrdinal = 0,
                    ConsumerFieldOrdinal = 0,
                };
            }

            /// <summary>
            /// 创建一条产出单值的 capture 或 constant evaluator 指令。
            /// </summary>
            private static GasEvaluatorInstructionBlob Instruction(bool useCapture)
            {
                return new GasEvaluatorInstructionBlob
                {
                    Opcode = useCapture ? GasEvaluatorOpcode.PushCapture : GasEvaluatorOpcode.PushConstant,
                    OperandIndex = 0,
                    ConstantValue = 1f,
                };
            }

            /// <summary>
            /// 写入单个 Instant GameplayEffect Definition 与排序索引。
            /// </summary>
            private static void PopulateEffect(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root,
                GasCatalogRange evaluatorRange,
                GasCatalogRange captureRange)
            {
                var indices = builder.Allocate(ref root.GameplayEffectIndex, 1);
                indices[0] = new GasDefinitionIndexEntry { DefinitionId = EffectId, DefinitionIndex = 0 };
                var effects = builder.Allocate(ref root.GameplayEffects, 1);
                effects[0] = CreateEffect(evaluatorRange, captureRange);
            }

            /// <summary>
            /// 创建策略完整且只包含一个 modifier 的 Instant GameplayEffect。
            /// </summary>
            private static GasGameplayEffectDefinitionBlob CreateEffect(
                GasCatalogRange evaluatorRange,
                GasCatalogRange captureRange)
            {
                return new GasGameplayEffectDefinitionBlob
                {
                    DefinitionId = EffectId,
                    Lifetime = GasEffectLifetimePolicy.Instant,
                    TargetPolicy = SelfTarget(),
                    CaptureRange = captureRange,
                    ModifierRange = Range(0, 1),
                    EvaluatorProgramRange = evaluatorRange,
                    StackPolicy = GasStackPolicy.None,
                    StackPayloadPolicy = GasStackPayloadPolicy.None,
                    StackLimitApplicationPolicy = GasStackLimitApplicationPolicy.None,
                    DurationRefreshPolicy = GasDurationRefreshPolicy.Never,
                    PeriodResetPolicy = GasPeriodResetPolicy.Never,
                    ExpiryPolicy = GasExpiryPolicy.Remove,
                    ExpiryPeriodPolicy = GasExpiryPeriodPolicy.Stop,
                    ExpirySameTickPolicy = GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                    InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                    InhibitedPeriodPolicy = GasInhibitedPeriodPolicy.None,
                    MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
                    Maxima = Maxima(captureRange.Count, evaluatorRange.Count),
                };
            }

            /// <summary>
            /// 创建不需要 TargetData 的显式 Self target policy。
            /// </summary>
            private static GasTargetPolicyBlob SelfTarget()
            {
                return new GasTargetPolicyBlob
                {
                    LogicalTarget = GasLogicalTargetPolicy.Self,
                    Avatar = GasAvatarTargetPolicy.FollowAsc,
                    Spatial = GasSpatialTargetPolicy.None,
                    Life = GasTargetLifePolicy.AliveOnly,
                };
            }

            /// <summary>
            /// 创建与测试 Definition 实际 payload 数量一致的静态 maxima。
            /// </summary>
            private static GasDefinitionMaxima Maxima(int captureCount, int evaluatorCount)
            {
                return new GasDefinitionMaxima
                {
                    MaximumCaptureDescriptorCount = captureCount,
                    MaximumModifierCount = 1,
                    MaximumEvaluatorInstructionCount = evaluatorCount,
                };
            }

            /// <summary>
            /// 为无 Definition 测试分配全部空 Runtime 根数组。
            /// </summary>
            private static void AllocateEmptyRuntimeArrays(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                AllocateEmptyNonEffectArrays(ref builder, ref root);
                builder.Allocate(ref root.GameplayEffectIndex, 0);
                builder.Allocate(ref root.GameplayEffects, 0);
                builder.Allocate(ref root.CaptureDescriptors, 0);
                builder.Allocate(ref root.Modifiers, 0);
                builder.Allocate(ref root.EvaluatorInstructions, 0);
            }

            /// <summary>
            /// 分配 modifier 测试未使用的全部根数组。
            /// </summary>
            private static void AllocateEmptyNonEffectArrays(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                builder.Allocate(ref root.AbilityIndex, 0);
                builder.Allocate(ref root.Abilities, 0);
                builder.Allocate(ref root.Requirements, 0);
                builder.Allocate(ref root.RequirementTagIndices, 0);
                builder.Allocate(ref root.DirectEffectProgramNodes, 0);
                builder.Allocate(ref root.CueReferences, 0);
                builder.Allocate(ref root.ValueViews, 0);
                builder.Allocate(ref root.SetByCallerDescriptors, 0);
                builder.Allocate(ref root.TargetDataDescriptors, 0);
                builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
            }
        }
    }
}
