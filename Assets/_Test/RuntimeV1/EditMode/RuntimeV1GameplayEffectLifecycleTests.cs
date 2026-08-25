using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 target-owned ActiveEffect 的 period claim、同 tick expiry、stack refresh 与 inhibition 语义。
    /// </summary>
    [TestFixture]
    public class RuntimeV1GameplayEffectLifecycleTests
    {
        /// <summary>
        /// 验证同一个 due tick 只 claim 一次并推进到下一个整数 tick。
        /// </summary>
        [Test]
        public void PeriodDue_重复扫描只产生一次claim()
        {
            using var world = new World("Runtime v1 lifecycle period test");
            var entity = CreateEntity(world, 0);
            using var catalog = CreateCatalog(
                GasExpirySameTickPolicy.PeriodDueBeforeExpiry,
                GasExpiryPolicy.Remove,
                GasExpiryPeriodPolicy.Stop,
                5,
                2);
            var head = CreateSlot(world, entity, catalog, 10, 2, 1);

            Assert.That(Process(world, entity, catalog, ref head, 2, out var first), Is.True);
            Assert.That(first.PeriodClaimCount, Is.EqualTo(1));
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0].NextPeriodTick, Is.EqualTo(4UL));
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0].ActiveCycleOrdinal, Is.EqualTo(1U));

            Assert.That(Process(world, entity, catalog, ref head, 2, out var second), Is.True);
            Assert.That(second.PeriodClaimCount, Is.Zero);
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0].ActiveCycleOrdinal, Is.EqualTo(1U));
        }

        /// <summary>
        /// 验证 expiry-before-period 同 tick 直接进入 tombstone，随后显式回收递增 generation。
        /// </summary>
        [Test]
        public void ExpiryBeforePeriod_先移除再允许回收()
        {
            using var world = new World("Runtime v1 lifecycle expiry test");
            var entity = CreateEntity(world, 0);
            using var catalog = CreateCatalog(
                GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                GasExpiryPolicy.Remove,
                GasExpiryPeriodPolicy.Stop,
                5,
                5);
            var head = CreateSlot(world, entity, catalog, 5, 5, 1);

            Assert.That(Process(world, entity, catalog, ref head, 5, out var result), Is.True);
            var effects = world.EntityManager.GetBuffer<ActiveEffectSlot>(entity);
            Assert.That(result.PeriodClaimCount, Is.Zero);
            Assert.That(result.TombstoneCount, Is.EqualTo(1));
            Assert.That(effects[0].Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(effects[0].State, Is.EqualTo(GasSlotBusinessState.Terminal));

            Assert.That(
                GasGameplayEffectLifecycleUtility.TryRecycleTombstones(
                    effects, ref head, 4, out var recycled, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(GasActiveEffectLifecycleFailure.None));
            Assert.That(recycled, Is.EqualTo(1));
            Assert.That(effects[0].Header.StorageState, Is.EqualTo(GasSlabSlotState.Free));
            Assert.That(effects[0].Header.Generation, Is.EqualTo(2U));
        }

        /// <summary>
        /// 验证 period-before-expiry 同 tick 先推进 ordinal，再完成整槽移除。
        /// </summary>
        [Test]
        public void PeriodBeforeExpiry_先claim再移除()
        {
            using var world = new World("Runtime v1 lifecycle ordering test");
            var entity = CreateEntity(world, 0);
            using var catalog = CreateCatalog(
                GasExpirySameTickPolicy.PeriodDueBeforeExpiry,
                GasExpiryPolicy.Remove,
                GasExpiryPeriodPolicy.Stop,
                5,
                5);
            var head = CreateSlot(world, entity, catalog, 5, 5, 1);

            Assert.That(Process(world, entity, catalog, ref head, 5, out var result), Is.True);
            var slot = world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0];
            Assert.That(result.PeriodClaimCount, Is.EqualTo(1));
            Assert.That(result.TombstoneCount, Is.EqualTo(1));
            Assert.That(slot.ActiveCycleOrdinal, Is.EqualTo(1U));
            Assert.That(slot.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
        }

        /// <summary>
        /// 验证 RemoveOneStackAndRefreshDuration 只撤销一层并重置 expiry/period。
        /// </summary>
        [Test]
        public void StackExpiry_移除一层并刷新时序()
        {
            using var world = new World("Runtime v1 lifecycle stack expiry test");
            var entity = CreateEntity(world, 0);
            using var catalog = CreateCatalog(
                GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                GasExpiryPolicy.RemoveOneStackAndRefreshDuration,
                GasExpiryPeriodPolicy.Reset,
                5,
                2);
            var head = CreateSlot(world, entity, catalog, 5, 5, 2);

            Assert.That(Process(world, entity, catalog, ref head, 5, out var result), Is.True);
            var slot = world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0];
            Assert.That(result.StackRefreshCount, Is.EqualTo(1));
            Assert.That(slot.StackCount, Is.EqualTo(1));
            Assert.That(slot.EndTick, Is.EqualTo(10UL));
            Assert.That(slot.NextPeriodTick, Is.EqualTo(7UL));
            Assert.That(slot.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
        }

        /// <summary>
        /// 验证 ongoing requirement 不满足时进入 inhibited，恢复 Tag 后保留同一槽身份重新激活。
        /// </summary>
        [Test]
        public void OngoingRequirement_抑制与恢复保留槽身份()
        {
            using var world = new World("Runtime v1 lifecycle inhibit test");
            var entity = CreateEntity(world, 1);
            using var catalog = CreateCatalog(
                GasExpirySameTickPolicy.PeriodDueBeforeExpiry,
                GasExpiryPolicy.Remove,
                GasExpiryPeriodPolicy.Stop,
                10,
                2,
                withOngoingRequirement: true);
            var head = CreateSlot(world, entity, catalog, 10, 2, 1);
            var original = world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0].Handle;

            Assert.That(Process(world, entity, catalog, ref head, 2, out var inhibited), Is.True);
            Assert.That(inhibited.InhibitedCount, Is.EqualTo(1));
            Assert.That(inhibited.PeriodClaimCount, Is.Zero);
            Assert.That(world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0].Inhibited, Is.EqualTo(1));

            var tags = world.EntityManager.GetBuffer<TagCountSlot>(entity);
            tags[0] = new TagCountSlot
            {
                ExactCount = 1,
                InclusiveCount = 1,
            };
            Assert.That(Process(world, entity, catalog, ref head, 3, out var resumed), Is.True);
            var slot = world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0];
            Assert.That(resumed.ReactivatedCount, Is.EqualTo(1));
            Assert.That(slot.Inhibited, Is.Zero);
            Assert.That(slot.Handle.Equals(original), Is.True);
        }

        /// <summary>
        /// 验证 period tick 回绕在预检阶段拒绝且不修改 slot/head。
        /// </summary>
        [Test]
        public void PeriodOverflow_预检失败时不写入()
        {
            using var world = new World("Runtime v1 lifecycle overflow test");
            var entity = CreateEntity(world, 0);
            using var catalog = CreateCatalog(
                GasExpirySameTickPolicy.PeriodDueBeforeExpiry,
                GasExpiryPolicy.Remove,
                GasExpiryPeriodPolicy.Stop,
                10,
                2);
            var head = CreateSlot(world, entity, catalog, ulong.MaxValue, ulong.MaxValue, 1);
            var before = world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0];

            Assert.That(Process(world, entity, catalog, ref head, ulong.MaxValue, out var result), Is.False);
            var after = world.EntityManager.GetBuffer<ActiveEffectSlot>(entity)[0];
            Assert.That(result.Failure, Is.EqualTo(GasActiveEffectLifecycleFailure.InvalidTiming));
            Assert.That(after.NextPeriodTick, Is.EqualTo(before.NextPeriodTick));
            Assert.That(after.EndTick, Is.EqualTo(before.EndTick));
            Assert.That(head.FreeCount, Is.EqualTo(0));
        }

        /// <summary>
        /// 创建只含 ActiveEffect 与 Tag authority buffer 的测试实体。
        /// </summary>
        private static Entity CreateEntity(World world, int tagCount)
        {
            var entity = world.EntityManager.CreateEntity();
            var effects = world.EntityManager.AddBuffer<ActiveEffectSlot>(entity);
            effects.EnsureCapacity(4);
            var tags = world.EntityManager.AddBuffer<TagCountSlot>(entity);
            tags.ResizeUninitialized(tagCount);
            return entity;
        }

        /// <summary>
        /// 在 non-compacting slab 中创建一个带稳定 handle 的 live ActiveEffect。
        /// </summary>
        private static GasSlabHead CreateSlot(
            World world,
            Entity entity,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog,
            ulong endTick,
            ulong nextPeriodTick,
            int stackCount)
        {
            var effects = world.EntityManager.GetBuffer<ActiveEffectSlot>(entity);
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
        /// 调用 lifecycle utility 并统一传入当前 simulation epoch。
        /// </summary>
        private static bool Process(
            World world,
            Entity entity,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog,
            ref GasSlabHead head,
            ulong candidateTick,
            out GasActiveEffectLifecycleResult result)
        {
            ref var root = ref catalog.Value;
            return GasGameplayEffectLifecycleUtility.TryProcessDue(
                ref root,
                1,
                candidateTick,
                world.EntityManager.GetBuffer<ActiveEffectSlot>(entity),
                world.EntityManager.GetBuffer<TagCountSlot>(entity),
                ref head,
                4,
                out result);
        }

        /// <summary>
        /// 构建包含 duration/period/expiry 与可选 ongoing requirement 的最小 catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCatalog(
            GasExpirySameTickPolicy sameTickPolicy,
            GasExpiryPolicy expiryPolicy,
            GasExpiryPeriodPolicy expiryPeriodPolicy,
            int durationTicks,
            int periodTicks,
            bool withOngoingRequirement = false)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
            builder.Allocate(ref root.AttributeLayout.Entries, 0);
            var tagEntries = builder.Allocate(
                ref root.TagCatalog.Entries, withOngoingRequirement ? 1 : 0);
            var ancestorIndices = builder.Allocate(
                ref root.TagCatalog.AncestorIndices, withOngoingRequirement ? 1 : 0);
            if (withOngoingRequirement)
            {
                tagEntries[0] = new GasTagCatalogEntryBlob
                {
                    TagId = 1,
                    TagIndex = 0,
                    AncestorIndexRange = new GasCatalogRange { Start = 0, Count = 1 },
                };
                ancestorIndices[0] = 0;
            }
            builder.Allocate(ref root.GameplayEffectIndex, 1)[0] = new GasDefinitionIndexEntry
            {
                DefinitionId = 100,
                DefinitionIndex = 0,
            };
            builder.Allocate(ref root.GameplayEffects, 1)[0] = new GasGameplayEffectDefinitionBlob
            {
                DefinitionId = 100,
                Lifetime = GasEffectLifetimePolicy.Duration,
                TargetPolicy = new GasTargetPolicyBlob { Life = GasTargetLifePolicy.AliveOnly },
                OngoingRequirementRange = withOngoingRequirement
                    ? new GasCatalogRange { Start = 0, Count = 1 }
                    : default,
                DurationTicks = durationTicks,
                PeriodTicks = periodTicks,
                StackLimit = 3,
                StackKey = GasStackKeyFields.Definition |
                           GasStackKeyFields.TargetAsc |
                           GasStackKeyFields.SourceAsc,
                StackPolicy = GasStackPolicy.AggregateBySource,
                StackPayloadPolicy = GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance,
                StackLimitApplicationPolicy = GasStackLimitApplicationPolicy.RejectAtLimit,
                DurationRefreshPolicy = GasDurationRefreshPolicy.Never,
                PeriodResetPolicy = GasPeriodResetPolicy.Never,
                ExpiryPolicy = expiryPolicy,
                ExpiryPeriodPolicy = expiryPeriodPolicy,
                ExpirySameTickPolicy = sameTickPolicy,
                InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                InhibitedPeriodPolicy = GasInhibitedPeriodPolicy.SkipExecution,
                MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
            };
            var requirements = builder.Allocate(
                ref root.Requirements, withOngoingRequirement ? 1 : 0);
            var requirementTags = builder.Allocate(
                ref root.RequirementTagIndices, withOngoingRequirement ? 1 : 0);
            if (withOngoingRequirement)
            {
                requirements[0] = new GasRequirementBlob
                {
                    RequirementId = 1,
                    Phase = GasRequirementPhase.Ongoing,
                    Match = GasTagRequirementMatch.All,
                    TagIndexRange = new GasCatalogRange { Start = 0, Count = 1 },
                };
                requirementTags[0] = 0;
            }
            AllocateEmptyArrays(ref root, builder);
            return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
        }

        /// <summary>
        /// 为 lifecycle catalog 补齐未使用的 BlobArray，保持结构可安全读取。
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
