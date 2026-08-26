using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 构造并持有 AutoChess 专属 Runtime v1 immutable Catalog，供唯一 Session owner 使用。
    /// </summary>
    internal static class AutoChessBattleDefinitionCatalogBuilder
    {
        internal const ulong SchemaHash = 0x4155544F43484331UL;
        internal const ulong ContentHash = 0x4155544F43484332UL;
        internal const ulong AttributeHash = 0x4155544F43484333UL;
        internal const ulong TagHash = 0x4155544F43484334UL;

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
        /// 构造满足 v1 validator 的最小 Attribute/Tag/Definition 闭集。
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
                var attributes = builder.Allocate(ref root.AttributeLayout.Entries, 2);
                builder.Allocate(ref root.TagCatalog.Entries, 0);
                builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
                builder.Allocate(ref root.AbilityIndex, 0);
                builder.Allocate(ref root.Abilities, 0);
                var effectIds = new[]
                {
                    AutoChessBattleRules.GameplayEffectPlayerAttackDamage,
                    AutoChessBattleRules.GameplayEffectEnemyAttackDamage,
                    AutoChessBattleRules.GameplayEffectPlayerPoison,
                    AutoChessBattleRules.GameplayEffectPlayerExecute,
                };
                var effectIndices = builder.Allocate(ref root.GameplayEffectIndex, effectIds.Length);
                var effects = builder.Allocate(ref root.GameplayEffects, effectIds.Length);
                for (var index = 0; index < effectIds.Length; index++)
                {
                    effectIndices[index] = new GasDefinitionIndexEntry
                    {
                        DefinitionId = effectIds[index],
                        DefinitionIndex = index,
                    };
                    effects[index] = CreateEffect(effectIds[index], index);
                }
                builder.Allocate(ref root.Requirements, 0);
                builder.Allocate(ref root.RequirementTagIndices, 0);
                builder.Allocate(ref root.CaptureDescriptors, 0);
                var modifiers = builder.Allocate(ref root.Modifiers, effectIds.Length);
                for (var index = 0; index < modifiers.Length; index++)
                {
                    modifiers[index] = new GasModifierDefinitionBlob
                    {
                        AttributeLayoutIndex = 0,
                        Operation = GasModifierOperation.Add,
                        EvaluatorProgramRange = new GasCatalogRange { Start = index, Count = 1 },
                    };
                }
                builder.Allocate(ref root.DirectEffectProgramNodes, 0);
                var cues = builder.Allocate(ref root.CueReferences, 1);
                cues[0] = new GasCueReferenceBlob
                {
                    CueDefinitionId = 9402,
                    CueDefinitionOrdinal = 0,
                    Phases = GasCuePhaseFlags.Executed,
                };
                builder.Allocate(ref root.ValueViews, 0);
                var evaluators = builder.Allocate(ref root.EvaluatorInstructions, effectIds.Length);
                evaluators[0] = CreateEvaluator(-44f);
                evaluators[1] = CreateEvaluator(-44f);
                evaluators[2] = CreateEvaluator(-10f);
                evaluators[3] = CreateEvaluator(-1000f);
                builder.Allocate(ref root.SetByCallerDescriptors, 0);
                builder.Allocate(ref root.TargetDataDescriptors, 0);
                builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
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
                return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(allocator);
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 创建一个最小 Instant Health modifier effect，保证 AutoChess typed effect 请求可被 v1 Catalog 解析。
        /// </summary>
        private static GasGameplayEffectDefinitionBlob CreateEffect(int definitionId, int definitionIndex)
        {
            var isPoison = definitionId == AutoChessBattleRules.GameplayEffectPlayerPoison;
            var isExecute = definitionId == AutoChessBattleRules.GameplayEffectPlayerExecute;
            return new GasGameplayEffectDefinitionBlob
            {
                DefinitionId = definitionId,
                Lifetime = isExecute
                    ? GasEffectLifetimePolicy.InstantExecution
                    : isPoison
                        ? GasEffectLifetimePolicy.Duration
                        : GasEffectLifetimePolicy.Instant,
                TargetPolicy = new GasTargetPolicyBlob
                {
                    LogicalTarget = GasLogicalTargetPolicy.Self,
                    Avatar = GasAvatarTargetPolicy.FollowAsc,
                    Spatial = GasSpatialTargetPolicy.None,
                    Life = GasTargetLifePolicy.AliveOnly,
                },
                ModifierRange = new GasCatalogRange { Start = definitionIndex, Count = 1 },
                EvaluatorProgramRange = new GasCatalogRange { Start = definitionIndex, Count = 1 },
                CueRange = isExecute
                    ? new GasCatalogRange { Start = 0, Count = 1 }
                    : default,
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
                DurationRefreshPolicy = isPoison
                    ? GasDurationRefreshPolicy.OnSuccessfulApplication
                    : GasDurationRefreshPolicy.Never,
                PeriodResetPolicy = isPoison
                    ? GasPeriodResetPolicy.OnSuccessfulApplication
                    : GasPeriodResetPolicy.Never,
                ExpiryPolicy = GasExpiryPolicy.Remove,
                ExpiryPeriodPolicy = GasExpiryPeriodPolicy.Stop,
                ExpirySameTickPolicy = GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                InhibitedPeriodPolicy = isPoison
                    ? GasInhibitedPeriodPolicy.ContinueExecution
                    : GasInhibitedPeriodPolicy.None,
                MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
                ExecuteOnApplication = 0,
                Maxima = new GasDefinitionMaxima
                {
                    MaximumTargetCount = 1,
                    MaximumPlannedApplicationCount = 1,
                    MaximumModifierCount = 1,
                    MaximumEvaluatorInstructionCount = 1,
                    MaximumCueCount = isExecute ? 1 : 0,
                },
            };
        }

        /// <summary>
        /// 构造一个仅从 immutable evaluator 常量读取的属性增量程序。
        /// </summary>
        private static GasEvaluatorInstructionBlob CreateEvaluator(float value)
        {
            return new GasEvaluatorInstructionBlob
            {
                Opcode = GasEvaluatorOpcode.PushConstant,
                ConstantValue = value,
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
