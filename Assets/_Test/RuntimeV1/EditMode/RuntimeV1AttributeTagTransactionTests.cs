using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 Attribute/Tag 单写事务的 clamp、代际、计数与失败原子性。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AttributeTagTransactionTests
    {
        /// <summary>
        /// 验证 Attribute 同时更新 Base/Current、clamp、revision 与 dirty 位，并返回冻结记录。
        /// </summary>
        [Test]
        public void AttributeDelta_ClampRevisionDirty与审计记录一致()
        {
            using var world = new World("Runtime v1 attribute transaction test");
            var asc = world.EntityManager.CreateEntity();
            var entityManager = world.EntityManager;
            entityManager.AddBuffer<AttributeValueSlot>(asc).Add(
                new AttributeValueSlot { Base = 50f, Current = 50f, Revision = 1 });
            entityManager.GetBuffer<AttributeValueSlot>(asc).Add(
                new AttributeValueSlot { Base = 10f, Current = 10f, Revision = 1 });
            entityManager.AddBuffer<AttributeDirtyWord>(asc).Add(default);
            var attributes = entityManager.GetBuffer<AttributeValueSlot>(asc);
            var dirty = entityManager.GetBuffer<AttributeDirtyWord>(asc);
            using var catalog = CreateCatalog();
            ref var root = ref catalog.Value;

            var applied = GasAttributeTransactionUtility.TryApplyDelta(
                ref root,
                0,
                70f,
                -100f,
                attributes,
                dirty,
                out var mutation,
                out var failure);

            Assert.That(applied, Is.True);
            Assert.That(failure, Is.EqualTo(GasAttributeMutationFailure.None));
            Assert.That(attributes[0].Base, Is.EqualTo(100f));
            Assert.That(attributes[0].Current, Is.EqualTo(0f));
            Assert.That(attributes[0].Revision, Is.EqualTo(2));
            Assert.That(dirty[0].Value & 1UL, Is.EqualTo(1UL));
            Assert.That(mutation.UnclampedBase, Is.EqualTo(120f));
            Assert.That(mutation.UnclampedCurrent, Is.EqualTo(-50f));
            Assert.That(mutation.AppliedBase, Is.EqualTo(100f));
            Assert.That(mutation.AppliedCurrent, Is.EqualTo(0f));
        }

        /// <summary>
        /// 验证 Attribute revision 溢出与非有限输入失败时不产生任何 authority 写入。
        /// </summary>
        [Test]
        public void AttributeDelta_失败路径保持原值与Dirty不变()
        {
            using var world = new World("Runtime v1 attribute failure test");
            var asc = world.EntityManager.CreateEntity();
            var entityManager = world.EntityManager;
            entityManager.AddBuffer<AttributeValueSlot>(asc).Add(
                new AttributeValueSlot { Base = 8f, Current = 7f, Revision = uint.MaxValue });
            entityManager.GetBuffer<AttributeValueSlot>(asc).Add(default);
            entityManager.AddBuffer<AttributeDirtyWord>(asc).Add(default);
            var attributes = entityManager.GetBuffer<AttributeValueSlot>(asc);
            var dirty = entityManager.GetBuffer<AttributeDirtyWord>(asc);
            using var catalog = CreateCatalog();
            ref var root = ref catalog.Value;

            var applied = GasAttributeTransactionUtility.TryApplyDelta(
                ref root,
                0,
                1f,
                0f,
                attributes,
                dirty,
                out _,
                out var failure);

            Assert.That(applied, Is.False);
            Assert.That(failure, Is.EqualTo(GasAttributeMutationFailure.RevisionOverflow));
            Assert.That(attributes[0].Base, Is.EqualTo(8f));
            Assert.That(attributes[0].Current, Is.EqualTo(7f));
            Assert.That(attributes[0].Revision, Is.EqualTo(uint.MaxValue));
            Assert.That(dirty[0].Value, Is.Zero);

            applied = GasAttributeTransactionUtility.TryApplyDelta(
                ref root,
                0,
                float.NaN,
                0f,
                attributes,
                dirty,
                out _,
                out failure);
            Assert.That(applied, Is.False);
            Assert.That(failure, Is.EqualTo(GasAttributeMutationFailure.NonFiniteInput));
            Assert.That(dirty[0].Value, Is.Zero);
        }

        /// <summary>
        /// 验证 Tag grant/remove 同步叶 exact、ancestor inclusive 与 presence 派生位图。
        /// </summary>
        [Test]
        public void TagDelta_Ancestor计数与Presence按事务更新()
        {
            using var world = new World("Runtime v1 tag transaction test");
            var asc = world.EntityManager.CreateEntity();
            var entityManager = world.EntityManager;
            entityManager.AddBuffer<TagCountSlot>(asc).Add(default);
            entityManager.GetBuffer<TagCountSlot>(asc).Add(default);
            entityManager.GetBuffer<TagCountSlot>(asc).Add(default);
            entityManager.AddBuffer<TagPresenceWord>(asc).Add(default);
            var counts = entityManager.GetBuffer<TagCountSlot>(asc);
            var presence = entityManager.GetBuffer<TagPresenceWord>(asc);
            using var catalog = CreateCatalog();
            ref var root = ref catalog.Value;

            Assert.That(
                GasTagTransactionUtility.TryApplyDelta(
                    ref root, 1, 1, counts, presence, out var grant, out var grantFailure),
                Is.True);
            Assert.That(grantFailure, Is.EqualTo(GasTagMutationFailure.None));
            Assert.That(counts[1].ExactCount, Is.EqualTo(1));
            Assert.That(counts[1].InclusiveCount, Is.EqualTo(1));
            Assert.That(counts[0].ExactCount, Is.Zero);
            Assert.That(counts[0].InclusiveCount, Is.EqualTo(1));
            Assert.That(presence[0].Value & 0b11UL, Is.EqualTo(0b11UL));
            Assert.That(grant.Changed, Is.EqualTo(1));

            Assert.That(
                GasTagTransactionUtility.TryApplyDelta(
                    ref root, 1, -1, counts, presence, out _, out var removeFailure),
                Is.True);
            Assert.That(removeFailure, Is.EqualTo(GasTagMutationFailure.None));
            Assert.That(counts[0].InclusiveCount, Is.Zero);
            Assert.That(counts[1].ExactCount, Is.Zero);
            Assert.That(presence[0].Value, Is.Zero);

            Assert.That(
                GasTagTransactionUtility.TryApplyDelta(
                    ref root, 1, -1, counts, presence, out _, out var underflow),
                Is.False);
            Assert.That(underflow, Is.EqualTo(GasTagMutationFailure.Underflow));
            Assert.That(counts[0].InclusiveCount, Is.Zero);
            Assert.That(counts[1].ExactCount, Is.Zero);
        }

        /// <summary>
        /// 验证 Tag 溢出在任何 ancestor 写入前失败，且既有计数保持不变。
        /// </summary>
        [Test]
        public void TagDelta_Overflow失败不产生部分Ancestor写入()
        {
            using var world = new World("Runtime v1 tag overflow test");
            var asc = world.EntityManager.CreateEntity();
            var entityManager = world.EntityManager;
            entityManager.AddBuffer<TagCountSlot>(asc).Add(
                new TagCountSlot { InclusiveCount = int.MaxValue });
            entityManager.GetBuffer<TagCountSlot>(asc).Add(
                new TagCountSlot { ExactCount = int.MaxValue, InclusiveCount = int.MaxValue });
            entityManager.GetBuffer<TagCountSlot>(asc).Add(default);
            entityManager.AddBuffer<TagPresenceWord>(asc).Add(default);
            var counts = entityManager.GetBuffer<TagCountSlot>(asc);
            var presence = entityManager.GetBuffer<TagPresenceWord>(asc);
            using var catalog = CreateCatalog();
            ref var root = ref catalog.Value;

            var applied = GasTagTransactionUtility.TryApplyDelta(
                ref root, 1, 1, counts, presence, out _, out var failure);

            Assert.That(applied, Is.False);
            Assert.That(failure, Is.EqualTo(GasTagMutationFailure.Overflow));
            Assert.That(counts[0].InclusiveCount, Is.EqualTo(int.MaxValue));
            Assert.That(counts[1].ExactCount, Is.EqualTo(int.MaxValue));
            Assert.That(counts[1].InclusiveCount, Is.EqualTo(int.MaxValue));
            Assert.That(presence[0].Value, Is.Zero);
        }

        /// <summary>
        /// 创建含两个 Attribute 与三层 Tag ancestor 关系的最小 Catalog Blob。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCatalog()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                var attributes = builder.Allocate(ref root.AttributeLayout.Entries, 2);
                attributes[0] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 1,
                    LayoutIndex = 0,
                    MinimumValue = 0f,
                    MaximumValue = 100f,
                    ClampMinimum = 1,
                    ClampMaximum = 1,
                };
                attributes[1] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 2,
                    LayoutIndex = 1,
                    MinimumValue = 0f,
                    MaximumValue = 1000f,
                    ClampMinimum = 1,
                    ClampMaximum = 0,
                };
                var ancestors = builder.Allocate(ref root.TagCatalog.AncestorIndices, 5);
                ancestors[0] = 0;
                ancestors[1] = 0;
                ancestors[2] = 1;
                ancestors[3] = 0;
                ancestors[4] = 2;
                var tags = builder.Allocate(ref root.TagCatalog.Entries, 3);
                tags[0] = new GasTagCatalogEntryBlob
                {
                    TagId = 10,
                    TagIndex = 0,
                    AncestorIndexRange = new GasCatalogRange { Start = 0, Count = 1 },
                };
                tags[1] = new GasTagCatalogEntryBlob
                {
                    TagId = 20,
                    TagIndex = 1,
                    AncestorIndexRange = new GasCatalogRange { Start = 1, Count = 2 },
                };
                tags[2] = new GasTagCatalogEntryBlob
                {
                    TagId = 30,
                    TagIndex = 2,
                    AncestorIndexRange = new GasCatalogRange { Start = 3, Count = 2 },
                };
                return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }
    }
}
