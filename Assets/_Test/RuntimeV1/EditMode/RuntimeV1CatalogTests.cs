using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 immutable Catalog 的 phase、capture、lookup 与 fail-closed 契约。
    /// </summary>
    [TestFixture]
    public class RuntimeV1CatalogTests
    {
        private const int ContractEffectId = 9000;
        private const int PoisonEffectId = 9203;
        private const int LegacyPoisonPeriodEffectId = 9204;
        private const int FinisherEffectId = 9207;
        private const ulong SchemaHash = 0x1101UL;
        private const ulong ContentHash = 0x2202UL;
        private const ulong AttributeLayoutHash = 0x3303UL;
        private const ulong TagCatalogHash = 0x4404UL;

        /// <summary>
        /// 验证四种 requirement phase 通过独立 range 往返且没有压平成共享字段。
        /// </summary>
        [Test]
        public void RequirementPhase_四个独立Range可完整往返()
        {
            using var catalog = RuntimeV1CatalogTestBlobFactory.Create();
            ref var root = ref catalog.Value;
            var effect = GetEffect(ref root, PoisonEffectId);

            AssertRequirementPhase(ref root, effect.ApplicationRequirementRange, GasRequirementPhase.Application);
            AssertRequirementPhase(ref root, effect.OngoingRequirementRange, GasRequirementPhase.Ongoing);
            AssertRequirementPhase(ref root, effect.RemovalRequirementRange, GasRequirementPhase.Removal);
            AssertRequirementPhase(ref root, effect.ImmunityRequirementRange, GasRequirementPhase.Immunity);
            Assert.That(effect.ApplicationRequirementRange.Start, Is.Not.EqualTo(effect.OngoingRequirementRange.Start));
            Assert.That(Validate(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 验证 Source/Target × Snapshot/Live 四组合及其合法 phase/scope 均可进入 Blob。
        /// </summary>
        [Test]
        public void CaptureDescriptor_四组合可生成并解析()
        {
            using var catalog = RuntimeV1CatalogTestBlobFactory.Create();
            ref var root = ref catalog.Value;
            var effect = GetEffect(ref root, ContractEffectId);

            AssertCapture(ref root, effect.CaptureRange, 0, GasCaptureOwner.Source,
                GasCaptureBinding.Snapshot, GasCapturePhase.SourceSpecProjection, GasLiveCaptureScope.None);
            AssertCapture(ref root, effect.CaptureRange, 1, GasCaptureOwner.Target,
                GasCaptureBinding.Snapshot, GasCapturePhase.TargetApplication, GasLiveCaptureScope.None);
            AssertCapture(ref root, effect.CaptureRange, 2, GasCaptureOwner.Source,
                GasCaptureBinding.Live, GasCapturePhase.CrossAscMaintenance, GasLiveCaptureScope.CrossAsc);
            AssertCapture(ref root, effect.CaptureRange, 3, GasCaptureOwner.Target,
                GasCaptureBinding.Live, GasCapturePhase.TargetStabilization, GasLiveCaptureScope.SameAsc);
            Assert.That(Validate(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 验证 Ability/GameplayEffect 使用排序索引二分查找且目标 Catalog 不含 legacy 9204。
        /// </summary>
        [Test]
        public void SortedLookup_稳定查找且Legacy9204缺席()
        {
            using var catalog = RuntimeV1CatalogTestBlobFactory.Create();
            ref var root = ref catalog.Value;

            Assert.That(GasDefinitionCatalogLookup.TryGetAbilityIndex(ref root, 9104, out var abilityIndex), Is.True);
            Assert.That(root.Abilities[abilityIndex].DefinitionId, Is.EqualTo(9104));
            Assert.That(GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref root, FinisherEffectId, out var effectIndex), Is.True);
            Assert.That(root.GameplayEffects[effectIndex].DefinitionId, Is.EqualTo(FinisherEffectId));
            Assert.That(GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                ref root,
                LegacyPoisonPeriodEffectId,
                out _), Is.False);
        }

        /// <summary>
        /// 验证 schema/content/layout/tag 任一安装期 header 不匹配都以 typed error 失败。
        /// </summary>
        [TestCase(CatalogHeaderMismatch.SchemaVersion, GasCatalogValidationError.SchemaVersionMismatch)]
        [TestCase(CatalogHeaderMismatch.SchemaHash, GasCatalogValidationError.SchemaHashMismatch)]
        [TestCase(CatalogHeaderMismatch.ContentHash, GasCatalogValidationError.ContentHashMismatch)]
        [TestCase(CatalogHeaderMismatch.AttributeLayoutHash, GasCatalogValidationError.AttributeLayoutHashMismatch)]
        [TestCase(CatalogHeaderMismatch.TagCatalogHash, GasCatalogValidationError.TagCatalogHashMismatch)]
        public void CatalogHeaderMismatch_显式失败且不Fallback(
            CatalogHeaderMismatch mismatch,
            GasCatalogValidationError expectedError)
        {
            using var catalog = RuntimeV1CatalogTestBlobFactory.Create();
            ref var root = ref catalog.Value;
            var expectation = CreateExpectation(mismatch);

            var result = GasDefinitionCatalogValidator.Validate(ref root, in expectation);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(expectedError));
        }

        /// <summary>
        /// 验证越界 range 携带 Definition 和字段类型 fail-closed。
        /// </summary>
        [Test]
        public void InvalidRange_返回机器可读失败定位()
        {
            using var catalog = RuntimeV1CatalogTestBlobFactory.Create(withInvalidPoisonCaptureRange: true);
            ref var root = ref catalog.Value;

            var result = Validate(ref root);

            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.RangeOutOfBounds));
            Assert.That(result.RangeKind, Is.EqualTo(GasCatalogRangeKind.Capture));
            Assert.That(result.DefinitionId, Is.EqualTo(PoisonEffectId));
        }

        /// <summary>
        /// 验证 9203 的 stack、capture、period、expiry、inhibit 与 evaluator 全部来自单一 Blob row。
        /// </summary>
        [Test]
        public void Poison9203_全部冻结关键字段进入CompiledBlob()
        {
            using var catalog = RuntimeV1CatalogTestBlobFactory.Create();
            ref var root = ref catalog.Value;
            var effect = GetEffect(ref root, PoisonEffectId);

            AssertPoisonCorePolicies(in effect);
            AssertPoisonTimingPolicies(in effect);
            AssertPoisonCaptureAndProgram(ref root, in effect);
            Assert.That(effect.TargetPolicy.LogicalTarget, Is.EqualTo(GasLogicalTargetPolicy.FrozenAsc));
            Assert.That(effect.TargetPolicy.Life, Is.EqualTo(GasTargetLifePolicy.AliveOnly));
            Assert.That(effect.ModifierRange.Count, Is.Zero, "9203 不得注入 dummy modifier");
            Assert.That(effect.Maxima.MaximumEvaluatorInstructionCount, Is.EqualTo(5));
        }

        /// <summary>
        /// 验证 9207 是 InstantExecution，并显式绑定 Health Base/Current/DefinitionMaxValue。
        /// </summary>
        [Test]
        public void Finisher9207_使用LayoutResolvedValueViews且无ActiveMarker()
        {
            using var catalog = RuntimeV1CatalogTestBlobFactory.Create();
            ref var root = ref catalog.Value;
            var effect = GetEffect(ref root, FinisherEffectId);

            Assert.That(effect.Lifetime, Is.EqualTo(GasEffectLifetimePolicy.InstantExecution));
            Assert.That(effect.DurationTicks, Is.Zero);
            Assert.That(effect.PeriodTicks, Is.Zero);
            Assert.That(effect.RequiredValueViews, Is.EqualTo(
                GasAttributeValueViewMask.Base
                | GasAttributeValueViewMask.Current
                | GasAttributeValueViewMask.DefinitionMaxValue));
            AssertValueView(ref root, effect.ValueViewRange, 0, GasAttributeValueView.Base);
            AssertValueView(ref root, effect.ValueViewRange, 1, GasAttributeValueView.Current);
            AssertValueView(ref root, effect.ValueViewRange, 2, GasAttributeValueView.DefinitionMaxValue);
            Assert.That(root.AttributeLayout.Entries[0].MaximumValue, Is.EqualTo(100f));
            Assert.That(root.EvaluatorInstructions[effect.EvaluatorProgramRange.Start].Opcode,
                Is.EqualTo(GasEvaluatorOpcode.PushValueView));
            Assert.That(Validate(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 验证 Definition→Spec 的 SetByCaller、TargetData 与 EffectContext ranges 均可往返。
        /// </summary>
        [Test]
        public void DefinitionToSpecContract_三个DescriptorRange均进入Blob()
        {
            using var catalog = RuntimeV1CatalogTestBlobFactory.Create();
            ref var root = ref catalog.Value;
            var contract = GetEffect(ref root, ContractEffectId);

            var setByCaller = root.SetByCallerDescriptors[contract.SetByCallerRange.Start];
            var targetData = root.TargetDataDescriptors[contract.TargetDataRange.Start];
            var context = root.EffectContextFieldDescriptors[contract.EffectContextFieldRange.Start];
            Assert.That(setByCaller.KeyId, Is.EqualTo(7001));
            Assert.That(setByCaller.Required, Is.EqualTo(1));
            Assert.That(targetData.Variant, Is.EqualTo(GasTargetDataVariant.StableAsc));
            Assert.That(context.Field, Is.EqualTo(GasEffectContextFieldKind.CausalityId));
            Assert.That(Validate(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 读取指定 GameplayEffect Definition，并让缺失直接表现为测试失败。
        /// </summary>
        private static GasGameplayEffectDefinitionBlob GetEffect(
            ref GasDefinitionCatalogBlob catalog,
            int definitionId)
        {
            var found = GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                ref catalog,
                definitionId,
                out var definitionIndex);
            Assert.That(found, Is.True, "V1 Catalog 缺少 GE " + definitionId);
            return GasDefinitionCatalogLookup.GetGameplayEffect(ref catalog, definitionIndex);
        }

        /// <summary>
        /// 验证 requirement range 只指向其声明 phase。
        /// </summary>
        private static void AssertRequirementPhase(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            GasRequirementPhase phase)
        {
            Assert.That(range.Count, Is.EqualTo(1));
            Assert.That(catalog.Requirements[range.Start].Phase, Is.EqualTo(phase));
        }

        /// <summary>
        /// 验证 capture descriptor 的 owner、binding、phase 与 Live scope。
        /// </summary>
        private static void AssertCapture(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            int ordinal,
            GasCaptureOwner owner,
            GasCaptureBinding binding,
            GasCapturePhase phase,
            GasLiveCaptureScope liveScope)
        {
            var capture = catalog.CaptureDescriptors[range.Start + ordinal];
            Assert.That(capture.CaptureOrdinal, Is.EqualTo(ordinal));
            Assert.That(capture.Owner, Is.EqualTo(owner));
            Assert.That(capture.Binding, Is.EqualTo(binding));
            Assert.That(capture.Phase, Is.EqualTo(phase));
            Assert.That(capture.LiveScope, Is.EqualTo(liveScope));
        }

        /// <summary>
        /// 验证 9203 stack 与核心 application policy。
        /// </summary>
        private static void AssertPoisonCorePolicies(in GasGameplayEffectDefinitionBlob effect)
        {
            const GasStackKeyFields expectedKey = GasStackKeyFields.Definition
                | GasStackKeyFields.TargetAsc
                | GasStackKeyFields.SourceAsc;
            Assert.That(effect.Lifetime, Is.EqualTo(GasEffectLifetimePolicy.Duration));
            Assert.That(effect.StackKey, Is.EqualTo(expectedKey));
            Assert.That(effect.StackPolicy, Is.EqualTo(GasStackPolicy.AggregateBySource));
            Assert.That(effect.StackPayloadPolicy, Is.EqualTo(GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance));
            Assert.That(effect.StackLimitApplicationPolicy, Is.EqualTo(GasStackLimitApplicationPolicy.AcceptAndKeepLimit));
            Assert.That(effect.StackLimit, Is.EqualTo(3));
            Assert.That(effect.ExecuteOnApplication, Is.Zero);
        }

        /// <summary>
        /// 验证 9203 duration/period/reapply/expiry/inhibit 的全部冻结策略。
        /// </summary>
        private static void AssertPoisonTimingPolicies(in GasGameplayEffectDefinitionBlob effect)
        {
            Assert.That(effect.DurationTicks, Is.EqualTo(8));
            Assert.That(effect.PeriodTicks, Is.EqualTo(2));
            Assert.That(effect.DurationRefreshPolicy, Is.EqualTo(GasDurationRefreshPolicy.Never));
            Assert.That(effect.PeriodResetPolicy, Is.EqualTo(GasPeriodResetPolicy.OnSuccessfulApplication));
            Assert.That(effect.ExpiryPolicy, Is.EqualTo(GasExpiryPolicy.RemoveOneStackAndRefreshDuration));
            Assert.That(effect.ExpiryPeriodPolicy, Is.EqualTo(GasExpiryPeriodPolicy.Reset));
            Assert.That(effect.ExpirySameTickPolicy, Is.EqualTo(GasExpirySameTickPolicy.PeriodDueBeforeExpiry));
            Assert.That(effect.InhibitTimePolicy, Is.EqualTo(GasInhibitTimePolicy.DurationContinues));
            Assert.That(effect.InhibitedPeriodPolicy, Is.EqualTo(GasInhibitedPeriodPolicy.SkipExecution));
            Assert.That(effect.MissedPeriodPolicy, Is.EqualTo(GasMissedPeriodPolicy.SkipNoCatchUp));
        }

        /// <summary>
        /// 验证 9203 Source Attack Snapshot 与通用 postfix period evaluator 指令。
        /// </summary>
        private static void AssertPoisonCaptureAndProgram(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob effect)
        {
            Assert.That(effect.CaptureRange.Count, Is.EqualTo(1));
            var capture = catalog.CaptureDescriptors[effect.CaptureRange.Start];
            Assert.That(capture.Owner, Is.EqualTo(GasCaptureOwner.Source));
            Assert.That(capture.Binding, Is.EqualTo(GasCaptureBinding.Snapshot));
            Assert.That(capture.Phase, Is.EqualTo(GasCapturePhase.SourceSpecProjection));
            Assert.That(capture.ValueView, Is.EqualTo(GasAttributeValueView.Current));
            Assert.That(effect.EvaluatorProgramRange.Count, Is.EqualTo(5));
            Assert.That(catalog.EvaluatorInstructions[effect.EvaluatorProgramRange.Start].Opcode,
                Is.EqualTo(GasEvaluatorOpcode.PushCapture));
            Assert.That(catalog.EvaluatorInstructions[effect.EvaluatorProgramRange.Start + 3].Opcode,
                Is.EqualTo(GasEvaluatorOpcode.PushStackCount));
        }

        /// <summary>
        /// 验证指定 ValueView descriptor 解析到 Health layout index 0。
        /// </summary>
        private static void AssertValueView(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            int ordinal,
            GasAttributeValueView expectedView)
        {
            var descriptor = catalog.ValueViews[range.Start + ordinal];
            Assert.That(descriptor.AttributeLayoutIndex, Is.Zero);
            Assert.That(descriptor.ValueView, Is.EqualTo(expectedView));
        }

        /// <summary>
        /// 使用当前冻结 header 验证一个 Catalog。
        /// </summary>
        private static GasCatalogValidationResult Validate(ref GasDefinitionCatalogBlob catalog)
        {
            var expectation = CreateExpectation(CatalogHeaderMismatch.None);
            return GasDefinitionCatalogValidator.Validate(ref catalog, in expectation);
        }

        /// <summary>
        /// 按测试维度构造唯一一处 header mismatch。
        /// </summary>
        private static GasCatalogValidationExpectation CreateExpectation(CatalogHeaderMismatch mismatch)
        {
            return new GasCatalogValidationExpectation(
                mismatch == CatalogHeaderMismatch.SchemaVersion ? GasDefinitionCatalogSchema.Version + 1 : GasDefinitionCatalogSchema.Version,
                mismatch == CatalogHeaderMismatch.SchemaHash ? SchemaHash + 1 : SchemaHash,
                mismatch == CatalogHeaderMismatch.ContentHash ? ContentHash + 1 : ContentHash,
                mismatch == CatalogHeaderMismatch.AttributeLayoutHash ? AttributeLayoutHash + 1 : AttributeLayoutHash,
                mismatch == CatalogHeaderMismatch.TagCatalogHash ? TagCatalogHash + 1 : TagCatalogHash);
        }

        /// <summary>
        /// 标识 header mismatch 参数化测试要破坏的唯一字段。
        /// </summary>
        public enum CatalogHeaderMismatch : byte
        {
            None = 0,
            SchemaVersion = 1,
            SchemaHash = 2,
            ContentHash = 3,
            AttributeLayoutHash = 4,
            TagCatalogHash = 5,
        }

        /// <summary>
        /// 构造测试专用 immutable Catalog Blob，不承担 Runtime Catalog 生命周期。
        /// </summary>
        private static class RuntimeV1CatalogTestBlobFactory
        {
            /// <summary>
            /// 构造完整合法 Catalog，或仅破坏 9203 capture range 以验证 fail-closed。
            /// </summary>
            public static BlobAssetReference<GasDefinitionCatalogBlob> Create(
                bool withInvalidPoisonCaptureRange = false)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                try
                {
                    ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                    PopulateHeader(ref root);
                    PopulateAttributeLayout(ref builder, ref root);
                    PopulateTagAndRequirements(ref builder, ref root);
                    PopulateAbilities(ref builder, ref root);
                    PopulateCaptures(ref builder, ref root);
                    PopulateEvaluatorPrograms(ref builder, ref root);
                    PopulateSpecContracts(ref builder, ref root);
                    PopulateCuesAndValueViews(ref builder, ref root);
                    PopulateEffects(ref builder, ref root, withInvalidPoisonCaptureRange);
                    builder.Allocate(ref root.Modifiers, 0);
                    return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                }
                finally
                {
                    builder.Dispose();
                }
            }

            /// <summary>
            /// 写入测试 Catalog 的冻结 schema/content header。
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
            /// 写入排序的 Health/Attack dense AttributeLayout。
            /// </summary>
            private static void PopulateAttributeLayout(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                var entries = builder.Allocate(ref root.AttributeLayout.Entries, 2);
                entries[0] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 1,
                    LayoutIndex = 0,
                    DefaultValue = 100f,
                    MinimumValue = 0f,
                    MaximumValue = 100f,
                    ClampMinimum = 1,
                    ClampMaximum = 1,
                };
                entries[1] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 2,
                    LayoutIndex = 1,
                    DefaultValue = 10f,
                    MinimumValue = 0f,
                    MaximumValue = 999f,
                    ClampMinimum = 1,
                    ClampMaximum = 0,
                };
            }

            /// <summary>
            /// 写入一个 dense Tag 以及四个独立 phase requirement。
            /// </summary>
            private static void PopulateTagAndRequirements(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                var ancestors = builder.Allocate(ref root.TagCatalog.AncestorIndices, 1);
                ancestors[0] = 0;
                var tags = builder.Allocate(ref root.TagCatalog.Entries, 1);
                tags[0] = new GasTagCatalogEntryBlob
                {
                    TagId = 4,
                    TagIndex = 0,
                    AncestorIndexRange = Range(0, 1),
                };

                var requirementTags = builder.Allocate(ref root.RequirementTagIndices, 4);
                var requirements = builder.Allocate(ref root.Requirements, 4);
                for (var index = 0; index < 4; index++)
                {
                    requirementTags[index] = 0;
                    requirements[index] = new GasRequirementBlob
                    {
                        RequirementId = 7100 + index,
                        Phase = (GasRequirementPhase)(index + 1),
                        Match = GasTagRequirementMatch.Any,
                        TagIndexRange = Range(index, 1),
                    };
                }
            }

            /// <summary>
            /// 写入排序 Ability index 与两个单节点 DirectEffectProgram。
            /// </summary>
            private static void PopulateAbilities(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                var index = builder.Allocate(ref root.AbilityIndex, 2);
                index[0] = DefinitionIndex(9103, 0);
                index[1] = DefinitionIndex(9104, 1);
                var nodes = builder.Allocate(ref root.DirectEffectProgramNodes, 2);
                nodes[0] = DirectNode(FinisherEffectId);
                nodes[1] = DirectNode(PoisonEffectId);
                var definitions = builder.Allocate(ref root.Abilities, 2);
                definitions[0] = Ability(9103, Range(0, 1));
                definitions[1] = Ability(9104, Range(1, 1));
            }

            /// <summary>
            /// 写入四组合 contract capture 与 9203 Source Attack Snapshot。
            /// </summary>
            private static void PopulateCaptures(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                var captures = builder.Allocate(ref root.CaptureDescriptors, 5);
                captures[0] = Capture(0, GasCaptureOwner.Source, GasCaptureBinding.Snapshot,
                    GasCapturePhase.SourceSpecProjection, GasLiveCaptureScope.None, 1);
                captures[1] = Capture(1, GasCaptureOwner.Target, GasCaptureBinding.Snapshot,
                    GasCapturePhase.TargetApplication, GasLiveCaptureScope.None, 0);
                captures[2] = Capture(2, GasCaptureOwner.Source, GasCaptureBinding.Live,
                    GasCapturePhase.CrossAscMaintenance, GasLiveCaptureScope.CrossAsc, 1);
                captures[3] = Capture(3, GasCaptureOwner.Target, GasCaptureBinding.Live,
                    GasCapturePhase.TargetStabilization, GasLiveCaptureScope.SameAsc, 0);
                captures[4] = Capture(0, GasCaptureOwner.Source, GasCaptureBinding.Snapshot,
                    GasCapturePhase.SourceSpecProjection, GasLiveCaptureScope.None, 1);
            }

            /// <summary>
            /// 写入 9203 与 9207 的通用 postfix evaluator instruction programs。
            /// </summary>
            private static void PopulateEvaluatorPrograms(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                var instructions = builder.Allocate(ref root.EvaluatorInstructions, 17);
                instructions[0] = Instruction(GasEvaluatorOpcode.PushCapture, operandIndex: 0);
                instructions[1] = Instruction(GasEvaluatorOpcode.PushConstant, constantValue: 0.3f);
                instructions[2] = Instruction(GasEvaluatorOpcode.Multiply);
                instructions[3] = Instruction(GasEvaluatorOpcode.PushStackCount);
                instructions[4] = Instruction(GasEvaluatorOpcode.Multiply);
                instructions[5] = Instruction(GasEvaluatorOpcode.PushValueView, operandIndex: 2);
                instructions[6] = Instruction(GasEvaluatorOpcode.PushValueView, operandIndex: 0);
                instructions[7] = Instruction(GasEvaluatorOpcode.Subtract);
                instructions[8] = Instruction(GasEvaluatorOpcode.PushConstant, constantValue: 0f);
                instructions[9] = Instruction(GasEvaluatorOpcode.Maximum);
                instructions[10] = Instruction(GasEvaluatorOpcode.PushConstant, constantValue: 0.5f);
                instructions[11] = Instruction(GasEvaluatorOpcode.Multiply);
                instructions[12] = Instruction(GasEvaluatorOpcode.PushConstant, constantValue: 16f);
                instructions[13] = Instruction(GasEvaluatorOpcode.Add);
                instructions[14] = Instruction(GasEvaluatorOpcode.PushConstant, constantValue: 12f);
                instructions[15] = Instruction(GasEvaluatorOpcode.PushConstant, constantValue: 42f);
                instructions[16] = Instruction(GasEvaluatorOpcode.Clamp);
            }

            /// <summary>
            /// 写入 SetByCaller、每 Effect StableAsc TargetData 与 EffectContext 字段契约。
            /// </summary>
            private static void PopulateSpecContracts(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                var setByCaller = builder.Allocate(ref root.SetByCallerDescriptors, 1);
                setByCaller[0] = new GasSetByCallerDescriptorBlob
                {
                    KeyId = 7001,
                    FieldOrdinal = 0,
                    Required = 1,
                };
                var targetData = builder.Allocate(ref root.TargetDataDescriptors, 3);
                var context = builder.Allocate(ref root.EffectContextFieldDescriptors, 3);
                for (var index = 0; index < 3; index++)
                {
                    targetData[index] = new GasTargetDataDescriptorBlob
                    {
                        Variant = GasTargetDataVariant.StableAsc,
                        FieldOrdinal = 0,
                        Required = 1,
                    };
                    context[index] = new GasEffectContextFieldDescriptorBlob
                    {
                        Field = GasEffectContextFieldKind.CausalityId,
                        FieldOrdinal = 0,
                        Required = 1,
                    };
                }
            }

            /// <summary>
            /// 写入 9203/9207 Cue contract 与 9207 layout-resolved ValueViews。
            /// </summary>
            private static void PopulateCuesAndValueViews(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root)
            {
                var cues = builder.Allocate(ref root.CueReferences, 2);
                cues[0] = new GasCueReferenceBlob
                {
                    CueDefinitionId = 9301,
                    CueDefinitionOrdinal = 0,
                    Phases = GasCuePhaseFlags.OnActive | GasCuePhaseFlags.WhileActive | GasCuePhaseFlags.Removed,
                };
                cues[1] = new GasCueReferenceBlob
                {
                    CueDefinitionId = 9301,
                    CueDefinitionOrdinal = 0,
                    Phases = GasCuePhaseFlags.Executed,
                };
                var views = builder.Allocate(ref root.ValueViews, 3);
                views[0] = ValueView(GasAttributeValueView.Base);
                views[1] = ValueView(GasAttributeValueView.Current);
                views[2] = ValueView(GasAttributeValueView.DefinitionMaxValue);
            }

            /// <summary>
            /// 写入排序 Effect index 与 contract、9203、9207 三个 Definition。
            /// </summary>
            private static void PopulateEffects(
                ref BlobBuilder builder,
                ref GasDefinitionCatalogBlob root,
                bool withInvalidPoisonCaptureRange)
            {
                var index = builder.Allocate(ref root.GameplayEffectIndex, 3);
                index[0] = DefinitionIndex(ContractEffectId, 0);
                index[1] = DefinitionIndex(PoisonEffectId, 1);
                index[2] = DefinitionIndex(FinisherEffectId, 2);
                var effects = builder.Allocate(ref root.GameplayEffects, 3);
                effects[0] = ContractEffect();
                effects[1] = PoisonEffect(withInvalidPoisonCaptureRange);
                effects[2] = FinisherEffect();
            }

            /// <summary>
            /// 构造四组合 capture/schema contract 测试 Definition。
            /// </summary>
            private static GasGameplayEffectDefinitionBlob ContractEffect()
            {
                var effect = BaseEffect(ContractEffectId, GasEffectLifetimePolicy.Duration, durationTicks: 1);
                effect.CaptureRange = Range(0, 4);
                effect.SetByCallerRange = Range(0, 1);
                effect.TargetDataRange = Range(0, 1);
                effect.EffectContextFieldRange = Range(0, 1);
                effect.Maxima = Maxima(captures: 4, setByCaller: 1, targetData: 1, context: 1);
                return effect;
            }

            /// <summary>
            /// 构造唯一目标态 9203 poison Definition，不引用 legacy 9204。
            /// </summary>
            private static GasGameplayEffectDefinitionBlob PoisonEffect(bool withInvalidCaptureRange)
            {
                var effect = BaseEffect(PoisonEffectId, GasEffectLifetimePolicy.Duration, durationTicks: 8);
                effect.ApplicationRequirementRange = Range(0, 1);
                effect.OngoingRequirementRange = Range(1, 1);
                effect.RemovalRequirementRange = Range(2, 1);
                effect.ImmunityRequirementRange = Range(3, 1);
                effect.CaptureRange = withInvalidCaptureRange ? Range(4, 2) : Range(4, 1);
                effect.CueRange = Range(0, 1);
                effect.EvaluatorProgramRange = Range(0, 5);
                effect.TargetDataRange = Range(1, 1);
                effect.EffectContextFieldRange = Range(1, 1);
                effect.PeriodTicks = 2;
                effect.StackLimit = 3;
                effect.StackKey = GasStackKeyFields.Definition | GasStackKeyFields.TargetAsc | GasStackKeyFields.SourceAsc;
                effect.StackPolicy = GasStackPolicy.AggregateBySource;
                effect.StackPayloadPolicy = GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance;
                effect.StackLimitApplicationPolicy = GasStackLimitApplicationPolicy.AcceptAndKeepLimit;
                effect.PeriodResetPolicy = GasPeriodResetPolicy.OnSuccessfulApplication;
                effect.ExpiryPolicy = GasExpiryPolicy.RemoveOneStackAndRefreshDuration;
                effect.ExpiryPeriodPolicy = GasExpiryPeriodPolicy.Reset;
                effect.ExpirySameTickPolicy = GasExpirySameTickPolicy.PeriodDueBeforeExpiry;
                effect.InhibitedPeriodPolicy = GasInhibitedPeriodPolicy.SkipExecution;
                effect.Maxima = Maxima(requirements: 4, captures: 1, cues: 1,
                    evaluatorInstructions: 5, targetData: 1, context: 1);
                return effect;
            }

            /// <summary>
            /// 构造 9207 InstantExecution Definition 与完整 ValueView/program contract。
            /// </summary>
            private static GasGameplayEffectDefinitionBlob FinisherEffect()
            {
                var effect = BaseEffect(FinisherEffectId, GasEffectLifetimePolicy.InstantExecution, durationTicks: 0);
                effect.CueRange = Range(1, 1);
                effect.ValueViewRange = Range(0, 3);
                effect.EvaluatorProgramRange = Range(5, 12);
                effect.TargetDataRange = Range(2, 1);
                effect.EffectContextFieldRange = Range(2, 1);
                effect.RequiredValueViews = GasAttributeValueViewMask.Base
                    | GasAttributeValueViewMask.Current
                    | GasAttributeValueViewMask.DefinitionMaxValue;
                effect.Maxima = Maxima(cues: 1, valueViews: 3,
                    evaluatorInstructions: 12, targetData: 1, context: 1);
                return effect;
            }

            /// <summary>
            /// 构造所有 Effect 共用的显式 target/time 默认 policy。
            /// </summary>
            private static GasGameplayEffectDefinitionBlob BaseEffect(
                int definitionId,
                GasEffectLifetimePolicy lifetime,
                int durationTicks)
            {
                return new GasGameplayEffectDefinitionBlob
                {
                    DefinitionId = definitionId,
                    Lifetime = lifetime,
                    TargetPolicy = FrozenAliveTarget(),
                    DurationTicks = durationTicks,
                    DurationRefreshPolicy = GasDurationRefreshPolicy.Never,
                    PeriodResetPolicy = GasPeriodResetPolicy.Never,
                    ExpiryPolicy = GasExpiryPolicy.Remove,
                    ExpiryPeriodPolicy = GasExpiryPeriodPolicy.Stop,
                    ExpirySameTickPolicy = GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                    InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                    MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
                };
            }

            /// <summary>
            /// 构造排序 Definition index entry。
            /// </summary>
            private static GasDefinitionIndexEntry DefinitionIndex(int definitionId, int definitionIndex)
            {
                return new GasDefinitionIndexEntry
                {
                    DefinitionId = definitionId,
                    DefinitionIndex = definitionIndex,
                };
            }

            /// <summary>
            /// 构造只应用一个 Effect 的静态有界 DirectEffectProgram 节点。
            /// </summary>
            private static GasDirectEffectProgramNodeBlob DirectNode(int effectDefinitionId)
            {
                return new GasDirectEffectProgramNodeBlob
                {
                    NodeOrdinal = 0,
                    EffectDefinitionId = effectDefinitionId,
                    MaximumTargetCount = 1,
                    MaximumOutputCount = 1,
                };
            }

            /// <summary>
            /// 构造一个 FrozenAsc/RequireSameAvatar/AliveOnly Ability Definition。
            /// </summary>
            private static GasAbilityDefinitionBlob Ability(int definitionId, GasCatalogRange programRange)
            {
                return new GasAbilityDefinitionBlob
                {
                    DefinitionId = definitionId,
                    Level = 1,
                    TargetPolicy = FrozenAliveTarget(),
                    DirectEffectProgramRange = programRange,
                    Maxima = Maxima(directNodes: 1, directOutputs: 1),
                };
            }

            /// <summary>
            /// 构造 Hostile Ability/Effect 的正交 target policy。
            /// </summary>
            private static GasTargetPolicyBlob FrozenAliveTarget()
            {
                return new GasTargetPolicyBlob
                {
                    LogicalTarget = GasLogicalTargetPolicy.FrozenAsc,
                    Avatar = GasAvatarTargetPolicy.RequireSameAvatar,
                    Spatial = GasSpatialTargetPolicy.None,
                    Life = GasTargetLifePolicy.AliveOnly,
                };
            }

            /// <summary>
            /// 构造一个 capture descriptor 测试 record。
            /// </summary>
            private static GasCaptureDescriptorBlob Capture(
                int ordinal,
                GasCaptureOwner owner,
                GasCaptureBinding binding,
                GasCapturePhase phase,
                GasLiveCaptureScope liveScope,
                int attributeLayoutIndex)
            {
                return new GasCaptureDescriptorBlob
                {
                    CaptureOrdinal = ordinal,
                    Owner = owner,
                    Binding = binding,
                    Phase = phase,
                    LiveScope = liveScope,
                    GonePolicy = GasCaptureGonePolicy.RejectApplication,
                    ValueView = GasAttributeValueView.Current,
                    AttributeLayoutIndex = attributeLayoutIndex,
                    ConsumerNodeOrdinal = 0,
                    ConsumerFieldOrdinal = ordinal,
                };
            }

            /// <summary>
            /// 构造一条通用 evaluator instruction。
            /// </summary>
            private static GasEvaluatorInstructionBlob Instruction(
                GasEvaluatorOpcode opcode,
                int operandIndex = 0,
                float constantValue = 0f)
            {
                return new GasEvaluatorInstructionBlob
                {
                    Opcode = opcode,
                    OperandIndex = operandIndex,
                    ConstantValue = constantValue,
                };
            }

            /// <summary>
            /// 构造一个解析到 Health layout index 的 ValueView descriptor。
            /// </summary>
            private static GasValueViewDescriptorBlob ValueView(GasAttributeValueView view)
            {
                return new GasValueViewDescriptorBlob
                {
                    AttributeLayoutIndex = 0,
                    ValueView = view,
                };
            }

            /// <summary>
            /// 构造测试 Definition 的静态容量上限。
            /// </summary>
            private static GasDefinitionMaxima Maxima(
                int requirements = 0,
                int captures = 0,
                int directNodes = 0,
                int directOutputs = 0,
                int cues = 0,
                int valueViews = 0,
                int evaluatorInstructions = 0,
                int setByCaller = 0,
                int targetData = 0,
                int context = 0)
            {
                return new GasDefinitionMaxima
                {
                    MaximumTargetCount = 1,
                    MaximumPlannedApplicationCount = 1,
                    MaximumRequirementCount = requirements,
                    MaximumCaptureDescriptorCount = captures,
                    MaximumDirectProgramNodeCount = directNodes,
                    MaximumDirectProgramOutputCount = directOutputs,
                    MaximumCueCount = cues,
                    MaximumValueViewCount = valueViews,
                    MaximumEvaluatorInstructionCount = evaluatorInstructions,
                    MaximumSetByCallerCount = setByCaller,
                    MaximumTargetDataCount = targetData,
                    MaximumEffectContextFieldCount = context,
                };
            }

            /// <summary>
            /// 构造根数组半开区间。
            /// </summary>
            private static GasCatalogRange Range(int start, int count)
            {
                return new GasCatalogRange { Start = start, Count = count };
            }
        }
    }
}
