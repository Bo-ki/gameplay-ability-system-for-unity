using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 target-owned GameplayEffect transaction 的 evaluator、requirement、stack 与 slab 原子语义。
    /// </summary>
    [TestFixture]
    public class RuntimeV1GameplayEffectTransactionTests
    {
        /// <summary>
        /// 验证 Instant Add 在目标 ASC 内更新 Base/Current、Revision 与 dirty bit。
        /// </summary>
        [Test]
        public void InstantAdd_在TargetTransaction内提交Attribute变更()
        {
            using var world = new World("Runtime v1 effect instant test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateCatalog(GasEffectLifetimePolicy.Instant, 5f, 0);
            using var stack = new NativeArray<float>(8, Allocator.Temp);

            var result = Apply(world, asc, catalog, stack, 1);

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.AppliedInstant));
            Assert.That(world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0].Base, Is.EqualTo(15f));
            Assert.That(world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0].Current, Is.EqualTo(15f));
            Assert.That(world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0].Revision, Is.EqualTo(2));
            Assert.That(world.EntityManager.GetBuffer<AttributeDirtyWord>(asc)[0].Value & 1UL, Is.EqualTo(1UL));
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(asc).Length, Is.Zero);
        }

        /// <summary>
        /// 验证 Duration effect 先完成时序预检，再只分配一个非压缩 ActiveEffect 槽。
        /// </summary>
        [Test]
        public void DurationEffect_创建带Generation的ActiveEffect槽()
        {
            using var world = new World("Runtime v1 effect duration test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateCatalog(GasEffectLifetimePolicy.Duration, 0f, 3);
            using var stack = new NativeArray<float>(8, Allocator.Temp);

            var result = Apply(world, asc, catalog, stack, 9);
            var effects = world.EntityManager.GetBuffer<ActiveEffectSlot>(asc);

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.CreatedActive));
            Assert.That(effects.Length, Is.EqualTo(1));
            Assert.That(effects[0].Handle.IsValid, Is.True);
            Assert.That(effects[0].StartTick, Is.EqualTo(9UL));
            Assert.That(effects[0].EndTick, Is.EqualTo(12UL));
        }

        /// <summary>
        /// 验证第二个 modifier 无效时第一条 modifier 不得留下 Attribute 或 dirty 部分写入。
        /// </summary>
        [Test]
        public void ModifierPreflight_失败时保持整条TargetTransaction原子()
        {
            using var world = new World("Runtime v1 effect atomic test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateCatalogWithInvalidSecondModifier();
            using var stack = new NativeArray<float>(8, Allocator.Temp);

            var result = Apply(world, asc, catalog, stack, 2);
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.RejectedDefinition));
            Assert.That(value.Base, Is.EqualTo(10f));
            Assert.That(value.Current, Is.EqualTo(10f));
            Assert.That(value.Revision, Is.EqualTo(1));
            Assert.That(world.EntityManager.GetBuffer<AttributeDirtyWord>(asc)[0].Value, Is.Zero);
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(asc).Length, Is.Zero);
        }

        /// <summary>
        /// 验证前一条 modifier 成功后，后一条顺序计算溢出也不得写入 Attribute。
        /// </summary>
        [Test]
        public void SequentialModifierOverflow_预检失败时不留下部分写入()
        {
            using var world = new World("Runtime v1 effect sequential overflow test");
            var asc = CreateTarget(world, 1e38f);
            using var catalog = CreateCatalogWithInvalidSecondModifier(
                GasEffectLifetimePolicy.Instant,
                sequentialOverflow: true);
            using var stack = new NativeArray<float>(8, Allocator.Temp);

            var result = Apply(world, asc, catalog, stack, 4);
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.RejectedDefinition));
            Assert.That(value.Base, Is.EqualTo(1e38f));
            Assert.That(value.Current, Is.EqualTo(1e38f));
            Assert.That(value.Revision, Is.EqualTo(1));
            Assert.That(world.EntityManager.GetBuffer<AttributeDirtyWord>(asc)[0].Value, Is.Zero);
        }

        /// <summary>
        /// 验证 Duration transaction 在 modifier 预检失败时连 ActiveEffect 槽也不分配。
        /// </summary>
        [Test]
        public void DurationModifierPreflight_失败时不分配ActiveEffect槽()
        {
            using var world = new World("Runtime v1 effect duration rollback test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateCatalogWithInvalidSecondModifier(GasEffectLifetimePolicy.Duration);
            using var stack = new NativeArray<float>(8, Allocator.Temp);

            var result = Apply(world, asc, catalog, stack, 6);
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.RejectedDefinition));
            Assert.That(value.Base, Is.EqualTo(10f));
            Assert.That(value.Current, Is.EqualTo(10f));
            Assert.That(value.Revision, Is.EqualTo(1));
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(asc).Length, Is.Zero);
        }

        /// <summary>
        /// 验证 application requirement 拒绝时不触碰 target Attribute 与 ActiveEffect slab。
        /// </summary>
        [Test]
        public void RequirementRejected_不产生任何TargetAuthority写入()
        {
            using var world = new World("Runtime v1 effect requirement test");
            var asc = CreateTarget(world, 10f, 1);
            using var catalog = CreateRequirementCatalog();
            using var stack = new NativeArray<float>(8, Allocator.Temp);

            var result = Apply(world, asc, catalog, stack, 3);
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.RejectedRequirement));
            Assert.That(value.Base, Is.EqualTo(10f));
            Assert.That(value.Revision, Is.EqualTo(1));
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(asc).Length, Is.Zero);
        }

        /// <summary>
        /// 验证 immunity range 越界属于 definition 错误，而不是“未免疫”继续执行。
        /// </summary>
        [Test]
        public void InvalidImmunityRange_拒绝且不产生TargetAuthority写入()
        {
            using var world = new World("Runtime v1 effect invalid immunity test");
            var asc = CreateTarget(world, 10f, 1);
            using var catalog = CreateRequirementCatalog(invalidImmunity: true);
            using var stack = new NativeArray<float>(8, Allocator.Temp);

            var result = Apply(world, asc, catalog, stack, 5);
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.RejectedDefinition));
            Assert.That(result.Failure, Is.EqualTo(GasGameplayEffectTransactionFailure.InvalidDefinition));
            Assert.That(value.Base, Is.EqualTo(10f));
            Assert.That(value.Current, Is.EqualTo(10f));
            Assert.That(value.Revision, Is.EqualTo(1));
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(asc).Length, Is.Zero);
        }

        /// <summary>
        /// 验证 stack reapply 的 ExecuteOnApplication 使用提交后的 stack count 执行一次 modifier body。
        /// </summary>
        [Test]
        public void StackMerge_ExecuteOnApplication使用拟议StackCount并提交一次()
        {
            using var world = new World("Runtime v1 effect stack execute test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateStackCatalog(executeOnApplication: 1, useStackCount: true);
            using var evaluatorStack = new NativeArray<float>(8, Allocator.Temp);
            var head = SeedActiveStack(world, asc, 1, 20, 11);

            var result = ApplyWithHead(world, asc, catalog, evaluatorStack, 101, ref head, 9);
            var slot = world.EntityManager.GetBuffer<ActiveEffectSlot>(asc)[0];
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.MergedStack));
            Assert.That(result.AppliedModifierCount, Is.EqualTo(1));
            Assert.That(slot.StackCount, Is.EqualTo(2));
            Assert.That(value.Current, Is.EqualTo(12f));
            Assert.That(value.Revision, Is.EqualTo(2U));
            Assert.That(world.EntityManager.GetBuffer<AttributeDirtyWord>(asc)[0].Value & 1UL,
                Is.EqualTo(1UL));
        }

        /// <summary>
        /// 验证 ExecuteOnApplication 关闭时 stack merge 不重复执行首次 grant 的 modifier。
        /// </summary>
        [Test]
        public void StackMerge_ExecuteOnApplication关闭时不重复Modifier()
        {
            using var world = new World("Runtime v1 effect stack no execute test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateStackCatalog(executeOnApplication: 0, useStackCount: false);
            using var evaluatorStack = new NativeArray<float>(8, Allocator.Temp);
            var head = SeedActiveStack(world, asc, 1, 20, 11);

            var result = ApplyWithHead(world, asc, catalog, evaluatorStack, 102, ref head, 9);
            var slot = world.EntityManager.GetBuffer<ActiveEffectSlot>(asc)[0];
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.MergedStack));
            Assert.That(result.AppliedModifierCount, Is.Zero);
            Assert.That(slot.StackCount, Is.EqualTo(2));
            Assert.That(value.Current, Is.EqualTo(10f));
            Assert.That(value.Revision, Is.EqualTo(1U));
            Assert.That(world.EntityManager.GetBuffer<AttributeDirtyWord>(asc)[0].Value, Is.Zero);
        }

        /// <summary>
        /// 验证 stack body 预检失败时不写入 Attribute、dirty 或 ActiveEffect stack。
        /// </summary>
        [Test]
        public void StackMerge_ModifierPreflight失败时保持整条事务原子()
        {
            using var world = new World("Runtime v1 effect stack atomic test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateStackCatalog(
                executeOnApplication: 1,
                useStackCount: false,
                invalidSecondModifier: true);
            using var evaluatorStack = new NativeArray<float>(8, Allocator.Temp);
            var head = SeedActiveStack(world, asc, 1, 20, 11);

            var result = ApplyWithHead(world, asc, catalog, evaluatorStack, 103, ref head, 9);
            var slot = world.EntityManager.GetBuffer<ActiveEffectSlot>(asc)[0];
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.RejectedDefinition));
            Assert.That(result.Failure, Is.EqualTo(GasGameplayEffectTransactionFailure.EvaluatorFailure));
            Assert.That(slot.StackCount, Is.EqualTo(1));
            Assert.That(slot.EndTick, Is.EqualTo(20UL));
            Assert.That(value.Current, Is.EqualTo(10f));
            Assert.That(value.Revision, Is.EqualTo(1U));
            Assert.That(world.EntityManager.GetBuffer<AttributeDirtyWord>(asc)[0].Value, Is.Zero);
            Assert.That(head.HighWater, Is.EqualTo(1));
            Assert.That(head.FreeCount, Is.Zero);
        }

        /// <summary>
        /// 验证 stack timing refresh 的 tick 溢出在写 stack 前拒绝且保留原槽。
        /// </summary>
        [Test]
        public void StackMerge_TimingOverflow预检失败且不写入()
        {
            using var world = new World("Runtime v1 effect stack timing overflow test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateStackCatalog(
                executeOnApplication: 0,
                useStackCount: false,
                durationRefreshPolicy: GasDurationRefreshPolicy.OnSuccessfulApplication);
            using var evaluatorStack = new NativeArray<float>(8, Allocator.Temp);
            var head = SeedActiveStack(world, asc, 1, 20, 11);

            var result = ApplyWithHead(
                world, asc, catalog, evaluatorStack, 104, ref head, ulong.MaxValue);
            var slot = world.EntityManager.GetBuffer<ActiveEffectSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.RejectedDefinition));
            Assert.That(result.Failure, Is.EqualTo(GasGameplayEffectTransactionFailure.InvalidDefinition));
            Assert.That(slot.StackCount, Is.EqualTo(1));
            Assert.That(slot.EndTick, Is.EqualTo(20UL));
            Assert.That(slot.NextPeriodTick, Is.EqualTo(11UL));
            Assert.That(head.HighWater, Is.EqualTo(1));
            Assert.That(head.FreeCount, Is.Zero);
        }

        /// <summary>
        /// 验证 AcceptAndKeepLimit 在上限 reapply 时保持 stack count 但仍执行 application body。
        /// </summary>
        [Test]
        public void StackMerge_达到上限时保持Count并执行Body()
        {
            using var world = new World("Runtime v1 effect stack limit test");
            var asc = CreateTarget(world, 10f);
            using var catalog = CreateStackCatalog(
                executeOnApplication: 1,
                useStackCount: true,
                stackLimitApplicationPolicy: GasStackLimitApplicationPolicy.AcceptAndKeepLimit);
            using var evaluatorStack = new NativeArray<float>(8, Allocator.Temp);
            var head = SeedActiveStack(world, asc, 3, 20, 11);

            var result = ApplyWithHead(world, asc, catalog, evaluatorStack, 105, ref head, 9);
            var slot = world.EntityManager.GetBuffer<ActiveEffectSlot>(asc)[0];
            var value = world.EntityManager.GetBuffer<AttributeValueSlot>(asc)[0];

            Assert.That(result.Outcome, Is.EqualTo(GasGameplayEffectApplicationOutcome.MergedStack));
            Assert.That(slot.StackCount, Is.EqualTo(3));
            Assert.That(value.Current, Is.EqualTo(13f));
        }

        /// <summary>
        /// 创建带固定 Attribute/Tag buffer 的 target ASC，并初始化唯一 stable owner。
        /// </summary>
        private static Entity CreateTarget(World world, float value, int tagCount = 0)
        {
            var entityManager = world.EntityManager;
            var entity = entityManager.CreateEntity();
            entityManager.AddBuffer<AttributeValueSlot>(entity).Add(
                new AttributeValueSlot { Base = value, Current = value, Revision = 1 });
            entityManager.AddBuffer<AttributeDirtyWord>(entity).Add(default);
            var tags = entityManager.AddBuffer<TagCountSlot>(entity);
            tags.ResizeUninitialized(tagCount);
            for (var index = 0; index < tags.Length; index++)
                tags[index] = default;
            var presence = entityManager.AddBuffer<TagPresenceWord>(entity);
            presence.ResizeUninitialized(tagCount <= 0 ? 0 : 1);
            for (var index = 0; index < presence.Length; index++)
                presence[index] = default;
            entityManager.AddBuffer<ActiveEffectSlot>(entity);
            return entity;
        }

        /// <summary>
        /// 以最小 request 调用 target transaction，输入数组生命周期由测试方法持有。
        /// </summary>
        private static GasGameplayEffectApplicationResult Apply(
            World world,
            Entity target,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog,
            NativeArray<float> evaluatorStack,
            ulong applicationId)
        {
            var heads = AscSlabHeads.CreateEmpty();
            return ApplyWithHead(world, target, catalog, evaluatorStack, applicationId,
                ref heads.ActiveEffect, 9);
        }

        /// <summary>
        /// 使用调用方持有的 ActiveEffect slab head 执行一次最小 target transaction。
        /// </summary>
        private static GasGameplayEffectApplicationResult ApplyWithHead(
            World world,
            Entity target,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog,
            NativeArray<float> evaluatorStack,
            ulong applicationId,
            ref GasSlabHead activeEffectHead,
            ulong startTick)
        {
            var em = world.EntityManager;
            var owner = new OwnerAscHandle(7, 1);
            var request = new GasGameplayEffectApplicationRequest
            {
                SimulationEpoch = 1,
                SourceAsc = owner,
                TargetAsc = owner,
                DefinitionIndex = 0,
                ApplicationId = applicationId,
                StartTick = startTick,
                TargetIsAlive = 1,
                CaptureValueCount = 0,
                ValueViewCount = 0,
            };
            ref var root = ref catalog.Value;
            GasGameplayEffectTransaction.TryApply(
                ref root,
                in request,
                em.GetBuffer<ActiveEffectSlot>(target),
                em.GetBuffer<AttributeValueSlot>(target),
                em.GetBuffer<AttributeDirtyWord>(target),
                em.GetBuffer<TagCountSlot>(target),
                em.GetBuffer<TagPresenceWord>(target),
                ref activeEffectHead,
                4,
                default(NativeArray<float>),
                default(NativeArray<float>),
                evaluatorStack,
                out var result);
            return result;
        }

        /// <summary>
        /// 构造带 stack contract 与单 modifier 的最小 Duration catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateStackCatalog(
            byte executeOnApplication,
            bool useStackCount,
            bool invalidSecondModifier = false,
            GasDurationRefreshPolicy durationRefreshPolicy = GasDurationRefreshPolicy.Never,
            GasPeriodResetPolicy periodResetPolicy = GasPeriodResetPolicy.Never,
            GasStackLimitApplicationPolicy stackLimitApplicationPolicy =
                GasStackLimitApplicationPolicy.RejectAtLimit,
            int stackLimit = 3)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                builder.Allocate(ref root.AttributeLayout.Entries, 1)[0] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 1,
                    LayoutIndex = 0,
                    MinimumValue = -1000f,
                    MaximumValue = 1000f,
                    ClampMinimum = 1,
                    ClampMaximum = 1,
                };
                builder.Allocate(ref root.TagCatalog.Entries, 0);
                builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
                builder.Allocate(ref root.GameplayEffectIndex, 1)[0] = new GasDefinitionIndexEntry
                {
                    DefinitionId = 101,
                    DefinitionIndex = 0,
                };
                var modifierCount = invalidSecondModifier ? 2 : 1;
                builder.Allocate(ref root.GameplayEffects, 1)[0] = new GasGameplayEffectDefinitionBlob
                {
                    DefinitionId = 101,
                    Lifetime = GasEffectLifetimePolicy.Duration,
                    TargetPolicy = new GasTargetPolicyBlob { Life = GasTargetLifePolicy.AliveOnly },
                    ModifierRange = new GasCatalogRange { Start = 0, Count = modifierCount },
                    DurationTicks = 20,
                    PeriodTicks = 2,
                    StackLimit = stackLimit,
                    StackKey = GasStackKeyFields.Definition |
                               GasStackKeyFields.TargetAsc |
                               GasStackKeyFields.SourceAsc,
                    StackPolicy = GasStackPolicy.AggregateBySource,
                    StackPayloadPolicy = GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance,
                    StackLimitApplicationPolicy = stackLimitApplicationPolicy,
                    DurationRefreshPolicy = durationRefreshPolicy,
                    PeriodResetPolicy = periodResetPolicy,
                    ExpiryPolicy = GasExpiryPolicy.Remove,
                    ExpiryPeriodPolicy = GasExpiryPeriodPolicy.Stop,
                    ExpirySameTickPolicy = GasExpirySameTickPolicy.PeriodDueBeforeExpiry,
                    InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                    InhibitedPeriodPolicy = GasInhibitedPeriodPolicy.SkipExecution,
                    MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
                    ExecuteOnApplication = executeOnApplication,
                };
                var modifiers = builder.Allocate(ref root.Modifiers, modifierCount);
                modifiers[0] = new GasModifierDefinitionBlob
                {
                    AttributeLayoutIndex = 0,
                    Operation = GasModifierOperation.Add,
                    EvaluatorProgramRange = new GasCatalogRange { Start = 0, Count = 1 },
                };
                if (invalidSecondModifier)
                {
                    modifiers[1] = new GasModifierDefinitionBlob
                    {
                        AttributeLayoutIndex = 99,
                        Operation = GasModifierOperation.Add,
                        EvaluatorProgramRange = new GasCatalogRange { Start = 1, Count = 1 },
                    };
                }
                var instructions = builder.Allocate(ref root.EvaluatorInstructions, modifierCount);
                instructions[0] = new GasEvaluatorInstructionBlob
                {
                    Opcode = useStackCount
                        ? GasEvaluatorOpcode.PushStackCount
                        : GasEvaluatorOpcode.PushConstant,
                    ConstantValue = useStackCount ? 0f : 5f,
                };
                if (invalidSecondModifier)
                    instructions[1] = new GasEvaluatorInstructionBlob
                    {
                        Opcode = GasEvaluatorOpcode.PushConstant,
                        ConstantValue = 1f,
                    };
                AllocateEmptyArrays(ref root, builder);
                return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 在测试 ASC 的 non-compacting slab 中植入一个可合并的 ActiveEffect 槽。
        /// </summary>
        private static GasSlabHead SeedActiveStack(
            World world,
            Entity asc,
            int stackCount,
            ulong endTick,
            ulong nextPeriodTick)
        {
            var effects = world.EntityManager.GetBuffer<ActiveEffectSlot>(asc);
            effects.EnsureCapacity(4);
            var head = GasSlabHead.CreateEmpty();
            var storage = new GasActiveEffectSlabStorage { Buffer = effects };
            Assert.That(
                GasNonCompactingSlabAllocator.TryAllocate(ref head, ref storage, 4, out var allocation),
                Is.EqualTo(GasSlabStorageFailure.None));
            var owner = new OwnerAscHandle(7, 1);
            effects[allocation.SlotIndex] = new ActiveEffectSlot
            {
                Header = allocation.LiveHeader,
                Handle = new ActiveEffectHandle(1, owner, allocation.SlotIndex, allocation.Generation),
                SourceAsc = owner,
                DefinitionIndex = 0,
                StartTick = 0,
                EndTick = endTick,
                NextPeriodTick = nextPeriodTick,
                StackCount = stackCount,
                State = GasSlotBusinessState.Active,
            };
            return head;
        }

        /// <summary>
        /// 创建单属性、单常量 modifier 的最小 catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCatalog(
            GasEffectLifetimePolicy lifetime,
            float magnitude,
            int durationTicks)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
            builder.Allocate(ref root.AttributeLayout.Entries, 1)[0] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = 1,
                LayoutIndex = 0,
                MinimumValue = -1000f,
                MaximumValue = 1000f,
                ClampMinimum = 1,
                ClampMaximum = 1,
            };
            builder.Allocate(ref root.TagCatalog.Entries, 0);
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
            builder.Allocate(ref root.GameplayEffectIndex, 1)[0] = new GasDefinitionIndexEntry
            {
                DefinitionId = 100,
                DefinitionIndex = 0,
            };
            builder.Allocate(ref root.GameplayEffects, 1)[0] = new GasGameplayEffectDefinitionBlob
            {
                DefinitionId = 100,
                Lifetime = lifetime,
                TargetPolicy = new GasTargetPolicyBlob { Life = GasTargetLifePolicy.AliveOnly },
                ModifierRange = new GasCatalogRange { Start = 0, Count = 1 },
                DurationTicks = durationTicks,
                StackKey = GasStackKeyFields.None,
                StackPolicy = GasStackPolicy.None,
            };
            builder.Allocate(ref root.Modifiers, 1)[0] = new GasModifierDefinitionBlob
            {
                AttributeLayoutIndex = 0,
                Operation = GasModifierOperation.Add,
                EvaluatorProgramRange = new GasCatalogRange { Start = 0, Count = 1 },
            };
            builder.Allocate(ref root.EvaluatorInstructions, 1)[0] = new GasEvaluatorInstructionBlob
            {
                Opcode = GasEvaluatorOpcode.PushConstant,
                ConstantValue = magnitude,
            };
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            builder.Allocate(ref root.AbilityIndex, 0);
            builder.Allocate(ref root.Abilities, 0);
            builder.Allocate(ref root.CaptureDescriptors, 0);
            builder.Allocate(ref root.DirectEffectProgramNodes, 0);
            builder.Allocate(ref root.CueReferences, 0);
            builder.Allocate(ref root.ValueViews, 0);
            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            builder.Allocate(ref root.TargetDataDescriptors, 0);
            builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
            return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
        }

        /// <summary>
        /// 创建第二个 modifier 越界的 catalog，用于验证 preflight 零部分写入。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCatalogWithInvalidSecondModifier(
            GasEffectLifetimePolicy lifetime = GasEffectLifetimePolicy.Instant,
            bool sequentialOverflow = false)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
            // 重新构建完整 Blob，避免在已创建 BlobAsset 上进行任何可变 patch。
            var attributes = builder.Allocate(ref root.AttributeLayout.Entries, 1);
            attributes[0] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = 1,
                LayoutIndex = 0,
                MinimumValue = sequentialOverflow ? -float.MaxValue : -1000f,
                MaximumValue = sequentialOverflow ? float.MaxValue : 1000f,
                ClampMinimum = sequentialOverflow ? (byte)0 : (byte)1,
                ClampMaximum = sequentialOverflow ? (byte)0 : (byte)1,
            };
            builder.Allocate(ref root.TagCatalog.Entries, 0);
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
            builder.Allocate(ref root.GameplayEffectIndex, 1)[0] = new GasDefinitionIndexEntry { DefinitionId = 100 };
            builder.Allocate(ref root.GameplayEffects, 1)[0] = new GasGameplayEffectDefinitionBlob
            {
                DefinitionId = 100,
                Lifetime = lifetime,
                TargetPolicy = new GasTargetPolicyBlob { Life = GasTargetLifePolicy.AliveOnly },
                ModifierRange = new GasCatalogRange { Start = 0, Count = 2 },
                DurationTicks = lifetime == GasEffectLifetimePolicy.Duration ? 3 : 0,
            };
            var modifiers = builder.Allocate(ref root.Modifiers, 2);
            modifiers[0] = new GasModifierDefinitionBlob
            {
                AttributeLayoutIndex = 0,
                Operation = GasModifierOperation.Add,
                EvaluatorProgramRange = new GasCatalogRange { Start = 0, Count = 1 },
            };
            modifiers[1] = new GasModifierDefinitionBlob
            {
                AttributeLayoutIndex = sequentialOverflow ? 0 : 99,
                Operation = sequentialOverflow ? GasModifierOperation.Multiply : GasModifierOperation.Add,
                EvaluatorProgramRange = new GasCatalogRange { Start = 1, Count = 1 },
            };
            var instructions = builder.Allocate(ref root.EvaluatorInstructions, 2);
            instructions[0] = new GasEvaluatorInstructionBlob
            {
                Opcode = GasEvaluatorOpcode.PushConstant,
                ConstantValue = sequentialOverflow ? 1e38f : 5f,
            };
            instructions[1] = new GasEvaluatorInstructionBlob
            {
                Opcode = GasEvaluatorOpcode.PushConstant,
                ConstantValue = sequentialOverflow ? 2f : 1f,
            };
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            builder.Allocate(ref root.AbilityIndex, 0);
            builder.Allocate(ref root.Abilities, 0);
            builder.Allocate(ref root.CaptureDescriptors, 0);
            builder.Allocate(ref root.DirectEffectProgramNodes, 0);
            builder.Allocate(ref root.CueReferences, 0);
            builder.Allocate(ref root.ValueViews, 0);
            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            builder.Allocate(ref root.TargetDataDescriptors, 0);
            builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
            return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
        }

        /// <summary>
        /// 创建包含一个必需 Tag requirement 的 catalog，且 target 不持有该 Tag。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateRequirementCatalog(
            bool invalidImmunity = false)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
            builder.Allocate(ref root.AttributeLayout.Entries, 1)[0] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = 1,
                LayoutIndex = 0,
                MinimumValue = -1000f,
                MaximumValue = 1000f,
            };
            builder.Allocate(ref root.TagCatalog.Entries, 1)[0] = new GasTagCatalogEntryBlob
            {
                TagId = 1,
                TagIndex = 0,
                AncestorIndexRange = new GasCatalogRange { Start = 0, Count = 1 },
            };
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 1)[0] = 0;
            builder.Allocate(ref root.GameplayEffectIndex, 1)[0] = new GasDefinitionIndexEntry { DefinitionId = 100 };
            builder.Allocate(ref root.GameplayEffects, 1)[0] = new GasGameplayEffectDefinitionBlob
            {
                DefinitionId = 100,
                Lifetime = GasEffectLifetimePolicy.Instant,
                TargetPolicy = new GasTargetPolicyBlob { Life = GasTargetLifePolicy.AliveOnly },
                ApplicationRequirementRange = invalidImmunity
                    ? default
                    : new GasCatalogRange { Start = 0, Count = 1 },
                ImmunityRequirementRange = invalidImmunity
                    ? new GasCatalogRange { Start = 1, Count = 1 }
                    : default,
            };
            builder.Allocate(ref root.Requirements, 1)[0] = new GasRequirementBlob
            {
                RequirementId = 1,
                Phase = GasRequirementPhase.Application,
                Match = GasTagRequirementMatch.All,
                TagIndexRange = new GasCatalogRange { Start = 0, Count = 1 },
            };
            builder.Allocate(ref root.RequirementTagIndices, 1)[0] = 0;
            AllocateEmptyArrays(ref root, builder);
            return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
        }

        /// <summary>
        /// 为测试 catalog 分配所有未使用的 BlobArray，确保每个 range 都可安全读取。
        /// </summary>
        private static void AllocateEmptyArrays(
            ref GasDefinitionCatalogBlob root,
            BlobBuilder builder)
        {
            builder.Allocate(ref root.AbilityIndex, 0);
            builder.Allocate(ref root.Abilities, 0);
            builder.Allocate(ref root.CaptureDescriptors, 0);
            builder.Allocate(ref root.Modifiers, 0);
            builder.Allocate(ref root.DirectEffectProgramNodes, 0);
            builder.Allocate(ref root.CueReferences, 0);
            builder.Allocate(ref root.ValueViews, 0);
            builder.Allocate(ref root.EvaluatorInstructions, 0);
            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            builder.Allocate(ref root.TargetDataDescriptors, 0);
            builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
        }
    }
}
