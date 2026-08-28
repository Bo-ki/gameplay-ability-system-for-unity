using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 构造并持有 AutoChess 专属 Runtime v1 immutable Catalog，供唯一 Session owner 使用。
    /// Catalog 只在初始化阶段物化；运行时热路径只读取已安装 Blob，不保留旧效果或兼容选择器。
    /// </summary>
    internal static class AutoChessBattleDefinitionCatalogBuilder
    {
        internal const ulong SchemaHash = 0x4155544F43484331UL;
        internal const ulong ContentHash = 0x4155544F43484332UL;
        internal const ulong AttributeHash = 0x4155544F43484335UL;
        internal const ulong TagHash = 0x4155544F43484336UL;

        private const int AttackAttributeLayoutIndex = 2;
        private const int CuePoison = 9403;
        private const int CueExecute = 9404;
        private static BlobAssetReference<GasDefinitionCatalogBlob> _catalog;

        /// <summary>
        /// 构造并安装唯一 AutoChess v1 Catalog；Session Stage-B 由宿主在 SpawnBatch 中引用该 Blob。
        /// </summary>
        internal static bool Install(EntityManager entityManager)
        {
            if (entityManager.World == null || !entityManager.World.IsCreated)
                return false;

            DisposeCatalog();
            _catalog = BuildCatalog(Allocator.Persistent);
            return _catalog.IsCreated;
        }

        /// <summary>
        /// 释放 AutoChess v1 Catalog，禁止留下跨 World 或跨 schema 的引用。
        /// </summary>
        internal static void Uninstall(EntityManager entityManager)
        {
            DisposeCatalog();
        }

        /// <summary>
        /// 返回当前 AutoChess v1 Catalog，未安装时返回未创建引用。
        /// </summary>
        internal static BlobAssetReference<GasDefinitionCatalogBlob> Catalog => _catalog;

        /// <summary>
        /// 构造 3 属性、1 Tag、4 Ability 与 4 GameplayEffect 的冻结闭集。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> BuildCatalog(Allocator allocator)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                root.SchemaVersion = GasDefinitionCatalogSchema.Version;
                root.SchemaHash = SchemaHash;
                root.ContentHash = ContentHash;
                root.AttributeLayout.LayoutHash = AttributeHash;
                root.TagCatalog.CatalogHash = TagHash;

                AllocateAttributes(builder, ref root);
                AllocateTags(builder, ref root);
                AllocateDefinitions(builder, ref root);
                AllocatePayloads(builder, ref root);
                return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(allocator);
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 写入稳定排序的 AttributeLayout；Attack 是 9203 source capture 的唯一输入。
        /// </summary>
        private static void AllocateAttributes(BlobBuilder builder, ref GasDefinitionCatalogBlob root)
        {
            var attributes = builder.Allocate(ref root.AttributeLayout.Entries, 3);
            attributes[0] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = AutoChessBattleRules.AttributeHealth,
                LayoutIndex = 0,
                DomainRole = GasAttributeDomainRole.Health,
                DefaultValue = 100f,
                MinimumValue = 0f,
                MaximumValue = 1000f,
                ClampMinimum = 1,
                ClampMaximum = 1,
            };
            attributes[1] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = AutoChessBattleRules.AttributeEnergy,
                LayoutIndex = 1,
                DomainRole = GasAttributeDomainRole.None,
                DefaultValue = 0f,
                MinimumValue = 0f,
                MaximumValue = 100f,
                ClampMinimum = 1,
                ClampMaximum = 1,
            };
            attributes[2] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = 3,
                LayoutIndex = AttackAttributeLayoutIndex,
                DomainRole = GasAttributeDomainRole.None,
                DefaultValue = 20f,
                MinimumValue = 0f,
                MaximumValue = 1000f,
                ClampMinimum = 1,
                ClampMaximum = 1,
            };
        }

        /// <summary>
        /// 写入稳定 Tag 与包含自身的 ancestor chain，供 cooldown gate 计数使用。
        /// </summary>
        private static void AllocateTags(BlobBuilder builder, ref GasDefinitionCatalogBlob root)
        {
            var tags = builder.Allocate(ref root.TagCatalog.Entries, 1);
            var ancestors = builder.Allocate(ref root.TagCatalog.AncestorIndices, 1);
            tags[0] = new GasTagCatalogEntryBlob
            {
                TagId = AutoChessBattleRules.TagAttackCooldown,
                TagIndex = 0,
                AncestorIndexRange = new GasCatalogRange { Start = 0, Count = 1 },
            };
            ancestors[0] = 0;
        }

        /// <summary>
        /// 写入 Ability/Effect 索引与 Definition header，所有 range 指向下方已分配的闭集数组。
        /// </summary>
        private static void AllocateDefinitions(BlobBuilder builder, ref GasDefinitionCatalogBlob root)
        {
            var abilityIds = new[] { 9101, 9102, 9103, 9104 };
            var effectIds = new[]
            {
                AutoChessBattleRules.GameplayEffectPlayerAttackDamage,
                AutoChessBattleRules.GameplayEffectEnemyAttackDamage,
                AutoChessBattleRules.GameplayEffectPlayerPoison,
                AutoChessBattleRules.GameplayEffectPlayerExecute,
            };
            var abilityIndex = builder.Allocate(ref root.AbilityIndex, abilityIds.Length);
            var abilities = builder.Allocate(ref root.Abilities, abilityIds.Length);
            for (var index = 0; index < abilityIds.Length; index++)
            {
                abilityIndex[index] = new GasDefinitionIndexEntry
                {
                    DefinitionId = abilityIds[index],
                    DefinitionIndex = index,
                };
                abilities[index] = CreateAbility(abilityIds[index], index);
            }

            var effectIndex = builder.Allocate(ref root.GameplayEffectIndex, effectIds.Length);
            var effects = builder.Allocate(ref root.GameplayEffects, effectIds.Length);
            for (var index = 0; index < effectIds.Length; index++)
            {
                effectIndex[index] = new GasDefinitionIndexEntry
                {
                    DefinitionId = effectIds[index],
                    DefinitionIndex = index,
                };
                effects[index] = CreateEffect(effectIds[index], index);
            }
        }

        /// <summary>
        /// 写入 evaluator、modifier、target/context descriptor 与 Cue 的稳定全局数组。
        /// </summary>
        private static void AllocatePayloads(BlobBuilder builder, ref GasDefinitionCatalogBlob root)
        {
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            var captures = builder.Allocate(ref root.CaptureDescriptors, 1);
            captures[0] = new GasCaptureDescriptorBlob
            {
                CaptureOrdinal = 0,
                Owner = GasCaptureOwner.Source,
                Binding = GasCaptureBinding.Snapshot,
                Phase = GasCapturePhase.SourceSpecProjection,
                LiveScope = GasLiveCaptureScope.None,
                GonePolicy = GasCaptureGonePolicy.RejectApplication,
                ValueView = GasAttributeValueView.Current,
                AttributeLayoutIndex = AttackAttributeLayoutIndex,
                ConsumerNodeOrdinal = 0,
                ConsumerFieldOrdinal = 0,
            };

            var modifiers = builder.Allocate(ref root.Modifiers, 4);
            for (var index = 0; index < modifiers.Length; index++)
            {
                modifiers[index] = new GasModifierDefinitionBlob
                {
                    AttributeLayoutIndex = 0,
                    Operation = GasModifierOperation.Add,
                    EvaluatorProgramRange = new GasCatalogRange { Start = index, Count = 1 },
                };
            }
            modifiers[2].CaptureRange = new GasCatalogRange { Start = 0, Count = 1 };
            modifiers[2].EvaluatorProgramRange = new GasCatalogRange { Start = 2, Count = 5 };
            modifiers[3].EvaluatorProgramRange = new GasCatalogRange { Start = 7, Count = 13 };

            var nodes = builder.Allocate(ref root.DirectEffectProgramNodes, 4);
            for (var index = 0; index < nodes.Length; index++)
            {
                nodes[index] = new GasDirectEffectProgramNodeBlob
                {
                    NodeOrdinal = 0,
                    EffectDefinitionId = index == 0
                        ? AutoChessBattleRules.GameplayEffectPlayerAttackDamage
                        : index == 1
                            ? AutoChessBattleRules.GameplayEffectEnemyAttackDamage
                            : index == 2
                                ? AutoChessBattleRules.GameplayEffectPlayerExecute
                                : AutoChessBattleRules.GameplayEffectPlayerPoison,
                    MaximumTargetCount = 1,
                    MaximumOutputCount = 1,
                };
            }

            var cues = builder.Allocate(ref root.CueReferences, 2);
            cues[0] = new GasCueReferenceBlob
            {
                CueDefinitionId = CuePoison,
                CueDefinitionOrdinal = 0,
                Phases = GasCuePhaseFlags.OnActive | GasCuePhaseFlags.WhileActive | GasCuePhaseFlags.Removed,
            };
            cues[1] = new GasCueReferenceBlob
            {
                CueDefinitionId = CueExecute,
                CueDefinitionOrdinal = 0,
                Phases = GasCuePhaseFlags.Executed,
            };

            var valueViews = builder.Allocate(ref root.ValueViews, 3);
            valueViews[0] = new GasValueViewDescriptorBlob
            {
                AttributeLayoutIndex = 0,
                ValueView = GasAttributeValueView.Base,
            };
            valueViews[1] = new GasValueViewDescriptorBlob
            {
                AttributeLayoutIndex = 0,
                ValueView = GasAttributeValueView.Current,
            };
            valueViews[2] = new GasValueViewDescriptorBlob
            {
                AttributeLayoutIndex = 0,
                ValueView = GasAttributeValueView.DefinitionMaxValue,
            };

            var evaluators = builder.Allocate(ref root.EvaluatorInstructions, 20);
            evaluators[0] = PushConstant(-44f);
            evaluators[1] = PushConstant(-44f);
            evaluators[2] = PushCapture(0);
            evaluators[3] = PushConstant(-0.3f);
            evaluators[4] = Op(GasEvaluatorOpcode.Multiply);
            evaluators[5] = Op(GasEvaluatorOpcode.PushStackCount);
            evaluators[6] = Op(GasEvaluatorOpcode.Multiply);
            evaluators[7] = PushValueView(2);
            evaluators[8] = PushValueView(0);
            evaluators[9] = Op(GasEvaluatorOpcode.Subtract);
            evaluators[10] = PushConstant(0f);
            evaluators[11] = Op(GasEvaluatorOpcode.Maximum);
            evaluators[12] = PushConstant(0.5f);
            evaluators[13] = Op(GasEvaluatorOpcode.Multiply);
            evaluators[14] = PushConstant(16f);
            evaluators[15] = Op(GasEvaluatorOpcode.Add);
            evaluators[16] = PushConstant(12f);
            evaluators[17] = PushConstant(42f);
            evaluators[18] = Op(GasEvaluatorOpcode.Clamp);
            evaluators[19] = Op(GasEvaluatorOpcode.Negate);

            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            var targetData = builder.Allocate(ref root.TargetDataDescriptors, 4);
            var context = builder.Allocate(ref root.EffectContextFieldDescriptors, 12);
            for (var index = 0; index < targetData.Length; index++)
                targetData[index] = new GasTargetDataDescriptorBlob
                {
                    Variant = GasTargetDataVariant.StableAsc,
                    FieldOrdinal = 0,
                    Required = 1,
                };
            for (var effectIndex = 0; effectIndex < 4; effectIndex++)
            {
                context[effectIndex * 3] = Context(GasEffectContextFieldKind.CausalityId, 0);
                context[effectIndex * 3 + 1] = Context(
                    GasEffectContextFieldKind.SourceAvatarBindingGeneration, 1);
                context[effectIndex * 3 + 2] = Context(
                    GasEffectContextFieldKind.TargetAvatarBindingGeneration, 2);
            }
        }

        /// <summary>
        /// 创建一个 owner-local Ability Definition，并绑定一个直接效果节点。
        /// </summary>
        private static GasAbilityDefinitionBlob CreateAbility(int definitionId, int definitionIndex)
        {
            return new GasAbilityDefinitionBlob
            {
                DefinitionId = definitionId,
                Level = 1,
                MaxConcurrentActivations = 1,
                TargetPolicy = FrozenTargetPolicy(),
                DirectEffectProgramRange = new GasCatalogRange
                {
                    Start = definitionIndex,
                    Count = 1,
                },
                Maxima = new GasDefinitionMaxima
                {
                    MaximumTargetCount = 1,
                    MaximumPlannedApplicationCount = 1,
                    MaximumRequirementCount = 0,
                    MaximumDirectProgramNodeCount = 1,
                    MaximumDirectProgramOutputCount = 1,
                },
            };
        }

        /// <summary>
        /// 创建一个符合 9201/9202/9203/9207 冻结语义的 GameplayEffect Definition。
        /// </summary>
        private static GasGameplayEffectDefinitionBlob CreateEffect(int definitionId, int definitionIndex)
        {
            var isPoison = definitionId == AutoChessBattleRules.GameplayEffectPlayerPoison;
            var isExecute = definitionId == AutoChessBattleRules.GameplayEffectPlayerExecute;
            var effect = new GasGameplayEffectDefinitionBlob
            {
                DefinitionId = definitionId,
                Lifetime = isExecute
                    ? GasEffectLifetimePolicy.InstantExecution
                    : isPoison
                        ? GasEffectLifetimePolicy.Duration
                        : GasEffectLifetimePolicy.Instant,
                TargetPolicy = FrozenTargetPolicy(),
                ModifierRange = new GasCatalogRange { Start = definitionIndex, Count = 1 },
                EvaluatorProgramRange = isPoison
                    ? new GasCatalogRange { Start = 2, Count = 5 }
                    : isExecute
                        ? new GasCatalogRange { Start = 7, Count = 13 }
                        : new GasCatalogRange { Start = definitionIndex, Count = 1 },
                TargetDataRange = new GasCatalogRange { Start = definitionIndex, Count = 1 },
                EffectContextFieldRange = new GasCatalogRange { Start = definitionIndex * 3, Count = 3 },
                DurationTicks = isPoison ? 8 : 0,
                PeriodTicks = isPoison ? 2 : 0,
                StackLimit = isPoison ? 3 : 0,
                StackKey = isPoison
                    ? GasStackKeyFields.Definition | GasStackKeyFields.TargetAsc | GasStackKeyFields.SourceAsc
                    : GasStackKeyFields.None,
                StackPolicy = isPoison ? GasStackPolicy.AggregateBySource : GasStackPolicy.None,
                StackPayloadPolicy = isPoison
                    ? GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance
                    : GasStackPayloadPolicy.None,
                StackLimitApplicationPolicy = isPoison
                    ? GasStackLimitApplicationPolicy.AcceptAndKeepLimit
                    : GasStackLimitApplicationPolicy.None,
                DurationRefreshPolicy = GasDurationRefreshPolicy.Never,
                PeriodResetPolicy = isPoison
                    ? GasPeriodResetPolicy.OnSuccessfulApplication
                    : GasPeriodResetPolicy.Never,
                ExpiryPolicy = isPoison
                    ? GasExpiryPolicy.RemoveOneStackAndRefreshDuration
                    : GasExpiryPolicy.Remove,
                ExpiryPeriodPolicy = isPoison
                    ? GasExpiryPeriodPolicy.Reset
                    : GasExpiryPeriodPolicy.Stop,
                ExpirySameTickPolicy = isPoison
                    ? GasExpirySameTickPolicy.PeriodDueBeforeExpiry
                    : GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                InhibitedPeriodPolicy = isPoison
                    ? GasInhibitedPeriodPolicy.SkipExecution
                    : GasInhibitedPeriodPolicy.None,
                MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
                ExecuteOnApplication = 0,
                Maxima = CreateEffectMaxima(isPoison, isExecute),
            };
            if (isPoison)
            {
                effect.CaptureRange = new GasCatalogRange { Start = 0, Count = 1 };
                effect.ModifierRange = new GasCatalogRange { Start = 2, Count = 1 };
                effect.CueRange = new GasCatalogRange { Start = 0, Count = 1 };
            }
            else if (isExecute)
            {
                effect.ModifierRange = new GasCatalogRange { Start = 3, Count = 1 };
                effect.ValueViewRange = new GasCatalogRange { Start = 0, Count = 3 };
                effect.RequiredValueViews = GasAttributeValueViewMask.Base
                    | GasAttributeValueViewMask.Current
                    | GasAttributeValueViewMask.DefinitionMaxValue;
                effect.CueRange = new GasCatalogRange { Start = 1, Count = 1 };
            }
            return effect;
        }

        /// <summary>
        /// 返回 AutoChess 全部目标解析所需的稳定 ASC、Avatar 与生命策略。
        /// </summary>
        private static GasTargetPolicyBlob FrozenTargetPolicy()
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
        /// 计算每个 Effect 的静态展开上限，拒绝运行时隐式扩容。
        /// </summary>
        private static GasDefinitionMaxima CreateEffectMaxima(bool isPoison, bool isExecute)
        {
            return new GasDefinitionMaxima
            {
                MaximumTargetCount = 1,
                MaximumPlannedApplicationCount = 1,
                MaximumRequirementCount = 0,
                MaximumCaptureDescriptorCount = isPoison ? 1 : 0,
                MaximumModifierCount = 1,
                MaximumDirectProgramNodeCount = 0,
                MaximumDirectProgramOutputCount = 0,
                MaximumCueCount = isPoison || isExecute ? 1 : 0,
                MaximumValueViewCount = isExecute ? 3 : 0,
                MaximumEvaluatorInstructionCount = isPoison ? 5 : isExecute ? 13 : 1,
                MaximumSetByCallerCount = 0,
                MaximumTargetDataCount = 1,
                MaximumEffectContextFieldCount = 3,
            };
        }

        /// <summary>
        /// 创建有限常量 evaluator 指令。
        /// </summary>
        private static GasEvaluatorInstructionBlob PushConstant(float value)
        {
            return new GasEvaluatorInstructionBlob
            {
                Opcode = GasEvaluatorOpcode.PushConstant,
                ConstantValue = value,
            };
        }

        /// <summary>
        /// 创建带 capture operand 的 evaluator 指令。
        /// </summary>
        private static GasEvaluatorInstructionBlob PushCapture(int operandIndex)
        {
            return new GasEvaluatorInstructionBlob
            {
                Opcode = GasEvaluatorOpcode.PushCapture,
                OperandIndex = operandIndex,
            };
        }

        /// <summary>
        /// 创建带 ValueView operand 的 evaluator 指令。
        /// </summary>
        private static GasEvaluatorInstructionBlob PushValueView(int operandIndex)
        {
            return new GasEvaluatorInstructionBlob
            {
                Opcode = GasEvaluatorOpcode.PushValueView,
                OperandIndex = operandIndex,
            };
        }

        /// <summary>
        /// 创建不携带 operand 的 evaluator 运算指令。
        /// </summary>
        private static GasEvaluatorInstructionBlob Op(GasEvaluatorOpcode opcode)
        {
            return new GasEvaluatorInstructionBlob { Opcode = opcode };
        }

        /// <summary>
        /// 创建一个带必填标记的 EffectContext descriptor。
        /// </summary>
        private static GasEffectContextFieldDescriptorBlob Context(
            GasEffectContextFieldKind field,
            int ordinal)
        {
            return new GasEffectContextFieldDescriptorBlob
            {
                Field = field,
                FieldOrdinal = ordinal,
                Required = 1,
            };
        }

        /// <summary>
        /// 释放当前 immutable blob，保证重复初始化不会泄漏 native allocation。
        /// </summary>
        private static void DisposeCatalog()
        {
            if (_catalog.IsCreated)
                _catalog.Dispose();
            _catalog = default;
        }
    }
}
